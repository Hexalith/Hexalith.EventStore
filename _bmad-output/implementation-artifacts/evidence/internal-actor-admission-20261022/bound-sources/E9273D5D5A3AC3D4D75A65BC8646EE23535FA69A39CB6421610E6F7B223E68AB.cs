using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Routes an exact registered admission through tenant lifecycle serialization.</summary>
/// <param name="Reference">The previously registered protected actor reference.</param>
/// <param name="Admission">The exact protected admission request.</param>
[DataContract]
public sealed record IdempotencyTenantLifecycleAdmissionRequest(
    [property: DataMember] IdempotencyTenantLifecycleReference Reference,
    [property: DataMember] IdempotencyAdmissionRequest Admission);
