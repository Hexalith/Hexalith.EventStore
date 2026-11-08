namespace Hexalith.EventStore.Aspire;

/// <summary>Explicit local custody and native coordinates for one disposable Development transport.</summary>
/// <param name="CustodyDirectory">A new private directory outside a checkout for durable sanitized receipts.</param>
/// <param name="RuntimeExecutable">The Dapr 1.18.7 native executable matching the existing qualification lock.</param>
/// <param name="PlacementAddress">An unused loopback placement endpoint, or null for allocation.</param>
/// <param name="SchedulerAddress">An unused loopback scheduler endpoint, or null for allocation.</param>
public sealed record DevelopmentRouteCatalogTransportOptions(
    string CustodyDirectory,
    string RuntimeExecutable,
    string? PlacementAddress = null,
    string? SchedulerAddress = null)
{
    /// <summary>The matched native/runtime profile, explicitly opted into by candidate callers.</summary>
    public DevelopmentRouteCatalogRuntimeProfile RuntimeProfile { get; init; } = DevelopmentRouteCatalogRuntimeProfile.Original;
}
