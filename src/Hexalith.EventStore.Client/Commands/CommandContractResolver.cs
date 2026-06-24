using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Client.Commands;

/// <summary>
/// Resolves and validates command contract metadata from <see cref="ICommandContract"/> implementations.
/// Results are cached per type using a thread-safe <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public static class CommandContractResolver {
    private static readonly ConcurrentDictionary<Type, CommandContractMetadata> _cache = new();

    /// <summary>
    /// Resolves and validates the command contract metadata for the specified command type.
    /// Reads static abstract members, validates all fields against kebab-case rules,
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TCommand">The command type implementing <see cref="ICommandContract"/>.</typeparam>
    /// <returns>The validated <see cref="CommandContractMetadata"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a static member returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when a static member value is invalid.</exception>
    public static CommandContractMetadata Resolve<TCommand>()
        where TCommand : ICommandContract => _cache.GetOrAdd(typeof(TCommand), static _ => {
            string commandType = TCommand.CommandType;
            string domain = TCommand.Domain;

            NamingConventionEngine.ValidateKebabCase(commandType, "CommandType");
            NamingConventionEngine.ValidateKebabCase(domain, "Domain");
            RejectColon(commandType, "CommandType");
            RejectColon(domain, "Domain");

            return new CommandContractMetadata(commandType, domain);
        });

    /// <summary>
    /// Clears the resolver cache. Intended for test isolation.
    /// </summary>
    internal static void ClearCache() => _cache.Clear();

    private static void RejectColon(string value, string parameterName) {
        if (value.Contains(':')) {
            throw new ArgumentException(
                $"{parameterName} '{value}' cannot contain colons (reserved as actor ID separator).",
                parameterName);
        }
    }
}
