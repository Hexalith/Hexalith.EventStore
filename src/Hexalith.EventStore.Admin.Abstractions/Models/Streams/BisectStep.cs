namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures one step in the bisect binary search process.
/// </summary>
/// <param name="StepNumber">The 1-based step index.</param>
/// <param name="TestedSequence">The midpoint sequence tested at this step.</param>
/// <param name="Verdict">The verdict: "good" when state matches expected field values, "bad" when state diverges.</param>
/// <param name="DivergentFieldCount">The number of watched fields that diverged at this midpoint; 0 when verdict is "good".</param>
public record BisectStep(
    int StepNumber,
    long TestedSequence,
    string Verdict,
    int DivergentFieldCount)
{
    /// <summary>Gets the verdict: "good" or "bad".</summary>
    public string Verdict { get; } = Verdict ?? string.Empty;

    /// <summary>
    /// Returns a string representation of the bisect step.
    /// </summary>
    public override string ToString()
        => $"Step {StepNumber}: seq {TestedSequence} = {Verdict}";
}
