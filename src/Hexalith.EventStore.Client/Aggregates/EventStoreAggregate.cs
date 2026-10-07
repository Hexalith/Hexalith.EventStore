
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.Client.Aggregates;
/// <summary>
/// Abstract base class for event-sourced aggregates. Provides reflection-based command dispatch
/// and state rehydration so that concrete aggregates only declare typed Handle and Apply methods.
/// </summary>
/// <typeparam name="TState">The aggregate state type. Must be a reference type with a parameterless constructor.</typeparam>
public abstract class EventStoreAggregate<TState> : IDomainProcessor, IAggregateReplay, IAsyncDomainProcessor, IAsyncAggregateReplay, IAdmittedLegacyAggregateReplay
    where TState : class, new() {
    private static readonly ConcurrentDictionary<Type, AggregateCommandDispatchMetadata> _metadataCache = new();

    /// <summary>Gets an optional owner declaration that detaches known typed command snapshots.</summary>
    /// <remarks>Undeclared legacy state retains its existing typed-reference behavior.</remarks>
    protected virtual DetachedStateCapture<TState>? SnapshotCapture => null;

    /// <summary>
    /// Called once during cascade configuration resolution to allow subclasses to set per-domain options imperatively (Layer 3).
    /// The default implementation is a no-op. Override this method to customize domain resource names.
    /// </summary>
    /// <param name="options">The domain options to configure. Set non-null values to override convention defaults.</param>
    /// <remarks>
    /// This method is called during <c>UseEventStore()</c> cascade resolution, NOT during command processing.
    /// It is invoked via <c>Activator.CreateInstance()</c> — the aggregate must have a parameterless constructor.
    /// </remarks>
    protected virtual void OnConfiguring(EventStoreDomainOptions options) {
        // No-op by default. Subclasses override to set per-domain options.
    }

    /// <summary>
    /// Internal entry point for the cascade resolver to invoke <see cref="OnConfiguring"/>.
    /// </summary>
    /// <param name="options">The domain options to configure.</param>
    internal void InvokeOnConfiguring(EventStoreDomainOptions options) => OnConfiguring(options);

    /// <inheritdoc/>
    public bool CanReplayAggregateType(string aggregateType) {
        if (string.IsNullOrWhiteSpace(aggregateType)) {
            return true;
        }

        string runtimeName = GetType().Name;
        string conventionalName = runtimeName.EndsWith("Aggregate", StringComparison.Ordinal)
            ? runtimeName[..^"Aggregate".Length]
            : runtimeName;
        return string.Equals(aggregateType, runtimeName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(aggregateType, conventionalName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public AggregateReconstructionResult Replay(AggregateReconstructionRequest request)
        => AggregateReplayer.Replay<TState>(request);

    /// <inheritdoc/>
    /// <remarks>Synchronous Apply observes cancellation before and after each call.</remarks>
    public Task<AggregateReconstructionResult> ReplayAsync(AggregateReconstructionRequest request, CancellationToken cancellationToken)
        => Task.FromResult(AggregateReplayer.Replay<TState>(request, cancellationToken));

    /// <inheritdoc/>
    bool IAdmittedLegacyAggregateReplay.CanReplayAdmitted(bool asynchronous) {
        Type replayInterface = asynchronous ? typeof(IAsyncAggregateReplay) : typeof(IAggregateReplay);
        string methodName = asynchronous ? nameof(IAsyncAggregateReplay.ReplayAsync) : nameof(IAggregateReplay.Replay);
        InterfaceMapping mapping = GetType().GetInterfaceMap(replayInterface);
        for (int index = 0; index < mapping.InterfaceMethods.Length; index++) {
            if (mapping.InterfaceMethods[index].Name == methodName) {
                return mapping.TargetMethods[index].DeclaringType == typeof(EventStoreAggregate<TState>);
            }
        }

        return false;
    }

    /// <inheritdoc/>
    AggregateReconstructionResult IAdmittedLegacyAggregateReplay.ReplayAdmitted(
        AggregateReconstructionRequest request, LegacyReplayInput input, CancellationToken cancellationToken)
        => AggregateReplayer.ReplayAdmitted<TState>(request, input, cancellationToken);

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        => ProcessAsync(command, currentState, CancellationToken.None);

    /// <inheritdoc/>
    /// <remarks>Legacy Handle calls observe cancellation at their boundaries; an already-running Handle cannot be interrupted.</remarks>
    public async Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        AggregateCommandDispatchMetadata metadata = GetOrBuildMetadata();
        DetachedStateCapture<TState>? snapshotCapture;
        try { snapshotCapture = SnapshotCapture; }
        catch (Exception) when (cancellationToken.IsCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        TState? state = DomainProcessorStateRehydrator.RehydrateState<TState>(currentState, metadata.ApplyMethods, cancellationToken, snapshotCapture);

        bool terminated = state is ITerminatable { IsTerminated: true };
        cancellationToken.ThrowIfCancellationRequested();
        if (terminated) {
            return DomainResult.Rejection(new IRejectionEvent[] {
                new AggregateTerminated(AggregateType: GetType().Name, AggregateId: command.AggregateId),
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        DomainResult result = await DispatchCommandAsync(command, state, metadata, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private AggregateCommandDispatchMetadata GetOrBuildMetadata() =>
        _metadataCache.GetOrAdd(GetType(), static aggregateType => {
            Dictionary<string, AggregateCommandHandleMethod> handleMethods = DiscoverHandleMethods(aggregateType);
            ApplyMethodTable applyMethods = DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState));
            return new AggregateCommandDispatchMetadata(handleMethods, applyMethods);
        });

    private static Dictionary<string, AggregateCommandHandleMethod> DiscoverHandleMethods(Type aggregateType) {
        var methods = new Dictionary<string, AggregateCommandHandleMethod>(StringComparer.Ordinal);

        foreach (MethodInfo method in aggregateType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            if (!method.Name.Equals("Handle", StringComparison.Ordinal)) {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length is < 2 or > 3) {
                continue;
            }

            Type commandType = parameters[0].ParameterType;
            Type stateParamType = parameters[1].ParameterType;

            // Verify second parameter is TState? (nullable TState)
            Type expectedStateType = typeof(TState);
            if (stateParamType != expectedStateType) {
                continue;
            }

            // If 3 parameters, verify the third is CommandEnvelope
            bool hasEnvelope = parameters.Length == 3
                && parameters[2].ParameterType == typeof(CommandEnvelope);
            if (parameters.Length == 3 && !hasEnvelope) {
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
            if (methods.ContainsKey(commandTypeName)) {
                throw new InvalidOperationException(
                    $"Multiple Handle methods found for command type '{commandTypeName}' on aggregate '{aggregateType.Name}'. "
                    + "Declare exactly one Handle overload per command type.");
            }

            var handleInfo = new AggregateCommandHandleMethod(method, commandType, isAsync, method.IsStatic, hasEnvelope);
            methods[commandTypeName] = handleInfo;

            // Also register the kebab-case ICommandContract.CommandType discriminator (e.g. "increment-counter")
            // as an alias for the same Handle method. Generated REST command controllers submit envelopes keyed
            // by ICommandContract.CommandType, whereas legacy submissions use the CLR short type name
            // (e.g. "IncrementCounter"). Both must dispatch to the same Handle overload; legacy lookup is unchanged.
            if (TryGetContractCommandType(commandType, out string? contractCommandType)
                && !string.Equals(contractCommandType, commandTypeName, StringComparison.Ordinal)) {
                if (methods.TryGetValue(contractCommandType!, out AggregateCommandHandleMethod? existing)
                    && !ReferenceEquals(existing, handleInfo)) {
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

    private async Task<DomainResult> DispatchCommandAsync(CommandEnvelope command, TState? state, AggregateCommandDispatchMetadata metadata,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        // The HandleMethods dictionary is keyed by short type name (e.g., "IncrementCounter"),
        // but command.CommandType may be assembly-qualified (e.g., "Namespace.IncrementCounter, Assembly").
        // Extract the short name for lookup.
        string lookupKey = ExtractShortTypeName(command.CommandType);
        if (!metadata.HandleMethods.TryGetValue(lookupKey, out AggregateCommandHandleMethod? handleInfo)) {
            throw new InvalidOperationException(
                $"No Handle method found for command type '{command.CommandType}' on aggregate '{GetType().Name}'.");
        }

        object commandPayload;
        try {
            commandPayload = command.Payload.Length == 0
                ? throw new InvalidOperationException(
                    $"Command '{command.CommandType}' has an empty payload. Expected valid JSON for {handleInfo.CommandType.Name}.")
                : JsonSerializer.Deserialize(command.Payload, handleInfo.CommandType, EventStorePayloadSerialization.Options)
                  ?? throw new InvalidOperationException(
                      $"Failed to deserialize payload for command '{command.CommandType}' to {handleInfo.CommandType.Name}.");
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        object?[] args = handleInfo.HasEnvelope
            ? [commandPayload, state, command]
            : [commandPayload, state];
        cancellationToken.ThrowIfCancellationRequested();
        object? result;
        try {
            result = handleInfo.Method.Invoke(handleInfo.IsStatic ? null : this, args);
        }
        catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        DomainResult completedResult = result switch {
            Task<DomainResult> asyncResult => await asyncResult.ConfigureAwait(false),
            Task asyncResult when handleInfo.IsAsync => await GetAsyncDomainResultAsync(asyncResult).ConfigureAwait(false),
            DomainResult syncResult => syncResult,
            _ => throw new InvalidOperationException(
                $"Handle method for '{command.CommandType}' returned unexpected type '{result?.GetType().Name ?? "null"}'."),
        };
        cancellationToken.ThrowIfCancellationRequested();
        return completedResult;
    }

    private static async Task<DomainResult> GetAsyncDomainResultAsync(Task asyncResult) {
        await asyncResult.ConfigureAwait(false);
        return asyncResult.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(asyncResult) as DomainResult
            ?? throw new InvalidOperationException(
                $"Handle method returned unexpected async result type '{asyncResult.GetType().Name}'.");
    }

    /// <summary>
    /// Extracts the short type name from a potentially assembly-qualified type string.
    /// "Namespace.TypeName, Assembly" → "TypeName", "Namespace.TypeName" → "TypeName", "TypeName" → "TypeName".
    /// </summary>
    private static string ExtractShortTypeName(string commandType) {
        // Strip assembly qualification: "Namespace.Type, Assembly" → "Namespace.Type"
        int commaIndex = commandType.IndexOf(',', StringComparison.Ordinal);
        string fullName = commaIndex >= 0 ? commandType[..commaIndex] : commandType;

        // Strip namespace: "Namespace.Type" → "Type"
        int dotIndex = fullName.LastIndexOf('.');
        return dotIndex >= 0 ? fullName[(dotIndex + 1)..] : fullName;
    }

}
