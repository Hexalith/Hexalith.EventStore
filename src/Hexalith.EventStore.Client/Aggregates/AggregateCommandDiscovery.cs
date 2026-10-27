using System.Reflection;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Shared effective Handle discovery for dispatch and offline verification.</summary>
internal static class AggregateCommandDiscovery
{
    /// <summary>Discovers the exact alias table used by aggregate command dispatch.</summary>
    internal static Dictionary<string, AggregateCommandHandleMethod> Discover(Type aggregateType, Type stateType) {
        var methods = new Dictionary<string, AggregateCommandHandleMethod>(StringComparer.Ordinal);

        foreach (MethodInfo method in aggregateType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            if (!method.Name.Equals("Handle", StringComparison.Ordinal)) {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length is < 2 or > 4) {
                continue;
            }

            Type commandType = parameters[0].ParameterType;
            Type stateParamType = parameters[1].ParameterType;

            // Verify second parameter is TState? (nullable TState)
            if (stateParamType != stateType) {
                continue;
            }

            bool hasEnvelope = parameters.Length >= 3 && parameters[2].ParameterType == typeof(CommandEnvelope);
            bool hasCancellationToken = parameters[^1].ParameterType == typeof(CancellationToken)
                && parameters.Length >= 3;
            if ((parameters.Length == 3 && !hasEnvelope && !hasCancellationToken)
                || (parameters.Length == 4 && (!hasEnvelope || !hasCancellationToken))) {
                continue;
            }

            bool isAsync = method.ReturnType.IsGenericType
                && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                && typeof(DomainResult).IsAssignableFrom(method.ReturnType.GenericTypeArguments[0]);
            bool isSync = typeof(DomainResult).IsAssignableFrom(method.ReturnType);
            if (!isAsync && !isSync) {
                continue;
            }

            string commandTypeName = commandType.Name;
            if (methods.TryGetValue(commandTypeName, out AggregateCommandHandleMethod? existingHandle)) {
                if (existingHandle.CommandType != commandType) {
                    throw new InvalidOperationException(
                        $"Multiple Handle methods found for distinct command types named '{commandTypeName}' on aggregate '{aggregateType.Name}'.");
                }
                if (existingHandle.HasCancellationToken && !hasCancellationToken) { continue; }
                if (existingHandle.HasCancellationToken == hasCancellationToken) {
                    throw new InvalidOperationException(
                        $"Multiple Handle methods found for command type '{commandTypeName}' on aggregate '{aggregateType.Name}'. "
                        + "Declare one Handle overload per command type and cancellation shape.");
                }
            }

            var handleInfo = new AggregateCommandHandleMethod(method, commandType, isAsync, method.IsStatic, hasEnvelope, hasCancellationToken);
            methods[commandTypeName] = handleInfo;

            // Also register the kebab-case ICommandContract.CommandType discriminator (e.g. "increment-counter")
            // as an alias for the same Handle method. Generated REST command controllers submit envelopes keyed
            // by ICommandContract.CommandType, whereas legacy submissions use the CLR short type name
            // (e.g. "IncrementCounter"). Both must dispatch to the same Handle overload; legacy lookup is unchanged.
            if (TryGetContractCommandType(commandType, out string? contractCommandType)
                && !string.Equals(contractCommandType, commandTypeName, StringComparison.Ordinal)) {
                if (methods.TryGetValue(contractCommandType!, out AggregateCommandHandleMethod? existing)
                    && !ReferenceEquals(existing, handleInfo)
                    && (existing.CommandType != commandType || existing.HasCancellationToken == hasCancellationToken)) {
                    throw new InvalidOperationException(
                        $"Command contract type '{contractCommandType}' declared by '{commandTypeName}' collides with another "
                        + $"Handle method on aggregate '{aggregateType.Name}'. ICommandContract.CommandType values must be unique per aggregate.");
                }

                methods[contractCommandType!] = handleInfo;
            }
        }

        return methods;
    }

    /// <summary>
    /// Attempts to read and validate the kebab-case <see cref="ICommandContract.CommandType"/> discriminator declared
    /// by a command type that implements <see cref="ICommandContract"/>. Returns <see langword="false"/> for commands
    /// that do not implement the contract, so they keep their CLR short-name dispatch only.
    /// </summary>
    private static bool TryGetContractCommandType(Type commandType, out string? commandTypeValue) {
        commandTypeValue = null;
        if (!typeof(ICommandContract).IsAssignableFrom(commandType)) {
            return false;
        }

        PropertyInfo? property = commandType.GetProperty(
            nameof(ICommandContract.CommandType),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (property is null) {
            throw new InvalidOperationException(
                $"Command contract '{commandType.FullName}' does not declare a public static {nameof(ICommandContract.CommandType)} property.");
        }

        commandTypeValue = property.GetValue(null) as string
            ?? throw new InvalidOperationException(
                $"Command contract '{commandType.FullName}' returned a null {nameof(ICommandContract.CommandType)} value.");
        NamingConventionEngine.ValidateKebabCase(commandTypeValue, nameof(ICommandContract.CommandType));
        return true;
    }

}
