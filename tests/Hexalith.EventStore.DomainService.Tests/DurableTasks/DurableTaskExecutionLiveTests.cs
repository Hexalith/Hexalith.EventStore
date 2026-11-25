using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>The two native execution phases, run before and after the Platform provider restart.</summary>
public sealed class DurableTaskExecutionLiveTests
{
    /// <summary>Persists the starting checkpoint and loses the second owner reply after its commit.</summary>
    [Fact]
    public async Task CommitWithLostOwnerReply()
    {
        using DaprClient first = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskExecutionTests.CommitWithLostReplyAsync(new DaprReadModelStore(first), new DaprReadModelStore(second));
    }

    /// <summary>Resumes from the retained store through new worker and sidecar processes.</summary>
    [Fact]
    public async Task RecoverCommittedReceiptAfterRestart()
    {
        using DaprClient first = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskExecutionTests.RecoverAfterLostReplyAsync(new DaprReadModelStore(first), new DaprReadModelStore(second));
    }
}
