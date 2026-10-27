namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Counts only executed checks, preserving every failed assertion within a case.</summary>
internal sealed class MeasuredAssertions
{
    /// <summary>Gets the number of actual evaluated assertions.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets failed assertion identifiers.</summary>
    internal List<string> Failures { get; } = [];

    /// <summary>Evaluates one named invariant.</summary>
    internal void Check(bool condition, string assertion)
    {
        Count++;
        if (!condition) Failures.Add(assertion);
    }
}
