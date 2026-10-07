using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Counter;

/// <summary>Verifies the sample's explicitly declared V1 writer against its existing command and wire behavior.</summary>
public sealed class CounterEventSerializationTests
{
    /// <summary>Compares real aggregate success, rejection and no-op responses with the legacy serializer.</summary>
    /// <param name="operation">The command/state combination selecting the emitted-event path.</param>
    [Theory]
    [InlineData("increment")]
    [InlineData("decrement")]
    [InlineData("reset")]
    [InlineData("close")]
    [InlineData("negative")]
    [InlineData("terminated")]
    [InlineData("noop")]
    public async Task RealCounterCommandRoutesPreserveExactLegacyPayloadsAndAliases(string operation)
    {
        var state = new CounterState();
        object command;
        switch (operation)
        {
            case "increment": command = new IncrementCounter(); break;
            case "decrement": state.Apply(new CounterIncremented()); command = new DecrementCounter(); break;
            case "reset": state.Apply(new CounterIncremented()); command = new ResetCounter(); break;
            case "close": command = new CloseCounter(); break;
            case "negative": command = new DecrementCounter(); break;
            case "terminated": state.Apply(new CounterClosed()); command = new IncrementCounter(); break;
            case "noop": command = new ResetCounter(); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }

        DomainServiceRequest request = Request(command, state);
        DomainServiceWireResult legacy = DomainServiceWireResult.FromDomainResult(
            await new CounterAggregate().ProcessAsync(request.Command, request.CurrentState));
        using ServiceProvider provider = RegisteredCounter();
        DomainServiceWireResult bounded = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        RequireIdenticalWireResult(legacy, bounded);
        if (operation is not ("terminated" or "noop"))
        {
            bounded.Events.ShouldHaveSingleItem().Payload.ShouldBe("{}"u8.ToArray());
        }
    }

