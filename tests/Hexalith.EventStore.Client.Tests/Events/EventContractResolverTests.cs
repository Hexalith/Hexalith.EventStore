using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

internal sealed record ValidCounterEvent(string AggregateId) : IEventContract {
    public static string EventType => "counter-created";
    public static string Domain => "counter";
}

internal sealed record InvalidKebabCaseEvent(string AggregateId) : IEventContract {
    public static string EventType => "CounterCreated";
    public static string Domain => "counter";
}

internal sealed record EmptyDomainEvent(string AggregateId) : IEventContract {
    public static string EventType => "counter-created";
    public static string Domain => "";
}

internal sealed record ColonEventTypeEvent(string AggregateId) : IEventContract {
    public static string EventType => "counter:created";
    public static string Domain => "counter";
}

internal sealed record NullEventTypeEvent(string AggregateId) : IEventContract {
    public static string EventType => null!;
    public static string Domain => "counter";
}

public class EventContractResolverTests : IDisposable {
    public EventContractResolverTests() => EventContractResolver.ClearCache();

    public void Dispose() {
        EventContractResolver.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_ValidContract_ReturnsCorrectMetadata() {
        EventContractMetadata metadata = EventContractResolver.Resolve<ValidCounterEvent>();

        Assert.Equal("counter-created", metadata.EventType);
        Assert.Equal("counter", metadata.Domain);
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsCachedInstance() {
        EventContractMetadata first = EventContractResolver.Resolve<ValidCounterEvent>();
        EventContractMetadata second = EventContractResolver.Resolve<ValidCounterEvent>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_InvalidKebabCase_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        EventContractResolver.Resolve<InvalidKebabCaseEvent>);

    [Fact]
    public void Resolve_EmptyDomain_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        EventContractResolver.Resolve<EmptyDomainEvent>);

    [Fact]
    public void Resolve_EventTypeWithColon_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        EventContractResolver.Resolve<ColonEventTypeEvent>);

    [Fact]
    public void Resolve_NullEventType_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(
        EventContractResolver.Resolve<NullEventTypeEvent>);
}
