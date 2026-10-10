using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Tests.Events;
using Hexalith.EventStore.Client.TestContracts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

/// <summary>Checks registration discovery and host-start validation.</summary>
public sealed class EventPayloadEvolutionStartupTests
{
    private sealed class UnrelatedLongerLegacyUpcaster : IEventPayloadUpcaster
    {
        public string EventTypeName => "Unrelated." + typeof(LegacyTestEvent).FullName;

        public int FromVersion => 1;

        public JsonObject Upcast(JsonObject payload) => payload;
    }

    [Fact]
    public void DiscoveryIgnoresUnrelatedLongerStepName()
    {
        var registration = new EventPayloadEvolutionRegistration();
        registration.AddKnownType(typeof(LegacyTestEvent));
        registration.AddAssembly(typeof(LegacyTestEvent).Assembly);

        EventPayloadEvolutionRegistry registry = registration.Build();

        registry.Read(typeof(LegacyTestEvent).FullName!, 1, "{\"Amount\":4}"u8.ToArray())
            .EventType.ShouldBe(typeof(LegacyTestEvent));
    }

    [Fact]
    public void DiscoveryAndExplicitRegistrationCountSameUpcasterOnce()
    {
        var registration = new EventPayloadEvolutionRegistration();
        registration.AddKnownType(typeof(VersionTwoTestEvent));
        registration.AddAssembly(typeof(VersionTwoTestEvent).Assembly);
        registration.AddUpcaster(typeof(VersionTwoTestUpcaster));

        EventPayloadEvolutionRegistry registry = registration.Build();

        registry.Read(typeof(VersionTwoTestEvent).FullName!, 1, "{\"Amount\":6}"u8.ToArray())
            .EventType.ShouldBe(typeof(VersionTwoTestEvent));
    }

    [Fact]
    public void DiscoveryAloneFindsKnownEventUpcaster()
    {
        var registration = new EventPayloadEvolutionRegistration();
        registration.AddKnownType(typeof(VersionTwoTestEvent));
        registration.AddAssembly(typeof(VersionTwoTestEvent).Assembly);

        EventPayloadEvolutionRegistry registry = registration.Build();

        registry.Read(typeof(VersionTwoTestEvent).FullName!, 1, "{\"Amount\":7}"u8.ToArray())
            .PayloadVersion.ShouldBe(2);
    }

    [Fact]
    public void DiscoveryIncludesVersionLinkedLongerHistoricalPredecessor()
    {
        var registration = new EventPayloadEvolutionRegistration();
        registration.AddKnownType(typeof(ChainedEvent));
        registration.AddAssembly(typeof(ChainedEvent).Assembly);

        EventPayloadEvolutionRegistry registry = registration.Build();

        ResolvedEventPayload result = registry.Read("Historical.ChainedEvent", 1, "{\"Value\":7}"u8.ToArray());
        result.EventType.ShouldBe(typeof(ChainedEvent));
        result.PayloadVersion.ShouldBe(3);
    }

    [Fact]
    public async Task MissingChainFailsHostStartupBeforeAcceptingWork()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddKnownEventPayload<VersionedTestEvent>())
            .Build();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            () => host.StartAsync());

        failure.Message.ShouldContain(typeof(VersionedTestEvent).FullName!);
        failure.Message.ShouldContain("version 3");
    }
}
