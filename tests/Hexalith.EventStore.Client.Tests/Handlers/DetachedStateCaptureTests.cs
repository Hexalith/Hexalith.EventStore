using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedStateCaptureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ApplyFailureOrCancellationMutatesOnlyDetachedState(bool cancel, bool aggregate)
    {
        using var probe = new DetachedSnapshotProbe();
        using var cancellation = new CancellationTokenSource();
        if (cancel) { probe.OnApply = cancellation.Cancel; }
        var snapshot = new DetachedSnapshotState { Value = 7 };
        EventEnvelope envelope = Event(new DetachedSnapshotEvent(3, Fail: !cancel));
        byte[] sourceBytes = envelope.Payload.ToArray();
        var current = new DomainServiceCurrentState(snapshot, [envelope], 1, 2);

        if (cancel)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => Process(aggregate, Capture(), current, cancellation.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            Exception error = await Should.ThrowAsync<Exception>(() => Process(aggregate, Capture(), current, cancellation.Token));
            error.ToString().ShouldContain("Injected Apply failure after mutation.");
        }

        snapshot.Value.ShouldBe(7);
        probe.AppliedState.ShouldNotBeSameAs(snapshot);
        probe.AppliedState!.Value.ShouldBe(10);
        probe.HandledState.ShouldBeNull();
        envelope.Payload.ShouldBe(sourceBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HandleFailureOrCancellationPreservesDirectTypedSource(bool cancel, bool aggregate)
    {
        using var probe = new DetachedSnapshotProbe { FailHandle = !cancel };
        using var cancellation = new CancellationTokenSource();
        if (cancel) { probe.OnHandle = cancellation.Cancel; }
        var snapshot = new DetachedSnapshotState { Value = 7 };
        if (cancel)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => Process(aggregate, Capture(), snapshot, cancellation.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            Exception error = await Should.ThrowAsync<Exception>(() => Process(aggregate, Capture(), snapshot, cancellation.Token));
            error.ToString().ShouldContain("Injected Handle failure after mutation.");
        }
        snapshot.Value.ShouldBe(7);
        probe.HandledState.ShouldNotBeSameAs(snapshot);
        probe.HandledState!.Value.ShouldBe(107);
    }

    [Fact]
    public async Task SnapshotIsCapturedBeforeCountAndEnumerationMutateSource()
    {
        using var probe = new DetachedSnapshotProbe();
        var snapshot = new DetachedSnapshotState { Value = 7 };
        var tail = new DetachedSnapshotTail(Event(new DetachedSnapshotEvent(3)), () => snapshot.Value = 40, () => snapshot.Value = 80);
        var nested = new DomainServiceCurrentState(snapshot, [], 0, 1);
        var current = new DomainServiceCurrentState(nested, tail, 1, 2);
        _ = await new DetachedSnapshotProcessor(Capture()).ProcessAsync(Command(), current);
        probe.CaptureCalls.ShouldBe(1);
        snapshot.Value.ShouldBe(80); // Caller callbacks caused this; replay uses its earlier detached value.
        probe.AppliedState!.Value.ShouldBe(110);
        probe.HandledState.ShouldBeSameAs(probe.AppliedState);
        probe.HandledState.ShouldNotBeSameAs(snapshot);
    }

    [Fact]
    public async Task CaptureCancellationPreservesOriginalTokenAndSkipsTailAndHandle()
    {
        using var probe = new DetachedSnapshotProbe();
        using var cancellation = new CancellationTokenSource();
        var snapshot = new DetachedSnapshotState { Value = 7 };
        var tail = new DetachedSnapshotTail(Event(new DetachedSnapshotEvent(3)),
            () => throw new InvalidOperationException("Forbidden Count."), () => throw new InvalidOperationException("Forbidden enumeration."));
        var capture = new DetachedStateCapture<DetachedSnapshotState>(256, (state, token) => {
            token.ShouldBe(cancellation.Token);
            cancellation.Cancel();
            return new DetachedSnapshotState { Value = state.Value };
        });
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            new DetachedSnapshotProcessor(capture).ProcessAsync(Command(), new DomainServiceCurrentState(snapshot, tail, 1, 2), cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        snapshot.Value.ShouldBe(7);
        probe.AppliedState.ShouldBeNull();
        probe.HandledState.ShouldBeNull();
    }

    [Fact]
    public async Task AliasedCaptureRefusesBeforeTailAndHandle()
    {
        using var probe = new DetachedSnapshotProbe();
        var snapshot = new DetachedSnapshotState { Value = 7 };
        var capture = new DetachedStateCapture<DetachedSnapshotState>(256, static (state, _) => state);
        var tail = new DetachedSnapshotTail(Event(new DetachedSnapshotEvent(3)),
            () => throw new InvalidOperationException("Forbidden Count."), () => throw new InvalidOperationException("Forbidden enumeration."));
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            new DetachedSnapshotProcessor(capture).ProcessAsync(Command(), new DomainServiceCurrentState(snapshot, tail, 1, 2)));
        error.Message.ShouldStartWith("DetachedStateInvalid:");
        probe.HandledState.ShouldBeNull();
        snapshot.Value.ShouldBe(7);
    }

    [Fact]
    public async Task WholeGraphChargeIsAdmittedBeforeCaptureCallback()
    {
        using var probe = new DetachedSnapshotProbe();
        var snapshot = new DetachedSnapshotState { Value = 7 };
        var capture = new DetachedStateCapture<DetachedSnapshotState>(256L * 1024 * 1024,
            static (_, _) => throw new InvalidOperationException("Forbidden capture."));
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            new DetachedSnapshotProcessor(capture).ProcessAsync(Command(), new DomainServiceCurrentState(snapshot, [], 1, 1)));
        error.Message.ShouldStartWith("LegacyArrayLimit:"); // Wrapper charge leaves less than the declared graph bound.
        probe.HandledState.ShouldBeNull();
    }

    [Fact]
    public async Task UndeclaredLegacyStateRetainsReferenceSemantics()
    {
        using var probe = new DetachedSnapshotProbe();
        var snapshot = new DetachedSnapshotState { Value = 7 };
        _ = await new DetachedSnapshotProcessor(null).ProcessAsync(Command(), snapshot);
        probe.HandledState.ShouldBeSameAs(snapshot);
        snapshot.Value.ShouldBe(107);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclarationGetterCancellationAndFailurePreservesOriginatingToken(bool aggregate)
    {
        using var probe = new DetachedSnapshotProbe();
        using var cancellation = new CancellationTokenSource();
        probe.OnDeclaration = () => { cancellation.Cancel(); throw new InvalidOperationException("Injected declaration getter failure."); };
        var snapshot = new DetachedSnapshotState { Value = 7 };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => Process(aggregate, Capture(), snapshot, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        probe.CaptureCalls.ShouldBe(0);
        probe.HandledState.ShouldBeNull();
        snapshot.Value.ShouldBe(7);
    }

    private static DetachedStateCapture<DetachedSnapshotState> Capture() => new(256, static (state, token) => {
        token.ThrowIfCancellationRequested();
        DetachedSnapshotProbe.Current!.CaptureCalls++;
        return new DetachedSnapshotState { Value = state.Value };
    });

    private static Task<Hexalith.EventStore.Contracts.Results.DomainResult> Process(bool aggregate,
        DetachedStateCapture<DetachedSnapshotState>? capture, object state, CancellationToken cancellationToken = default)
        => aggregate ? new DetachedSnapshotAggregate<int>(capture).ProcessAsync(Command(), state, cancellationToken)
            : new DetachedSnapshotProcessor(capture).ProcessAsync(Command(), state, cancellationToken);

    private static CommandEnvelope Command() => new("command-id", "tenant", "domain", "aggregate", nameof(DetachedSnapshotCommand), "{}"u8.ToArray(), "correlation", null, "user", null);
    private static EventEnvelope Event(DetachedSnapshotEvent payload) => new(new EventMetadata("message", "aggregate", "snapshot", "tenant", "domain", 2, 2,
        DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", nameof(DetachedSnapshotEvent), 1, "json"), JsonSerializer.SerializeToUtf8Bytes(payload), null);
}
