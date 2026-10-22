using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Private atomic-owner admission/receipt/lookup adapter tests; synthetic transaction authority never proves physical target write linearization.</summary>
public sealed class DirectoryAtomicAppendTests
{
    private static DirectoryAtomicAppendRequest Request() => new(new CommandEnvelope("original-command", "tenant-a", "agents", "interaction-a", "Concrete.Create",
        "synthetic-command-bytes"u8.ToArray(), "correlation", null, "dedicated-private-worker", null), "epoch-1", DirectoryWriteKind.Create, 0,
        new AggregateIdentity("tenant-a", "agents", "directory-a"), "conversation-a", "permit-a", "effect-capability", 3, new string('A', 64), "retained-digest-v1");
    private static DirectoryAtomicAppendOutcome Accepted(DirectoryAtomicAppendRequest request) => new(request.Command.MessageId, DirectoryAtomicAppendClient.RequestDigest(request),
        DirectoryAtomicAppendState.Accepted, 1, 2, 9, "independent-original-atomic-receipt");
    private static IDirectoryAtomicAppendAuthority Authority()
    {
        var authority = Substitute.For<IDirectoryAtomicAppendAuthority>();
        authority.AuthorizeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.VerifyOutcomeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<DirectoryAtomicAppendOutcome>(), Arg.Any<CancellationToken>()).Returns(true); return authority;
    }
    /// <summary>Missing transaction implementation or current exact authority cannot reach a target effect.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task MissingAtomicOwnerOrPrivateAuthorityNeverAppends(bool ownerMissing)
    {
        var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority();
        if (!ownerMissing) { authority.AuthorizeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false); }
        (await new DirectoryAtomicAppendClient(TimeProvider.System, ownerMissing ? null : owner, authority).AppendAsync(Request(), TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unavailable);
        owner.ReceivedCalls().ShouldBeEmpty();
    }
    /// <summary>Missing/mismatched writer attribution and unauthenticated/self-reported source result never become accepted evidence.</summary>
    [Theory]
    [InlineData("ordinal")][InlineData("high-water")][InlineData("changed-intent")][InlineData("unverified")][InlineData("withdrawn")]
    public async Task ExactAuthenticatedAtomicReceiptIsMandatory(string vector)
    {
        var request = Request(); var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority(); var outcome = Accepted(request);
        outcome = vector switch { "ordinal" => outcome with { AcceptedAtAdmissionFenceOrdinal = -1 }, "high-water" => outcome with { AcceptedAtGuardHighWater = 0 },
            "changed-intent" => outcome with { RequestDigest = new string('B', 64) }, _ => outcome };
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(outcome);
        if (vector == "unverified") { authority.VerifyOutcomeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<DirectoryAtomicAppendOutcome>(), Arg.Any<CancellationToken>()).Returns(false); }
        if (vector == "withdrawn") { int calls = 0; authority.AuthorizeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => ++calls == 1); }
        (await new DirectoryAtomicAppendClient(TimeProvider.System, owner, authority).AppendAsync(request, TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unavailable);
    }
    /// <summary>Unknown append uses authenticated original lookup with stable scope/intent; no blind second transaction is issued.</summary>
    [Fact]
    public async Task UnknownResultRecoversOriginalExactLookupWithoutAnotherAppend()
    {
        var request = Request(); var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority(); var outcome = Accepted(request);
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new DirectoryAtomicAppendOutcome(outcome.OperationId, outcome.RequestDigest, DirectoryAtomicAppendState.Unknown, 0, 0, 0, null));
        owner.LookupAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(outcome);
        var client = new DirectoryAtomicAppendClient(TimeProvider.System, owner, authority);
        (await client.AppendAsync(request, TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unknown);
        (await client.LookupAsync(request, TestContext.Current.CancellationToken)).ShouldBe(outcome);
        await owner.Received(1).TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), outcome.RequestDigest, Arg.Any<CancellationToken>());
        var wrong = request with { EpochId = "restored-epoch" }; (await client.LookupAsync(wrong, TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unavailable);
    }
    /// <summary>Provider entry precedes controlled timeout/cancellation; original caller token wins and no late acceptance is released.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task StalledAtomicOwnerCannotRetainCallerOrReleaseLateEvidence(bool cancelCaller)
    {
        var clock = new AuthoritativeReadTimeProvider(); var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = new TaskCompletionSource<DirectoryAtomicAppendOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var caller = new CancellationTokenSource(); var reading = new DirectoryAtomicAppendClient(clock, owner, authority).AppendAsync(Request(), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (cancelCaller) { caller.Cancel(); var exception = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); exception.CancellationToken.ShouldBe(caller.Token); }
        else { clock.Advance(TimeSpan.FromSeconds(30)); (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unavailable); }
        pending.TrySetResult(Accepted(Request()));
    }
    /// <summary>The actual append guard assigns zero outside all deletion scopes; an independently verified committed result remains accepted.</summary>
    [Fact]
    public async Task OutOfScopeCommittedWriteAcceptsOrdinalZero()
    {
        var request = Request(); var state = new TenantGovernanceGuardState("tenant-a", "installed", 1, "epoch-1", "legacy", "writers", null, [], [], [], [], [], []);
        var facts = new GovernanceWriteFacts("tenant-a", "interaction-a", "conversation-a", "conversation-a", "tenant-a:directory:directory-a", "permit-a", "effect-capability", 3, DirectoryWriteKind.Create, "epoch-1", "source-proof");
        var command = new GovernanceGuardTransition("tenant-a", "write", GovernanceGuardOperation.AppendWrite, state.Revision, "epoch-1", "", null, facts, null, null, null, null, "", "");
        var now = DateTimeOffset.UtcNow;
        var evidence = new GovernanceGuardEvidence("tenant-a", "intent", new string('D', 64), "authority", "receipt", now, now.AddMinutes(1), [], [], [], "writers", "legacy", "zero", 0, "", "", [], "") { AppendResourceId = facts.AgentInteractionId };
        var reduced = Hexalith.EventStore.Server.Security.GovernanceScopeGuardReducer.Reduce(state, command, evidence, "intent");
        reduced.Receipt.Status.ShouldBe("Accepted"); reduced.Receipt.AcceptedAtAdmissionFenceOrdinal.ShouldBe(0);
        reduced.Receipt.AcceptedWriteResourceId.ShouldBe(facts.AgentInteractionId); reduced.Receipt.AcceptedWriteFacts.ShouldBe(facts);
        reduced.Receipt.AcceptedTargetMutationDigest.ShouldBe(evidence.TargetMutationDigest);
        var outcome = Accepted(request) with { AcceptedAtAdmissionFenceOrdinal = reduced.Receipt.AcceptedAtAdmissionFenceOrdinal, AcceptedAtGuardHighWater = reduced.Receipt.GuardHighWater };
        var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(outcome);
        (await new DirectoryAtomicAppendClient(TimeProvider.System, owner, Authority()).AppendAsync(request, TestContext.Current.CancellationToken)).ShouldBe(outcome);
    }

    /// <summary>Discarded captured request plaintext is cleared on every completed provider path; the caller's original bytes remain immutable.</summary>
    [Theory]
    [InlineData("accepted")][InlineData("denied")][InlineData("unknown")][InlineData("fault")][InlineData("withdrawn")]
    public async Task CompletedCapturedPayloadIsRetired(string vector)
    {
        var request = Request(); var original = request.Command.Payload.ToArray(); byte[]? captured = null;
        var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority(); int admissions = 0;
        authority.AuthorizeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured ??= call.Arg<DirectoryAtomicAppendRequest>().Command.Payload;
            captured.ShouldNotBeSameAs(request.Command.Payload); captured.ShouldBe(original);
            return vector != "denied" && (vector != "withdrawn" || ++admissions == 1);
        });
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<DirectoryAtomicAppendRequest>().Command.Payload.ShouldBe(original);
            if (vector == "fault") { throw new IOException("controlled owner fault"); }
            return vector == "unknown" ? Accepted(request) with { State = DirectoryAtomicAppendState.Unknown, CommittedTargetRevision = 0, AcceptedAtAdmissionFenceOrdinal = 0, AcceptedAtGuardHighWater = 0, AuthenticatedReceiptId = null } : Accepted(request);
        });
        var client = new DirectoryAtomicAppendClient(TimeProvider.System, owner, authority);
        if (vector == "fault") { await Should.ThrowAsync<IOException>(() => client.AppendAsync(request, TestContext.Current.CancellationToken)); }
        else
        {
            var result = await client.AppendAsync(request, TestContext.Current.CancellationToken);
            result.State.ShouldBe(vector == "accepted" ? DirectoryAtomicAppendState.Accepted : vector == "unknown" ? DirectoryAtomicAppendState.Unknown : DirectoryAtomicAppendState.Unavailable);
        }
        captured.ShouldNotBeNull(); captured.All(b => b == 0).ShouldBeTrue(); request.Command.Payload.ShouldBe(original);
    }

    /// <summary>Abandoned authority/owner borrowers retain unchanged input until their task ends, then retire it without another phase or effect.</summary>
    [Theory]
    [InlineData("admission", false)][InlineData("admission", true)]
    [InlineData("owner", false)][InlineData("owner", true)]
    [InlineData("verification", false)][InlineData("verification", true)]
    [InlineData("final-admission", false)][InlineData("final-admission", true)]
    public async Task AbandonedCapturedPayloadRetiresOnlyAfterBorrowerCompletion(string stage, bool expires)
    {
        var clock = new AuthoritativeReadTimeProvider(); var request = Request(); var original = request.Command.Payload.ToArray();
        var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority(); byte[]? captured = null; int admissions = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcome = new TaskCompletionSource<DirectoryAtomicAppendOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        authority.AuthorizeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured ??= call.Arg<DirectoryAtomicAppendRequest>().Command.Payload;
            if (stage == "admission" || stage == "final-admission" && ++admissions == 2) { entered.TrySetResult(); return allowed.Task; }
            return Task.FromResult(true);
        });
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<DirectoryAtomicAppendRequest>().Command.Payload;
            if (stage == "owner") { entered.TrySetResult(); return outcome.Task; }
            return Task.FromResult(Accepted(request));
        });
        authority.VerifyOutcomeAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<DirectoryAtomicAppendOutcome>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { if (stage == "verification") { entered.TrySetResult(); return allowed.Task; } return Task.FromResult(true); });
        using var caller = new CancellationTokenSource(); var reading = new DirectoryAtomicAppendClient(clock, owner, authority).AppendAsync(request, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        try
        {
            if (expires) { clock.Advance(TimeSpan.FromSeconds(30)); (await reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).State.ShouldBe(DirectoryAtomicAppendState.Unavailable); }
            else { caller.Cancel(); var error = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)); error.CancellationToken.ShouldBe(caller.Token); }
            captured.ShouldNotBeNull(); captured.ShouldBe(original); request.Command.Payload.ShouldBe(original);
            int ownerCalls = owner.ReceivedCalls().Count(); int authorityCalls = authority.ReceivedCalls().Count();
            allowed.TrySetResult(true); outcome.TrySetResult(Accepted(request));
            SpinWait.SpinUntil(() => captured.All(b => b == 0), TimeSpan.FromSeconds(2)).ShouldBeTrue();
            owner.ReceivedCalls().Count().ShouldBe(ownerCalls); authority.ReceivedCalls().Count().ShouldBe(authorityCalls); request.Command.Payload.ShouldBe(original);
            if (stage == "owner")
            {
                owner.LookupAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Accepted(request));
                var restored = new DirectoryAtomicAppendClient(TimeProvider.System, owner, Authority());
                (await restored.LookupAsync(request, TestContext.Current.CancellationToken)).ShouldBe(Accepted(request));
                await owner.Received(1).TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            }
        }
        finally { allowed.TrySetResult(true); outcome.TrySetResult(Accepted(request)); }
    }

    /// <summary>The actual capture path retires a copy allocated before a later malformed-extension failure.</summary>
    [Fact]
    public void CaptureFailureAfterCopyRetiresOwnedBytes()
    {
        var request = Request(); var original = request.Command.Payload.ToArray();
        request = request with { Command = request.Command with { Extensions = new Dictionary<string, string> { ["valid"] = "\uD800" } } };
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken, TimeProvider.System.GetTimestamp());
        var lifetime = new DirectoryAtomicAppendPayloadLifetime();
        try
        {
            Should.Throw<ArgumentException>(() => DirectoryAtomicAppendClient.Capture(request, deadline, lifetime));
            lifetime.Payload.ShouldNotBeNull(); lifetime.Payload.ShouldBe(original); lifetime.Payload.ShouldNotBeSameAs(request.Command.Payload);
        }
        finally { lifetime.Dispose(); }
        lifetime.Payload!.All(b => b == 0).ShouldBeTrue(); request.Command.Payload.ShouldBe(original);
        lifetime.Dispose(); lifetime.Payload.All(b => b == 0).ShouldBeTrue();
    }

    /// <summary>Optional causation retains strict 2048 UTF-8 bytes, with malformed/oversized denial before provider calls and exact original payload.</summary>
    [Theory]
    [InlineData("oversized")][InlineData("malformed")][InlineData("null")][InlineData("boundary")]
    public async Task OptionalCausationUsesExistingIdentityCarrier(string vector)
    {
        var request = Request(); string? causation = vector switch { "oversized" => new string('x', 2049), "malformed" => "\uD800", "boundary" => new string('é', 1024), _ => null };
        request = request with { Command = request.Command with { CausationId = causation } }; var original = request.Command.Payload.ToArray();
        var owner = Substitute.For<IAtomicDirectoryAppendOwner>(); var authority = Authority();
        owner.TryAppendAsync(Arg.Any<DirectoryAtomicAppendRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Accepted(request));
        var client = new DirectoryAtomicAppendClient(TimeProvider.System, owner, authority);
        if (vector is "oversized" or "malformed")
        {
            await Should.ThrowAsync<ArgumentException>(() => client.AppendAsync(request, TestContext.Current.CancellationToken));
            owner.ReceivedCalls().ShouldBeEmpty(); authority.ReceivedCalls().ShouldBeEmpty();
        }
        else
        {
            (await client.AppendAsync(request, TestContext.Current.CancellationToken)).ShouldBe(Accepted(request));
            await owner.Received(1).TryAppendAsync(Arg.Is<DirectoryAtomicAppendRequest>(r => r.Command.CausationId == causation), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        request.Command.CausationId.ShouldBe(causation); request.Command.Payload.ShouldBe(original);
    }

}
