using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Gateway-to-actor proof binding tests.</summary>
public sealed class TrustedEffectGatewayProofTests
{
    /// <summary>Only the exact signed tuple and provenance validate.</summary>
    [Fact]
    public async Task ChangedPurposeCannotReuseGatewayProof()
    {
        IIdempotencyDigestKeyProvider keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        _ = keys.GetKeyRingAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            new ValueTask<IdempotencyDigestKeyRing>(new IdempotencyDigestKeyRing(
                "test-v1",
                new Dictionary<string, byte[]> { ["test-v1"] = Enumerable.Repeat((byte)7, 32).ToArray() },
                [])));
        var proof = new TrustedEffectGatewayProof(keys);
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 2,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "ResumeWorkItem", [123, 125], messageId, messageId);
        var context = new TrustedEffectContext("reactor", "date-resume", "cause-1", "signed-token");
        var admission = new TrustedEffectAdmission(submission, context, "SEMANTIC-DIGEST");

        string signature = await proof.SignAsync(admission);
        await proof.ValidateAsync(admission, signature);
        await Should.ThrowAsync<InvalidOperationException>(() => proof.ValidateAsync(
            admission with { Context = context with { Purpose = "cascade-cancel" } }, signature));
        await Should.ThrowAsync<InvalidOperationException>(() => proof.ValidateAsync(admission, "forged-proof"));
        signature.ShouldStartWith("test-v1.");
    }

    /// <summary>A proof signed before key rotation validates while its version is a retained reader.</summary>
    [Fact]
    public async Task RetainedKeyVersionValidatesAndUnknownVersionIsRejected()
    {
        byte[] oldKey = Enumerable.Repeat((byte)7, 32).ToArray();
        byte[] newKey = Enumerable.Repeat((byte)9, 32).ToArray();
        TrustedEffectAdmission admission = CreateAdmission();
        string signature = await new TrustedEffectGatewayProof(Keys("v1", new() { ["v1"] = oldKey }, []))
            .SignAsync(admission);

        await new TrustedEffectGatewayProof(Keys("v2", new() { ["v1"] = oldKey, ["v2"] = newKey }, ["v1"]))
            .ValidateAsync(admission, signature);
        InvalidOperationException unknown = await Should.ThrowAsync<InvalidOperationException>(
            () => new TrustedEffectGatewayProof(Keys("v2", new() { ["v2"] = newKey }, []))
                .ValidateAsync(admission, signature));
        unknown.Message.ShouldBe("Trusted effect gateway proof key is unavailable.");
        signature.ShouldStartWith("v1.");
    }

    private static IIdempotencyDigestKeyProvider Keys(
        string activeVersion,
        Dictionary<string, byte[]> keys,
        string[] readerVersions)
    {
        IIdempotencyDigestKeyProvider provider = Substitute.For<IIdempotencyDigestKeyProvider>();
        _ = provider.GetKeyRingAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            new ValueTask<IdempotencyDigestKeyRing>(new IdempotencyDigestKeyRing(activeVersion, keys, readerVersions)));
        return provider;
    }

    private static TrustedEffectAdmission CreateAdmission()
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 2,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        return new TrustedEffectAdmission(
            new TrustedEffectSubmission(identity, "ResumeWorkItem", [123, 125], messageId, messageId),
            new TrustedEffectContext("reactor", "date-resume", "cause-1", "signed-token"),
            "SEMANTIC-DIGEST");
    }
}
