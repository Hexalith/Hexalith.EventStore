namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Declares the fixed human maintenance scope for retained administrator delivery state.</summary>
public static class ProjectionDeliveryMaintenanceScopeCatalog
{
    /// <summary>The explicit namespace for administrator delivery reconciliation.</summary>
    public const string Namespace = "platform:global-administrators-delivery-reconciliation";

    /// <summary>The exact human permission required for this maintenance operation.</summary>
    public const string Permission = "admin:reconcile-global-administrators-delivery";

    /// <summary>The sole public route relative to the projection maintenance controller.</summary>
    public const string Route = "platform/global-administrators/delivery-reconciliation";

    /// <summary>The request-local marker established after maintenance admission succeeds.</summary>
    public const string ValidatedContextKey = "Hexalith.EventStore.Admin.ValidatedPlatformDeliveryMaintenanceScope";

    /// <summary>Resolves only the compiled administrator delivery maintenance action.</summary>
    /// <param name="controller">The compiled controller type name.</param>
    /// <param name="action">The compiled action method name.</param>
    /// <returns>The exact maintenance admission, or null for another action.</returns>
    public static PlatformAdmissionDeclaration? ForAction(string controller, string action)
        => controller == "Hexalith.EventStore.Controllers.AdminProjectionRebuildController"
            && action == "ReconcileGlobalAdministratorsDeliveryFromEventStoreAsync"
                ? new PlatformAdmissionDeclaration(Namespace, Permission)
                : null;
}
