namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>The generic full-prefix fixture's complete selected detail/index value.</summary>
/// <param name="Count">The canonical counter state.</param>
/// <param name="ProjectionVersion">The selected source-head version.</param>
public sealed record LiveReplayCounterModel(int Count, string ProjectionVersion);
