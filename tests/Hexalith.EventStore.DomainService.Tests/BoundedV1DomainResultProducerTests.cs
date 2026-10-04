using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class BoundedV1DomainResultProducerTests
{
    [Fact]
    public async Task PreflightRejectsCompleteDeclaredResultBeforeCallingSerializer()
    {
        bool called = false;
        var producer = Producer(1024 * 1024, (_, _, _) => { called = true; return Task.CompletedTask; });
        DomainResult result = DomainResult.Success(Enumerable.Range(0, 100).Select(_ => (IEventPayload)new BoundedProducerTestEvent()).ToArray());
        (await Should.ThrowAsync<InvalidOperationException>(() => producer.ProduceAsync(result, CancellationToken.None))).Message.ShouldContain("ResultLimit");
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task CountAndUnknownTypeRefusalPrecedeAllSerialization()
    {
        bool called = false;
        var producer = Producer(16, (_, _, _) => { called = true; return Task.CompletedTask; });
        await Should.ThrowAsync<InvalidOperationException>(() => producer.ProduceAsync(
            DomainResult.Success(Enumerable.Range(0, 1001).Select(_ => (IEventPayload)new BoundedProducerTestEvent()).ToArray()), CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => producer.ProduceAsync(
            DomainResult.Success([new BoundedProducerUnknownTestEvent()]), CancellationToken.None));
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task SerializesOncePropagatesOriginalTokenAndInvalidatesRetainedSink()
    {
        Stream? retained = null;
        int calls = 0;
        using var cancellation = new CancellationTokenSource();
        var producer = Producer(16, async (_, stream, token) =>
        {
            calls++;
            token.ShouldBe(cancellation.Token);
            retained = stream;
            await stream.WriteAsync(new byte[] { 1, 2 }, token);
        });
        DomainServiceWireResult result = await producer.ProduceAsync(DomainResult.Success([new BoundedProducerTestEvent()]), cancellation.Token);
        calls.ShouldBe(1);
        result.Events.Single().EventTypeName.ShouldBe("legacy-exact-alias");
        result.Events.Single().Payload.ShouldBe([1, 2]);
        Should.Throw<ObjectDisposedException>(() => retained!.WriteByte(3));
    }

    [Fact]
    public async Task SwallowedOverflowStillRefusesResultAndOriginalCancellationCannotBeBypassed()
    {
        var overflowing = Producer(1, (_, stream, _) =>
        {
            try { stream.Write([1, 2]); } catch (InvalidOperationException) { }
            return Task.CompletedTask;
        });
        await Should.ThrowAsync<InvalidOperationException>(() => overflowing.ProduceAsync(DomainResult.Success([new BoundedProducerTestEvent()]), CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        var canceled = Producer(16, (_, stream, _) =>
        {
            cancellation.Cancel();
            Should.Throw<OperationCanceledException>(() => stream.WriteAsync(new byte[] { 1 }, CancellationToken.None));
            return Task.CompletedTask;
        });
        await Should.ThrowAsync<OperationCanceledException>(() => canceled.ProduceAsync(DomainResult.Success([new BoundedProducerTestEvent()]), cancellation.Token));
    }

    [Fact]
    public async Task AdmittedSerializedBytesUseExactAliasAndSkipClrReserialization()
    {
        bool called = false;
        var producer = new BoundedV1DomainResultProducer([
            new(typeof(BoundedProducerSerializedTestEvent), "legacy-exact-alias", "json", 16,
                (_, _, _) => { called = true; throw new InvalidOperationException("Must not serialize wrapper"); }),
        ]);
        byte[] original = [1, 2];
        DomainServiceWireResult result = await producer.ProduceAsync(DomainResult.Success([new BoundedProducerSerializedTestEvent(original)]), CancellationToken.None);
        original[0] = 9;
        result.Events[0].Payload.ShouldBe([1, 2]);
        called.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => producer.ProduceAsync(
            DomainResult.Success([new BoundedProducerSerializedTestEvent(new byte[17])]), CancellationToken.None));
    }

    [Fact]
    public void DefaultReadableLimitRequiresExplicitLargerMeasuredProfile()
    {
        Should.Throw<ArgumentException>(() => Producer(1024 * 1024 + 1, (_, _, _) => Task.CompletedTask));
        _ = new BoundedV1DomainResultProducer([
            new(typeof(BoundedProducerTestEvent), "legacy-exact-alias", "json", 1024 * 1024 + 1, (_, _, _) => Task.CompletedTask),
        ], 1024 * 1024 + 1);
    }

    [Fact]
    public async Task CombinedEscapedMetadataRefusesBeforeSerializerAndInvalidUnicodeCannotRegister()
    {
        bool called = false;
        var producer = new BoundedV1DomainResultProducer([
            new(typeof(BoundedProducerTestEvent), new string('a', 60_000), new string('b', 60_000), 16,
                (_, _, _) => { called = true; return Task.CompletedTask; }),
        ]);
        (await Should.ThrowAsync<InvalidOperationException>(() => producer.ProduceAsync(
            DomainResult.Success([new BoundedProducerTestEvent()]), CancellationToken.None))).Message.ShouldContain("MetadataLimit");
        called.ShouldBeFalse();
        Should.Throw<System.Text.EncoderFallbackException>(() => new BoundedV1DomainResultProducer([
            new(typeof(BoundedProducerTestEvent), "\ud800", "json", 16, (_, _, _) => Task.CompletedTask),
        ]));
    }

    [Fact]
    public async Task WireRendererStreamsCanonicalBase64AndEscapedUnicodeWithoutWholeJsonSerialization()
    {
        byte[] payload = Enumerable.Range(0, 40_001).Select(i => (byte)i).ToArray();
        var result = new DomainServiceWireResult(false, [new("legacy-\"alias", payload, "json")], "é😀\n\u0001");
        using var output = new MemoryStream();
        await BoundedV1WireResultResponse.WriteAsync(output, result, CancellationToken.None);
        output.Position = 0;
        DomainServiceWireResult parsed = (await System.Text.Json.JsonSerializer.DeserializeAsync<DomainServiceWireResult>(
            output, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
        parsed.Events[0].Payload.ShouldBe(payload);
        parsed.Events[0].EventTypeName.ShouldBe(result.Events[0].EventTypeName);
        parsed.ResultPayload.ShouldBe(result.ResultPayload);
        parsed.WriterMode.ShouldBeNull();
    }

    [Fact]
    public async Task WireRendererPreservesCancellationAndRefusesUnsolicitedMetadata()
    {
        using var output = new MemoryStream();
        await Should.ThrowAsync<OperationCanceledException>(() => BoundedV1WireResultResponse.WriteAsync(output,
            new DomainServiceWireResult(false, []), new CancellationToken(true)));
        output.Length.ShouldBe(0);
        await Should.ThrowAsync<InvalidOperationException>(() => BoundedV1WireResultResponse.WriteAsync(output,
            new DomainServiceWireResult(false, []) { WriterMode = "V2" }, CancellationToken.None));
        output.Length.ShouldBe(0);
    }

    private static BoundedV1DomainResultProducer Producer(int maximum, Func<IEventPayload, Stream, CancellationToken, Task> serialize)
        => new([new(typeof(BoundedProducerTestEvent), "legacy-exact-alias", "json", maximum, serialize)]);

}
