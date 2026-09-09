namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using System.Net;
using System.Net.Http.Headers;
using System.Text;

[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class AppHostAuthenticationModelTests
{
    private static readonly string[] EnvironmentKeys =
    [
        "SKIP_PREREQUISITE_CHECK",
        HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
        HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey,
        "AppHostTesting__FailAfterRealmRender",
        "Authentication__JwtBearer__Authority",
        "Authentication__JwtBearer__Issuer",
        "Authentication__JwtBearer__Audience",
        "Authentication__JwtBearer__ValidAudiences__0",
        "Authentication__JwtBearer__ValidAudiences__1",
        "Authentication__JwtBearer__AllowedAlgorithms__0",
        "Authentication__JwtBearer__TokenEndpoint",
        "Authentication__JwtBearer__AudienceParameterName",
        "Authentication__JwtBearer__AudienceParameterValue",
        "Authentication__JwtBearer__SampleUi__GrantType",
        "Authentication__JwtBearer__SampleUi__Scope",
        "Authentication__JwtBearer__AdminUi__GrantType",
        "Authentication__JwtBearer__AdminUi__Scope",
        "Parameters__external-sample-auth-client-id",
        "Parameters__external-sample-auth-username",
        "Parameters__external-sample-auth-password",
        "Parameters__external-sample-auth-client-secret",
        "Parameters__external-admin-auth-client-id",
        "Parameters__external-admin-auth-username",
        "Parameters__external-admin-auth-password",
        "Parameters__external-admin-auth-client-secret",
    ];

    [Fact]
    public async Task RunModel_SeparatesUiIdentities_MarksSecrets_AndCleansRealmOnDisposal()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        string[] before = GetOwnedRunDirectories();
        string? ownedDirectory = null;
        IDistributedApplicationTestingBuilder? builder = null;
        DistributedApplication? application = null;
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey,
                "false");

            builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            ParameterResource signingKey = Parameter(builder, "local-auth-signing-key");
            ParameterResource samplePassword = Parameter(builder, "local-auth-sample-password");
            ParameterResource adminPassword = Parameter(builder, "local-auth-admin-password");
            signingKey.Secret.ShouldBeTrue();
            samplePassword.Secret.ShouldBeTrue();
            adminPassword.Secret.ShouldBeTrue();

            ProjectResource sampleUi = Project(builder, "sample-blazor-ui");
            ProjectResource adminUi = Project(builder, "eventstore-admin-ui");
            IReadOnlyDictionary<string, object> sampleEnvironment = await GetEnvironmentAsync(
                sampleUi,
                builder.ExecutionContext).ConfigureAwait(true);
            IReadOnlyDictionary<string, object> adminEnvironment = await GetEnvironmentAsync(
                adminUi,
                builder.ExecutionContext).ConfigureAwait(true);

            ReferenceExpression localAuthority = sampleEnvironment["EventStore__Authentication__Authority"]
                .ShouldBeOfType<ReferenceExpression>();
            localAuthority.ValueExpression.ShouldEndWith("/realms/hexalith");
            Project(builder, "sample-api").Annotations
                .OfType<ResourceCommandAnnotation>()
                .ShouldNotContain(static annotation => string.Equals(
                    annotation.Name,
                    LocalAuthenticationTokenCommand.CommandName,
                    StringComparison.Ordinal));
            foreach (string resourceName in new[] { "eventstore", "eventstore-admin", "sample-api" })
            {
                ProjectResource resource = Project(builder, resourceName);
                IReadOnlyDictionary<string, object> environment = await GetEnvironmentAsync(
                    resource,
                    builder.ExecutionContext).ConfigureAwait(true);
                environment["Authentication__JwtBearer__Authority"].ShouldBeSameAs(localAuthority);
                environment["Authentication__JwtBearer__Issuer"].ShouldBeSameAs(localAuthority);
                environment["Authentication__JwtBearer__RequireHttpsMetadata"].ShouldBe("false");
                environment["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("RS256");
                environment["Authentication__JwtBearer__SigningKey"].ShouldBe(string.Empty);
            }

            foreach ((ProjectResource resource, IReadOnlyDictionary<string, object> environment) in
                new[] { (sampleUi, sampleEnvironment), (adminUi, adminEnvironment) })
            {
                environment["EventStore__Authentication__Authority"].ShouldBeSameAs(localAuthority);
                environment["EventStore__Authentication__Issuer"].ShouldBeSameAs(localAuthority);
                environment["EventStore__Authentication__RequireHttpsMetadata"].ShouldBe("false");
                environment["EventStore__Authentication__AllowedAlgorithms__0"].ShouldBe("RS256");
                environment["EventStore__Authentication__SigningKey"].ShouldBe(string.Empty);
                environment["EventStore__Authentication__GrantType"].ShouldBe("password");
                environment["EventStore__Authentication__ClientSecret"].ShouldBe(string.Empty);
            }

            string sampleUsername = await ResolveAsync(
                sampleEnvironment["EventStore__Authentication__Username"],
                sampleUi,
                builder.ExecutionContext).ConfigureAwait(true);
            string adminUsername = await ResolveAsync(
                adminEnvironment["EventStore__Authentication__Username"],
                adminUi,
                builder.ExecutionContext).ConfigureAwait(true);
            string samplePasswordValue = await ResolveAsync(
                sampleEnvironment["EventStore__Authentication__Password"],
                sampleUi,
                builder.ExecutionContext).ConfigureAwait(true);
            string adminPasswordValue = await ResolveAsync(
                adminEnvironment["EventStore__Authentication__Password"],
                adminUi,
                builder.ExecutionContext).ConfigureAwait(true);

            sampleUsername.ShouldBe("tenant-a-user");
            adminUsername.ShouldNotBe(sampleUsername);
            samplePasswordValue.ShouldNotBe(adminPasswordValue);
            ownedDirectory = GetOwnedRunDirectories().Except(before, StringComparer.Ordinal).ShouldHaveSingleItem();
            application = await builder.BuildAsync().ConfigureAwait(true);

        }
        finally
        {
            if (application is not null)
            {
                await application.DisposeAsync().ConfigureAwait(true);
            }

            if (builder is not null)
            {
                await builder.DisposeAsync().ConfigureAwait(true);
            }

            RestoreEnvironment(original);
        }

        Directory.Exists(ownedDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task PublishModel_UsesCanonicalAudiences_AndDistinctExternalCredentials()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__Authority", "https://identity.example.test/tenant");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__Issuer", "https://issuer.example.test/tenant");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__ValidAudiences__0", "primary-api");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__ValidAudiences__1", "secondary-api");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AllowedAlgorithms__0", "RS256");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__TokenEndpoint", "https://tokens.example.test/oauth/token");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AudienceParameterName", "resource");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AudienceParameterValue", "https://api.example.test");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__SampleUi__GrantType", "password");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__SampleUi__Scope", "sample.read");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AdminUi__GrantType", "client_credentials");
            Environment.SetEnvironmentVariable("Authentication__JwtBearer__AdminUi__Scope", "admin.read");
            Environment.SetEnvironmentVariable("Parameters__external-sample-auth-client-id", "sample-ui");
            Environment.SetEnvironmentVariable("Parameters__external-sample-auth-username", "sample-user");
            Environment.SetEnvironmentVariable("Parameters__external-sample-auth-password", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("Parameters__external-admin-auth-client-id", "admin-ui");
            Environment.SetEnvironmentVariable("Parameters__external-admin-auth-client-secret", Guid.NewGuid().ToString("N"));

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>(["--AppHost:Operation=publish"])
                .ConfigureAwait(true);

            builder.Resources.OfType<KeycloakResource>().ShouldBeEmpty();
            Project(builder, "sample-api").Annotations
                .OfType<ResourceCommandAnnotation>()
                .ShouldNotContain(static annotation => string.Equals(
                    annotation.Name,
                    LocalAuthenticationTokenCommand.CommandName,
                    StringComparison.Ordinal));
            Parameter(builder, "external-sample-auth-client-id").Secret.ShouldBeFalse();
            Parameter(builder, "external-sample-auth-username").Secret.ShouldBeTrue();
            Parameter(builder, "external-sample-auth-password").Secret.ShouldBeTrue();
            Parameter(builder, "external-admin-auth-client-id").Secret.ShouldBeFalse();
            Parameter(builder, "external-admin-auth-client-secret").Secret.ShouldBeTrue();

            foreach (string resourceName in new[] { "eventstore", "eventstore-admin", "sample-api" })
            {
                ProjectResource resource = Project(builder, resourceName);
                IReadOnlyDictionary<string, object> environment = await GetEnvironmentAsync(
                    resource,
                    builder.ExecutionContext).ConfigureAwait(true);
                environment["Authentication__JwtBearer__Audience"].ShouldBe("primary-api");
                environment["Authentication__JwtBearer__ValidAudiences__0"].ShouldBe("primary-api");
                environment["Authentication__JwtBearer__ValidAudiences__1"].ShouldBe("secondary-api");
                environment["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("RS256");
                environment["Authentication__JwtBearer__Authority"].ShouldBe("https://identity.example.test/tenant");
                environment["Authentication__JwtBearer__Issuer"].ShouldBe("https://issuer.example.test/tenant");
                environment["Authentication__JwtBearer__RequireHttpsMetadata"].ShouldBe("true");
                environment["Authentication__JwtBearer__SigningKey"].ShouldBe(string.Empty);
            }

            IReadOnlyDictionary<string, object> sampleEnvironment = await GetEnvironmentAsync(
                Project(builder, "sample-blazor-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            IReadOnlyDictionary<string, object> adminEnvironment = await GetEnvironmentAsync(
                Project(builder, "eventstore-admin-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            sampleEnvironment["EventStore__Authentication__Audience"].ShouldBe("primary-api");
            adminEnvironment["EventStore__Authentication__Audience"].ShouldBe("primary-api");
            sampleEnvironment["EventStore__Authentication__Authority"].ShouldBe("https://identity.example.test/tenant");
            adminEnvironment["EventStore__Authentication__Authority"].ShouldBe("https://identity.example.test/tenant");
            sampleEnvironment["EventStore__Authentication__Issuer"].ShouldBe("https://issuer.example.test/tenant");
            adminEnvironment["EventStore__Authentication__Issuer"].ShouldBe("https://issuer.example.test/tenant");
            sampleEnvironment["EventStore__Authentication__RequireHttpsMetadata"].ShouldBe("true");
            adminEnvironment["EventStore__Authentication__RequireHttpsMetadata"].ShouldBe("true");
            sampleEnvironment["EventStore__Authentication__AllowedAlgorithms__0"].ShouldBe("RS256");
            adminEnvironment["EventStore__Authentication__AllowedAlgorithms__0"].ShouldBe("RS256");
            sampleEnvironment["EventStore__Authentication__SigningKey"].ShouldBe(string.Empty);
            adminEnvironment["EventStore__Authentication__SigningKey"].ShouldBe(string.Empty);
            sampleEnvironment["EventStore__Authentication__TokenEndpoint"]
                .ShouldBe("https://tokens.example.test/oauth/token");
            adminEnvironment["EventStore__Authentication__TokenEndpoint"]
                .ShouldBe("https://tokens.example.test/oauth/token");
            sampleEnvironment["EventStore__Authentication__Scope"].ShouldBe("sample.read");
            adminEnvironment["EventStore__Authentication__Scope"].ShouldBe("admin.read");
            sampleEnvironment["EventStore__Authentication__GrantType"].ShouldBe("password");
            adminEnvironment["EventStore__Authentication__GrantType"].ShouldBe("client_credentials");
            sampleEnvironment["EventStore__Authentication__AudienceParameterName"].ShouldBe("resource");
            adminEnvironment["EventStore__Authentication__AudienceParameterName"].ShouldBe("resource");
            sampleEnvironment["EventStore__Authentication__AudienceParameterValue"].ShouldBe("https://api.example.test");
            adminEnvironment["EventStore__Authentication__AudienceParameterValue"].ShouldBe("https://api.example.test");
            sampleEnvironment.ShouldContainKey("EventStore__Authentication__Username");
            sampleEnvironment.ShouldContainKey("EventStore__Authentication__Password");
            sampleEnvironment["EventStore__Authentication__ClientSecret"].ShouldBe(string.Empty);
            adminEnvironment["EventStore__Authentication__Username"].ShouldBe(string.Empty);
            adminEnvironment["EventStore__Authentication__Password"].ShouldBe(string.Empty);
            adminEnvironment.ShouldContainKey("EventStore__Authentication__ClientSecret");

            string sampleClientId = await ResolveAsync(
                sampleEnvironment["EventStore__Authentication__ClientId"],
                Project(builder, "sample-blazor-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            string adminClientId = await ResolveAsync(
                adminEnvironment["EventStore__Authentication__ClientId"],
                Project(builder, "eventstore-admin-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            sampleClientId.ShouldBe("sample-ui");
            adminClientId.ShouldBe("admin-ui");
            sampleClientId.ShouldNotBe(adminClientId);

            string sampleUsername = await ResolveAsync(
                sampleEnvironment["EventStore__Authentication__Username"],
                Project(builder, "sample-blazor-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            string samplePassword = await ResolveAsync(
                sampleEnvironment["EventStore__Authentication__Password"],
                Project(builder, "sample-blazor-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            string adminClientSecret = await ResolveAsync(
                adminEnvironment["EventStore__Authentication__ClientSecret"],
                Project(builder, "eventstore-admin-ui"),
                builder.ExecutionContext).ConfigureAwait(true);
            sampleUsername.ShouldBe("sample-user");
            samplePassword.ShouldBe(Environment.GetEnvironmentVariable("Parameters__external-sample-auth-password"));
            adminClientSecret.ShouldBe(Environment.GetEnvironmentVariable("Parameters__external-admin-auth-client-secret"));
            samplePassword.ShouldNotBe(adminClientSecret);
        }
        finally
        {
            RestoreEnvironment(original);
        }
    }

    [Fact]
    public async Task RunModel_WithoutKeycloak_ExposesOnlyATokenCommand_ThatAuthorizesAProtectedRequest()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        using LocalAuthenticationTestInvocation invocation =
            LocalAuthenticationCredentials.RegisterTestInvocation(signingKey);
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "false");
            invocation.Activate();

            await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(true);

            builder.Resources.OfType<KeycloakResource>().ShouldBeEmpty();
            ProjectResource sampleApi = Project(builder, "sample-api");
            ParameterResource signingKeyParameter = Parameter(builder, "local-auth-signing-key");
            IReadOnlyDictionary<string, object> environment = await GetEnvironmentAsync(
                sampleApi,
                builder.ExecutionContext).ConfigureAwait(true);
            environment["Authentication__JwtBearer__SigningKey"].ShouldBeSameAs(signingKeyParameter);
            environment.Values.OfType<string>().ShouldNotContain(signingKey);
            environment["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("HS256");

            ResourceCommandAnnotation command = sampleApi.Annotations
                .OfType<ResourceCommandAnnotation>()
                .ShouldHaveSingleItem();
            command.Name.ShouldBe(LocalAuthenticationTokenCommand.CommandName);
            var logger = new RecordingLogger();
            ExecuteCommandResult commandResult = await command.ExecuteCommand(
                new ExecuteCommandContext
                {
                    Services = new ServiceCollection().BuildServiceProvider(),
                    ResourceName = sampleApi.Name,
                    CancellationToken = TestContext.Current.CancellationToken,
                    Logger = logger,
                    Arguments = new InteractionInputCollection([]),
                }).ConfigureAwait(true);

            commandResult.Success.ShouldBeTrue();
            commandResult.Message.ShouldBe(string.Empty);
            commandResult.Data.ShouldNotBeNull();
            commandResult.Data.Format.ShouldBe(CommandResultFormat.Text);
            string token = commandResult.Data.Value;
            token.Split('.').Length.ShouldBe(3);
            token.ShouldNotContain(signingKey);
            logger.Messages.ShouldBeEmpty();

            WebApplicationBuilder webBuilder = WebApplication.CreateSlimBuilder();
            webBuilder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            _ = webBuilder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "hexalith-dev",
                    ValidateAudience = true,
                    ValidAudience = HexalithEventStoreSecurityOptions.DefaultAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                });
            _ = webBuilder.Services.AddAuthorization();
            await using WebApplication webApplication = webBuilder.Build();
            _ = webApplication.UseAuthentication();
            _ = webApplication.UseAuthorization();
            _ = webApplication.MapGet("/protected", static () => string.Empty)
                .RequireAuthorization();
            await webApplication.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string address = webApplication.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await client
                .GetAsync("/protected", TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            RestoreEnvironment(original);
        }
    }

    [Fact]
    public async Task RunModel_WhenConstructionFailsAfterRealmRender_CleansOwnedDirectory()
    {
        Dictionary<string, string?> original = CaptureEnvironment();
        string[] before = GetOwnedRunDirectories();
        try
        {
            ClearEnvironment();
            Environment.SetEnvironmentVariable("SKIP_PREREQUISITE_CHECK", "true");
            Environment.SetEnvironmentVariable(
                HexalithEventStoreSecurityOptions.DefaultEnableKeycloakConfigurationKey,
                "true");
            Environment.SetEnvironmentVariable("AppHostTesting__FailAfterRealmRender", "true");

            _ = await Should.ThrowAsync<Exception>(() => DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>());

            GetOwnedRunDirectories().ShouldBe(before, ignoreOrder: true);
        }
        finally
        {
            RestoreEnvironment(original);
        }
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

    private static async Task<string> ResolveAsync(
        object value,
        IResource caller,
        DistributedApplicationExecutionContext executionContext)
    {
        if (value is not IValueProvider provider)
        {
            return value.ToString() ?? string.Empty;
        }

        return await provider.GetValueAsync(
            new ValueProviderContext
            {
                Caller = caller,
                ExecutionContext = executionContext,
            },
            CancellationToken.None).ConfigureAwait(true) ?? string.Empty;
    }

    private static Dictionary<string, string?> CaptureEnvironment()
        => EnvironmentKeys.ToDictionary(
            static key => key,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);

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

    private static string[] GetOwnedRunDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), "hexalith-eventstore-keycloak");
        return Directory.Exists(root)
            ? Directory.GetDirectories(root, "run-*", SearchOption.TopDirectoryOnly)
            : [];
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
