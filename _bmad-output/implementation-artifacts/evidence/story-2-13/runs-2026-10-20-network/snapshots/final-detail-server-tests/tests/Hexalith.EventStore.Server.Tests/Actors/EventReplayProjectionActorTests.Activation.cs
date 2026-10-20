using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public partial class EventReplayProjectionActorTests
{
    [Fact]
    public async Task OwningDaprDependencyInjectionFactorySelectsLifecycleFencedPublicationConstructor()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(Substitute.For<IETagService>());
        registrations.AddSingleton(Substitute.For<IProjectionChangeNotifier>());
        registrations.AddSingleton(Substitute.For<IActorProxyFactory>());
        registrations.AddActors(_ => { });
        using ServiceProvider services = registrations.BuildServiceProvider();
        ActorHost host = ActorHost.CreateForTest<EventReplayProjectionActor>(new ActorTestOptions { ActorId = new ActorId(ActorIdString) });
        ActorActivator activator = services.GetRequiredService<ActorActivatorFactory>().CreateActivator(host.ActorTypeInfo);
        ActorActivatorState activated = await activator.CreateAsync(host).ConfigureAwait(true);
        try { activated.Actor.ShouldBeOfType<EventReplayProjectionActor>(); }
        finally { await activator.DeleteAsync(activated).ConfigureAwait(true); }
    }
}
