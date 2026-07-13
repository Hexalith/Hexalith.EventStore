namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Controls the deterministic index-handler failure used by live v2 retry evidence.</summary>
public sealed class LiveNamedProjectionFaultControl {
    /// <summary>Gets or sets whether the index handler returns a retryable outcome.</summary>
    public bool FailIndex { get; set; }
}
