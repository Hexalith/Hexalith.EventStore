using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

public sealed partial class AdminProjectionRebuildController
{
    /// <summary>Reconciles only retained global-administrator delivery evidence using authoritative history.</summary>
    /// <param name="cancellationToken">Cancels the maintenance operation.</param>
    /// <returns>The existing reconciliation outcome without dispatching or appending domain events.</returns>
    [HttpPost(ProjectionDeliveryMaintenanceScopeCatalog.Route)]
    [ProducesResponseType(typeof(ProjectionDeliveryReconciliationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ReconcileGlobalAdministratorsDeliveryFromEventStoreAsync(
        CancellationToken cancellationToken)
    {
        IActionResult? authFailure = EnsureGlobalAdministrator();
        if (authFailure is not null) return authFailure;
        if (User.Identity?.IsAuthenticated != true
            || !HttpContext.Items.TryGetValue(ProjectionDeliveryMaintenanceScopeCatalog.ValidatedContextKey, out object? admission)
            || !string.Equals(admission as string, ProjectionDeliveryMaintenanceScopeCatalog.Namespace, StringComparison.Ordinal))
        {
            return ProblemWithReason(StatusCodes.Status403Forbidden, "Forbidden",
                "Explicit administrator delivery maintenance admission is required.", StreamReplayReasonCodes.ForbiddenRole);
        }

        string? operatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return ProblemWithReason(StatusCodes.Status403Forbidden, "Forbidden",
                "An attributable operator identity is required.", StreamReplayReasonCodes.ForbiddenRole);
        }

        IProjectionDeliveryReconciler? reconciler = serviceProvider?.GetService<IProjectionDeliveryReconciler>()
            ?? HttpContext.RequestServices.GetService<IProjectionDeliveryReconciler>();
        if (reconciler is null)
        {
            return ProblemWithReason(StatusCodes.Status503ServiceUnavailable, "Service Unavailable",
                "Projection delivery reconciliation capability is not available.", StreamReplayReasonCodes.ServiceUnavailable);
        }

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            new AggregateIdentity(TenantCanonicalizer.ReservedStorageTenant, "global-administrators", "global-administrators"),
            "global-administrators", operatorId, cancellationToken).ConfigureAwait(false);
        return MapDeliveryReconciliationResult(result);
    }
}
