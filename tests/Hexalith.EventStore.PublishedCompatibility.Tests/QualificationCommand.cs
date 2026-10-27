namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Describes one bounded invocation without shell interpretation.</summary>
internal sealed record QualificationCommand(string FileName, string[] Arguments, string WorkingDirectory,
    TimeSpan Timeout, IReadOnlyDictionary<string, string>? Environment = null, string? Input = null);
