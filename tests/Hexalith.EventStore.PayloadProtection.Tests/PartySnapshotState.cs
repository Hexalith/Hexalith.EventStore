namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// A registered snapshot state used by Story 8.4 snapshot-routing tests.
/// </summary>
/// <param name="Name">The party display name.</param>
/// <param name="Version">The state version.</param>
internal sealed record PartySnapshotState(string Name, int Version);
