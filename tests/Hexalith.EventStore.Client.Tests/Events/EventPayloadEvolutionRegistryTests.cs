using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventPayloadEvolutionRegistryTests
{
    private const string OldName = "Historical.LegacyTestEvent";

    [Fact]
    public void Registration_RejectsOverlappingFullAndShortStepNamesAtSameVersion()
    {
        string fullName = typeof(VersionedTestEvent).FullName!;
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(fullName, 1, null, static payload => payload),
             new TestPayloadUpcaster(nameof(VersionedTestEvent), 1, null, static payload => payload),
             new TestPayloadUpcaster(fullName, 2, null, static payload => payload)]));

        failure.Message.ShouldContain("version 1");
        failure.Message.ShouldContain(nameof(VersionedTestEvent));
    }

    [Fact]
    public void Read_UpcastPayloadSerializationFailureIsTypedAndSupportSafe()
    {
        string name = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, static payload =>
             {
                 payload["Value"] = double.NaN;
                 return payload;
             }),
             new TestPayloadUpcaster(name, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(name, 1, "{}"u8.ToArray(), 11));

        failure.SequenceNumber.ShouldBe(11);
        failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
        failure.InnerExceptionTypeName.ShouldNotBeNull();
        failure.InnerException.ShouldBeNull();
        failure.Message.ShouldContain("upcast payload serialization failed");
        failure.Message.ShouldNotContain("NaN");
    }

    [Fact]
    public void Read_RenamesExactHistoricalNameAndRunsOrderedStepsOnce()
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

        ResolvedEventPayload resolved = registry.Read(OldName, null, stored, 7);

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
    public void ReadForReplay_RejectsEffectivePayloadExpandedPastReadableLimit()
    {
        string name = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, static payload => payload),
             new TestPayloadUpcaster(name, 2, null, static payload =>
             {
                 payload["Value"] = new string('x', 64 * 1024 * 1024);
                 return payload;
             })]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.ReadForReplay(name, 1, "{}"u8.ToArray(), 4));

        failure.EventTypeName.ShouldBe(name);
        failure.SequenceNumber.ShouldBe(4);
        failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
        failure.Message.ShouldContain("payload exceeds the readable limit");
    }

    [Fact]
    public void Read_ImmediateSizeFailureReportsLastUpcaster()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, static payload =>
            {
                payload["Value"] = new string('x', 64 * 1024 * 1024);
                return payload;
            })]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(name, 1, "{}"u8.ToArray(), 5));

        failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
        failure.Message.ShouldContain("payload exceeds the readable limit");
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

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    public void Registration_RejectsInvalidStepBoundsWithEventName(int fromVersion)
    {
        string name = typeof(VersionedTestEvent).FullName!;
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
                [new TestPayloadUpcaster(name, fromVersion, null, static payload => payload)]));

        failure.Message.ShouldContain(name);
        failure.Message.ShouldContain(fromVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(typeof(InvalidZeroVersionTestEvent), 0)]
    [InlineData(typeof(InvalidHighVersionTestEvent), 1025)]
    public void Registration_RejectsInvalidDeclaredVersion(Type type, int version)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentOutOfRangeException failure = Should.Throw<ArgumentOutOfRangeException>(() =>
            new EventPayloadEvolutionRegistry([type], []));

        failure.Message.ShouldContain(type.FullName!);
        failure.Message.ShouldContain(version.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Registration_RejectsUnknownRenameTarget()
    {
        string name = typeof(VersionedTestEvent).FullName!;
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
                [new TestPayloadUpcaster(name, 1, "Missing.Target", static payload => payload),
                 new TestPayloadUpcaster(name, 2, null, static payload => payload)]));

        failure.Message.ShouldContain("Missing.Target");
        failure.Message.ShouldContain("version 1");
    }

    [Fact]
    public void Registration_RejectsDanglingOutputVersion()
    {
        string name = typeof(LegacyTestEvent).FullName!;
        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry([typeof(LegacyTestEvent)],
                [new TestPayloadUpcaster(name, 1, null, static payload => payload)]));

        failure.Message.ShouldContain(name);
        failure.Message.ShouldContain("version 2");
    }

    [Fact]
    public void Read_OneStepAndAssemblyQualifiedNameReachCurrentType()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, payload =>
            {
                payload["Value"] = payload["Amount"]!.GetValue<int>();
                payload.Remove("Amount");
                return payload;
            })]);

        ResolvedEventPayload result = registry.Read($"{name}, {typeof(VersionTwoTestEvent).Assembly.GetName().Name}",
            1, "{\"Amount\":4}"u8.ToArray());

        result.EventType.ShouldBe(typeof(VersionTwoTestEvent));
        result.PayloadVersion.ShouldBe(2);
        System.Text.Json.JsonSerializer.Deserialize<VersionTwoTestEvent>(result.Payload)!.Value.ShouldBe(4);
    }

    [Fact]
    public void Read_ShortStoredNameUsesFullNameStepOfUniqueKnownType()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, payload =>
            {
                payload["Value"] = payload["Amount"]!.GetValue<int>();
                payload.Remove("Amount");
                return payload;
            })]);

        ResolvedEventPayload result = registry.Read(nameof(VersionTwoTestEvent), 1,
            "{\"Amount\":4}"u8.ToArray());

        result.EventType.ShouldBe(typeof(VersionTwoTestEvent));
        System.Text.Json.JsonSerializer.Deserialize<VersionTwoTestEvent>(result.Payload)!.Value.ShouldBe(4);
    }

    [Fact]
    public void Registration_AcceptsRenameTargetingUniqueShortName()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster("Historical.ValueRaised", 1, nameof(VersionTwoTestEvent),
                payload => { payload["Value"] = 4; return payload; })]);

        registry.Read("Historical.ValueRaised", 1, "{}"u8.ToArray()).EventType
            .ShouldBe(typeof(VersionTwoTestEvent));
    }

    [Theory]
    [InlineData("VersionedTestEvent")]
    [InlineData("Contracts.VersionedTestEvent")]
    public void Read_HistoricalAliasMatchesLongerStepNameAtEveryVersion(string storedName)
    {
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster("Old.Contracts.VersionedTestEvent", 1, null,
                payload => { payload["Value"] = 3; return payload; }),
             new TestPayloadUpcaster("Old.Contracts.VersionedTestEvent", 2, typeof(VersionedTestEvent).FullName,
                static payload => payload)]);

        ResolvedEventPayload result = registry.Read(storedName, 1, "{}"u8.ToArray());

        result.EventType.ShouldBe(typeof(VersionedTestEvent));
        result.PayloadVersion.ShouldBe(3);
    }

    [Fact]
    public void Read_PostUpcastDeserializationFailureKeepsUpcasterType()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null,
                payload => { payload["Value"] = "not a number"; return payload; })]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(name, 1, "{}"u8.ToArray(), 6));

        failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
        failure.SequenceNumber.ShouldBe(6);
        failure.InnerExceptionTypeName.ShouldBe(nameof(System.Text.Json.JsonException));
    }

    [Fact]
    public void Read_ExactFullNameWinsOverAnotherTypesShortName()
    {
        string name = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent), typeof(Other.VersionedTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, static payload => payload),
             new TestPayloadUpcaster(name, 2, null, static payload => payload)]);

        registry.Read(name, 3, "{\"Value\":2}"u8.ToArray()).EventType.ShouldBe(typeof(VersionedTestEvent));
    }

    [Fact]
    public void Read_ExactKnownTypeIgnoresUnrelatedLongerUpcasterName()
    {
        string legacyName = typeof(LegacyTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(LegacyTestEvent), typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster("Unrelated." + legacyName, 1,
                typeof(VersionTwoTestEvent).FullName, static payload => payload)]);

        ResolvedEventPayload result = registry.Read(legacyName, 1, "{\"Amount\":4}"u8.ToArray());

        result.EventType.ShouldBe(typeof(LegacyTestEvent));
        result.PayloadVersion.ShouldBe(1);
    }

    [Fact]
    public void Read_UniqueKnownShortNameIgnoresUnrelatedLongerUpcaster()
    {
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(LegacyTestEvent), typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster("Unrelated.LegacyTestEvent", 1,
                typeof(VersionTwoTestEvent).FullName, static payload => payload)]);

        ResolvedEventPayload result = registry.Read(nameof(LegacyTestEvent), 1, "{\"Amount\":4}"u8.ToArray());

        result.EventType.ShouldBe(typeof(LegacyTestEvent));
        result.PayloadVersion.ShouldBe(1);
    }

    [Fact]
    public void Read_KnownCurrentShortNameWinsOverLongerHistoricalRename()
    {
        int renameCalls = 0;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(LegacyTestEvent), typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(OldName, 1, typeof(VersionTwoTestEvent).FullName, payload =>
            {
                renameCalls++;
                payload["Value"] = payload["Amount"]!.GetValue<int>();
                payload.Remove("Amount");
                return payload;
            })]);
        byte[] stored = "{\"Amount\":4}"u8.ToArray();

        ResolvedEventPayload known = registry.Read(nameof(LegacyTestEvent), 1, stored);
        ResolvedEventPayload historical = registry.Read(OldName, 1, stored);

        known.EventType.ShouldBe(typeof(LegacyTestEvent));
        known.PayloadVersion.ShouldBe(1);
        known.Payload.ShouldBeSameAs(stored);
        historical.EventType.ShouldBe(typeof(VersionTwoTestEvent));
        historical.PayloadVersion.ShouldBe(2);
        JsonNode.Parse(historical.Payload)!["Value"]!.GetValue<int>().ShouldBe(4);
        renameCalls.ShouldBe(1);
    }

    [Fact]
    public void Read_KnownShortNameCollisionCannotChooseOneOfTwoLongerRenames()
    {
        string currentName = typeof(VersionedTestEvent).FullName!;
        string otherName = typeof(Other.VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent), typeof(Other.VersionedTestEvent)],
            [new TestPayloadUpcaster(currentName, 1, currentName, static payload => payload),
             new TestPayloadUpcaster(otherName, 1, currentName, static payload => payload),
             new TestPayloadUpcaster(currentName, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(nameof(VersionedTestEvent), 1, "{}"u8.ToArray(), 13));

        failure.SequenceNumber.ShouldBe(13);
        failure.Message.ShouldContain("ambiguous event type");
    }

    [Fact]
    public void Registration_UnrelatedLongerNameCannotCompleteKnownTypeChain()
    {
        string currentName = typeof(VersionTwoTestEvent).FullName!;

        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
                [new TestPayloadUpcaster("Unrelated." + currentName, 1, null, static payload => payload)]));

        failure.Message.ShouldContain(currentName);
        failure.Message.ShouldContain("continuous chain");
    }

    [Fact]
    public void Read_ExplicitRenameDisambiguatesHistoricalShortAlias()
    {
        string currentName = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent), typeof(Other.VersionedTestEvent)],
            [new TestPayloadUpcaster("Historical.VersionedTestEvent", 1, currentName,
                static payload => { payload["Value"] = 4; return payload; }),
             new TestPayloadUpcaster(currentName, 2, null, static payload => payload)]);

        ResolvedEventPayload result = registry.Read(nameof(VersionedTestEvent), 1, "{}"u8.ToArray());

        result.EventType.ShouldBe(typeof(VersionedTestEvent));
        result.PayloadVersion.ShouldBe(3);
    }

    [Fact]
    public void Read_ShortNameCollisionWithoutRenameFailsTyped()
    {
        string currentName = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent), typeof(Other.VersionedTestEvent)],
            [new TestPayloadUpcaster(currentName, 1, null, static payload => payload),
             new TestPayloadUpcaster(currentName, 2, null, static payload => payload)]);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            registry.Read(nameof(VersionedTestEvent), 1, "{}"u8.ToArray(), 12));

        failure.SequenceNumber.ShouldBe(12);
        failure.Message.ShouldContain("ambiguous event type");
    }

    [Fact]
    public void Read_NullOrCancellationFromUpcasterFailsTyped()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        foreach (Func<System.Text.Json.Nodes.JsonObject, System.Text.Json.Nodes.JsonObject> transform in new Func<System.Text.Json.Nodes.JsonObject, System.Text.Json.Nodes.JsonObject>[]
        {
            static _ => null!,
            static _ => throw new OperationCanceledException("secret"),
        })
        {
            var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
                [new TestPayloadUpcaster(name, 1, null, transform)]);
            EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
                registry.Read(name, 1, "{}"u8.ToArray(), 8));
            failure.SequenceNumber.ShouldBe(8);
            failure.UpcasterTypeName.ShouldBe(typeof(TestPayloadUpcaster).FullName);
            failure.InnerException.ShouldBeNull();
            failure.Message.ShouldNotContain("secret");
        }
    }
}
