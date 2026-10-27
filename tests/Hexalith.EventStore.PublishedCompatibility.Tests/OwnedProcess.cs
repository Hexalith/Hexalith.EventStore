using System.Diagnostics;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Keeps a started process and both output drains alive until explicit cleanup.</summary>
internal sealed record OwnedProcess(Process Process, Task<string> Output, Task<string> Error, string Marker);
