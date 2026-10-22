namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Read-model value used by the live process-restart watermark proof.</summary>
/// <param name="AppliedEventCount">Number of events in the persisted slice.</param>
/// <param name="Watermark">Highest positive persisted global position in that slice.</param>
public sealed record ProjectionWatermarkProcessState(int AppliedEventCount, long Watermark);
