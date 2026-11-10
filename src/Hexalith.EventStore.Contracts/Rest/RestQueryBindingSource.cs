namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Identifies how a generated REST query endpoint should populate the EventStore query envelope.
/// </summary>
public enum RestQueryBindingSource
{
    /// <summary>No explicit value is supplied.</summary>
    None,

    /// <summary>Use the literal value supplied by the attribute.</summary>
    Constant,

    /// <summary>Use the value of a route parameter named by the attribute.</summary>
    Route,
}
