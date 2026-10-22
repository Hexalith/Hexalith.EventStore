using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Advances one fully persisted promotion phase under the tenant directory actor.</summary>
/// <param name="Aliases">Every protected alias bound to the directory entry.</param>
/// <param name="ExpectedPhase">The phase whose external action completed durably.</param>
[DataContract]
public sealed record IdempotencyAdmissionDirectoryAdvanceRequest(
    [property: DataMember] IdempotencyAdmissionDirectoryAlias[] Aliases,
    [property: DataMember] IdempotencyAdmissionPromotionPhase ExpectedPhase);
