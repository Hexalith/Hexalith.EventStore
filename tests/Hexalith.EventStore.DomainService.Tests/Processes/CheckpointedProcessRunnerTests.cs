using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Persisted checkpoint and receipt replay cases for the synthetic process runner.</summary>
public sealed class CheckpointedProcessRunnerTests
{
    private static readonly ProcessSource Source = new("tenant-a", "sample", "aggregate-1");

    /// <summary>Two effects replay after an ambiguous commit and a reconstructed runner advances once.</summary>
    [Fact]
    public async Task AmbiguousCommitReplaysBothReceiptsAfterRunnerRestart()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Page(call.Arg<StreamReadRequest>().FromSequence, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([
                new(EffectKindCatalog.Registry, "sample", "target-a", 0, "DoA", [1]),
                new(EffectKindCatalog.Registry, "sample", "target-b", 1, "DoB", [2]),
            ]));
        IProcessDelegationProvider delegation = Delegation();
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        var committed = new HashSet<string>(StringComparer.Ordinal);
        bool ambiguous = true;
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = EffectIdentityCodec.ComputeEffectId(call.Arg<TrustedEffectSubmission>().Identity);
                bool replayed = !committed.Add(id);
                if (ambiguous)
                {
                    ambiguous = false;
                    throw new HttpRequestException("Synthetic lost response after target commit.");
                }

