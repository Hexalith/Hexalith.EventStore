using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides Aspire hosting extensions for Hexalith EventStore security resources.
/// </summary>
public static class HexalithEventStoreSecurityExtensions
{
    private const string FalseLiteral = "false";

    /// <summary>
    /// Adds the local Keycloak-backed security resource used by Hexalith EventStore AppHosts.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="options">Optional security resource settings. Defaults match the EventStore local topology.</param>
    /// <returns>
    /// The added security resources, or <see langword="null"/> when the configured
    /// <see cref="HexalithEventStoreSecurityOptions.EnableKeycloakConfigurationKey"/> value is <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an option that identifies a resource, realm, audience, path, or key is blank.</exception>
    public static HexalithEventStoreSecurityResources? AddHexalithEventStoreSecurity(
        this IDistributedApplicationBuilder builder,
        HexalithEventStoreSecurityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        options ??= new HexalithEventStoreSecurityOptions();
        ValidateOptions(options);

        if (string.Equals(
            builder.Configuration[options.EnableKeycloakConfigurationKey],
            FalseLiteral,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Dev fast-start (opt-in, default OFF). Set KeycloakPersistent=true to reuse the Keycloak
        // container across `aspire run` restarts so the cold-start + realm import is paid once
        // instead of every restart. Default OFF honors the project's "prefer non-persistent
        // resources" rule. NOTE: a reused container does NOT re-import the realm -- after editing
        // KeycloakRealms/hexalith-realm.json, remove the container (`docker rm -f`) so it re-imports
        // on the next start.
        bool keycloakPersistent = bool.TryParse(
            builder.Configuration[options.PersistentConfigurationKey]?.Trim(),
            out bool persistentParsed)
            && persistentParsed;

        // When persistent, resolve the fixed proxyless host ports up front so the AddKeycloak host-port
        // arg and the endpoint pins agree (and the client-facing realm URL, derived from GetEndpoint,
        // tracks them automatically). KeycloakHttpPort/KeycloakManagementPort override the 8180/8543
        // defaults and are validated fail-fast (integer 1..65535, distinct, not the EventStore 8080).
        // The values are read ONLY in the persistent path, so the default (dynamic, proxied) topology
        // is byte-for-byte unchanged and the override knobs are ignored unless reuse is opted into.
        int keycloakHttpPort = KeycloakFastStartPorts.DefaultHttpPort;
        int keycloakManagementPort = KeycloakFastStartPorts.DefaultManagementPort;
        if (keycloakPersistent)
        {
            (keycloakHttpPort, keycloakManagementPort) = KeycloakFastStartPorts.Resolve(
                builder.Configuration[options.HttpPortConfigurationKey],
                builder.Configuration[options.ManagementPortConfigurationKey]);
        }

        IResourceBuilder<KeycloakResource> keycloak = builder.AddKeycloak(options.ResourceName, keycloakHttpPort)
            .WithRealmImport(options.RealmImportPath);

        if (keycloakPersistent)
        {
            // DCP only REUSES a persistent container when its lifecycle-key (a hash of the
            // container's docker create spec) is byte-stable across runs. By default Aspire
            // assigns RANDOM host ports to Keycloak's endpoints on every run, which churns that
            // hash and forces a delete+recreate (full cold-start + realm re-import) -- defeating
            // the fast-start. Pin the endpoints to fixed, proxyless host ports so the docker
            // bindings are deterministic and reuse can actually engage. The ports are configurable
            // via KeycloakHttpPort/KeycloakManagementPort to relocate them off a host collision.
            _ = keycloak
                .WithLifetime(ContainerLifetime.Persistent)
                .WithEndpoint("http", e => { e.Port = keycloakHttpPort; e.IsProxied = false; })
                .WithEndpoint("management", e => { e.Port = keycloakManagementPort; e.IsProxied = false; });
        }

        EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
        ReferenceExpression realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/{options.RealmName}");
        return new HexalithEventStoreSecurityResources(
            keycloak,
            realmUrl,
            options.Audience,
            options.RequireHttpsMetadata);
    }

    /// <summary>
    /// Adds a dependency on the security resource without adding authentication environment variables.
    /// </summary>
    /// <param name="resource">The project resource that depends on security.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithSecurityDependency(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);

        return resource
            .WithReference(security.Keycloak)
            .WaitFor(security.Keycloak);
    }

    /// <summary>
    /// Wires JWT bearer authority, issuer, audience, HTTPS metadata, and signing-key override settings.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <param name="audience">Optional audience override. Defaults to the audience stored in <paramref name="security"/>.</param>
    /// <param name="requireHttpsMetadata">Optional HTTPS metadata override. Defaults to the value stored in <paramref name="security"/>.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithJwtBearerSecurity(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security,
        string? audience = null,
        bool? requireHttpsMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);

