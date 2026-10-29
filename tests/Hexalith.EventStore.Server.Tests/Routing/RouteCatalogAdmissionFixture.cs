using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Explicit Development admission registrations with independently available disposable test keys.</summary>
internal static class RouteCatalogAdmissionFixture
{
    public static void Configure(IServiceCollection services, IEnumerable<IDeclaredIdempotencyIntentAdapter> adapters)
    {
        foreach (var adapter in adapters) services.AddSingleton<IIdempotencyIntentAdapter>(adapter);
        services.AddSingleton<IOptions<IdempotencyAdmissionOptions>>(Options.Create(new IdempotencyAdmissionOptions
        {
            Enabled = true, ActiveDigestKeyVersion = "development-v1", DigestKeySource = IdempotencyDigestKeySource.Configuration,
            DigestKeySecretStoreName = "secretstore", DigestKeySecretName = "eventstore-idempotency-ring", DigestKeySecretGeneration = "development-v1",
            DigestKeys = new(StringComparer.Ordinal) { ["development-v1"] = Convert.ToBase64String(new byte[32]) },
        }));
        services.AddSingleton<IIdempotencyDigestKeyProvider, ConfigurationIdempotencyDigestKeyProvider>();
    }
}
