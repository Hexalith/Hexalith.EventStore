using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Opt-in isolated Dapr/Redis proof for the default Resumable profile; no backend profile qualification.</summary>
public sealed class PendingBatchRecoveryLiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartRecoversInterruptedConditionalBatchAndPreservesLaterSharedIndex(bool committed)
    {
        string? http = Environment.GetEnvironmentVariable("STORY61_DAPR_HTTP_ENDPOINT");
        string? grpc = Environment.GetEnvironmentVariable("STORY61_DAPR_GRPC_ENDPOINT");
        Assert.SkipUnless(http is not null && grpc is not null, "Requires isolated Dapr endpoints.");
        string prefix = "pending-recovery-" + Guid.NewGuid().ToString("N");
        var scope = new ReadModelBatchScope("statestore", "recovery-tenant", "projects", prefix, "project-reads", "first");
        string index = prefix + ":index", detail = prefix + ":detail";
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write(index, new PendingBatchRecoveryValue(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(detail, new PendingBatchRecoveryValue(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        using (DaprClient client = new DaprClientBuilder().UseHttpEndpoint(http!).UseGrpcEndpoint(grpc!).Build())
        {
            var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, "statestore"), new(), NullLogger.Instance);
            var fault = new PendingBatchRecoveryStopAtPhase(committed ? ReadModelBatchPhase.AfterCompaction : ReadModelBatchPhase.AfterInstallOperation);
            (await protocol.ExecuteAsync(batch, fault, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
            var store = new DaprReadModelStore(client);
            var before = await store.GetAsync<PendingBatchRecoveryValue>("statestore", index, TestContext.Current.CancellationToken);
            if (committed)
            {
                before.Value!.Number.ShouldBe(1);
                var later = new ReadModelBatch(scope with { AggregateId = prefix + "-other", BatchId = "later" },
                    [ReadModelBatchOperation.Write(index, new PendingBatchRecoveryValue(2), ReadModelBatchConcurrency.Match(before.ETag!))]) { RetainManifestForRecovery = true };
                (await store.ExecuteAsync(later, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            }
            else
            {
                before.Value.ShouldBeNull();
                (await store.GetAsync<PendingBatchRecoveryValue>("statestore", detail, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            }
        }
        using DaprClient restarted = new DaprClientBuilder().UseHttpEndpoint(http!).UseGrpcEndpoint(grpc!).Build();
        var resumed = new DaprReadModelStore(restarted);
        (await resumed.RecoverPendingAsync(scope with { BatchId = "unknown" }, committed ? detail : index, TestContext.Current.CancellationToken)).IsResolved.ShouldBeTrue();
        (await resumed.GetAsync<PendingBatchRecoveryValue>("statestore", index, TestContext.Current.CancellationToken)).Value!.Number.ShouldBe(committed ? 2 : 1);
        (await resumed.GetAsync<PendingBatchRecoveryValue>("statestore", detail, TestContext.Current.CancellationToken)).Value!.Number.ShouldBe(1);
        (await resumed.ExecuteAsync(batch, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

}
