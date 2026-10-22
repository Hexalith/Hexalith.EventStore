using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Bounded payload-free original owner inventory retained for late terminal scratch reconciliation.</summary>
/// <param name="Identity">The exact original authorized rebuild identity.</param>
/// <param name="OwnerOrdinal">The original accepted owner ordinal.</param>
/// <param name="ChunkCount">The original physical chunk count.</param>
/// <param name="TransportPartCount">The original transport part count.</param>
internal sealed record DomainSharedProjectionScratchRetirement(DomainSharedProjectionRebuildIdentity Identity,
    long OwnerOrdinal, int ChunkCount, int TransportPartCount, SharedProjectionCapturedIdentityReference? CapturedIdentityReference = null);
