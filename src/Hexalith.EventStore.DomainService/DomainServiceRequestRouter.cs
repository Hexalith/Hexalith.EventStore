using System.Diagnostics;

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
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A wire-safe representation of the domain result.</returns>
    public static async Task<DomainServiceWireResult> ProcessAsync(
        IServiceProvider serviceProvider,
        DomainServiceRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        DomainServiceAdmissionContext? admissionContext = null;
        EventStoreDomainDiagnostics? diagnostics = null;
        foreach (IDomainServiceAdmissionStage stage in serviceProvider.GetServices<IDomainServiceAdmissionStage>()) {
            admissionContext ??= new DomainServiceAdmissionContext(request);
            diagnostics ??= serviceProvider.GetService<EventStoreDomainDiagnostics>();
            DomainServiceAdmissionResult admissionResult = await EvaluateAdmissionStageAsync(
                stage,
                admissionContext,
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            if (admissionResult.IsRejected) {
                var rejection = DomainResult.Rejection(admissionResult.RejectionEvents);
                return DomainServiceWireResult.FromDomainResult(rejection);
            }
        }

        IDomainProcessor processor = serviceProvider.GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain);
        DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);

        return DomainServiceWireResult.FromDomainResult(result);
    }

    private static async Task<DomainServiceAdmissionResult> EvaluateAdmissionStageAsync(
        IDomainServiceAdmissionStage stage,
        DomainServiceAdmissionContext context,
        EventStoreDomainDiagnostics? diagnostics,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(context);

        string stageName = string.IsNullOrWhiteSpace(stage.Name) ? stage.GetType().Name : stage.Name.Trim();
        using Activity? activity = diagnostics?.ActivitySource.StartActivity("eventstore.domain.admission.stage");
        SetAdmissionTags(activity, context, stageName);

        long start = Stopwatch.GetTimestamp();
        try {
            DomainServiceAdmissionResult result = await stage.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(result);
            TimeSpan duration = Stopwatch.GetElapsedTime(start);
            _ = (activity?.SetTag("eventstore.admission.accepted", result.IsAccepted));
            _ = (activity?.SetTag("eventstore.admission.duration_ms", duration.TotalMilliseconds));
            diagnostics?.RecordAdmissionStage(context.Command.CommandType, stageName, result.IsAccepted, duration);
            return result;
        }
        catch {
            _ = (activity?.SetStatus(ActivityStatusCode.Error));
            throw;
        }
    }

    private static void SetAdmissionTags(Activity? activity, DomainServiceAdmissionContext context, string stageName) {
        if (activity is null) {
            return;
        }

        _ = activity.SetTag("eventstore.domain", context.Command.Domain);
        _ = activity.SetTag("eventstore.command.type", context.Command.CommandType);
        _ = activity.SetTag("eventstore.admission.stage", stageName);
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
