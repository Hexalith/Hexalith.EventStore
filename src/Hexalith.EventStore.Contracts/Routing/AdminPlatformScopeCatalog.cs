namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Declares the explicit human scope for EventStore Admin cross-tenant list reads.</summary>
public static class AdminPlatformScopeCatalog
{
    /// <summary>The disjoint platform namespace for Admin list reads.</summary>
    public const string Namespace = "platform:eventstore-admin";

    /// <summary>The narrow human permission required in addition to AdminReadOnly.</summary>
    public const string ReadPermission = "admin:read";

    /// <summary>The request-local marker set only after the declared scope and permission pass.</summary>
    public const string ValidatedContextKey = "Hexalith.EventStore.Admin.ValidatedPlatformReadScope";

    /// <summary>Gets the declaration for the two existing cross-tenant list actions.</summary>
    /// <param name="controller">The compiled controller type name.</param>
    /// <param name="action">The compiled action method name.</param>
    /// <returns>The declared admission, or null for any other action.</returns>
    public static PlatformAdmissionDeclaration? ForAction(string controller, string action)
        => controller == "Hexalith.EventStore.Admin.Server.Controllers.AdminStreamsController"
            && action is "GetRecentCommandsAsync" or "GetRecentlyActiveStreamsAsync"
                ? new PlatformAdmissionDeclaration(Namespace, ReadPermission)
                : null;
}
