using System.Text.Json;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Routes a <see cref="ProjectionRequest"/> to the registered <see cref="IDomainProjectionHandler"/> whose
/// <see cref="IDomainProjectionHandler.Domain"/> matches. Backs the SDK's <c>/project</c> endpoint, mirroring
/// how <see cref="DomainServiceRequestRouter"/> backs <c>/process</c> and <see cref="DomainQueryDispatcher"/>
/// backs <c>/query</c>.
/// </summary>
public static class DomainProjectionDispatcher {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    /// <summary>
    /// Projects a request by dispatching it to the matching domain projection handler.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The projection request to dispatch.</param>
    /// <returns>
    /// The handler's <see cref="ProjectionResponse"/>, or <c>null</c> when no handler is registered for the
    /// request's domain (the endpoint maps a <c>null</c> result to <c>404 Not Found</c>).
    /// </returns>
    public static ProjectionResponse? Project(IServiceProvider serviceProvider, ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        IDomainProjectionHandler? handler = DomainProjectionHandlerRouteValidator
            .MaterializeAndValidate(serviceProvider.GetServices<IDomainProjectionHandler>())
            .FirstOrDefault(h => string.Equals(h.Domain, request.Domain, StringComparison.OrdinalIgnoreCase));

        return handler?.Project(request);
    }

