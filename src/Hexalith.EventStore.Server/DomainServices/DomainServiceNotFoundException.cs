namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Exception thrown when no domain service is registered for a given tenant and domain.
/// Includes the expected config store key pattern for operator guidance.
/// </summary>
public class DomainServiceNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceNotFoundException"/> class.
    /// </summary>
    public DomainServiceNotFoundException()
        : base("No domain service registered.")
    {
        TenantId = string.Empty;
        Domain = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DomainServiceNotFoundException(string message)
        : base(message)
    {
        TenantId = string.Empty;
        Domain = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DomainServiceNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
        TenantId = string.Empty;
        Domain = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceNotFoundException"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="version">The service version (defaults to "v1").</param>
    public DomainServiceNotFoundException(string tenantId, string domain, string version = "v1")
        : base($"No domain service registered for tenant '{tenantId}', domain '{domain}', version '{version}'. Register via DAPR config store with key '{tenantId}:{domain}:{version}'.")
    {
        TenantId = tenantId;
        Domain = domain;
        Version = version;
    }

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; }

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; }

    /// <summary>Gets the service version.</summary>
    public string Version { get; } = "v1";
}