    /// <summary>Checks the closed source inventory cannot silently acquire data members or undeclared events.</summary>
    [Fact]
    public void CounterApplicationInventoryContainsExactlyTheFiveEmptyMarkerTypes()
    {
        Type[] payloadTypes = typeof(CounterAggregate).Assembly.GetTypes()
            .Where(type => type.Namespace == "Hexalith.EventStore.Sample.Counter.Events"
                && typeof(IEventPayload).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        payloadTypes.Select(type => type.Name).ShouldBe(new[] {
            "CounterCannotGoNegative", "CounterClosed", "CounterDecremented", "CounterIncremented", "CounterReset",
        });
        foreach (Type type in payloadTypes)
        {
            type.IsSealed.ShouldBeTrue();
            type.GetProperties().ShouldBeEmpty();
        }
    }

    /// <summary>Checks the identity grammar's complete maximum token fits the exact declared JSON bound.</summary>
    [Fact]
    public async Task MaximumLengthTerminationRejectionPreservesThe309ByteLegacyImage()
    {
        string identifier = "A" + string.Concat(Enumerable.Repeat("._-a", 63)) + "xyz";
        identifier.Length.ShouldBe(256);
        var state = new CounterState();
        state.Apply(new CounterClosed());
        DomainServiceRequest request = Request(new IncrementCounter(), state, identifier);
        using ServiceProvider provider = RegisteredCounter();

        DomainServiceWireResult bounded = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        DomainServiceWireEvent emitted = bounded.Events.ShouldHaveSingleItem();
        bounded.IsRejection.ShouldBeTrue();
        emitted.EventTypeName.ShouldBe("Hexalith.EventStore.Contracts.Events.AggregateTerminated");
        emitted.Payload.Length.ShouldBe(309);
        emitted.Payload.ShouldBe(Encoding.UTF8.GetBytes(
            "{\"AggregateType\":\"CounterAggregate\",\"AggregateId\":\"" + identifier + "\"}"));
        emitted.Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new AggregateTerminated("CounterAggregate", identifier)));
    }

    /// <summary>Checks an undeclared payload cannot enter Counter through a result from another domain.</summary>
    [Fact]
    public async Task UndeclaredPayloadRefusesTheCounterResultBeforeItCanBecomeWireOutput()
    {
        using ServiceProvider provider = SuppliedResult(DomainResult.Success([new GreetingSent()]));

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            DomainServiceRequestRouter.ProcessAsync(provider, Request(new IncrementCounter())));

        failure.Message.ShouldStartWith("CapabilityMismatch:");
    }

    /// <summary>Checks Counter's explicit framework adapter refuses a foreign route or invalid identifier.</summary>
    /// <param name="aggregateType">The emitted rejection's route.</param>
    /// <param name="aggregateId">The emitted rejection's aggregate identifier.</param>
    /// <param name="foreignRoute">Whether a capability refusal rather than identity validation is expected.</param>
    [Theory]
    [InlineData("GreetingAggregate", "counter-1", true)]
    [InlineData("CounterAggregate", "invalid\"identifier", false)]
    [InlineData("CounterAggregate", "é", false)]
    public async Task FrameworkRejectionAdapterRefusesForeignRouteOrInvalidIdentity(string aggregateType, string aggregateId, bool foreignRoute)
    {
        using ServiceProvider provider = SuppliedResult(DomainResult.Rejection([new AggregateTerminated(aggregateType, aggregateId)]));

        if (foreignRoute)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(
                provider, Request(new IncrementCounter())))).Message.ShouldStartWith("CapabilityMismatch:");
        }
        else
        {
            await Should.ThrowAsync<ArgumentException>(() => DomainServiceRequestRouter.ProcessAsync(provider, Request(new IncrementCounter())));
        }
    }

    /// <summary>Checks the small real bounds admit the maximum count while retaining the global count fence.</summary>
    /// <param name="count">The complete emitted marker count.</param>
    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task ActualMarkerBoundsSupportTheApprovedResultCountLimit(int count)
    {
        IEventPayload[] markers = Enumerable.Range(0, count).Select(_ => (IEventPayload)new CounterIncremented()).ToArray();
        using ServiceProvider provider = SuppliedResult(DomainResult.Success(markers));

        if (count == 1001)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(
                provider, Request(new IncrementCounter())))).Message.ShouldStartWith("ResultLimit:");
            return;
        }

        DomainServiceWireResult produced = await DomainServiceRequestRouter.ProcessAsync(provider, Request(new IncrementCounter()));
        produced.Events.Count.ShouldBe(1000);
        foreach (DomainServiceWireEvent item in produced.Events)
        {
            item.Payload.ShouldBe("{}"u8.ToArray());
            item.EventTypeName.ShouldBe("Hexalith.EventStore.Sample.Counter.Events.CounterIncremented");
        }
    }

    /// <summary>Checks cancellation after processing keeps the originating token and returns no wire result.</summary>
    [Fact]
    public async Task CancellationAtTheDomainBoundaryPreservesTheOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new BoundedCounterResultProcessor(DomainResult.Success([new CounterIncremented()]), cancellation.Cancel);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("counter", processor);
        services.AddCounterEventSerialization();
        using ServiceProvider provider = services.BuildServiceProvider();

        await Should.ThrowAsync<OperationCanceledException>(() => DomainServiceRequestRouter.ProcessAsync(
            provider, Request(new IncrementCounter()), cancellation.Token));
        processor.ObservedToken.ShouldBe(cancellation.Token);
    }

    /// <summary>Checks Counter's registration preserves Greeting's existing independent wire route.</summary>
    [Fact]
    public async Task CounterProfileKeepsGreetingOnItsCompatibleSerializer()
    {
        var services = new ServiceCollection();
        services.AddEventStore(typeof(CounterAggregate).Assembly);
        services.AddCounterEventSerialization();
        using ServiceProvider provider = services.BuildServiceProvider();
        DomainServiceRequest request = Request(new SendGreeting());
        request = request with { Command = request.Command with { Domain = "greeting" } };
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(GreetingSent).FullName);
        result.Events[0].Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new GreetingSent()));
        result.WriterMode.ShouldBeNull();
        result.RegistryFingerprint.ShouldBeNull();
    }

    private static ServiceProvider RegisteredCounter()
    {
        var services = new ServiceCollection();
        services.AddEventStore(typeof(CounterAggregate).Assembly);
        services.AddCounterEventSerialization();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider SuppliedResult(DomainResult result)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("counter", new BoundedCounterResultProcessor(result));
        services.AddCounterEventSerialization();
        return services.BuildServiceProvider();
    }

    private static DomainServiceRequest Request(object command, object? state = null, string aggregateId = "counter-1")
        => new(new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "sample-tenant", "counter", aggregateId,
            command.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(command, command.GetType()),
            "01ARZ3NDEKTSV4RRFFQ69G5FAW", null, "test-user", null), state);

    private static void RequireIdenticalWireResult(DomainServiceWireResult expected, DomainServiceWireResult actual)
    {
        actual.IsRejection.ShouldBe(expected.IsRejection);
        actual.ResultPayload.ShouldBe(expected.ResultPayload);
        actual.Events.Count.ShouldBe(expected.Events.Count);
        actual.WriterMode.ShouldBeNull();
        actual.RegistryFingerprint.ShouldBeNull();
        for (int index = 0; index < expected.Events.Count; index++)
        {
            actual.Events[index].EventTypeName.ShouldBe(expected.Events[index].EventTypeName);
            actual.Events[index].SerializationFormat.ShouldBe(expected.Events[index].SerializationFormat);
            actual.Events[index].Payload.ShouldBe(expected.Events[index].Payload);
            actual.Events[index].MetadataVersion.ShouldBeNull();
            actual.Events[index].EventContractType.ShouldBeNull();
            actual.Events[index].PayloadVersion.ShouldBeNull();
        }
    }
}
