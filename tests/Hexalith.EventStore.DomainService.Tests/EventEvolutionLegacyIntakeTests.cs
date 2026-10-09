using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Verifies every legacy replay and projection route fences unverified versioned input before callbacks.</summary>
public sealed class EventEvolutionLegacyIntakeTests
{
    /// <summary>Checks complete-batch refusal precedes both replay registration paths.</summary>
    [Theory]
    [InlineData(2, "evt", 1)]
    [InlineData(1, "evt", 1)]
    [InlineData(1, null, 0)]
    public async Task ReplayRefusesVersionedBatchBeforeSyncOrAsyncRouteSelection(int metadataVersion, string? type, int? version)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        var versioned = new ReplayEventEnvelope(2, "legacy", [123, 125], "json", metadataVersion, "message", null, null)
        {
            StoredEventContractType = type,
            StoredPayloadVersion = version,
        };
        var request = new AggregateReconstructionRequest("tenant", "d", "aggregate", "id", 2,
            [versioned with { SequenceNumber = 1, MetadataVersion = 1, StoredEventContractType = null, StoredPayloadVersion = null }, versioned], true, null);
        DomainServiceRequestRouter.Replay(provider, request).ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        (await DomainServiceRequestRouter.ReplayAsync(provider, request)).ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks requested-prefix selection preserves the original async cancellation token.</summary>
    [Fact]
    public async Task ReplayIgnoresUnrequestedFutureVersionAndForwardsOriginalToken()
    {
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        _ = replay.CanReplayAggregateType("aggregate").Returns(true);
        using ServiceProvider provider = new ServiceCollection().AddKeyedSingleton("d", replay).BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var future = new ReplayEventEnvelope(2, "legacy", [123, 125], "json", 2, "message", null, null)
        {
            StoredEventContractType = "evt", StoredPayloadVersion = 1,
        };
        var request = new AggregateReconstructionRequest("tenant", "d", "aggregate", "id", 1, [future], false, null);
        _ = replay.ReplayAsync(Arg.Any<AggregateReconstructionRequest>(), cancellation.Token).Returns(AggregateReconstructionResult.Failed(
            AggregateReconstructionErrorCategory.UnknownEventType, "fixture"));
        _ = await DomainServiceRequestRouter.ReplayAsync(provider, request, cancellation.Token);
        _ = await replay.Received(1).ReplayAsync(Arg.Is<AggregateReconstructionRequest>(
            captured => captured.UpToSequence == request.UpToSequence && captured.Events.Count == 0), cancellation.Token);
    }

    /// <summary>Checks full, named, staged, reconciliation and shared rebuild routes refuse before resolving state or handlers.</summary>
    [Theory]
    [InlineData(2, "evt", 1)]
    [InlineData(1, "evt", 1)]
    [InlineData(1, null, 0)]
    public async Task EveryProjectionIntakeRefusesVersionedMetadataBeforeHandlerOrStoreResolution(int metadataVersion, string? type, int? version)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        ProjectionEventDto item = Event() with { MetadataVersion = metadataVersion, StoredEventContractType = type, StoredPayloadVersion = version };
        var request = new ProjectionRequest("tenant", "d", "id", [item]);
        var dispatch = new ProjectionDispatchRequest(request, ["projection"], "operation", "catalog");
        var options = new ProjectionDispatchOptions();
        var identity = new DomainProjectionIdentityOptions { AppId = "app", ServiceVersion = "v1" };
        Should.Throw<ProjectionDispatchValidationException>(() => DomainProjectionDispatcher.Project(provider, request))
            .ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        var actions = new Func<Task<ProjectionDispatchResponse>>[]
        {
            () => DomainProjectionDispatcher.DispatchAsync(provider, dispatch, options, new DomainProjectionCatalogRegistry(), CancellationToken.None),
            () => DomainProjectionDispatcher.ReconcileAsync(provider, dispatch, options, identity, CancellationToken.None),
            () => DomainProjectionDispatcher.RebuildAsync(provider, dispatch, options, identity, CancellationToken.None),
            () => DomainProjectionDispatcher.StageRebuildAsync(provider, dispatch, options, identity, CancellationToken.None),
            () => DomainProjectionDispatcher.CommitRebuildAsync(provider, dispatch, options, identity, CancellationToken.None),
            () => DomainProjectionDispatcher.AbortRebuildAsync(provider, dispatch, options, identity, CancellationToken.None),
            () => DomainProjectionDispatcher.VerifyRebuildAsync(provider, dispatch, options, identity, CancellationToken.None),
        };
        foreach (Func<Task<ProjectionDispatchResponse>> action in actions)
        {
            (await Should.ThrowAsync<ProjectionDispatchValidationException>(action)).ReasonCode
                .ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        var shared = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Accumulate, new("tenant", "d", "projection", "operation", "catalog"),
            AggregateOrdinal: 0, AggregateId: "id", Events: [item]);
        (await Should.ThrowAsync<ProjectionDispatchValidationException>(() => DomainSharedProjectionRebuildDispatcher.DispatchAsync(
            provider, shared, options, identity, CancellationToken.None))).ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks caller-created proof and effective-view hints confer no legacy dispatch authority.</summary>
    [Fact]
    public void ProjectionProofHintsCannotEnterLegacyHandlerSelection()
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        var request = new ProjectionRequest("tenant", "d", "id", [Event()]);
        Should.Throw<ProjectionDispatchValidationException>(() => DomainProjectionDispatcher.Project(
            provider, request with { VerifiedEffectiveEvents = [] }));
        Should.Throw<ProjectionDispatchValidationException>(() => DomainProjectionDispatcher.Project(
            provider, request with { EventEvolutionProof = [] }));
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    private static ProjectionEventDto Event()
        => new("legacy", [123, 125], "json", 1, DateTimeOffset.UnixEpoch, "correlation");
}
