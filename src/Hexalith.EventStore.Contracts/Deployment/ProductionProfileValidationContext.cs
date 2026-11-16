namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Caller-owned composing inventory; must not be derived from the profile being validated.</summary>
/// <param name="ExpectedApplicationIds">The required expected application ids.</param>
/// <param name="ExpectedComponentIds">The required expected component ids.</param>
public sealed record ProductionProfileValidationContext(
    IReadOnlyList<string> ExpectedApplicationIds,
    IReadOnlyList<string> ExpectedComponentIds);
