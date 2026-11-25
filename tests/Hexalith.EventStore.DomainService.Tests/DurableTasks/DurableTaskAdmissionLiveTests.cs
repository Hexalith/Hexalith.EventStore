using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Exercises admission and recovery against two sidecars connected to a private persistent provider.</summary>
public sealed partial class DurableTaskAdmissionLiveTests
{
    private const string Component = "g1-task-state";

    /// <summary>Verifies concurrent first admissions through distinct live Dapr connections.</summary>
    [Fact]
    public async Task CompetingReplicasResolveTheSameTaskOnlyAdmission()
    {
        using DaprClient first = Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskAdmissionTests.VerifyCompetingAdmissionsAsync(new DaprReadModelStore(first), new DaprReadModelStore(second), Component);
    }

    /// <summary>Inspects provider state before and after replaying an admission whose response was lost.</summary>
    /// <param name="commitBeforeLoss">Whether the original request committed before its response was lost.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationCommitIsVisibleFromAnotherSidecar(bool commitBeforeLoss)
    {
        using DaprClient first = Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskAdmissionTests.VerifyConfirmationCommitAsync(
            new DaprReadModelStore(first), new DaprReadModelStore(second), Component, commitBeforeLoss);
    }

    /// <summary>Resolves status by task identifier, cancellation and completion through distinct live Dapr connections.</summary>
    [Fact]
    public async Task TaskStatusAndCancellationConvergeAcrossSidecars()
    {
        using DaprClient first = Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskLifecycleTests.VerifyLifecycleAcrossReplicasAsync(new DaprReadModelStore(first), new DaprReadModelStore(second), Component);
    }

    /// <summary>Resolves delegated step bindings, the failure budget and an owner refusal through distinct live Dapr connections.</summary>
    [Fact]
    public async Task StepBindingsAndOwnerOutcomesConvergeAcrossSidecars()
    {
        using DaprClient first = Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = Client("HEXALITH_G1_REPLICA_GRPC");
        await DurableTaskStepBindingTests.VerifyStepBindingsAcrossReplicasAsync(new DaprReadModelStore(first), new DaprReadModelStore(second), Component);
    }

    internal static DaprClient Client(string variable)
    {
        string? endpoint = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(endpoint is not null, "Requires the Platform G-1 isolated runtime fixture.");
        return new DaprClientBuilder().UseGrpcEndpoint(endpoint!).Build();
    }
}
