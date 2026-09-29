using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Contracts.Effects;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>Signed delegation bindings for trusted effect admission.</summary>
public sealed class JwtTrustedEffectDelegationVerifierTests
{
    private const string Issuer = "https://identity.example.test";

    /// <summary>A valid signature cannot delegate another tenant, target, purpose, or command digest.</summary>
    [Fact]
    public async Task SignedDelegationRejectsChangedBindings()
    {
        using RSA rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(key);
        var options = new JwtBearerOptions
        {
            ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration),
        };
        IOptionsMonitor<JwtBearerOptions> monitor = Substitute.For<IOptionsMonitor<JwtBearerOptions>>();
        _ = monitor.Get(JwtBearerDefaults.AuthenticationScheme).Returns(options);
        var verifier = new JwtTrustedEffectDelegationVerifier(monitor);
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 7,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "ResumeWorkItem", [1, 2], messageId, messageId);
        string digest = new('A', 52);
        string token = CreateToken(key, submission, digest);
        var context = new TrustedEffectContext("reactor", "date-resume", "source-cause", token);

        await verifier.VerifyAsync(submission, context, digest);
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission with { Identity = identity with { Tenant = "tenant-b" } }, context, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission with { Identity = identity with { SourceEnvelopeSequence = 8 } }, context, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission with { Identity = identity with { TargetAggregate = "target-2" } }, context, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission with { CommandType = "CancelWorkItem" }, context, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission, context with { Workload = "timer" }, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission, context with { Purpose = "cascade-cancel" }, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission, context with { CausationId = "other-cause" }, digest));
        await Should.ThrowAsync<InvalidOperationException>(() => verifier.VerifyAsync(
            submission, context, new string('B', 52)));
    }

    /// <summary>A delegation whose exp minus iat exceeds the documented maximum lifetime is rejected.</summary>
    [Fact]
    public async Task OverLongDelegationLifetimeIsRejected()
    {
        (JwtTrustedEffectDelegationVerifier verifier, RsaSecurityKey key, TrustedEffectSubmission submission, string digest) = CreateVerifier();
        DateTime issuedAt = DateTime.UtcNow.AddMinutes(-1);
        string withinBound = CreateToken(key, submission, digest, issuedAt, issuedAt.Add(JwtTrustedEffectDelegationVerifier.MaximumLifetime));
        string tooLong = CreateToken(key, submission, digest, issuedAt, issuedAt.Add(JwtTrustedEffectDelegationVerifier.MaximumLifetime).AddSeconds(1));

        await verifier.VerifyAsync(submission, Context(withinBound), digest);
        await Should.ThrowAsync<InvalidOperationException>(
            () => verifier.VerifyAsync(submission, Context(tooLong), digest));
    }

    /// <summary>Expiry beyond the symmetric clock skew is a token validation failure; expiry within it is tolerated.</summary>
    [Fact]
    public async Task ExpiredDelegationIsRejectedOutsideClockSkew()
    {
        (JwtTrustedEffectDelegationVerifier verifier, RsaSecurityKey key, TrustedEffectSubmission submission, string digest) = CreateVerifier();
        DateTime now = DateTime.UtcNow;
        string expired = CreateToken(key, submission, digest, now.AddMinutes(-10), now.AddMinutes(-2));
        string withinSkew = CreateToken(key, submission, digest, now.AddMinutes(-5), now.AddSeconds(-10));

        await Should.ThrowAsync<SecurityTokenExpiredException>(
            () => verifier.VerifyAsync(submission, Context(expired), digest));
        await verifier.VerifyAsync(submission, Context(withinSkew), digest);
    }

    /// <summary>An unknown signing key refreshes authority metadata once and retries validation.</summary>
    [Fact]
    public async Task UnknownSigningKeyRefreshesAuthorityMetadataAndRetriesOnce()
    {
        using RSA oldRsa = RSA.Create(2048);
        using RSA newRsa = RSA.Create(2048);
        var oldKey = new RsaSecurityKey(oldRsa) { KeyId = "old-key" };
        var newKey = new RsaSecurityKey(newRsa) { KeyId = "new-key" };
        var manager = new RotatingConfigurationManager(Configuration(oldKey), Configuration(newKey));
        var verifier = new JwtTrustedEffectDelegationVerifier(Monitor(manager));
        (TrustedEffectSubmission submission, string digest) = CreateSubmission();
        DateTime issuedAt = DateTime.UtcNow.AddMinutes(-1);
        string rotated = CreateToken(newKey, submission, digest, issuedAt, issuedAt.AddMinutes(5));

        await verifier.VerifyAsync(submission, Context(rotated), digest);

        manager.RefreshRequests.ShouldBe(1);
        manager.Fetches.ShouldBe(2);

        using RSA unknownRsa = RSA.Create(2048);
        var unknownKey = new RsaSecurityKey(unknownRsa) { KeyId = "unknown-key" };
        string unknown = CreateToken(unknownKey, submission, digest, issuedAt, issuedAt.AddMinutes(5));
        await Should.ThrowAsync<SecurityTokenSignatureKeyNotFoundException>(
            () => verifier.VerifyAsync(submission, Context(unknown), digest));
        manager.RefreshRequests.ShouldBe(2);
        manager.Fetches.ShouldBe(4);
    }

    private static (JwtTrustedEffectDelegationVerifier Verifier, RsaSecurityKey Key, TrustedEffectSubmission Submission, string Digest) CreateVerifier()
    {
        var key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" };
        var verifier = new JwtTrustedEffectDelegationVerifier(
            Monitor(new StaticConfigurationManager<OpenIdConnectConfiguration>(Configuration(key))));
        (TrustedEffectSubmission submission, string digest) = CreateSubmission();
        return (verifier, key, submission, digest);
    }

    private static (TrustedEffectSubmission Submission, string Digest) CreateSubmission()
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 7,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        return (new TrustedEffectSubmission(identity, "ResumeWorkItem", [1, 2], messageId, messageId), new string('A', 52));
    }

    private static TrustedEffectContext Context(string token)
        => new("reactor", "date-resume", "source-cause", token);

    private static OpenIdConnectConfiguration Configuration(SecurityKey key)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(key);
        return configuration;
    }

    private static IOptionsMonitor<JwtBearerOptions> Monitor(IConfigurationManager<OpenIdConnectConfiguration> manager)
    {
        var options = new JwtBearerOptions { ConfigurationManager = manager };
        IOptionsMonitor<JwtBearerOptions> monitor = Substitute.For<IOptionsMonitor<JwtBearerOptions>>();
        _ = monitor.Get(JwtBearerDefaults.AuthenticationScheme).Returns(options);
        return monitor;
    }

    private static string CreateToken(SecurityKey key, TrustedEffectSubmission submission, string digest)
        => CreateToken(key, submission, digest, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

    private static string CreateToken(
        SecurityKey key,
        TrustedEffectSubmission submission,
        string digest,
        DateTime issuedAt,
        DateTime expiresAt)
    {
        EffectIdentity identity = submission.Identity;
        Claim[] claims =
        [
            new("effect_id", EffectIdentityCodec.ComputeEffectId(identity)),
            new("iat", new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("tenant", identity.Tenant),
            new("source_domain", identity.SourceDomain),
            new("source_aggregate", identity.SourceAggregate),
            new("source_envelope_sequence", identity.SourceEnvelopeSequence.ToString(CultureInfo.InvariantCulture)),
            new("effect_kind", identity.EffectKind),
            new("target_domain", identity.TargetDomain),
            new("target_aggregate", identity.TargetAggregate),
            new("effect_ordinal", identity.Ordinal.ToString(CultureInfo.InvariantCulture)),
            new("command_type", submission.CommandType),
            new("command_digest", digest),
            new("workload", "reactor"),
            new("purpose", "date-resume"),
            new("causation", "source-cause"),
        ];
        var jwt = new JwtSecurityToken(
            Issuer,
            "eventstore-gateway",
            claims,
            issuedAt,
            expiresAt,
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class RotatingConfigurationManager(
        OpenIdConnectConfiguration current,
        OpenIdConnectConfiguration rotated) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private bool _refreshed;

        public int Fetches { get; private set; }

        public int RefreshRequests { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            Fetches++;
            return Task.FromResult(_refreshed ? rotated : current);
        }

        public void RequestRefresh()
        {
            RefreshRequests++;
            _refreshed = true;
        }
    }
}
