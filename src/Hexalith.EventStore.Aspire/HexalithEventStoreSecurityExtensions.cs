using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides Aspire hosting extensions for Hexalith EventStore security resources.
/// </summary>
public static class HexalithEventStoreSecurityExtensions
{
    private const string FalseLiteral = "false";
    private const string Rs256 = "RS256";
    private static readonly HashSet<string> SupportedAsymmetricAlgorithms = new(StringComparer.Ordinal)
    {
        "RS256",
        "RS384",
        "RS512",
        "PS256",
        "PS384",
        "PS512",
        "ES256",
        "ES384",
        "ES512",
    };

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
            .WithEnvironment("Authentication__JwtBearer__AllowedAlgorithms__0", Rs256)
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
        string[] allowedAlgorithms = ResolveAllowedAlgorithms(options);
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
                allowedAlgorithms,
                requireHttpsMetadata);
        }

        return AddExternalJwtAuthenticationEnvironment(
            resource,
            authority,
            issuer,
            audiences,
            allowedAlgorithms,
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
    /// <returns>The same project resource builder for chaining.</returns>
    /// <remarks>
    /// This source-compatible overload creates required parameter resources instead of restoring
    /// reusable user-name or password defaults. Consumers must supply the parameter values per run.
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);

        string parameterPrefix = $"{resource.Resource.Name}-eventstore-auth";
        IResourceBuilder<ParameterResource> username = resource.ApplicationBuilder.AddParameter(
            $"{parameterPrefix}-username",
            secret: true);
        IResourceBuilder<ParameterResource> password = resource.ApplicationBuilder.AddParameter(
            $"{parameterPrefix}-password",
            secret: true);
        return resource.WithEventStoreClientCredentials(
            security,
            HexalithEventStoreSecurityOptions.DefaultEventStoreClientId,
            username,
            password);
    }

    /// <summary>
    /// Wires service credentials for EventStore client token acquisition against the security realm.
    /// </summary>
    /// <param name="resource">The project resource to configure.</param>
    /// <param name="security">The security resources returned by <see cref="AddHexalithEventStoreSecurity"/>.</param>
    /// <param name="clientId">The explicit OIDC client id used for token acquisition.</param>
    /// <param name="username">The per-run user-name parameter.</param>
    /// <param name="password">The per-run password parameter.</param>
    /// <returns>The same project resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithEventStoreClientCredentials(
        this IResourceBuilder<ProjectResource> resource,
        HexalithEventStoreSecurityResources security,
        string clientId,
        IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        return resource
            .WithEventStoreAuthenticationValidation(security)
            .WithEnvironment(
                "EventStore__Authentication__TokenEndpoint",
                ReferenceExpression.Create($"{security.RealmUrl}/protocol/openid-connect/token"))
            .WithEnvironment("EventStore__Authentication__Scope", "openid")
            .WithEnvironment("EventStore__Authentication__ClientId", clientId)
            .WithEnvironment("EventStore__Authentication__Username", username)
            .WithEnvironment("EventStore__Authentication__Password", password);
    }

    /// <summary>
    /// Wires external token-acquisition settings to a published UI without embedding credentials.
    /// </summary>
    /// <param name="resource">The published UI resource.</param>
    /// <param name="authority">The external HTTPS identity authority.</param>
    /// <param name="audience">The token audience requested by the UI.</param>
    /// <param name="clientId">The publish parameter holding the OIDC client identifier.</param>
    /// <param name="username">The publish secret parameter holding the user name.</param>
    /// <param name="password">The publish secret parameter holding the password.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithExternalEventStoreClientCredentials(
        this IResourceBuilder<ProjectResource> resource,
        string authority,
        string audience,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password)
        => resource.WithExternalEventStoreClientCredentials(
            authority,
            audience,
            tokenEndpoint: null,
            scope: "openid",
            clientId,
            username,
            password);

    /// <summary>
    /// Wires provider-neutral external token-acquisition settings to a published UI.
    /// </summary>
    /// <param name="resource">The published UI resource.</param>
    /// <param name="authority">The external HTTPS identity authority.</param>
    /// <param name="audience">The token audience requested by the UI.</param>
    /// <param name="tokenEndpoint">An optional explicit HTTPS token endpoint; when absent the UI uses OIDC discovery.</param>
    /// <param name="scope">The explicit OAuth scope request.</param>
    /// <param name="clientId">The publish parameter holding the OIDC client identifier.</param>
    /// <param name="username">The publish secret parameter holding the user name.</param>
    /// <param name="password">The publish secret parameter holding the password.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithExternalEventStoreClientCredentials(
        this IResourceBuilder<ProjectResource> resource,
        string authority,
        string audience,
        string? tokenEndpoint,
        string scope,
        IResourceBuilder<ParameterResource> clientId,
        IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        string resolvedAuthority = ResolveExternalEndpoint(authority, nameof(authority));
        IResourceBuilder<ProjectResource> configured = resource
            .WithEnvironment("EventStore__Authentication__Authority", resolvedAuthority)
            .WithEnvironment("EventStore__Authentication__Audience", audience.Trim())
            .WithEnvironment("EventStore__Authentication__Scope", scope.Trim())
            .WithEnvironment("EventStore__Authentication__ClientId", clientId)
            .WithEnvironment("EventStore__Authentication__Username", username)
            .WithEnvironment("EventStore__Authentication__Password", password)
            .WithEnvironment("EventStore__Authentication__SigningKey", string.Empty);

        return string.IsNullOrWhiteSpace(tokenEndpoint)
            ? configured.WithEnvironment("EventStore__Authentication__TokenEndpoint", string.Empty)
            : configured.WithEnvironment(
                "EventStore__Authentication__TokenEndpoint",
                ResolveExternalEndpoint(tokenEndpoint, nameof(tokenEndpoint)));
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
        IReadOnlyList<string> allowedAlgorithms,
        bool requireHttpsMetadata)
    {
        _ = resource
            .WithEnvironment("Authentication__JwtBearer__Authority", authority)
            .WithEnvironment("Authentication__JwtBearer__Issuer", issuer);

        return AddJwtAudienceEnvironment(resource, audiences, allowedAlgorithms, requireHttpsMetadata);
    }

    private static IResourceBuilder<ProjectResource> AddJwtAudienceEnvironment(
        IResourceBuilder<ProjectResource> resource,
        IReadOnlyList<string> audiences,
        IReadOnlyList<string> allowedAlgorithms,
        bool requireHttpsMetadata)
    {
        _ = resource.WithEnvironment("Authentication__JwtBearer__Audience", audiences[0]);

        for (int index = 0; index < audiences.Count; index++)
        {
            _ = resource.WithEnvironment(
                $"Authentication__JwtBearer__ValidAudiences__{index}",
                audiences[index]);
        }

        for (int index = 0; index < allowedAlgorithms.Count; index++)
        {
            _ = resource.WithEnvironment(
                $"Authentication__JwtBearer__AllowedAlgorithms__{index}",
                allowedAlgorithms[index]);
        }

        return resource
            .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", ToConfigurationValue(requireHttpsMetadata))
            .WithEnvironment("Authentication__JwtBearer__SigningKey", string.Empty);
    }

    private static IResourceBuilder<ProjectResource> AddLocalJwtAuthenticationEnvironment(
        IResourceBuilder<ProjectResource> resource,
        ReferenceExpression realmUrl,
        IReadOnlyList<string> audiences,
        IReadOnlyList<string> allowedAlgorithms,
        bool requireHttpsMetadata)
    {
        _ = resource
            .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
            .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl);

        return AddJwtAudienceEnvironment(resource, audiences, allowedAlgorithms, requireHttpsMetadata);
    }

    private static string[] ResolveAudiences(HexalithEventStoreJwtAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.ValidAudiences);

        var audiences = new List<string>(options.ValidAudiences.Count + 1);
        var uniqueAudiences = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(options.PrimaryAudience))
        {
            AddAudience(options.PrimaryAudience);
        }

        foreach (string audience in options.ValidAudiences)
        {
            AddAudience(audience);
        }

        if (audiences.Count == 0)
        {
            throw new ArgumentException(
                "At least one non-blank primary or valid audience is required.",
                nameof(options.ValidAudiences));
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

    private static string[] ResolveAllowedAlgorithms(HexalithEventStoreJwtAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.AllowedAlgorithms);
        if (options.AllowedAlgorithms.Count == 0)
        {
            throw new ArgumentException(
                "At least one explicit asymmetric signing algorithm is required.",
                nameof(options.AllowedAlgorithms));
        }

        var algorithms = new List<string>(options.AllowedAlgorithms.Count);
        var uniqueAlgorithms = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? algorithm in options.AllowedAlgorithms)
        {
            string trimmedAlgorithm = algorithm?.Trim() ?? string.Empty;
            if (!SupportedAsymmetricAlgorithms.Contains(trimmedAlgorithm))
            {
                throw new ArgumentException(
                    "Only supported asymmetric signing algorithms may be configured.",
                    nameof(options.AllowedAlgorithms));
            }

            if (uniqueAlgorithms.Add(trimmedAlgorithm))
            {
                algorithms.Add(trimmedAlgorithm);
            }
        }

        return [.. algorithms];
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
