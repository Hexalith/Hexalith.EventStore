using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Verifies serving built-in legacy seams stop at synchronous callback boundaries.</summary>
public sealed class BuiltInCancellationTests
{
    /// <summary>Checks cancellation never abandons an already-started legacy async handler.</summary>
    [Fact]
    public async Task LegacyAsyncHandlerCompletesBeforeAdapterObservesCancellation()
    {
        using var scope = new CancellationTestScope();
        var aggregate = new CancellationAsyncFixtureAggregate();
        Task<Hexalith.EventStore.Contracts.Results.DomainResult> processing = aggregate.ProcessAsync(Command(), null, scope.Cancellation.Token);
        processing.IsCompleted.ShouldBeFalse();
        aggregate.Completion.SetResult(Hexalith.EventStore.Contracts.Results.DomainResult.NoOp());
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => processing);
        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
    }

    /// <summary>Checks a started legacy task fault is awaited and observed after prelude cancellation.</summary>
    [Fact]
    public async Task LegacyAsyncHandlerFailureIsObservedAfterPreludeCancellation()
    {
        using var scope = new CancellationTestScope();
        var aggregate = new CancellationAsyncFixtureAggregate();
        Task<Hexalith.EventStore.Contracts.Results.DomainResult> processing = aggregate.ProcessAsync(Command(), null, scope.Cancellation.Token);
        processing.IsCompleted.ShouldBeFalse();
        var expected = new InvalidOperationException("Legacy task failed.");
        aggregate.Completion.SetException(expected);
        (await Should.ThrowAsync<InvalidOperationException>(() => processing)).ShouldBeSameAs(expected);
    }

    /// <summary>Checks a cancelled request invokes neither Apply nor Handle.</summary>
    [Fact]
    public async Task PreCancelledRequestStopsBuiltInReplayAndProcessing()
    {
        using var scope = new CancellationTestScope();
        scope.Cancellation.Cancel();
        var aggregate = new CancellationFixtureAggregate();
        var processor = new CancellationFixtureProcessor();

        OperationCanceledException replay = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ReplayAsync(ReplayRequest(), scope.Cancellation.Token));
        OperationCanceledException command = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), CurrentState(), scope.Cancellation.Token));
        OperationCanceledException custom = await Should.ThrowAsync<OperationCanceledException>(
            () => processor.ProcessAsync(Command(), CurrentState(), scope.Cancellation.Token));

        replay.CancellationToken.ShouldBe(scope.Cancellation.Token);
        command.CancellationToken.ShouldBe(scope.Cancellation.Token);
        custom.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(0);
        scope.Handled.ShouldBe(0);
    }

    /// <summary>Checks cancellation during Apply cannot produce Partial or invoke the next event.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReplayCancellationPreservesOriginalTokenAndStopsBeforeNextApply(bool throws, bool timeline)
    {
        using var scope = new CancellationTestScope { CancelAfterApply = 1, ThrowInApply = throws };
        var aggregate = new CancellationFixtureAggregate();
        AggregateReconstructionRequest request = ReplayRequest() with { IncludeTimeline = timeline };
        byte[][] original = request.Events.Select(item => item.Payload.ToArray()).ToArray();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ReplayAsync(request, scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(1);
        request.Events.Select(item => item.Payload).ShouldBe(original);
    }

    /// <summary>Checks every converter return path observes cancellation before Apply or a failure result.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ReplayConverterCancellationNeverReturnsAReconstructionResult(int outcome)
    {
        using var scope = new CancellationTestScope { ConverterCancellationOutcome = outcome };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ReplayAsync(ReplayRequest(), scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(0);
    }

    /// <summary>Checks cancellation while sealing successor state does not expose that state or run another Apply.</summary>
    [Fact]
    public async Task ReplayStateSerializationCancellationStopsAtTheSuccessorBoundary()
    {
        using var scope = new CancellationTestScope { CancelInStateGetter = true };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ReplayAsync(ReplayRequest(), scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(1);
    }

    /// <summary>Checks every supported legacy state shape stops between historical events before Handle.</summary>
    [Theory]
    [InlineData("snapshot", false)]
    [InlineData("snapshot", true)]
    [InlineData("json-snapshot", false)]
    [InlineData("json-snapshot", true)]
    [InlineData("enumerable", false)]
    [InlineData("enumerable", true)]
    [InlineData("json-array", false)]
    [InlineData("json-array", true)]
    public async Task CommandRehydrationCancellationStopsBeforeNextApplyAndHandler(string shape, bool throws)
    {
        using var scope = new CancellationTestScope { CancelAfterApply = 1, ThrowInApply = throws };
        var aggregate = new CancellationFixtureAggregate();
        object state = StateShape(shape);

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), state, scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(1);
        scope.Handled.ShouldBe(0);
    }

    /// <summary>Checks command deserialization cancellation cannot invoke Handle.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CommandConverterCancellationStopsBeforeHandler(int outcome)
    {
        using var scope = new CancellationTestScope { ConverterCancellationOutcome = outcome };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), null, scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Handled.ShouldBe(0);
    }

    /// <summary>Checks every historical converter exit preserves cancellation before Apply or Handle.</summary>
    [Theory]
    [InlineData("snapshot", 1)]
    [InlineData("snapshot", 2)]
    [InlineData("snapshot", 3)]
    [InlineData("snapshot", 4)]
    [InlineData("json-snapshot", 1)]
    [InlineData("json-snapshot", 2)]
    [InlineData("json-snapshot", 3)]
    [InlineData("json-snapshot", 4)]
    [InlineData("enumerable", 1)]
    [InlineData("enumerable", 2)]
    [InlineData("enumerable", 3)]
    [InlineData("enumerable", 4)]
    [InlineData("json-array", 1)]
    [InlineData("json-array", 2)]
    [InlineData("json-array", 3)]
    [InlineData("json-array", 4)]
    [InlineData("json-object-array", 1)]
    [InlineData("json-object-array", 2)]
    [InlineData("json-object-array", 3)]
    [InlineData("json-object-array", 4)]
    [InlineData("json-direct-array", 1)]
    [InlineData("json-direct-array", 2)]
    [InlineData("json-direct-array", 3)]
    [InlineData("json-direct-array", 4)]
    public async Task RehydrationConverterCancellationStopsBeforeApplyAndHandler(string shape, int outcome)
    {
        using var scope = new CancellationTestScope { ConverterCancellationOutcome = outcome };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), StateShape(shape), scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(0);
        scope.Handled.ShouldBe(0);
    }

    /// <summary>Checks cancellation in the termination getter takes precedence over a rejection result.</summary>
    [Fact]
    public async Task TerminationGetterCancellationCannotReturnARejection()
    {
        using var scope = new CancellationTestScope { CancelInTerminationGetter = true };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), new CancellationReplayState(), scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Handled.ShouldBe(0);
    }

    /// <summary>Checks Handle cancellation is distinct from both domain result and reflection failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousHandleCancellationPreservesOriginalToken(bool throws)
    {
        using var scope = new CancellationTestScope { CancelInHandle = true, ThrowInHandle = throws };
        var aggregate = new CancellationFixtureAggregate();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => aggregate.ProcessAsync(Command(), null, scope.Cancellation.Token));

        error.CancellationToken.ShouldBe(scope.Cancellation.Token);
        scope.Handled.ShouldBe(1);
    }

    /// <summary>Checks the protected async seam receives the unmodified request token.</summary>
    [Fact]
    public async Task DomainProcessorBaseForwardsOriginalTokenToProtectedHandler()
    {
        using var scope = new CancellationTestScope();
        IAsyncDomainProcessor processor = new CancellationFixtureProcessor();

        (await processor.ProcessAsync(Command(), CurrentState(), scope.Cancellation.Token)).IsNoOp.ShouldBeTrue();

        scope.ObservedHandlerToken.ShouldBe(scope.Cancellation.Token);
        scope.Applied.ShouldBe(2);
        scope.Handled.ShouldBe(1);
    }

    /// <summary>Checks the legacy entry points still process and replay without a request token.</summary>
    [Fact]
    public async Task ExistingSignaturesRemainUsable()
    {
        using var scope = new CancellationTestScope();
        IDomainProcessor aggregate = new CancellationFixtureAggregate();

        (await aggregate.ProcessAsync(Command(), CurrentState())).IsNoOp.ShouldBeTrue();
        AggregateReconstructionResult result = ((IAggregateReplay)aggregate).Replay(ReplayRequest());

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(2);
        scope.Applied.ShouldBe(4);
    }

    private static CommandEnvelope Command()
        => new("message", "tenant", "fixture", "aggregate", nameof(CancellationReplayEvent), "{}"u8.ToArray(), "correlation", null, "user", null);

    private static AggregateReconstructionRequest ReplayRequest()
        => new("tenant", "fixture", "CancellationFixture", "aggregate", 2,
            [ReplayEvent(1), ReplayEvent(2)], false, null);

    private static ReplayEventEnvelope ReplayEvent(long sequence)
        => new(sequence, nameof(CancellationReplayEvent), "{}"u8.ToArray(), "json", 1, $"message-{sequence}", "correlation", null);

    private static DomainServiceCurrentState CurrentState()
        => new(null, [ContractEvent(1), ContractEvent(2)], 0, 2);

    private static EventEnvelope ContractEvent(long sequence)
        => new(new EventMetadata($"message-{sequence}", "aggregate", "CancellationFixture", "tenant", "fixture", sequence, sequence,
            DateTimeOffset.UnixEpoch, "correlation", string.Empty, "user", "v1", nameof(CancellationReplayEvent), 1, "json"), "{}"u8.ToArray(), null);

    private static object StateShape(string shape)
        => shape switch
        {
            "snapshot" => CurrentState(),
            "json-snapshot" => JsonSerializer.SerializeToElement(CurrentState(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "enumerable" => new object[] { ContractEvent(1), ContractEvent(2) },
            "json-array" => JsonSerializer.SerializeToElement(new[]
            {
                new { eventTypeName = nameof(CancellationReplayEvent), payload = "{}"u8.ToArray() },
                new { eventTypeName = nameof(CancellationReplayEvent), payload = "{}"u8.ToArray() },
            }),
            "json-object-array" => JsonSerializer.SerializeToElement(new[]
            {
                new { eventTypeName = nameof(CancellationReplayEvent), payload = new { } },
                new { eventTypeName = nameof(CancellationReplayEvent), payload = new { } },
            }),
            "json-direct-array" => JsonSerializer.SerializeToElement(new[]
            {
                new { eventTypeName = nameof(CancellationReplayEvent) },
                new { eventTypeName = nameof(CancellationReplayEvent) },
            }),
            _ => throw new ArgumentException("Unknown fixture state shape.", nameof(shape)),
        };
}
