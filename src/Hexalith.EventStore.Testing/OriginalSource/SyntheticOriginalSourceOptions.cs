namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Run-private selection of a synthetic original-primary source, never a production import policy.</summary>
public sealed class SyntheticOriginalSourceOptions
{
    /// <summary>The only supported explicit fixture mode.</summary>
    public const string SelectedMode = "synthetic-original-primary-v1";
    /// <summary>Gets or sets the exact selected mode.</summary>
    public string Mode { get; set; } = string.Empty;
    /// <summary>Gets or sets the independently generated run identity.</summary>
    public string RunId { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing owner-private durable IPC/source directory.</summary>
    public string Directory { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact run-owned tenant.</summary>
    public string Tenant { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact source domain.</summary>
    public string Domain { get; set; } = string.Empty;
    /// <summary>Gets or sets the explicitly selected synthetic resource namespace.</summary>
    public string AggregatePrefix { get; set; } = string.Empty;
}
