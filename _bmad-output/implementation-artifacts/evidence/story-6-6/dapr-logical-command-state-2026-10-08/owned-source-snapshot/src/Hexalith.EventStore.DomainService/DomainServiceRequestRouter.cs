using System.Diagnostics;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Events;
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

        return await ProcessCoreAsync(serviceProvider, request, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Routes only privately verified completed logical state through the actual owner's current authority.</summary>
    internal static async Task<DomainServiceWireResult> ProcessCompletedLogicalAsync(IServiceProvider serviceProvider,
        DomainServiceRequest request, PrivateLogicalCommandState completed, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(serviceProvider); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(completed);
        object state = await completed.AdmitAsync(request, cancellationToken).ConfigureAwait(false);
        return await ProcessCoreAsync(serviceProvider, request with { Command = completed.Command, CurrentState = state }, completed.RequireCurrentAsync, cancellationToken, completed.Budget, completed.RequireStateGraphAsync).ConfigureAwait(false);
    }

    private static async Task<DomainServiceWireResult> ProcessCoreAsync(IServiceProvider serviceProvider,
        DomainServiceRequest request, Func<CancellationToken, Task>? fence, CancellationToken cancellationToken, EventBufferBudget? budget = null, Func<object, CancellationToken, Task>? stateFence = null) {
        async Task RequireCurrentAsync() {
            cancellationToken.ThrowIfCancellationRequested();
            if (fence is not null) { await fence(cancellationToken).ConfigureAwait(false); }
            cancellationToken.ThrowIfCancellationRequested();
        }
        async Task<DomainServiceWireResult> ProduceAuthorizedAsync(DomainResult result) {
            await RequireCurrentAsync().ConfigureAwait(false);
            using PrivateProducedDomainResult wire = await ProduceWireOwnedAsync(serviceProvider, request.Command.Domain, result, cancellationToken, fence, budget).ConfigureAwait(false);
            await RequireCurrentAsync().ConfigureAwait(false);
            return wire.Release();
        }
        await RequireCurrentAsync().ConfigureAwait(false);
        DomainServiceAdmissionContext? admissionContext = null;
        EventStoreDomainDiagnostics? diagnostics = null;
        bool diagnosticsResolved = false;
        IDomainServiceAdmissionStage[] stages;
        try { stages = serviceProvider.GetServices<IDomainServiceAdmissionStage>().ToArray(); }
        finally { await RequireCurrentAsync().ConfigureAwait(false); }
        foreach (IDomainServiceAdmissionStage stage in stages) {
            admissionContext ??= new DomainServiceAdmissionContext(request);
            if (!diagnosticsResolved) {
                diagnostics = ResolveDiagnostics(serviceProvider, request.Command.Domain);
                diagnosticsResolved = true;
            }

            await RequireCurrentAsync().ConfigureAwait(false);
            DomainServiceAdmissionResult admissionResult;
            try { admissionResult = await EvaluateAdmissionStageAsync(stage, admissionContext, diagnostics, cancellationToken).ConfigureAwait(false); }
            finally {
                await RequireCurrentAsync().ConfigureAwait(false);
                if (stateFence is not null) { await stateFence(request.CurrentState!, cancellationToken).ConfigureAwait(false); }
            }

            if (admissionResult.IsRejected) {
                var rejection = DomainResult.Rejection(admissionResult.RejectionEvents);
                return await ProduceAuthorizedAsync(rejection).ConfigureAwait(false);
            }
        }

        await RequireCurrentAsync().ConfigureAwait(false);
        IAsyncDomainProcessor? asyncProcessor;
        try { asyncProcessor = serviceProvider.GetKeyedService<IAsyncDomainProcessor>(request.Command.Domain); }
        finally { await RequireCurrentAsync().ConfigureAwait(false); }
        DomainResult result;
        if (asyncProcessor is not null) {
            try { result = await asyncProcessor.ProcessAsync(request.Command, request.CurrentState, cancellationToken).ConfigureAwait(false); }
            finally { await RequireCurrentAsync().ConfigureAwait(false); }
        }
        else {
            IDomainProcessor processor;
            try { processor = serviceProvider.GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain); }
            finally { await RequireCurrentAsync().ConfigureAwait(false); }
            try { result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false); }
            finally { await RequireCurrentAsync().ConfigureAwait(false); }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await RequireCurrentAsync().ConfigureAwait(false);
        return await ProduceAuthorizedAsync(result).ConfigureAwait(false);
    }

    internal static bool HasBoundedV1Producer(IServiceProvider serviceProvider, string domain)
        => ResolveBoundedV1Producer(serviceProvider, domain) is not null;

    private static BoundedV1DomainResultProducer? ResolveBoundedV1Producer(IServiceProvider serviceProvider, string domain)
        => !string.IsNullOrWhiteSpace(domain)
            ? serviceProvider.GetKeyedService<BoundedV1DomainResultProducer>(domain)
                ?? serviceProvider.GetService<BoundedV1DomainResultProducer>()
            : serviceProvider.GetService<BoundedV1DomainResultProducer>();

    private static async Task<PrivateProducedDomainResult> ProduceWireOwnedAsync(
        IServiceProvider serviceProvider, string domain, DomainResult result, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? commandFence = null, EventBufferBudget? budget = null) {
        cancellationToken.ThrowIfCancellationRequested();
        BoundedV1DomainResultProducer? producer = ResolveBoundedV1Producer(serviceProvider, domain);
        if (producer is not null) {
            return await producer.ProduceOwnedAsync(result, cancellationToken, commandFence, budget).ConfigureAwait(false);
        }

        if (commandFence is not null) {
            throw new InvalidOperationException("CapabilityMismatch: logical command results require the explicit bounded producer.");
        }
        DomainServiceWireResult wireResult = DomainServiceWireResult.FromDomainResult(result);
        cancellationToken.ThrowIfCancellationRequested();
        return new PrivateProducedDomainResult(wireResult, null);
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
    /// Replays an aggregate's events through its independently registered replay capability.
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

        using LegacyReplayInput input = LegacyReplayInput.Capture(request, CancellationToken.None);
        if (input.Refusal is not null) {
            return input.Refusal;
        }

        request = request with { Events = input.Events };
        AggregateReconstructionResult? refusal = RefuseVersionedReplay(request) ?? input.ValidatePrefix(CancellationToken.None);
        if (refusal is not null) {
            return refusal;
        }

        return ReplayLegacyAdmitted(serviceProvider, request, input, CancellationToken.None);
    }

    private static AggregateReconstructionResult ReplayLegacyAdmitted(IServiceProvider serviceProvider,
        AggregateReconstructionRequest request, LegacyReplayInput input, CancellationToken cancellationToken) {
        IAggregateReplay[] routes = SelectReplayRoutes(
            serviceProvider.GetKeyedServices<IAggregateReplay>(request.Domain),
            route => route.CanReplayAggregateType(request.AggregateType), cancellationToken);
        if (routes.Length > 1) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Aggregate type '{request.AggregateType}' has ambiguous replay ownership in domain '{request.Domain}'.");
        }

        IAggregateReplay? replay = routes.SingleOrDefault();
        if (replay is null) {
            // Preserve existing manually registered command processors as a compatibility seam.
            cancellationToken.ThrowIfCancellationRequested();
            IDomainProcessor? processor = serviceProvider.GetKeyedService<IDomainProcessor>(request.Domain);
            cancellationToken.ThrowIfCancellationRequested();
            if (processor is null) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownAggregateType,
                    $"No domain processor is registered for domain '{request.Domain}'.");
            }

            if (processor is not IAggregateReplay legacyReplay) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownAggregateType,
                    $"Domain processor '{processor.GetType().Name}' for domain '{request.Domain}' does not implement IAggregateReplay. Inherit from EventStoreAggregate<TState> to enable Admin replay.");
            }

            bool ownsAggregate = legacyReplay.CanReplayAggregateType(request.AggregateType);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ownsAggregate) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownAggregateType,
                    $"Aggregate type '{request.AggregateType}' is not owned by domain '{request.Domain}'.");
            }

            replay = legacyReplay;
        }

        cancellationToken.ThrowIfCancellationRequested();
        AggregateReconstructionResult result = replay is IAdmittedLegacyAggregateReplay admitted && admitted.CanReplayAdmitted(asynchronous: false)
            ? admitted.ReplayAdmitted(request, input, cancellationToken)
            : replay.Replay(request);
        cancellationToken.ThrowIfCancellationRequested();
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

        using LegacyReplayInput input = LegacyReplayInput.Capture(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Refusal is not null) {
            return input.Refusal;
        }

        request = request with { Events = input.Events };
        AggregateReconstructionResult? refusal = RefuseVersionedReplay(request) ?? input.ValidatePrefix(cancellationToken);
        if (refusal is not null) {
            return refusal;
        }

        IAsyncAggregateReplay[] asyncRoutes = SelectReplayRoutes(
            serviceProvider.GetKeyedServices<IAsyncAggregateReplay>(request.Domain),
            route => route.CanReplayAggregateType(request.AggregateType), cancellationToken);
        if (asyncRoutes.Length > 1) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Aggregate type '{request.AggregateType}' has ambiguous async replay ownership in domain '{request.Domain}'.");
        }

        if (asyncRoutes.Length == 1) {
            AggregateReconstructionResult result = asyncRoutes[0] is IAdmittedLegacyAggregateReplay admitted && admitted.CanReplayAdmitted(asynchronous: true)
                ? admitted.ReplayAdmitted(request, input, cancellationToken)
                : await asyncRoutes[0].ReplayAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (request.PagedContext is null && result.Status == AggregateReconstructionStatus.InProgress) {
                throw new InvalidOperationException("ReplayRestartRequired: whole-array replay cannot return incomplete page state.");
            }

            return result;
        }

        // The old synchronous path is an explicit compatibility adapter. It can
        // observe cancellation at either edge, but cannot interrupt Apply itself.
        AggregateReconstructionResult legacyResult = ReplayLegacyAdmitted(serviceProvider, request, input, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return legacyResult;
    }

    private static TRoute[] SelectReplayRoutes<TRoute>(IEnumerable<TRoute> routes,
        Func<TRoute, bool> ownsAggregate, CancellationToken cancellationToken) {
        var selected = new List<TRoute>(2);
        foreach (TRoute route in routes) {
            cancellationToken.ThrowIfCancellationRequested();
            bool owns = ownsAggregate(route);
            cancellationToken.ThrowIfCancellationRequested();
            if (owns) {
                selected.Add(route);
                if (selected.Count == 2) { break; }
            }
        }

        return selected.ToArray();
    }

    private static AggregateReconstructionResult? RefuseVersionedReplay(AggregateReconstructionRequest request) {
        ReplayEventEnvelope? versioned = request.Events.FirstOrDefault(item => item.SequenceNumber <= request.UpToSequence
            && (item.MetadataVersion != 1 || item.StoredEventContractType is not null || item.StoredPayloadVersion is not null
                || item.StoredSerializationFormat is not null || item.StoredEventTypeName is not null
                || item.StoredDigest is not null || item.RegistryFingerprint is not null || item.IsAdapted is not null
                || item.EffectiveEventContractType is not null || item.EffectivePayloadVersion is not null
                || item.EffectiveSerializationFormat is not null || item.EffectivePayload is not null));
        return versioned is null ? null : AggregateReconstructionResult.Failed(
            AggregateReconstructionErrorCategory.UnsupportedVersion,
            "RollbackReaderCapabilityHold: this replay route cannot verify versioned effective input.",
            failedSequenceNumber: versioned.SequenceNumber,
            failedEventType: versioned.EventTypeName);
    }
}
