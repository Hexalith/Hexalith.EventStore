using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Internal signed capability binding one current admission fence to one command boundary.</summary>
/// <param name="SchemaVersion">The execution-context schema version.</param>
/// <param name="AdmissionActorId">The protected admission authority.</param>
/// <param name="FencingToken">The current non-zero persisted fence.</param>
/// <param name="DigestKeyVersion">The signing digest-key version.</param>
/// <param name="MessageId">The stable command identity.</param>
/// <param name="CorrelationId">The stable aggregate-checkpoint correlation identity.</param>
/// <param name="Tenant">The managed tenant.</param>
/// <param name="Domain">The bounded context.</param>
/// <param name="AggregateId">The aggregate target.</param>
/// <param name="CommandType">The authorized operation type.</param>
/// <param name="Proof">The domain-separated support-safe capability proof.</param>
[DataContract]
public sealed record IdempotencyExecutionContext(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string AdmissionActorId,
    [property: DataMember] long FencingToken,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string MessageId,
    [property: DataMember] string CorrelationId,
    [property: DataMember] string Tenant,
    [property: DataMember] string Domain,
    [property: DataMember] string AggregateId,
    [property: DataMember] string CommandType,
    [property: DataMember] string Proof)
{
    /// <summary>Gets the only execution-context schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
