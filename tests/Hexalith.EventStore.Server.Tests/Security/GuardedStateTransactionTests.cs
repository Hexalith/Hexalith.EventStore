using Dapr.Client;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual Dapr transaction source over a serialized all-or-none model; real backend partition/ETag/restore qualification remains separate.</summary>
public sealed class GuardedStateTransactionTests
{
    /// <summary>Lost acknowledgement and exact restart lookup retain one joint commit, immutable original receipt and source creation.</summary>
    [Fact]
    public async Task LostAcknowledgementUsesOriginalJointReceiptWithoutSecondCommit()
    {
        var fixture = new GuardedTransactionFixture { LoseAcknowledgement = true }; var request = fixture.Request();
        var first = await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken);
        first.Status.ShouldBe(GuardedStateCommitStatus.Committed); first.Receipt!.GuardRevision.ShouldBe(2);
        var restarted = new DaprGuardedStateTransaction(fixture.Client, TimeProvider.System, fixture.Authority);
        (await restarted.CommitAsync(request, TestContext.Current.CancellationToken)).ShouldBe(first);
        (await restarted.LookupAsync(request, TestContext.Current.CancellationToken)).ShouldBe(first);
        fixture.TransactionCalls.ShouldBe(1); fixture.CommittedTransactions.ShouldBe(1); fixture.Stored.Count.ShouldBe(3);
        (await restarted.CommitAsync(fixture.Request(value: "changed-sealed-content"), TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Conflict);
        fixture.TransactionCalls.ShouldBe(1);
    }

    /// <summary>Two writers staging from the same guard ETag cannot both accept; losing the compare writes neither target nor outcome.</summary>
    [Fact]
    public async Task ConcurrentGuardAndTargetCreatesHaveOneAtomicWinner()
    {
        var fixture = new GuardedTransactionFixture(); int entered = 0; var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeTransaction = async () => { if (Interlocked.Increment(ref entered) == 2) { both.TrySetResult(); } await both.Task.ConfigureAwait(false); };
        var first = fixture.Owner.CommitAsync(fixture.Request("effect-a", "source-a"), TestContext.Current.CancellationToken);
        var second = fixture.Owner.CommitAsync(fixture.Request("effect-b", "source-b"), TestContext.Current.CancellationToken);
        var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        outcomes.Count(result => result.Status == GuardedStateCommitStatus.Committed).ShouldBe(1);
        outcomes.Count(result => result.Status == GuardedStateCommitStatus.Unknown).ShouldBe(1);
        fixture.CommittedTransactions.ShouldBe(1); fixture.Stored.Count.ShouldBe(3);
        fixture.Stored.ContainsKey(fixture.Key("source-a")).ShouldNotBe(fixture.Stored.ContainsKey(fixture.Key("source-b")));
    }

