namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Outbound settings used to obtain workload assertions, bound from <see cref="SectionName"/>.
/// </summary>
/// <remarks>
/// <para>
/// Credentials enter only through user-secrets, environment variables, or the Aspire parameter mechanism; no
/// value is ever committed.
/// </para>
/// <para>
/// In authority mode every (audience, operation) pair is requested as its own client-credentials token through two
/// scopes: <see cref="AudienceScopePrefix"/> followed by the receiving audience, and <see cref="OperationScopePrefix"/>
/// followed by the operation with each <c>:</c> replaced by <c>.</c>. The authority grants no audience or operation by
/// default, so a token never carries more than the one pair it was requested for.
/// </para>
/// </remarks>
public sealed class WorkloadAssertionIssuerOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Authentication:WorkloadIssuer";

    /// <summary>Gets the default lifetime of a self-signed Development assertion.</summary>
    public const int DefaultLifetimeSeconds = 120;

    /// <summary>Gets the default prefix of the scope that grants one receiving audience.</summary>
    public const string DefaultAudienceScopePrefix = "eventstore-audience.";

    /// <summary>Gets the default prefix of the scope that grants one operation.</summary>
    public const string DefaultOperationScopePrefix = "eventstore-operation.";

    /// <summary>Gets or sets this workload's identity, written as the <c>azp</c> claim of signed assertions.</summary>
    public string? Workload { get; set; }

    /// <summary>Gets or sets the client identifier used for the client-credentials grant in authority mode.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the client secret used for the client-credentials grant in authority mode.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets an explicit token endpoint; when omitted it is discovered from the authority metadata.</summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>Gets or sets the lifetime of a self-signed assertion in seconds.</summary>
    public int LifetimeSeconds { get; set; } = DefaultLifetimeSeconds;

    /// <summary>Gets or sets the prefix of the authority scope that grants one receiving audience.</summary>
    public string AudienceScopePrefix { get; set; } = DefaultAudienceScopePrefix;

    /// <summary>Gets or sets the prefix of the authority scope that grants one operation.</summary>
    public string OperationScopePrefix { get; set; } = DefaultOperationScopePrefix;

    /// <summary>
    /// Gets whether the client-credentials grant is configured.
    /// </summary>
    /// <returns><see langword="true"/> when both the client identifier and the client secret are present.</returns>
    public bool HasClientCredentials()
        => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// Builds the space-separated authority scope requesting exactly one audience and one operation.
    /// </summary>
    /// <param name="audience">The receiving audience.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The scope parameter value.</returns>
    public string BuildScope(string audience, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return GetAudienceScope(audience) + " " + GetOperationScope(operation);
    }

    /// <summary>
    /// Gets the authority scope that grants one receiving audience.
    /// </summary>
    /// <param name="audience">The receiving audience.</param>
    /// <returns>The scope name.</returns>
    public string GetAudienceScope(string audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return (AudienceScopePrefix ?? DefaultAudienceScopePrefix) + audience.Trim();
    }

    /// <summary>
    /// Gets the authority scope that grants one operation. Each <c>:</c> of the operation becomes <c>.</c>, so the
    /// scope name stays a plain token for every identity provider.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The scope name.</returns>
    public string GetOperationScope(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return (OperationScopePrefix ?? DefaultOperationScopePrefix) + operation.Trim().Replace(':', '.');
    }
}
