using System.Buffers.Binary;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Checks composed allow-listed validation and deserialization without granting source or production authority.</summary>
public sealed class EventEvolutionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolvesCurrentObjectThroughEveryBoundVersionAndClearsPrivateOwner(bool upcasting)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting);
        using var cancellation = new CancellationTokenSource();
        var calls = new List<string>();
        var retained = new List<IReadOnlyPayload>();
        var budget = new EventBufferBudget();
        EventVersionValidator schema = (_, _, version, _, payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            retained.Add(payload);
            calls.Add($"schema-{version}");
        };
        EventVersionValidator identity = (_, _, version, _, payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            retained.Add(payload);
            calls.Add($"identity-{version}");
        };
        var service = CreateService(registry, schema, identity, (payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            retained.Add(payload);
            byte[] bytes = new byte[payload.Length]; payload.CopyTo(0, bytes); bytes.ShouldBe([1, 2]);
            calls.Add("deserialize");
            return new AllowlistedCurrentEventTestValue();
        });
        byte[] original = [1, 2];

        object value = await service.ResolveAndDeserializeAsync("d", "Legacy.Event", 1, null, null,
            "json", original, "r", cancellation.Token, budget);

        value.ShouldBeOfType<AllowlistedCurrentEventTestValue>();
        calls.ShouldBe(upcasting ? ["schema-1", "identity-1", "schema-2", "identity-2", "deserialize"]
            : ["schema-1", "identity-1", "deserialize"]);
        original.ShouldBe([1, 2]);
        budget.LiveBytes.ShouldBe(0);
        foreach (IReadOnlyPayload payload in retained)
        {
            Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[2]));
        }
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("identity")]
    [InlineData("deserialize")]
    public async Task FailurePreservesOriginalBytesAndReleasesAllPrivateCapacity(string stage)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        var calls = new List<string>();
        var retained = new List<IReadOnlyPayload>();
        void Observe(string name, IReadOnlyPayload payload)
        {
            calls.Add(name); retained.Add(payload);
            if (name == stage) { throw new InvalidOperationException("refusal-sentinel"); }
        }
        var service = CreateService(registry,
            (_, _, _, _, payload, _) => Observe("schema", payload),
            (_, _, _, _, payload, _) => Observe("identity", payload),
            (payload, _) => { Observe("deserialize", payload); return new AllowlistedCurrentEventTestValue(); });
        var budget = new EventBufferBudget();
        byte[] original = [1, 2];

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ResolveAndDeserializeAsync("d", "Legacy.Event", 1, null, null,
                "json", original, "r", CancellationToken.None, budget));

        failure.Message.ShouldBe("refusal-sentinel");
        calls[^1].ShouldBe(stage);
        original.ShouldBe([1, 2]);
        budget.LiveBytes.ShouldBe(0);
        foreach (IReadOnlyPayload payload in retained)
        {
            Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[2]));
        }
    }

    [Theory]
    [InlineData("schema", false)]
    [InlineData("identity", false)]
    [InlineData("deserialize", false)]
    [InlineData("schema", true)]
    [InlineData("identity", true)]
    [InlineData("deserialize", true)]
    public async Task CancellationOrObservedLossRefusesSuccessAtEachCallback(string stage, bool loss)
    {
        using EventDomainRegistry registry = CreateRegistry();
        using var cancellation = new CancellationTokenSource();
        var calls = new List<string>();
        void Observe(string name, CancellationToken token)
        {
            token.ShouldBe(cancellation.Token); calls.Add(name);
            if (name == stage)
            {
                if (loss) { registry.CapabilityLoss.ObserveViolation(); }
                else { cancellation.Cancel(); }
            }
        }
        var service = CreateService(registry,
            (_, _, _, _, _, token) => Observe("schema", token),
            (_, _, _, _, _, token) => Observe("identity", token),
            (_, token) => { Observe("deserialize", token); return new AllowlistedCurrentEventTestValue(); });
        var budget = new EventBufferBudget();
        byte[] original = [1, 2];
        Task<object> pending = service.ResolveAndDeserializeAsync("d", "Legacy.Event", 1, null, null,
            "json", original, "r", cancellation.Token, budget).AsTask();

        if (loss)
        {
            (await Should.ThrowAsync<InvalidOperationException>(async () => await pending)).Message.ShouldContain("CapabilityMismatch");
        }
        else
        {
            (await Should.ThrowAsync<OperationCanceledException>(async () => await pending)).CancellationToken.ShouldBe(cancellation.Token);
        }
        calls.ShouldBe(new[] { "schema", "identity", "deserialize" }.Take(Array.IndexOf(new[] { "schema", "identity", "deserialize" }, stage) + 1));
        budget.LiveBytes.ShouldBe(0);
        original.ShouldBe([1, 2]);
    }

    [Theory]
    [InlineData("missing-validation")]
    [InlineData("extra-validation")]
    [InlineData("missing-deserializer")]
    [InlineData("extra-deserializer")]
    [InlineData("missing-transform")]
    [InlineData("extra-transform")]
    [InlineData("foreign-registry")]
    [InlineData("wrong-schema")]
    [InlineData("wrong-identity")]
    [InlineData("wrong-deserializer")]
    public void IncompleteOrContradictoryCatalogRefusesBeforeAnyApplicationCallback(string mutation)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        using EventDomainRegistry foreign = CreateRegistry(upcasting: true);
        var calls = new List<string>();
        RegisteredEventVersionValidation validation = CreateValidation(mutation == "foreign-registry" ? foreign : registry,
            (_, _, _, _, _, _) => calls.Add("schema"), (_, _, _, _, _, _) => calls.Add("identity"),
            mutation == "wrong-schema" ? "wrong" : "test-schema", mutation == "wrong-identity" ? "wrong" : "test-identity",
            () => { calls.Add("options"); return "{}\n"u8.ToArray(); });
        var validations = new Dictionary<(string, int), RegisteredEventVersionValidation> { [("evt", 1)] = validation, [("evt", 2)] = validation };
        var deserializers = new Dictionary<string, RegisteredCurrentEventDeserializer>
        {
            ["evt"] = new(typeof(AllowlistedCurrentEventTestValue), mutation == "wrong-deserializer" ? "wrong" : "test-serializer",
                (_, _) => { calls.Add("deserialize"); return new AllowlistedCurrentEventTestValue(); }, "{}\n"u8.ToArray(), [],
                () => { calls.Add("options"); return "{}\n"u8.ToArray(); }),
        };
        var transforms = new Dictionary<(string, int), RegisteredEventUpcaster>
        {
            [("evt", 1)] = new("test-upcaster", new LeaseRecordingEventUpcaster(), new byte[32]),
        };
        if (mutation == "missing-validation") { validations.Remove(("evt", 2)); }
        if (mutation == "extra-validation") { validations[("other", 1)] = validation; }
        if (mutation == "missing-deserializer") { deserializers.Clear(); }
        if (mutation == "extra-deserializer") { deserializers["other"] = deserializers["evt"]; }
        if (mutation == "missing-transform") { transforms.Clear(); }
        if (mutation == "extra-transform") { transforms[("evt", 2)] = transforms[("evt", 1)]; }

        Should.Throw<InvalidOperationException>(() => new EventEvolutionService(registry, validations, transforms, deserializers))
            .Message.ShouldContain("CapabilityMismatch");

        calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeserializerMutationOfCallerSourceRefusesAllowedObjectAndClearsPrivateOwner()
    {
        using EventDomainRegistry registry = CreateRegistry();
        byte[] original = [1, 2];
        IReadOnlyPayload? retained = null;
        var service = CreateService(registry, static (_, _, _, _, _, _) => { }, static (_, _, _, _, _, _) => { },
            (payload, _) => { retained = payload; original[0] = 9; return new AllowlistedCurrentEventTestValue(); });
        var budget = new EventBufferBudget();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ResolveAndDeserializeAsync("d", "Legacy.Event", 1, null, null,
                "json", original, "r", CancellationToken.None, budget));

        failure.Message.ShouldContain("original application payload changed during deserialization");
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => retained!.CopyTo(0, new byte[2]));
        // Trusted callbacks can close over the caller's array; detection refuses success
        // but cannot undo that callback's external mutation.
        original.ShouldBe([9, 2]);
    }

    [Fact]
    public async Task SnapshotRegistrationsIgnoreCallerDictionaryChangesAndSourceAdmissionStillRefuses()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var calls = new List<string>();
        RegisteredEventVersionValidation validation = CreateValidation(registry,
            (_, _, _, _, _, _) => calls.Add("schema"), (_, _, _, _, _, _) => calls.Add("identity"));
        var validations = new Dictionary<(string, int), RegisteredEventVersionValidation> { [("evt", 1)] = validation };
        var deserializers = new Dictionary<string, RegisteredCurrentEventDeserializer>
        {
            ["evt"] = new(typeof(AllowlistedCurrentEventTestValue), "test-serializer",
                (_, _) => { calls.Add("deserialize"); return new AllowlistedCurrentEventTestValue(); }, "{}\n"u8.ToArray(), []),
        };
        var service = new EventEvolutionService(registry, validations, new Dictionary<(string, int), RegisteredEventUpcaster>(), deserializers);
        validations.Clear(); deserializers.Clear();
        var budget = new EventBufferBudget();
        foreach ((string domain, string alias, string route) in new[] { ("foreign", "Legacy.Event", "r"), ("d", "unknown", "r"), ("d", "Legacy.Event", "foreign") })
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => await service.ResolveAndDeserializeAsync(
                domain, alias, 1, null, null, "json", new byte[] { 1, 2 }, route, CancellationToken.None, budget));
        }
        calls.ShouldBeEmpty(); budget.LiveBytes.ShouldBe(0);

        (await service.ResolveAndDeserializeAsync("d", "Legacy.Event", 1, null, null, "json",
            new byte[] { 1, 2 }, "r", CancellationToken.None, budget)).ShouldBeOfType<AllowlistedCurrentEventTestValue>();

        calls.ShouldBe(["schema", "identity", "deserialize"]); budget.LiveBytes.ShouldBe(0);
    }

    private static EventEvolutionService CreateService(EventDomainRegistry registry, EventVersionValidator schema,
        EventVersionValidator identity, Func<IReadOnlyPayload, CancellationToken, object> deserialize)
    {
        RegisteredEventVersionValidation validation = CreateValidation(registry, schema, identity);
        var validations = registry.Rows.Where(static row => row.Tag == 0x56).ToDictionary(
            static row => (row.GetTextKey(1), row.GetVersionKey(2)), _ => validation);
        var transforms = registry.Rows.Where(static row => row.Tag == 0x45).ToDictionary(
            static row => (row.GetTextKey(1), row.GetVersionKey(2)),
            _ => new RegisteredEventUpcaster("test-upcaster", new LeaseRecordingEventUpcaster(), new byte[32]));
        return new EventEvolutionService(registry, validations, transforms,
            new Dictionary<string, RegisteredCurrentEventDeserializer>
            {
                ["evt"] = new(typeof(AllowlistedCurrentEventTestValue), "test-serializer", deserialize, "{}\n"u8.ToArray(), []),
            });
    }

    private static RegisteredEventVersionValidation CreateValidation(EventDomainRegistry registry, EventVersionValidator schema,
        EventVersionValidator identity, string schemaId = "test-schema", string identityId = "test-identity",
        Func<ReadOnlyMemory<byte>>? runtimeOptions = null)
        => new(registry, schemaId, schema, "{}\n"u8.ToArray(), [], identityId, identity, "{}\n"u8.ToArray(), [], runtimeOptions, runtimeOptions);

    private static EventDomainRegistry CreateRegistry(bool upcasting = false)
    {
        using EventDomainRegistry current = EventLocalImplementationBindingTests.CreateRegistry();
        if (!upcasting)
        {
            return new EventDomainRegistry("d", current.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()).ToArray(),
                capabilityLoss: new EventEvolutionCapabilityLoss());
        }

        using EventDomainRegistry chain = EventUpcastChainExecutorTests.CreateRegistry();
        var rows = current.Rows.Select(static row => (ReadOnlyMemory<byte>)row.Encoded.ToArray()).ToList();
        byte[] descriptor = rows.Single(bytes => bytes.Span[0] == 0x44).ToArray();
        BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2);
        rows.RemoveAll(static bytes => bytes.Span[0] == 0x44); rows.Add(descriptor);
        byte[] version = rows.Single(bytes => bytes.Span[0] == 0x56).ToArray();
        BinaryPrimitives.WriteInt32BigEndian(version.AsSpan(13, 4), 2); rows.Add(version);
        rows.Add(chain.Rows.Single(static row => row.Tag == 0x45).Encoded.ToArray());
        return new EventDomainRegistry("d", rows, capabilityLoss: new EventEvolutionCapabilityLoss());
    }
}
