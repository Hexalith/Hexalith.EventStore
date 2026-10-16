using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>One complete bounded readable prefix and its checkpoint-relative admission for canonical replay.</summary>
internal sealed record ProjectionSourceGuardReplayAdmission(ProjectionSourceAdmittedHistory Tail, ProjectionEventDto[] Events);
