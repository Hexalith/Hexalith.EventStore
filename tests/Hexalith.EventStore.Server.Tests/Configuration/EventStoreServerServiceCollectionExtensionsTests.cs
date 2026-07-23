using System.Net;

using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class EventStoreServerServiceCollectionExtensionsTests {
    private static readonly string _strongDigestKey = Convert.ToBase64String(Enumerable.Repeat((byte)0x2A, 32).ToArray());

    [Fact]
    public void AddEventStoreServerWithoutDaprClientDoesNotActivateCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldNotContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerWithDaprClientActivatesCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerRegistersDaprGlobalPositionAllocator() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IGlobalPositionAllocator allocator = provider.GetRequiredService<IGlobalPositionAllocator>();

        _ = allocator.ShouldBeOfType<DaprGlobalPositionAllocator>();
    }

    [Fact]
    public void AddEventStoreServerRegistersGlobalPositionActor() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        ActorRuntime actorRuntime = provider.GetRequiredService<ActorRuntime>();

        actorRuntime.RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(GlobalPositionActor)
            && registration.Type.ActorTypeName == GlobalPositionActor.ActorTypeName);
    }

    [Fact]
    public void AddEventStoreServerRegistersIdempotencyAdmissionActorsWithStableNames() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        ActorRuntime actorRuntime = provider.GetRequiredService<ActorRuntime>();

        (Type ImplementationType, string ActorTypeName)[] expectedActors = [
            (typeof(IdempotencyAdmissionActor), IdempotencyAdmissionActor.ActorTypeName),
            (typeof(IdempotencyAdmissionDirectoryActor), IdempotencyAdmissionDirectoryActor.ActorTypeName),
            (typeof(IdempotencyTenantLifecycleActor), IdempotencyTenantLifecycleActor.ActorTypeName),
            (typeof(IdempotencyLegacyInventoryActor), IdempotencyLegacyInventoryActor.ActorTypeName),
        ];
        foreach ((Type implementationType, string actorTypeName) in expectedActors) {
            actorRuntime.RegisteredActors.ShouldContain(registration =>
                registration.Type.ImplementationType == implementationType
                && registration.Type.ActorTypeName == actorTypeName);
        }
    }

    [Fact]
    public void AddEventStoreServerSelectsConfigurationDigestKeyProvider() {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            CreateConfigurationDigestKeySettings());

        provider.GetRequiredService<IIdempotencyDigestKeyProvider>()
            .ShouldBeOfType<ConfigurationIdempotencyDigestKeyProvider>();
        _ = provider.GetRequiredService<IdempotencyKeyProtector>();
    }

    [Fact]
    public void AddEventStoreServerSelectsDaprSecretDigestKeyProvider() {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: true,
            CreateDaprSecretDigestKeySettings());

        provider.GetRequiredService<IIdempotencyDigestKeyProvider>()
            .ShouldBeOfType<DaprSecretIdempotencyDigestKeyProvider>();
        _ = provider.GetRequiredService<IdempotencyKeyProtector>();
    }

    [Fact]
    public void AddEventStoreServerRetainsBuiltInIdempotencyAdmissionValidator() {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:IdempotencyAdmission:Enabled"] = "true",
                ["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "v1",
                ["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration",
            })
            .Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        RegisterDevelopmentEnvironment(services);
        IValidateOptions<IdempotencyAdmissionOptions> consumerValidator =
            Substitute.For<IValidateOptions<IdempotencyAdmissionOptions>>();
        _ = consumerValidator.Validate(Arg.Any<string?>(), Arg.Any<IdempotencyAdmissionOptions>())
            .Returns(ValidateOptionsResult.Success);
        _ = services.AddSingleton(consumerValidator);
        _ = services.AddEventStoreServer(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<IdempotencyAdmissionOptions>>().Value);

        provider.GetServices<IValidateOptions<IdempotencyAdmissionOptions>>().Count().ShouldBe(2);
        exception.OptionsType.ShouldBe(typeof(IdempotencyAdmissionOptions));
        exception.Failures.ShouldContain(failure => failure.Contains("strong key", StringComparison.Ordinal));
    }

    [Fact]
    public void AddEventStoreServerRegistersDeliveryStateMachineAndReadyHealthGate() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        provider.GetRequiredService<IProjectionDeliveryStateStore>()
            .ShouldBeOfType<DaprProjectionDeliveryStateStore>();
        provider.GetRequiredService<IProjectionDeliveryIdempotencyCoordinator>()
            .ShouldBeOfType<ProjectionDeliveryIdempotencyCoordinator>();
        provider.GetRequiredService<IProjectionDeliveryReconciler>()
            .ShouldBeOfType<ProjectionDeliveryReconciler>();
        provider.GetRequiredService<IProjectionDeliveryCutover>()
            .ShouldBeOfType<ProjectionDeliveryCutover>();
        provider.GetRequiredService<IOptions<ProjectionDeliveryIdempotencyOptions>>().Value
            .CompletedReceiptLimit.ShouldBe(256);
        HealthCheckRegistration registration = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations
            .Single(value => value.Name == "projection-delivery-writer-protocol");
        registration.Tags.ShouldContain("ready");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds + 1)]
    public void AddEventStoreServerRejectsInvocationTimeoutOutsideSupportedRange(int timeoutSeconds) {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"] = timeoutSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            });

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DomainServiceOptions>>().Value);
        exception.OptionsType.ShouldBe(typeof(DomainServiceOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains(nameof(DomainServiceOptions.InvocationTimeoutSeconds), StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddEventStoreServerRejectsInvalidInvocationTimeoutAtHostStartupAsync() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(static configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "0",
                }))
            .ConfigureServices(static (context, services) =>
                _ = services.AddEventStoreServer(context.Configuration))
            .Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);
        exception.OptionsType.ShouldBe(typeof(DomainServiceOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains(nameof(DomainServiceOptions.InvocationTimeoutSeconds), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds)]
    public void AddEventStoreServerAcceptsInvocationTimeoutBoundaries(int timeoutSeconds) {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"]
                    = timeoutSeconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
            });

        provider.GetRequiredService<IOptions<DomainServiceOptions>>()
            .Value.InvocationTimeoutSeconds.ShouldBe(timeoutSeconds);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(DaprDomainServiceInvoker.HttpClientName);
        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public async Task AddEventStoreServerAcceptsOneSecondInvocationTimeoutAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "1",
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        await host.StartAsync().ConfigureAwait(true);

        host.Services.GetRequiredService<IOptions<DomainServiceOptions>>()
            .Value.InvocationTimeoutSeconds.ShouldBe(1);
        await host.StopAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AddEventStoreServerRejectsInvalidInvocationTimeoutAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "0",
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);
        exception.OptionsType.ShouldBe(typeof(DomainServiceOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains(nameof(DomainServiceOptions.InvocationTimeoutSeconds), StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddEventStoreServerRejectsInvalidIdempotencyAdmissionAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:IdempotencyAdmission:Enabled"] = "true",
            ["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "v1",
            ["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration",
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);

        exception.OptionsType.ShouldBe(typeof(IdempotencyAdmissionOptions));
        exception.Failures.ShouldContain(failure => failure.Contains("strong key", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("c2hvcnQ=")]
    public async Task AddEventStoreServerRejectsWeakConfiguredDigestKeyAtHostStartAsync(string encodedKey) {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        Dictionary<string, string?> settings = CreateConfigurationDigestKeySettings();
        settings["EventStore:IdempotencyAdmission:DigestKeys:v1"] = encodedKey;
        _ = builder.Configuration.AddInMemoryCollection(settings);
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);

        exception.OptionsType.ShouldBe(typeof(IdempotencyAdmissionOptions));
        exception.Failures.ShouldContain(failure => failure.Contains("strong key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddEventStoreServerRejectsConfiguredDigestKeyInProductionAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        _ = builder.Configuration.AddInMemoryCollection(CreateConfigurationDigestKeySettings());
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);

        exception.OptionsType.ShouldBe(typeof(IdempotencyAdmissionOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains("permitted only in Development or test environments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddEventStoreServerRejectsUndefinedDigestKeySourceAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:IdempotencyAdmission:Enabled"] = "true",
            ["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "v1",
            ["EventStore:IdempotencyAdmission:DigestKeySource"] = "999",
            ["EventStore:IdempotencyAdmission:DigestKeys:v1"] = _strongDigestKey,
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);

        exception.OptionsType.ShouldBe(typeof(IdempotencyAdmissionOptions));
        exception.Failures.ShouldContain(failure =>
            failure.Contains("digest-key source is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DomainServiceInvocationClientDoesNotInheritDefaultResilienceRetriesAsync() {
        var handler = new CountingFailureHandler();
        var services = new ServiceCollection();
        _ = services.ConfigureHttpClientDefaults(clientBuilder =>
            _ = clientBuilder.AddStandardResilienceHandler(options => {
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.MaxRetryAttempts = 3;
            }));
        _ = services.AddHttpClient(DaprDomainServiceInvoker.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        _ = services.AddLogging();
        _ = services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(DaprDomainServiceInvoker.HttpClientName);

        using HttpResponseMessage response = await client
            .PostAsync("https://domain-service.test/process", content: null)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        handler.AttemptCount.ShouldBe(1);
    }

    private static ServiceProvider BuildProvider(
        bool registerDaprClient,
        IEnumerable<KeyValuePair<string, string?>>? configurationValues = null) {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? [])
            .Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        RegisterDevelopmentEnvironment(services);
        if (registerDaprClient) {
            _ = services.AddSingleton(Substitute.For<DaprClient>());
        }

        _ = services.AddEventStoreServer(configuration);
        return services.BuildServiceProvider();
    }

    private static void RegisterDevelopmentEnvironment(IServiceCollection services) {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        _ = environment.EnvironmentName.Returns(Environments.Development);
        _ = services.AddSingleton(environment);
    }

    private static Dictionary<string, string?> CreateConfigurationDigestKeySettings() => new() {
        ["EventStore:IdempotencyAdmission:Enabled"] = "true",
        ["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "v1",
        ["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration",
        ["EventStore:IdempotencyAdmission:DigestKeys:v1"] = _strongDigestKey,
    };

    private static Dictionary<string, string?> CreateDaprSecretDigestKeySettings() => new() {
        ["EventStore:IdempotencyAdmission:Enabled"] = "true",
        ["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "v1",
        ["EventStore:IdempotencyAdmission:DigestKeySource"] = "DaprSecret",
        ["EventStore:IdempotencyAdmission:DigestKeySecretStoreName"] = "secretstore",
        ["EventStore:IdempotencyAdmission:DigestKeySecretName"] = "eventstore-idempotency",
        ["EventStore:IdempotencyAdmission:DigestKeySecretGeneration"] = "generation-1",
    };

    private sealed class CountingFailureHandler : HttpMessageHandler {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            AttemptCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
