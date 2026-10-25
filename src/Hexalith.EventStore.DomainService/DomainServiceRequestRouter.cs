using System.Diagnostics;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;

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
        CancellationToken cancellationToken = default)
        => await ProcessAdmittedSourceAsync(serviceProvider, request, AdmitCompleteSource(serviceProvider, request), cancellationToken).ConfigureAwait(false);

    /// <summary>Authenticates the original request before trusted administrator normalization.</summary>
    internal static CompleteCommandSource? AdmitCompleteSource(IServiceProvider serviceProvider, DomainServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider); ArgumentNullException.ThrowIfNull(request);
        var options = serviceProvider.GetService<CompleteCommandSourceVerificationOptions>();
        if (request.CompleteSource is null && options?.RequiredDomains.Contains(request.Command.Domain, StringComparer.Ordinal) != true) return null;
        var http = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        string? platform = options?.PlatformWorkloadId
            ?? serviceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>()?["EventStore:CompleteCommandSource:PlatformWorkloadId"];
        if (http is null || string.IsNullOrWhiteSpace(platform)) throw new UnauthorizedAccessException("CompleteCommandSourceDenied");
        return new CompleteCommandSourceVerifier(serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System).Verify(request, http.User, platform);
    }

    /// <summary>Routes trusted normalized input while exposing its previously admitted source only for this invocation.</summary>
    internal static async Task<DomainServiceWireResult> ProcessAdmittedSourceAsync(IServiceProvider serviceProvider,
        DomainServiceRequest request, CompleteCommandSource? completeSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using Hexalith.EventStore.Client.Effects.TrustedEffectOriginScope originScope =
            Hexalith.EventStore.Client.Effects.TrustedEffectOriginScope.Enter(
                await DomainServiceTrustedEffectOrigin.ValidateAsync(serviceProvider, request, cancellationToken).ConfigureAwait(false));
        if (request.WriterMode is not null || request.RegistryFingerprint is not null
            || request.CommandStateProof is not null || request.VerifiedEffectiveEvents is not null) {
            throw new InvalidOperationException(
                "CapabilityMismatch: this domain route has no authenticated versioned command-state intake.");
        }

        CompleteCommandSource? previousSource = CompleteCommandSourceContext.Set(completeSource);
        try
        {
            return await ProcessCoreAsync(serviceProvider, request, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Reflection.TargetInvocationException { InnerException: UnauthorizedAccessException })
        {
            // Binding denials were already audited above, before entering domain routing.
            // Adapter authorization failures use this one common boundary, including absent origin.
            await DomainServiceTrustedEffectOrigin.RecordAdapterDenialAsync(serviceProvider, request, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally { CompleteCommandSourceContext.Set(previousSource); }
    }

    private static async Task<DomainServiceWireResult> ProcessCoreAsync(IServiceProvider serviceProvider,
        DomainServiceRequest request, Func<CancellationToken, Task>? fence, CancellationToken cancellationToken, EventBufferBudget? budget = null, Func<object, CancellationToken, Task>? stateFence = null) {
        async Task RequireCurrentAsync() {
            cancellationToken.ThrowIfCancellationRequested();
            if (fence is not null) { await fence(cancellationToken).ConfigureAwait(false); }
            cancellationToken.ThrowIfCancellationRequested();
        }
        async Task<DomainServiceWireResult> ProduceAuthorizedAsync(
            DomainResult result,
            Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?>? capture = null,
            FoldedSnapshotCaptureLimits? captureLimits = null) {
            await RequireCurrentAsync().ConfigureAwait(false);
            // A processor cannot inject state evidence before exact event/result admission.
            result = result with { PostCommandStateJson = null };
            using PrivateProducedDomainResult wire = await ProduceWireOwnedAsync(serviceProvider, request.Command.Domain, result, cancellationToken, fence, budget).ConfigureAwait(false);
            await RequireCurrentAsync().ConfigureAwait(false);
            string? foldedJson = null;
            if (capture is not null && captureLimits is not null && wire.Wire.Events.Count > 0
                && BoundedV1WireResultAdmission.CanReserveOptionalState(
                    wire.Wire, captureLimits.MaximumEscapedWireStateBytes, cancellationToken))
            {
                DomainServiceWireEvent[]? detached = null;
                try
                {
                    IReadOnlyList<DomainServiceWireEvent> admitted = wire.Wire.Events;
                    if (admitted.Count > captureLimits.MaximumEventCount)
                    {
                        throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
                    }
                    long bytes = 0;
                    foreach (DomainServiceWireEvent item in admitted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        bytes = checked(bytes + item.Payload.Length);
                    }
                    if (bytes > captureLimits.MaximumEventBytes
                        || checked(bytes + 256L * admitted.Count) > captureLimits.MaximumDetachedEventGraphBytes)
                    {
                        throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
                    }
                    detached = new DomainServiceWireEvent[admitted.Count];
                    for (int index = 0; index < detached.Length; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        DomainServiceWireEvent item = admitted[index];
                        detached[index] = item with { Payload = item.Payload.ToArray() };
                    }
                    foldedJson = capture(Array.AsReadOnly(detached), cancellationToken);
                    if (foldedJson is not null)
                    {
                        _ = BoundedV1WireResultAdmission.Admit(
                            wire.Wire with { PostCommandStateJson = foldedJson }, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { foldedJson = null; }
                finally
                {
                    if (detached is not null)
                    {
                        foreach (DomainServiceWireEvent? item in detached)
                        {
                            if (item is not null) { CryptographicOperations.ZeroMemory(item.Payload); }
                        }
                    }
                }
            }
            await RequireCurrentAsync().ConfigureAwait(false);
            return wire.Release() with { PostCommandStateJson = foldedJson };
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
        Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?>? capture = null;
        FoldedSnapshotCaptureLimits? captureLimits = null;
        if (asyncProcessor is not null) {
            try {
                FoldedSnapshotQualification? qualification = null;
                IAsyncFoldedSnapshotProcessor? snapshotProcessor = asyncProcessor as IAsyncFoldedSnapshotProcessor;
                bool captureQualified = false;
                if (request.IncludePostCommandState && request.SnapshotCaptureProfileId is not null && snapshotProcessor is not null)
                {
                    try
                    {
                        qualification = ResolveBoundedV1Producer(serviceProvider, request.Command.Domain) is not null
                            ? serviceProvider.GetService<FoldedSnapshotQualificationRegistry>()?
                                .FindByProfile(request.Command.Domain, request.SnapshotCaptureProfileId)
                            : null;
                        captureQualified = qualification is not null
                            && snapshotProcessor.IsQualified(qualification.StateType, qualification.ProfileId,
                                qualification.ParityEvidenceId, qualification.Limits);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch { captureQualified = false; }
                }
                if (captureQualified)
                {
                    FoldedSnapshotProcessingResult processed = await snapshotProcessor!.ProcessForSnapshotAsync(
                        request, qualification!.ProfileId, qualification.Limits, cancellationToken).ConfigureAwait(false);
                    result = processed.Result;
                    capture = processed.Capture;
                    captureLimits = qualification.Limits;
                }
                else
                {
                    result = await asyncProcessor.ProcessAsync(request.Command, request.CurrentState, cancellationToken).ConfigureAwait(false);
                }
            }
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
        return await ProduceAuthorizedAsync(result, capture, captureLimits).ConfigureAwait(false);
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
            && (item.MetadataVersion != 1 || item.StoredEventContractType is not null || item.StoredPayloadVersion is < 1 or > 1024
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
