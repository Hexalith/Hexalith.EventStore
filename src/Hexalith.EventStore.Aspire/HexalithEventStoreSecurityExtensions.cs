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

        // Keycloak must be addressed through a direct host endpoint for browser OIDC/PAR redirect_uri
        // validation. The default remains non-persistent and dynamic: choose free direct host ports for
        // each run, preferring 8180/8543 and moving forward when either port is busy. When persistence is
        // enabled, resolve fixed proxyless host ports up front so the AddKeycloak host-port arg and endpoint
        // pins agree (and the client-facing realm URL, derived from GetEndpoint, tracks them automatically).
        // KeycloakHttpPort/KeycloakManagementPort override the 8180/8543 defaults and are validated fail-fast
        // only in the persistent path.
        (int keycloakHttpPort, int keycloakManagementPort) = keycloakPersistent
            ? KeycloakFastStartPorts.Resolve(
                builder.Configuration[options.HttpPortConfigurationKey],
                builder.Configuration[options.ManagementPortConfigurationKey])
            : KeycloakFastStartPorts.ResolveDynamic();

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
        else
        {
            _ = keycloak
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
    /// Wires audience-aware JWT bearer validation settings for the current Aspire execution mode.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="localSecurity">
    /// The local Keycloak resources returned by <see cref="AddHexalithEventStoreSecurity"/>.
    /// Required in run mode and ignored in publish mode.
    /// </param>
    /// <param name="options">The validation-only JWT bearer settings.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resource"/> or <paramref name="options"/> is <see langword="null"/>,
    /// or when <paramref name="localSecurity"/> is <see langword="null"/> in run mode.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an audience is blank, or when a publish authority or issuer is missing or is not an
    /// absolute HTTPS URI without user information, a query, or a fragment.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Aspire execution mode is unsupported or the helper has already configured the resource.
    /// </exception>
    public static IResourceBuilder<ProjectResource> WithEventStoreJwtAuthentication(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources? localSecurity,
        HexalithEventStoreJwtAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(options);

        string[] audiences = ResolveAudiences(options);
        bool isRunMode = resource.ApplicationBuilder.ExecutionContext.IsRunMode;
        string authority;
        string issuer;
        bool requireHttpsMetadata;

        if (isRunMode)
        {
            ArgumentNullException.ThrowIfNull(localSecurity);
            authority = string.Empty;
            issuer = string.Empty;
            requireHttpsMetadata = localSecurity.RequireHttpsMetadata;
        }
        else if (resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            authority = ResolveExternalEndpoint(options.ExternalAuthority, nameof(options.ExternalAuthority));
            issuer = ResolveExternalEndpoint(options.ExternalIssuer, nameof(options.ExternalIssuer));
            requireHttpsMetadata = true;
        }
        else
        {
            throw new InvalidOperationException("The Aspire execution mode must be run or publish.");
        }

        if (resource.Resource.Annotations.OfType<EventStoreJwtAuthenticationAnnotation>().Any())
        {
            throw new InvalidOperationException(
                "EventStore JWT authentication can be configured only once per project resource.");
        }

        _ = resource.WithAnnotation(
            new EventStoreJwtAuthenticationAnnotation(),
            ResourceAnnotationMutationBehavior.Append);

        if (isRunMode)
        {
            return AddLocalJwtAuthenticationEnvironment(
                resource.WithSecurityDependency(localSecurity!),
                localSecurity!.RealmUrl,
                audiences,
                requireHttpsMetadata);
        }

        return AddExternalJwtAuthenticationEnvironment(
            resource,
            authority,
            issuer,
            audiences,
            requireHttpsMetadata);
    }

    /// <summary>
    /// Wires EventStore bearer-token validation settings without injecting service-account credentials.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithEventStoreAuthenticationValidation(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);

        return resource
            .WithSecurityDependency(security)
            .WithEnvironment("EventStore__Authentication__Authority", security.RealmUrl)
            .WithEnvironment("EventStore__Authentication__Audience", security.Audience)
            .WithEnvironment("EventStore__Authentication__RequireHttpsMetadata", ToConfigurationValue(security.RequireHttpsMetadata));
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
            .WithEventStoreAuthenticationValidation(security)
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

    private static IResourceBuilder<ProjectResource> AddExternalJwtAuthenticationEnvironment(
        IResourceBuilder<ProjectResource> resource,
        string authority,
        string issuer,
        IReadOnlyList<string> audiences,
        bool requireHttpsMetadata)
    {
        _ = resource
            .WithEnvironment("Authentication__JwtBearer__Authority", authority)
            .WithEnvironment("Authentication__JwtBearer__Issuer", issuer);

        return AddJwtAudienceEnvironment(resource, audiences, requireHttpsMetadata);
    }

    private static IResourceBuilder<ProjectResource> AddJwtAudienceEnvironment(
        IResourceBuilder<ProjectResource> resource,
        IReadOnlyList<string> audiences,
        bool requireHttpsMetadata)
    {
        _ = resource.WithEnvironment("Authentication__JwtBearer__Audience", audiences[0]);

        for (int index = 0; index < audiences.Count; index++)
        {
            _ = resource.WithEnvironment(
                $"Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__{index}",
                audiences[index]);
        }

        return resource
            .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", ToConfigurationValue(requireHttpsMetadata))
            .WithEnvironment("Authentication__JwtBearer__SigningKey", string.Empty);
    }

    private static IResourceBuilder<ProjectResource> AddLocalJwtAuthenticationEnvironment(
        IResourceBuilder<ProjectResource> resource,
        ReferenceExpression realmUrl,
        IReadOnlyList<string> audiences,
        bool requireHttpsMetadata)
    {
        _ = resource
            .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
            .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl);

        return AddJwtAudienceEnvironment(resource, audiences, requireHttpsMetadata);
    }

    private static string[] ResolveAudiences(HexalithEventStoreJwtAuthenticationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PrimaryAudience);
        ArgumentNullException.ThrowIfNull(options.ValidAudiences);

        var audiences = new List<string>(options.ValidAudiences.Count + 1);
        var uniqueAudiences = new HashSet<string>(StringComparer.Ordinal);
        AddAudience(options.PrimaryAudience);

        foreach (string audience in options.ValidAudiences)
        {
            AddAudience(audience);
        }

        return [.. audiences];

        void AddAudience(string audience)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(audience, nameof(options.ValidAudiences));
            string trimmedAudience = audience.Trim();
            if (uniqueAudiences.Add(trimmedAudience))
            {
                audiences.Add(trimmedAudience);
            }
        }
    }

    private static string ResolveExternalEndpoint(string? endpoint, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, parameterName);
        string trimmedEndpoint = endpoint.Trim();
        if (!Uri.TryCreate(trimmedEndpoint, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "The endpoint must be an absolute HTTPS URI without user information, a query, or a fragment.",
                parameterName);
        }

        return trimmedEndpoint;
    }

    private static string ToConfigurationValue(bool value) => value ? "true" : "false";

    private sealed record EventStoreJwtAuthenticationAnnotation : IResourceAnnotation;
}
