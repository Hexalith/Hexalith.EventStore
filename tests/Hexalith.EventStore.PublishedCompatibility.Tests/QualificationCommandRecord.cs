namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Safe persisted command output, retaining original and independently recomputable safe-output identities.</summary>
internal sealed record QualificationCommandRecord(QualificationCommand Command, DateTimeOffset Started, DateTimeOffset Ended,
    int ExitCode, string StandardOutput, string StandardError, string OutputHash, bool OutputTruncated,
    DateTimeOffset? DeadlineStarted, string? Interruption, string SafeOutputHash, bool OutputProjected);
