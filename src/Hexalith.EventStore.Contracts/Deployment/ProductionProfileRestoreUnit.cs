namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>One explicitly ordered backup/restore unit with component and semantic coverage.</summary>
/// <param name="Id">The required id.</param>
/// <param name="ComponentIds">The required component ids.</param>
/// <param name="Coverage">The required coverage.</param>
/// <param name="BackupMethod">The required backup method.</param>
public sealed record ProductionProfileRestoreUnit(
    string Id,
    IReadOnlyList<string> ComponentIds,
    IReadOnlyList<string> Coverage,
    string BackupMethod);
