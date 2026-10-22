using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventUpcastChainExecutorTests
{
    [Fact]
    public async Task ExecutesAdjacentRegisteredHopWithoutChangingSourceAndInvalidatesRetainedHandles()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var budget = new EventBufferBudget();
        using var cancellation = new CancellationTokenSource();
        var validations = new List<int>();
        var executor = CreateExecutor(registry, upcaster, (_, _, version, _, payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            payload.Length.ShouldBe(2);
            validations.Add(version);
        });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]);
        sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        using ImmutablePayload effective = await executor.UpcastAsync("evt", 1, source, budget, cancellation.Token);

        byte[] bytes = new byte[2];
        effective.CopyTo(0, bytes);
        bytes.ShouldBe([1, 2]);
        source.CopyTo(0, bytes);
        bytes.ShouldBe([1, 2]);
        validations.ShouldBe([1, 2]);
        upcaster.ObservedToken.ShouldBe(cancellation.Token);
        Should.Throw<ObjectDisposedException>(() => upcaster.Input!.CopyTo(0, new byte[2]));
        Should.Throw<ObjectDisposedException>(() => upcaster.Writer!.Write([3]));
        Should.Throw<ObjectDisposedException>(() => upcaster.Scratch!.WithScratch(1, _ => { }, CancellationToken.None));
        budget.LiveBytes.ShouldBe(1024 * 1024);
        effective.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("missing-complete")]
    [InlineData("swallowed-second-complete")]
    [InlineData("wrong-identity")]
    public async Task FailedHop_ReleasesAllCapacityAndRefusesRetainedInput(string behavior)
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster(behavior);
        var budget = new EventBufferBudget();
        var executor = CreateExecutor(registry, upcaster, static (_, _, _, _, _, _) => { });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]);
        sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        await Should.ThrowAsync<Exception>(async () => await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None));

        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => upcaster.Input!.CopyTo(0, new byte[2]));
    }

    [Fact]
    public async Task SharedBudget_ChargesSourceOutputAndScratchBeforeCallbackAllocation()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var budget = new EventBufferBudget(1024 * 1024 + 3);
        var executor = CreateExecutor(registry, upcaster, static (_, _, _, _, _, _) => { });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]);
        sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None));

        failure.Message.ShouldContain("ScratchLimit");
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task CancellationAfterWrite_PreservesOriginalTokenAndReleasesAllOwners()
    {
        using EventDomainRegistry registry = CreateRegistry();
        using var cancellation = new CancellationTokenSource();
        var upcaster = new LeaseRecordingEventUpcaster(afterWrite: cancellation.Cancel);
        var budget = new EventBufferBudget();
        var executor = CreateExecutor(registry, upcaster, static (_, _, _, _, _, _) => { });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]);
        sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(
            async () => await executor.UpcastAsync("evt", 1, source, budget, cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => upcaster.Input!.CopyTo(0, new byte[2]));
    }

    [Fact]
    public void RejectsOptionsDigestMismatchBeforeInvokingAnyUpcaster()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        byte[] changedOptions = new byte[32];
        changedOptions[0] = 1;
        var binding = new RegisteredEventUpcaster("test-upcaster", upcaster, changedOptions);

        Should.Throw<InvalidOperationException>(() => new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster> { [("evt", 1)] = binding },
            static (_, _, _, _, _, _) => { }));
        upcaster.Input.ShouldBeNull();
    }

    [Fact]
    public async Task SwallowedLateScratchAttemptDuringOutputValidation_RejectsBeforeAcceptingOutput()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var budget = new EventBufferBudget();
        var executor = CreateExecutor(registry, upcaster, (_, _, version, _, _, _) =>
        {
            if (version == 2)
            {
                try
                {
                    upcaster.Scratch!.WithScratch(1, _ => { }, CancellationToken.None);
                }
                catch (ObjectDisposedException)
                {
                    // Refusal must remain sticky even when domain code swallows it.
                }
            }
        });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]);
        sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None));

        failure.Message.ShouldContain("UpcasterContractViolation");
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks an earlier observation prevents allocation and every domain callback.</summary>
    [Fact]
    public async Task ObservedLossBeforeInvocationRefusesWithoutAllocatingOrCallingDomainCode()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var budget = new EventBufferBudget();
        bool validated = false;
        var executor = CreateExecutor(registry, upcaster, (_, _, _, _, _, _) => validated = true);
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
        registry.CapabilityLoss.ObserveViolation();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None));

        failure.Message.ShouldContain("CapabilityMismatch");
        validated.ShouldBeFalse();
        upcaster.Input.ShouldBeNull();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks observed loss from each callback refuses output and clears all private owners.</summary>
    [Theory]
    [InlineData("source-validation")]
    [InlineData("hop")]
    [InlineData("output-validation")]
    public async Task ObservedLossDuringCallbackRefusesResultAndReleasesPrivateOwners(string stage)
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster(afterWrite: () =>
        {
            if (stage == "hop") { registry.CapabilityLoss.ObserveViolation(); }
        });
        var budget = new EventBufferBudget();
        var executor = CreateExecutor(registry, upcaster, (_, _, version, _, _, _) =>
        {
            if (stage == "source-validation" && version == 1 || stage == "output-validation" && version == 2)
            {
                registry.CapabilityLoss.ObserveViolation();
            }
        });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None));

        failure.Message.ShouldContain("CapabilityMismatch");
        budget.LiveBytes.ShouldBe(0);
        byte[] original = new byte[2]; source.CopyTo(0, original); original.ShouldBe([1, 2]);
        if (stage == "source-validation") { upcaster.Input.ShouldBeNull(); }
        else { Should.Throw<ObjectDisposedException>(() => upcaster.Input!.CopyTo(0, new byte[2])); }
    }

    /// <summary>Checks a concurrent observation during an awaited callback prevents an uncommitted success.</summary>
    [Fact]
    public async Task ConcurrentObservationDuringAwaitRefusesResumedSuccess()
    {
        using EventDomainRegistry registry = CreateRegistry();
        var upcaster = new SuspendedEventUpcaster();
        var executor = CreateExecutor(registry, upcaster, static (_, _, _, _, _, _) => { });
        var budget = new EventBufferBudget();
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
        Task<ImmutablePayload> pending = executor.UpcastAsync("evt", 1, source, budget, CancellationToken.None).AsTask();
        try
        {
            await upcaster.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            registry.CapabilityLoss.ObserveViolation();
        }
        finally { upcaster.Resume.TrySetResult(); }

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(async () => await pending);

        failure.Message.ShouldContain("CapabilityMismatch");
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => upcaster.Input!.CopyTo(0, new byte[2]));
    }

    /// <summary>Checks one shared observation refuses zero-hop execution and new executor admission.</summary>
    [Fact]
    public async Task LossInSharedScopeRefusesExistingAndNewExecutorsAcrossRegistries()
    {
        var loss = new EventEvolutionCapabilityLoss();
        using EventDomainRegistry first = CreateRegistry(loss);
        using EventDomainRegistry second = CreateRegistry(loss);
        var upcaster = new LeaseRecordingEventUpcaster();
        var executor = CreateExecutor(second, upcaster, static (_, _, _, _, _, _) => { });
        using var sourceWriter = new BoundedPayloadWriter(2, CancellationToken.None);
        sourceWriter.Write([1, 2]); sourceWriter.Complete();
        using ImmutablePayload source = sourceWriter.TakeCompletedPayload();
        first.CapabilityLoss.ObserveViolation();

        await Should.ThrowAsync<InvalidOperationException>(async () => await executor.UpcastAsync(
            "evt", 2, source, new EventBufferBudget(), CancellationToken.None));
        Should.Throw<InvalidOperationException>(() => CreateExecutor(second, upcaster, static (_, _, _, _, _, _) => { }));
        upcaster.Input.ShouldBeNull();
    }

    private static EventUpcastChainExecutor CreateExecutor(EventDomainRegistry registry, IEventUpcaster upcaster,
        EventVersionValidator validator)
        => new(registry, new Dictionary<(string, int), RegisteredEventUpcaster>
        {
            [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
        }, validator);

    /// <summary>Creates test-local two-version rows with the actual executing callable assembly digest.</summary>
    internal static EventDomainRegistry CreateRegistry(EventEvolutionCapabilityLoss? capabilityLoss = null)
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        byte[] descriptor = Convert.FromHexString(fixture["DescriptorRow"]);
        // D current-version field starts after tag + U(d) + U(evt) + count + U(r) and field tag.
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2);
        byte[] versionOne = Convert.FromHexString(fixture["VersionRow"]);
        byte[] versionTwo = versionOne.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(versionTwo.AsSpan(13, 4), 2);
        using var edge = new EventEvolutionBinaryWriter(1024);
        edge.WriteByte(0x45);
        edge.WriteString("d");
        edge.WriteString("evt");
        edge.WriteInt32(1);
        edge.WriteUInt16(12);
        edge.WriteByte(1); edge.WriteInt32(2);
        edge.WriteByte(2); edge.WriteString("json");
        edge.WriteByte(3); edge.WriteString("json");
        edge.WriteByte(4); edge.WriteString("serializer");
        edge.WriteByte(5); edge.WriteHash(new byte[32]);
        edge.WriteByte(6); edge.WriteHash(new byte[32]);
        edge.WriteByte(7); edge.WriteString("test-upcaster");
        edge.WriteByte(8);
        using (FileStream assembly = File.OpenRead(typeof(LeaseRecordingEventUpcaster).Assembly.Location))
        {
            edge.WriteHash(SHA256.HashData(assembly));
        }

        edge.WriteByte(9); edge.WriteHash(new byte[32]);
        edge.WriteByte(10); edge.WriteString("no-payload-identity");
        edge.WriteByte(11); edge.WriteHash(new byte[32]);
        edge.WriteByte(12); edge.WriteHash(new byte[32]);
        return new EventDomainRegistry("d", [Convert.FromHexString(fixture["AliasRow"]), descriptor,
            versionOne, versionTwo, edge.CopyEncodedBytes(), Convert.FromHexString(fixture["SharedRow"])],
            capabilityLoss: capabilityLoss ?? new EventEvolutionCapabilityLoss());
    }
}
