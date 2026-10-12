using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Serializes renewal fault injection with the shared control-read admission pool.</summary>
[Collection("Process read slots")]
public sealed class ProcessSweepRenewalTests
{
    /// <summary>A cleanup-time renewal fault cannot turn a completed source into source-operation evidence.</summary>
    [Fact]
    public async Task SweepRenewalFaultPreservesCompletedSourceObservation()
    {
        InMemoryReadModelStore inner = new();
        SweepRenewalFaultStore faulting = new(inner);
        ManualProcessTimeProvider clock = new(DateTimeOffset.UtcNow);
        TaskCompletionSource translatingSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseTranslation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventStoreProcessOptions options = new()
        {
            SourcesPerPass = 4, SupportedCandidateCount = 1, MaxEffectsPerEnvelope = 1,
            OperationTimeout = TimeSpan.FromSeconds(10), LeaseDuration = TimeSpan.FromSeconds(30),
            SourceServiceQuantum = TimeSpan.FromSeconds(80), ReadinessMaxAge = TimeSpan.FromMinutes(10),
        };
        options.Validate().ShouldBeEmpty();
        ProcessSource source = new("tenant-a", "sample", "renewal");
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IReadOnlyList<ProcessEffect>>(TranslateAsync(call.Arg<StreamReadEvent>(), call.Arg<CancellationToken>())));
        async Task<IReadOnlyList<ProcessEffect>> TranslateAsync(StreamReadEvent envelope, CancellationToken token)
        {
            if (envelope.SequenceNumber == 2)
            {
                translatingSecond.TrySetResult();
                await releaseTranslation.Task.WaitAsync(token);
            }
            return [];
        }
        (ProcessCheckpointStore checkpoints, ProcessSweepStore sweeps, ProcessReconciler reconciler) = ProcessRecoveryTests.Runtime(faulting, options, Catalog([source]),
            request => new StreamReadPage(request.Tenant, request.Domain, request.AggregateId,
                [new(1, "SyntheticEvent", [1], "json", 1, "message-1", null, null, DateTimeOffset.UtcNow, null),
                 new(2, "SyntheticEvent", [1], "json", 1, "message-2", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(request.FromSequence, null, 2, 2, 2, false, null)), translator, clock);
        using (reconciler)
        {
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(TimeSpan.FromSeconds(20));
            Task<ProcessReconciliationPass> pass = reconciler.RunPassAsync(cancellation.Token);
            try
            {
                await translatingSecond.Task.WaitAsync(cancellation.Token);
                (await checkpoints.GetAsync(source)).Checkpoint.ShouldBe(1);
                clock.LastDueTime.ShouldBe(options.LeaseDuration / 3);
                faulting.Arm();
                clock.Advance(options.LeaseDuration / 3);
                await faulting.RenewalEntered.WaitAsync(cancellation.Token);
                releaseTranslation.TrySetResult();
                _ = await pass;
                faulting.Fired.ShouldBeTrue();
                ProcessSourceState persisted = await checkpoints.GetAsync(source);
                persisted.Checkpoint.ShouldBe(2);
                persisted.Owner.ShouldBeNull();
                persisted.Status.ShouldBe(ProcessSourceStatus.Active);
                persisted.ObservedUnresolved.ShouldBeFalse();
                (await sweeps.GetAsync()).Reasons.ShouldBeEmpty();
            }
            finally
            {
                releaseTranslation.TrySetResult();
                faulting.Release();
                cancellation.Cancel();
                try { _ = await pass; }
                catch (Exception) { }
            }
        }
    }

    private static IProcessSourceCatalog Catalog(IReadOnlyList<ProcessSource> sources)
    {
        IProcessSourceCatalog catalog = Substitute.For<IProcessSourceCatalog>();
        catalog.ListAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessSourcePage(sources, null)));
        return catalog;
    }
}
