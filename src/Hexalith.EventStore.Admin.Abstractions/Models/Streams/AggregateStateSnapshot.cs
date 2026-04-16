namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// A snapshot of an aggregate's state at a specific sequence position.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="SequenceNumber">The sequence number at which the snapshot was taken.</param>
/// <param name="Timestamp">When the snapshot was taken.</param>
/// <param name="StateJson">The serialized aggregate state as opaque JSON.</param>
public record AggregateStateSnapshot(
    string TenantId,
    string Domain,
    string AggregateId,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string StateJson) {
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = !string.IsNullOrWhiteSpace(AggregateId)
        ? AggregateId
        : throw new ArgumentException("AggregateId cannot be null, empty, or whitespace.", nameof(AggregateId));

    /// <summary>Gets the serialized aggregate state as opaque JSON.</summary>
    public string StateJson { get; } = StateJson ?? throw new ArgumentNullException(nameof(StateJson));

    /// <summary>
    /// Returns a string representation with StateJson redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"AggregateStateSnapshot {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, SequenceNumber = {SequenceNumber}, Timestamp = {Timestamp}, StateJson = [REDACTED] }}";
}
