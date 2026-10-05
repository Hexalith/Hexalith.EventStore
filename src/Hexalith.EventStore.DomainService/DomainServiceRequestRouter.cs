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
        cancellationToken.ThrowIfCancellationRequested();
        if (request.WriterMode is not null || request.RegistryFingerprint is not null
            || request.CommandStateProof is not null || request.VerifiedEffectiveEvents is not null) {
            throw new InvalidOperationException(
                "CapabilityMismatch: this domain route has no authenticated versioned command-state intake.");
        }

        DomainServiceAdmissionContext? admissionContext = null;
        EventStoreDomainDiagnostics? diagnostics = null;
        bool diagnosticsResolved = false;
        foreach (IDomainServiceAdmissionStage stage in serviceProvider.GetServices<IDomainServiceAdmissionStage>()) {
            admissionContext ??= new DomainServiceAdmissionContext(request);
            if (!diagnosticsResolved) {
                diagnostics = ResolveDiagnostics(serviceProvider, request.Command.Domain);
                diagnosticsResolved = true;
            }

            DomainServiceAdmissionResult admissionResult = await EvaluateAdmissionStageAsync(
                stage,
                admissionContext,
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            if (admissionResult.IsRejected) {
                var rejection = DomainResult.Rejection(admissionResult.RejectionEvents);
                return await ProduceWireResultAsync(serviceProvider, request.Command.Domain, rejection, cancellationToken).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        IAsyncDomainProcessor? asyncProcessor = serviceProvider.GetKeyedService<IAsyncDomainProcessor>(request.Command.Domain);
        DomainResult result;
        if (asyncProcessor is not null) {
            result = await asyncProcessor.ProcessAsync(request.Command, request.CurrentState, cancellationToken).ConfigureAwait(false);
        }
        else {
            IDomainProcessor processor = serviceProvider.GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain);
            cancellationToken.ThrowIfCancellationRequested();
            result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await ProduceWireResultAsync(serviceProvider, request.Command.Domain, result, cancellationToken).ConfigureAwait(false);
    }

    internal static bool HasBoundedV1Producer(IServiceProvider serviceProvider, string domain)
        => ResolveBoundedV1Producer(serviceProvider, domain) is not null;

    private static BoundedV1DomainResultProducer? ResolveBoundedV1Producer(IServiceProvider serviceProvider, string domain)
        => !string.IsNullOrWhiteSpace(domain)
            ? serviceProvider.GetKeyedService<BoundedV1DomainResultProducer>(domain)
                ?? serviceProvider.GetService<BoundedV1DomainResultProducer>()
            : serviceProvider.GetService<BoundedV1DomainResultProducer>();

    private static async Task<DomainServiceWireResult> ProduceWireResultAsync(
        IServiceProvider serviceProvider, string domain, DomainResult result, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        BoundedV1DomainResultProducer? producer = ResolveBoundedV1Producer(serviceProvider, domain);
        if (producer is not null) {
            return await producer.ProduceAsync(result, cancellationToken).ConfigureAwait(false);
        }

        DomainServiceWireResult wireResult = DomainServiceWireResult.FromDomainResult(result);
        cancellationToken.ThrowIfCancellationRequested();
        return wireResult;
    }

    private static EventStoreDomainDiagnostics? ResolveDiagnostics(IServiceProvider serviceProvider, string domain) {
        // Telemetry is best-effort: never let diagnostics resolution throw on a malformed (missing) domain.
        // The downstream keyed-processor lookup produces the canonical error for an unknown/blank domain.
        if (string.IsNullOrWhiteSpace(domain)) {
            return null;
        }

        EventStoreDomainDiagnosticsRegistry? registry = serviceProvider.GetService<EventStoreDomainDiagnosticsRegistry>();
        if (registry is not null && registry.TryGetDiagnostics(domain, out EventStoreDomainDiagnostics? registered)) {
            return registered;
        }

        if (registry is not null) {
            return null;
        }

        string normalizedDomain = domain.Trim();
        return serviceProvider.GetServices<EventStoreDomainDiagnostics>()
            .FirstOrDefault(diagnostics => string.Equals(diagnostics.Domain, normalizedDomain, StringComparison.OrdinalIgnoreCase));
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
        if (request.PagedContext is not null) {
            throw new InvalidOperationException("ReplayRestartRequired: legacy replay cannot consume a paged context.");
        }

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

        AggregateReconstructionResult result = replay.Replay(request);
        if (result.Status == AggregateReconstructionStatus.InProgress) {
            throw new InvalidOperationException("ReplayRestartRequired: legacy replay cannot return incomplete page state.");
        }

        return result;
    }

    /// <summary>Replays through an explicitly registered async aggregate route when available.</summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The reconstruction request.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The reconstruction result.</returns>
    public static async Task<AggregateReconstructionResult> ReplayAsync(
        IServiceProvider serviceProvider,
        AggregateReconstructionRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.PagedContext is not null) {
            throw new InvalidOperationException(
                "ReplayRestartRequired: this route has no authenticated paged source or private state session.");
        }

        IAsyncAggregateReplay[] asyncRoutes = serviceProvider
            .GetKeyedServices<IAsyncAggregateReplay>(request.Domain)
            .Where(route => route.CanReplayAggregateType(request.AggregateType))
            .Take(2)
            .ToArray();
        if (asyncRoutes.Length > 1) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Aggregate type '{request.AggregateType}' has ambiguous async replay ownership in domain '{request.Domain}'.");
        }

        if (asyncRoutes.Length == 1) {
            AggregateReconstructionResult result = await asyncRoutes[0].ReplayAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (request.PagedContext is null && result.Status == AggregateReconstructionStatus.InProgress) {
                throw new InvalidOperationException("ReplayRestartRequired: whole-array replay cannot return incomplete page state.");
            }

            return result;
        }

        // The old synchronous path is an explicit compatibility adapter. It can
        // observe cancellation at either edge, but cannot interrupt Apply itself.
        AggregateReconstructionResult legacyResult = Replay(serviceProvider, request);
        cancellationToken.ThrowIfCancellationRequested();
        return legacyResult;
    }
}
