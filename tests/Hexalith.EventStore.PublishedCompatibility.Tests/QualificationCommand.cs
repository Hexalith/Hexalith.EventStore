using System.Text;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Describes one bounded invocation without shell interpretation.</summary>
/// <param name="FileName">The executable name.</param>
/// <param name="Arguments">The literal argument vector.</param>
/// <param name="WorkingDirectory">The exact working directory.</param>
/// <param name="Timeout">The finite execution deadline.</param>
/// <param name="Environment">The explicitly bound environment overrides.</param>
/// <param name="Input">The optional standard-input fixture.</param>
internal sealed record QualificationCommand(string FileName, string[] Arguments, string WorkingDirectory,
    TimeSpan Timeout, [property: JsonIgnore] IReadOnlyDictionary<string, string>? Environment = null,
    [property: JsonIgnore] string? Input = null)
{
    /// <summary>Gets explicitly supplied environment names without disclosing their values.</summary>
    public string[] EnvironmentKeys => Environment?.Keys.Order(StringComparer.Ordinal).ToArray() ?? [];
    /// <summary>Gets the input identity without retaining the input or its credentials.</summary>
    public string? InputHash => Input is null ? null : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(Input));
}
