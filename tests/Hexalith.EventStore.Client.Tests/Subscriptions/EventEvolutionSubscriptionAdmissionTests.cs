using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

/// <summary>Verifies versioned subscription refusal retains delivery without marker or handler effects.</summary>
public sealed class EventEvolutionSubscriptionAdmissionTests
{
    /// <summary>Checks complete and partial version tuples refuse before marker acquisition on every retry.</summary>
    [Theory]
    [InlineData(2, "evt", 1)]
    [InlineData(1, "evt", 1)]
    [InlineData(1, "evt", null)]
    [InlineData(1, null, 1)]
    [InlineData(0, null, null)]
    public async Task UnsupportedVersionedDeliveryRefusesBeforeAnyMarkerOrHandler(int metadataVersion, string? type, int? version)
    {
        IEventStoreDomainEventMarkerStore markers = Substitute.For<IEventStoreDomainEventMarkerStore>();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var processor = new EventStoreDomainEventProcessor(services.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type>(), markers, NullLogger<EventStoreDomainEventProcessor>.Instance);
        EventStoreDomainEventEnvelope envelope = Envelope() with
        {
            MetadataVersion = metadataVersion,
            EventContractType = type,
            PayloadVersion = version,
        };

        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);
        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);
        markers.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks cancellation at intake prevents all marker operations.</summary>
    [Fact]
    public async Task PreCancelledDeliveryCreatesNoMarker()
    {
        IEventStoreDomainEventMarkerStore markers = Substitute.For<IEventStoreDomainEventMarkerStore>();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var processor = new EventStoreDomainEventProcessor(services.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type>(), markers, NullLogger<EventStoreDomainEventProcessor>.Instance);
        await Should.ThrowAsync<OperationCanceledException>(() => processor.ProcessAsync(Envelope(), new CancellationToken(true)));
        markers.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks a caller-created effective carrier is held before markers and survives the exact dormant JSON name.</summary>
    [Fact]
    public async Task UnverifiedEffectiveEventHintCannotEnterLegacyMarkerOrHandlerFlow()
    {
        var view = new VerifiedEffectiveEventView(1, new byte[32], "evt", 1, "json", [123, 125], [], "key", new byte[64]);
        EventStoreDomainEventEnvelope envelope = Envelope() with { VerifiedEffectiveEvent = view };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(envelope, options);
        json.ShouldContain("\"verifiedEffectiveEvent\":");
        EventStoreDomainEventEnvelope incoming = JsonSerializer.Deserialize<EventStoreDomainEventEnvelope>(json, options)!;
        IEventStoreDomainEventMarkerStore markers = Substitute.For<IEventStoreDomainEventMarkerStore>();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var processor = new EventStoreDomainEventProcessor(services.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type>(), markers, NullLogger<EventStoreDomainEventProcessor>.Instance);
        (await processor.ProcessAsync(incoming)).ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);
        markers.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks legacy null omission and the additive published tuple's JSON round trip.</summary>
    [Fact]
    public void LegacyWireOmitsVersionMembersAndVersionedWireRetainsCompleteTuple()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string legacy = JsonSerializer.Serialize(Envelope(), options);
        legacy.ShouldNotContain("metadataVersion");
        legacy.ShouldNotContain("eventContractType");
        legacy.ShouldNotContain("payloadVersion");
        string wire = JsonSerializer.Serialize(Envelope() with { MetadataVersion = 2, EventContractType = "evt", PayloadVersion = 3 }, options);
        EventStoreDomainEventEnvelope result = JsonSerializer.Deserialize<EventStoreDomainEventEnvelope>(wire, options)!;
        result.MetadataVersion.ShouldBe(2);
        result.EventContractType.ShouldBe("evt");
        result.PayloadVersion.ShouldBe(3);
    }

    private static EventStoreDomainEventEnvelope Envelope()
        => new(UniqueIdHelper.GenerateSortableUniqueStringId(), "aggregate", "tenant", "legacy", 1,
            DateTimeOffset.UnixEpoch, "correlation", "json", [123, 125]);
}
