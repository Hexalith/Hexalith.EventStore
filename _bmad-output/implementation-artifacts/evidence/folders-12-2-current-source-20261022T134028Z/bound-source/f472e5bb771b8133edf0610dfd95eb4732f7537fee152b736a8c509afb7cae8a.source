using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Authoritative contextual read outages retain their source until recovery or audited disposition.</summary>
public sealed class ProcessEvidenceUnavailableTests
{
    /// <summary>A reconstructed runner retries typed read outages without quarantining or skipping evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextOutageRetainsCheckpointAndRetryBudgetAcrossRestart(bool exhaust)
    {
        var source = new ProcessSource("tenant-a", "sample", "source");
        var store = new InMemoryReadModelStore();
        var options = new EventStoreProcessOptions { RetryLimit = 2 };
        var checkpoints = new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new StreamReadPage(source.Tenant, source.Domain, source.Aggregate,
                [new StreamReadEvent(1, "Fact", [123], "json", 1, "source-message", null, null, DateTimeOffset.UtcNow, null)],
                new StreamReadMetadata(call.Arg<StreamReadRequest>().FromSequence, null, 1, 1, 1, false, null))));
        bool unavailable = true;
        IProcessTranslator translator = Substitute.For<IProcessTranslator>();
        translator.TranslateAsync(Arg.Any<ProcessSource>(), Arg.Any<StreamReadEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => unavailable
                ? throw new ProcessEvidenceUnavailableException(new HttpRequestException("dependency unavailable"))
                : ValueTask.FromResult<IReadOnlyList<ProcessEffect>>([]));
        ITrustedEffectSubmitter submitter = Substitute.For<ITrustedEffectSubmitter>();
        var runner = new CheckpointedProcessRunner(gateway, translator, Substitute.For<IProcessDelegationProvider>(),
            submitter, checkpoints, Options.Create(options));

        (await runner.RunAsync(source)).Status.ShouldBe(ProcessSourceStatus.Retrying);
        ProcessSourceState failed = await checkpoints.GetAsync(source);
        failed.Checkpoint.ShouldBe(0);
        failed.FailureSequence.ShouldBe(1);
        failed.Attempts.ShouldBe(1);
        failed.ReasonCode.ShouldBe("evidence-unavailable");
        (await checkpoints.ContainsCandidateAsync(source)).ShouldBeTrue();

        unavailable = exhaust;
        var restartedCheckpoints = new ProcessCheckpointStore(store, Options.Create(options), TimeProvider.System);
        var restarted = new CheckpointedProcessRunner(gateway, translator, Substitute.For<IProcessDelegationProvider>(),
            submitter, restartedCheckpoints, Options.Create(options));
        ProcessRunResult recovered = await restarted.RunAsync(source);
        recovered.Status.ShouldBe(exhaust ? ProcessSourceStatus.Parked : ProcessSourceStatus.Active);
        recovered.Checkpoint.ShouldBe(exhaust ? 0 : 1);
        (await restartedCheckpoints.GetAsync(source)).Attempts.ShouldBe(exhaust ? 2 : 0);
        (await restartedCheckpoints.ContainsCandidateAsync(source)).ShouldBeTrue();
        await submitter.DidNotReceive().SubmitAsync(Arg.Any<Hexalith.EventStore.Contracts.Effects.TrustedEffectSubmission>(),
            Arg.Any<Hexalith.EventStore.Contracts.Effects.TrustedEffectContext>(), Arg.Any<CancellationToken>());
    }
}
