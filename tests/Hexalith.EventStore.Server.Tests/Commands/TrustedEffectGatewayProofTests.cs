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
}
