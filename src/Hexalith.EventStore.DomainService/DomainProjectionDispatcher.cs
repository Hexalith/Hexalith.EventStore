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

        HashSet<string> requestedTypes = new(dispatchRequest.ProjectionTypes, StringComparer.Ordinal);
        IAsyncDomainProjectionHandler[] handlers = [.. DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(serviceProvider.GetServices<IAsyncDomainProjectionHandler>(), options)
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
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                outcome = new ProjectionDispatchOutcome(
                    handler.ProjectionType,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.HandlerFailure);
            }

            outcomes.Add(outcome);
            if (GetEnvelopeSize(outcomes) <= options.MaxOutcomeEnvelopeBytes) {
                continue;
            }

            outcomes[^1] = new ProjectionDispatchOutcome(
                handler.ProjectionType,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome);
            if (GetEnvelopeSize(outcomes) > options.MaxOutcomeEnvelopeBytes) {
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            }
        }

        return new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes);
    }

    private static void ValidateRequest(ProjectionDispatchRequest dispatchRequest, ProjectionDispatchOptions options) {
        if (dispatchRequest.Request is null
            || string.IsNullOrWhiteSpace(dispatchRequest.Request.TenantId)
            || string.IsNullOrWhiteSpace(dispatchRequest.Request.AggregateId)
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

    private static ProjectionDispatchOutcome NormalizeOutcome(
        string projectionType,
        DomainProjectionHandlerResult? result,
        ProjectionDispatchOptions options) {
        if (result is null
            || !Enum.IsDefined(result.Status)
            || (result.State is not null
                && result.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted)
            || !IsValidReasonCode(result.ReasonCode, options.MaxReasonCodeBytes)) {
            return new ProjectionDispatchOutcome(
                projectionType,
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        JsonElement? state = result.State?.Clone();
        return new ProjectionDispatchOutcome(projectionType, result.Status, state, result.ReasonCode);
    }

    private static bool IsValidReasonCode(string? reasonCode, int maxBytes) {
        if (reasonCode is null) {
            return true;
        }

        return reasonCode.Length <= maxBytes
            && reasonCode.All(static character => character <= 0x7f);
    }

    private static int GetEnvelopeSize(IReadOnlyList<ProjectionDispatchOutcome> outcomes)
        => JsonSerializer.SerializeToUtf8Bytes(
            new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes)).Length;
}
