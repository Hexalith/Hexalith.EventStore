namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Describes one bounded invocation without shell interpretation.</summary>
/// <param name="FileName">The executable name.</param>
/// <param name="Arguments">The literal argument vector.</param>
/// <param name="WorkingDirectory">The exact working directory.</param>
/// <param name="Timeout">The finite execution deadline.</param>
/// <param name="Environment">The explicitly bound environment overrides.</param>
/// <param name="Input">The optional standard-input fixture.</param>
internal sealed record QualificationCommand(string FileName, string[] Arguments, string WorkingDirectory,
    TimeSpan Timeout, IReadOnlyDictionary<string, string>? Environment = null, string? Input = null);
