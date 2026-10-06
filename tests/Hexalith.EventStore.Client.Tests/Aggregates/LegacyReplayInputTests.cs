using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Verifies whole-array refusal and immutable private input before domain callbacks.</summary>
public sealed class LegacyReplayInputTests
{
    /// <summary>Checks count and conservative overhead are refused before source reads or state creation.</summary>
    [Theory]
    [InlineData(100_001)]
    [InlineData(32_769)]
    public void OversizedCountRefusesBeforeAllocationOrDomainCode(int count)
    {
        using var scope = new LegacyReplayMutationScope();
        var events = new ReplayInputProbeCollection(count);
        AggregateReconstructionResult result = AggregateReplayer.Replay<LegacyReplayMutationState>(Request(events));
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Limit);
        result.ReasonCode.ShouldBe("LegacyArrayLimit");
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        events.CountReads.ShouldBe(1);
        events.IndexReads.ShouldBe(0);
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks complete payload and metadata admission precedes the first Apply.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedLaterEventRefusesBeforeStateOrApply(bool metadata)
    {
        using var scope = new LegacyReplayMutationScope();
        ReplayEventEnvelope first = Event(1);
        ReplayEventEnvelope second = metadata
            ? Event(2) with { EventTypeName = new string('x', 600_000) }
            : Event(2) with { Payload = new byte[64 * 1024 * 1024] };
        AggregateReconstructionResult result = AggregateReplayer.Replay<LegacyReplayMutationState>(Request([first, second]));
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Limit);
        result.ReasonCode.ShouldBe(metadata ? "MetadataLimit" : "LegacyArrayLimit");
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks encoded metadata capacity is distinct from conservative live-string accounting.</summary>
    [Theory]
    [InlineData('x', 100_000, false)]
    [InlineData('x', 600_000, true)]
    [InlineData('\u0001', 100_000, true)]
    [InlineData('\u00e9', 100_000, true)]
    public void MetadataCeilingCountsActualCanonicalJsonEscaping(char value, int length, bool rejected)
    {
        ReplayEventEnvelope original = Event(1) with { EventTypeName = new string(value, length) };
        string wire = JsonSerializer.Serialize(original with { Payload = [] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        (System.Text.Encoding.UTF8.GetByteCount(wire) > 512 * 1024).ShouldBe(rejected);
        using LegacyReplayInput input = LegacyReplayInput.Capture(Request([original]), CancellationToken.None);
        if (rejected) { input.Refusal!.ReasonCode.ShouldBe("MetadataLimit"); }
        else { input.Refusal.ShouldBeNull(); input.Events.Count.ShouldBe(1); }
    }

    /// <summary>Checks the exact encoded ceiling admits its last byte and refuses the next one.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void MetadataBoundaryMatchesCanonicalWebJsonLength(int delta)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ReplayEventEnvelope template = Event(1) with { Payload = [], EventTypeName = string.Empty };
        int fixedBytes = System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(template, options));
        ReplayEventEnvelope item = template with { EventTypeName = new string('a', 512 * 1024 - fixedBytes + delta) };
        int actualBytes = System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(item, options));
        actualBytes.ShouldBe(512 * 1024 + delta);
        using LegacyReplayInput input = LegacyReplayInput.Capture(Request([item]), CancellationToken.None);
        if (delta > 0) { input.Refusal!.ReasonCode.ShouldBe("MetadataLimit"); }
        else { input.Refusal.ShouldBeNull(); }
    }

    /// <summary>Checks encoded metadata across the complete array consumes the aggregate accounting budget.</summary>
    [Fact]
    public void CompleteArrayChargesEscapedMetadataBeforeApply()
    {
        using var scope = new LegacyReplayMutationScope();
        ReplayEventEnvelope shared = Event(1) with { EventTypeName = new string('\u0001', 80_000) };
        ReplayEventEnvelope[] events = Enumerable.Repeat(shared, 600).ToArray();
        AggregateReconstructionResult result = AggregateReplayer.Replay<LegacyReplayMutationState>(Request(events));
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Limit);
        result.ReasonCode.ShouldBe("LegacyArrayLimit");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks a state constructor and Apply cannot substitute or corrupt a later source event.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DomainMutationCannotChangeCapturedPayloadsOrEventReferences(bool constructor)
    {
        using var scope = new LegacyReplayMutationScope();
        ReplayEventEnvelope[] events = [Event(1), Event(2)];
        byte[] originalSecond = events[1].Payload;
        Action mutate = () =>
        {
            originalSecond[10] = (byte)'9';
            events[1] = Event(20) with { MetadataVersion = 2 };
        };
        if (constructor) { scope.OnConstruct = mutate; }
        else { scope.OnFirstApply = mutate; }
        AggregateReconstructionResult result = AggregateReplayer.Replay<LegacyReplayMutationState>(Request(events));
        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(2);
        scope.Applied.ShouldBe([1, 1]);
        JsonDocument.Parse(result.StateJson!).RootElement.GetProperty("total").GetInt32().ShouldBe(2);
        originalSecond[10].ShouldBe((byte)'9');
    }

    /// <summary>Checks only private input is cleared after the owner ends.</summary>
    [Fact]
    public void DisposalClearsPrivatePayloadsAndPreservesCallerBytes()
    {
        ReplayEventEnvelope original = Event(1);
        byte[] before = original.Payload.ToArray();
        LegacyReplayInput input = LegacyReplayInput.Capture(Request([original]), CancellationToken.None);
        byte[] privateBytes = input.Events[0].Payload;
        privateBytes.ShouldNotBeSameAs(original.Payload);
        privateBytes.ShouldBe(before);
        input.Dispose();
        privateBytes.ShouldAllBe(static value => value == 0);
        original.Payload.ShouldBe(before);
    }

    /// <summary>Checks cancellation is observed before even reading the source count.</summary>
    [Fact]
    public void PreCancellationPreservesTokenAndReadsNoSource()
    {
        var source = new ReplayInputProbeCollection(1, Event(1));
        var token = new CancellationToken(true);
        OperationCanceledException error = Should.Throw<OperationCanceledException>(
            () => LegacyReplayInput.Capture(Request(source), token));
        error.CancellationToken.ShouldBe(token);
        source.CountReads.ShouldBe(0);
        source.IndexReads.ShouldBe(0);
    }

    /// <summary>Checks malformed Unicode yields a typed conflict without exposing state.</summary>
    [Fact]
    public void InvalidMetadataUnicodeRefusesBeforeStateCreation()
    {
        using var scope = new LegacyReplayMutationScope();
        AggregateReconstructionResult result = AggregateReplayer.Replay<LegacyReplayMutationState>(
            Request([Event(1) with { MessageId = "\ud800" }]));
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Conflict);
        result.ReasonCode.ShouldBe("ReplayScalarInvalid");
        scope.Constructed.ShouldBe(0);
    }

    private static ReplayEventEnvelope Event(long sequence)
        => new(sequence, nameof(LegacyReplayMutationEvent), "{\"amount\":1}"u8.ToArray(), "json", 1, "message", null, null);

    private static AggregateReconstructionRequest Request(IReadOnlyList<ReplayEventEnvelope> events)
        => new("tenant", "domain", "aggregate", "id", 2, events, true, null);
}
