using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Composes the SDK gateway client's workload command and stream transport for a domain host.</summary>
public static class EventStoreGatewayWorkloadTransportExtensions
{
    /// <summary>
    /// Uses workload-only command and stream endpoints with assertions from the host's independently configured
    /// issuer. Each assertion grants exactly one operation, receiving audience, tenant and configured domain.
    /// </summary>
    /// <remarks>
    /// Register one domain per gateway client composition. Authority modes unable to issue resource-bound
    /// assertions fail closed; neither a forwarded human token nor a Dapr attribution header substitutes for one.
    /// </remarks>
    /// <param name="builder">The registered EventStore gateway client.</param>
    /// <param name="domain">The domain this host is authorized to access.</param>
    /// <param name="gatewayAudience">The gateway's receiving workload audience.</param>
    /// <returns>The client builder.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayWorkloadTransport(
        this IHttpClientBuilder builder,
        string domain,
        string gatewayAudience = EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return builder.AddEventStoreGatewayMultiDomainWorkloadTransport([domain.Trim()], gatewayAudience);
    }

    /// <summary>Composes exact per-request domain grants from a bounded configured allowlist.</summary>
    /// <param name="builder">The registered gateway client.</param>
    /// <param name="domains">The exact domains this host may access; wildcards and empty sets are rejected.</param>
    /// <param name="gatewayAudience">The receiving gateway workload.</param>
    /// <returns>The client builder.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayMultiDomainWorkloadTransport(
        this IHttpClientBuilder builder,
        IReadOnlyCollection<string> domains,
        string gatewayAudience = EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayAudience);
        if (domains.Count is < 1 or > 32 || domains.Any(domain => string.IsNullOrWhiteSpace(domain)
            || domain != domain.Trim() || domain.Contains('*', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Configure between one and 32 exact gateway domains.", nameof(domains));
        }
        HashSet<string> allowed = new(domains, StringComparer.Ordinal);
        _ = builder.Services.AddEventStoreWorkloadAssertionIssuer();
        _ = builder.Services.Configure<EventStoreGatewayClientOptions>(static options => options.UseWorkloadTransport = true);
        builder.Services.TryAddSingleton<IEventStoreGatewayWorkloadAssertionSource>(services =>
            new DomainServiceGatewayWorkloadAssertionSource(
                services.GetRequiredService<IWorkloadAssertionIssuer>(), allowed, gatewayAudience.Trim()));
        return builder;
    }
    /// <summary>Signs exact command/principal/metadata proofs for a bounded configured domain allowlist.</summary>
    /// <param name="builder">The registered gateway client.</param>
    /// <param name="caller">The exact authenticated workload name trusted by the receiver.</param>
    /// <param name="privateKeyPkcs8Base64">An independently provisioned RSA private key; never a human bearer credential.</param>
    /// <param name="domains">The exact command domains authorized for this proof key.</param>
    /// <returns>The client builder.</returns>
    public static IHttpClientBuilder AddEventStoreGatewayBoundCommandProof(
        this IHttpClientBuilder builder, string caller, string privateKeyPkcs8Base64, IReadOnlyCollection<string> domains)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPkcs8Base64);
        ArgumentNullException.ThrowIfNull(domains);
        if (caller.Length > 200 || caller != caller.Trim() || privateKeyPkcs8Base64.Length > 16384
            || domains.Count is < 1 or > 32 || domains.Any(domain => string.IsNullOrWhiteSpace(domain)
                || domain != domain.Trim() || domain.Contains('*', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Configure one exact caller, an RSA key and one to 32 exact command domains.");
        }
        using RSA rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyPkcs8Base64), out _);
        if (rsa.KeySize < 2048) { throw new ArgumentException("The command proof RSA key must be at least 2048 bits.", nameof(privateKeyPkcs8Base64)); }
        HashSet<string> allowed = new(domains, StringComparer.Ordinal);
        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.TryAddSingleton<IEventStoreGatewayBoundCommandProofSource>(services =>
            new DomainServiceGatewayBoundCommandProofSource(caller, privateKeyPkcs8Base64, allowed, services.GetRequiredService<TimeProvider>()));
        return builder;
    }

}
