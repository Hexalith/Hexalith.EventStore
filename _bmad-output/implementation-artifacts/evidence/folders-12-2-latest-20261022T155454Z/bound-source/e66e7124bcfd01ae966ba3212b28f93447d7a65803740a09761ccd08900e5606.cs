using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Selected detail/index value used by the real guarded delivery fixture.</summary>
/// <param name="Count">The semantic canonical counter value.</param>
/// <param name="ProjectionVersion">The persisted freshness version.</param>
/// <param name="ProjectionOwner">The existing selected-owner receipt binding.</param>
public sealed record LiveGuardedCounterModel(int Count, string ProjectionVersion, ProjectionOwnerSelection ProjectionOwner);
