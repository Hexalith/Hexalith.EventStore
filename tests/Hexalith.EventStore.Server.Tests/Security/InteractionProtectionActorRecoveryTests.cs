using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.PayloadProtection.Tests;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Actors;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual engine/actor coordination tests with independently inspected in-memory committed source state.</summary>
public sealed class InteractionProtectionActorRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessCommandCommitsOriginalJournalAndCompletesBeforePublication(bool loseSaveAcknowledgement)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        if (loseSaveAcknowledgement) { state.FaultAfterCall("SaveState", 2, new InvalidOperationException("lost acknowledgement")); }
        ActorTestContext actor = Actor(fixture, state);
        CommandEnvelope command = Command(fixture, "create");
        (await actor.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        PayloadProtectionCompletionJournal journal = state.CommittedState[PayloadProtectionCompletionJournal.StateName]
            .ShouldBeOfType<PayloadProtectionCompletionJournal>();
        journal.Completed.ShouldBeTrue();
        journal.Originals.ShouldHaveSingleItem().OperationId.ShouldBe(fixture.Binding.OperationId);
        var stored = state.CommittedState[$"{command.AggregateIdentity.EventStreamKeyPrefix}1"].ShouldBeOfType<EventEnvelope>();
        stored.Payload.ShouldBe(fixture.Occurrences.Record.Sealed!.PayloadBytes);
        stored.Extensions!.Values.ShouldNotContain(fixture.Binding.OperationId);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        await actor.EventPublisher.Received(1).PublishEventsAsync(command.AggregateIdentity,
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 1), command.CorrelationId,
            Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task CompletionInterruptionRecoversOriginalJournalOnEventsStoredResume()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        bool available = false;
        ConfigureWriterProof(fixture, state, () => available);
        ActorTestContext actor = Actor(fixture, state);
        CommandEnvelope command = Command(fixture, "interrupted");
        await Should.ThrowAsync<PayloadProtectionRecoveryException>(() => actor.Actor.ProcessCommandAsync(command));
        var pending = state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>();
        pending.Completed.ShouldBeFalse();
        state.CommittedState[$"{command.AggregateIdentity.PipelineKeyPrefix}{command.CorrelationId}"]
            .ShouldBeOfType<PipelineState>().CurrentStage.ShouldBe(CommandStatus.EventsStored);
        await actor.EventPublisher.DidNotReceiveWithAnyArgs().PublishEventsAsync(default!, default!, default!, default, default);
        available = true;
        await state.ClearCacheAsync();
        ActorTestContext resumed = Actor(fixture, state);
        (await resumed.Actor.ProcessCommandAsync(command)).Accepted.ShouldBeTrue();
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>()
            .Originals.ShouldBe(pending.Originals);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        resumed.Invoker.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfirmedCommitWithLostCancellationResultCompletesUsingIndependentRecoveryBudget()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        state.FaultAfterCall("SaveState", 2, new OperationCanceledException());
        ActorTestContext actor = Actor(fixture, state);
        await Should.ThrowAsync<OperationCanceledException>(() => actor.Actor.ProcessCommandAsync(Command(fixture, "lost-result")));
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState.ContainsKey(PayloadProtectionCompletionJournal.StateName).ShouldBeTrue();
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
    }

    [Fact]
    public async Task ProductionActorFoldsErasedWhileExternalProductionReaderRemainsUnreadable()
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        using EventEvolutionManifestCandidate candidate = Candidate();
        var services = new ServiceCollection();
        services.AddKeyedSingleton("d", candidate);
        using ServiceProvider provider = services.BuildServiceProvider();
        ActorTestContext actor = Actor(fixture, state, provider);
        (await actor.Actor.ProcessCommandAsync(Command(fixture, "first"))).Accepted.ShouldBeTrue();
        fixture.Occurrences.Keys.RootResolver = _ => Task.FromResult<Hexalith.EventStore.PayloadProtection.InteractionOccurrenceOwnedKey?>(null);
        fixture.Erased = true;
        object? observed = null;
        actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => { observed = call.ArgAt<object?>(1); return DomainResult.NoOp(); });
        (await actor.Actor.ProcessCommandAsync(Command(fixture, "second"))).Accepted.ShouldBeTrue();
        var domain = observed.ShouldBeOfType<DomainServiceCurrentState>();
        using JsonDocument erased = JsonDocument.Parse(domain.Events.Single().Payload);
        erased.RootElement.GetProperty("Safe").GetInt32().ShouldBe(1);
        erased.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe(nameof(Erased));
        DaprProductionLogicalEventReader external = DaprProductionLogicalEventReader.FromCallerPin(state, fixture.Service, candidate)!;
        await Should.ThrowAsync<ProtectedDataUnreadableException>(() => external.ReadPageAsync(
            fixture.Binding.Identity.Owner, "r", 1, 1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("lineage")]
    public async Task ActualEngineLogicalReaderRequiresOriginalCiphertextDigestBeforeProviderCalls(string mutation)
    {
        var fixture = Fixture();
        var state = new FaultInjectingActorStateManager();
        ConfigureWriterProof(fixture, state, static () => true);
        (await Actor(fixture, state).Actor.ProcessCommandAsync(Command(fixture, "first"))).Accepted.ShouldBeTrue();
        string key = $"{fixture.Binding.Identity.Owner.EventStreamKeyPrefix}1";
        var source = (EventEnvelope)state.CommittedState[key];
        EventEnvelope changed = mutation switch {
            "missing" => source with { ApplicationPayloadDigest = null },
            "changed" => source with { ApplicationPayloadDigest = new string('0', 64) },
            _ => source with { CorrelationId = "substituted" },
        };
        await state.SeedCommittedStateAsync(new Dictionary<string, object> { [key] = changed });
        using EventEvolutionManifestCandidate candidate = Candidate();
        DaprProductionLogicalEventReader reader = DaprProductionLogicalEventReader.FromCallerPin(state, fixture.Service, candidate)!;
        fixture.Occurrences.Registry.ClearReceivedCalls();
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(
            fixture.Binding.Identity.Owner, "r", 1, 1, TestContext.Current.CancellationToken));
        failure.Message.ShouldContain("LogicalDigestMismatch");
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        source.Payload.ShouldBe(changed.Payload);
    }

    [Fact]
    public async Task ManualSnapshotLostSaveAcknowledgementCompletesExactOriginalBeforeCreated()
    {
        var fixture = Fixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with {
            Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:folded-state" } };
        var state = new FaultInjectingActorStateManager();
        AggregateIdentity owner = fixture.Binding.Identity.Owner;
        var source = new EventEnvelope(InteractionOccurrenceProtectorFixture.Reference, owner.AggregateId, "r", owner.TenantId, owner.Domain, 1, 1,
            DateTimeOffset.UnixEpoch, "source", "source", "actor", "v1", "Legacy.Event", 1, "json", "{}"u8.ToArray(), null);
        await state.SeedCommittedStateAsync(new Dictionary<string, object> {
            [owner.MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null),
            [$"{owner.EventStreamKeyPrefix}1"] = source,
        });
        var reconstructor = Substitute.For<IAggregateStateReconstructor>();
        reconstructor.ReconstructAsync(owner, "r", Arg.Any<IReadOnlyList<EventEnvelope>>(), 1, false,
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(AggregateReconstructionResult.Succeeded("{\"count\":1}", 1));
        var services = new ServiceCollection();
        services.AddSingleton(reconstructor);
        services.AddSingleton(new FoldedSnapshotQualificationRegistry([new FoldedSnapshotQualification("d", "r", typeof(JsonElement),
            "profile", "parity", SnapshotTestLimits.ForState())]));
        using ServiceProvider provider = services.BuildServiceProvider();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()), NullLogger<SnapshotManager>.Instance, fixture.Service);
        ActorTestContext actor = CreateActor(fixture.Service, stateManager: state, snapshotManager: snapshots,
            serviceProvider: provider, actorId: owner.ActorId);
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), true, Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (!state.CommittedState.ContainsKey(owner.SnapshotKey))
            { return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null); }
            fixture.Occurrences.Record = fixture.Occurrences.Record! with {
                WriterState = InteractionOccurrenceWriterState.Active, WriterProofId = call.ArgAt<string>(2) };
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed, fixture.Occurrences.Record);
        });
        state.FaultAfterCall("SaveState", 1, new InvalidOperationException("lost snapshot save acknowledgement"));
        ManualSnapshotResult result = await actor.Actor.CreateManualSnapshotAsync("snapshot");
        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created, result.ReasonCode + "; " + string.Join(" | ", state.Trace));
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        state.CommittedState[PayloadProtectionCompletionJournal.StateName].ShouldBeOfType<PayloadProtectionCompletionJournal>()
            .Originals.ShouldHaveSingleItem().Occurrence.PayloadKind.ShouldBe(PayloadProtectionPayloadKind.Snapshot);
    }

    internal static InteractionPayloadProtectionHookFixture Fixture()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with {
            Owner = new AggregateIdentity("tenant-a", "d", "interaction-a"), PayloadTypeId = "Legacy.Event" } };
        return fixture;
    }

    internal static EventEvolutionManifestCandidate Candidate()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) { root = root.Parent; }
        string path = Path.Combine(root!.FullName, "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        var fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        ReadOnlyMemory<byte>[] rows = [Convert.FromHexString(fixture["AliasRow"]), Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]), Convert.FromHexString(fixture["SharedRow"])];
        using var registry = new EventDomainRegistry("d", rows);
        return new("d", rows, registry.Fingerprint, 0);
    }

    private static CommandEnvelope Command(InteractionPayloadProtectionHookFixture fixture, string id)
        => new(id, fixture.Binding.Identity.Owner.TenantId, "d", "interaction-a", "Create", [1], id, null, "actor", null);

    private static ActorTestContext Actor(InteractionPayloadProtectionHookFixture fixture, FaultInjectingActorStateManager state,
        IServiceProvider? services = null)
    {
        ActorTestContext actor = CreateActor(payloadProtectionService: fixture.Service, stateManager: state,
            actorId: fixture.Binding.Identity.Owner.ActorId, serviceProvider: services);
        actor.AggregateTypeResolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>()).Returns("r");
        var payload = new SerializedDomainEventPayload("Legacy.Event", InteractionPayloadProtectionHookFixture.Payload(), "json", null, null, null);
        actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(DomainResult.Success([payload]));
        return actor;
    }

    private static void ConfigureWriterProof(InteractionPayloadProtectionHookFixture fixture, FaultInjectingActorStateManager state, Func<bool> available)
    {
        fixture.Occurrences.Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var identity = call.ArgAt<InteractionOccurrenceIdentity>(0);
            string key = $"{identity.Owner.EventStreamKeyPrefix}{identity.Sequence}";
            if (!available() || !call.ArgAt<bool>(3) || !state.CommittedState.ContainsKey(key))
            { return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null); }
            fixture.Occurrences.Record = fixture.Occurrences.Record! with {
                WriterState = InteractionOccurrenceWriterState.Active, WriterProofId = call.ArgAt<string>(2) };
            return new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Sealed, fixture.Occurrences.Record);
        });
    }
}
