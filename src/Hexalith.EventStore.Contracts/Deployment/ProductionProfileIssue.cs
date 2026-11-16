namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Safe deterministic structural diagnostic; contains no input values.</summary>
/// <param name="Path">The required path.</param>
/// <param name="Code">The required code.</param>
public sealed record ProductionProfileIssue(
    string Path,
    string Code);
