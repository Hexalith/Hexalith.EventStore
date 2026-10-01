using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Composition of the typed-reminder runtime through the presence-keyed SDK registration.</summary>
[Collection("Reminder environment")]
public sealed class EventStoreReminderCompositionTests
{
    private const string ReminderRoute = "actors/{actorTypeName}/{actorId}/method/remind/{reminderName}";
    private const string ActorType = "WidgetReminderActor";

    /// <summary>One registration call wires options, the actor type, reconciler, filter, readiness, and the registrar.</summary>
    [Fact]
    public void AddEventStoreRemindersRegistersRuntime()
    {
        using ServiceProvider provider = CreateServices(new Dictionary<string, string?>
        {
            ["EventStore:Reminders:ActorTypeName"] = "WidgetReminderActor",
            ["EventStore:Reminders:Purposes:" + EffectKindCatalog.DateResume] = "synthetic-date-resume",
            ["EventStore:Reminders:ReconciliationInterval"] = "00:02:00",
        }).BuildServiceProvider();

        EventStoreReminderOptions options = provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value;
        options.ActorTypeName.ShouldBe("WidgetReminderActor");
        options.Purposes[EffectKindCatalog.DateResume].ShouldBe("synthetic-date-resume");
        options.ReconciliationInterval.ShouldBe(TimeSpan.FromMinutes(2));
        string? appId = Environment.GetEnvironmentVariable("DAPR_APP_ID");
        options.Workload.ShouldBe(string.IsNullOrWhiteSpace(appId) ? "widget-host" : appId);

        ActorRegistration registration = provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors
            .Single(r => r.Type.ActorTypeName == "WidgetReminderActor");
        registration.Type.ImplementationType.ShouldBe(typeof(ReminderActor));

        provider.GetRequiredService<IReminderRegistrar>().ShouldBeOfType<ReminderRegistrar>();
        provider.GetServices<IHostedService>().ShouldContain(service => service is ReminderReconciler);
        provider.GetServices<IStartupFilter>().ShouldContain(filter => filter is ReminderCallbackTokenStartupFilter);
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ReminderCoordinator>().ShouldNotBeNull();

        HealthCheckRegistration readiness = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(r => r.Name == EventStoreDomainTelemetry.RemindersUnresolvedHealthCheckName);
        readiness.Name.ShouldBe("eventstore-reminders-unresolved");
        readiness.Tags.ShouldContain("ready");
        readiness.FailureStatus.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>A second registration layers configuration without duplicating the runtime.</summary>
    [Fact]
    public void AddEventStoreRemindersIsIdempotent()
    {
        IServiceCollection services = CreateServices();
        _ = services.AddEventStoreReminders<FakeReminderIntentSource>(options => options.RetryInitialDelay = TimeSpan.FromSeconds(5));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IStartupFilter>().Count(filter => filter is ReminderCallbackTokenStartupFilter).ShouldBe(1);
        provider.GetServices<IHostedService>().Count(service => service is ReminderReconciler).ShouldBe(1);
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Count(r => r.Name == EventStoreDomainTelemetry.RemindersUnresolvedHealthCheckName).ShouldBe(1);
        provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors
            .Count(r => r.Type.ActorTypeName == ActorType).ShouldBe(1);
        provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value.RetryInitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
    }

    /// <summary>A blank Dapr application identifier does not suppress the host application-name fallback.</summary>
    [Fact]
    public void BlankDaprApplicationIdUsesHostApplicationName()
    {
        string? previous = Environment.GetEnvironmentVariable("DAPR_APP_ID");
        try
        {
            Environment.SetEnvironmentVariable("DAPR_APP_ID", " \t");
            using ServiceProvider provider = CreateServices().BuildServiceProvider();

            provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value.Workload.ShouldBe("widget-host");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAPR_APP_ID", previous);
        }
    }

    /// <summary>Invalid options fail validation instead of producing unscoped or unsafe keys.</summary>
    [Theory]
    [InlineData("ActorTypeName", "")]
    [InlineData("ActorTypeName", "bad actor:type")]
    [InlineData("ActorTypeName", "WidgetReminderActor\n")]
    [InlineData("StateStoreName", " ")]
    [InlineData("ReconciliationInterval", "00:00:00")]
    [InlineData("ReconciliationInterval", "50.00:00:00")]
    [InlineData("RetryMaxDelay", "60.00:00:00")]
    [InlineData("RetryInitialDelay", "01:00:00")]
    [InlineData("RetryInitialDelay", "00:00:00")]
    [InlineData("MaxCandidatesPerTenant", "0")]
    [InlineData("IndexWriteAttempts", "0")]
    [InlineData("IndexWriteAttempts", "101")]
    [InlineData("Purposes:works.date-resume.v1", " ")]
    [InlineData("Purposes:works.cascade-cancel.v1", "synthetic")]
    public void InvalidOptionsFailValidation(string key, string value)
    {
        using ServiceProvider provider = CreateServices(new Dictionary<string, string?>
        {
            ["EventStore:Reminders:" + key] = value,
        }).BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value);
    }

    /// <summary>The canonical host maps the Dapr actor routes only when reminders are registered.</summary>
    [Fact]
    public void UseEventStoreDomainServiceMapsReminderRoutesOnlyWhenRegistered()
    {
        WebApplication withReminders = BuildApp(registerReminders: true);
        WebApplication withoutReminders = BuildApp(registerReminders: false);

        _ = withReminders.UseEventStoreDomainService();
        _ = withoutReminders.UseEventStoreDomainService();

        CountReminderRoutes(withReminders).ShouldBe(1);
        CountReminderRoutes(withoutReminders).ShouldBe(0);
    }

    /// <summary>A host that already mapped the Dapr actor handlers stays authoritative and is not duplicated.</summary>
    [Fact]
    public void PreMappedActorHandlersAreNotDuplicated()
    {
        WebApplication app = BuildApp(registerReminders: true);
        _ = app.MapActorsHandlers();

        _ = app.UseEventStoreDomainService();
        _ = app.MapEventStoreReminders();

        CountReminderRoutes(app).ShouldBe(1);
    }

    /// <summary>Mapping reminder routes without the registration is a composition error.</summary>
    [Fact]
    public void MapEventStoreRemindersRequiresRegistration()
    {
        WebApplication app = BuildApp(registerReminders: false);

        _ = Should.Throw<InvalidOperationException>(() => app.MapEventStoreReminders());
    }

    private static IServiceCollection CreateServices(Dictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        // The actor type name has no shared default: every host names its own.
        var configuration = new Dictionary<string, string?> { ["EventStore:Reminders:ActorTypeName"] = ActorType };
        foreach (KeyValuePair<string, string?> setting in settings ?? [])
        {
            configuration[setting.Key] = setting.Value;
        }

        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.ApplicationName.Returns("widget-host");
        _ = services.AddSingleton(environment);
        _ = services.AddEventStoreReminders<FakeReminderIntentSource>();
        return services;
    }

    private static WebApplication BuildApp(bool registerReminders)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        if (registerReminders)
        {
            _ = builder.Services.AddEventStoreReminders<FakeReminderIntentSource>(options => options.ActorTypeName = ActorType);
        }

        return builder.Build();
    }

    private static int CountReminderRoutes(IEndpointRouteBuilder endpoints)
        => endpoints.DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Count(static endpoint => string.Equals(
                    endpoint.RoutePattern.RawText?.TrimStart('/'),
                    ReminderRoute,
                    StringComparison.OrdinalIgnoreCase)
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Put) != false);
}
