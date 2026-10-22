namespace Hexalith.EventStore.Client.Projections;

/// <summary>Validated live control-index membership with explicit tenant-local read incompleteness.</summary>
/// <param name="Tenants">Only tenants whose offboarding tombstone was successfully read and absent.</param>
/// <param name="IsIncomplete">Whether any tenant tombstone could not be validated.</param>
public sealed record SharedProjectionControlIndexReadResult(IReadOnlyList<string> Tenants, bool IsIncomplete);
