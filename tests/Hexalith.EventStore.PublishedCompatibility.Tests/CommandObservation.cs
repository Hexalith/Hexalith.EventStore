namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Records the actual command, timing, exit and output identity of one operation.</summary>
internal sealed record CommandObservation(QualificationCommand Command, DateTimeOffset Started,
    DateTimeOffset Ended, int ExitCode, string StandardOutput, string StandardError, string OutputHash);
