namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Immutable container for resolved command contract metadata.
/// Produced by command contract resolvers from <see cref="ICommandContract"/> implementations.
/// </summary>
/// <param name="CommandType">The command type name (kebab-case routing key).</param>
/// <param name="Domain">The owning domain name.</param>
public record CommandContractMetadata(
    string CommandType,
    string Domain);
