using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Streams;

public sealed class SourcePublicationAuthorityTests
{
    [Fact]
    public async Task MissingEnrollmentOrCurrentEvidenceDeniesAndLegacyActorCallsHaveNoAuthority()
    {
        var fixture = new SourcePublicationAuthorityFixture();
        (await fixture.InstallAsync()).ShouldBeFalse(); fixture.JournalBackend.CommittedState.ShouldBeEmpty();
        fixture.Current = false; (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeFalse();
        fixture.Current = true; (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeTrue();
        (await fixture.InstallAsync()).ShouldBeTrue();
        (await fixture.Namespace.ReadAsync(fixture.Scope)).ShouldBeNull();
        (await fixture.Index.ReadAsync(fixture.Scope)).ShouldBeNull();
        var original = RecoverableAnchoredState.Digest(fixture.NamespaceBackend.CommittedState);
        fixture.Current = false; (await fixture.ReadNamespaceAsync()).ShouldBeNull();
        RecoverableAnchoredState.Digest(fixture.NamespaceBackend.CommittedState).ShouldBe(original);
    }

    [Theory]
    [InlineData("transition-admit")]
    [InlineData("transition-commit")]
    public async Task LostAuthorityResponseAndRestartRecoverTheSameOriginalRoster(string lostMethod)
    {
        var fixture = new SourcePublicationAuthorityFixture();
        (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeTrue();
        fixture.LoseJournalResponse = lostMethod;
        await Should.ThrowAsync<HttpRequestException>(() => fixture.InstallAsync());
        fixture.Recreate();
        if (lostMethod == "transition-admit") (await fixture.InstallAsync()).ShouldBeTrue();
        var roster = (await fixture.ReadNamespaceAsync()).ShouldNotBeNull(); roster.Revision.ShouldBe(1);
        var journal = fixture.JournalBackend.CommittedState.Values.OfType<SourcePublicationAuthorityState>().Single();
        journal.Records.Count(item => item.Key.StartsWith("admitted:", StringComparison.Ordinal)).ShouldBe(1);
        journal.Records.Count(item => item.Key.StartsWith("transition:", StringComparison.Ordinal)).ShouldBe(1);
        fixture.NamespaceBackend.CommittedState.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConditionalSuccessorRetainsJournalAndRegistersAfterReceiverAndEpochRollover()
    {
        var fixture = new SourcePublicationAuthorityFixture();
        (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeTrue(); (await fixture.InstallAsync()).ShouldBeTrue();
        var first = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "first");
        (await fixture.Primary.InvokeAsync<bool>(fixture.Scope, "register-source", new SourcePublicationSourceRegistration(fixture.Scope, 1, first))).ShouldBeTrue();
        var original = fixture.Enrollment;
        fixture.Enrollment = original with { DeliveryTarget = "receiver-v2", DeliveryAuthorityRevision = "delivery-2" };
        var change = new SourcePublicationAuthorityEnrollmentChange(original.Epoch, RecoverableAnchoredState.Digest(original), fixture.Enrollment);
        (await fixture.Journal.RenewEnrollmentAsync(change)).ShouldBeTrue();
        (await fixture.Journal.RenewEnrollmentAsync(change)).ShouldBeTrue();
        fixture.Recreate();
        var second = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "second");
        (await fixture.Primary.InvokeAsync<bool>(fixture.Scope, "register-source", new SourcePublicationSourceRegistration(fixture.Scope, 2, second))).ShouldBeTrue();
        original = fixture.Enrollment; fixture.Enrollment = original with { Epoch = "epoch-2" };
        (await fixture.Journal.RenewEnrollmentAsync(new(original.Epoch, RecoverableAnchoredState.Digest(original), fixture.Enrollment))).ShouldBeTrue();
        fixture.Recreate();
        var third = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "third");
        (await fixture.Primary.InvokeAsync<bool>(fixture.Scope, "register-source", new SourcePublicationSourceRegistration(fixture.Scope, 3, third))).ShouldBeTrue();
        var roster = (await fixture.ReadNamespaceAsync()).ShouldNotBeNull(); roster.Revision.ShouldBe(4); roster.AuthorityRevision.ShouldBe("epoch-2");
        roster.Sources.ShouldBe(new[] { first, second, third });
        (await fixture.Authority.AuthorizeAsync(roster)).ShouldNotBeNull();
        var journal = (await fixture.Journal.ReadAsync(fixture.Enrollment)).ShouldNotBeNull();
        journal.Records.Count(item => item.Key.StartsWith("transition:", StringComparison.Ordinal)).ShouldBe(4);
        journal.Records.Count(item => item.Key.StartsWith("enrollment-successor:", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public async Task CompleteInventoryIncludesZeroAgentSourcesAndOnlyIndependentConsecutiveReceiptsAdvance()
    {
        var fixture = await PreparedAsync(); var feed = Feed(fixture);
        var page = (await feed.ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        page.Checkpoint.Sources.Count.ShouldBe(2); page.Checkpoint.Sources.ShouldContain(item => item.Head == 0);
        page.Entries.Count.ShouldBe(1);
        var entry = page.Entries.Single();
        var receipt = new SourcePublicationAcknowledgement(entry, fixture.Enrollment.DeliveryTarget, fixture.Enrollment.DeliveryAuthorityRevision, 9, "original-receipt");
        fixture.SourceReceipts[1] = receipt;
        var advance = new SourcePublicationDispatchAdvance(page.Checkpoint, 0, [entry]);
        (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", advance)).ShouldBeNull();
        (await fixture.ReadIndexAsync()).ShouldNotBeNull().DispatchProgress.ShouldBeNull();
        fixture.ReceiverReceipts[1] = receipt;
        var progress = (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "advance-dispatch", advance)).ShouldNotBeNull();
        progress.AcknowledgedPrefix.ShouldBe(1);
        fixture.Recreate();
        var recovered = (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "read-dispatch", page.Checkpoint)).ShouldNotBeNull();
        recovered.ShouldBe(progress);
        var old = fixture.Enrollment; fixture.Enrollment = old with { DeliveryTarget = "receiver-v2", DeliveryAuthorityRevision = "delivery-2" };
        (await fixture.Journal.RenewEnrollmentAsync(new(old.Epoch, RecoverableAnchoredState.Digest(old), fixture.Enrollment))).ShouldBeTrue();
        fixture.Recreate();
        (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "read-dispatch", page.Checkpoint)).ShouldBe(progress);
        fixture.ReceiverReceipts[1] = receipt with { ProtectedDeletionRevision = 10 };
        (await fixture.Primary.InvokeAsync<SourcePublicationDispatchProgress>(fixture.Scope, "read-dispatch", page.Checkpoint)).ShouldBeNull();
        (await fixture.ReadIndexAsync()).ShouldNotBeNull().DispatchProgress.ShouldBe(progress);
    }

    [Fact]
    public async Task LostReconciliationIssuanceRecoversOriginalInsteadOfMintingAnotherProof()
    {
        var fixture = await PreparedAsync(); fixture.LoseJournalResponse = "reconciliation";
        (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldBeNull();
        var original = (await fixture.Journal.LookupAsync<SourcePublicationReconciliationProgress>(fixture.Enrollment, "reconciliation-head")).ShouldNotBeNull();
        fixture.Recreate();
        (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        var retained = (await fixture.Journal.LookupAsync<SourcePublicationReconciliationProgress>(fixture.Enrollment, "reconciliation:" + original.ReceiptId)).ShouldNotBeNull();
        RecoverableAnchoredState.Digest(retained).ShouldBe(RecoverableAnchoredState.Digest(original));
        var state = (await fixture.ReadIndexAsync()).ShouldNotBeNull(); state.Entries.Count.ShouldBe(1); state.ReconciliationProgress!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task RolledBackPrimaryCannotReleaseAnOlderIndex()
    {
        var fixture = await PreparedAsync(); (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldNotBeNull();
        await fixture.IndexBackend.TryRemoveStateAsync("source-publications-v1"); await fixture.IndexBackend.SaveStateAsync(); fixture.Recreate();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReadIndexAsync());
        (await Feed(fixture).ReadAsync(fixture.Scope)).Page.ShouldBeNull();
        fixture.IndexBackend.CommittedState.ShouldBeEmpty();
        fixture.JournalBackend.CommittedState.Values.OfType<SourcePublicationAuthorityState>().Single().Records.ShouldContain(item => item.Key.StartsWith("transition:", StringComparison.Ordinal));
    }

    private static async Task<SourcePublicationAuthorityFixture> PreparedAsync()
    {
        var fixture = new SourcePublicationAuthorityFixture();
        var first = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "a-with-signal");
        var zero = new AggregateIdentity(fixture.Scope.Tenant, fixture.Scope.Domain, "z-zero-agent");
        fixture.Enrollment = fixture.Enrollment with { InitialSources = [first, zero], CommandSources = [first, zero] };
        var native = NativeSourceProofCodec.CreateSeed(first, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var item = new StreamReadEvent(1, "DeletionApproved", "{}"u8.ToArray(), "json", 1, "event-1", "correlation", "cause", DateTimeOffset.UtcNow, "worker") { NativeEventDigest = new string('a', 64) };
        native = NativeSourceProofCodec.ExtendDigest(native, first, 1, item.MessageId, item.NativeEventDigest);
        fixture.Sources[first.ActorId] = new(first, 1, DateTimeOffset.UtcNow, [item], "source-observation") { NativeProof = native };
        fixture.Sources[zero.ActorId] = new(zero, 0, DateTimeOffset.UtcNow, [], "empty-observation") { NativeProof = NativeSourceProofCodec.CreateSeed(zero, "01ARZ3NDEKTSV4RRFFQ69G5FAW") };
        fixture.Publications = [new("signal-1", first, 1, item.MessageId, new string('A', 64))];
        fixture.Recreate();
        (await fixture.Journal.EnrollAsync(fixture.Enrollment)).ShouldBeTrue(); (await fixture.InstallAsync()).ShouldBeTrue(); return fixture;
    }
    private static SourcePublicationFeed Feed(SourcePublicationAuthorityFixture fixture)
    {
        var source = Substitute.For<ISourcePublicationNamespaceSource>();
        source.ReadAsync(fixture.Scope, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            var roster = await fixture.ReadNamespaceAsync(); if (roster is null) return null;
            var grant = await fixture.Authority.AuthorizeAsync(roster); if (grant is null) return null;
            return new SourcePublicationCut(fixture.Scope, grant.AuthorityRevision + ":" + roster.Revision,
                DateTimeOffset.UtcNow, grant.ValidUntil, roster.Sources.Select(identity => new SourcePublicationHead(identity, fixture.Sources[identity.ActorId].Head)
                    { NativeProof = fixture.Sources[identity.ActorId].NativeProof }).ToArray(), true);
        });
        return new(source, fixture.Streams, fixture.Projector, new DaprSourcePublicationIndexStore(fixture.Proxies, fixture.Primary), TimeProvider.System);
    }
}
