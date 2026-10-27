using System.Diagnostics;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Keeps a started process and both output drains alive until explicit cleanup.</summary>
/// <param name="Process">The directly owned process.</param>
/// <param name="Output">The active standard-output drain.</param>
/// <param name="Error">The active diagnostic-output drain.</param>
/// <param name="Marker">The inherited command ownership marker.</param>
internal sealed record OwnedProcess(Process Process, Task<string> Output, Task<string> Error, string Marker);
