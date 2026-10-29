using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.PayloadProtection.Tests;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Shipping Server persistence/distribution component checks; in-memory committed state is not Live provider qualification.</summary>
public sealed class InteractionProtectionPersistenceTests
{
    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("binary")]
    [InlineData("JSON")]
    public async Task NoOpFormatLabelCannotHideReservedJsonContent(string format)
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        var state = new InMemoryStateManager();
        var noOp = new NoOpEventPayloadProtectionService();
        var persister = new EventPersister(state, NullLogger<EventPersister>.Instance, noOp);
        var owner = fixture.Binding.Identity.Owner;
        var command = new CommandEnvelope("message", owner.TenantId, owner.Domain, owner.AggregateId, "Create", [1], "correlation", null, "actor", null);
        var payload = new SerializedDomainEventPayload(fixture.Binding.Identity.PayloadTypeId,
            InteractionPayloadProtectionHookFixture.Payload(), format, null, null, null);
        await Should.ThrowAsync<InvalidOperationException>(() => persister.PersistEventsAsync(owner, "Interaction", command,
            DomainResult.Success([payload]), "v1", TestContext.Current.CancellationToken));
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        state.CommittedState.ShouldBeEmpty();
        (await noOp.TryUnprotectEventPayloadAsync(owner, "event", payload.PayloadBytes, format, null,
            TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public async Task FailedPreparationRetainsOriginalWhenIndependentNeverWrittenProofIsUnavailable()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null));
        var state = new InMemoryStateManager();
        var persister = new EventPersister(state, NullLogger<EventPersister>.Instance, fixture.Service);
        var owner = fixture.Binding.Identity.Owner;
        var command = new CommandEnvelope("message", owner.TenantId, owner.Domain, owner.AggregateId, "Create", [1], "correlation", null, "actor", null);
        var valid = new SerializedDomainEventPayload(fixture.Binding.Identity.PayloadTypeId,
            InteractionPayloadProtectionHookFixture.Payload(), "json", null, null, null);
        var invalid = valid with { PayloadBytes = "{\"ProtectedContent\":{}}"u8.ToArray() };
        await Should.ThrowAsync<InvalidOperationException>(() => persister.PersistEventsAsync(owner, "Interaction", command,
            DomainResult.Success([valid, invalid]), "v1", TestContext.Current.CancellationToken));
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        byte[] originalCiphertext = fixture.Occurrences.Record.Sealed!.PayloadBytes.ToArray();
        state.CommittedState.ShouldBeEmpty();
        EventPersistResult retry = await Persist(fixture, persister);
        retry.PersistedEnvelopes[0].Payload.ShouldBe(originalCiphertext);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        await fixture.Service.RecoverOriginalCommittedOccurrenceAsync(owner, fixture.Occurrence, TestContext.Current.CancellationToken);
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
    }

    [Fact]
    public async Task DefaultRegistrationRefusesReservedContentBeforeEventStagingOrPublication()
    {
        var services = new ServiceCollection();
        services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPayloadProtectionService noOp = provider.GetRequiredService<IEventPayloadProtectionService>();
        noOp.ShouldBeOfType<NoOpEventPayloadProtectionService>();
        var fixture = new InteractionPayloadProtectionHookFixture();
        var state = new InMemoryStateManager();
        var persister = new EventPersister(state, NullLogger<EventPersister>.Instance, noOp);
        await Should.ThrowAsync<InvalidOperationException>(() => Persist(fixture, persister));
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        state.CommittedState.ShouldBeEmpty();
        PayloadUnprotectionOutcome read = await noOp.TryUnprotectEventPayloadAsync(fixture.Binding.Identity.Owner,
            fixture.Binding.Identity.PayloadTypeId, InteractionPayloadProtectionHookFixture.Payload(), "json", null,
            TestContext.Current.CancellationToken);
        read.IsUnreadable.ShouldBeTrue();
        read.PayloadBytes.ShouldBeNull();
    }

    [Fact]
    public void ExplicitRegistrationDoesNotManufactureMissingCustodyOrBindingAuthority()
    {
        var services = new ServiceCollection();
        services.AddInteractionPayloadProtection();
        using ServiceProvider provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IEventPayloadProtectionService>());
    }

    [Fact]
    public async Task CommittedEventAndProjectionStaySealedAfterRootErasure()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        var state = new InMemoryStateManager();
        var persister = new EventPersister(state, NullLogger<EventPersister>.Instance, fixture.Service);
        EventPersistResult result = await Persist(fixture, persister);
        state.CommittedState.ShouldBeEmpty();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        result.ProtectionCompletions.Count.ShouldBe(1);
        PayloadProtectionCompletionContext completion = result.ProtectionCompletions[0];
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(completion, TestContext.Current.CancellationToken);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(completion, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        var stored = (EventEnvelope)state.CommittedState[$"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1"];
        stored.SerializationFormat.ShouldBe("json+pdenc-v2");
        Encoding.UTF8.GetString(stored.Payload).ShouldNotContain("confidential-canary");
        EventLogicalDigest.RequireMatching(stored, stored.SerializationFormat, stored.Payload);
        fixture.Occurrences.Keys.RootResolver = _ => Task.FromResult<Hexalith.EventStore.PayloadProtection.InteractionOccurrenceOwnedKey?>(null);
        fixture.Erased = true;
        ProjectionEventReadabilityResult projection = await ProjectionEventWireBuilder.BuildAsync(fixture.Service,
            fixture.Binding.Identity.Owner, [stored], TestContext.Current.CancellationToken);
        projection.UnreadableReason.ShouldBeNull();
        projection.Events![0].Payload.ShouldBe(stored.Payload);
        projection.Events[0].SerializationFormat.ShouldBe("json+pdenc-v2");
        var dapr = Substitute.For<DaprClient>();
        EventEnvelope? published = null;
        dapr.PublishEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<EventEnvelope>(value => published = value),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var publisher = new EventPublisher(dapr, Options.Create(new EventPublisherOptions()), NullLogger<EventPublisher>.Instance,
            fixture.Service, new NoOpProjectionUpdateOrchestrator());
        (await publisher.PublishEventsAsync(fixture.Binding.Identity.Owner, [stored], "correlation",
            TestContext.Current.CancellationToken, triggerProjectionUpdate: false)).Success.ShouldBeTrue();
        published.ShouldNotBeNull().Payload.ShouldBe(stored.Payload);
        state.CommittedState[$"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1"].ShouldBeSameAs(stored);
    }

    [Theory]
    [InlineData("{\"ProtectedContent\":{}}")]
    [InlineData("{\"nested\":[{\"protectedcontent\":{}}]}")]
    public async Task NoOpRefusesReservedSnapshotAndEventShapes(string json)
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        var noOp = new NoOpEventPayloadProtectionService();
        using JsonDocument document = JsonDocument.Parse(json);
        await Should.ThrowAsync<InvalidOperationException>(() => noOp.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            document.RootElement, TestContext.Current.CancellationToken));
        (await noOp.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner, document.RootElement, null,
            TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public async Task DirectorySnapshotPersistsOnlyCiphertextAndRetryKeepsTheOriginalInteractionRoot()
    {
        var fixture = SnapshotFixture();
        var state = new InMemoryStateManager();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        using JsonDocument content = JsonDocument.Parse(InteractionPayloadProtectionHookFixture.Payload());
        using JsonDocument source = JsonDocument.Parse("{\"Nested\":[{\"Request\":" + content.RootElement.GetProperty("ProtectedContent").GetRawText() + "}]}");
        List<PayloadProtectionCompletionContext> completions = [];
        (await manager.StageSnapshotAsync(fixture.Binding.Identity.Owner, 1, source.RootElement, state, JsonSerializerOptions.Web,
            SnapshotTestLimits.ForState(), "correlation", TestContext.Current.CancellationToken, true, completions)).ShouldBeTrue();
        state.CommittedState.ShouldBeEmpty();
        PayloadProtectionCompletionContext original = completions.ShouldHaveSingleItem();
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(original, TestContext.Current.CancellationToken);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(original, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        var stored = state.CommittedState[fixture.Binding.Identity.Owner.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        JsonSerializer.Serialize(stored).ShouldNotContain("confidential-canary");
        stored.State.ShouldBeOfType<ProtectedSnapshotPayloadV2>().Envelope.ShouldNotBeNullOrWhiteSpace();
        fixture.Occurrences.Record!.Request.Identity.Target.AgentInteractionId.ShouldBe("interaction-a");
        fixture.Occurrences.Record.Request.Identity.Owner.AggregateId.ShouldBe("directory");
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        SnapshotUnprotectionOutcome read = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner,
            stored.State, stored.ProtectionMetadata, SnapshotStateJsonContext.Default.JsonElement, fixture.Occurrence,
            TestContext.Current.CancellationToken);
        read.IsReadable.ShouldBeTrue();
        read.State.ShouldBeOfType<JsonElement>().GetProperty("Nested")[0].GetProperty("Request")
            .GetProperty("Value").GetProperty("Secret").GetString().ShouldBe("confidential-canary");

        completions.Clear();
        (await manager.StageSnapshotAsync(fixture.Binding.Identity.Owner, 1, source.RootElement, state, JsonSerializerOptions.Web,
            SnapshotTestLimits.ForState(), "retry", TestContext.Current.CancellationToken, true, completions)).ShouldBeTrue();
        completions.ShouldHaveSingleItem().ShouldBe(original);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        state.CommittedState[fixture.Binding.Identity.Owner.SnapshotKey].ShouldBeOfType<SnapshotRecord>().State.ShouldBe(stored.State);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        fixture.Occurrences.Keys.WriteRoots.ShouldBe(1);
    }

    /// <summary>Server commit/retry and transported cache replay preserve each enrolled root, including partial erasure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnrolledMixedSnapshotCommitsOriginalCiphertextsAndReplaysFromSerializedCache(bool eraseFirst)
    {
        var fixture = new InteractionMixedSnapshotPersistenceFixture();
        var state = new InMemoryStateManager();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        List<PayloadProtectionCompletionContext> completions = [];
        (await manager.StageSnapshotAsync(fixture.Owner, 1, fixture.Source, state, JsonSerializerOptions.Web, SnapshotTestLimits.ForState(),
            "mixed", TestContext.Current.CancellationToken, true, completions)).ShouldBeTrue();
        state.CommittedState.ShouldBeEmpty();
        PayloadProtectionCompletionContext originalCompletion = completions.ShouldHaveSingleItem();
        originalCompletion.SnapshotEntryManifest.ShouldBe(fixture.Entries);
        originalCompletion.SnapshotContainerDigest.ShouldBe(Convert.ToHexString(SHA256.HashData(fixture.Skeleton)));
        originalCompletion.SnapshotEntries!.Length.ShouldBe(2);
        fixture.Records.All(record => record.WriterState == InteractionOccurrenceWriterState.SealedPending).ShouldBeTrue();
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(originalCompletion, TestContext.Current.CancellationToken);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(originalCompletion, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);

        var stored = state.CommittedState[fixture.Owner.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        var carrier = stored.State.ShouldBeOfType<ProtectedSnapshotEntriesV2>();
        carrier.SkeletonBytes.ShouldBe(fixture.Skeleton);
        byte[] originalCarrier = JsonSerializer.SerializeToUtf8Bytes(carrier, JsonSerializerOptions.Web);
        Encoding.UTF8.GetString(originalCarrier).ShouldNotContain("secret-");
        Encoding.UTF8.GetString(carrier.SkeletonBytes).ShouldNotContain("secret-");
        byte[][] originals = fixture.Records.Select(record => record.Sealed!.PayloadBytes.ToArray()).ToArray();
        for (int index = 0; index < originals.Length; index++)
        {
            carrier.Entries[index].Path.ShouldBe(fixture.Entries[index].Path);
            carrier.Entries[index].Target.ShouldBe(fixture.Entries[index].Target);
            carrier.Entries[index].Protected.ShouldBe(JsonSerializer.Deserialize<ProtectedSnapshotPayloadV2>(originals[index]));
            fixture.Records[index].Request.Identity.ShouldBe(fixture.Bindings[index].Identity);
        }
        var loaded = (await manager.LoadSnapshotAsync(fixture.Owner, state, "before-erasure", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(loaded.State), JsonSerializer.SerializeToNode(fixture.Source)).ShouldBeTrue();

        completions.Clear();
        (await manager.StageSnapshotAsync(fixture.Owner, 1, fixture.Source, state, JsonSerializerOptions.Web, SnapshotTestLimits.ForState(),
            "retry", TestContext.Current.CancellationToken, true, completions)).ShouldBeTrue();
        var retryCompletion = completions.ShouldHaveSingleItem();
        JsonSerializer.SerializeToUtf8Bytes(retryCompletion).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(originalCompletion));
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(retryCompletion, TestContext.Current.CancellationToken);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(retryCompletion, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        var retry = state.CommittedState[fixture.Owner.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        JsonSerializer.SerializeToUtf8Bytes(retry.State, JsonSerializerOptions.Web).ShouldBe(originalCarrier);

        // Dapr returns a serialized cell after activation, whereas the original turn caches SnapshotRecord.
        JsonElement cached = JsonSerializer.SerializeToElement(retry, JsonSerializerOptions.Web);
        byte[] originalCache = JsonSerializer.SerializeToUtf8Bytes(cached);
        await state.SetStateAsync(fixture.Owner.SnapshotKey, cached, TestContext.Current.CancellationToken);
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        fixture.EraseFirst = eraseFirst;
        if (eraseFirst)
        {
            (await fixture.Service.TryUnprotectSnapshotAsync(fixture.Owner, retry.State, retry.ProtectionMetadata,
                SnapshotStateJsonContext.Default.JsonElement, fixture.Occurrence, TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();
        }
        var restarted = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        var replay = (await restarted.LoadSnapshotAsync(fixture.Owner, state, "cache-replay", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var restored = replay.State.ShouldBeOfType<JsonElement>();
        restored.GetProperty("Safe").GetInt32().ShouldBe(7);
        var first = restored.GetProperty("Entries")[0].GetProperty("ProtectedContent").GetProperty("Value");
        if (eraseFirst) { first.GetProperty("State").GetString().ShouldBe("Erased"); }
        else { first.GetProperty("Secret").GetString().ShouldBe("secret-a"); }
        restored.GetProperty("Entries")[1].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("Secret").GetString().ShouldBe("secret-b");
        JsonSerializer.SerializeToUtf8Bytes(state.CommittedState[fixture.Owner.SnapshotKey]).ShouldBe(originalCache);
        fixture.Keys.Leases.ShouldBe(2);
        fixture.Keys.WriteRoots.ShouldBe(2);
        for (int index = 0; index < originals.Length; index++)
        {
            fixture.Records[index].WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
            fixture.Records[index].Sealed!.PayloadBytes.ShouldBe(originals[index]);
        }
        fixture.OwnedBuffers.ShouldNotBeEmpty();
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("interaction-a", "interaction-b")]
    [InlineData("alias-a", "alias-b")]
    [InlineData("tenant-a", "tenant-b")]
    public async Task UnenrolledMixedTargetDirectorySnapshotStagesNothingBeforeRegistryOrKeyEffects(string before, string after)
    {
        var fixture = SnapshotFixture();
        var state = new InMemoryStateManager();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        string first = Encoding.UTF8.GetString(InteractionPayloadProtectionHookFixture.Payload());
        using JsonDocument source = JsonDocument.Parse("{\"Nested\":[" + first + "," + first.Replace(before, after, StringComparison.Ordinal) + "]}");
        List<PayloadProtectionCompletionContext> completions = [];
        (await manager.StageSnapshotAsync(fixture.Binding.Identity.Owner, 1, source.RootElement, state, JsonSerializerOptions.Web,
            SnapshotTestLimits.ForState(), "correlation", TestContext.Current.CancellationToken, false, completions)).ShouldBeFalse();
        await state.SaveStateAsync(TestContext.Current.CancellationToken);
        state.CommittedState.ShouldBeEmpty();
        completions.ShouldBeEmpty();
        if (before == "tenant-a")
        {
            fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
        }
        else
        {
            var target = fixture.Binding.Identity.Target;
            SnapshotProtectionEntry[] expected =
            [
                new("/Nested/0/ProtectedContent/Value", target),
                new("/Nested/1/ProtectedContent/Value", target with
                {
                    AgentInteractionId = target.AgentInteractionId.Replace(before, after, StringComparison.Ordinal),
                    TargetProtectionKeyAlias = target.TargetProtectionKeyAlias.Replace(before, after, StringComparison.Ordinal),
                }),
            ];
            JsonNode skeleton = JsonNode.Parse(source.RootElement.GetRawText())!;
            foreach (JsonNode? entry in skeleton["Nested"]!.AsArray()) { entry!["ProtectedContent"]!["Value"] = null; }
            string digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(skeleton)));
            fixture.Bindings.ReceivedCalls().ShouldHaveSingleItem().GetMethodInfo().Name
                .ShouldBe(nameof(Hexalith.EventStore.PayloadProtection.IInteractionProtectionBindingProvider.ResolveSnapshotEntriesAsync));
            await fixture.Bindings.Received(1).ResolveSnapshotEntriesAsync(fixture.Binding.Identity.Owner, fixture.Occurrence,
                digest, Arg.Is<IReadOnlyList<SnapshotProtectionEntry>>(entries => entries.SequenceEqual(expected)), true,
                Arg.Any<CancellationToken>());
        }
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Record.ShouldBeNull();
        fixture.Occurrences.OwnedBuffers.ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(0);
    }

    private static InteractionPayloadProtectionHookFixture SnapshotFixture()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Binding = fixture.Binding with
        {
            Identity = fixture.Binding.Identity with
            {
                Owner = new("tenant-a", "agents", "directory"),
                Kind = PayloadProtectionPayloadKind.Snapshot,
                PayloadTypeId = "hx-snapshot-v1:folded-state",
            },
        };
        return fixture;
    }

    private static Task<EventPersistResult> Persist(InteractionPayloadProtectionHookFixture fixture, EventPersister persister)
    {
        var owner = fixture.Binding.Identity.Owner;
        var command = new CommandEnvelope("message", owner.TenantId, owner.Domain, owner.AggregateId, "Create", [1],
            "correlation", null, "actor", null);
        var payload = new SerializedDomainEventPayload(fixture.Binding.Identity.PayloadTypeId,
            InteractionPayloadProtectionHookFixture.Payload(), "json", null, null, null);
        return persister.PersistEventsAsync(owner, "Interaction", command, DomainResult.Success([payload]), "v1",
            TestContext.Current.CancellationToken);
    }
}
