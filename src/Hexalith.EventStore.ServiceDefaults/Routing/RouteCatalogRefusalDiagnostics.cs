using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Emits bounded nonsecret refusal categories without request bodies, credentials, proofs or exception messages.</summary>
public static partial class RouteCatalogRefusalDiagnostics
{
    /// <summary>Reports a refusal at one fixed lifecycle, receiver or readiness boundary.</summary>
    public static void Report(ILogger? logger, string boundary, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (logger is null) return;
        string reason = exception switch
        {
            RouteCatalogUnavailableException unavailable => unavailable.Category switch
            {
                RouteCatalogRefusalReason.Configuration => "configuration-missing",
                RouteCatalogRefusalReason.Trust => "owner-trust-refused",
                RouteCatalogRefusalReason.Topology => "topology-disagreement",
                RouteCatalogRefusalReason.Inventory => "installed-inventory-disagreement",
                RouteCatalogRefusalReason.Backend => "store-or-transport-unavailable",
                RouteCatalogRefusalReason.Draining => "prior-dispatch-draining",
                _ => "catalog-disagreement",
            },
            Contracts.Routing.RouteCatalogValidationException => "catalog-invalid",
            OperationCanceledException => "canceled",
            HttpRequestException or IOException => "store-or-transport-unavailable",
            _ => "host-validation-unavailable",
        };
        Refused(logger, boundary, reason);
    }

    [LoggerMessage(5130, LogLevel.Warning, "Route catalog refusal: Boundary={Boundary}, Reason={Reason}")]
    private static partial void Refused(ILogger logger, string boundary, string reason);
}
