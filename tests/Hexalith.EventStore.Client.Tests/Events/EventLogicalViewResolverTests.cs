using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Checks allow-listed logical resolution preserves original evidence and composes private owner budgets.</summary>
public sealed class EventLogicalViewResolverTests
{
    [Theory]
    [InlineData(1, "Legacy.Event", null, null)]
    [InlineData(2, "evt", "evt", 1)]
    public async Task ResolvesAllowListedSourceThroughRegisteredChainWithoutChangingOriginal(
        int metadataVersion, string eventTypeName, string? canonicalType, int? payloadVersion)
    {
        using EventDomainRegistry registry = EventUpcastChainExecutorTests.CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, static (_, _, _, _, _, _) => { });
        var resolver = new EventLogicalViewResolver(registry, executor);
        byte[] source = [1, 2];

        using ResolvedLogicalEvent result = await resolver.ResolveAsync("d", eventTypeName,
            metadataVersion, canonicalType, payloadVersion, "json", source, CancellationToken.None).ConfigureAwait(true);

        result.CanonicalType.ShouldBe("evt");
        result.SourceVersion.ShouldBe(1);
        result.CurrentVersion.ShouldBe(2);
        result.SerializationFormat.ShouldBe("json");
        byte[] effective = new byte[result.Payload.Length];
        result.Payload.CopyTo(0, effective);
        effective.ShouldBe([1, 2]);
        source.ShouldBe([1, 2]);
        upcaster.Input.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(1, "Unlisted.Event", null, null, "json")]
    [InlineData(1, "Legacy.Event", "evt", null, "json")]
    [InlineData(2, "Legacy.Event", "evt", 1, "json")]
    [InlineData(2, "evt", "evt", 3, "json")]
    [InlineData(2, "evt", "evt", 1, "xml")]
    public async Task RefusesUnknownOrContradictorySourceBeforeUpcaster(
        int metadataVersion, string eventTypeName, string? canonicalType, int? payloadVersion, string format)
    {
        using EventDomainRegistry registry = EventUpcastChainExecutorTests.CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, static (_, _, _, _, _, _) => { });
        var resolver = new EventLogicalViewResolver(registry, executor);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync("d", eventTypeName, metadataVersion, canonicalType,
                payloadVersion, format, new byte[] { 1, 2 }, CancellationToken.None).ConfigureAwait(true));

        upcaster.Input.ShouldBeNull();
    }

    [Fact]
    public void RejectsExecutorBoundToAnotherRegistryInstance()
    {
        using EventDomainRegistry first = EventUpcastChainExecutorTests.CreateRegistry();
        using EventDomainRegistry second = EventUpcastChainExecutorTests.CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var executor = new EventUpcastChainExecutor(first,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, static (_, _, _, _, _, _) => { });

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() =>
            new EventLogicalViewResolver(second, executor));

        error.Message.ShouldContain("CapabilityMismatch");
    }

    /// <summary>Checks retained effective owners remain charged and a refused successor releases its attempted private copy.</summary>
    [Fact]
    public async Task SharedBudgetIncludesEarlierEffectiveOwnersAndReleasesFailedCopy()
    {
        using EventDomainRegistry registry = EventUpcastChainExecutorTests.CreateRegistry();
        var upcaster = new LeaseRecordingEventUpcaster();
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, static (_, _, _, _, _, _) => { });
        var resolver = new EventLogicalViewResolver(registry, executor);
        var budget = new EventBufferBudget(5);
        byte[] source = [1, 2];
        using ResolvedLogicalEvent first = await resolver.ResolveAsync("d", "evt", 2, "evt", 2,
            "json", source, CancellationToken.None, budget).ConfigureAwait(true);
        using ResolvedLogicalEvent second = await resolver.ResolveAsync("d", "evt", 2, "evt", 2,
            "json", source, CancellationToken.None, budget).ConfigureAwait(true);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync("d", "evt", 2, "evt", 2, "json", source,
                CancellationToken.None, budget).ConfigureAwait(true));

        error.Message.ShouldContain("ScratchLimit");
        budget.LiveBytes.ShouldBe(4);
        source.ShouldBe([1, 2]);
        first.Dispose();
        second.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }
}
