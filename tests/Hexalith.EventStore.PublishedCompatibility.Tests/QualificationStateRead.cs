namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independent actual state-read transport/value commitment, without state contents.</summary>
internal sealed record QualificationStateRead(string Id, string Profile, string Owner, string AppId, string Backend,
    string Actor, string Key, string Address, int HostPid, DateTimeOffset Started, DateTimeOffset Ended,
    int Status, long Bytes, string BodySha256, string? ValueSha256);
