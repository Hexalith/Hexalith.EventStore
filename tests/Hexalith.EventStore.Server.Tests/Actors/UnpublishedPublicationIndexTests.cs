
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Story 4.4: the fixed-key publication recovery index. Every branch of <c>TryAdd</c>,
/// <c>TryRemove</c> and <c>Prune</c> is exercised directly so no term in the actor's guards is
/// merely defensive.
/// </summary>
public class UnpublishedPublicationIndexTests {
    private static UnpublishedPublicationEntry Entry(string messageId, string correlationId = "corr-1")
        => new(messageId, correlationId, DateTimeOffset.UtcNow);

    [Fact]
    public void StateKey_IsTheStableFixedKey() => UnpublishedPublicationIndex.StateKey.ShouldBe("publication-index");

    [Fact]
    public void Empty_HasNoEntries() => UnpublishedPublicationIndex.Empty.Entries.ShouldBeEmpty();

    [Fact]
    public void Entries_NullCollection_IsNormalizedToEmpty() => new UnpublishedPublicationIndex(null!).Entries.ShouldBeEmpty();

    [Fact]
    public void TryAdd_BelowCapacity_TracksTheEntry() {
        PublicationIndexAddOutcome outcome = UnpublishedPublicationIndex.Empty.TryAdd(
            Entry("msg-1"), maxEntries: 2, out UnpublishedPublicationIndex updated);

        outcome.ShouldBe(PublicationIndexAddOutcome.Added);
        updated.Entries.Count.ShouldBe(1);
        updated.Contains("msg-1").ShouldBeTrue();
    }

    [Fact]
    public void TryAdd_DuplicateMessageId_RefreshesWithoutConsumingCapacity() {
        var index = new UnpublishedPublicationIndex([Entry("msg-1", "corr-old")]);

        PublicationIndexAddOutcome outcome = index.TryAdd(
            Entry("msg-1", "corr-new"), maxEntries: 1, out UnpublishedPublicationIndex updated);

        outcome.ShouldBe(PublicationIndexAddOutcome.Added);
        updated.Entries.Count.ShouldBe(1);
        updated.Entries[0].CorrelationId.ShouldBe("corr-new");
    }

    [Fact]
    public void TryAdd_AtCapacity_ReportsRefusalToTheCallerAndLeavesTheIndexUnchanged() {
        var index = new UnpublishedPublicationIndex([Entry("msg-1")]);

        PublicationIndexAddOutcome outcome = index.TryAdd(
            Entry("msg-2"), maxEntries: 1, out UnpublishedPublicationIndex updated);

        // Distinct from InvalidEntry: only this one is an operational backpressure condition.
        outcome.ShouldBe(PublicationIndexAddOutcome.AtCapacity);
        updated.ShouldBeSameAs(index);
        updated.Contains("msg-2").ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "corr-1")]
    [InlineData("   ", "corr-1")]
    [InlineData("msg-1", "")]
    [InlineData("msg-1", "   ")]
    public void TryAdd_MalformedEntry_IsRefused(string messageId, string correlationId) {
        PublicationIndexAddOutcome outcome = UnpublishedPublicationIndex.Empty.TryAdd(
            Entry(messageId, correlationId), maxEntries: 8, out UnpublishedPublicationIndex updated);

        // A data defect, never reported to the caller as "too many pending commands".
        outcome.ShouldBe(PublicationIndexAddOutcome.InvalidEntry);
        updated.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void TryRemove_KnownMessageId_RemovesOnlyThatEntry() {
        var index = new UnpublishedPublicationIndex([Entry("msg-1"), Entry("msg-2")]);

        bool removed = index.TryRemove("msg-1", out UnpublishedPublicationIndex updated);

        removed.ShouldBeTrue();
        updated.Contains("msg-1").ShouldBeFalse();
        updated.Contains("msg-2").ShouldBeTrue();
    }

    [Theory]
    [InlineData("msg-unknown")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryRemove_UnknownOrBlankMessageId_ReportsNoRemoval(string messageId) {
        var index = new UnpublishedPublicationIndex([Entry("msg-1")]);

        bool removed = index.TryRemove(messageId, out UnpublishedPublicationIndex updated);

        removed.ShouldBeFalse();
        updated.ShouldBeSameAs(index);
    }

    [Fact]
    public void Contains_IsOrdinalAndCaseSensitive() {
        var index = new UnpublishedPublicationIndex([Entry("msg-1")]);

        index.Contains("msg-1").ShouldBeTrue();
        index.Contains("MSG-1").ShouldBeFalse();
        index.Contains(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void Prune_RemovesEveryNamedEntryIncludingBlankIdentities() {
        var index = new UnpublishedPublicationIndex([
            Entry("msg-1"),
            Entry("msg-2"),
            new UnpublishedPublicationEntry(string.Empty, "corr-blank", DateTimeOffset.UtcNow),
        ]);

        UnpublishedPublicationIndex pruned = index.Prune(["msg-1", string.Empty]);

        pruned.Entries.Count.ShouldBe(1);
        pruned.Contains("msg-2").ShouldBeTrue();
    }

    [Fact]
    public void Prune_EmptySet_ReturnsTheSameInstance() {
        var index = new UnpublishedPublicationIndex([Entry("msg-1")]);

        index.Prune([]).ShouldBeSameAs(index);
    }

    // --- Persisted actor-state shape ---

    [Fact]
    public void JsonRoundTrip_PreservesEveryEntryIdentity() {
        // The index is persisted through IActorStateManager, which serializes with System.Text.Json.
        var original = new UnpublishedPublicationIndex([
            new UnpublishedPublicationEntry("msg-1", "corr-1", new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.Zero)),
            new UnpublishedPublicationEntry("msg-2", "corr-2", new DateTimeOffset(2026, 8, 7, 9, 5, 0, TimeSpan.Zero)),
        ]);

        UnpublishedPublicationIndex? roundTripped =
            JsonSerializer.Deserialize<UnpublishedPublicationIndex>(JsonSerializer.Serialize(original));

        _ = roundTripped.ShouldNotBeNull();
        roundTripped.Entries.Count.ShouldBe(2);
        roundTripped.Contains("msg-1").ShouldBeTrue();
        roundTripped.Contains("msg-2").ShouldBeTrue();
        roundTripped.Entries[0].CorrelationId.ShouldBe("corr-1");
        roundTripped.Entries[1].CommittedAt.ShouldBe(new DateTimeOffset(2026, 8, 7, 9, 5, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Deserialize_PayloadWithNullEntryElement_IsNormalizedInsteadOfPoisoningEveryActivation() {
        // A null element used to NRE on every activation, so OnActivateAsync degraded forever and
        // the index could never be repaired.
        const string poisoned = """{"Entries":[null,{"MessageId":"msg-1","CorrelationId":"corr-1","CommittedAt":"2026-08-07T09:00:00+00:00"}]}""";

        UnpublishedPublicationIndex? index = JsonSerializer.Deserialize<UnpublishedPublicationIndex>(poisoned);

        _ = index.ShouldNotBeNull();
        index.Entries.Count.ShouldBe(1);
        index.Contains("msg-1").ShouldBeTrue();
        _ = Should.NotThrow(() => index.TryAdd(Entry("msg-2"), maxEntries: 8, out _));
    }

    [Fact]
    public void Deserialize_PayloadWithoutEntries_IsNormalizedToEmpty() {
        UnpublishedPublicationIndex? index = JsonSerializer.Deserialize<UnpublishedPublicationIndex>("{}");

        _ = index.ShouldNotBeNull();
        index.Entries.ShouldBeEmpty();
    }
}
