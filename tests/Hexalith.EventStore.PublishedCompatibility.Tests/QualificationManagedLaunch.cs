namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Actual generated managed process identity and measured OS lifetime, without raw arguments/environment.</summary>
internal sealed record QualificationManagedLaunch(string Id, string Owner, string Root, string Role, string Assembly,
    IReadOnlyDictionary<string, string> ExecutableHashes, int Pid, string? Profile, string? Address, DateTimeOffset Started, DateTimeOffset? Ended);
