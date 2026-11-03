using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Uses the existing SDK workload stream client and exact owner proof to observe content-free native metadata.</summary>
public sealed class BoundOwnerNativeSourceMetadataReader : INativeSourceMetadataReader
{
    private readonly IHttpClientFactory _clients;
    private readonly string _clientName;
    private readonly IReadOnlySet<string> _domains;
    private readonly TimeProvider _clock;
    private readonly IEventStoreGatewayWorkloadAssertionSource _assertions;
    private readonly IEventStoreGatewayStreamReadProofSource _proofs;

    /// <summary>Creates an adapter over an independently enrolled gateway HTTP client and owner identity.</summary>
    /// <param name="clients">The factory retaining the normal SDK catalog fence.</param>
    /// <param name="clientName">The host-owned gateway client name.</param>
    /// <param name="issuer">The real configured workload issuer.</param>
    /// <param name="caller">The exact owner workload.</param>
    /// <param name="privateKey">The independently provisioned owner proof key.</param>
    /// <param name="domains">Finite exact owner domains.</param>
    /// <param name="clock">The observation clock.</param>
    /// <param name="gatewayAudience">The real gateway audience.</param>
    public BoundOwnerNativeSourceMetadataReader(IHttpClientFactory clients, string clientName,
        IWorkloadAssertionIssuer issuer, string caller, string privateKey, IReadOnlyCollection<string> domains,
        TimeProvider clock, string gatewayAudience = "eventstore")
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayAudience);
        if (caller != caller.Trim() || caller.Length > 200 || privateKey.Length > 16384 || domains.Count is < 1 or > 32
            || domains.Any(domain => string.IsNullOrWhiteSpace(domain) || domain != domain.Trim() || domain.Contains('*')))
            throw new ArgumentException("Exact owner identity, proof key and bounded domains are required.");
        using RSA rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        if (rsa.KeySize < 2048) throw new ArgumentException("Owner proof RSA keys must be at least 2048 bits.", nameof(privateKey));
        _clients = clients;
        _clientName = clientName;
        _domains = new HashSet<string>(domains, StringComparer.Ordinal);
        _clock = clock;
        _assertions = new DomainServiceGatewayWorkloadAssertionSource(issuer, _domains, gatewayAudience);
        _proofs = new DomainServiceGatewayBoundStreamReadProofSource(caller, privateKey, _domains, clock);
    }

    /// <inheritdoc/>
    public async Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_domains.Contains(identity.Domain)) return null;
        using HttpClient http = _clients.CreateClient(_clientName);
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions
        {
            UseWorkloadTransport = true, MaxStreamReadResponseBytes = 32768,
        }), _assertions, _proofs);
        var metadata = new GatewayNativeSourceMetadataReader(gateway, _clock);
        return await metadata.ReadAsync(identity, cancellationToken).ConfigureAwait(false);
    }
}
