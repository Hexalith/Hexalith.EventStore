using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual durable candidate uniqueness/retry/completion/restore source tests; ciphertext/writer/independent anchor ports are synthetic.</summary>
public sealed class InteractionOccurrenceRegistryActorTests
{
    /// <summary>Reservation commits before any sealed result; same exact retry keeps the original reference and sealed bytes across restart.</summary>
    [Fact]
    public async Task DurableUniqueReservationAndOriginalSealedRetrySurviveRestart()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); var reserved = await f.Actor.ReserveAsync(request);
        reserved.Status.ShouldBe(InteractionOccurrenceReservationStatus.Reserved); f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Single().Sealed.ShouldBeNull();
        var sealedResult = InteractionOccurrenceFixture.Sealed(); var retained = await f.Actor.RetainSealedAsync(request.Identity, sealedResult);
        sealedResult.PayloadBytes[0] = 0; retained.Record!.Sealed!.PayloadBytes[0].ShouldBe((byte)'{');
        var retry = await f.Actor.ReserveAsync(request with { ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" }); retry.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); retry.Record.Sealed!.PayloadBytes.ShouldBe(retained.Record.Sealed!.PayloadBytes);
        var saved = f.Backend.CommittedState.Single(); var restored = new InMemoryStateManager(); await restored.SetStateAsync(saved.Key,
            JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(JsonSerializer.Serialize(saved.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var lookup = await InteractionOccurrenceFixture.Create(restored, f.Authority).LookupAsync(request.Identity); lookup.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); lookup.Record.Sealed!.PayloadBytes.ShouldBe(retained.Record.Sealed.PayloadBytes);
        JsonSerializer.Serialize(saved.Value).ShouldNotContain("synthetic-intent"); JsonSerializer.Serialize(saved.Value).ShouldNotContain("DataEncryptionKey");
    }
    /// <summary>Unknown completion remains unreadable; only exact writer proof activates, and changed evidence/key reference cannot reclassify it.</summary>
    [Fact]
    public async Task WriterProofIsRequiredAndBoundToOriginalReference()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        f.Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "unknown", true)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await f.Actor.LookupAsync(request.Identity)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        f.Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        (await f.Actor.CompleteWriterAsync(request.Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW", "source-proof", true)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        var active = await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "source-proof", true); active.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "changed", false)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
    }
    /// <summary>Complete reference namespace rejects collision even across aliases; changed content/DigestKey/root version cannot reuse a source occurrence.</summary>
    [Fact]
    public async Task CollisionAndChangedIntentCannotAuthorizeEncryption()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(2))).Status.ShouldBe(InteractionOccurrenceReservationStatus.ReferenceCollision);
        (await f.Actor.ReserveAsync(request with { ContentIntentHmac = new string('B', 64) })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { DigestKeyVersion = "new-digest-v2" })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { Identity = request.Identity with { RootKeyVersion = "new-root-v2" } })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Count.ShouldBe(1);
    }
    /// <summary>Authoritative never-written proof permits only the next attempt with another globally unique reference; abandoned references remain used.</summary>
    [Fact]
    public async Task AbortedAttemptNeverReusesReferenceAndRequiresGapFreeSuccessor()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "authoritative-never-written", false);
        (await f.Actor.ReserveAsync(request)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await f.Actor.ReserveAsync(request with { ReservationAttemptOrdinal = 3, ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" })).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Actor.ReserveAsync(request with { ReservationAttemptOrdinal = 2 })).Status.ShouldBe(InteractionOccurrenceReservationStatus.ReferenceCollision);
        var next = request with { ReservationAttemptOrdinal = 2, ProposedKeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW" };
        (await f.Actor.ReserveAsync(next)).Record!.KeyReference.ShouldBe(next.ProposedKeyReference);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().Records.Count.ShouldBe(2);
    }
    /// <summary>Older durable rollback and changed installed epoch deny original lookup/new reservations; no reference/key can be revived.</summary>
    [Fact]
    public async Task IndependentMonotonicAnchorDeniesRollbackAndRestoredEpoch()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request); var old = f.Backend.CommittedState.Single();
        byte[] oldBytes = JsonSerializer.SerializeToUtf8Bytes(old.Value); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        var restored = new InMemoryStateManager(); await restored.SetStateAsync(old.Key, JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(oldBytes)!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var actor = InteractionOccurrenceFixture.Create(restored, f.Authority); (await actor.LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        (await actor.ReserveAsync(InteractionOccurrenceFixture.Request(2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Epoch = "epoch-2"; (await f.Actor.LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Backend.CommittedState.Single().Value.ShouldBeOfType<InteractionOccurrenceRegistrySnapshot>().EpochId.ShouldBe("epoch-1");
    }
    /// <summary>Missing/withdrawn exact current caller/root/writer authority never exposes metadata or authorizes a reserved reference.</summary>
    [Fact]
    public async Task MissingOrWithdrawnAuthorityFailsClosed()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request();
        (await InteractionOccurrenceFixture.Create(f.Backend).ReserveAsync(request)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable); f.Backend.CommittedState.ShouldBeEmpty();
        await f.Actor.ReserveAsync(request); f.Authority.AuthorizeOperationAsync(Arg.Any<InteractionOccurrenceIdentity>(), "Lookup", Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.LookupAsync(request.Identity)).Record.ShouldBeNull();
        f.Authority.AuthorizeReservationAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(2, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }
    /// <summary>Precommit save failure advances the independent anchor conservatively and closes availability; committed lost ack reuses the original reference only.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrLostAckReservationNeverUsesStagedCache(bool commitBeforeFault)
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); var manager = Substitute.For<IActorStateManager>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => f.Backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<InteractionOccurrenceRegistrySnapshot>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryGetStateAsync<InteractionOccurrenceRegistrySnapshot>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<InteractionOccurrenceRegistrySnapshot>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.SetStateAsync(call.Arg<string>(), call.Arg<InteractionOccurrenceRegistrySnapshot>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call => { if (commitBeforeFault) { await f.Backend.SaveStateAsync(call.Arg<CancellationToken>()).ConfigureAwait(false); } throw new HttpRequestException("Controlled candidate registry save fault."); });
        await Should.ThrowAsync<HttpRequestException>(() => InteractionOccurrenceFixture.Create(manager, f.Authority).ReserveAsync(request)); f.Head.ShouldBe(1);
        var retry = await f.Actor.ReserveAsync(request); retry.Status.ShouldBe(commitBeforeFault ? InteractionOccurrenceReservationStatus.Reserved : InteractionOccurrenceReservationStatus.Unavailable);
        if (commitBeforeFault) { retry.Record!.KeyReference.ShouldBe(request.ProposedKeyReference); f.Backend.CommittedState.Count.ShouldBe(1); }
        else { f.Backend.CommittedState.ShouldBeEmpty(); }
    }
    /// <summary>Existing reference bound denies a new occurrence before anchor/save while retaining exact original committed ciphertext lookup.</summary>
    [Fact]
    public async Task AtReferenceBoundRetainsOriginalOutcomeAndIndependentAnchor()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request();
        await f.Actor.ReserveAsync(request); await f.Actor.RetainSealedAsync(request.Identity, InteractionOccurrenceFixture.Sealed());
        var original = (await f.Actor.CompleteWriterAsync(request.Identity, request.ProposedKeyReference, "commit-proof", true)).Record!;
        var saved = f.Backend.CommittedState.Single(); var state = (InteractionOccurrenceRegistrySnapshot)saved.Value;
        var records = new List<InteractionOccurrenceRecord> { original };
        for (int n = 1; n < 10000; n++)
        {
            string reference = "01ARZ3NDEKTSV4RRF" + n.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
            var extra = InteractionOccurrenceFixture.Request(checked((ulong)n + 1), reference);
            records.Add(new(extra, reference, n + 3, InteractionOccurrenceWriterState.Reserved, null, null));
        }
        state = state with { Revision = 10002, Records = records.ToArray() }; f.Head = state.Revision; f.AnchorDigest = InteractionOccurrenceFixture.Digest(state);
        await f.Backend.SetStateAsync(saved.Key, state, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await f.Actor.ReserveAsync(InteractionOccurrenceFixture.Request(10001, "01ARZ3NDEKTSV4RRFFQ69G5FAW"))).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        f.Head.ShouldBe(10002); ((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Records.Count.ShouldBe(10000);
        var read = (await f.Actor.LookupAsync(request.Identity)).Record!; read.KeyReference.ShouldBe(original.KeyReference); read.Sealed!.PayloadBytes.ShouldBe(original.Sealed!.PayloadBytes);
    }
    /// <summary>Equal epoch/revision with divergent committed source identity is denied by the independent exact-state anchor.</summary>
    [Fact]
    public async Task SameRevisionDivergentRestoreCannotReleaseOriginalReference()
    {
        var f = new InteractionOccurrenceFixture(); var request = InteractionOccurrenceFixture.Request(); await f.Actor.ReserveAsync(request);
        var saved = f.Backend.CommittedState.Single(); var state = (InteractionOccurrenceRegistrySnapshot)saved.Value;
        var record = state.Records.Single(); var changed = record with { Request = record.Request with { ContentIntentHmac = new string('B', 64) } };
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, state with { Records = [changed] }, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await InteractionOccurrenceFixture.Create(restored, f.Authority).LookupAsync(request.Identity)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        ((InteractionOccurrenceRegistrySnapshot)restored.CommittedState.Single().Value).Revision.ShouldBe(f.Head);
    }

}
