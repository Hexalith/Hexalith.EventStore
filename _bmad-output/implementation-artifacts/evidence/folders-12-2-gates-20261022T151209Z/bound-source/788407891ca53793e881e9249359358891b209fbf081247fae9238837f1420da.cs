
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public class SnapshotManagerTests(ITestOutputHelper output) {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    [Fact]
    public void SnapshotLoadOutcome_PreservesPublishedNumericValues() {
        ((int)SnapshotLoadOutcome.Absent).ShouldBe(0);
        ((int)SnapshotLoadOutcome.Readable).ShouldBe(1);
        ((int)SnapshotLoadOutcome.UnreadableProtected).ShouldBe(2);
        ((int)SnapshotLoadOutcome.ProviderOpaque).ShouldBe(3);
        ((int)SnapshotLoadOutcome.Corrupt).ShouldBe(4);
        ((int)SnapshotLoadOutcome.InfrastructureFailure).ShouldBe(5);
    }

    private static (SnapshotManager Manager, IActorStateManager StateManager) CreateManager(
        SnapshotOptions? options = null,
        ISnapshotPolicyResolver? snapshotPolicyResolver = null,
        IEventPayloadProtectionService? protection = null) {
        options ??= new SnapshotOptions();
        IOptions<SnapshotOptions> optionsWrapper = Options.Create(options);
        ILogger<SnapshotManager> logger = Substitute.For<ILogger<SnapshotManager>>();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        return (new SnapshotManager(optionsWrapper, logger, protection ?? new NoOpEventPayloadProtectionService(), snapshotPolicyResolver), stateManager);
    }

    private static void SetRawSnapshot(IActorStateManager stateManager, SnapshotRecord snapshot) {
        JsonElement raw = JsonSerializer.SerializeToElement(snapshot, JsonSerializerOptions.Web);
        _ = stateManager.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<object>(true, raw));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancellationAfterNormallyCompletedRawReadPreventsAnyInterpretation(bool present, bool corrupt)
    {
        // HX-FS-V1 §9.2, V11: a provider may complete normally after canceling the caller.
        using var cancellation = new CancellationTokenSource();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (SnapshotManager manager, IActorStateManager store) = CreateManager(protection: protection);
        JsonElement raw = JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":null}""");
        if (!corrupt)
        {
            raw = JsonSerializer.Deserialize<JsonElement>(raw.GetRawText().Replace(
                "\"createdAt\":\"bad\"", "\"createdAt\":\"1970-01-01T00:00:00+00:00\"", StringComparison.Ordinal));
        }
        _ = store.TryGetStateAsync<object>(TestIdentity.SnapshotKey, cancellation.Token).Returns(_ =>
        {
            cancellation.Cancel();
            return new ConditionalValue<object>(present, raw);
        });

        await Should.ThrowAsync<OperationCanceledException>(() => manager.LoadSnapshotForCommandAsync(
            TestIdentity, store, cancellationToken: cancellation.Token, authoritativeHead: 1));

        await protection.DidNotReceive().TryUnprotectSnapshotAsync(TestIdentity,
            Arg.Any<object>(), Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializerScratchRefusalPrecedesProbeProtectionAndStaging(bool throwOnFailure)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            DefaultBufferSize = 64 * 1024 * 1024,
        };
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (SnapshotManager manager, IActorStateManager store) = CreateManager(protection: protection);
        if (throwOnFailure)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => manager.CreateSnapshotAsync(
                TestIdentity, 1, JsonSerializer.Deserialize<JsonElement>("{}"), store,
                serializerOptions, SnapshotTestLimits.ForState(), throwOnFailure: true)))
                .Message.ShouldBe("SnapshotCaptureLimitExceeded");
        }
        else
        {
            await manager.CreateSnapshotAsync(TestIdentity, 1, JsonSerializer.Deserialize<JsonElement>("{}"),
                store, serializerOptions, SnapshotTestLimits.ForState());
        }
        await protection.DidNotReceive().ProtectSnapshotAsync(TestIdentity,
            Arg.Any<object>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(159_744, true)]
    [InlineData(159_743, false)]
    [InlineData(16_384, false)]
    public async Task SerializerWorkspaceBoundaryStagesOnlyTheAdmittedCandidate(long scratchBytes, bool admitted)
    {
        // HX-FS-V1 §8.6, V5/V16: the final envelope maximum governs pre-admission, even for a tiny state.
        var logger = new SnapshotManagerCaptureLogger();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), logger,
            new NoOpEventPayloadProtectionService());
        var store = new InMemoryStateManager();
        var limits = SnapshotTestLimits.ForState(1024, serializerScratchBytes: scratchBytes, envelopeBytes: 8192);
        JsonElement state = JsonSerializer.Deserialize<JsonElement>("""{"count":1}""");

        await manager.CreateSnapshotAsync(TestIdentity, 1, state, store, JsonSerializerOptions.Web, limits);
        store.CommittedState.ContainsKey(TestIdentity.SnapshotKey).ShouldBeFalse();
        await store.SaveStateAsync();
        store.CommittedState.ContainsKey(TestIdentity.SnapshotKey).ShouldBe(admitted);
        if (admitted)
        {
            SnapshotRecord persisted = store.CommittedState[TestIdentity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
            JsonSerializer.SerializeToUtf8Bytes(persisted.State, JsonSerializerOptions.Web)
                .ShouldBe(JsonSerializer.SerializeToUtf8Bytes(state, JsonSerializerOptions.Web));
            persisted.SnapshotEnvelopeVersion.ShouldBe(1);
        }
        else
        {
            logger.Messages.ShouldContain(message => message.Contains(
                "FailureClass=SnapshotCaptureLimitExceeded", StringComparison.Ordinal));
            (await Should.ThrowAsync<InvalidOperationException>(() => manager.CreateSnapshotAsync(
                TestIdentity, 1, state, store, JsonSerializerOptions.Web, limits, throwOnFailure: true)))
                .Message.ShouldBe("SnapshotCaptureLimitExceeded");
        }
    }

    [Fact]
    public void OversizedEscapableInputCannotAllocateBeyondItsEncoderScratchReservation()
    {
        // HX-FS-V1 §8.6, V5/V16: a valid 132,096-byte graph can contain this source scalar.
        const int maximumOutput = 1024;
        const long maximumInput = 132_096;
        const long scratch = 262_144;
        const long requiredScratch = 16_384 + 24 * maximumInput + 4096 + 64 * 64;
        BoundedSnapshotJsonBuffer.CanSerialize(maximumOutput, JsonSerializerOptions.Web, requiredScratch, maximumInput)
            .ShouldBeTrue();
        BoundedSnapshotJsonBuffer.CanSerialize(maximumOutput, JsonSerializerOptions.Web, requiredScratch - 1, maximumInput)
            .ShouldBeFalse();
        BoundedSnapshotJsonBuffer.CanSerialize(maximumOutput, JsonSerializerOptions.Web, requiredScratch, long.MaxValue)
            .ShouldBeFalse();
        string oversized = new('\u0001', 65_536);
        using var outputBuffer = new MemoryStream(maximumOutput);
        BoundedSnapshotJsonBuffer.CanSerialize(maximumOutput, JsonSerializerOptions.Web, scratch, maximumInput)
            .ShouldBeFalse();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Exception failure = Record.Exception(() => BoundedSnapshotJsonBuffer.Serialize(outputBuffer,
            oversized, typeof(string), JsonSerializerOptions.Web, maximumOutput, scratch, maximumInput)).ShouldNotBeNull();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("SnapshotCaptureLimitExceeded");
        outputBuffer.Length.ShouldBe(0);
        allocated.ShouldBeLessThan(scratch);
        output.WriteLine($"Refused oversized source string before encoding: allocated={allocated}, reservedScratch={scratch}.");
    }

    [Fact]
    public void FixedSerializerScratchBoundaryPreservesCanonicalStateAndEnvelopeBytes()
    {
        // HX-FS-V1 §§6.2/8.6, V5/V16: equal options produce exact canonical bytes at the admitted boundary.
        JsonSerializerOptions options = JsonSerializerOptions.Web;
        const int maximum = 8192;
        const long requiredScratch = 3 * maximum + 4096 + 24 * maximum + 4096 + 64 * 64;
        BoundedSnapshotJsonBuffer.CanSerialize(maximum, options, requiredScratch, maximumInputBytes: maximum).ShouldBeTrue();
        BoundedSnapshotJsonBuffer.CanSerialize(maximum, options, requiredScratch - 1, maximumInputBytes: maximum).ShouldBeFalse();
        object[] values = [JsonSerializer.Deserialize<JsonElement>("""{"name":"<界>","items":[1,2,3]}"""),
            new SnapshotRecord(7, JsonSerializer.Deserialize<JsonElement>("""{"count":7}"""),
                DateTimeOffset.UnixEpoch, "d", "a", "t", EventStorePayloadProtectionMetadata.Unprotected())
                { SnapshotEnvelopeVersion = 1 }];
        foreach (object value in values)
        {
            using var output = new BoundedSnapshotByteStream(maximum, options, requiredScratch, maximumInputBytes: maximum);
            BoundedSnapshotJsonBuffer.Serialize(output, value, value.GetType(), options, maximum, requiredScratch, maximumInputBytes: maximum);
            output.ToArray().ShouldBe(JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), options));
        }

        // One escape near the start rents a worst-case encoder buffer for the entire suffix.
        const int largeMaximum = 128 * 1024;
        const long largeScratch = 27L * largeMaximum + 4096 + 4096 + 64 * 64;
        string sparseEscaping = "<" + new string('a', 100_000);
        using var largeOutput = new BoundedSnapshotByteStream(largeMaximum, options, largeScratch, maximumInputBytes: largeMaximum);
        BoundedSnapshotJsonBuffer.Serialize(largeOutput, sparseEscaping, typeof(string), options, largeMaximum, largeScratch, maximumInputBytes: largeMaximum);
        largeOutput.ToArray().ShouldBe(JsonSerializer.SerializeToUtf8Bytes(sparseEscaping, options));

        string deepJson = new string('[', 65) + "0" + new string(']', 65);
        using JsonDocument deep = JsonDocument.Parse(deepJson, new JsonDocumentOptions { MaxDepth = 100 });
        Exception canonicalFailure = Record.Exception(() => JsonSerializer.SerializeToUtf8Bytes(
            deep.RootElement, options)).ShouldNotBeNull();
        using var boundedOutput = new BoundedSnapshotByteStream(maximum, options, requiredScratch, maximumInputBytes: maximum);
        Exception boundedFailure = Record.Exception(() => BoundedSnapshotJsonBuffer.Serialize(
            boundedOutput, deep.RootElement, typeof(JsonElement), options, maximum, requiredScratch, maximumInputBytes: maximum)).ShouldNotBeNull();
        boundedFailure.GetType().ShouldBe(canonicalFailure.GetType());
    }

    [Theory]
    [InlineData(false, 7103, "SnapshotReadUnavailable")]
    [InlineData(true, 7104, "SnapshotRemovalUnavailable")]
    public async Task SnapshotInfrastructureLogsCarrySafeClassAndIdentityWithoutException(
        bool duringRemoval, int eventId, string failureClass)
    {
        // HX-FS-V1 §11, V11: capture generated messages before their pooled state is released.
        var logger = new SnapshotManagerCaptureLogger();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), logger,
            new NoOpEventPayloadProtectionService());
        IActorStateManager store = Substitute.For<IActorStateManager>();
        if (duringRemoval)
        {
            _ = store.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<object>(true, JsonSerializer.Deserialize<JsonElement>(
                    """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":null}""")));
            _ = store.RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("provider-private-detail"));
        }
        else
        {
            _ = store.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("provider-private-detail"));
        }

        SnapshotLoadResult result = await manager.LoadSnapshotForCommandAsync(
            TestIdentity, store, "corr-infrastructure", authoritativeHead: 1);

        result.ReasonCode.ShouldBe(duringRemoval ? "CorruptSnapshot" : "SnapshotReadUnavailable");
        logger.EventIds.ShouldHaveSingleItem().Id.ShouldBe(eventId);
        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain($"FailureClass={failureClass}");
        message.ShouldContain("CorrelationId=corr-infrastructure");
        message.ShouldContain("TenantId=test-tenant");
        message.ShouldContain("Domain=test-domain");
        message.ShouldContain("AggregateId=agg-001");
        message.ShouldNotContain("provider-private-detail");
        logger.Exceptions.ShouldAllBe(exception => exception == null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoOpProtectionCannotChangeTheAdmittedFold(bool throwOnFailure)
    {
        // HX-FS-V1 §6.3, V5/V6: freeze the canonical state before invoking the provider.
        JsonElement folded = JsonSerializer.Deserialize<JsonElement>("""{"count":1}""");
        JsonElement changed = JsonSerializer.Deserialize<JsonElement>("""{"count":2}""");
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotProtectionResult(changed, EventStorePayloadProtectionMetadata.Unprotected()));
        var logger = new SnapshotManagerCaptureLogger();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), logger, protection);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();

        if (throwOnFailure)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => manager.CreateSnapshotAsync(
                TestIdentity, 1, folded, stateManager, throwOnFailure: true)))
                .Message.ShouldBe("SnapshotStateChangedByNoOpProtection");
        }
        else
        {
            await manager.CreateSnapshotAsync(TestIdentity, 1, folded, stateManager);
            logger.Messages.ShouldContain(message => message.Contains(
                "FailureClass=SnapshotStateChangedByNoOpProtection", StringComparison.Ordinal));
        }
        folded.GetProperty("count").GetInt32().ShouldBe(1);
        await stateManager.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        logger.Exceptions.ShouldAllBe(exception => exception == null);
    }

    [Fact]
    public async Task ProtectedVersionOneEnvelopePersistsMetadataAndReloadsThroughUnprotection()
    {
        // HX-FS-V1 §§5/6/9, V3/V7/V10: protected sizes are outside the no-op overhead bound.
        JsonElement folded = JsonSerializer.Deserialize<JsonElement>("""{"count":7}""");
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected, 1, "test-protection", "test-reference", "application/json", null);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotProtectionResult("provider-opaque-value", metadata));
        _ = protection.TryUnprotectSnapshotAsync(TestIdentity, Arg.Any<object>(), metadata, Arg.Any<CancellationToken>())
            .Returns(SnapshotUnprotectionOutcome.Readable(folded, metadata));
        (SnapshotManager manager, _) = CreateManager(protection: protection);
        var store = new InMemoryStateManager();

        await manager.CreateSnapshotAsync(TestIdentity, 7, folded, store, JsonSerializerOptions.Web,
            SnapshotTestLimits.ForState(), throwOnFailure: true);
        store.CommittedState.ContainsKey(TestIdentity.SnapshotKey).ShouldBeFalse();
        await store.SaveStateAsync();
        SnapshotRecord persisted = store.CommittedState[TestIdentity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        persisted.SnapshotEnvelopeVersion.ShouldBe(1);
        persisted.SequenceNumber.ShouldBe(7);
        persisted.State.ShouldBe("provider-opaque-value");
        persisted.ProtectionMetadata.ShouldBe(metadata);

        // Use serialized durable bytes, so the read exercises raw version and typed binding.
        var restarted = new InMemoryStateManager();
        JsonElement raw = JsonSerializer.SerializeToElement(persisted, JsonSerializerOptions.Web);
        raw.GetProperty("snapshotEnvelopeVersion").GetInt32().ShouldBe(1);
        raw.GetProperty("protectionMetadata").GetProperty("state").GetInt32()
            .ShouldBe((int)PayloadProtectionState.Protected);
        await restarted.SetStateAsync(TestIdentity.SnapshotKey, raw);
        await restarted.SaveStateAsync();
        SnapshotLoadResult loaded = await manager.LoadSnapshotForCommandAsync(
            TestIdentity, restarted, authoritativeHead: 7);

        loaded.Outcome.ShouldBe(SnapshotLoadOutcome.Readable);
        loaded.Snapshot.ShouldNotBeNull().SnapshotEnvelopeVersion.ShouldBe(1);
        JsonElement.DeepEquals(loaded.Snapshot.State.ShouldBeOfType<JsonElement>(), folded).ShouldBeTrue();
        await protection.Received(1).TryUnprotectSnapshotAsync(TestIdentity,
            Arg.Is<object>(state => state is JsonElement && ((JsonElement)state).GetString() == "provider-opaque-value"),
            metadata, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StringEnumActorSerializerIsUnsupportedBeforeProtectionOrStaging(bool throwOnFailure)
    {
        // HX-FS-V1 §§6.2/9.2, V5/V12: the durable Web reader requires numeric protection enums.
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);

        if (throwOnFailure)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => manager.CreateSnapshotAsync(
                TestIdentity, 1, JsonSerializer.Deserialize<JsonElement>("{}"), stateManager, options,
                throwOnFailure: true))).Message.ShouldBe("SnapshotSerializerUnsupported");
        }
        else
        {
            await manager.CreateSnapshotAsync(TestIdentity, 1,
                JsonSerializer.Deserialize<JsonElement>("{}"), stateManager, options);
        }
        await protection.DidNotReceive().ProtectSnapshotAsync(TestIdentity,
            Arg.Any<object>(), Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAfterSameActivationWriteAcceptsCachedSnapshotRecord()
    {
        (SnapshotManager manager, _) = CreateManager();
        var stateManager = new InMemoryStateManager();
        await manager.CreateSnapshotAsync(TestIdentity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager);
        await stateManager.SaveStateAsync();

        SnapshotLoadResult loaded = await manager.LoadSnapshotForCommandAsync(
            TestIdentity, stateManager, authoritativeHead: 1);

        loaded.Outcome.ShouldBe(SnapshotLoadOutcome.Readable);
        loaded.Snapshot.ShouldNotBeNull().SequenceNumber.ShouldBe(1);
        loaded.Snapshot.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("AddressMismatch")]
    [InlineData("SnapshotSequenceInvalid")]
    [InlineData("SnapshotStateInvalid")]
    [InlineData("SnapshotStateShapeCollision")]
    [InlineData("CorruptSnapshot")]
    [InlineData("SnapshotReadUnavailable")]
    [InlineData("SnapshotUnprotectUnavailable")]
    public async Task AutomaticReplacementAllowsRetainedFailureClasses(string reason)
    {
        (SnapshotManager manager, _) = CreateManager();
        var stateManager = new InMemoryStateManager();
        await stateManager.SetStateAsync(TestIdentity.SnapshotKey,
            JsonSerializer.Deserialize<JsonElement>("""{"retained":"old"}"""));
        await stateManager.SaveStateAsync();

        bool staged = await manager.CreateAutomaticReplacementAsync(TestIdentity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
            JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), reason, "corr-1", CancellationToken.None);
        await stateManager.SaveStateAsync();

        staged.ShouldBeTrue();
        SnapshotRecord written = (SnapshotRecord)stateManager.CommittedState[TestIdentity.SnapshotKey];
        written.SnapshotEnvelopeVersion.ShouldBe(1);
        written.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(1);
    }

    // === 8.2: ShouldCreateSnapshot at default interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_AtDefaultInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", 100, 0);

        result.ShouldBeTrue();
    }

    // === 8.3: ShouldCreateSnapshot below interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_BelowInterval_ReturnsFalse() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", 50, 0);

        result.ShouldBeFalse();
    }

    // === 8.4: ShouldCreateSnapshot at multiples of interval ===

    [Theory]
    [InlineData(200, 100)]
    [InlineData(300, 200)]
    [InlineData(500, 400)]
    public async Task ShouldCreateSnapshot_AtMultipleOfInterval_ReturnsTrue(long currentSequence, long lastSnapshotSequence) {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", currentSequence, lastSnapshotSequence);

        result.ShouldBeTrue();
    }

    // === 8.5: ShouldCreateSnapshot with custom domain interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_WithCustomDomainInterval_UsesOverride() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["fast-domain"] = 50 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 50 events since last snapshot -- should trigger with domain interval of 50
        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "fast-domain", 50, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_UnknownDomain_UsesDefaultInterval() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["fast-domain"] = 50 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 50 events -- below default interval of 100
        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "other-domain", 50, 0);

        result.ShouldBeFalse();
    }

    // === 8.6: CreateSnapshot stores via actor state manager ===

    [Fact]
    public async Task CreateSnapshot_StoresViaActorStateManager() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Name = "test-state" };

        await manager.CreateSnapshotAsync(TestIdentity, 100, state, stateManager);

        await stateManager.Received(1).SetStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Is<SnapshotRecord>(s => s.SequenceNumber == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSnapshot_ForwardsOriginalTokenToStateStage() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        using var cancellation = new CancellationTokenSource();

        await manager.CreateSnapshotAsync(TestIdentity, 100, new { Value = "state" }, stateManager,
            cancellationToken: cancellation.Token);

        await stateManager.Received(1).SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), cancellation.Token);
    }

    [Fact]
    public async Task CreateSnapshot_NullProtectedStateNeverStagesVersionOneRecord() {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotProtectionResult(null!, EventStorePayloadProtectionMetadata.Unprotected()));
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
                throwOnFailure: true));

        failure.Message.ShouldBe("SnapshotStateInvalid");
        await stateManager.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSnapshot_PreCanceledRequestDoesNotStageState() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => manager.CreateSnapshotAsync(
            TestIdentity, 100, new { Value = "state" }, stateManager, cancellationToken: cancellation.Token));

        await stateManager.DidNotReceive().SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutomaticSnapshot_ProtectionInternalCancellationIsAdvisory()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SnapshotProtectionResult>(
                new OperationCanceledException("protection canceled internally")));
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);

        bool staged = await manager.CreateAutomaticReplacementAsync(TestIdentity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
            JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), "SnapshotStateInvalid", "corr-1",
            CancellationToken.None);

        staged.ShouldBeFalse();
        await stateManager.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutomaticSnapshot_ProtectionCallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<SnapshotProtectionResult>(cancellation.Token);
            });
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            manager.CreateAutomaticReplacementAsync(TestIdentity, 1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
                JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), "SnapshotStateInvalid", "corr-1",
                cancellation.Token));

        await stateManager.DidNotReceive().SetStateAsync(TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadSnapshot_InternalCancellationReturnsTypedInfrastructureFailure(bool duringUnprotect)
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);
        if (duringUnprotect)
        {
            SetRawSnapshot(stateManager, new SnapshotRecord(1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), DateTimeOffset.UtcNow,
                TestIdentity.Domain, TestIdentity.AggregateId, TestIdentity.TenantId,
                EventStorePayloadProtectionMetadata.Unprotected()) { SnapshotEnvelopeVersion = 1 });
            _ = protection.TryUnprotectSnapshotAsync(TestIdentity, Arg.Any<object>(),
                    Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<SnapshotUnprotectionOutcome>(
                    new OperationCanceledException("unprotect canceled internally")));
        }
        else
        {
            _ = stateManager.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ConditionalValue<object>>(
                    new OperationCanceledException("read canceled internally")));
        }

        SnapshotLoadResult result = await manager.LoadSnapshotForCommandAsync(
            TestIdentity, stateManager, authoritativeHead: 1);

        result.Outcome.ShouldBe(SnapshotLoadOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe(duringUnprotect ? "SnapshotUnprotectUnavailable" : "SnapshotReadUnavailable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadSnapshot_CallerCancellationDuringReadOrUnprotectPropagates(bool duringUnprotect)
    {
        using var cancellation = new CancellationTokenSource();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);
        if (duringUnprotect)
        {
            SetRawSnapshot(stateManager, new SnapshotRecord(1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), DateTimeOffset.UtcNow,
                TestIdentity.Domain, TestIdentity.AggregateId, TestIdentity.TenantId,
                EventStorePayloadProtectionMetadata.Unprotected()) { SnapshotEnvelopeVersion = 1 });
            _ = protection.TryUnprotectSnapshotAsync(TestIdentity, Arg.Any<object>(),
                    Arg.Any<EventStorePayloadProtectionMetadata>(), cancellation.Token)
                .Returns(_ =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<SnapshotUnprotectionOutcome>(cancellation.Token);
                });
        }
        else
        {
            _ = stateManager.TryGetStateAsync<object>(TestIdentity.SnapshotKey, cancellation.Token)
                .Returns(_ =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<ConditionalValue<object>>(cancellation.Token);
                });
        }

        _ = await Should.ThrowAsync<OperationCanceledException>(() => manager.LoadSnapshotForCommandAsync(
            TestIdentity, stateManager, authoritativeHead: 1, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CreateSnapshot_EscapedMetadataBeyondOverheadBoundDoesNotStage() {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        var compatibilityFlags = Enumerable.Range(0, 8).ToDictionary(
            index => $"flag-{index}", _ => new string('<', 256));
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Unprotected, 1, null, null, null, compatibilityFlags);
        byte[] originalMetadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, JsonSerializerOptions.Web);
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new SnapshotProtectionResult(call.ArgAt<object>(1), metadata)));
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager(protection: protection);

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
                JsonSerializerOptions.Web, throwOnFailure: true));

        failure.Message.ShouldBe("SnapshotEnvelopeBoundExceeded");
        JsonSerializer.SerializeToUtf8Bytes(metadata, JsonSerializerOptions.Web).ShouldBe(originalMetadataBytes);
        await stateManager.DidNotReceive().SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateSnapshot_UnqualifiedEnvelopeSerializerDoesNotStage(bool indented) {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var serializerOptions = new JsonSerializerOptions(JsonSerializerOptions.Web);
        if (indented) { serializerOptions.WriteIndented = true; }
        else { serializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower; }

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 1,
                JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
                serializerOptions, throwOnFailure: true));

        failure.Message.ShouldBe("SnapshotSerializerUnsupported");
        await stateManager.DidNotReceive().SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSnapshot_LargeCompactFoldHasBoundedEnvelopeAndStableStateBytes() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        JsonElement state = JsonSerializer.SerializeToElement(new { values = Enumerable.Range(0, 5000).ToArray() });
        var staged = new List<SnapshotRecord>();
        _ = stateManager.SetStateAsync(TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => { staged.Add(call.ArgAt<SnapshotRecord>(1)); return Task.CompletedTask; });

        foreach (long sequence in new long[] { 1, 100, 1000 }) {
            await manager.CreateSnapshotAsync(TestIdentity, sequence, state, stateManager,
                JsonSerializerOptions.Web, throwOnFailure: true);
        }

        staged.Count.ShouldBe(3);
        byte[][] stateBytes = staged.Select(snapshot => JsonSerializer.SerializeToUtf8Bytes(snapshot.State, JsonSerializerOptions.Web)).ToArray();
        stateBytes[0].Length.ShouldBeGreaterThan(10_000);
        stateBytes[1].ShouldBe(stateBytes[0]);
        stateBytes[2].ShouldBe(stateBytes[0]);
        foreach (SnapshotRecord snapshot in staged) {
            int fullBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSerializerOptions.Web).Length;
            output.WriteLine($"FoldedStateBytes={stateBytes[0].Length}, SnapshotEnvelopeBytes={fullBytes}, CoveredSequence={snapshot.SequenceNumber}");
            fullBytes.ShouldBeLessThanOrEqualTo(stateBytes[0].Length + 4096);
        }
    }

    [Fact]
    public async Task CreateSnapshot_DscsShapeCollisionCannotBeStaged() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        JsonElement dscs = JsonSerializer.Deserialize<JsonElement>("""{"currentSequence":1,"events":[],"snapshotState":{"count":1}}""");

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 1, dscs, stateManager,
                JsonSerializerOptions.Web, throwOnFailure: true));

        failure.Message.ShouldBe("SnapshotStateShapeCollision");
        await stateManager.DidNotReceive().SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    // === 8.7: CreateSnapshot includes sequence number ===

    [Fact]
    public async Task CreateSnapshot_IncludesSequenceNumber() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Value = 42 };

        await manager.CreateSnapshotAsync(TestIdentity, 250, state, stateManager);

        await stateManager.Received(1).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<SnapshotRecord>(s =>
                s.SequenceNumber == 250 &&
                s.Domain == "test-domain" &&
                s.AggregateId == "agg-001" &&
                s.TenantId == "test-tenant" &&
                s.CreatedAt > DateTimeOffset.MinValue),
            Arg.Any<CancellationToken>());
    }

    // === 8.8: CreateSnapshot overwrites previous ===

    [Fact]
    public async Task CreateSnapshot_OverwritesPrevious() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state1 = new { Version = 1 };
        var state2 = new { Version = 2 };

        await manager.CreateSnapshotAsync(TestIdentity, 100, state1, stateManager);
        await manager.CreateSnapshotAsync(TestIdentity, 200, state2, stateManager);

        // SetStateAsync called twice with the SAME key -- second call overwrites first
        await stateManager.Received(2).SetStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(),
            Arg.Any<CancellationToken>());
    }

    // === 8.9: LoadSnapshot returns null when no snapshot ===

    [Fact]
    public async Task LoadSnapshot_ReturnsNullWhenNoSnapshot() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = stateManager.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<object>(false, default!));

        SnapshotRecord? result = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        result.ShouldBeNull();
    }

    // === 8.10: LoadSnapshot returns stored snapshot ===

    [Fact]
    public async Task LoadSnapshot_ReturnsStoredSnapshot() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var snapshot = new SnapshotRecord(100, new { Value = "stored" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        SetRawSnapshot(stateManager, snapshot);

        SnapshotRecord? result = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        _ = result.ShouldNotBeNull();
        result.SequenceNumber.ShouldBe(100);
    }

    /// <summary>Historical folded Counter state is readable, while the same envelope containing a replay DTO remains refused.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalSnapshotNineDistinguishesFoldedStateFromReplayDtoCollision(bool collision)
    {
        (SnapshotManager manager, _) = CreateManager();
        JsonElement state = JsonSerializer.Deserialize<JsonElement>(collision
            ? """{"currentSequence":10,"events":[],"snapshotState":null,"count":9}"""
            : """{"count":9}""");
        var original = new SnapshotRecord(9, state, DateTimeOffset.UnixEpoch,
            TestIdentity.Domain, TestIdentity.AggregateId, TestIdentity.TenantId,
            EventStorePayloadProtectionMetadata.Unprotected());
        JsonElement raw = JsonSerializer.SerializeToElement(original, JsonSerializerOptions.Web);
        var memory = new InMemoryStateManager();
        await memory.SetStateAsync(TestIdentity.SnapshotKey, raw);
        await memory.SaveStateAsync();

        SnapshotLoadResult loaded = await manager.LoadSnapshotForCommandAsync(
            TestIdentity, memory, authoritativeHead: 12);

        loaded.Outcome.ShouldBe(collision ? SnapshotLoadOutcome.Corrupt : SnapshotLoadOutcome.Readable);
        if (collision)
        {
            loaded.ReasonCode.ShouldBe("SnapshotStateShapeCollision");
            loaded.Snapshot.ShouldBeNull();
            loaded.CoveredSequence.ShouldBe(9);
        }
        else
        {
            SnapshotRecord readable = loaded.Snapshot.ShouldNotBeNull();
            readable.SequenceNumber.ShouldBe(9);
            readable.SnapshotEnvelopeVersion.ShouldBe(0);
            readable.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(9);
            readable.ProtectionMetadata.ShouldBe(original.ProtectionMetadata);
        }
        JsonElement.DeepEquals(memory.CommittedState[TestIdentity.SnapshotKey].ShouldBeOfType<JsonElement>(), raw).ShouldBeTrue();
        (await memory.ContainsStateAsync(TestIdentity.SnapshotKey)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    public async Task LoadSnapshot_AddressMismatchRetainsRecordAndReplaysFromStart(string changedField) {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var snapshot = new SnapshotRecord(100, new { Value = "stored" }, DateTimeOffset.UtcNow,
            "test-domain", "agg-001", "test-tenant");
        snapshot = changedField switch {
            "tenant" => snapshot with { TenantId = "other-tenant" },
            "domain" => snapshot with { Domain = "other-domain" },
            _ => snapshot with { AggregateId = "other-aggregate" },
        };
        SetRawSnapshot(stateManager, snapshot);

        SnapshotRecord? replaySnapshot = await manager.LoadSnapshotAsync(TestIdentity, stateManager);
        SnapshotLoadResult overwriteSnapshot = await manager.InspectSnapshotForManualOverwriteAsync(TestIdentity, stateManager);

        replaySnapshot.ShouldBeNull();
        overwriteSnapshot.Outcome.ShouldBe(SnapshotLoadOutcome.Corrupt);
        overwriteSnapshot.ReasonCode.ShouldBe("AddressMismatch");
        await stateManager.DidNotReceive().RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadSnapshot_PreCanceledRequestDoesNotReadOrDelete() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => manager.LoadSnapshotAsync(TestIdentity, stateManager, cancellationToken: cancellation.Token));

        _ = stateManager.DidNotReceive().TryGetStateAsync<object>(
            TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
    }

    // === 8.11: SnapshotOptions default interval is 100 ===

    [Fact]
    public void SnapshotOptions_DefaultIntervalIs100() {
        var options = new SnapshotOptions();

        options.DefaultInterval.ShouldBe(100);
    }

    // === 8.12: SnapshotOptions interval must be at least 10 ===

    [Fact]
    public void SnapshotOptions_IntervalBelowMinimum_ValidationFails() {
        var options = new SnapshotOptions { DefaultInterval = 5 };

        Should.Throw<InvalidOperationException>(options.Validate)
            .Message.ShouldContain("10");
    }

    [Fact]
    public void SnapshotOptions_DomainIntervalBelowMinimum_ValidationFails() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["bad-domain"] = 3 }
        };

        Should.Throw<InvalidOperationException>(options.Validate)
            .Message.ShouldContain("bad-domain");
    }

    [Fact]
    public void SnapshotOptions_ValidIntervals_ValidationPasses() {
        var options = new SnapshotOptions {
            DefaultInterval = 50,
            DomainIntervals = new Dictionary<string, int> { ["fast"] = 10, ["slow"] = 500 }
        };

        Should.NotThrow(options.Validate);
    }

    // === 8.13: CreateSnapshot serialization failure logs warning and skips ===

    [Fact]
    public async Task CreateSnapshot_SerializationFailure_LogsWarningAndSkips() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Value = "test" };

        _ = stateManager.SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<SnapshotRecord>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Serialization failed"));

        // Should NOT throw -- advisory per rule #12
        await Should.NotThrowAsync(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, state, stateManager));
    }

    [Fact]
    public async Task CreateSnapshot_ProviderMessageWithSnapshotPrefixNeverBecomesFailureClass()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(TestIdentity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Snapshot provider secret material"));
        ILogger<SnapshotManager> logger = Substitute.For<ILogger<SnapshotManager>>();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), logger, protection);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();

        await manager.CreateSnapshotAsync(TestIdentity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager);

        string[] messages = logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()[2]?.ToString() ?? string.Empty)
            .ToArray();
        messages.ShouldContain(message => message.Contains("FailureClass=SnapshotCreateUnavailable", StringComparison.Ordinal));
        messages.ShouldNotContain(message => message.Contains("secret", StringComparison.OrdinalIgnoreCase));
        await stateManager.DidNotReceive().SetStateAsync(
            TestIdentity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>());
    }

    // === 8.14: Addressed plaintext binding failure may delete with a verified stream head ===

    [Fact]
    public async Task LoadSnapshot_AddressedPlaintextBindingFailureWithVerifiedHeadDeletesCorrupt() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();

        JsonElement malformed = JsonSerializer.Deserialize<JsonElement>("""{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":{},"createdAt":"invalid","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":null}""");
        _ = stateManager.TryGetStateAsync<object>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<object>(true, malformed));

        SnapshotRecord? result = (await manager.LoadSnapshotForCommandAsync(
            TestIdentity, stateManager, authoritativeHead: 100)).Snapshot;

        result.ShouldBeNull();
        await stateManager.Received(1).RemoveStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Any<CancellationToken>());
    }

    // === Additional edge cases ===

    [Fact]
    public async Task ShouldCreateSnapshot_ExactlyAtInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        // Exactly 100 events since last snapshot at 0
        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", 100, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_OneBeforeInterval_ReturnsFalse() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", 99, 0);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_OneAfterInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-tenant", "test-domain", 101, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshot_NullIdentity_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(null!, 100, new object(), stateManager));
    }

    [Fact]
    public async Task CreateSnapshot_NullState_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, null!, stateManager));
    }

    [Fact]
    public async Task CreateSnapshot_NullStateManager_ThrowsArgumentNullException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, new object(), null!));
    }

    [Fact]
    public async Task LoadSnapshot_NullIdentity_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.LoadSnapshotAsync(null!, stateManager));
    }

    [Fact]
    public async Task ShouldCreateSnapshot_NullDomain_ThrowsArgumentException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            manager.ShouldCreateSnapshotAsync("test-tenant", null!, 100, 0));
    }

    // === Per-tenant-domain interval override tests (Story 7.1) ===

    [Fact]
    public async Task ShouldCreateSnapshot_WithTenantDomainOverride_UsesTenantDomainInterval() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 25 events -- should trigger with tenant-domain interval of 25
        bool result = await manager.ShouldCreateSnapshotAsync("acme", "payments", 25, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_TenantDomainOverrideWinsOverDomainOverride() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 30 events -- above tenant-domain (25) but below domain (50) -- should trigger
        bool result = await manager.ShouldCreateSnapshotAsync("acme", "payments", 30, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_NoTenantDomainOverride_UsesDomainOverride() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // Different tenant, no tenant-domain override -- uses domain override of 50
        bool result = await manager.ShouldCreateSnapshotAsync("other-tenant", "payments", 50, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_NoOverrides_UsesDefaultInterval() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // Different tenant and domain, no overrides -- uses default of 100
        bool result = await manager.ShouldCreateSnapshotAsync("other-tenant", "other-domain", 99, 0);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_PersistedPolicyWinsOverStaticFallbacks() {
        ISnapshotPolicyResolver resolver = Substitute.For<ISnapshotPolicyResolver>();
        _ = resolver.GetIntervalAsync("acme", "payments", "InvoiceAggregate", Arg.Any<CancellationToken>())
            .Returns(12);
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25 }
        };
        (SnapshotManager manager, _) = CreateManager(options, resolver);

        bool result = await manager.ShouldCreateSnapshotAsync("acme", "payments", "InvoiceAggregate", 12, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_PersistedPolicyIsAggregateTypeSpecific() {
        ISnapshotPolicyResolver resolver = Substitute.For<ISnapshotPolicyResolver>();
        _ = resolver.GetIntervalAsync("acme", "payments", "InvoiceAggregate", Arg.Any<CancellationToken>())
            .Returns(10);
        _ = resolver.GetIntervalAsync("acme", "payments", "OrderAggregate", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        var options = new SnapshotOptions { DefaultInterval = 100 };
        (SnapshotManager manager, _) = CreateManager(options, resolver);

        bool invoice = await manager.ShouldCreateSnapshotAsync("acme", "payments", "InvoiceAggregate", 10, 0);
        bool order = await manager.ShouldCreateSnapshotAsync("acme", "payments", "OrderAggregate", 10, 0);

        invoice.ShouldBeTrue();
        order.ShouldBeFalse();
    }

    [Fact]
    public void SnapshotOptions_TenantDomainIntervalBelowMinimum_ValidationFails() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            TenantDomainIntervals = new Dictionary<string, int> { ["bad-tenant:bad-domain"] = 5 }
        };

        Should.Throw<InvalidOperationException>(options.Validate)
            .Message.ShouldContain("bad-tenant:bad-domain");
    }

    [Fact]
    public void SnapshotOptions_ValidTenantDomainIntervals_ValidationPasses() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["payments"] = 50 },
            TenantDomainIntervals = new Dictionary<string, int> { ["acme:payments"] = 25, ["big-corp:audit"] = 200 }
        };

        Should.NotThrow(options.Validate);
    }

    [Fact]
    public async Task ShouldCreateSnapshot_NullTenantId_ThrowsArgumentException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            manager.ShouldCreateSnapshotAsync(null!, "test-domain", 100, 0));
    }

    [Fact]
    public async Task ShouldCreateSnapshot_EmptyTenantId_ThrowsArgumentException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            manager.ShouldCreateSnapshotAsync("", "test-domain", 100, 0));
    }
}
