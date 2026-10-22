using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

internal sealed record ValidCounterEvent(string AggregateId) : IEventContract
{
    public static string EventType => "counter-created";

    public static string Domain => "counter";
}

internal sealed record InvalidKebabCaseEvent(string AggregateId) : IEventContract
{
    public static string EventType => "CounterCreated";

    public static string Domain => "counter";
}

internal sealed record EmptyDomainEvent(string AggregateId) : IEventContract
{
    public static string EventType => "counter-created";

    public static string Domain => "";
}

internal sealed record ColonEventTypeEvent(string AggregateId) : IEventContract
{
    public static string EventType => "counter:created";

    public static string Domain => "counter";
}

internal sealed record ColonDomainEvent(string AggregateId) : IEventContract
{
    public static string EventType => "counter-created";

    public static string Domain => "counter:admin";
}

internal sealed record NullEventTypeEvent(string AggregateId) : IEventContract
{
    public static string EventType => null!;

    public static string Domain => "counter";
}

internal sealed record NullDomainEvent(string AggregateId) : IEventContract
{
    public static string EventType => "counter-created";

    public static string Domain => null!;
}

public class EventContractResolverTests : IDisposable
{
    public EventContractResolverTests() => EventContractResolver.ClearCache();

    public void Dispose()
    {
        EventContractResolver.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_ValidContract_ReturnsCorrectMetadata()
    {
        EventContractMetadata metadata = EventContractResolver.Resolve<ValidCounterEvent>();

        metadata.EventType.ShouldBe("counter-created");
        metadata.Domain.ShouldBe("counter");
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsCachedInstance()
    {
        EventContractMetadata first = EventContractResolver.Resolve<ValidCounterEvent>();
        EventContractMetadata second = EventContractResolver.Resolve<ValidCounterEvent>();

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Resolve_InvalidKebabCase_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EventContractResolver.Resolve<InvalidKebabCaseEvent>());

    [Fact]
    public void Resolve_EmptyDomain_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EventContractResolver.Resolve<EmptyDomainEvent>());

    [Fact]
    public void Resolve_EventTypeWithColon_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EventContractResolver.Resolve<ColonEventTypeEvent>());

    [Fact]
    public void Resolve_DomainWithColon_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EventContractResolver.Resolve<ColonDomainEvent>());

    [Fact]
    public void Resolve_NullEventType_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => EventContractResolver.Resolve<NullEventTypeEvent>());

    [Fact]
    public void Resolve_NullDomain_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => EventContractResolver.Resolve<NullDomainEvent>());
}
