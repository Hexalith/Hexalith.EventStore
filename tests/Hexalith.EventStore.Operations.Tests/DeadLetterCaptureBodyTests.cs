using System.Diagnostics.Metrics;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Endpoints;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies content-length-free capture bodies remain strictly bounded while streaming.
/// </summary>
public sealed class DeadLetterCaptureBodyTests
{
    /// <summary>Verifies the exact body-size boundary is retained without an oversize result.</summary>
    [Fact]
    public async Task ChunkedBodyAtBoundaryIsAccepted()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = null;
        context.Request.Body = new ChunkedReadStream(Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray(), 3);

        (byte[] body, bool tooLarge) = await DeadLetterOperationsEndpointExtensions
            .ReadBoundedBodyAsync(context.Request, 16);

        tooLarge.ShouldBeFalse();
        body.Length.ShouldBe(16);
    }

    /// <summary>Verifies streaming stops at max plus one and reports an oversized chunked body.</summary>
    [Fact]
    public async Task ChunkedBodyOverBoundaryIsRejectedWithoutReadingPastSentinel()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = null;
        var stream = new ChunkedReadStream(new byte[128], 5);
        context.Request.Body = stream;

        (byte[] body, bool tooLarge) = await DeadLetterOperationsEndpointExtensions
            .ReadBoundedBodyAsync(context.Request, 16);

        tooLarge.ShouldBeTrue();
        body.ShouldBeEmpty();
        stream.BytesRead.ShouldBe(17);
    }

    /// <summary>Verifies the capture endpoint accepts an exact-boundary chunked envelope.</summary>
    [Fact]
    public async Task CaptureEndpointAcceptsChunkedEnvelopeAtExactBoundary()
    {
        (byte[] body, _) = StructuredCloudEventFixture.Create();
        var context = new DefaultHttpContext();
        context.Request.ContentLength = null;
        context.Request.Body = new ChunkedReadStream(body, 7);
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.CaptureAsync(Arg.Any<DeadLetterCaptureRequest>())
            .Returns(new DeadLetterCaptureResult(DeadLetterCaptureOutcome.Captured));
        IActorProxyFactory factory = Factory(actor);

        using ServiceProvider services = Metrics();

        IResult result = await DeadLetterOperationsEndpointExtensions.CaptureAsync(
            context.Request,
            factory,
            Options.Create(new EventStoreOperationsOptions { MaxBodyBytes = body.Length }),
            TimeProvider.System,
            Telemetry(services));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        _ = await actor.Received(1).CaptureAsync(Arg.Is<DeadLetterCaptureRequest>(request =>
            request.Body.SequenceEqual(body)));
    }

    /// <summary>
    /// Verifies a chunked oversize body is dropped before actor work and acknowledged rather than looped.
    /// </summary>
    /// <remarks>
    /// A body over the retention limit is over it on every redelivery. Because this topic has no dead-letter
    /// destination of its own, returning a non-2xx would make Dapr redeliver it forever while nothing is ever
    /// retained. The bounded oversize capture counter is the operator's signal instead.
    /// </remarks>
    [Fact]
    public async Task CaptureEndpointAcknowledgesChunkedOversizeBeforeActorInvocation()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = null;
        context.Request.Body = new ChunkedReadStream(new byte[128], 9);
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        using ServiceProvider services = Metrics();

        IResult result = await DeadLetterOperationsEndpointExtensions.CaptureAsync(
            context.Request,
            Factory(actor),
            Options.Create(new EventStoreOperationsOptions { MaxBodyBytes = 16 }),
            TimeProvider.System,
            Telemetry(services));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        _ = await actor.DidNotReceiveWithAnyArgs().CaptureAsync(default!);
    }

    /// <summary>Verifies a hash conflict is acknowledged, since redelivery can never resolve it.</summary>
    [Fact]
    public async Task CaptureEndpointAcknowledgesHashConflict()
    {
        (byte[] body, _) = StructuredCloudEventFixture.Create();
        var context = new DefaultHttpContext();
        context.Request.ContentLength = null;
        context.Request.Body = new ChunkedReadStream(body, 11);
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.CaptureAsync(Arg.Any<DeadLetterCaptureRequest>())
            .Returns(new DeadLetterCaptureResult(DeadLetterCaptureOutcome.HashConflict));
        using ServiceProvider services = Metrics();

        IResult result = await DeadLetterOperationsEndpointExtensions.CaptureAsync(
            context.Request,
            Factory(actor),
            Options.Create(new EventStoreOperationsOptions { MaxBodyBytes = body.Length }),
            TimeProvider.System,
            Telemetry(services));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    private static ServiceProvider Metrics() => new ServiceCollection().AddMetrics().BuildServiceProvider();

    private static EventStoreOperationsTelemetry Telemetry(ServiceProvider services)
        => new(
            services.GetRequiredService<IMeterFactory>(),
            TimeProvider.System,
            Options.Create(new EventStoreOperationsOptions()));

    private static IActorProxyFactory Factory(IDeadLetterDrainActor actor)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IDeadLetterDrainActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
        return factory;
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : Stream
    {
        private int _position;

        internal int BytesRead => _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => bytes.Length;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = Math.Min(Math.Min(count, chunkSize), bytes.Length - _position);
            bytes.AsSpan(_position, read).CopyTo(buffer.AsSpan(offset, read));
            _position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = Math.Min(Math.Min(buffer.Length, chunkSize), bytes.Length - _position);
            bytes.AsMemory(_position, read).CopyTo(buffer);
            _position += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
