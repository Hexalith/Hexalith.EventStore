using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Failure classification, byte bounds, and per-envelope progress cases.</summary>
public sealed class ProcessRunnerEdgeTests
{
    private static readonly ProcessSource Source = new("tenant-a", "sample", "runner-edge");

    /// <summary>Stable gateway corruption is invalid source evidence, even without a returned page.</summary>
    [Theory]
    [InlineData(StreamReplayReasonCodes.MissingEvent, false)]
    [InlineData(StreamReplayReasonCodes.CorruptEvent, false)]
    [InlineData(StreamReplayReasonCodes.MissingEvent, true)]
    [InlineData(StreamReplayReasonCodes.CorruptEvent, true)]
    public async Task GatewayCorruptionQuarantinesWithUnknownPosition(string reason, bool codeOnly)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(codeOnly
                ? new EventStoreGatewayException(409, "synthetic corruption", code: reason)
                : new EventStoreGatewayException(409, "synthetic corruption", reasonCode: reason)));

        ProcessRunResult result = await Runner(store, options, gateway, Translator([]), Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.ReasonCode.ShouldBe("invalid-evidence");
        persisted.FailureSequence.ShouldBeNull();
    }

    /// <summary>Malformed gateway JSON is invalid source evidence, not a transient read outage.</summary>
    [Fact]
    public async Task MalformedGatewayJsonQuarantinesWithoutCheckpoint()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new JsonException("Malformed synthetic gateway response.")));

        ProcessRunResult result = await Runner(store, options, gateway, Translator([]), Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.FailureSequence.ShouldBeNull();
        persisted.ReasonCode.ShouldBe("invalid-evidence");
    }

    /// <summary>Translator read-pool admission failure leaves a durable unresolved source at its old checkpoint.</summary>
    [Fact]
    public async Task TranslatorAdmissionFailurePersistsRetryEvidence()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<IReadOnlyList<ProcessEffect>>(new ProcessReadAdmissionException()));

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), translator, Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Retrying);
        result.ReasonCode.ShouldBe("read-worker-exhausted");
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.FailureSequence.ShouldBe(1);
    }

    /// <summary>Read outages retain a known failed envelope so alternating failures exhaust one retry budget.</summary>
    [Fact]
    public async Task AlternatingReadAndEnvelopeFailuresReachParked()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { RetryLimit = 3 };
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<IReadOnlyList<ProcessEffect>>(new TimeoutException("Synthetic translation outage.")));
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        int reads = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++reads == 2
                ? Task.FromException<StreamReadPage>(new TimeoutException("Synthetic read outage."))
                : Task.FromResult(OneEventPage()));
        CheckpointedProcessRunner runner = Runner(store, options, gateway, translator, Delegation(), Submitter());

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);

        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.Attempts.ShouldBe(3);
        persisted.FailureSequence.ShouldBe(1);
    }

    /// <summary>An unknown receipt enum is invalid target evidence even when its effect ID matches.</summary>
    [Fact]
    public async Task UndefinedReceiptDispositionQuarantinesWithoutCheckpoint()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
                return Task.FromResult(new TrustedEffectResult(EffectIdentityCodec.ComputeEffectId(submission.Identity),
                    (TrustedEffectDisposition)999, false, null));
            });

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), Delegation(), submitter).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        result.Checkpoint.ShouldBe(0);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).ReasonCode.ShouldBe("invalid-receipt");
    }

    /// <summary>A lease-release store fault cannot turn a committed checkpoint into a failed run.</summary>
    [Fact]
    public async Task ReleaseFailureDoesNotReplaceSuccessfulRunResult()
    {
        InMemoryReadModelStore durable = new();
        EventStoreProcessOptions options = new();
        FaultInjectingProcessStore fault = new(durable, (key, value) => key == ProcessStateKeys.Source(Source)
            && value is ProcessSourceState { Owner: null, Checkpoint: 1 });
        ProcessCheckpointStore checkpoints = new(fault, Options.Create(options), TimeProvider.System);
        CheckpointedProcessRunner runner = new(Gateway(OneEventPage()), Translator([]), Delegation(), Submitter(), checkpoints, Options.Create(options));

        ProcessRunResult result = await runner.RunAsync(Source);

        fault.Fired.ShouldBeTrue();
        result.Status.ShouldBe(ProcessSourceStatus.Active);
        result.Checkpoint.ShouldBe(1);
        (await new ProcessCheckpointStore(durable, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A repeated translation failure at the same validated head consumes the hot retry budget.</summary>
    [Fact]
    public async Task SameHeadTranslationFailuresReachParked()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { RetryLimit = 3 };
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<IReadOnlyList<ProcessEffect>>(new TimeoutException("Synthetic repeat failure.")));
        CheckpointedProcessRunner runner = Runner(store, options, Gateway(OneEventPage()), translator, Delegation(), Submitter());

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);

        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.Attempts.ShouldBe(3);
        persisted.FailureSequence.ShouldBe(1);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);
        await translator.Received(3).TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Delegation and target outages at one envelope also consume the same durable retry budget.</summary>
    [Theory]
    [InlineData("delegation")]
    [InlineData("target")]
    public async Task SameHeadEffectFailuresReachParked(string failure)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { RetryLimit = 3 };
        IProcessDelegationProvider delegation = Delegation();
        ITrustedEffectSubmitter submitter = Submitter();
        if (failure == "delegation")
            delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<TrustedEffectContext>(new TimeoutException("Synthetic delegation outage.")));
        else
            submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<TrustedEffectResult>(new TimeoutException("Synthetic target outage.")));
        CheckpointedProcessRunner runner = Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), delegation, submitter);

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);

        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Attempts.ShouldBe(3);
        persisted.FailureSequence.ShouldBe(1);
        persisted.Checkpoint.ShouldBe(0);
    }

    /// <summary>An empty head cannot erase unresolved evidence for an envelope already seen at this checkpoint.</summary>
    [Fact]
    public async Task EmptyHeadAfterEnvelopeFailureQuarantinesMissingEvidence()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IProcessTranslator failing = Substitute.For<IProcessTranslator>();
        failing.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<IReadOnlyList<ProcessEffect>>(new TimeoutException("Synthetic translation outage.")));
        (await Runner(store, options, Gateway(OneEventPage()), failing, Delegation(), Submitter()).RunAsync(Source))
            .Status.ShouldBe(ProcessSourceStatus.Retrying);
        StreamReadPage empty = new(Source.Tenant, Source.Domain, Source.Aggregate, [],
            new StreamReadMetadata(0, null, null, 0, 0, false, null));

        ProcessRunResult result = await Runner(store, options, Gateway(empty), Translator([]), Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.FailureSequence.ShouldBe(1);
    }

    /// <summary>A blocking adapter is isolated before it returns its task.</summary>
    [Fact]
    public async Task SynchronouslyBlockingTranslatorCannotHoldSourceDeadline()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            SourceServiceQuantum = TimeSpan.FromMilliseconds(800),
            MaxEffectsPerEnvelope = 1,
        };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                release.Task.GetAwaiter().GetResult();
                return new ValueTask<IReadOnlyList<ProcessEffect>>([]);
            });

        try
        {
            ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), translator, Delegation(), Submitter())
                .RunAsync(Source).WaitAsync(TimeSpan.FromSeconds(2));
            result.Status.ShouldBe(ProcessSourceStatus.Retrying);
            (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(0);
        }
        finally
        {
            release.SetResult();
        }
    }

    /// <summary>Moving wall time backwards does not extend the source service quantum.</summary>
    [Fact]
    public async Task BackwardWallClockMoveDoesNotExtendSourceQuantum()
    {
        InMemoryReadModelStore store = new();
        ManualProcessTimeProvider time = new(DateTimeOffset.UtcNow);
        EventStoreProcessOptions options = new()
        {
            OperationTimeout = TimeSpan.FromMilliseconds(200),
            SourceServiceQuantum = TimeSpan.FromMilliseconds(1600),
            MaxEffectsPerEnvelope = 1,
        };
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                time.MoveWallClock(TimeSpan.FromHours(-1));
                time.Advance(TimeSpan.FromSeconds(2));
                return new ValueTask<IReadOnlyList<ProcessEffect>>([]);
            });
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), time);
        CheckpointedProcessRunner runner = new(Gateway(TwoEventPage()), translator, Delegation(), Submitter(), checkpoints, Options.Create(options), time);

        ProcessRunResult result = await runner.RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Incomplete);
        result.Checkpoint.ShouldBe(1);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A target sees durable incomplete coverage, including on the first effect of an active source.</summary>
    [Fact]
    public async Task FirstEffectSeesReadinessInvalidated()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(Source);
        ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
        ProcessSweepState acquired = (await sweeps.AcquireAsync()).ShouldNotBeNull();
        ProcessSweepState completed = await sweeps.UpdateOwnedAsync(acquired, state => state with
        {
            Complete = true,
            CatalogComplete = true,
            CandidatesComplete = true,
            LastCompleteAt = DateTimeOffset.UtcNow,
        });
        await sweeps.ReleaseAsync(completed);
        (await new ProcessReadinessHealthCheck(sweeps, Options.Create(options), TimeProvider.System)
            .CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);

        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            ProcessSourceState source = store.Snapshot<ProcessSourceState>(options.StateStoreName, ProcessStateKeys.Source(Source)).ShouldNotBeNull();
            source.Status.ShouldBe(ProcessSourceStatus.Incomplete);
            ProcessSweepState sweep = store.Snapshot<ProcessSweepState>(options.StateStoreName, ProcessStateKeys.Sweep).ShouldNotBeNull();
            sweep.Complete.ShouldBeFalse();
            sweep.PendingTransitions.ShouldContainKey(ProcessStateKeys.SourceId(Source));
            TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
            return Task.FromResult(new TrustedEffectResult(EffectIdentityCodec.ComputeEffectId(submission.Identity), TrustedEffectDisposition.Success, false, null));
        });

        (await Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), Delegation(), submitter).RunAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A recovered nonempty head starts a fresh hot retry budget.</summary>
    [Fact]
    public async Task ValidatedHeadResetsPriorRetryAttempts()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { RetryLimit = 3 };
        IEventStoreGatewayClient failed = Substitute.For<IEventStoreGatewayClient>();
        failed.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("synthetic outage")));
        CheckpointedProcessRunner retry = Runner(store, options, failed, Translator([]), Delegation(), Submitter());
        (await retry.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await retry.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);

        IProcessTranslator timedOut = Substitute.For<IProcessTranslator>();
        timedOut.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>(Task.FromException<IReadOnlyList<ProcessEffect>>(new TimeoutException("synthetic"))));
        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), timedOut, Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Attempts.ShouldBe(1);
    }

    /// <summary>A changed retry budget parks persisted exhausted work before a new source read.</summary>
    [Fact]
    public async Task LoweredRetryLimitParksPersistedRetryingSourceBeforeRead()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions oldOptions = new() { RetryLimit = 3 };
        IEventStoreGatewayClient failedGateway = Substitute.For<IEventStoreGatewayClient>();
        failedGateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("synthetic outage")));
        CheckpointedProcessRunner oldRunner = Runner(store, oldOptions, failedGateway, Translator([]), Delegation(), Submitter());
        (await oldRunner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await oldRunner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);

        EventStoreProcessOptions lowered = new() { RetryLimit = 2 };
        IEventStoreGatewayClient newGateway = Gateway(OneEventPage());
        ProcessRunResult result = await Runner(store, lowered, newGateway, Translator([]), Delegation(), Submitter()).RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Parked);
        result.Checkpoint.ShouldBe(0);
        (await new ProcessCheckpointStore(store, Options.Create(lowered), TimeProvider.System).GetAsync(Source)).Attempts.ShouldBe(3);
        _ = newGateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A crash after marking an envelope incomplete does not bypass a lowered retry limit.</summary>
    [Fact]
    public async Task LoweredRetryLimitParksPersistedIncompleteSourceBeforeRead()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { RetryLimit = 2 };
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(Source),
            new ProcessSourceState(1, Source, 0, ProcessSourceStatus.Incomplete, 2, 1, null,
                null, null, 0, 0, DateTimeOffset.UtcNow));
        IEventStoreGatewayClient gateway = Gateway(OneEventPage());

        ProcessRunResult result = await Runner(store, options, gateway, Translator([]), Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Parked);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(0);
        _ = gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A translator deadline is an operational retry, not evidence of an unknown source event.</summary>
    [Fact]
    public async Task TranslationTimeoutRetriesWithoutQuarantine()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromMilliseconds(100) };
        TaskCompletionSource<IReadOnlyList<ProcessEffect>> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<IReadOnlyList<ProcessEffect>>(held.Task));

        try
        {
            ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), translator, Delegation(), Submitter()).RunAsync(Source);
            result.Status.ShouldBe(ProcessSourceStatus.Retrying);
            result.Checkpoint.ShouldBe(0);
            (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).ReasonCode.ShouldBe("translation-timeout");
        }
        finally
        {
            held.TrySetResult([]);
        }
    }

    /// <summary>Caller cancellation during pure translation or target submission does not consume retry budget.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledRunDoesNotRecordOperationalFailure(bool duringSubmission)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(Source);
        ProcessSourceState initial = new(1, Source, 0, ProcessSourceStatus.Incomplete, 0, null, null,
            null, null, 0, 0, DateTimeOffset.UtcNow);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(Source), initial);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => duringSubmission
                ? new ValueTask<IReadOnlyList<ProcessEffect>>([Effect([1])])
                : new ValueTask<IReadOnlyList<ProcessEffect>>(WaitForTranslationCancellationAsync(call.Arg<CancellationToken>(), entered)));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForSubmissionCancellationAsync(call.Arg<CancellationToken>(), entered));
        CheckpointedProcessRunner runner = new(Gateway(OneEventPage()), translator, Delegation(), submitter,
            checkpoints, Options.Create(options));
        using CancellationTokenSource cancellation = new();

        Task<ProcessRunResult> running = runner.RunAsync(Source, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => running);

        ProcessSourceState after = await checkpoints.GetAsync(Source);
        after.Checkpoint.ShouldBe(initial.Checkpoint);
        after.Status.ShouldBe(initial.Status);
        after.Attempts.ShouldBe(initial.Attempts);
        after.ReasonCode.ShouldBe(initial.ReasonCode);
        after.Owner.ShouldBeNull();
        after.LeaseUntil.ShouldBeNull();
        ProcessSourceState reacquired = (await checkpoints.AcquireAsync(Source)).ShouldNotBeNull();
        reacquired.Fence.ShouldBeGreaterThan(after.Fence);
        await checkpoints.ReleaseAsync(reacquired);
        (await Runner(store, options, Gateway(OneEventPage()), Translator([]), Delegation(), Submitter()).RunAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A committed effect survives caller cancellation and replays under its original identity.</summary>
    [Fact]
    public async Task CancelledSubmissionAfterCommitReplaysOneDurableEffect()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        TaskCompletionSource committed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HashSet<string> durable = [];
        List<string> submitted = [];
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = EffectIdentityCodec.ComputeEffectId(call.Arg<TrustedEffectSubmission>().Identity);
                submitted.Add(id);
                bool replayed = !durable.Add(id);
                if (!replayed) return WaitForSubmissionCancellationAsync(call.Arg<CancellationToken>(), committed);
                return Task.FromResult(new TrustedEffectResult(id, TrustedEffectDisposition.Success, true, null));
            });
        CheckpointedProcessRunner runner = new(Gateway(OneEventPage()), Translator([Effect([1])]), Delegation(), submitter,
            checkpoints, Options.Create(options));
        using CancellationTokenSource cancellation = new();

        Task<ProcessRunResult> first = runner.RunAsync(Source, cancellation.Token);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);

        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        submitted.Count.ShouldBe(2);
        submitted[1].ShouldBe(submitted[0]);
        durable.Count.ShouldBe(1);
    }

    /// <summary>A delegation provider conflict is retryable and must never be labeled a target conflict.</summary>
    [Fact]
    public async Task DelegationConflictRetriesBeforeTargetSubmission()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TrustedEffectContext>(new HttpRequestException("synthetic delegation conflict", null, HttpStatusCode.Conflict)));
        ITrustedEffectSubmitter submitter = Submitter();

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), delegation, submitter).RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).ReasonCode.ShouldBe("delegation-unavailable");
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A missing delegation is an unavailable credential and never reaches the target.</summary>
    [Fact]
    public async Task NullDelegationRetriesBeforeTargetSubmission()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TrustedEffectContext>(null!));
        ITrustedEffectSubmitter submitter = Submitter();

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), delegation, submitter).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Retrying);
        result.Checkpoint.ShouldBe(0);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).ReasonCode.ShouldBe("delegation-unavailable");
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A delegation for another cause cannot submit this envelope and remains recoverable.</summary>
    [Theory]
    [InlineData("other-message")]
    [InlineData("")]
    [InlineData(null)]
    public async Task MismatchedDelegationCausationRetriesBeforeSubmission(string? causation)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TrustedEffectContext("synthetic", "process", causation!, "token")));
        ITrustedEffectSubmitter submitter = Submitter();
        CheckpointedProcessRunner runner = Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), delegation, submitter);
        ProcessRunResult result = await runner.RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Retrying);
        result.Checkpoint.ShouldBe(0);
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Attempts.ShouldBe(1);
        persisted.ReasonCode.ShouldBe("delegation-unavailable");
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new TrustedEffectContext("synthetic", "process", call.ArgAt<string>(1), "token")));
        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Active);
    }

    /// <summary>A null receipt is invalid target evidence and cannot advance the checkpoint.</summary>
    [Fact]
    public async Task NullReceiptQuarantinesWithoutCheckpoint()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TrustedEffectResult>(null!));

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), Delegation(), submitter).RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        result.Checkpoint.ShouldBe(0);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).ReasonCode.ShouldBe("invalid-receipt");
    }

    /// <summary>The command name is part of the translated byte budget.</summary>
    [Fact]
    public async Task OversizedEncodedCommandTypeQuarantinesBeforeSubmission()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { MaxCommandBytesPerEnvelope = 8, MaxCommandBytesPerPage = 8 };
        ITrustedEffectSubmitter submitter = Submitter();
        ProcessEffect effect = Effect([]) with { CommandType = "ééééé" };

        ProcessRunResult result = await Runner(store, options, Gateway(OneEventPage()), Translator([effect]), Delegation(), submitter).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        result.Checkpoint.ShouldBe(0);
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The complete serialized envelope, including metadata, is bounded before translation.</summary>
    [Fact]
    public async Task OversizedEnvelopeMetadataQuarantinesBeforeTranslation()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { MaxEventBytes = 256, MaxPageBytes = 1024 };
        StreamReadEvent oversized = new(1, new string('E', 512), [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null);
        StreamReadPage page = new(Source.Tenant, Source.Domain, Source.Aggregate, [oversized],
            new StreamReadMetadata(0, null, 1, 1, 1, false, null));
        IProcessTranslator translator = Translator([]);

        ProcessRunResult result = await Runner(store, options, Gateway(page), translator, Delegation(), Submitter()).RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        result.Checkpoint.ShouldBe(0);
        _ = translator.DidNotReceive().TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The target command and delegation causation come from the translated command and source envelope.</summary>
    [Fact]
    public async Task SubmissionUsesTranslatedCommandTypeAndSourceMessageCausation()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessEffect effect = Effect([1]) with { CommandType = "ApplySynthetic" };
        IProcessDelegationProvider delegation = Delegation();
        ITrustedEffectSubmitter submitter = Submitter();

        (await Runner(store, options, Gateway(OneEventPage()), Translator([effect]), delegation, submitter).RunAsync(Source)).Checkpoint.ShouldBe(1);

        await delegation.Received(1).CreateAsync(
            Arg.Is<TrustedEffectSubmission>(submission => submission.CommandType == "ApplySynthetic"),
            "message-1", Arg.Any<CancellationToken>());
        await submitter.Received(1).SubmitAsync(
            Arg.Is<TrustedEffectSubmission>(submission => submission.CommandType == "ApplySynthetic"),
            Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Command-byte admission requests a smaller page and makes valid work durable.</summary>
    [Fact]
    public async Task CumulativeCommandBytesYieldBeforeSecondValidEnvelope()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { MaxCommandBytesPerEnvelope = 5, MaxCommandBytesPerPage = 6 };
        ITrustedEffectSubmitter submitter = Submitter();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            request.PageSize.ShouldBe(1);
            long next = request.FromSequence + 1;
            StreamReadEvent[] events = [new(next, "SyntheticEvent", [123], "json", 1, $"message-{next}", null, null, DateTimeOffset.UtcNow, null)];
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(request.FromSequence, null, next, 2, 1, next < 2, null)));
        });
        CheckpointedProcessRunner runner = Runner(store, options, gateway, Translator([Effect([1, 2, 3])]), Delegation(), submitter);
        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        ProcessRunResult result = await runner.RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Active);
        result.Checkpoint.ShouldBe(2);
        ProcessSourceState persisted = await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source);
        persisted.Checkpoint.ShouldBe(2);
        persisted.ReasonCode.ShouldBeNull();
        await submitter.Received(2).SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The source-byte budget caps the gateway request independently of command-byte admission.</summary>
    [Fact]
    public async Task SourceByteBudgetRequestsOneEnvelopeAndAdvancesAcrossPages()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            MaxEventBytes = 1024,
            MaxPageBytes = 1100,
            MaxCommandBytesPerEnvelope = 1,
            MaxCommandBytesPerPage = 1024,
        };
        var cursors = new List<long>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            request.PageSize.ShouldBe(1);
            cursors.Add(request.FromSequence);
            long next = request.FromSequence + 1;
            StreamReadEvent[] events = [new(next, "SyntheticEvent", [1, 2, 3], "json", 1,
                $"message-{next}", null, null, DateTimeOffset.UtcNow, null)];
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(request.FromSequence, null, next, 2, 1, next < 2, null)));
        });
        CheckpointedProcessRunner runner = Runner(store, options, gateway, Translator([]), Delegation(), Submitter());

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        ProcessRunResult final = await runner.RunAsync(Source);

        final.Status.ShouldBe(ProcessSourceStatus.Active);
        final.Checkpoint.ShouldBe(2);
        cursors.ShouldBe([0L, 1L]);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(2);
    }

    /// <summary>A slow delegation cannot submit with the source lease close to expiry.</summary>
    [Fact]
    public async Task DelegationRenewsSourceLeaseImmediatelyBeforeTargetSubmission()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromMilliseconds(300),
        };
        options.Validate().ShouldBeEmpty();
        ManualProcessTimeProvider clock = new(DateTimeOffset.UtcNow);
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), clock);
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(240));
            return Task.FromResult(new TrustedEffectContext("synthetic", "process", call.ArgAt<string>(1), "token"));
        });
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            ProcessSourceState state = store.Snapshot<ProcessSourceState>(options.StateStoreName, ProcessStateKeys.Source(Source)).ShouldNotBeNull();
            (state.LeaseUntil.ShouldNotBeNull() - clock.GetUtcNow()).ShouldBeGreaterThan(TimeSpan.FromMilliseconds(100));
            TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
            clock.Advance(TimeSpan.FromMilliseconds(270));
            return Task.FromResult(new TrustedEffectResult(EffectIdentityCodec.ComputeEffectId(submission.Identity), TrustedEffectDisposition.Success, false, null));
        });
        CheckpointedProcessRunner runner = new(Gateway(OneEventPage()), Translator([Effect([1])]), delegation, submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        await submitter.Received(1).SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A target that completes after its deadline cannot advance the source; replay uses the same effect identity.</summary>
    [Fact]
    public async Task LateTargetCompletionLeavesCheckpointForDeterministicReplay()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromMilliseconds(150) };
        TaskCompletionSource<TrustedEffectResult> late = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> effectIds = [];
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string effectId = EffectIdentityCodec.ComputeEffectId(call.Arg<TrustedEffectSubmission>().Identity);
                effectIds.Add(effectId);
                return effectIds.Count == 1
                    ? late.Task
                    : Task.FromResult(new TrustedEffectResult(effectId, TrustedEffectDisposition.Success, true, null));
            });
        CheckpointedProcessRunner runner = Runner(store, options, Gateway(OneEventPage()), Translator([Effect([1])]), Delegation(), submitter);

        ProcessRunResult first = await runner.RunAsync(Source);
        first.Status.ShouldBe(ProcessSourceStatus.Retrying);
        first.ReasonCode.ShouldBe("effect-unknown");
        first.Checkpoint.ShouldBe(0);
        late.SetResult(new TrustedEffectResult(effectIds[0], TrustedEffectDisposition.Success, false, null));
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(0);

        ProcessRunResult replay = await runner.RunAsync(Source);
        replay.Checkpoint.ShouldBe(1);
        replay.Status.ShouldBe(ProcessSourceStatus.Active);
        effectIds.Count.ShouldBe(2);
        effectIds[1].ShouldBe(effectIds[0]);
    }

    /// <summary>Zero freshness slack cannot admit any supported catalog.</summary>
    [Fact]
    public void EqualReadinessAgeAndIntervalIsRejected()
    {
        EventStoreProcessOptions options = new()
        {
            ReconciliationInterval = TimeSpan.FromSeconds(30),
            ReadinessMaxAge = TimeSpan.FromSeconds(30),
        };

        options.Validate().ShouldContain(error => error.Contains("ReadinessMaxAge", StringComparison.Ordinal));
    }

    /// <summary>Two valid translations may exceed one operation timeout in total while each envelope still commits.</summary>
    [Fact]
    public async Task MultiEnvelopeTranslationMakesDurableProgressAcrossOperationDeadlines()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromMilliseconds(500), PageSize = 2 };
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IReadOnlyList<ProcessEffect>>(DelayedEmptyAsync(call.Arg<CancellationToken>())));

        ProcessRunResult result = await Runner(store, options, Gateway(TwoEventPage()), translator, Delegation(), Submitter()).RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Active);
        result.Checkpoint.ShouldBe(2);
        result.Processed.ShouldBe(2);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(2);
    }

    /// <summary>A finite source service quantum yields only after a durable checkpoint, then resumes the remainder.</summary>
    [Fact]
    public async Task SourceServiceQuantumYieldsAndResumesAfterCheckpoint()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new()
        {
            OperationTimeout = TimeSpan.FromMilliseconds(500),
            SourceServiceQuantum = TimeSpan.FromMilliseconds(4500),
            MaxEffectsPerEnvelope = 1,
            MaxEnvelopesPerRun = 20,
            PageSize = 20,
            MaxCommandBytesPerEnvelope = 32 * 1024,
        };
        options.Validate().ShouldBeEmpty();
        var cursors = new List<long>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            cursors.Add(request.FromSequence);
            StreamReadEvent[] events = Enumerable.Range(checked((int)request.FromSequence + 1), 20 - (int)request.FromSequence)
                .Select(sequence => new StreamReadEvent(sequence, "SyntheticEvent", [123], "json", 1,
                    $"message-{sequence}", null, null, DateTimeOffset.UtcNow, null)).ToArray();
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(request.FromSequence, null, 20, 20, events.Length, false, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IReadOnlyList<ProcessEffect>>(DelayedEmptyAsync(call.Arg<CancellationToken>())));
        CheckpointedProcessRunner runner = Runner(store, options, gateway, translator, Delegation(), Submitter());

        ProcessRunResult first = await runner.RunAsync(Source);
        first.Status.ShouldBe(ProcessSourceStatus.Incomplete);
        first.Checkpoint.ShouldBeGreaterThan(0);
        first.Checkpoint.ShouldBeLessThan(20);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(first.Checkpoint);

        ProcessRunResult resumed = await runner.RunAsync(Source);
        for (int pass = 0; pass < 3 && resumed.Checkpoint < 20; pass++)
            resumed = await runner.RunAsync(Source);

        resumed.Checkpoint.ShouldBe(20);
        resumed.Status.ShouldBe(ProcessSourceStatus.Active);
        cursors[0].ShouldBe(0);
        cursors[1].ShouldBe(first.Checkpoint);
        (await new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System).GetAsync(Source)).Checkpoint.ShouldBe(20);
    }

    /// <summary>A real plan-persist/cancel crash cannot become healthy when the authoritative source disappears.</summary>
    [Fact]
    public async Task EmptyHeadQuarantinesAuthenticPendingPlanAfterCancellation()
    {
        var store = new InMemoryReadModelStore();
        var options = new EventStoreProcessOptions();
        using var cancellation = new CancellationTokenSource();
        var delegation = Delegation();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<TrustedEffectContext>(cancellation.Token);
        });
        var submitter = Submitter();
        await Should.ThrowAsync<OperationCanceledException>(() => Runner(store, options, Gateway(OneEventPage()),
            Translator([Effect([1])]), delegation, submitter).RunAsync(Source, cancellation.Token));
        var checkpoints = new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System);
        ProcessSourceState before = await checkpoints.GetAsync(Source);
        before.PendingPlan.ShouldNotBeNull().Sequence.ShouldBe(1);
        before.FailureSequence.ShouldBeNull();
        var empty = new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, [],
            new StreamReadMetadata(0, null, null, 0, 0, false, null));
        ProcessRunResult result = await Runner(store, options, Gateway(empty), Translator([]), Delegation(), submitter).RunAsync(Source);
        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState after = await checkpoints.GetAsync(Source);
        after.Checkpoint.ShouldBe(0);
        JsonSerializer.Serialize(after.PendingPlan).ShouldBe(JsonSerializer.Serialize(before.PendingPlan));
        after.ReasonCode.ShouldBe("invalid-evidence");
        after.FailureSequence.ShouldBe(1);
        submitter.ReceivedCalls().ShouldBeEmpty();
    }

    private static CheckpointedProcessRunner Runner(InMemoryReadModelStore store, EventStoreProcessOptions options,
        IEventStoreGatewayClient gateway, IProcessTranslator translator, IProcessDelegationProvider delegation, ITrustedEffectSubmitter submitter)
        => new(gateway, translator, delegation, submitter, new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System), Options.Create(options));

    private static IEventStoreGatewayClient Gateway(StreamReadPage page)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(page));
        return gateway;
    }

    private static IProcessTranslator Translator(IReadOnlyList<ProcessEffect> effects)
    {
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>(effects));
        return translator;
    }

    private static IProcessDelegationProvider Delegation()
    {
        IProcessDelegationProvider provider = Substitute.For<IProcessDelegationProvider>();
        provider.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new TrustedEffectContext("synthetic", "process", call.ArgAt<string>(1), "token")));
        return provider;
    }

    private static ITrustedEffectSubmitter Submitter()
    {
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new TrustedEffectResult(
                EffectIdentityCodec.ComputeEffectId(call.Arg<TrustedEffectSubmission>().Identity), TrustedEffectDisposition.Success, false, null)));
        return submitter;
    }

    private static ProcessEffect Effect(byte[] bytes) => new(EffectKindCatalog.Registry, "sample", "target", 0, "Do", bytes);

    private static async Task<IReadOnlyList<ProcessEffect>> DelayedEmptyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);
        return [];
    }

    private static async Task<IReadOnlyList<ProcessEffect>> WaitForTranslationCancellationAsync(CancellationToken cancellationToken, TaskCompletionSource entered)
    {
        entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }

    private static async Task<TrustedEffectResult> WaitForSubmissionCancellationAsync(CancellationToken cancellationToken, TaskCompletionSource entered)
    {
        entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation should complete this synthetic submission.");
    }

    private static StreamReadPage OneEventPage() => Page(1);

    private static StreamReadPage TwoEventPage() => Page(2);

    private static StreamReadPage Page(int count)
    {
        StreamReadEvent[] events = Enumerable.Range(1, count).Select(position =>
            new StreamReadEvent(position, "SyntheticEvent", [123], "json", 1, $"message-{position}", null, null, DateTimeOffset.UtcNow, null)).ToArray();
        return new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
            new StreamReadMetadata(0, null, count, count, count, false, null));
    }
}
