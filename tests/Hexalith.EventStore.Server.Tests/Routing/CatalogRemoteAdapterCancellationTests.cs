using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogRemoteAdapterCancellationTests
{
    [Theory]
    [InlineData("events", false)]
    [InlineData("dead-letter", false)]
    [InlineData("projection", false)]
    [InlineData("projection-detail", false)]
    [InlineData("correlation", false)]
    [InlineData("events", true)]
    [InlineData("dead-letter", true)]
    [InlineData("projection", true)]
    [InlineData("projection-detail", true)]
    [InlineData("correlation", true)]
    public async Task ActualAdapterDistinguishesPreCancellationFromDispatchedLostResponse(string adapter, bool dispatched)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        DaprClient backend = Substitute.For<DaprClient>();
        ConfigureLostResponses(backend);
        Func<CancellationToken, Task> invoke = Adapter(adapter, backend);
        using var cancellation = new CancellationTokenSource();
        if (!dispatched) await cancellation.CancelAsync();

        await using (var lease = await fixture.Authorities[0].AcquireAsync())
        {
            using var scope = RouteCatalogRemoteCall.Enter(lease);
            if (!dispatched)
                await Should.ThrowAsync<OperationCanceledException>(() => invoke(cancellation.Token));
            else if (adapter is "events" or "dead-letter")
                await invoke(cancellation.Token); // These publishers preserve their advisory failure results.
            else
                await Should.ThrowAsync<TimeoutException>(() => invoke(cancellation.Token));
        }

        int writes = backend.ReceivedCalls().Count(call => call.GetMethodInfo().Name is "PublishEventAsync" or "TrySaveStateAsync");
        writes.ShouldBe(dispatched ? 1 : 0);
        var leases = (await fixture.Store.ReadAsync(default)).State.Leases;
        if (dispatched)
        {
            leases.ShouldHaveSingleItem();
            var successor = fixture.Candidate(2, first.RootDigest);
            await fixture.Authorities[0].PrepareAsync(successor);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(successor.AttemptId));
        }
        else leases.ShouldBeEmpty();
    }

    private static Func<CancellationToken, Task> Adapter(string adapter, DaprClient backend)
    {
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-one");
        switch (adapter)
        {
            case "events":
                var events = new EventPublisher(backend, Options.Create(new EventPublisherOptions()),
                    NullLogger<EventPublisher>.Instance, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator());
                var envelope = new EventEnvelope("event-one", identity.AggregateId, "counter", identity.TenantId, identity.Domain,
                    1, 0, DateTimeOffset.UtcNow, "correlation-one", "cause-one", "service", "v1", "incremented", 1, "json", [123, 125], null);
                return token => events.PublishEventsAsync(identity, [envelope], "correlation-one", token, triggerProjectionUpdate: false);
            case "dead-letter":
                var letters = new DeadLetterPublisher(backend, Options.Create(new EventPublisherOptions()), NullLogger<DeadLetterPublisher>.Instance);
                var command = new CommandEnvelope("command-one", identity.TenantId, identity.Domain, identity.AggregateId,
                    "increment-counter", [123, 125], "correlation-one", null, "service", null);
                var message = DeadLetterMessage.FromException(command, CommandStatus.Processing, new InvalidOperationException("Fixture failure."));
                return token => letters.PublishDeadLetterAsync(identity, message, token);
            case "projection":
            case "projection-detail":
                IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
                issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<string?>("fixture-provenance"));
                var notifier = new DaprProjectionChangeNotifier(backend, Substitute.For<IActorProxyFactory>(),
                    Substitute.For<IProjectionChangedBroadcaster>(), Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub }),
                    NullLogger<DaprProjectionChangeNotifier>.Instance, issuer);
                return adapter == "projection"
                    ? token => notifier.NotifyProjectionChangedAsync("counter", identity.TenantId, identity.AggregateId, token)
                    : token => notifier.NotifyProjectionChangedAsync(new ProjectionChangedDetail("counter", identity.TenantId, null, new Dictionary<string, string>()), token);
            case "correlation":
                var index = new DaprCommandCorrelationIndex(backend, Options.Create(new CommandStatusOptions()),
                    Options.Create(new CommandCorrelationIndexOptions()), NullLogger<DaprCommandCorrelationIndex>.Instance);
                return token => index.AddAsync(identity.TenantId, "correlation-one", "command-one", token);
            default:
                throw new ArgumentOutOfRangeException(nameof(adapter));
        }
    }

    private static void ConfigureLostResponses(DaprClient backend)
    {
        backend.PublishEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new TimeoutException("Publish response lost.")));
        backend.PublishEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new TimeoutException("Publish response lost.")));
        backend.PublishEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Hexalith.EventStore.Contracts.Projections.ProjectionChangedNotification>(),
            Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new TimeoutException("Publish response lost.")));
        backend.GetStateAndETagAsync<CommandCorrelationIndexRecord>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns((default(CommandCorrelationIndexRecord)!, string.Empty));
        backend.TrySaveStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandCorrelationIndexRecord>(), Arg.Any<string>(),
            Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<bool>(new TimeoutException("State persisted; response lost.")));
    }
}
