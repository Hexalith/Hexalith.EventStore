using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Streams;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.Http;
using Dapr.Actors;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Streams;

public sealed partial class SourcePublicationAuthorityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcknowledgedHistorySurvivesGrowthButLaterDispatchReprovesEveryConsecutiveReceipt(bool registerSource)
    {
        var fixture = await PreparedAsync(); var page = (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        var originalEntry = page.Entries.Single();
        var originalReceipt = new SourcePublicationAcknowledgement(originalEntry, fixture.Enrollment.DeliveryTarget, fixture.Enrollment.DeliveryAuthorityRevision, 9, "original");
        fixture.SourceReceipts[1] = originalReceipt; fixture.ReceiverReceipts[1] = originalReceipt;
        var original = (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", new SourcePublicationDispatchAdvance(page.Checkpoint, 0, page.Entries))).ShouldNotBeNull();
        var source = fixture.Sources.Values.Single(item => item.Identity.AggregateId == "a-with-signal");
        if (registerSource)
        {
            var identity = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "c-new-source");
            (await fixture.Primary.InvokeAsync<bool>(fixture.Scope, "register-source", new SourcePublicationSourceRegistration(fixture.Scope, 1, identity))).ShouldBeTrue();
            source = new(identity, 0, fixture.Clock.GetUtcNow(), [], "new")
            { NativeProof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAZ") };
        }
        var next = new StreamReadEvent(source.Head + 1, "DeletionApproved", "{}"u8.ToArray(), "json", 1, "event-next", "correlation", "cause", fixture.Clock.GetUtcNow(), "worker") { NativeEventDigest = new string('b', 64) };
        fixture.Sources[source.Identity.ActorId] = source with { Head = next.SequenceNumber, Events = source.Events.Append(next).ToArray(),
            NativeProof = NativeSourceProofCodec.ExtendDigest(source.NativeProof!, source.Identity, next.SequenceNumber, next.MessageId, next.NativeEventDigest) };
        fixture.Publications = fixture.Publications.Append(new("signal-next", source.Identity, next.SequenceNumber, next.MessageId, new string('B', 64))).ToArray();
        page = (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull(); page.Entries.Count.ShouldBe(2);
        (await fixture.ReadIndexAsync()).ShouldNotBeNull().DispatchProgress.ShouldBe(original);
        var nextEntry = page.Entries[1];
        var nextReceipt = new SourcePublicationAcknowledgement(nextEntry, fixture.Enrollment.DeliveryTarget, fixture.Enrollment.DeliveryAuthorityRevision, 10, "next");
        fixture.SourceReceipts[2] = nextReceipt; fixture.ReceiverReceipts[2] = nextReceipt;
        (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", new SourcePublicationDispatchAdvance(page.Checkpoint, 1, [nextEntry]))).ShouldBeNull();
        fixture.ReceiverReceipts.Remove(1);
        (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", new SourcePublicationDispatchAdvance(page.Checkpoint, 0, page.Entries))).ShouldBeNull();
        fixture.ReceiverReceipts[1] = originalReceipt;
        var current = (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", new SourcePublicationDispatchAdvance(page.Checkpoint, 0, page.Entries))).ShouldNotBeNull();
        current.AcknowledgedPrefix.ShouldBe(2); fixture.Recreate();
        (await fixture.ReadIndexAsync()).ShouldNotBeNull().DispatchProgress.ShouldBe(current);
    }

    [Fact]
    public async Task UncertainSecondSourceObservationCannotPersistPoison()
    {
        var fixture = await PreparedAsync(); (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        var current = (await fixture.ReadIndexAsync()).ShouldNotBeNull(); int reads = 0;
        fixture.Streams.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var identity = call.Arg<AggregateIdentity>();
            return identity.AggregateId == "a-with-signal" && ++reads == 2 ? new AuthoritativeStreamReadResult(null, "temporarily-unavailable")
                : new(SourcePublicationAuthorityFixture.Clone(fixture.Sources[identity.ActorId]), null);
        });
        (await fixture.Primary.InvokeAsync<bool>(fixture.Scope, "write-index", new SourcePublicationIndexWrite(current.Revision,
            current with { Revision = current.Revision + 1, PoisonCode = "publication-reference-conflict" }))).ShouldBeFalse();
        (await fixture.ReadIndexAsync()).ShouldNotBeNull().PoisonCode.ShouldBeNull();
        fixture.IndexBackend.CommittedState.Values.OfType<SourcePublicationIndexState>().Single().Revision.ShouldBe(current.Revision);
    }

    [Theory]
    [InlineData("source", false)]
    [InlineData("receiver", false)]
    [InlineData("source", true)]
    [InlineData("receiver", true)]
    public async Task NoncooperativeObservationTimesOutWithoutAdvancementAndReleasesThePrimaryTurn(string provider, bool callerCancellation)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow); var fixture = await PreparedAsync(clock);
        var page = (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverSource = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverReceipt = new TaskCompletionSource<SourcePublicationAcknowledgement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = page.Entries.Single(); var receipt = new SourcePublicationAcknowledgement(entry, fixture.Enrollment.DeliveryTarget, fixture.Enrollment.DeliveryAuthorityRevision, 9, "receipt");
        fixture.SourceReceipts[1] = receipt; fixture.ReceiverReceipts[1] = receipt;
        using var abort = new CancellationTokenSource();
        if (callerCancellation) fixture.HttpContexts.HttpContext = new DefaultHttpContext { RequestAborted = abort.Token };
        CancellationToken observedToken = default;
        if (provider == "source") fixture.Streams.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        { observedToken = call.Arg<CancellationToken>(); entered.TrySetResult(); return neverSource.Task; });
        else fixture.Acknowledgements.ReadReceiverAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call =>
        { observedToken = call.Arg<CancellationToken>(); entered.TrySetResult(); return neverReceipt.Task; });
        var pending = fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", new SourcePublicationDispatchAdvance(page.Checkpoint, 0, [entry]));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (callerCancellation) abort.Cancel(); else clock.Advance(TimeSpan.FromSeconds(31));
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        fixture.HttpContexts.HttpContext = null;
        (await fixture.ReadIndexAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldNotBeNull().DispatchProgress.ShouldBeNull();
        observedToken.IsCancellationRequested.ShouldBeTrue();
        neverSource.TrySetResult(new(null, "late")); neverReceipt.TrySetResult(null);
    }

    [Theory]
    [InlineData("arbitrary")]
    [InlineData("epoch-1:1")]
    public async Task DirectProducerCannotCommitFabricatedEmptyIndexAgainstNonemptyInventory(string authority)
    {
        var fixture = await PreparedAsync(); var original = RecoverableAnchoredState.Digest(fixture.JournalBackend.CommittedState);
        var candidate = new SourcePublicationIndexState(fixture.Scope, 1, authority, [], []);
        var transition = RecoverableAnchoredState.Prepare(fixture.Scope.ActorId + "|source-publications-v1", 0, 1, (SourcePublicationIndexState?)null, candidate);
        (await fixture.Journal.CallAsync<bool>(fixture.Enrollment, "transition-admit", transition)).ShouldBeFalse();
        (await fixture.Journal.CallAsync<bool>(fixture.Enrollment, "transition-commit", transition)).ShouldBeFalse();
        RecoverableAnchoredState.Digest(fixture.JournalBackend.CommittedState).ShouldBe(original);
    }

    [Fact]
    public async Task DirectProducerCanRetainQualifiedEmptyInventoryAndAnIndependentlyIssuedPartialPrefix()
    {
        var empty = new SourcePublicationAuthorityFixture(); (await empty.Journal.EnrollAsync(empty.Enrollment)).ShouldBeTrue(); (await empty.InstallAsync()).ShouldBeTrue();
        var state = new SourcePublicationIndexState(empty.Scope, 1, "epoch-1:1", [], []);
        var transition = RecoverableAnchoredState.Prepare(empty.Scope.ActorId + "|source-publications-v1", 0, 1, (SourcePublicationIndexState?)null, state);
        (await empty.Journal.CallAsync<bool>(empty.Enrollment, "transition-admit", transition)).ShouldBeTrue();
        (await empty.Journal.CallAsync<bool>(empty.Enrollment, "transition-commit", transition)).ShouldBeTrue();
        var partial = await PreparedAsync(); partial.LoseJournalResponse = "reconciliation"; (await Feed(partial).ReadAsync(partial.Scope)).Page.ShouldBeNull();
        var progress = (await partial.Journal.LookupAsync<SourcePublicationReconciliationProgress>(partial.Enrollment, "reconciliation-head")).ShouldNotBeNull();
        state = new(partial.Scope, 1, "epoch-1:1", [], []) { ReconciliationProgress = progress };
        transition = RecoverableAnchoredState.Prepare(partial.Scope.ActorId + "|source-publications-v1", 0, 1, (SourcePublicationIndexState?)null, state);
        (await partial.Journal.CallAsync<bool>(partial.Enrollment, "transition-admit", transition)).ShouldBeTrue();
        (await partial.Journal.CallAsync<bool>(partial.Enrollment, "transition-commit", transition)).ShouldBeTrue();
    }

    [Fact]
    public async Task DirectProducerCannotOmitPublicationsFromAnIndependentlyCompletedPrefix()
    {
        var fixture = await PreparedAsync();
        var proxy = fixture.Proxies.CreateActorProxy<ISourcePublicationIndexActor>(new ActorId(fixture.Scope.ActorId), SourcePublicationIndexActor.ActorTypeName);
        proxy.InvokeAuthenticatedAsync(Arg.Is<SourcePublicationAuthorityCall>(call => call.Method == "write-index"))
            .Returns(Task.FromResult<byte[]?>(JsonSerializer.SerializeToUtf8Bytes(false)));
        (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldBeNull();
        var retained = fixture.IndexBackend.CommittedState.Values.OfType<SourcePublicationIndexState>().Single();
        var complete = retained.ReconciliationProgress.ShouldNotBeNull(); complete.VerifiedSources.ShouldBe(complete.Sources.Count);
        var missing = retained with { Revision = retained.Revision + 1, Sources = complete.Sources, Entries = [] };
        var transition = RecoverableAnchoredState.Prepare(fixture.Scope.ActorId + "|source-publications-v1", retained.Revision, missing.Revision, retained, missing);
        (await fixture.Journal.CallAsync<bool>(fixture.Enrollment, "transition-admit", transition)).ShouldBeFalse();
        var valid = missing with { Entries = complete.Publications.Select((publication, index) => new SourcePublicationIndexEntry(index + 1L, publication)).ToArray() };
        transition = RecoverableAnchoredState.Prepare(fixture.Scope.ActorId + "|source-publications-v1", retained.Revision, valid.Revision, retained, valid);
        (await fixture.Journal.CallAsync<bool>(fixture.Enrollment, "transition-admit", transition)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenewalPreflightsRecordCountAndActualBase64ExpandedSerializationBeforeSaving(bool bytes)
    {
        var fixture = new SourcePublicationAuthorityFixture(); (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeTrue();
        SourcePublicationAuthorityState state;
        if (bytes)
        {
            state = new(fixture.Enrollment, [new("capacity-pad", [])]);
            int overhead = JsonSerializer.SerializeToUtf8Bytes(state).Length;
            int bodyLength = (RecoverableAnchoredState.MaximumPendingBytes - overhead) / 4 * 3;
            state = state with { Records = [new("capacity-pad", new byte[bodyLength])] };
        }
        else state = new(fixture.Enrollment, Enumerable.Range(0, 30000).Select(index => new SourcePublicationAuthorityRecord("pad-" + index, [])).ToArray());
        SourcePublicationAuthorityActor.CanPersist(state).ShouldBeTrue();
        await fixture.JournalBackend.SetStateAsync("source-publication-authority-v1", state); await fixture.JournalBackend.SaveStateAsync();
        string original = RecoverableAnchoredState.Digest(state); var previous = fixture.Enrollment;
        fixture.Enrollment = previous with { DeliveryTarget = "successor" };
        (await fixture.Journal.RenewEnrollmentAsync(new(previous.Epoch, RecoverableAnchoredState.Digest(previous), fixture.Enrollment))).ShouldBeFalse();
        RecoverableAnchoredState.Digest(fixture.JournalBackend.CommittedState.Values.OfType<SourcePublicationAuthorityState>().Single()).ShouldBe(original);
    }
}