        string effectiveAudience = ResolveOptionalValue(audience, security.Audience);
        bool effectiveRequireHttpsMetadata = requireHttpsMetadata ?? security.RequireHttpsMetadata;

        return resource
            .WithSecurityDependency(security)
            .WithEnvironment("Authentication__JwtBearer__Authority", security.RealmUrl)
            .WithEnvironment("Authentication__JwtBearer__Issuer", security.RealmUrl)
            .WithEnvironment("Authentication__JwtBearer__Audience", effectiveAudience)
            .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", ToConfigurationValue(effectiveRequireHttpsMetadata))
            // Explicitly clear SigningKey to prevent dual-mode auth conflict. If SigningKey exists in
            // appsettings/secrets, clearing it ensures ConfigureJwtBearerOptions uses OIDC discovery mode only.
            .WithEnvironment("Authentication__JwtBearer__SigningKey", string.Empty);
    }

    /// <summary>
    /// Wires service credentials for EventStore client token acquisition against the security realm.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <param name="clientId">The OIDC client id used for token acquisition.</param>
    /// <param name="username">The service user name.</param>
    /// <param name="password">The service user password.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security,
        string clientId = HexalithEventStoreSecurityOptions.DefaultEventStoreClientId,
        string username = "admin-user",
        string password = "admin-pass")
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return resource
            .WithSecurityDependency(security)
            .WithEnvironment("EventStore__Authentication__Authority", security.RealmUrl)
            .WithEnvironment("EventStore__Authentication__ClientId", clientId)
            .WithEnvironment("EventStore__Authentication__Username", username)
            .WithEnvironment("EventStore__Authentication__Password", password);
    }

    /// <summary>
    /// Wires OpenID Connect client settings for an interactive UI resource.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <param name="clientId">The OpenID Connect client id.</param>
    /// <param name="clientSecret">The OpenID Connect client secret.</param>
    /// <param name="audience">Optional audience override. Defaults to the audience stored in <paramref name="security"/>.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithOpenIdConnectSecurity(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security,
        string clientId,
        string clientSecret,
        string? audience = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        string effectiveAudience = ResolveOptionalValue(audience, security.Audience);

        return resource
            .WithSecurityDependency(security)
            .WithEnvironment("Authentication__OpenIdConnect__Authority", security.RealmUrl)
            .WithEnvironment("Authentication__OpenIdConnect__ClientId", clientId)
            .WithEnvironment("Authentication__OpenIdConnect__ClientSecret", clientSecret)
            .WithEnvironment("Authentication__OpenIdConnect__Audience", effectiveAudience);
    }

    private static void ValidateOptions(HexalithEventStoreSecurityOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ResourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RealmName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RealmImportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.EnableKeycloakConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PersistentConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HttpPortConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ManagementPortConfigurationKey);
    }

    private static string ResolveOptionalValue(string? value, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value;
    }

    private static string ToConfigurationValue(bool value) => value ? "true" : "false";
}
