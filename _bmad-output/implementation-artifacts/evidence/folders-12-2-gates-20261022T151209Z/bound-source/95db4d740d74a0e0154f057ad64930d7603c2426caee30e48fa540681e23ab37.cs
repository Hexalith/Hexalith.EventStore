namespace Hexalith.EventStore.DomainService;

/// <summary>Declares how a named projection handler consumes rebuild input.</summary>
public enum DomainProjectionRebuildSemantics {
    /// <summary>The handler requires the complete event prefix through the requested boundary.</summary>
    FullReplay = 0,

    /// <summary>The handler requires prior staged state followed by one contiguous page.</summary>
    Incremental = 1,
}
