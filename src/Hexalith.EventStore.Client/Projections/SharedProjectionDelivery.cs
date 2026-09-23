namespace Hexalith.EventStore.Client.Projections;

/// <summary>One source position, its canonical payload, and its consumer checkpoint.</summary>
public sealed record SharedProjectionDelivery(
    string SourceStream,
    long EnvelopePosition,
    byte[] CanonicalPayload,
    byte[] Checkpoint,
    SharedProjectionControlIndexIntent? ControlIndexIntent = null);
