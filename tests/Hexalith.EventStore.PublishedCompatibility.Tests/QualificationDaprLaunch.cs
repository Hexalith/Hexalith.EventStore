namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Actual sidecar process and finite launch coordinates; no environment or credentials.</summary>
internal sealed record QualificationDaprLaunch(string Profile, string Owner, string Executable, string Components,
    string AppId, int HostPort, int SidecarPort, int Pid, DateTimeOffset Observed, string Role, DateTimeOffset Started, DateTimeOffset? Ended);
