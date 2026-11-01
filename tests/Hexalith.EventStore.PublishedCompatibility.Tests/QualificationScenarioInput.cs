namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independent digest of an actual owner-prepared runtime input, without component contents.</summary>
internal sealed record QualificationScenarioInput(string Profile, string Owner, string Root, string Kind, string Path,
    long Bytes, string Sha256, DateTimeOffset Observed, QualificationComponentBinding? Component = null);
