using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

using static Hexalith.EventStore.ServiceDefaults.Authentication.ConfigureWorkloadJwtBearerOptions;

namespace Hexalith.EventStore.ServiceDefaults.Tests.Authentication;

public sealed class CommandSourceAuthorityTests
{
    [Theory]
    [InlineData("command")]
    [InlineData("command-longest")]
    [InlineData("primary")]
    [InlineData("journal")]
    public async Task ExplicitDynamicDigestGrantsSignRsaAndVerifyExactRequests(string family)
    {
        ArgumentNullException.ThrowIfNull(family);
        bool commandFamily = family.StartsWith("command", StringComparison.Ordinal);
        var identity = family == "command-longest" ? new AggregateIdentity(new string('t', 64), new string('d', 64), new string('a', 256))
            : new AggregateIdentity("tenant-a", "conversation", "source-1");
        var scope = new SourcePublicationScope(identity.TenantId, identity.Domain, "deletion", "installation-1");
        const string incarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        // An explicit empty seed; no source I/O is needed to authenticate an exact request.
        var source = new CompleteCommandSource(identity, "epoch-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1),
            new(1, identity.TenantId, identity.Domain, identity.AggregateId, incarnation, 0, null, new string('a', 64), 1, false), []);
        var request = new DomainServiceRequest(new("message-1", identity.TenantId, identity.Domain, identity.AggregateId,
            "Create", "{}"u8.ToArray(), "correlation-1", null, "worker", null), new DomainServiceCurrentState(null, [], 0, 0)) { CompleteSource = source };
        var call = new SourcePublicationAuthorityCall(family == "journal" ? "transition-admit" : "write-index", scope, "epoch-1", "{}"u8.ToArray(), "");
        var bindings = commandFamily ? CommandSourceAuthorityBinding.Bind(request) : SourcePublicationWorkloadBinding.Bind(call);
        string operation = commandFamily ? EventStoreWorkloadOperations.DomainServiceProcess
            : family == "journal" ? SourcePublicationWorkloadBinding.AuthorityOperation : SourcePublicationWorkloadBinding.PrimaryOperation;
        string dynamicClaim = commandFamily ? CommandSourceAuthorityBinding.DigestClaim : SourcePublicationWorkloadBinding.DigestClaim;
        string directory = Path.Combine(Path.GetTempPath(), "command-source-rsa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using RSA rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=component-test-only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            string path = Path.Combine(directory, "issuer.pfx");
            await File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Pfx), TestContext.Current.CancellationToken);
            string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var grant = new WorkloadAuthorityGrant
            {
                GrantId = "grant-1", Revision = 1, ClientId = "producer", Workload = "producer", Audience = "receiver",
                ClientSecretSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))),
                NotBefore = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), Operations = [operation],
                Bindings = bindings.Where(pair => pair.Key != dynamicClaim).ToDictionary(pair => pair.Key, pair => new[] { pair.Value }, StringComparer.Ordinal),
            };
            var options = new WorkloadAuthorityOptions { Enabled = true, NonconformingDevelopmentFixture = true, Issuer = "https://issuer.example.test", KeyId = "test-key", SigningCertificatePath = path, Grants = [grant] };
            var monitor = Substitute.For<IOptionsMonitor<WorkloadAuthorityOptions>>(); monitor.CurrentValue.Returns(options);
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Development);
            var authority = new WorkloadBindingAuthority(monitor, environment, TimeProvider.System);
            var details = WorkloadAuthorizationDetails.Decode(WorkloadAuthorizationDetails.Encode(new("receiver", operation, bindings)))!;
            string oauthScope = new WorkloadAssertionIssuerOptions().BuildScope("receiver", operation);
            authority.Issue("producer", secret, oauthScope, details).ShouldBeNull();
            grant.AllowCompleteCommandSourceDigest = commandFamily; grant.AllowSourcePublicationDigest = !commandFamily;
            string assertion = authority.Issue("producer", secret, oauthScope, details).ShouldNotBeNull();
            var bearer = new JwtBearerOptions { TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = options.Issuer, ValidateAudience = true, ValidAudience = "receiver",
                ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)), ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero,
                PropertyBag = new Dictionary<string, object> { [ConfiguredMarkerKey] = true },
            } };
            var bearerMonitor = Substitute.For<IOptionsMonitor<JwtBearerOptions>>(); bearerMonitor.Get(Arg.Any<string>()).Returns(bearer);
            var workloadMonitor = Substitute.For<IOptionsMonitor<WorkloadAuthenticationOptions>>();
            workloadMonitor.Get(Arg.Any<string>()).Returns(new WorkloadAuthenticationOptions { Audience = "receiver", AllowedCallers = ["producer"] });
            var validator = new WorkloadAssertionValidator(bearerMonitor, workloadMonitor, WorkloadSecurityClock.System);
            var verified = await validator.ValidateLocallyAsync(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, assertion, operation, ["producer"], "receiver", TestContext.Current.CancellationToken);
            verified.Succeeded.ShouldBeTrue(); verified.Bindings.ShouldBe(bindings);
            if (commandFamily)
            {
                CommandSourceAuthorityBinding.Verify(request, verified.Principal!, "producer").ShouldBeTrue();
                bindings[CommandSourceAuthorityBinding.SourceClaim].Length.ShouldBeLessThanOrEqualTo(256);
                CommandSourceAuthorityBinding.Verify(request with { Command = request.Command with { TenantId = "foreign" } }, verified.Principal!, "producer").ShouldBeFalse();
                request.Command.Payload[0] ^= 1;
                CommandSourceAuthorityBinding.Verify(request, verified.Principal!, "producer").ShouldBeFalse();
            }
            else
            {
                SourcePublicationWorkloadBinding.Verify(call, verified).ShouldBeTrue();
                SourcePublicationWorkloadBinding.Verify(call with { Epoch = "foreign-epoch" }, verified).ShouldBeFalse();
            }
            foreach (var pair in bindings.Where(pair => pair.Key != dynamicClaim))
            {
                var changed = new Dictionary<string, string>(bindings, StringComparer.Ordinal) { [pair.Key] = "foreign" };
                authority.Issue("producer", secret, oauthScope, details with { Bindings = changed }).ShouldBeNull();
            }
            grant.Revoked = true; authority.Issue("producer", secret, oauthScope, details).ShouldBeNull();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
