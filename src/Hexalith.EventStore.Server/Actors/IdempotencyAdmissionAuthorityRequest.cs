using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests durable validation of one signed execution context at its authority actor.</summary>
/// <param name="FencingToken">The signed non-zero fence.</param>
/// <param name="DigestKeyVersion">The signed digest-key version.</param>
/// <param name="ExecutionMessageId">The signed stable execution identity.</param>
/// <param name="ExecutionCorrelationId">The signed stable checkpoint identity.</param>
/// <param name="Purpose">The protected operation being authorized.</param>
[DataContract]
public sealed record IdempotencyAdmissionAuthorityRequest(
    [property: DataMember] long FencingToken,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string ExecutionMessageId,
    [property: DataMember] string ExecutionCorrelationId,
    [property: DataMember] IdempotencyExecutionPurpose Purpose);
