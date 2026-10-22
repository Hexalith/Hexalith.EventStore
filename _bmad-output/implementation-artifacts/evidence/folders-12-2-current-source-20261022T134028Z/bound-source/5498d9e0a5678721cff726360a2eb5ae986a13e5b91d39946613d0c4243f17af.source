using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Exact-match, deny-by-default trusted effect command authority.</summary>
public sealed class ConfiguredTrustedEffectCommandAuthorityTests
{
    private static readonly TrustedEffectAuthorityRule _rule = new("reactor", "date-resume", "works", "ResumeWorkItem");

    /// <summary>No configured rule allows nothing.</summary>
    [Fact]
    public void EmptyRulesDenyEveryEffect()
    {
        var authority = new ConfiguredTrustedEffectCommandAuthority(Options.Create(new TrustedEffectAuthorityOptions()));
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();

        authority.IsAllowed(submission, context).ShouldBeFalse();
    }

    /// <summary>An exact workload, purpose, target domain, and command type match is allowed.</summary>
    [Fact]
    public void ExactRuleMatchAllowsEffect()
    {
        ConfiguredTrustedEffectCommandAuthority authority = CreateAuthority();
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();

        authority.IsAllowed(submission, context).ShouldBeTrue();
    }

    /// <summary>Changing any one bound coordinate denies the effect.</summary>
    [Theory]
    [InlineData("workload")]
    [InlineData("purpose")]
    [InlineData("target-domain")]
    [InlineData("command-type")]
    public void ChangingAnyBoundCoordinateDenies(string changed)
    {
        ConfiguredTrustedEffectCommandAuthority authority = CreateAuthority();
        (TrustedEffectSubmission submission, TrustedEffectContext context) = CreateEffect();
        (submission, context) = changed switch
        {
            "workload" => (submission, context with { Workload = "timer" }),
            "purpose" => (submission, context with { Purpose = "cascade-cancel" }),
            "target-domain" => (submission with { Identity = submission.Identity with { TargetDomain = "orders" } }, context),
            "command-type" => (submission with { CommandType = "CancelWorkItem" }, context),
            _ => throw new ArgumentOutOfRangeException(nameof(changed), changed, null),
        };

        authority.IsAllowed(submission, context).ShouldBeFalse();
    }

    private static ConfiguredTrustedEffectCommandAuthority CreateAuthority()
        => new(Options.Create(new TrustedEffectAuthorityOptions { Rules = [_rule] }));

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
