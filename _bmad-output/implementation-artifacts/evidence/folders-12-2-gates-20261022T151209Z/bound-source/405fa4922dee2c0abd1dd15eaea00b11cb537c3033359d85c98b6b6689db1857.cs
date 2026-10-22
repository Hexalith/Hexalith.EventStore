using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;
using Hexalith.EventStore.Sample.Tests.Counter;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Greeting;

/// <summary>Checks the source-derived Greeting declaration through the production router.</summary>
public sealed class GreetingEventSerializationTests
{
    /// <summary>Compares the actual command route's bounded image with the independent legacy serializer.</summary>
    [Fact]
    public async Task GreetingCommandPreservesLegacyWireBytesAndAlias()
    {
        var services = new ServiceCollection();
        services.AddEventStore(typeof(GreetingAggregate).Assembly);
        services.AddCounterEventSerialization();
        services.AddGreetingEventSerialization();
        using ServiceProvider provider = services.BuildServiceProvider();
        DomainServiceRequest request = Request();
        DomainServiceWireResult expected = DomainServiceWireResult.FromDomainResult(
            await new GreetingAggregate().ProcessAsync(request.Command, request.CurrentState));

        DomainServiceWireResult actual = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        DomainServiceWireEvent item = actual.Events.ShouldHaveSingleItem();
        item.EventTypeName.ShouldBe(expected.Events[0].EventTypeName);
        item.Payload.ShouldBe(expected.Events[0].Payload);
        item.Payload.ShouldBe("{}"u8.ToArray());
        item.SerializationFormat.ShouldBe(expected.Events[0].SerializationFormat);
        actual.IsRejection.ShouldBeFalse();
        actual.WriterMode.ShouldBeNull();
        actual.RegistryFingerprint.ShouldBeNull();
        item.MetadataVersion.ShouldBeNull();
        item.EventContractType.ShouldBeNull();
        item.PayloadVersion.ShouldBeNull();
    }

    /// <summary>Checks the exact source inventory has no data members or additional emitted types.</summary>
    [Fact]
    public void GreetingInventoryIsOneEmptySealedMarker()
    {
        Type marker = typeof(GreetingSent);
        marker.Assembly.GetTypes().Where(type => type.Namespace == marker.Namespace
            && typeof(IEventPayload).IsAssignableFrom(type)).ShouldBe([marker]);
        marker.IsSealed.ShouldBeTrue();
        marker.GetProperties().ShouldBeEmpty();
        JsonSerializer.SerializeToUtf8Bytes(new GreetingSent()).ShouldBe("{}"u8.ToArray());
    }

    /// <summary>Checks the bounded route refuses a result carrying an event from Counter.</summary>
    [Fact]
    public async Task ForeignPayloadRefusesBeforeWireOutput()
    {
        using ServiceProvider provider = SuppliedResult(DomainResult.Success([new CounterIncremented()]));
        (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(provider, Request())))
            .Message.ShouldStartWith("CapabilityMismatch:");
    }

    /// <summary>Checks the complete marker result retains its count bound.</summary>
    /// <param name="count">The emitted-event count.</param>
    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task WholeResultCountBoundaryUsesExactTwoByteDeclaration(int count)
    {
        using ServiceProvider provider = SuppliedResult(DomainResult.Success(
            Enumerable.Range(0, count).Select(_ => (IEventPayload)new GreetingSent()).ToArray()));
        if (count == 1001)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(provider, Request())))
                .Message.ShouldStartWith("ResultLimit:");
            return;
        }
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, Request());
        result.Events.Count.ShouldBe(1000);
        foreach (DomainServiceWireEvent item in result.Events)
        {
            item.EventTypeName.ShouldBe(typeof(GreetingSent).FullName);
            item.Payload.ShouldBe("{}"u8.ToArray());
        }
    }

    private static ServiceProvider SuppliedResult(DomainResult result)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("greeting", new BoundedCounterResultProcessor(result));
        services.AddGreetingEventSerialization();
        return services.BuildServiceProvider();
    }

    private static DomainServiceRequest Request() => new(new CommandEnvelope(
        "01ARZ3NDEKTSV4RRFFQ69G5FAV", "sample-tenant", "greeting", "greeting-1", nameof(SendGreeting),
        JsonSerializer.SerializeToUtf8Bytes(new SendGreeting()), "01ARZ3NDEKTSV4RRFFQ69G5FAW",
        null, "test-user", null), null);
}