    /// <summary>Absent/preinstalled guard, empty ETag, wrong tenant and missing qualification cannot create any source or outcome.</summary>
    [Theory]
    [InlineData("missing-guard")][InlineData("missing-etag")][InlineData("foreign")][InlineData("unqualified")][InlineData("guard-absence")]
    public async Task UnqualifiedOrUnconditionalGuardNeverExecutesTransaction(string vector)
    {
        var fixture = new GuardedTransactionFixture(); var request = fixture.Request();
        if (vector == "missing-guard") { fixture.Stored.Clear(); }
        if (vector == "missing-etag")
        {
            fixture.Client.GetStateAndETagAsync<GuardedStateCell>(fixture.Target.ComponentName, fixture.Key(fixture.Target.GuardCellId), Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns((new(fixture.Target.TenantId,
                    fixture.Target.InstallationId, fixture.Target.GuardCellId, 1, "initial-guard"u8.ToArray()), ""));
        }
        if (vector == "foreign") { request = request with { TenantId = "tenant-foreign" }; }
        if (vector == "guard-absence") { request = request with { Guard = request.Guard with { ExpectedRevision = 0, ExpectedDigest = GuardedTransactionFixture.Hash([]) } }; }
        var owner = vector == "unqualified" ? new DaprGuardedStateTransaction(fixture.Client, TimeProvider.System) : fixture.Owner;
        (await owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);
        fixture.TransactionCalls.ShouldBe(0);
    }

    /// <summary>Current authority withdrawal during delayed original receipt validation blocks exact replay success.</summary>
    [Fact]
    public async Task ExactReplayReconfirmsAuthorityAfterDelayedReceipt()
    {
        var fixture = new GuardedTransactionFixture(); var request = fixture.Request();
        (await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Committed);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ReceiptRead = async receipt => { entered.TrySetResult(); await release.Task.ConfigureAwait(false); return receipt; };
        var replay = fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fixture.Authority.GetCurrentAsync(fixture.Target.TenantId, Arg.Any<CancellationToken>()).Returns(fixture.Target with { AuthorityRevision = "withdrawn-original-authority" });
        release.SetResult();
        (await replay.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);
        fixture.TransactionCalls.ShouldBe(1);
    }

    /// <summary>Missing old guard after restore is independently denied rather than accepted as new source absence.</summary>
    [Fact]
    public async Task RestoredMissingStateCannotRecreateOriginalGuardOrTarget()
    {
        var fixture = new GuardedTransactionFixture(); var request = fixture.Request();
        (await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Committed);
        fixture.Stored.Clear();
        (await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);
        fixture.TransactionCalls.ShouldBe(1); fixture.Stored.ShouldBeEmpty();
    }

    /// <summary>Suspended authority can change only its own detached input; digest, persisted target and original receipt describe the owner's original bytes.</summary>
    [Fact]
    public async Task AuthorityCannotMutateOwnerSnapshotOrReceiptAfterRetainingInput()
    {
        var fixture = new GuardedTransactionFixture(); var request = fixture.Request(); GuardedStateCommitRequest? retained = null;
        fixture.Authority.AuthorizeAsync(fixture.Target, Arg.Any<GuardedStateCommitRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            retained = call.Arg<GuardedStateCommitRequest>(); await Task.Yield(); retained.Targets[0].NextValue[0] ^= 255; return true;
        });
        var result = await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GuardedStateCommitStatus.Committed); retained.ShouldNotBeSameAs(request);
        JsonSerializer.Deserialize<GuardedStateCell>(fixture.Stored[fixture.Key("source-a")])!.Value.ShouldBe(request.Targets[0].NextValue);
        (await fixture.Owner.LookupAsync(request, TestContext.Current.CancellationToken)).ShouldBe(result);
    }

    /// <summary>Exclusive target authority expiry during the final suspended authority await denies commit/read/lookup despite the unexpired whole-call budget.</summary>
    [Theory]
    [InlineData("commit")][InlineData("read")][InlineData("lookup")]
    public async Task FinalAuthorityAwaitCannotReleaseExpiredTarget(string operation)
    {
        var fixture = new GuardedTransactionFixture(); var request = fixture.Request();
        (await fixture.Owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Committed);
        var clock = new RetainedHistoryTimeProvider(fixture.Now.AddSeconds(59)); var owner = new DaprGuardedStateTransaction(fixture.Client, clock, fixture.Authority);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int calls = 0;
        async Task<bool> Final()
        {
            if (Interlocked.Increment(ref calls) == 2) { entered.SetResult(); await release.Task.ConfigureAwait(false); }
            return true;
        }
        Task<bool> result;
        if (operation == "commit")
        {
            fixture.Authority.AuthorizeAsync(fixture.Target, Arg.Any<GuardedStateCommitRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Final());
            result = CheckCommit();
        }
        else if (operation == "read")
        {
            fixture.Authority.AuthorizeReadAsync(fixture.Target, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Final()); result = CheckRead();
        }
        else
        {
            fixture.Authority.AuthorizeLookupAsync(fixture.Target, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Final()); result = CheckLookup();
        }
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromSeconds(1)); release.SetResult();
        (await result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeTrue(); fixture.TransactionCalls.ShouldBe(1);
        async Task<bool> CheckCommit() => (await owner.CommitAsync(request, TestContext.Current.CancellationToken)).Status == GuardedStateCommitStatus.Unavailable;
        async Task<bool> CheckRead() => await owner.ReadGuardAsync(fixture.Target.TenantId, TestContext.Current.CancellationToken) is null;
        async Task<bool> CheckLookup() => (await owner.LookupByIntentAsync(fixture.Target.TenantId, request.OperationId, GuardedTransactionFixture.Hash("intent"u8.ToArray()), TestContext.Current.CancellationToken)).Status == GuardedStateCommitStatus.Unavailable;
    }

    /// <summary>No-effect compares must advance backend CAS generation even while preserving logical guard bytes; concurrent same-id outcomes cannot overwrite the original.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task NoEffectRaceCannotOverwriteOriginalOutcomeOrDelayedSourceEffect(bool sourceEffect)
    {
        var fixture = new GuardedTransactionFixture(); var original = fixture.Request("same-operation");
        var first = original with { Guard = original.Guard with { NextValue = "initial-guard"u8.ToArray() }, Targets = [], CompareGuardWithoutMutation = true,
            LogicalIntentDigest = GuardedTransactionFixture.Hash("logical-intent"u8.ToArray()), Outcome = "original-no-effect"u8.ToArray() };
        var second = sourceEffect ? original with { LogicalIntentDigest = first.LogicalIntentDigest, Outcome = "original-source-effect"u8.ToArray() }
            : first with { Outcome = "competing-no-effect"u8.ToArray() };
        int entered = 0; var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeExactTransaction = async operations =>
        {
            var receipt = System.Text.Json.JsonSerializer.Deserialize<GuardedStateCommitReceipt>(operations[^1].Value.ToArray())!;
            if (Interlocked.Increment(ref entered) == 2) { both.SetResult(); }
            await both.Task.ConfigureAwait(false);
            if (!receipt.Outcome.AsSpan().SequenceEqual(first.Outcome)) { await releaseSecond.Task.ConfigureAwait(false); }
        };
        var firstPending = fixture.Owner.CommitAsync(first, TestContext.Current.CancellationToken); var secondPending = fixture.Owner.CommitAsync(second, TestContext.Current.CancellationToken);
        var firstResult = await firstPending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); releaseSecond.SetResult();
        var results = new[] { firstResult, await secondPending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken) };
        fixture.CommittedTransactions.ShouldBe(1);
        results.Count(value => value.Status == GuardedStateCommitStatus.Committed).ShouldBe(1);
        var winner = results.Single(value => value.Status == GuardedStateCommitStatus.Committed);
        var restart = await fixture.Owner.LookupByIntentAsync(fixture.Target.TenantId, first.OperationId, first.LogicalIntentDigest, TestContext.Current.CancellationToken);
        restart.Status.ShouldBe(winner.Status); restart.Receipt!.RequestDigest.ShouldBe(winner.Receipt!.RequestDigest); restart.Receipt.Outcome.ShouldBe(winner.Receipt.Outcome);
    }
}
