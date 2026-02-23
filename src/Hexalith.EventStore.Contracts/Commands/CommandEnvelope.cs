
using Hexalith.EventStore.Contracts.Identity;

using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Commands;
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
[DataContract]
public record CommandEnvelope(
    [property: DataMember] string TenantId,
    [property: DataMember] string Domain,
    [property: DataMember] string AggregateId,
    string CommandType,
    byte[] Payload,
    string CorrelationId,
    [property: DataMember] string? CausationId,
    string UserId,
    Dictionary<string, string>? Extensions) {
    // Eagerly validate identity components during normal construction.
    // DataContractSerializer bypasses constructors, so deserialized instances skip this —
    // which is correct since the data was already validated when originally constructed.
    private readonly bool _identityValidated = ValidateIdentityComponents(TenantId, Domain, AggregateId);

    /// <summary>Gets the computed aggregate identity derived from TenantId, Domain, and AggregateId.</summary>
    public AggregateIdentity AggregateIdentity => new(TenantId, Domain, AggregateId);

    private static bool ValidateIdentityComponents(string tenantId, string domain, string aggregateId) {
        _ = new AggregateIdentity(tenantId, domain, aggregateId);
        return true;
    }

    /// <summary>Gets the fully qualified command type name.</summary>
    [DataMember]
    public string CommandType { get; init; } = !string.IsNullOrWhiteSpace(CommandType)
        ? CommandType
        : throw new ArgumentException("CommandType cannot be null, empty, or whitespace.", nameof(CommandType));

    /// <summary>Gets the serialized command payload as raw bytes.</summary>
    [DataMember]
    public byte[] Payload { get; init; } = Payload ?? throw new ArgumentNullException(nameof(Payload));

    /// <summary>Gets the correlation identifier for request tracing.</summary>
    [DataMember]
    public string CorrelationId { get; init; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>Gets the user who initiated the command.</summary>
    [DataMember]
    public string UserId { get; init; } = !string.IsNullOrWhiteSpace(UserId)
        ? UserId
        : throw new ArgumentException("UserId cannot be null, empty, or whitespace.", nameof(UserId));

    /// <summary>Gets the extension metadata (null if no extensions). Defensively copied to preserve immutability.</summary>
    [DataMember]
    public Dictionary<string, string>? Extensions { get; init; } = Extensions is not null
        ? new Dictionary<string, string>(Extensions)
        : null;

    /// <summary>
    /// Returns a string representation with Payload redacted (SEC-5, Rule #5).
    /// Framework-level enforcement: even if a developer logs the entire CommandEnvelope,
    /// the payload is never exposed.
    /// </summary>
    public override string ToString() {
        string extensionKeys = Extensions is not null
            ? string.Join(", ", Extensions.Keys)
            : "none";
        return $"CommandEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, CommandType = {CommandType}, CorrelationId = {CorrelationId}, CausationId = {CausationId}, UserId = {UserId}, Payload = [REDACTED], Extensions = [{extensionKeys}] }}";
    }
}
