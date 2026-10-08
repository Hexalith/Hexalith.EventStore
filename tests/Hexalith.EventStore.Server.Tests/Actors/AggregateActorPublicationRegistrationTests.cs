using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Actual aggregate persistence never commits events ahead of a configured namespace registration.</summary>
public sealed class AggregateActorPublicationRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredRegistrationPrecedesEventCommitAndRefusalPreventsAppend(bool refuse)
    {
        var backend = new InMemoryStateManager(); var context = CreateActor(stateManager: backend);
        context.Invoker.InvokeAsync(Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        var registration = Substitute.For<ISourcePublicationWriterRegistration>();
        int registered = 0;
        registration.RegisterBeforeWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            registered++; backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty();
            call.Arg<AggregateIdentity>().ActorId.ShouldBe("test-tenant:test-domain:agg-001");
            return refuse ? Task.FromException(new InvalidOperationException("Controlled namespace registration refusal.")) : Task.CompletedTask;
        });
        using var services = new ServiceCollection().AddSingleton(registration).BuildServiceProvider();
        var actor = new AggregateActor(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new("test-tenant:test-domain:agg-001") }),
            context.Logger, context.Invoker, context.SnapshotManager, new NoOpEventPayloadProtectionService(), context.StatusStore,
            context.EventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), context.DeadLetterPublisher,
            serviceProvider: services, commandAggregateTypeResolver: context.AggregateTypeResolver);
        ActorStateManagerTestHelper.SetStateManager(actor, backend);
        var result = await actor.ProcessCommandAsync(CreateTestEnvelope());
        registered.ShouldBe(1);
        if (refuse) { backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty(); result.Accepted.ShouldBeFalse(); }
        else { backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(1); result.Accepted.ShouldBeTrue(); }
    }
}
