namespace Hexalith.EventStore.Testing;

/// <summary>A metadata-only refusal of an invalid or unavailable runner handoff.</summary>
/// <param name="rule">The stable contract refusal category.</param>
/// <param name="message">A fixed message without supplied values or credentials.</param>
public sealed class TestEnvironmentDescriptorException(string rule, string message) : InvalidOperationException($"{rule}: {message}")
{
    /// <summary>Gets the stable refusal category.</summary>
    public string Rule { get; } = rule;
}
