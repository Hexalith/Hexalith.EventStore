using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Checks bounded envelope admission and deterministic private-byte clearing during command replay.</summary>
public sealed class LegacyCommandReplayInputTests
{
    /// <summary>Checks large declared counts refuse before enumeration or state construction.</summary>
    [Theory]
    [InlineData(100_001)]
    [InlineData(32_769)]
    public void OversizedCountRefusesBeforeSourceEnumerationOrStateConstruction(int count)
    {
        using var scope = new LegacyReplayMutationScope();
        var source = new CommandReplayProbeCollection(count);
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(source));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
        source.CountReads.ShouldBe(1);
        source.EntriesRead.ShouldBe(0);
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks uncounted sources stop within the conservative live-array budget.</summary>
    [Fact]
    public void UncountedEnumerationCannotAllocateAnUnboundedList()
    {
        using var scope = new LegacyReplayMutationScope();
        var source = new CommandReplayUnknownCountSequence(100_001);
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(source));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
        source.EntriesRead.ShouldBeLessThanOrEqualTo(32_769);
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks every envelope is admitted before the first state constructor or Apply.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedLaterEnvelopeRefusesWholeInputBeforeDomainCode(bool metadata)
    {
        using var scope = new LegacyReplayMutationScope();
        EventEnvelope second = metadata
            ? Event(2, extensions: new Dictionary<string, string> { ["evidence"] = new string('\u0001', 100_000) })
            : Event(2, new byte[64 * 1024 * 1024]);
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(new[] { Event(1), second }));
        error.Message.ShouldStartWith(metadata ? "MetadataLimit:" : "LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks nested snapshot histories share one readable-source ceiling.</summary>
    [Fact]
    public void NestedSnapshotEnvelopeSequencesSharePayloadAdmission()
    {
        using var scope = new LegacyReplayMutationScope();
        var snapshot = new DomainServiceCurrentState(null, [Event(1, new byte[32 * 1024 * 1024])], 0, 1);
        var current = new DomainServiceCurrentState(snapshot, [Event(2, new byte[32 * 1024 * 1024 + 1])], 1, 2);
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(current));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks empty wrapper chains cannot bypass admission and exhaust recursive replay.</summary>
    [Fact]
    public void DeepEmptySnapshotWrappersRefuseBeforeStateConstruction()
    {
        using var scope = new LegacyReplayMutationScope();
        object? source = null;
        for (int depth = 0; depth < 128; depth++)
        {
            source = new DomainServiceCurrentState(source, [], 0, 0);
        }

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(source!));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks encoded extension metadata composes with the fixed per-event live charge.</summary>
    [Fact]
    public void CompleteSequenceChargesEncodedMetadataBeforeApply()
    {
        using var scope = new LegacyReplayMutationScope();
        EventEnvelope shared = Event(1, extensions: new Dictionary<string, string> { ["evidence"] = new string('\u0001', 80_000) });
        EventEnvelope[] source = Enumerable.Repeat(shared, 600).ToArray();
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => Rehydrate(source));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks escaped metadata admission matches independent serialized envelope sizes.</summary>
    [Theory]
    [InlineData('a', 100_000, false)]
    [InlineData('\u0001', 100_000, true)]
    [InlineData('\u00e9', 100_000, true)]
    public void MetadataAdmissionCountsJsonEscapingAndExtensions(char value, int length, bool refused)
    {
        EventEnvelope source = Event(1, extensions: new Dictionary<string, string> { ["evidence"] = new string(value, length) });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] wire = JsonSerializer.SerializeToUtf8Bytes(new EventEnvelope(source.Metadata, [], source.Extensions), options);
        (wire.Length > 512 * 1024).ShouldBe(refused);
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        if (refused)
        {
            Should.Throw<InvalidOperationException>(() => owner.CaptureEvents(new[] { source })).Message.ShouldStartWith("MetadataLimit:");
        }
        else
        {
            owner.CaptureEvents(new[] { source }).Count.ShouldBe(1);
        }
    }

