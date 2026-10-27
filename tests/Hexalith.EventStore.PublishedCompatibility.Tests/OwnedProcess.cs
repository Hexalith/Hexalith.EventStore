using System.Diagnostics;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Keeps a started process and both output drains alive until explicit cleanup.</summary>
/// <param name="Process">The directly owned process.</param>
/// <param name="StandardOutput">The bounded standard-output drain.</param>
/// <param name="StandardError">The bounded diagnostic-output drain.</param>
/// <param name="Marker">The inherited command ownership marker.</param>
/// <param name="Command">The command retained for private diagnostic redaction.</param>
internal sealed record OwnedProcess(Process Process, BoundedProcessOutput StandardOutput, BoundedProcessOutput StandardError, string Marker, QualificationCommand Command)
{
    private int _recorded;
    /// <summary>Gets the active bounded standard-output drain.</summary>
    internal Task<string> Output => StandardOutput.Completion;
    /// <summary>Gets the active bounded diagnostic-output drain.</summary>
    internal Task<string> Error => StandardError.Completion;
    /// <summary>Gets whether either pipe exceeded its bound.</summary>
    internal bool Overflowed => StandardOutput.Overflowed || StandardError.Overflowed;
    /// <summary>Gets the asynchronous overflow cleanup observation.</summary>
    internal Task OverflowCleanup { get; set; } = Task.CompletedTask;
    /// <summary>Claims a unique final command observation.</summary>
    internal bool TryRecord() => Interlocked.Exchange(ref _recorded, 1) == 0;
}
