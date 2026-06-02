using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Routes domain service requests to the keyed processor matching the command domain.
/// </summary>
public static class DomainServiceRequestRouter {
    /// <summary>
    /// Processes a domain service request using the keyed processor registered for the request domain.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The domain service request to process.</param>
    /// <returns>A wire-safe representation of the domain result.</returns>
    public static async Task<DomainServiceWireResult> ProcessAsync(IServiceProvider serviceProvider, DomainServiceRequest request) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        IDomainProcessor processor = serviceProvider.GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain);
        DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);

        return DomainServiceWireResult.FromDomainResult(result);
    }

    /// <summary>
    /// Replays an aggregate's events through the owning domain processor's Apply convention.
    /// Implements the canonical <c>POST /replay-state</c> endpoint required by the Admin
    /// state-inspection surface (admin-ui-aggregate-state-replay-correctness story).
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The reconstruction request.</param>
    /// <returns>The reconstruction result.</returns>
    public static AggregateReconstructionResult Replay(IServiceProvider serviceProvider, AggregateReconstructionRequest request) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        IDomainProcessor? processor = serviceProvider.GetKeyedService<IDomainProcessor>(request.Domain);
        if (processor is null) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"No domain processor is registered for domain '{request.Domain}'.");
        }

        if (processor is not IAggregateReplay replay) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Domain processor '{processor.GetType().Name}' for domain '{request.Domain}' does not implement IAggregateReplay. Inherit from EventStoreAggregate<TState> to enable Admin replay.");
        }

        if (!replay.CanReplayAggregateType(request.AggregateType)) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Aggregate type '{request.AggregateType}' is not owned by domain '{request.Domain}'.");
        }

        return replay.Replay(request);
    }
}