    /// <summary>Checks the exact metadata ceiling against independent Web JSON serialization.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void MetadataBoundaryMatchesTheCompleteEncodedEnvelope(int delta)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        EventEnvelope template = Event(1, [], new Dictionary<string, string> { ["evidence"] = string.Empty });
        int fixedBytes = JsonSerializer.SerializeToUtf8Bytes(template, options).Length;
        EventEnvelope source = Event(1, [], new Dictionary<string, string>
        {
            ["evidence"] = new string('a', 512 * 1024 - fixedBytes + delta),
        });
        JsonSerializer.SerializeToUtf8Bytes(source, options).Length.ShouldBe(512 * 1024 + delta);
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        if (delta > 0)
        {
            Should.Throw<InvalidOperationException>(() => owner.CaptureEvents(new[] { source })).Message.ShouldStartWith("MetadataLimit:");
        }
        else
        {
            owner.CaptureEvents(new[] { source }).Count.ShouldBe(1);
        }
    }

    /// <summary>Checks historical maps are retained while distinct copied dictionary capacity is charged.</summary>
    [Fact]
    public void ManySmallLegacyExtensionEntriesCannotBypassTheLiveArrayCeiling()
    {
        var extensions = Enumerable.Range(0, 20_000).ToDictionary(static index => index.ToString(System.Globalization.CultureInfo.InvariantCulture), static _ => "");
        EventEnvelope shared = Event(1, [], extensions);
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        InvalidOperationException error = Should.Throw<InvalidOperationException>(
            () => owner.CaptureEvents(Enumerable.Repeat(shared, 60).ToArray()));
        error.Message.ShouldStartWith("LegacyArrayLimit:");
    }

    /// <summary>Checks private bytes retained by a failed nested admission are cleared when its owner ends.</summary>
    [Fact]
    public void LaterRefusalDoesNotExtendTheLifetimeOfAnEarlierPrivateCopy()
    {
        EventEnvelope original = Event(1);
        var owner = new LegacyCommandReplayInput(CancellationToken.None);
        EventEnvelope captured = (EventEnvelope)owner.CaptureEvents(new[] { original })[0]!;
        Should.Throw<InvalidOperationException>(
            () => owner.CaptureEvents(new CommandReplayProbeCollection(100_001))).Message.ShouldStartWith("LegacyArrayLimit:");
        owner.Dispose();
        captured.Payload.ShouldAllBe(static value => value == 0);
        original.Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
    }

    /// <summary>Checks private copies remain isolated and are cleared while source bytes stay unchanged.</summary>
    [Fact]
    public void DisposalClearsEveryPrivatePayloadAndPreservesSourceBytes()
    {
        EventEnvelope[] sources = [Event(1), Event(2)];
        byte[] first = sources[0].Payload.ToArray();
        byte[] second = sources[1].Payload.ToArray();
        var owner = new LegacyCommandReplayInput(CancellationToken.None);
        EventEnvelope[] captured = owner.CaptureEvents(sources).Cast<EventEnvelope>().ToArray();
        captured[0].Payload.ShouldNotBeSameAs(sources[0].Payload);
        captured[1].Payload.ShouldNotBeSameAs(sources[1].Payload);
        owner.Dispose();
        owner.Dispose();
        captured.ShouldAllBe(static item => item.Payload.All(static value => value == 0));
        sources[0].Payload.ShouldBe(first);
        sources[1].Payload.ShouldBe(second);
    }

    /// <summary>Checks a state constructor cannot corrupt the privately captured successor payload.</summary>
    [Fact]
    public void ConstructorMutationCannotChangeCapturedPayloadOrEventReference()
    {
        using var scope = new LegacyReplayMutationScope();
        EventEnvelope[] source = [Event(1), Event(2)];
        scope.OnConstruct = () =>
        {
            source[1].Payload[10] = (byte)'9';
            source[1] = Event(20);
        };
        Rehydrate(source).Total.ShouldBe(2);
        scope.Applied.ShouldBe([1, 1]);
    }

    /// <summary>Checks an early converter cannot mutate the payload or replace the later source event.</summary>
    [Fact]
    public void FirstConverterCannotChangeLaterCapturedPayloadOrEventReference()
    {
        using var scope = new LegacyReplayMutationScope();
        EventEnvelope[] source = [Event(1), Event(2)];
        source = source.Select(item => new EventEnvelope(item.Metadata with
        {
            EventTypeName = nameof(CommandReplayDeserializationEvent),
        }, item.Payload, item.Extensions)).ToArray();
        byte[] secondSourceBytes = source[1].Payload;
        scope.OnFirstApply = () =>
        {
            secondSourceBytes[10] = (byte)'9';
            source[1] = Event(20);
        };
        CommandReplayDeserializationState state = DomainProcessorStateRehydrator.RehydrateState<CommandReplayDeserializationState>(source,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(CommandReplayDeserializationState)))!;
        state.Total.ShouldBe(2);
        scope.Applied.ShouldBe([1, 1]);
        secondSourceBytes[10].ShouldBe((byte)'9');
        source[1].Metadata.SequenceNumber.ShouldBe(20);
    }

    /// <summary>Checks cancellation from a payload converter wins before state construction or Apply.</summary>
    [Fact]
    public void ConverterCancellationPreservesTheOriginalTokenAndPreventsApply()
    {
        using var scope = new LegacyReplayMutationScope();
        using var cancellation = new CancellationTokenSource();
        EventEnvelope original = Event(1);
        EventEnvelope source = new(original.Metadata with { EventTypeName = nameof(CommandReplayDeserializationEvent) }, original.Payload, original.Extensions);
        byte[] originalBytes = original.Payload.ToArray();
        scope.OnFirstApply = cancellation.Cancel;
        OperationCanceledException error = Should.Throw<OperationCanceledException>(() =>
            DomainProcessorStateRehydrator.RehydrateState<CommandReplayDeserializationState>(new[] { source },
                DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(CommandReplayDeserializationState)), cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
        original.Payload.ShouldBe(originalBytes);
    }

    /// <summary>Checks cancellation before admission reads no source and preserves the exact original token.</summary>
    [Fact]
    public void PreCancellationReadsNoSourceAndPreservesToken()
    {
        var source = new CommandReplayProbeCollection(1, Event(1));
        var cancellation = new CancellationToken(true);
        OperationCanceledException error = Should.Throw<OperationCanceledException>(() => Rehydrate(source, cancellation));
        error.CancellationToken.ShouldBe(cancellation);
        source.CountReads.ShouldBe(0);
        source.EntriesRead.ShouldBe(0);
    }

    /// <summary>Checks a cancelled owner still clears payloads that were captured before cancellation.</summary>
    [Fact]
    public void CancellationThenDisposalClearsPreviouslyCapturedPrivatePayload()
    {
        using var cancellation = new CancellationTokenSource();
        var owner = new LegacyCommandReplayInput(cancellation.Token);
        EventEnvelope original = Event(1);
        EventEnvelope captured = (EventEnvelope)owner.CaptureEvents(new[] { original })[0]!;
        cancellation.Cancel();
        OperationCanceledException error = Should.Throw<OperationCanceledException>(() => owner.CaptureEvents(new[] { Event(2) }));
        error.CancellationToken.ShouldBe(cancellation.Token);
        owner.Dispose();
        captured.Payload.ShouldAllBe(static value => value == 0);
        original.Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
    }

    private static LegacyReplayMutationState Rehydrate(object source, CancellationToken cancellation = default)
        => DomainProcessorStateRehydrator.RehydrateState<LegacyReplayMutationState>(source,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(LegacyReplayMutationState)), cancellation)!;

    private static EventEnvelope Event(long sequence, byte[]? payload = null, IReadOnlyDictionary<string, string>? extensions = null)
        => new(new EventMetadata("message", "aggregate", "mutation", "tenant", "domain", sequence, sequence,
            DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", nameof(LegacyReplayMutationEvent), 1, "json"),
            payload ?? "{\"amount\":1}"u8.ToArray(), extensions);
}
