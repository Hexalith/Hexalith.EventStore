namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Refuses a profile without retaining input values or parser exceptions.</summary>
public sealed class ProductionProfileValidationException : Exception
{
    /// <summary>Creates a safe structural failure.</summary>
    /// <param name="issues">Schema paths and fixed reason codes only.</param>
    public ProductionProfileValidationException(IReadOnlyList<ProductionProfileIssue> issues)
        : base("Production profile rejected: " + string.Join("; ", issues.Take(64).Select(i => $"{i.Path}: {i.Code}")))
    {
        Issues = issues.ToArray();
    }

    /// <summary>Gets the structural issues without input values.</summary>
    public IReadOnlyList<ProductionProfileIssue> Issues { get; }
}
