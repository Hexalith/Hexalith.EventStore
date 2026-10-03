using System.Security.Cryptography;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Identity;

public sealed class IdentityAdmissionTests
{
    [Fact]
    public void ExactScope_VerifiesWithPublicKeyAndRejectsForeignPurposeAndRevision()
    {
        using RSA rsa = RSA.Create(2048);
        IOptionsMonitor<IdentityAdmissionSigningOptions> signing = Substitute.For<IOptionsMonitor<IdentityAdmissionSigningOptions>>();
        signing.CurrentValue.Returns(new IdentityAdmissionSigningOptions { PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem() });
        IOptionsMonitor<IdentityAdmissionOptions> verification = Substitute.For<IOptionsMonitor<IdentityAdmissionOptions>>();
        verification.CurrentValue.Returns(new IdentityAdmissionOptions { VerificationKeyPem = rsa.ExportSubjectPublicKeyInfoPem(), AuthorityRevision = 1 });
        var scope = new IdentityAdmissionScope("tenant-a", "party", "party-1", "Bind", "message", "logical", "digest");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var evidence = new IdentityAdmissionEvidence(scope, "identity-writer", "01HX0000000000000000000001", "01HX0000000000000000000002", 1, true, now, now.AddMinutes(1), 1);
        string proof = new IdentityAdmissionSigner(signing).Sign(evidence);
        var verifier = new IdentityAdmissionProof(verification, TimeProvider.System);
        verifier.Verify(proof, scope).ShouldBe(evidence);
        verifier.Verify(proof, scope with { Domain = "identity-registry" }).ShouldBeNull();
        verifier.Verify(proof, scope with { TenantId = "tenant-b" }).ShouldBeNull();
        verifier.Verify(proof, scope with { PayloadDigest = "other" }).ShouldBeNull();
        verification.CurrentValue.AuthorityRevision = 2;
        verifier.Verify(proof, scope).ShouldBeNull();
    }

    [Fact]
    public void VerifierBoundary_CannotSignAndRejectsPrivateKeyConfiguration()
    {
        typeof(IIdentityAdmissionProof).GetMethods().ShouldAllBe(method => method.Name != "Sign");
        using RSA signer = RSA.Create(2048);
        using RSA publicOnly = RSA.Create();
        publicOnly.ImportFromPem(signer.ExportSubjectPublicKeyInfoPem());
        Should.Throw<CryptographicException>(() => publicOnly.SignData("evidence"u8.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        IOptionsMonitor<IdentityAdmissionOptions> options = Substitute.For<IOptionsMonitor<IdentityAdmissionOptions>>();
        var scope = new IdentityAdmissionScope("tenant", "party", "p", "op", "m", "l", "d");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IOptionsMonitor<IdentityAdmissionSigningOptions> signing = Substitute.For<IOptionsMonitor<IdentityAdmissionSigningOptions>>();
        signing.CurrentValue.Returns(new IdentityAdmissionSigningOptions { PrivateKeyPem = signer.ExportPkcs8PrivateKeyPem() });
        string proof = new IdentityAdmissionSigner(signing).Sign(new(scope, "writer", null, null, 0, false, now, now.AddMinutes(1), 1));
        options.CurrentValue.Returns(new IdentityAdmissionOptions { VerificationKeyPem = signer.ExportSubjectPublicKeyInfoPem(), AuthorityRevision = 1 });
        var verifier = new IdentityAdmissionProof(options, TimeProvider.System);
        verifier.Verify(proof, scope).ShouldNotBeNull();
        options.CurrentValue.VerificationKeyPem = signer.ExportPkcs8PrivateKeyPem();
        verifier.Verify(proof, scope).ShouldBeNull();
    }

    [Theory]
    [InlineData("binding-closure")]
    [InlineData("arbitrary")]
    [InlineData("")]
    public void UnsupportedTrigger_DoesNotQualify(string trigger)
    {
        var policy = new IdentityHistoryPolicy("synthetic", TimeSpan.FromDays(1), trigger);
        policy.IsValid.ShouldBeFalse();
        new IdentityHistoryCustodyEvidence("synthetic", "party-actor-history-v1", DateTimeOffset.UtcNow, 1, "custody", true, true, true)
            .Satisfies(policy, DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void OverflowAndWrongExpiry_AreSafeDenials()
    {
        var policy = new IdentityHistoryPolicy("synthetic", TimeSpan.FromDays(1), "binding-effective-at");
        policy.DeriveExpiry(DateTimeOffset.MaxValue).ShouldBeNull();
        new IdentityHistoryCustodyEvidence("synthetic", "party-actor-history-v1", DateTimeOffset.UnixEpoch, 1, "custody", true, true, true)
            .Satisfies(policy, DateTimeOffset.UnixEpoch).ShouldBeFalse();
    }
}
