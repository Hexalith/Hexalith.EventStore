using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Fail-closed effect admission tests.</summary>
public sealed class TrustedEffectAdmissionPolicyTests
{
    /// <summary>A missing retention gate denies even a signed and authorized request.</summary>
    [Fact]
    public async Task MissingRetentionGateDeniesBeforeReceiptAccess()
    {
        var adapters = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var verifier = Substitute.For<ITrustedEffectDelegationVerifier>();
        var authority = Substitute.For<ITrustedEffectCommandAuthority>();
        var policy = new TrustedEffectAdmissionPolicy(adapters, verifier, authority);
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();

        await Should.ThrowAsync<InvalidOperationException>(() => policy.AdmitAsync(submission, context));
        _ = adapters.DidNotReceive().Resolve(Arg.Any<SubmitCommand>());
    }

    /// <summary>Client-selected keys cannot replace the tuple-derived identifier.</summary>
    [Fact]
    public async Task ForgedMessageIdDeniesBeforeSemanticAdapter()
    {
        var adapters = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var verifier = Substitute.For<ITrustedEffectDelegationVerifier>();
        var authority = Substitute.For<ITrustedEffectCommandAuthority>();
        var retention = Substitute.For<ITrustedEffectRetentionGate>();
        var audit = Substitute.For<ITrustedEffectAuditSink>();
        var policy = new TrustedEffectAdmissionPolicy(adapters, verifier, authority, retention, audit);
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();

        await Should.ThrowAsync<InvalidOperationException>(
            () => policy.AdmitAsync(submission with { MessageId = "wrk-forged" }, context));
        _ = adapters.DidNotReceive().Resolve(Arg.Any<SubmitCommand>());
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Disposition == "denied"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Semantic digest comes from the registered adapter and must pass all gates.</summary>
    [Fact]
    public async Task AuthorizedEffectUsesServerCanonicalIntent()
    {
        var adapters = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var verifier = Substitute.For<ITrustedEffectDelegationVerifier>();
        var authority = Substitute.For<ITrustedEffectCommandAuthority>();
        var retention = Substitute.For<ITrustedEffectRetentionGate>();
        var audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();
        _ = adapters.Resolve(Arg.Any<SubmitCommand>()).Returns(new TrustedIdempotencyDescriptor(
            "adapter", "operation", 1, [1, 2, 3], IdempotencyReplayRetentionTier.Mutation));
        _ = authority.IsAllowed(submission, context).Returns(true);
        var policy = new TrustedEffectAdmissionPolicy(adapters, verifier, authority, retention, audit);

        TrustedEffectAdmission result = await policy.AdmitAsync(submission, context);

        result.SemanticDigest.Length.ShouldBe(52);
        _ = verifier.Received(1).VerifyAsync(
            submission, context, result.SemanticDigest, Arg.Any<CancellationToken>());
        _ = retention.Received(1).ValidateAsync(submission.Identity, Arg.Any<CancellationToken>());
        string expectedEffectId = EffectIdentityCodec.ComputeEffectId(submission.Identity);
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Disposition == "authorized"
                && record.Tenant == "tenant-a" && record.EffectId == expectedEffectId),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<TrustedEffectAuditRecord>(record => record.Disposition == "attempted"
                && record.EffectId == expectedEffectId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>An unavailable append-only sink prevents an otherwise authorized submission.</summary>
    [Fact]
    public async Task AuditFailurePreventsAuthorizedSubmission()
    {
        var adapters = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var verifier = Substitute.For<ITrustedEffectDelegationVerifier>();
        var authority = Substitute.For<ITrustedEffectCommandAuthority>();
        var retention = Substitute.For<ITrustedEffectRetentionGate>();
        var audit = Substitute.For<ITrustedEffectAuditSink>();
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();
        _ = adapters.Resolve(Arg.Any<SubmitCommand>()).Returns(new TrustedIdempotencyDescriptor(
            "adapter", "operation", 1, [1], IdempotencyReplayRetentionTier.Mutation));
        _ = authority.IsAllowed(submission, context).Returns(true);
        _ = audit.AppendAsync(Arg.Any<TrustedEffectAuditRecord>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("audit unavailable"));
        var policy = new TrustedEffectAdmissionPolicy(adapters, verifier, authority, retention, audit);

        await Should.ThrowAsync<IOException>(() => policy.AdmitAsync(submission, context));
        _ = retention.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default);
    }

    private static (TrustedEffectSubmission, TrustedEffectContext) CreateEffect()
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 1,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        return (
            new TrustedEffectSubmission(identity, "ResumeWorkItem", [123, 125], messageId, messageId),
            new TrustedEffectContext("reactor", "date-resume", "source-event", "signed-token"));
    }
}
