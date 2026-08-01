namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

public sealed class HexalithEventStoreJwtAuthenticationTests
{
    private const string AuthorityKey = "Authentication__JwtBearer__Authority";
    private const string AudienceKey = "Authentication__JwtBearer__Audience";
    private const string IssuerKey = "Authentication__JwtBearer__Issuer";
    private const string RequireHttpsMetadataKey = "Authentication__JwtBearer__RequireHttpsMetadata";
    private const string SigningKey = "Authentication__JwtBearer__SigningKey";
    private const string ValidAudiencePrefix = "Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__";

    [Fact]
    public async Task WithEventStoreJwtAuthentication_WhenRunning_UsesLocalSecurityAndOrderedAudiences()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: false);
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity(
            new HexalithEventStoreSecurityOptions { RequireHttpsMetadata = false })!;
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("parties");

        _ = resource.WithEventStoreJwtAuthentication(
            security,
            new HexalithEventStoreJwtAuthenticationOptions
            {
                PrimaryAudience = "hexalith-parties",
                ValidAudiences = ["hexalith-eventstore", "hexalith-parties", "hexalith-eventstore"],
            });

        KeyValuePair<string, object>[] environment = await GetJwtEnvironmentAsync(resource.Resource, builder.ExecutionContext);

        environment.Select(static entry => entry.Key).ShouldBe(
        [
            AuthorityKey,
            IssuerKey,
            AudienceKey,
            ValidAudiencePrefix + "0",
            ValidAudiencePrefix + "1",
            RequireHttpsMetadataKey,
            SigningKey,
        ]);
        environment[0].Value.ShouldBeSameAs(security.RealmUrl);
        environment[1].Value.ShouldBeSameAs(security.RealmUrl);
        environment[2].Value.ShouldBe("hexalith-parties");
        environment[3].Value.ShouldBe("hexalith-parties");
        environment[4].Value.ShouldBe("hexalith-eventstore");
        environment[5].Value.ShouldBe("false");
        environment[6].Value.ShouldBe(string.Empty);
        GetReferencedResourceNames(resource.Resource).ShouldContain(security.Keycloak.Resource.Name);
        GetWaitedResourceNames(resource.Resource).ShouldBe([security.Keycloak.Resource.Name]);
    }

    [Fact]
    public async Task WithEventStoreJwtAuthentication_WhenPublishing_UsesExternalSecurityWithoutKeycloakDependency()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<KeycloakResource> ignoredKeycloak = builder.AddKeycloak("ignored-keycloak", 8181);
        var ignoredLocalSecurity = new HexalithEventStoreSecurityResources(
            ignoredKeycloak,
            ReferenceExpression.Create($"{ignoredKeycloak.GetEndpoint("http")}/realms/ignored"),
            "ignored-audience",
            false);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("parties-mcp");

        _ = resource.WithEventStoreJwtAuthentication(
            ignoredLocalSecurity,
            new HexalithEventStoreJwtAuthenticationOptions
            {
                PrimaryAudience = "hexalith-parties-mcp",
                ValidAudiences = ["hexalith-eventstore", "hexalith-tenants"],
                ExternalAuthority = "https://identity.example.com/realms/hexalith",
                ExternalIssuer = "https://issuer.example.com/realms/hexalith",
            });

        KeyValuePair<string, object>[] environment = await GetJwtEnvironmentAsync(resource.Resource, builder.ExecutionContext);

        environment.Select(static entry => entry.Key).ShouldBe(
        [
            AuthorityKey,
            IssuerKey,
            AudienceKey,
            ValidAudiencePrefix + "0",
            ValidAudiencePrefix + "1",
            ValidAudiencePrefix + "2",
            RequireHttpsMetadataKey,
            SigningKey,
        ]);
        environment.Select(static entry => entry.Value).ShouldBe(
        [
            "https://identity.example.com/realms/hexalith",
            "https://issuer.example.com/realms/hexalith",
            "hexalith-parties-mcp",
            "hexalith-parties-mcp",
            "hexalith-eventstore",
            "hexalith-tenants",
            "true",
            string.Empty,
        ]);
        builder.Resources.ShouldContain(ignoredKeycloak.Resource);
        GetReferencedResourceNames(resource.Resource).ShouldBeEmpty();
        GetWaitedResourceNames(resource.Resource).ShouldBeEmpty();
    }

    [Fact]
    public async Task WithEventStoreJwtAuthentication_WhenAudiencesRepeat_PreservesFirstSeenOrder()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("tenants");

        _ = resource.WithEventStoreJwtAuthentication(
            null,
            CreatePublishOptions(
                " hexalith-tenants ",
                ["hexalith-eventstore", "hexalith-tenants", "hexalith-eventstore", " hexalith-parties "]));

        KeyValuePair<string, object>[] environment = await GetJwtEnvironmentAsync(resource.Resource, builder.ExecutionContext);

        environment
            .Where(static entry => entry.Key.StartsWith(ValidAudiencePrefix, StringComparison.Ordinal))
            .Select(static entry => entry.Value)
            .ShouldBe(["hexalith-tenants", "hexalith-eventstore", "hexalith-parties"]);
    }

    [Theory]
    [InlineData(null, "https://issuer.example.com")]
    [InlineData("", "https://issuer.example.com")]
    [InlineData("identity.example.com", "https://issuer.example.com")]
    [InlineData("http://identity.example.com", "https://issuer.example.com")]
    [InlineData("https://user:password@identity.example.com", "https://issuer.example.com")]
    [InlineData("https://identity.example.com?tenant=hexalith", "https://issuer.example.com")]
    [InlineData("https://identity.example.com#realm", "https://issuer.example.com")]
    [InlineData("https://identity.example.com", null)]
    [InlineData("https://identity.example.com", "")]
    [InlineData("https://identity.example.com", "issuer.example.com")]
    [InlineData("https://identity.example.com", "http://issuer.example.com")]
    [InlineData("https://identity.example.com", "https://user:password@issuer.example.com")]
    [InlineData("https://identity.example.com", "https://issuer.example.com?tenant=hexalith")]
    [InlineData("https://identity.example.com", "https://issuer.example.com#realm")]
    public void WithEventStoreJwtAuthentication_WhenExternalEndpointInvalid_FailsBeforeMutation(
        string? authority,
        string? issuer)
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("invalid-endpoint");
        int annotationCount = resource.Resource.Annotations.Count;
        var options = new HexalithEventStoreJwtAuthenticationOptions
        {
            PrimaryAudience = "hexalith-parties",
            ExternalAuthority = authority,
            ExternalIssuer = issuer,
        };

        _ = Should.Throw<ArgumentException>(() => resource.WithEventStoreJwtAuthentication(null, options));

        resource.Resource.Annotations.Count.ShouldBe(annotationCount);
    }

    [Fact]
    public void WithEventStoreJwtAuthentication_WhenConfiguredTwice_FailsBeforeSecondMutation()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("duplicate-registration");

        _ = resource.WithEventStoreJwtAuthentication(
            null,
            CreatePublishOptions("hexalith-parties", ["hexalith-eventstore", "hexalith-tenants"]));
        int annotationCount = resource.Resource.Annotations.Count;

        _ = Should.Throw<InvalidOperationException>(() => resource.WithEventStoreJwtAuthentication(
            null,
            CreatePublishOptions("hexalith-parties", [])));

        resource.Resource.Annotations.Count.ShouldBe(annotationCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithEventStoreJwtAuthentication_WhenPrimaryAudienceBlank_FailsBeforeMutation(string? audience)
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("invalid-primary-audience");
        int annotationCount = resource.Resource.Annotations.Count;
        HexalithEventStoreJwtAuthenticationOptions options = CreatePublishOptions(audience!, []);

        _ = Should.Throw<ArgumentException>(() => resource.WithEventStoreJwtAuthentication(null, options));

        resource.Resource.Annotations.Count.ShouldBe(annotationCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithEventStoreJwtAuthentication_WhenValidAudienceBlank_FailsBeforeMutation(string audience)
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: true);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("invalid-valid-audience");
        int annotationCount = resource.Resource.Annotations.Count;
        HexalithEventStoreJwtAuthenticationOptions options = CreatePublishOptions("hexalith-parties", [audience]);

        _ = Should.Throw<ArgumentException>(() => resource.WithEventStoreJwtAuthentication(null, options));

        resource.Resource.Annotations.Count.ShouldBe(annotationCount);
    }

    [Fact]
    public void WithEventStoreJwtAuthentication_WhenRunSecurityMissing_FailsBeforeMutation()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: false);
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("missing-security");
        int annotationCount = resource.Resource.Annotations.Count;

        _ = Should.Throw<ArgumentNullException>(() => resource.WithEventStoreJwtAuthentication(
            null,
            new HexalithEventStoreJwtAuthenticationOptions { PrimaryAudience = "hexalith-parties" }));

        resource.Resource.Annotations.Count.ShouldBe(annotationCount);
    }

    [Fact]
    public void HexalithEventStoreJwtAuthenticationOptions_WhenInspected_ContainsOnlyValidationSettings()
    {
        string[] propertyNames = typeof(HexalithEventStoreJwtAuthenticationOptions)
            .GetProperties()
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        propertyNames.ShouldBe(["ExternalAuthority", "ExternalIssuer", "PrimaryAudience", "ValidAudiences"]);
        propertyNames.ShouldNotContain(static name =>
            name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SigningKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithJwtBearerSecurity_WhenCalledWithLegacySignature_RetainsLocalBehavior()
    {
        IDistributedApplicationBuilder builder = CreateBuilder(publish: false);
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey] = "true";
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey] = "false";
        HexalithEventStoreSecurityResources security = builder.AddHexalithEventStoreSecurity()!;
        IResourceBuilder<ProjectResource> resource = builder.AddProject<EventStoreProjectMetadata>("legacy");

        _ = resource.WithJwtBearerSecurity(security);

        KeyValuePair<string, object>[] environment = await GetJwtEnvironmentAsync(resource.Resource, builder.ExecutionContext);

        environment.Select(static entry => entry.Key).ShouldBe(
        [
            AuthorityKey,
            IssuerKey,
            AudienceKey,
            RequireHttpsMetadataKey,
            SigningKey,
        ]);
        environment[0].Value.ShouldBeSameAs(security.RealmUrl);
        environment[1].Value.ShouldBeSameAs(security.RealmUrl);
        environment[2].Value.ShouldBe(security.Audience);
        environment[3].Value.ShouldBe("false");
        environment[4].Value.ShouldBe(string.Empty);
        GetWaitedResourceNames(resource.Resource).ShouldBe([security.Keycloak.Resource.Name]);
    }

    private static HexalithEventStoreJwtAuthenticationOptions CreatePublishOptions(
        string primaryAudience,
        IReadOnlyList<string> validAudiences)
        => new()
        {
            PrimaryAudience = primaryAudience,
            ValidAudiences = validAudiences,
            ExternalAuthority = "https://identity.example.com/realms/hexalith",
            ExternalIssuer = "https://issuer.example.com/realms/hexalith",
        };

    private static IDistributedApplicationBuilder CreateBuilder(bool publish)
        => DistributedApplication.CreateBuilder(
            publish ? ["--AppHost:Operation=publish"] : []);

    private static async Task<KeyValuePair<string, object>[]> GetJwtEnvironmentAsync(
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

        return [.. context.EnvironmentVariables.Where(static entry =>
            entry.Key.StartsWith("Authentication__JwtBearer__", StringComparison.Ordinal))];
    }

    private static string[] GetReferencedResourceNames(ProjectResource resource)
        => [.. resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(static annotation => string.Equals(annotation.Type, "Reference", StringComparison.Ordinal))
            .Select(static annotation => annotation.Resource.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static string[] GetWaitedResourceNames(ProjectResource resource)
        => [.. resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(static annotation => annotation.Resource.Name)
            .Order(StringComparer.Ordinal)];
}
