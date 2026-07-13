namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Frozen named-projection dispatch protocol constants.
/// </summary>
public static class ProjectionDispatchProtocol {
    /// <summary>The version of the named projection dispatch wire envelope.</summary>
    public const int Version = 2;

    /// <summary>The operational metadata capability advertised by a v2 domain service.</summary>
    public const string Capability = "named-projection-dispatch-v2";
}
