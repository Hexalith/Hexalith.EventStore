namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>One explicitly enrolled, expiring workload grant. No wildcard binding values are interpreted.</summary>
public sealed class WorkloadAuthorityGrant
{
    /// <summary>Gets or sets the immutable grant identity.</summary>
    public string GrantId { get; set; } = string.Empty;
    /// <summary>Gets or sets the current positive authority revision.</summary>
    public long Revision { get; set; }
    /// <summary>Gets or sets the OAuth client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>Gets or sets the uppercase SHA-256 digest of a high-entropy protected client secret.</summary>
    public string ClientSecretSha256 { get; set; } = string.Empty;
    /// <summary>Gets or sets the authenticated workload, never supplied by the token request.</summary>
    public string Workload { get; set; } = string.Empty;
    /// <summary>Gets or sets the one receiving audience.</summary>
    public string Audience { get; set; } = string.Empty;
    /// <summary>Gets or sets explicitly approved operations.</summary>
    public string[] Operations { get; set; } = [];
    /// <summary>Gets or sets exact permitted values for every mandatory binding claim; omitted claims are denied.</summary>
    public Dictionary<string, string[]> Bindings { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Gets or sets whether this grant permits requests without resource bindings.</summary>
    public bool AllowUnbound { get; set; }
    /// <summary>Gets or sets whether exact Tenant/domain query requests may carry a canonical per-request SHA-256 digest. Disabled by default.</summary>
    public bool AllowQueryAuthorityDigest { get; set; }
    /// <summary>Gets or sets whether this exact source/method/epoch grant permits complete-command request digests. Disabled by default.</summary>
    public bool AllowCompleteCommandSourceDigest { get; set; }
    /// <summary>Gets or sets whether this exact namespace/method/epoch grant permits publication request digests. Disabled by default.</summary>
    public bool AllowSourcePublicationDigest { get; set; }
    /// <summary>Gets or sets the actual grant's inclusive start.</summary>
    public DateTimeOffset NotBefore { get; set; }
    /// <summary>Gets or sets the actual grant's exclusive end.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Gets or sets whether the grant has been revoked.</summary>
    public bool Revoked { get; set; }
}
