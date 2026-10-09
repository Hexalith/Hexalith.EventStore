using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventPayloadEvolutionRegistryTests
{
    private const string OldName = "Historical.LegacyTestEvent";

    [Fact]
    public void Read_RenamesShortAliasAndRunsOrderedStepsOnce()
    {
        int firstCalls = 0;
        int secondCalls = 0;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(LegacyTestEvent), typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(OldName, 1, typeof(VersionedTestEvent).FullName, payload =>
            {
                firstCalls++;
                payload["Value"] = payload["Amount"]!.GetValue<int>() + 1;
                payload.Remove("Amount");
                return payload;
            }),
            new TestPayloadUpcaster(typeof(VersionedTestEvent).Name, 2, null, payload =>
            {
                secondCalls++;
                payload["Value"] = payload["Value"]!.GetValue<int>() + 1;
                return payload;
            })]);
        byte[] stored = "{\"Amount\":1}"u8.ToArray();

        ResolvedEventPayload resolved = registry.Read(nameof(LegacyTestEvent), null, stored, 7);

        resolved.EventType.ShouldBe(typeof(VersionedTestEvent));
        resolved.PayloadVersion.ShouldBe(3);
        JsonNode.Parse(resolved.Payload)!["Value"]!.GetValue<int>().ShouldBe(3);
        stored.ShouldBe("{\"Amount\":1}"u8.ToArray());
        firstCalls.ShouldBe(1);
        secondCalls.ShouldBe(1);
    }

    [Fact]
    public void Read_KnownCurrentPayloadKeepsBytesAndRejectsMalformedJson()
    {
        var registry = new EventPayloadEvolutionRegistry([typeof(LegacyTestEvent)], []);
        byte[] stored = "{\"Amount\":5}"u8.ToArray();

        registry.Read(typeof(LegacyTestEvent).FullName!, null, stored, 1).Payload.ShouldBeSameAs(stored);
        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(typeof(LegacyTestEvent).FullName!, null, "not-json"u8.ToArray(), 2));
        failure.EventTypeName.ShouldBe(typeof(LegacyTestEvent).FullName);
        failure.SequenceNumber.ShouldBe(2);
        failure.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Read_HistoricalAliasWithMissingVersionFailsTyped()
    {
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(LegacyTestEvent), typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(OldName, 1, typeof(VersionedTestEvent).FullName, static payload => payload),
             new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(OldName, 2, "{}"u8.ToArray(), 9));

        failure.StoredVersion.ShouldBe(2);
        failure.SequenceNumber.ShouldBe(9);
        failure.Message.ShouldContain("missing step");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1025)]
    public void Read_KnownOutOfRangeOrFutureVersionFailsTyped(int version)
    {
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 1, null, static payload => payload),
             new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(typeof(VersionedTestEvent).FullName!, version, "{}"u8.ToArray(), 12));

        failure.StoredVersion.ShouldBe(version);
        failure.SequenceNumber.ShouldBe(12);
    }

    [Fact]
    public void Read_UnknownNameKeepsOriginalBytes()
    {
        var registry = new EventPayloadEvolutionRegistry([typeof(LegacyTestEvent)], []);
        byte[] bytes = [0xFF];

        registry.Read("Other.LegacyTestEventVariant", 2, bytes).Payload.ShouldBeSameAs(bytes);
    }

    [Fact]
    public void Read_ThrowingUpcasterExposesOnlyExceptionTypeAndDoesNotChainIt()
    {
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 1, null,
                static _ => throw new InvalidOperationException("secret payload")),
             new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(typeof(VersionedTestEvent).FullName!, 1, "{}"u8.ToArray(), 8));

        failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
        failure.InnerExceptionTypeName.ShouldBe(nameof(InvalidOperationException));
        failure.InnerException.ShouldBeNull();
        failure.Message.ShouldNotContain("secret payload");
    }

    [Fact]
    public void Registration_RejectsIncompleteChainWithEventVersion()
    {
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry(
                [typeof(VersionedTestEvent)],
                [new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 1, null, static payload => payload)]));

        failure.Message.ShouldContain(typeof(VersionedTestEvent).FullName!);
        failure.Message.ShouldContain("version 2");
    }
}
