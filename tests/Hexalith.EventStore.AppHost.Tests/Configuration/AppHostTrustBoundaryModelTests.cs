namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Text.Json;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.Aspire;

/// <summary>
/// Story 5.5 (FR28, AD-28): the local topology wires a per-run app-channel token between each internally
/// authenticated application and its own sidecar, gives domain services the shared JWT contract, obtains EventStore
/// workload assertions from the trusted issuer, and no longer allow-lists any internal caller by app id.
/// </summary>
[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class AppHostTrustBoundaryModelTests
{
    private static readonly string[] EnvironmentKeys =
    [
        "SKIP_PREREQUISITE_CHECK",
        HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
        HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey,
    ];

    /// <summary>Keycloak run mode wires channel tokens, workload issuance, domain validation, and delegated bootstrap.</summary>
    [Fact]
    public async Task RunModel_WithKeycloak_WiresTheInternalTrustBoundary()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey, "true");
            Environment.SetEnvironmentVariable(HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey, "false");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            foreach (string name in new[] { "eventstore", "sample", "tenants" })
            {
                await AssertAppChannelTokenSharedWithSidecarOnlyAsync(builder, name).ConfigureAwait(true);
            }

            IReadOnlyDictionary<string, object> eventStore = await GetEnvironmentAsync(Project(builder, "eventstore"), builder.ExecutionContext)
                .ConfigureAwait(true);
            eventStore.Keys.ShouldNotContain(static key => key.StartsWith("Authentication__DaprInternal__AllowedCallers", StringComparison.Ordinal));
            eventStore["Authentication__WorkloadIssuer__ClientId"].ShouldBe("eventstore");
            ParameterResource workloadSecret = Parameter(builder, "local-auth-workload-client-secret");
            workloadSecret.Secret.ShouldBeTrue();
            eventStore["Authentication__WorkloadIssuer__ClientSecret"].ShouldBeSameAs(workloadSecret);

            IReadOnlyDictionary<string, object> sample = await GetEnvironmentAsync(Project(builder, "sample"), builder.ExecutionContext)
                .ConfigureAwait(true);
            sample.ShouldContainKey("Authentication__JwtBearer__Authority");
            sample["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("RS256");
            sample["EventStore__DomainService__AppId"].ShouldBe("sample");

            IReadOnlyDictionary<string, object> tenants = await GetEnvironmentAsync(Project(builder, "tenants"), builder.ExecutionContext)
                .ConfigureAwait(true);
            tenants["EventStore__DomainService__AppId"].ShouldBe("tenants");
            tenants["EventStore__Authentication__ClientId"].ShouldBe(HexalithEventStoreSecurityOptions.DefaultEventStoreClientId);
            tenants["EventStore__Authentication__Username"].ShouldBeSameAs(Parameter(builder, "local-auth-admin-username"));
            tenants["EventStore__Authentication__Password"].ShouldBeSameAs(Parameter(builder, "local-auth-admin-password"));
            tenants["Tenants__BootstrapGlobalAdminUserId"].ShouldBeSameAs(Parameter(builder, "local-auth-admin-user-id"));
            tenants.ShouldContainKey("EventStore__Authentication__Authority");
        }
        finally
        {
            RestoreEnvironment(original);
        }
    }

    /// <summary>Symmetric run mode gives the domain services the same per-run validation key as EventStore.</summary>
    [Fact]
    public async Task RunModel_WithoutKeycloak_DomainServicesValidateTheSymmetricContract()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey, "false");

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            ParameterResource signingKey = Parameter(builder, "local-auth-signing-key");
            foreach (string name in new[] { "eventstore", "sample", "tenants" })
            {
                IReadOnlyDictionary<string, object> environment = await GetEnvironmentAsync(Project(builder, name), builder.ExecutionContext)
                    .ConfigureAwait(true);
                environment["Authentication__JwtBearer__SigningKey"].ShouldBeSameAs(signingKey, name);
                environment["Authentication__JwtBearer__Issuer"].ShouldBe("hexalith-dev", name);
                environment["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("HS256", name);
                await AssertAppChannelTokenSharedWithSidecarOnlyAsync(builder, name).ConfigureAwait(true);
            }

            // Story 5.5 (BS-B): with no app-id grant, the Tenants bootstrap signs a short-lived delegated credential for
            // the configured administrator with the same per-run Development key; it gets no Keycloak credentials and
            // EventStore allow-lists no internal caller.
            IReadOnlyDictionary<string, object> tenants = await GetEnvironmentAsync(Project(builder, "tenants"), builder.ExecutionContext)
                .ConfigureAwait(true);
            tenants["Tenants__BootstrapGlobalAdminUserId"].ShouldBeSameAs(Parameter(builder, "local-auth-admin-user-id"));
            tenants["Authentication__JwtBearer__Audience"].ShouldBe(HexalithEventStoreSecurityOptions.DefaultAudience);
            tenants.Keys.ShouldNotContain("EventStore__Authentication__Password");
            IReadOnlyDictionary<string, object> eventStore = await GetEnvironmentAsync(Project(builder, "eventstore"), builder.ExecutionContext)
                .ConfigureAwait(true);
            eventStore["Authentication__JwtBearer__Audience"].ShouldBe(HexalithEventStoreSecurityOptions.DefaultAudience);
            eventStore.Keys.ShouldNotContain(static key => key.StartsWith(HexalithEventStoreTrustedEffectExtensions.AllowedCallersVariablePrefix, StringComparison.Ordinal));
            eventStore.Keys.ShouldNotContain(HexalithEventStoreTrustedEffectExtensions.WorkloadClientSecretVariable);
        }
        finally
        {
            RestoreEnvironment(original);
        }
    }

    /// <summary>
    /// Story 5.5 (BS-C): the checked-in realm declares a confidential, short-lived, service-account-only
    /// <c>eventstore</c> workload client that grants no audience and no operation by default. Each audience and each
    /// operation is a separate optional client scope with exactly one mapper, so a token requested for one (audience,
    /// operation) pair carries only that pair. The realm keeps Keycloak's built-in client scopes, and the rendered realm
    /// carries the generated per-run secret.
    /// </summary>
    [Fact]
    public void RealmTemplate_DeclaresAScopedShortLivedEventStoreWorkloadClient()
    {
        string sourceDirectory = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "KeycloakRealms");
        using JsonDocument template = JsonDocument.Parse(File.ReadAllText(Path.Combine(sourceDirectory, "hexalith-realm.json")));
        JsonElement root = template.RootElement;
        JsonElement client = root.GetProperty("clients").EnumerateArray()
            .Single(static candidate => candidate.GetProperty("clientId").GetString() == "eventstore");

        client.GetProperty("publicClient").GetBoolean().ShouldBeFalse();
        client.GetProperty("serviceAccountsEnabled").GetBoolean().ShouldBeTrue();
        client.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("standardFlowEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("implicitFlowEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("fullScopeAllowed").GetBoolean().ShouldBeFalse();
        client.GetProperty("secret").GetString().ShouldBe(string.Concat("__", "HEXALITH", "_WORKLOAD_CLIENT_SECRET__"));
        int.Parse(client.GetProperty("attributes").GetProperty("access.token.lifespan").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeLessThanOrEqualTo(300);

        // Nothing is granted by default: no client-level mapper and no default client scope.
        client.TryGetProperty("protocolMappers", out JsonElement clientMappers).ShouldBeFalse(clientMappers.ToString());
        client.GetProperty("defaultClientScopes").GetArrayLength().ShouldBe(0);
        string[] optionalScopes = [.. client.GetProperty("optionalClientScopes").EnumerateArray().Select(static scope => scope.GetString()!)];
        string[] expectedAudiences = ["sample", "tenants"];
        string[] expectedOperations =
        [
            "domain-service:process",
            "domain-service:replay-state",
            "domain-service:query",
            "domain-service:project",
            "domain-service:metadata",
        ];
        optionalScopes.Order(StringComparer.Ordinal).ShouldBe(
            expectedAudiences.Select(static audience => "eventstore-audience." + audience)
                .Concat(expectedOperations.Select(static operation => "eventstore-operation." + operation.Replace(':', '.')))
                .Order(StringComparer.Ordinal));
        optionalScopes.ShouldNotContain("eventstore-operation.projection.notify");
        optionalScopes.ShouldNotContain("eventstore-operation.eventstore.trusted-effect");

        // The issuer's scope naming and the realm agree.
        var issuerOptions = new Hexalith.EventStore.ServiceDefaults.Authentication.WorkloadAssertionIssuerOptions();
        foreach (string audience in expectedAudiences)
        {
            optionalScopes.ShouldContain(issuerOptions.GetAudienceScope(audience));
        }

        foreach (string operation in expectedOperations)
        {
            optionalScopes.ShouldContain(issuerOptions.GetOperationScope(operation));
        }

        JsonElement[] scopes = [.. root.GetProperty("clientScopes").EnumerateArray()];
        foreach (string scopeName in optionalScopes)
        {
            JsonElement scope = scopes.Single(candidate => candidate.GetProperty("name").GetString() == scopeName);
            JsonElement mapper = scope.GetProperty("protocolMappers").EnumerateArray().ShouldHaveSingleItem();
            JsonElement config = mapper.GetProperty("config");
            if (scopeName.StartsWith("eventstore-audience.", StringComparison.Ordinal))
            {
                mapper.GetProperty("protocolMapper").GetString().ShouldBe("oidc-audience-mapper");
                config.GetProperty("included.custom.audience").GetString().ShouldBe(scopeName["eventstore-audience.".Length..]);
            }
            else
            {
                mapper.GetProperty("protocolMapper").GetString().ShouldBe("oidc-hardcoded-claim-mapper");
                config.GetProperty("claim.name").GetString().ShouldBe("eventstore:operation");
                config.GetProperty("jsonType.label").GetString().ShouldBe("String");
                issuerOptions.GetOperationScope(config.GetProperty("claim.value").GetString()!).ShouldBe(scopeName);
            }

            config.GetProperty("access.token.claim").GetString().ShouldBe("true");
            config.GetProperty("id.token.claim").GetString().ShouldBe("false");
        }

        // Declaring client scopes would otherwise suppress Keycloak's built-in ones that the human clients rely on.
        root.GetProperty("attributes").GetProperty("CreateDefaultClientScopes").GetString().ShouldBe("true");
        string?[] claimNames = [.. scopes
            .SelectMany(static scope => scope.GetProperty("protocolMappers").EnumerateArray())
            .Select(static mapper => mapper.GetProperty("config"))
            .Where(static config => config.TryGetProperty("claim.name", out _))
            .Select(static config => config.GetProperty("claim.name").GetString())];
        claimNames.ShouldNotContain("global_admin");
        claimNames.ShouldNotContain("eventstore:tenant");

        LocalAuthenticationCredentials credentials = LocalAuthenticationCredentials.Create();
        using KeycloakRealmTemplate rendered = KeycloakRealmTemplate.Render(sourceDirectory, credentials);
        using JsonDocument realm = JsonDocument.Parse(File.ReadAllText(Path.Combine(rendered.ImportDirectory, "hexalith-realm.json")));
        string.Equals(
            realm.RootElement.GetProperty("clients").EnumerateArray()
                .Single(static candidate => candidate.GetProperty("clientId").GetString() == "eventstore")
                .GetProperty("secret").GetString(),
            credentials.WorkloadClientSecret,
            StringComparison.Ordinal).ShouldBeTrue();
        credentials.WorkloadClientSecret.Length.ShouldBeGreaterThanOrEqualTo(32);
    }

    /// <summary>
    /// Story 5.5 (P-12): publish mode wires EventStore's external workload client from parameters, the secret one
    /// marked secret, and gives the sample domain service the shared authority contract it validates assertions with.
    /// </summary>
    [Fact]
    public async Task PublishModel_WiresTheExternalWorkloadClientAndTheSampleContract()
    {
        string[] publishKeys =
        [
            "Authentication__JwtBearer__Authority",
            "Authentication__JwtBearer__Issuer",
            "Authentication__JwtBearer__ValidAudiences__0",
            "Authentication__JwtBearer__AllowedAlgorithms__0",
            "Authentication__JwtBearer__SampleUi__GrantType",
            "Authentication__JwtBearer__SampleUi__Scope",
            "Authentication__JwtBearer__AdminUi__GrantType",
            "Authentication__JwtBearer__AdminUi__Scope",
            "Parameters__external-sample-auth-client-id",
            "Parameters__external-sample-auth-client-secret",
            "Parameters__external-admin-auth-client-id",
            "Parameters__external-admin-auth-client-secret",
            "Parameters__external-eventstore-workload-client-id",
            "Parameters__external-eventstore-workload-client-secret",
        ];
        Dictionary<string, string?> original = CaptureEnvironment();
        Dictionary<string, string?> originalPublish = publishKeys.ToDictionary(static key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__Authority", "https://identity.example.test/tenant");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__Issuer", "https://identity.example.test/tenant");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__ValidAudiences__0", "primary-api");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AllowedAlgorithms__0", "RS256");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__SampleUi__GrantType", "client_credentials");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__SampleUi__Scope", "sample.read");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AdminUi__GrantType", "client_credentials");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AdminUi__Scope", "admin.read");
            Environment.SetEnvironmentVariable("Parameters__external-sample-auth-client-id", "sample-ui");
            Environment.SetEnvironmentVariable("Parameters__external-sample-auth-client-secret", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("Parameters__external-admin-auth-client-id", "admin-ui");
            Environment.SetEnvironmentVariable("Parameters__external-admin-auth-client-secret", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("Parameters__external-eventstore-workload-client-id", "eventstore");
            Environment.SetEnvironmentVariable("Parameters__external-eventstore-workload-client-secret", Guid.NewGuid().ToString("N"));

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>(["--AppHost:Operation=publish"])
                .ConfigureAwait(true);

            ParameterResource clientId = Parameter(builder, "external-eventstore-workload-client-id");
            ParameterResource clientSecret = Parameter(builder, "external-eventstore-workload-client-secret");
            clientId.Secret.ShouldBeFalse();
            clientSecret.Secret.ShouldBeTrue();
            IReadOnlyDictionary<string, object> eventStore = await GetEnvironmentAsync(Project(builder, "eventstore"), builder.ExecutionContext)
                .ConfigureAwait(true);
            eventStore[HexalithEventStoreTrustedEffectExtensions.WorkloadClientIdVariable].ShouldBeSameAs(clientId);
            eventStore[HexalithEventStoreTrustedEffectExtensions.WorkloadClientSecretVariable].ShouldBeSameAs(clientSecret);
            eventStore.Keys.ShouldNotContain(static key => key.StartsWith(HexalithEventStoreTrustedEffectExtensions.AllowedCallersVariablePrefix, StringComparison.Ordinal));

            IReadOnlyDictionary<string, object> sample = await GetEnvironmentAsync(Project(builder, "sample"), builder.ExecutionContext)
                .ConfigureAwait(true);
            sample["Authentication__JwtBearer__Authority"].ShouldBe("https://identity.example.test/tenant");
            sample["Authentication__JwtBearer__Issuer"].ShouldBe("https://identity.example.test/tenant");
            sample["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("RS256");
            sample["Authentication__JwtBearer__RequireHttpsMetadata"].ShouldBe("true");
            sample["Authentication__JwtBearer__SigningKey"].ShouldBe(string.Empty);
            sample["EventStore__DomainService__AppId"].ShouldBe("sample");
            sample.Keys.ShouldNotContain(HexalithEventStoreTrustedEffectExtensions.WorkloadClientSecretVariable);
        }
        finally
        {
            RestoreEnvironment(original);
            RestoreEnvironment(originalPublish);
        }
    }

    /// <summary>
    /// Story 5.5 (BS-A): the platform AppHost API allow-lists each trusted-effect submitter on EventStore by its workload
    /// identity, once, and gives a workload its own secret client registration.
    /// </summary>
    [Fact]
    public async Task TrustedEffectSubmitterProvisioning_AllowListsEachSubmitterAndWiresItsClient()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> eventStore = builder.AddProject<EventStoreProjectMetadata>("eventstore");
        IResourceBuilder<ProjectResource> works = builder.AddProject<EventStoreProjectMetadata>("works");
        IResourceBuilder<ParameterResource> secret = builder.AddParameter("works-workload-client-secret", secret: true);
        IResourceBuilder<ParameterResource> plain = builder.AddParameter("works-workload-plain");

        _ = eventStore
            .WithEventStoreTrustedEffectSubmitter("works")
            .WithEventStoreTrustedEffectSubmitter(" works ")
            .WithEventStoreTrustedEffectSubmitter("billing");
        _ = works.WithEventStoreWorkloadClientCredentials("works", secret);

        IReadOnlyDictionary<string, object> eventStoreEnvironment = await GetEnvironmentAsync(eventStore.Resource, builder.ExecutionContext)
            .ConfigureAwait(true);
        eventStoreEnvironment.Where(static entry => entry.Key.StartsWith(HexalithEventStoreTrustedEffectExtensions.AllowedCallersVariablePrefix, StringComparison.Ordinal))
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => entry.Value)
            .ShouldBe(["works", "billing"]);
        IReadOnlyDictionary<string, object> worksEnvironment = await GetEnvironmentAsync(works.Resource, builder.ExecutionContext)
            .ConfigureAwait(true);
        worksEnvironment[HexalithEventStoreTrustedEffectExtensions.WorkloadClientIdVariable].ShouldBe("works");
        worksEnvironment[HexalithEventStoreTrustedEffectExtensions.WorkloadClientSecretVariable].ShouldBeSameAs(secret.Resource);
        _ = Should.Throw<ArgumentException>(() => works.WithEventStoreWorkloadClientCredentials("works", plain));
    }

    private static async Task AssertAppChannelTokenSharedWithSidecarOnlyAsync(IDistributedApplicationTestingBuilder builder, string name)
    {
        ProjectResource project = Project(builder, name);
        ParameterResource channelParameter = Parameter(builder, name + "-app-api-token");
        channelParameter.Secret.ShouldBeTrue(name);
        IReadOnlyDictionary<string, object> environment = await GetEnvironmentAsync(project, builder.ExecutionContext)
            .ConfigureAwait(true);
        environment[HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable].ShouldBeSameAs(channelParameter, name);

        IDaprSidecarResource sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        var context = new EnvironmentCallbackContext(
            builder.ExecutionContext,
            sidecar,
            new Dictionary<string, object>(),
            CancellationToken.None);
        foreach (EnvironmentCallbackAnnotation annotation in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        context.EnvironmentVariables[HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable].ShouldBeSameAs(channelParameter, name);
    }

    private static ParameterResource Parameter(IDistributedApplicationTestingBuilder builder, string name)
        => builder.Resources.OfType<ParameterResource>().Single(resource => resource.Name == name);

    private static ProjectResource Project(IDistributedApplicationTestingBuilder builder, string name)
        => builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == name);

    private static async Task<IReadOnlyDictionary<string, object>> GetEnvironmentAsync(
        ProjectResource resource,
        DistributedApplicationExecutionContext executionContext)
    {
        var context = new EnvironmentCallbackContext(
            executionContext,
            resource,
            new Dictionary<string, object>(),
            CancellationToken.None);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        return context.EnvironmentVariables;
    }

    private static Dictionary<string, string?> CaptureEnvironment()
        => EnvironmentKeys.ToDictionary(static key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

    private static void ClearEnvironment()
    {
        foreach (string key in EnvironmentKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static void RestoreEnvironment(IReadOnlyDictionary<string, string?> original)
    {
        foreach (KeyValuePair<string, string?> entry in original)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }
}
