namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Configures the local Hexalith security resource used by Aspire AppHosts.
/// </summary>
public sealed record HexalithEventStoreSecurityOptions
{
    /// <summary>
    /// The default Aspire resource name for the identity provider.
    /// </summary>
    public const string DefaultResourceName = "security";

    /// <summary>
    /// The default Keycloak realm name.
    /// </summary>
    public const string DefaultRealmName = "hexalith";

    /// <summary>
    /// The default local realm import directory.
    /// </summary>
    public const string DefaultRealmImportPath = "./KeycloakRealms";

    /// <summary>
    /// The default audience accepted by the EventStore platform services.
    /// </summary>
    public const string DefaultAudience = "hexalith-eventstore";

    /// <summary>
    /// The default OIDC client id used for EventStore service-token acquisition.
    /// </summary>
    public const string DefaultEventStoreClientId = "hexalith-eventstore";

    /// <summary>
    /// The configuration key that disables the security resource when set to <c>false</c>.
    /// </summary>
    public const string DefaultEnableKeycloakConfigurationKey = "EnableKeycloak";

    /// <summary>
    /// The configuration key that enables persistent Keycloak container reuse.
    /// </summary>
    public const string DefaultPersistentConfigurationKey = "KeycloakPersistent";

    /// <summary>
    /// The configuration key that overrides the persistent Keycloak HTTP host port.
    /// </summary>
    public const string DefaultHttpPortConfigurationKey = "KeycloakHttpPort";

    /// <summary>
    /// The configuration key that overrides the persistent Keycloak management host port.
    /// </summary>
    public const string DefaultManagementPortConfigurationKey = "KeycloakManagementPort";

    /// <summary>
    /// Gets the Aspire resource name for the identity provider.
    /// </summary>
    public string ResourceName { get; init; } = DefaultResourceName;

    /// <summary>
    /// Gets the Keycloak realm name used to build the authority URL.
    /// </summary>
    public string RealmName { get; init; } = DefaultRealmName;

    /// <summary>
    /// Gets the local Keycloak realm import path.
    /// </summary>
    public string RealmImportPath { get; init; } = DefaultRealmImportPath;

    /// <summary>
    /// Gets the default JWT audience for Hexalith EventStore platform services.
    /// </summary>
    public string Audience { get; init; } = DefaultAudience;

    /// <summary>
    /// Gets a value indicating whether JWT bearer metadata discovery should require HTTPS.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; }

    /// <summary>
    /// Gets the configuration key that disables the Keycloak-backed security resource when set to <c>false</c>.
    /// </summary>
    public string EnableKeycloakConfigurationKey { get; init; } = DefaultEnableKeycloakConfigurationKey;

    /// <summary>
    /// Gets the configuration key that enables persistent Keycloak container reuse.
    /// </summary>
    public string PersistentConfigurationKey { get; init; } = DefaultPersistentConfigurationKey;

    /// <summary>
    /// Gets the configuration key that overrides the persistent Keycloak HTTP host port.
    /// </summary>
    public string HttpPortConfigurationKey { get; init; } = DefaultHttpPortConfigurationKey;

    /// <summary>
    /// Gets the configuration key that overrides the persistent Keycloak management host port.
    /// </summary>
    public string ManagementPortConfigurationKey { get; init; } = DefaultManagementPortConfigurationKey;
}
