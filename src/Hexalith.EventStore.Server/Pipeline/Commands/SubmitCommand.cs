
using MediatR;

namespace Hexalith.EventStore.Server.Pipeline.Commands;
/// <summary>
/// MediatR command for submitting a domain command through the pipeline.
/// </summary>
public record SubmitCommand(
    string MessageId,
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    string CorrelationId,
    string UserId,
    Dictionary<string, string>? Extensions = null,
    bool IsGlobalAdmin = false,
    string? IdempotencyKey = null) : IRequest<SubmitCommandResult>
{
    /// <summary>Gateway-only authenticated human claims; excluded from command persistence and client JSON.</summary>
    [System.Runtime.Serialization.IgnoreDataMember]
    [System.Text.Json.Serialization.JsonIgnore]
    public HumanAuthoritySource? HumanAuthority { get; init; }

    /// <summary>Ephemeral signed human receipt, assigned only after idempotency admission fixes the execution identity.</summary>
    [System.Runtime.Serialization.IgnoreDataMember]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? HumanAuthorityReceipt { get; init; }

    /// <summary>Redacts gateway-only claims and payload from diagnostics.</summary>
    public override string ToString() => $"SubmitCommand {{ MessageId = {MessageId}, Tenant = {Tenant}, Domain = {Domain}, AggregateId = {AggregateId}, CommandType = {CommandType}, Payload = [REDACTED], HumanAuthority = [REDACTED] }}";
}

/// <summary>
/// Result of processing a <see cref="SubmitCommand"/>.
/// </summary>
public record SubmitCommandResult(
    string CorrelationId,
    string? ResultPayload = null,
    string? MessageId = null);
