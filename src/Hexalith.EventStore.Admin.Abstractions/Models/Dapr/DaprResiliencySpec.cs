namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Complete parsed DAPR resiliency specification.
/// </summary>
/// <param name="RetryPolicies">The parsed retry policies.</param>
/// <param name="TimeoutPolicies">The parsed timeout policies.</param>
/// <param name="CircuitBreakerPolicies">The parsed circuit breaker policies.</param>
/// <param name="TargetBindings">The flattened target-to-policy assignments.</param>
/// <param name="IsConfigurationAvailable">Whether the YAML was successfully read and parsed.</param>
/// <param name="RawYamlContent">The original YAML file content for the source viewer, or null when file not found.</param>
/// <param name="ErrorMessage">The parse error message, or null on success.</param>
public record DaprResiliencySpec(
    IReadOnlyList<DaprRetryPolicy> RetryPolicies,
    IReadOnlyList<DaprTimeoutPolicy> TimeoutPolicies,
    IReadOnlyList<DaprCircuitBreakerPolicy> CircuitBreakerPolicies,
    IReadOnlyList<DaprResiliencyTargetBinding> TargetBindings,
    bool IsConfigurationAvailable,
    string? RawYamlContent,
    string? ErrorMessage)
{
    /// <summary>Gets the parsed retry policies.</summary>
    public IReadOnlyList<DaprRetryPolicy> RetryPolicies { get; } = RetryPolicies
        ?? throw new ArgumentNullException(nameof(RetryPolicies));

    /// <summary>Gets the parsed timeout policies.</summary>
    public IReadOnlyList<DaprTimeoutPolicy> TimeoutPolicies { get; } = TimeoutPolicies
        ?? throw new ArgumentNullException(nameof(TimeoutPolicies));

    /// <summary>Gets the parsed circuit breaker policies.</summary>
    public IReadOnlyList<DaprCircuitBreakerPolicy> CircuitBreakerPolicies { get; } = CircuitBreakerPolicies
        ?? throw new ArgumentNullException(nameof(CircuitBreakerPolicies));

    /// <summary>Gets the flattened target-to-policy assignments.</summary>
    public IReadOnlyList<DaprResiliencyTargetBinding> TargetBindings { get; } = TargetBindings
        ?? throw new ArgumentNullException(nameof(TargetBindings));

    /// <summary>Gets a spec indicating resiliency configuration is not available (no path configured).</summary>
    public static DaprResiliencySpec Unavailable => new(
        [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null, ErrorMessage: null);

    /// <summary>Gets a spec indicating the resiliency configuration file was not found.</summary>
    /// <param name="path">The file path that was not found.</param>
    /// <returns>A spec with error message.</returns>
    public static DaprResiliencySpec NotFound(string path) => new(
        [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null,
        ErrorMessage: $"Resiliency configuration file not found: {path}");

    /// <summary>Gets a spec indicating a read error occurred.</summary>
    /// <param name="path">The file path that could not be read.</param>
    /// <param name="error">The error message.</param>
    /// <returns>A spec with error message.</returns>
    public static DaprResiliencySpec ReadError(string path, string error) => new(
        [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null,
        ErrorMessage: $"Failed to read resiliency configuration from {path}: {error}");

    /// <summary>Gets a spec indicating a YAML parse error occurred.</summary>
    /// <param name="path">The file path that could not be parsed.</param>
    /// <param name="rawYaml">The raw YAML content for display.</param>
    /// <param name="error">The parse error message.</param>
    /// <returns>A spec with error message and raw YAML preserved.</returns>
    public static DaprResiliencySpec ParseError(string path, string rawYaml, string error) => new(
        [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: rawYaml,
        ErrorMessage: $"Failed to parse resiliency YAML from {path}: {error}");
}
