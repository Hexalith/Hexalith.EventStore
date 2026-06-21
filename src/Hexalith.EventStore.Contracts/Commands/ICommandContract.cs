namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Marks a command message as exposable through a generated REST endpoint and defines its
/// mandatory routing metadata as typed members.
/// Mirrors <see cref="Hexalith.EventStore.Contracts.Queries.IQueryContract"/> for commands.
/// </summary>
public interface ICommandContract {
    /// <summary>
    /// Gets the command type discriminator used for routing and the command envelope.
    /// Must be kebab-case, no colons (reserved as actor ID separator).
    /// Example: "create-counter".
    /// </summary>
    static abstract string CommandType { get; }

    /// <summary>
    /// Gets the owning domain name (kebab-case).
    /// Example: "counter".
    /// </summary>
    static abstract string Domain { get; }

    /// <summary>
    /// Gets the aggregate id this command targets, used for routing and the command envelope.
    /// </summary>
    string AggregateId { get; }
}
