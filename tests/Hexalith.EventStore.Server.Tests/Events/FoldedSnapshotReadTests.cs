using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Verifies raw envelope admission and retained snapshot bypass.</summary>
public sealed class FoldedSnapshotReadTests
{
    private static readonly AggregateIdentity Identity = new("tenant", "domain", "aggregate");

    [Fact]
    public async Task LegacyDscsIsRetainedAndDoesNotContributeASequence()
    {
        var store = new InMemoryStateManager();
        JsonElement state = JsonSerializer.Deserialize<JsonElement>(
            """{"currentSequence":1,"events":[],"snapshotState":{"count":1}}""");
        await store.SetStateAsync(Identity.SnapshotKey, Record(1, state));
        await store.SetStateAsync(Identity.MetadataKey, new AggregateMetadata(1, DateTimeOffset.UtcNow, null));
        await store.SetStateAsync($"{Identity.EventStreamKeyPrefix}1", Event(1));
        await store.SaveStateAsync();

        SnapshotRecord? snapshot = await Manager().LoadSnapshotAsync(Identity, store);
        snapshot.ShouldBeNull();
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
        RehydrationResult? replay = await Reader(store).RehydrateAsync(Identity, snapshot);
        replay.ShouldNotBeNull().LastSnapshotSequence.ShouldBe(0);
        replay.Events.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"1\"")]
    [InlineData("1,\"snapshotEnvelopeVersion\":1")]
    public async Task UnknownVersionIsRetainedBeforeStateInterpretation(string versionJson)
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey,
            JsonSerializer.Deserialize<JsonElement>(
                "{\"snapshotEnvelopeVersion\":" + versionJson + ",\"sequenceNumber\":1,\"state\":\"secret\"}"));
        await store.SaveStateAsync();

        SnapshotLoadResult result = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);
        result.Outcome.ShouldBe(SnapshotLoadOutcome.ProviderOpaque);
        result.ReasonCode.ShouldBe("SnapshotEnvelopeVersionUnknown");
        (await Manager().InspectSnapshotForManualOverwriteAsync(Identity, store)).Outcome
            .ShouldBe(SnapshotLoadOutcome.ProviderOpaque);
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("[]")]
    public async Task KnownVersionNonObjectStateIsRetainedAndBypassed(string stateJson)
    {
        var store = new InMemoryStateManager();
        JsonElement raw = JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":1,\"sequenceNumber\":1,\"state\":" + stateJson
            + ",\"createdAt\":\"2026-01-01T00:00:00Z\",\"domain\":\"domain\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\"}");
        await store.SetStateAsync(Identity.SnapshotKey, raw);
        await store.SaveStateAsync();

        (await Manager().LoadSnapshotAsync(Identity, store)).ShouldBeNull();
        (await Manager().InspectSnapshotForManualOverwriteAsync(Identity, store)).ReasonCode
            .ShouldBe("SnapshotStateInvalid");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Fact]
    public async Task MissingStateIsRetainedAndRejectedBeforeTypedBinding()
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"createdAt":"2026-01-01T00:00:00Z","domain":"domain","aggregateId":"aggregate","tenantId":"tenant"}"""));
        await store.SaveStateAsync();

        (await Manager().LoadSnapshotAsync(Identity, store)).ShouldBeNull();
        (await Manager().InspectSnapshotForManualOverwriteAsync(Identity, store)).ReasonCode
            .ShouldBe("SnapshotStateInvalid");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"bad\"")]
    [InlineData("1.5")]
    public async Task MalformedCoveredSequenceIsRetainedAndRejected(string sequenceJson)
    {
        var store = new InMemoryStateManager();
        JsonElement raw = JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":1,\"sequenceNumber\":" + sequenceJson
            + ",\"state\":{},\"createdAt\":\"2026-01-01T00:00:00Z\",\"domain\":\"domain\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\"}");
        await store.SetStateAsync(Identity.SnapshotKey, raw);
        await store.SaveStateAsync();

        (await Manager().LoadSnapshotAsync(Identity, store)).ShouldBeNull();
        (await Manager().InspectSnapshotForManualOverwriteAsync(Identity, store)).ReasonCode
            .ShouldBe("SnapshotSequenceInvalid");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task NonPositiveCoveredSequenceIsRetainedAndBypassed(int version, long sequence)
    {
        var store = new InMemoryStateManager();
        JsonElement raw = JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":" + version + ",\"sequenceNumber\":" + sequence
            + ",\"state\":{},\"createdAt\":\"2026-01-01T00:00:00Z\",\"domain\":\"domain\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\"}");
        await store.SetStateAsync(Identity.SnapshotKey, raw);
        await store.SaveStateAsync();

        SnapshotLoadResult commandResult = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);
        commandResult.Snapshot.ShouldBeNull();
        commandResult.Outcome.ShouldBe(SnapshotLoadOutcome.Corrupt);
        commandResult.ReasonCode.ShouldBe("SnapshotSequenceInvalid");
        (await Manager().InspectSnapshotForManualOverwriteAsync(Identity, store)).ReasonCode
            .ShouldBe("SnapshotSequenceInvalid");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Fact]
    public async Task KnownPlaintextBindingFailureMayDeleteOnlyWithVerifiedHeadButProtectedFailureRetains()
    {
        var store = new InMemoryStateManager();
        JsonElement corrupt = JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"domain","aggregateId":"aggregate","tenantId":"tenant","protectionMetadata":null}""");
        await store.SetStateAsync(Identity.SnapshotKey, corrupt);
        await store.SaveStateAsync();
        (await Manager().LoadSnapshotForCommandAsync(Identity, store, authoritativeHead: 1)).Snapshot.ShouldBeNull();
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeFalse();

        await store.SetStateAsync(Identity.SnapshotKey,
            JsonSerializer.Deserialize<JsonElement>(
                """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":"cipher","createdAt":"bad","domain":"domain","aggregateId":"aggregate","tenantId":"tenant","protectionMetadata":{"state":1,"metadataVersion":1}}"""));
        await store.SaveStateAsync();
        SnapshotLoadResult protectedMalformed = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);
        protectedMalformed.Snapshot.ShouldBeNull();
        protectedMalformed.Outcome.ShouldBe(SnapshotLoadOutcome.Corrupt);
        protectedMalformed.ReasonCode.ShouldBe("CorruptSnapshot");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();

        await store.SetStateAsync(Identity.SnapshotKey,
            JsonSerializer.Deserialize<JsonElement>(
                """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":"cipher","createdAt":"2026-01-01T00:00:00Z","domain":"domain","aggregateId":"aggregate","tenantId":"tenant","protectionMetadata":{"state":1,"metadataVersion":1,"scheme":"test-provider"}}"""));
        await store.SaveStateAsync();
        SnapshotLoadResult protectedUnreadable = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);
        protectedUnreadable.Snapshot.ShouldBeNull();
        protectedUnreadable.Outcome.ShouldBe(SnapshotLoadOutcome.UnreadableProtected);
        protectedUnreadable.ReasonCode.ShouldBe("SnapshotUnreadableProtected");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("other", "{}", 1, "AddressMismatch")]
    [InlineData("domain", "42", 1, "SnapshotStateInvalid")]
    [InlineData("domain", "{}", 2, "SnapshotSequenceInvalid")]
    public async Task RawRejectionPrecedesBindingFailureAndRetainsKey(
        string domain, string stateJson, long sequence, string expectedReason)
    {
        var store = new InMemoryStateManager();
        JsonElement raw = JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":1,\"sequenceNumber\":" + sequence
            + ",\"state\":" + stateJson
            + ",\"createdAt\":\"bad\",\"domain\":\"" + domain
            + "\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\",\"protectionMetadata\":null}");
        await store.SetStateAsync(Identity.SnapshotKey, raw);
        await store.SaveStateAsync();

        SnapshotLoadResult result = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);

        result.Outcome.ShouldBe(SnapshotLoadOutcome.Corrupt);
        result.ReasonCode.ShouldBe(expectedReason);
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Fact]
    public async Task DirectLoadWithoutVerifiedHeadRetainsPlaintextBindingFailure()
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"domain","aggregateId":"aggregate","tenantId":"tenant","protectionMetadata":null}"""));
        await store.SaveStateAsync();

        (await Manager().LoadSnapshotAsync(Identity, store)).ShouldBeNull();
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Fact]
    public async Task AmbiguousProtectionMetadataCannotProvePlaintextForDeletion()
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"domain","aggregateId":"aggregate","tenantId":"tenant","protectionMetadata":{"state":1,"state":0,"metadataVersion":1}}"""));
        await store.SaveStateAsync();

        SnapshotLoadResult result = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);
        result.Snapshot.ShouldBeNull();
        result.Outcome.ShouldBe(SnapshotLoadOutcome.ProviderOpaque);
        result.ReasonCode.ShouldBe("SnapshotProtectionOpaque");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("{\"count\":1,\"COUNT\":2}")]
    [InlineData("{\"nested\":{\"count\":1,\"COUNT\":2}}")]
    [InlineData("{\"items\":[{\"count\":1,\"COUNT\":2}]}")]
    public async Task CommandReadBypassesCaseInsensitiveDuplicateFoldedStateWithoutDeletingIt(string stateJson)
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":1,\"sequenceNumber\":1,\"state\":" + stateJson
            + ",\"createdAt\":\"2026-01-01T00:00:00Z\",\"domain\":\"domain\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\",\"protectionMetadata\":null}"));
        await store.SaveStateAsync();

        SnapshotLoadResult result = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);

        result.Snapshot.ShouldBeNull();
        result.Outcome.ShouldBe(SnapshotLoadOutcome.Corrupt);
        result.ReasonCode.ShouldBe("SnapshotStateInvalid");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("\"protectionMetadata\":null,\"ProtectionMetadata\":{\"state\":0}")]
    [InlineData("\"protectionMetadata\":{\"state\":1,\"State\":0,\"metadataVersion\":1}")]
    public async Task CommandReadRetainsAmbiguousProtectionMetadataBeforeTypedBinding(string protectionJson)
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, JsonSerializer.Deserialize<JsonElement>(
            "{\"snapshotEnvelopeVersion\":1,\"sequenceNumber\":1,\"state\":{\"count\":1},"
            + "\"createdAt\":\"2026-01-01T00:00:00Z\",\"domain\":\"domain\",\"aggregateId\":\"aggregate\",\"tenantId\":\"tenant\","
            + protectionJson + "}"));
        await store.SaveStateAsync();

        SnapshotLoadResult result = await Manager().LoadSnapshotForCommandAsync(
            Identity, store, authoritativeHead: 1);

        result.Snapshot.ShouldBeNull();
        result.Outcome.ShouldBe(SnapshotLoadOutcome.ProviderOpaque);
        result.ReasonCode.ShouldBe("SnapshotProtectionOpaque");
        (await store.ContainsStateAsync(Identity.SnapshotKey)).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanSnapshotCannotCreateAStreamHead()
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.SnapshotKey, Record(1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}""")));
        await store.SaveStateAsync();
        SnapshotRecord? snapshot = await Manager().LoadSnapshotAsync(Identity, store);
        snapshot.ShouldNotBeNull();
        (await Should.ThrowAsync<InvalidOperationException>(() => Reader(store).RehydrateAsync(Identity, snapshot)))
            .Message.ShouldBe("SnapshotStreamUnavailable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectReaderRejectsUnaddressedOrUnknownVersionSnapshot(bool unknownVersion)
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.MetadataKey, new AggregateMetadata(1, DateTimeOffset.UtcNow, null));
        await store.SetStateAsync($"{Identity.EventStreamKeyPrefix}1", Event(1));
        await store.SaveStateAsync();
        SnapshotRecord candidate = Record(1, JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""));
        candidate = unknownVersion
            ? candidate with { SnapshotEnvelopeVersion = 2 }
            : candidate with { TenantId = "other" };

        RehydrationResult? replay = await Reader(store).RehydrateAsync(Identity, candidate);

        replay.ShouldNotBeNull().LastSnapshotSequence.ShouldBe(0);
        replay.UsedSnapshot.ShouldBeFalse();
        replay.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FutureSnapshotCannotSupplyAHeadOrSkipThePersistedPrefix()
    {
        var store = new InMemoryStateManager();
        await store.SetStateAsync(Identity.MetadataKey, new AggregateMetadata(1, DateTimeOffset.UtcNow, null));
        await store.SetStateAsync($"{Identity.EventStreamKeyPrefix}1", Event(1));
        await store.SaveStateAsync();
        SnapshotRecord future = Record(2, JsonSerializer.Deserialize<JsonElement>("""{"count":2}"""))
            with { SnapshotEnvelopeVersion = 1 };

        RehydrationResult? replay = await Reader(store).RehydrateAsync(Identity, future);

        replay.ShouldNotBeNull().CurrentSequence.ShouldBe(1);
        replay.LastSnapshotSequence.ShouldBe(0);
        replay.Events.Count.ShouldBe(1);
    }

    private static SnapshotManager Manager()
        => new(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance,
            new NoOpEventPayloadProtectionService());

    private static EventStreamReader Reader(InMemoryStateManager store)
        => new(store, NullLogger<EventStreamReader>.Instance);

    private static SnapshotRecord Record(long sequence, JsonElement state)
        => new(sequence, state, DateTimeOffset.UtcNow, Identity.Domain, Identity.AggregateId, Identity.TenantId);

    private static EventEnvelope Event(long sequence)
        => new("01HZX6FKA8B3YTX6MBQ3AYT3PT", Identity.AggregateId, "aggregate", Identity.TenantId,
            Identity.Domain, sequence, 0, DateTimeOffset.UtcNow, "corr", "cause", "user", "v1",
            "Event", 1, "json", "{}"u8.ToArray(), null);
}
