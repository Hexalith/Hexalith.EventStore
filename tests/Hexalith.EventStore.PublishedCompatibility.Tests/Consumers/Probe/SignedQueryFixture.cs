#if SELECTED
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PublishedConsumer;

/// <summary>Creates real signed admission evidence with an invocation-local private key kept only in memory.</summary>
public static class SignedQueryFixture
{
    /// <summary>Signs and verifies exact query admission with an ephemeral in-memory RSA key.</summary>
    public static object Create()
    {
        using RSA key = RSA.Create(2048);
        var services = new ServiceCollection();
        services.Configure<IdentityAdmissionSigningOptions>(options => options.PrivateKeyPem = key.ExportRSAPrivateKeyPem());
        services.Configure<IdentityAdmissionOptions>(options => { options.VerificationKeyPem = key.ExportRSAPublicKeyPem(); options.AuthorityRevision = 1; });
        using ServiceProvider provider = services.BuildServiceProvider();
        var signer = new IdentityAdmissionSigner(provider.GetRequiredService<IOptionsMonitor<IdentityAdmissionSigningOptions>>());
        var verifier = new IdentityAdmissionProof(provider.GetRequiredService<IOptionsMonitor<IdentityAdmissionOptions>>(), TimeProvider.System);
        var scope = new IdentityAdmissionScope("tenant-a", "counter", "counter-1", "get-counter", "corr", "corr", IdentityAdmissionProof.Digest([]));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string proof = signer.Sign(new IdentityAdmissionEvidence(scope, "fixture-workload", "actor", "actor", 1, true, now, now.AddMinutes(4), 1));
        var query = new QueryEnvelope("tenant-a", "counter", "counter-1", "get-counter", [], "corr", "user", null, false, null,
            "actor", "workload", true, ["counter.read"], ["eventstore"], "delegation") { IdentityAdmissionProof = proof };
        return new { value = JsonSerializer.SerializeToElement(query, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            verified = verifier.Verify(proof, scope) is not null,
            missingRefused = verifier.Verify(null, scope) is null,
            forgedRefused = verifier.Verify(proof + "forged", scope) is null,
            foreignTenantRefused = verifier.Verify(proof, scope with { TenantId = "tenant-b" }) is null,
            identities = ConsumerIdentity.Read() };
    }
}
#endif
