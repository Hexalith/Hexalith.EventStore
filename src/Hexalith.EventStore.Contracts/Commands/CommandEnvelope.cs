namespace Hexalith.EventStore.Contracts.Commands;

using System.Collections.ObjectModel;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Command payload envelope containing all command fields and a computed aggregate identity.
/// Validates required fields eagerly at construction time.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The fully qualified command type name.</param>
/// <param name="Payload">The serialized command payload as raw bytes.</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="CausationId">The optional causation identifier.</param>
/// <param name="UserId">The user who initiated the command.</param>
/// <param name="Extensions">Optional extension metadata (null if no extensions).</param>
public record CommandEnvelope(
    string TenantId,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    string CorrelationId,
    string? CausationId,
    string UserId,
    IReadOnlyDictionary<string, string>? Extensions)
{
    /// <summary>Gets the computed aggregate identity derived from TenantId, Domain, and AggregateId. Eagerly validated at construction.</summary>
    public AggregateIdentity AggregateIdentity { get; } = new(TenantId, Domain, AggregateId);

    /// <summary>Gets the fully qualified command type name.</summary>
    public string CommandType { get; } = !string.IsNullOrWhiteSpace(CommandType)
        ? CommandType
        : throw new ArgumentException("CommandType cannot be null, empty, or whitespace.", nameof(CommandType));

    /// <summary>Gets the serialized command payload as raw bytes.</summary>
    public byte[] Payload { get; } = Payload ?? throw new ArgumentNullException(nameof(Payload));

    /// <summary>Gets the correlation identifier for request tracing.</summary>
    public string CorrelationId { get; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>Gets the user who initiated the command.</summary>
    public string UserId { get; } = !string.IsNullOrWhiteSpace(UserId)
        ? UserId
        : throw new ArgumentException("UserId cannot be null, empty, or whitespace.", nameof(UserId));

    /// <summary>Gets the extension metadata (null if no extensions). Defensively copied to preserve immutability.</summary>
    public IReadOnlyDictionary<string, string>? Extensions { get; } = Extensions is not null
        ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Extensions))
        : null;

    /// <summary>
    /// Returns a string representation with Payload redacted (SEC-5, Rule #5).
    /// Framework-level enforcement: even if a developer logs the entire CommandEnvelope,
    /// the payload is never exposed.
    /// </summary>
    public override string ToString()
    {
        string extensionKeys = Extensions is not null
            ? string.Join(", ", Extensions.Keys)
            : "none";
        return $"CommandEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, CommandType = {CommandType}, CorrelationId = {CorrelationId}, CausationId = {CausationId}, UserId = {UserId}, Payload = [REDACTED], Extensions = [{extensionKeys}] }}";
    }
}