                return Task.FromResult(new TrustedEffectResult(id, TrustedEffectDisposition.Success, replayed, null));
            });

        CheckpointedProcessRunner first = new(gateway, translator, delegation, submitter, checkpoints, Options.Create(options));
        (await first.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);
        CheckpointedProcessRunner restarted = new(gateway, translator, delegation, submitter,
            new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System), Options.Create(options));
        ProcessRunResult result = await restarted.RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Active);
        result.Checkpoint.ShouldBe(1);
        committed.Count.ShouldBe(2);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        await delegation.Received(3).CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A lost target response on a full page replays after restart, then every envelope reaches the final checkpoint.</summary>
    [Fact]
    public async Task AmbiguousCommitOnMultiPageSourceReplaysAndReachesFinalCheckpoint()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { PageSize = 2 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        var cursors = new List<long>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            cursors.Add(request.FromSequence);
            long[] positions = request.FromSequence == 0 ? [1, 2] : [3];
            StreamReadEvent[] events = positions.Select(position => new StreamReadEvent(position, "SyntheticEvent", [123], "json", 1,
                $"message-{position}", null, null, DateTimeOffset.UtcNow, null)).ToArray();
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(request.FromSequence, null, positions[^1], 3, events.Length, request.FromSequence == 0, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([new ProcessEffect(EffectKindCatalog.Registry, "sample", "target", 0, "Do", [1])]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        var committed = new HashSet<string>(StringComparer.Ordinal);
        var submissions = new List<(long Sequence, string EffectId, bool Replayed)>();
        bool loseFirstResponse = true;
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
                string effectId = EffectIdentityCodec.ComputeEffectId(submission.Identity);
                bool replayed = !committed.Add(effectId);
                submissions.Add((submission.Identity.SourceEnvelopeSequence, effectId, replayed));
                if (loseFirstResponse)
                {
                    loseFirstResponse = false;
                    throw new HttpRequestException("Synthetic lost response after target commit.");
                }

                return Task.FromResult(new TrustedEffectResult(effectId, TrustedEffectDisposition.Success, replayed, null));
            });

        CheckpointedProcessRunner first = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));
        (await first.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);

        ProcessCheckpointStore restartedCheckpoints = new(store, Options.Create(options), TimeProvider.System);
        CheckpointedProcessRunner restarted = new(gateway, translator, Delegation(), submitter, restartedCheckpoints, Options.Create(options));
        (await restarted.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        ProcessRunResult final = await restarted.RunAsync(Source);

        final.Status.ShouldBe(ProcessSourceStatus.Active);
        final.Checkpoint.ShouldBe(3);
        cursors.ShouldBe([0L, 0L, 2L]);
        submissions.Select(value => value.Sequence).ShouldBe([1L, 1L, 2L, 3L]);
        submissions[0].EffectId.ShouldBe(submissions[1].EffectId);
        submissions[0].Replayed.ShouldBeFalse();
        submissions[1].Replayed.ShouldBeTrue();
        committed.Count.ShouldBe(3);
        (await restartedCheckpoints.GetAsync(Source)).Checkpoint.ShouldBe(3);
    }

    /// <summary>An inconsistent page head cannot submit effects or advance a checkpoint.</summary>
    [Fact]
    public async Task InvalidPageMetadataQuarantinesBeforeEffects()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Page(0, false)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        ProcessRunResult result = await runner.RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        result.Checkpoint.ShouldBe(0);
        (await checkpoints.GetAsync(Source)).FailureSequence.ShouldBeNull();
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A target receipt for another effect cannot complete an envelope.</summary>
    [Fact]
    public async Task MismatchedReceiptIdQuarantinesWithoutCheckpoint()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([new ProcessEffect(EffectKindCatalog.Registry, "sample", "target-a", 0, "DoA", [1])]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TrustedEffectResult("wrong", TrustedEffectDisposition.Success, false, null)));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        ProcessRunResult result = await runner.RunAsync(Source);

        result.Status.ShouldBe(ProcessSourceStatus.Quarantined);
        (await checkpoints.GetAsync(Source)).FailureSequence.ShouldBe(1);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);
    }

    /// <summary>Page bytes must fit one permitted envelope even when page size is reduced.</summary>
    [Fact]
    public async Task HostedByteLimitsRejectImpossibleEventBudget()
    {
        EventStoreProcessOptions options = new() { MaxEventBytes = 1024, MaxPageBytes = 512 };
        options.Validate().ShouldContain(value => value.Contains("MaxPageBytes", StringComparison.Ordinal));
        using IHost host = Host.CreateDefaultBuilder().ConfigureServices((_, services) =>
        {
            services.AddSingleton<IReadModelStore>(new InMemoryReadModelStore());
            services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>(value =>
            {
                value.MaxEventBytes = 1024;
                value.MaxPageBytes = 512;
            });
        }).Build();
        await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
    }

    /// <summary>Each next page reuses the last returned sequence as the exclusive lower bound.</summary>
    [Fact]
    public async Task LongSourceAdvancesEveryPositionWithoutDoubleIncrement()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new() { PageSize = 1 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        var requested = new List<long>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            requested.Add(request.FromSequence);
            long position = request.FromSequence + 1;
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate,
                [new StreamReadEvent(position, "SyntheticEvent", [123], "json", 1, $"message-{position}", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(request.FromSequence, null, position, 3, 1, position < 3, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Active);

        requested.ShouldBe([0L, 1L, 2L]);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(3);
    }

    /// <summary>Rejection and no-op are terminal target receipts, so both effects finish the envelope.</summary>
    [Fact]
    public async Task TerminalRejectionAndNoOpAdvanceOnlyAfterBothReceipts()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([
                new(EffectKindCatalog.Registry, "sample", "target-a", 0, "DoA", [1]),
                new(EffectKindCatalog.Registry, "sample", "target-b", 1, "DoB", [2]),
            ]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        var receipts = new List<TrustedEffectDisposition>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (receipts.Count == 1)
                    checkpoints.GetAsync(Source).GetAwaiter().GetResult().Checkpoint.ShouldBe(0);
                TrustedEffectDisposition disposition = receipts.Count == 0 ? TrustedEffectDisposition.Rejection : TrustedEffectDisposition.NoOp;
                receipts.Add(disposition);
                return Task.FromResult(new TrustedEffectResult(EffectIdentityCodec.ComputeEffectId(call.Arg<TrustedEffectSubmission>().Identity), disposition, false, null));
            });
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Active);
        receipts.ShouldBe([TrustedEffectDisposition.Rejection, TrustedEffectDisposition.NoOp]);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>A terminal first receipt does not advance the envelope when the second effect is uncertain.</summary>
    [Fact]
    public async Task SecondEffectFailureRetainsCheckpointAndReplaysFirstEffectIdentity()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([
                new(EffectKindCatalog.Registry, "sample", "target-a", 0, "DoA", [1]),
                new(EffectKindCatalog.Registry, "sample", "target-b", 1, "DoB", [2]),
            ]));
        List<string> firstEffectIds = [];
        List<string> secondEffectIds = [];
        bool failSecond = true;
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
                string id = EffectIdentityCodec.ComputeEffectId(submission.Identity);
                if (submission.Identity.Ordinal == 0) firstEffectIds.Add(id);
                if (submission.Identity.Ordinal == 1) secondEffectIds.Add(id);
                if (submission.Identity.Ordinal == 1 && failSecond)
                {
                    failSecond = false;
                    throw new HttpRequestException("Synthetic ambiguous target result.");
                }
                return Task.FromResult(new TrustedEffectResult(id, TrustedEffectDisposition.NoOp, firstEffectIds.Count > 1, null));
            });
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);
        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        firstEffectIds.Count.ShouldBe(2);
        firstEffectIds[1].ShouldBe(firstEffectIds[0]);
        secondEffectIds.Count.ShouldBe(2);
        secondEffectIds[1].ShouldBe(secondEffectIds[0]);
    }

    /// <summary>Source-read outages park after the hot budget without inventing a failed event position.</summary>
    [Fact]
    public async Task ReadFailureParksWithUnknownPositionAndNoFurtherRead()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new() { RetryLimit = 2 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("Synthetic outage.")));
        CheckpointedProcessRunner runner = new(gateway, Substitute.For<IProcessTranslator>(), Delegation(),
            Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Parked);
        ProcessSourceState persisted = await checkpoints.GetAsync(Source);
        persisted.Checkpoint.ShouldBe(0);
        persisted.FailureSequence.ShouldBeNull();
        persisted.Attempts.ShouldBe(2);
        _ = gateway.Received(2).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The first CAS loss causes a fresh read and cannot prevent a later checkpoint.</summary>
    [Fact]
    public async Task ForcedSourceCasConflictRetriesFromPersistedState()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(Source);
        bool injected = false;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (injected) return;
            injected = true;
            store.ConcurrentWriteBeforeTrySave = null;
            store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(Source),
                new ProcessSourceState(1, Source, 0, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, DateTimeOffset.UtcNow));
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        injected.ShouldBeTrue();
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
    }

    /// <summary>Delegation cannot mutate a captured command through the translator's reused buffer.</summary>
    [Fact]
    public async Task CapturedEffectsCopyTranslatorOwnedCommandBytes()
    {
        var store = new InMemoryReadModelStore();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate,
                [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null),
                 new StreamReadEvent(2, "SyntheticEvent", [2], "json", 1, "message-2", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(request.FromSequence, null, 2, 2, 2, false, null)));
        });
        byte[] reusable = [0];
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            reusable[0] = checked((byte)call.Arg<StreamReadEvent>().SequenceNumber);
            return new ValueTask<IReadOnlyList<ProcessEffect>>([new ProcessEffect(EffectKindCatalog.Registry, "sample", "target", 0, "Do", reusable)]);
        });
        var submitted = new List<byte>();
        IProcessDelegationProvider delegation = Substitute.For<IProcessDelegationProvider>();
        delegation.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            reusable[0] = 99;
            return Task.FromResult(new TrustedEffectContext("synthetic", "process", "cause", "token"));
        });
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            TrustedEffectSubmission submission = call.Arg<TrustedEffectSubmission>();
            submitted.Add(submission.CommandPayload[0]);
            return Task.FromResult(new TrustedEffectResult(EffectIdentityCodec.ComputeEffectId(submission.Identity), TrustedEffectDisposition.Success, false, null));
        });
        CheckpointedProcessRunner runner = new(gateway, translator, delegation, submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(2);
        submitted.ShouldBe([1, 2]);
    }

    /// <summary>Foreign scope, reordered positions, and unknown event types leave a source-scoped quarantine.</summary>
    [Theory]
    [InlineData("foreign")]
    [InlineData("reordered")]
    [InlineData("gap")]
    [InlineData("unknown")]
    public async Task InvalidEvidencePersistsQuarantineWithoutSubmitting(string failure)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            StreamReadEvent[] events = failure == "reordered"
                ? [new StreamReadEvent(2, "SyntheticEvent", [2], "json", 1, "message-2", null, null, DateTimeOffset.UtcNow, null),
                   new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)]
                : failure == "gap"
                ? [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null),
                   new StreamReadEvent(3, "SyntheticEvent", [3], "json", 1, "message-3", null, null, DateTimeOffset.UtcNow, null)]
                : [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)];
            return Task.FromResult(new StreamReadPage(failure == "foreign" ? "other-tenant" : Source.Tenant,
                Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(0, null, events[^1].SequenceNumber, events[^1].SequenceNumber, events.Length, false, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => failure == "unknown"
                ? throw new InvalidOperationException("Synthetic unknown event type.")
                : new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState state = await checkpoints.GetAsync(Source);
        state.Checkpoint.ShouldBe(0);
        state.ReasonCode.ShouldBe("invalid-evidence");
        (await checkpoints.ContainsCandidateAsync(Source)).ShouldBeTrue();
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Every invalid translator result is quarantined before the first target submission.</summary>
    [Theory]
    [InlineData("too-many")]
    [InlineData("duplicate")]
    [InlineData("null-list")]
    [InlineData("null-effect")]
    [InlineData("null-payload")]
    [InlineData("blank-type")]
    [InlineData("unknown-kind")]
    [InlineData("negative-ordinal")]
    [InlineData("noncanonical-target")]
    public async Task InvalidTranslatorOutputQuarantinesBeforeSubmission(string failure)
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { MaxEffectsPerEnvelope = 2 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        ProcessEffect valid = new(EffectKindCatalog.Registry, "sample", "target", 0, "Do", [1]);
        IReadOnlyList<ProcessEffect>? effects = failure switch
        {
            "too-many" => [valid, valid with { Ordinal = 1 }, valid with { Ordinal = 2 }],
            "duplicate" => [valid, valid],
            "null-list" => null,
            "null-effect" => [valid, null!],
            "null-payload" => [valid with { CommandPayload = null! }],
            "blank-type" => [valid with { CommandType = " " }],
            "unknown-kind" => [valid, valid with { Kind = "unknown-effect-kind" }],
            "negative-ordinal" => [valid, valid with { Ordinal = -1 }],
            "noncanonical-target" => [valid, valid with { TargetDomain = "Sample" }],
            _ => throw new InvalidOperationException("Unknown fixture."),
        };
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>(effects!));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState state = await checkpoints.GetAsync(Source);
        state.Checkpoint.ShouldBe(0);
        state.FailureSequence.ShouldBe(1);
        state.ReasonCode.ShouldBe("invalid-evidence");
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An expired foreign lease is taken over with a higher fence, and its prior owner cannot write.</summary>
    [Fact]
    public async Task ExpiredForeignLeaseAllowsFencedCrashTakeover()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ManualProcessTimeProvider clock = new(DateTimeOffset.UtcNow);
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), clock);
        await checkpoints.EnsureCandidateAsync(Source);
        ProcessSourceState stale = new(1, Source, 0, ProcessSourceStatus.Active, 0, null, null,
            "crashed-owner", clock.GetUtcNow() + options.LeaseDuration, 7, 0, clock.GetUtcNow());
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(Source), stale);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), Substitute.For<ITrustedEffectSubmitter>(),
            checkpoints, Options.Create(options), clock);

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(0);
        _ = gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        clock.Advance(options.LeaseDuration + TimeSpan.FromTicks(1));
        (await runner.RunAsync(Source)).Checkpoint.ShouldBe(1);
        (await checkpoints.GetAsync(Source)).Fence.ShouldBeGreaterThan(stale.Fence);
        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.UpdateOwnedAsync(stale, state => state with { Attempts = 1 }));
    }

    /// <summary>An old envelope after a durable checkpoint cannot regress or submit an effect.</summary>
    [Fact]
    public async Task OldDeliveryQuarantinesWithoutCheckpointRegression()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        store.SeedRaw(options.StateStoreName, ProcessStateKeys.Source(Source),
            new ProcessSourceState(1, Source, 1, ProcessSourceStatus.Active, 0, null, null, null, null, 0, 0, DateTimeOffset.UtcNow));
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(
            new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate,
                [new StreamReadEvent(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(1, null, 1, 1, 1, false, null))));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        CheckpointedProcessRunner runner = new(gateway, Substitute.For<IProcessTranslator>(), Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Quarantined);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Only one runner can own a source while another waits for the same checkpoint.</summary>
    [Fact]
    public async Task ConcurrentRunnerCannotReadOrAdvanceOwnedSource()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromSeconds(5) };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.TrySetResult();
            await release.Task;
            return Page(0, true);
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        Task<ProcessRunResult> first = runner.RunAsync(Source);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            ProcessRunResult contender = await runner.RunAsync(Source);
            contender.Status.ShouldBe(ProcessSourceStatus.Incomplete);
            contender.Checkpoint.ShouldBe(0);
        }
        finally
        {
            release.TrySetResult();
        }
        (await first).Checkpoint.ShouldBe(1);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(1);
        _ = gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A target-side identity conflict is source-scoped quarantine, never checkpoint success.</summary>
    [Fact]
    public async Task TargetConflictQuarantinesWithoutAdvancing()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new();
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Page(0, true)));
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([new ProcessEffect(EffectKindCatalog.Registry, "sample", "target", 0, "Do", [1])]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        submitter.SubmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TrustedEffectResult>(new HttpRequestException("Synthetic conflict.", null, System.Net.HttpStatusCode.Conflict)));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Quarantined);
        ProcessSourceState state = await checkpoints.GetAsync(Source);
        state.Checkpoint.ShouldBe(0);
        state.FailureSequence.ShouldBe(1);
        state.ReasonCode.ShouldBe("effect-conflict");
    }

    /// <summary>A full first page and short final page preserve the exclusive continuation coordinate.</summary>
    [Fact]
    public async Task FullThenPartialPageUsesLastReturnedSequence()
    {
        InMemoryReadModelStore store = new();
        EventStoreProcessOptions options = new() { PageSize = 2 };
        ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
        var cursors = new List<long>();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            cursors.Add(request.FromSequence);
            long[] positions = request.FromSequence == 0 ? [1, 2] : [3];
            StreamReadEvent[] events = positions.Select(position => new StreamReadEvent(position, "SyntheticEvent", [123], "json", 1,
                $"message-{position}", null, null, DateTimeOffset.UtcNow, null)).ToArray();
            return Task.FromResult(new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
                new StreamReadMetadata(request.FromSequence, null, positions[^1], 3, events.Length, request.FromSequence == 0, null)));
        });
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<ProcessEffect>>([]));
        CheckpointedProcessRunner runner = new(gateway, translator, Delegation(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Incomplete);
        (await runner.RunAsync(Source)).Status.ShouldBe(ProcessSourceStatus.Active);
        cursors.ShouldBe([0L, 2L]);
        (await checkpoints.GetAsync(Source)).Checkpoint.ShouldBe(3);
    }

    private static IProcessDelegationProvider Delegation()
    {
        IProcessDelegationProvider provider = Substitute.For<IProcessDelegationProvider>();
        provider.CreateAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TrustedEffectContext("synthetic", "process", "cause", "token")));
        return provider;
    }

    private static StreamReadPage Page(long from, bool consistent)
    {
        StreamReadEvent[] events = from == 0
            ? [new(1, "SyntheticEvent", [123], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null)]
            : [];
        return new StreamReadPage(Source.Tenant, Source.Domain, Source.Aggregate, events,
            new StreamReadMetadata(from, null, events.Length == 0 ? null : 1, consistent ? 1 : 2,
                events.Length, false, null));
    }
}
