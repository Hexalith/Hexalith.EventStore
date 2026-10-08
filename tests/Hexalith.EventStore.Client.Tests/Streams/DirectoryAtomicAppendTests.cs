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
        outcome = vector switch { "ordinal" => outcome with { AcceptedAtAdmissionFenceOrdinal = 0 }, "high-water" => outcome with { AcceptedAtGuardHighWater = 0 },
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
}
