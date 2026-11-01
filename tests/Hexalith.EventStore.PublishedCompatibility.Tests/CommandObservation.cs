using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Records the actual command, timing, exit and output identity of one operation.</summary>
/// <param name="Command">The exact invoked command.</param>
/// <param name="Started">The observed UTC start.</param>
/// <param name="Ended">The observed UTC completion.</param>
/// <param name="ExitCode">The actual process exit or interruption code.</param>
/// <param name="StandardOutput">The retained standard output.</param>
/// <param name="StandardError">The retained diagnostic output.</param>
/// <param name="OutputHash">The SHA-256 of the observed output.</param>
/// <param name="OutputTruncated">Whether a pipe exceeded its retained bound, making this observation explicitly nonpassing.</param>
[JsonConverter(typeof(QualificationCommandObservationConverter))]
internal sealed record CommandObservation(QualificationCommand Command, DateTimeOffset Started,
    DateTimeOffset Ended, int ExitCode, string StandardOutput, string StandardError, string OutputHash, bool OutputTruncated = false, DateTimeOffset? DeadlineStarted = null, string? Interruption = null, bool OriginalBytesRedacted = false);
