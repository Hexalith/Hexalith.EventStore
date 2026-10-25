using System.Text;
using System.Text.Json;

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

    [Theory]
    [InlineData("interaction-a", "interaction-b")]
    [InlineData("alias-a", "alias-b")]
    [InlineData("tenant-a", "tenant-b")]
    public async Task MixedTargetDirectorySnapshotStagesNothingAndConsultsNoAuthority(string before, string after)
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
        fixture.Bindings.ReceivedCalls().ShouldBeEmpty();
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
