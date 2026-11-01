namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Actual bounded owned-process observation from a cleanup pass.</summary>
internal sealed record QualificationProcessScan(string Id, string Owner, DateTimeOffset Started, DateTimeOffset Ended, IReadOnlyList<int> Pids);
