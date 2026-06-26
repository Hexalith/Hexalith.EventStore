using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Represents the security resources added to a Hexalith Aspire AppHost.
/// </summary>
/// <param name="Keycloak">The Keycloak-backed Aspire resource exposed as the AppHost security service.</param>
/// <param name="RealmUrl">The Keycloak realm URL used as the JWT/OIDC authority.</param>
/// <param name="Audience">The default audience accepted by EventStore platform services.</param>
/// <param name="RequireHttpsMetadata">Whether JWT/OIDC metadata discovery should require HTTPS.</param>
public sealed record HexalithEventStoreSecurityResources(
    IResourceBuilder<KeycloakResource> Keycloak,
    ReferenceExpression RealmUrl,
    string Audience,
    bool RequireHttpsMetadata);