    /// <summary>Dispatches one v2 request to every exact admitted named projection handler.</summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="dispatchRequest">The version-2 dispatch request.</param>
    /// <param name="options">The validated dispatch bounds.</param>
    /// <param name="catalogRegistry">The fingerprints issued by this service's metadata endpoint.</param>
    /// <param name="cancellationToken">Propagates request cancellation and prevents later starts.</param>
    /// <returns>A bounded deterministic per-projection response.</returns>
    public static async Task<ProjectionDispatchResponse> DispatchAsync(
        IServiceProvider serviceProvider,
        ProjectionDispatchRequest dispatchRequest,
        ProjectionDispatchOptions options,
        DomainProjectionCatalogRegistry catalogRegistry,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalogRegistry);
        options.Validate();
        ValidateRequest(dispatchRequest, options);
        if (!catalogRegistry.Contains(dispatchRequest.CatalogFingerprint)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        if (!catalogRegistry.Authorizes(
            dispatchRequest.CatalogFingerprint,
            dispatchRequest.Request.Domain,
            dispatchRequest.ProjectionTypes)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        return await DispatchCoreAsync(serviceProvider, dispatchRequest, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Plans every required named full-replay projection and promotes the candidates through one
    /// coordinated read-model batch.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="dispatchRequest">The rebuild dispatch containing the complete event prefix.</param>
    /// <param name="options">The validated dispatch bounds.</param>
    /// <param name="identityOptions">The authoritative local service binding.</param>
    /// <param name="cancellationToken">Propagates request cancellation and prevents later starts.</param>
    /// <returns>One durable, distinguishable outcome per required projection route.</returns>
    public static async Task<ProjectionDispatchResponse> RebuildAsync(
        IServiceProvider serviceProvider,
        ProjectionDispatchRequest dispatchRequest,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identityOptions);
        options.Validate();
        identityOptions.Validate();
        ValidateRequest(dispatchRequest, options);
        ValidateRebuildEventHistory(dispatchRequest.Request.Events, options.MaxRebuildEventCount);

        IAsyncDomainProjectionHandler[] allHandlers = DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(serviceProvider.GetServices<IAsyncDomainProjectionHandler>(), options);
        ProjectionDispatchRoute[] authoritativeRoutes = [.. allHandlers
            .Where(handler => string.Equals(handler.Domain, dispatchRequest.Request.Domain, StringComparison.Ordinal))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        if (authoritativeRoutes.Length == 0
            || !string.Equals(
                ProjectionRouteCatalogFingerprint.Compute(
                    identityOptions.AppId,
                    identityOptions.ServiceVersion,
                    authoritativeRoutes),
                dispatchRequest.CatalogFingerprint,
                StringComparison.Ordinal)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        HashSet<string> requestedTypes = new(dispatchRequest.ProjectionTypes, StringComparer.Ordinal);
        if (requestedTypes.Count != authoritativeRoutes.Length
            || authoritativeRoutes.Any(route => !requestedTypes.Contains(route.ProjectionType))) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        IAsyncDomainProjectionHandler[] handlers = [.. allHandlers.Where(handler =>
            string.Equals(handler.Domain, dispatchRequest.Request.Domain, StringComparison.Ordinal)
            && requestedTypes.Contains(handler.ProjectionType))];
        if (handlers.Length != requestedTypes.Count) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        var outcomes = new Dictionary<string, ProjectionDispatchOutcome>(StringComparer.Ordinal);
        var operations = new List<ReadModelBatchOperation>();
        string? storeName = null;
        foreach (IAsyncDomainProjectionHandler handler in handlers) {
            cancellationToken.ThrowIfCancellationRequested();
            if (handler is not IAsyncDomainProjectionRebuildHandler rebuildHandler
                || rebuildHandler.RebuildSemantics != DomainProjectionRebuildSemantics.FullReplay) {
                outcomes[handler.ProjectionType] = new ProjectionDispatchOutcome(
                    handler.ProjectionType,
                    ProjectionDispatchStatus.Failed,
                    null,
                    ProjectionDispatchReasonCodes.UnsupportedCapability);
                continue;
            }

            try {
                DomainProjectionRebuildPlan plan = await rebuildHandler
                    .PrepareRebuildAsync(
                        dispatchRequest.Request,
                        dispatchRequest.DispatchId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (storeName is not null && !string.Equals(storeName, plan.StoreName, StringComparison.Ordinal)) {
                    outcomes[handler.ProjectionType] = new ProjectionDispatchOutcome(
                        handler.ProjectionType,
                        ProjectionDispatchStatus.Failed,
                        null,
                        ProjectionDispatchReasonCodes.UnsupportedCapability);
                    continue;
                }

                storeName ??= plan.StoreName;
                operations.AddRange(plan.Operations);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception) {
                outcomes[handler.ProjectionType] = new ProjectionDispatchOutcome(
                    handler.ProjectionType,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.HandlerFailure);
            }
        }

        if (outcomes.Count > 0) {
            return new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [.. handlers.Select(handler => outcomes.TryGetValue(handler.ProjectionType, out ProjectionDispatchOutcome? outcome)
                    ? outcome
                    : new ProjectionDispatchOutcome(
                        handler.ProjectionType,
                        ProjectionDispatchStatus.Retryable,
                        null,
                        ProjectionDispatchReasonCodes.PartialRetry))]);
        }

        IReadModelBatchStore? batchStore = serviceProvider.GetService<IReadModelBatchStore>();
        if (batchStore is null || storeName is null) {
            return FailureForEveryRoute(handlers, ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        ReadModelBatch batch;
        try {
            batch = new ReadModelBatch(
                new ReadModelBatchScope(
                    storeName,
                    dispatchRequest.Request.TenantId,
                    dispatchRequest.Request.Domain,
                    dispatchRequest.Request.AggregateId,
                    "rebuild",
                    dispatchRequest.DispatchId),
                operations);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentNullException) {
            return FailureForEveryRoute(handlers, ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        DomainProjectionHandlerResult result;
        try {
            ReadModelBatchResult batchResult = await batchStore
                .ExecuteAsync(batch, cancellationToken)
                .ConfigureAwait(false);
            result = ReadModelBatchProjectionResultMapper.Map(batchResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            result = DomainProjectionHandlerResult.Indeterminate(ProjectionDispatchReasonCodes.HandlerFailure);
        }

        return new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [.. handlers.Select(handler => new ProjectionDispatchOutcome(
                handler.ProjectionType,
                result.Status,
                null,
                result.ReasonCode))]);
    }

    /// <summary>Dispatches using a catalog fingerprint recomputed from authoritative local identity and routes.</summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="dispatchRequest">The version-2 dispatch request.</param>
    /// <param name="options">The validated dispatch bounds.</param>
    /// <param name="identityOptions">The authoritative local service binding.</param>
    /// <param name="cancellationToken">Propagates request cancellation and prevents later starts.</param>
    /// <returns>A bounded deterministic per-projection response.</returns>
    public static async Task<ProjectionDispatchResponse> DispatchAsync(
        IServiceProvider serviceProvider,
        ProjectionDispatchRequest dispatchRequest,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identityOptions);
        options.Validate();
        identityOptions.Validate();
        ValidateRequest(dispatchRequest, options);

        IAsyncDomainProjectionHandler[] allHandlers = DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(serviceProvider.GetServices<IAsyncDomainProjectionHandler>(), options)
            .ToArray();
        ProjectionDispatchRoute[] authoritativeRoutes = [.. allHandlers
            .Where(handler => string.Equals(handler.Domain, dispatchRequest.Request.Domain, StringComparison.Ordinal))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        if (authoritativeRoutes.Length == 0
            || !string.Equals(
                ProjectionRouteCatalogFingerprint.Compute(
                    identityOptions.AppId,
                    identityOptions.ServiceVersion,
                    authoritativeRoutes),
                dispatchRequest.CatalogFingerprint,
                StringComparison.Ordinal)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        return await DispatchCoreAsync(
                serviceProvider,
                dispatchRequest,
                options,
                cancellationToken,
                allHandlers)
            .ConfigureAwait(false);
    }

    private static async Task<ProjectionDispatchResponse> DispatchCoreAsync(
        IServiceProvider serviceProvider,
        ProjectionDispatchRequest dispatchRequest,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken,
        IReadOnlyList<IAsyncDomainProjectionHandler>? materializedHandlers = null) {
        HashSet<string> requestedTypes = new(dispatchRequest.ProjectionTypes, StringComparer.Ordinal);
        IAsyncDomainProjectionHandler[] handlers = [.. (materializedHandlers
            ?? DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
                serviceProvider.GetServices<IAsyncDomainProjectionHandler>(),
                options))
            .Where(handler => string.Equals(handler.Domain, dispatchRequest.Request.Domain, StringComparison.Ordinal)
                && requestedTypes.Contains(handler.ProjectionType))];
        if (handlers.Length != requestedTypes.Count) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        var outcomes = new List<ProjectionDispatchOutcome>(handlers.Length);
        foreach (IAsyncDomainProjectionHandler handler in handlers) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionDispatchOutcome outcome;
            try {
                DomainProjectionHandlerResult result = await handler
                    .ProjectAsync(dispatchRequest.Request, dispatchRequest.DispatchId, cancellationToken)
                    .ConfigureAwait(false);
                outcome = NormalizeOutcome(handler.ProjectionType, result, options);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (OperationCanceledException) {
                outcome = new ProjectionDispatchOutcome(
                    handler.ProjectionType,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.HandlerFailure);
            }
            catch (Exception) {
                outcome = new ProjectionDispatchOutcome(
                    handler.ProjectionType,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.HandlerFailure);
            }

            outcomes.Add(outcome);
            if (TryGetEnvelopeSize(outcomes, out int envelopeSize)
                && envelopeSize <= options.MaxOutcomeEnvelopeBytes) {
                continue;
            }

            outcomes[^1] = new ProjectionDispatchOutcome(
                handler.ProjectionType,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome);
            for (int index = 0;
                (!TryGetEnvelopeSize(outcomes, out envelopeSize)
                    || envelopeSize > options.MaxOutcomeEnvelopeBytes)
                && index < outcomes.Count;
                index++) {
                ProjectionDispatchOutcome prior = outcomes[index];
                if (prior.State is null) {
                    continue;
                }

                outcomes[index] = new ProjectionDispatchOutcome(
                    prior.ProjectionType,
                    ProjectionDispatchStatus.Failed,
                    null,
                    ProjectionDispatchReasonCodes.MalformedOutcome);
            }
        }

        return new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes);
    }

    private static ProjectionDispatchResponse FailureForEveryRoute(
        IEnumerable<IAsyncDomainProjectionHandler> handlers,
        string reasonCode)
        => new(
            ProjectionDispatchProtocol.Version,
            [.. handlers.Select(handler => new ProjectionDispatchOutcome(
                handler.ProjectionType,
                ProjectionDispatchStatus.Failed,
                null,
                reasonCode))]);

    private static void ValidateRequest(ProjectionDispatchRequest dispatchRequest, ProjectionDispatchOptions options) {
        if (dispatchRequest.Request is null
            || string.IsNullOrWhiteSpace(dispatchRequest.Request.TenantId)
            || string.IsNullOrWhiteSpace(dispatchRequest.Request.AggregateId)
            || dispatchRequest.Request.Events is null
            || dispatchRequest.Request.Events.Any(static item => item is null)
            || dispatchRequest.ProjectionTypes is not { Count: > 0 }
            || dispatchRequest.ProjectionTypes.Count > options.MaxOutcomes
            || string.IsNullOrWhiteSpace(dispatchRequest.DispatchId)
            || string.IsNullOrWhiteSpace(dispatchRequest.CatalogFingerprint)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        try {
            NamingConventionEngine.ValidateKebabCase(dispatchRequest.Request.Domain, nameof(dispatchRequest.Request.Domain));
            foreach (string projectionType in dispatchRequest.ProjectionTypes) {
                NamingConventionEngine.ValidateKebabCase(projectionType, nameof(dispatchRequest.ProjectionTypes));
            }
        }
        catch (ArgumentException) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        if (dispatchRequest.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != dispatchRequest.ProjectionTypes.Count) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DuplicateRoute);
        }
    }

    private static void ValidateRebuildEventHistory(
        IReadOnlyList<ProjectionEventDto> events,
        int maxEventCount) {
        if (events.Count > maxEventCount) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        long expectedSequence = 1;
        foreach (ProjectionEventDto item in events) {
            if (item.SequenceNumber != expectedSequence
                || item.Payload is null
                || string.IsNullOrWhiteSpace(item.EventTypeName)
                || string.IsNullOrWhiteSpace(item.SerializationFormat)) {
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            }

            expectedSequence++;
        }
    }

    private static ProjectionDispatchOutcome NormalizeOutcome(
        string projectionType,
        DomainProjectionHandlerResult? result,
        ProjectionDispatchOptions options) {
        if (result is null
            || !Enum.IsDefined(result.Status)
            || result.State is { ValueKind: JsonValueKind.Undefined }
            || (result.State is not null
                && result.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted)
            || !IsValidReasonCode(result.ReasonCode, options.MaxReasonCodeBytes)) {
            return new ProjectionDispatchOutcome(
                projectionType,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        try {
            JsonElement? state = result.State?.Clone();
            return new ProjectionDispatchOutcome(projectionType, result.Status, state, result.ReasonCode);
        }
        catch (InvalidOperationException) {
            return new ProjectionDispatchOutcome(
                projectionType,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome);
        }
    }

    private static bool IsValidReasonCode(string? reasonCode, int maxBytes) {
        if (reasonCode is null) {
            return true;
        }

        return reasonCode.Length <= maxBytes
            && reasonCode.All(static character => character <= 0x7f);
    }

    private static bool TryGetEnvelopeSize(
        IReadOnlyList<ProjectionDispatchOutcome> outcomes,
        out int envelopeSize) {
        try {
            envelopeSize = JsonSerializer.SerializeToUtf8Bytes(
                new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes),
                SerializerOptions).Length;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or NotSupportedException) {
            envelopeSize = int.MaxValue;
            return false;
        }
    }
}
