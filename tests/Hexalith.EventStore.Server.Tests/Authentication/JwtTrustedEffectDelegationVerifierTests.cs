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

    private static string CreateToken(SecurityKey key, TrustedEffectSubmission submission, string digest)
    {
        EffectIdentity identity = submission.Identity;
        Claim[] claims =
        [
            new("effect_id", EffectIdentityCodec.ComputeEffectId(identity)),
            new("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
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
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
