namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Isolates process-wide diagnostic listeners from tests executing in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DiagnosticsCollection
{
    /// <summary>Gets the isolated diagnostic collection name.</summary>
    public const string Name = "PayloadProtection diagnostics";
}
