using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using FluentValidation;

using Hexalith.EventStore;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.SignalRHub;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderVerificationHost : IAsyncDisposable
{
    private readonly WebApplication _application;

    private ProviderVerificationHost(WebApplication application, Uri baseAddress)
    {
        _application = application;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public static async Task<ProviderVerificationHost> StartAsync(
        ProviderStateCoordinator coordinator,
        string repositoryRoot,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken,
        ProviderVerificationTimeline timeline,
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>>? readinessProbeAsync = null)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        string contentRoot = Path.Combine(repositoryRoot, "src", "Hexalith.EventStore");
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CommandsController).Assembly.GetName().Name,
            ContentRootPath = contentRoot,
            EnvironmentName = Environments.Development,
            Args = ["--urls=http://127.0.0.1:0"],
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "provider-verification",
            ["Authentication:JwtBearer:Audience"] = "provider-verification",
            ["Authentication:JwtBearer:SigningKey"] = "ProviderVerificationSigningKey-AtLeast32Chars!",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["EventStore:OpenApi:Enabled"] = "false",
            ["EventStore:SignalR:Enabled"] = "false",
            ["EventStore:RateLimiting:PermitLimit"] = "10000",
            ["EventStore:RateLimiting:ConsumerPermitLimit"] = "10000",
            ["EventStore:Backpressure:RetryAfterSeconds"] = "5",
        });

        builder.AddServiceDefaults();
        builder.Services.AddDaprClient();
        builder.Services.AddHealthChecks().AddEventStoreDaprHealthChecks();
        builder.Services.AddEventStore();
        builder.Services.AddEventStoreServer(builder.Configuration);
        builder.Services.AddEventStoreDomainQueryRouting();
        builder.Services.AddEventStoreSignalR(builder.Configuration);
        ConfigureOverrides(builder.Services, coordinator);
        builder.Logging.ClearProviders();

        WebApplication application = builder.Build();
        application.UseMiddleware<CorrelationIdMiddleware>();
        application.UseExceptionHandler();
        application.MapDefaultEndpoints();
        application.UseAuthentication();
        application.UseRateLimiter();
        application.UseAuthorization();
        application.UseCloudEvents();
        application.MapControllers();
        application.MapErrorReferences();
        application.MapApiVersionFallback();
        application.MapSubscribeHandler();
        application.MapActorsHandlers();
        application.MapApplicationRuntimeProofEndpoints();
        MapProviderStateEndpoint(application, coordinator);

        Uri? baseAddress = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(startupTimeout);
            await application.StartAsync(timeout.Token).ConfigureAwait(false);
            IServerAddressesFeature feature = application.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("server-address-unavailable");
            string address = feature.Addresses.Single(value => value.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
            baseAddress = new Uri(address, UriKind.Absolute);
            if (!IPAddress.TryParse(baseAddress.Host, out IPAddress? ipAddress)
                || !IPAddress.Loopback.Equals(ipAddress)
                || baseAddress.Port <= 0)
            {
                throw new InvalidOperationException("server-address-invalid");
            }

            timeline.MarkHostBound();
            timeline.CompleteStartup("startup.succeeded");
            timeline.BeginReadiness();
            using var client = new HttpClient { BaseAddress = baseAddress, Timeout = startupTimeout };
            readinessProbeAsync ??= static (httpClient, token) => httpClient.GetAsync("/ready", token);
            using HttpResponseMessage ready = await readinessProbeAsync(client, timeout.Token).ConfigureAwait(false);
            if (!ready.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("readiness-failed");
            }

            timeline.CompleteReadiness("readiness.succeeded");
            return new ProviderVerificationHost(application, baseAddress);
        }
        catch
        {
            timeline.CompletePendingFailures();
            timeline.BeginCleanup();
            bool cleanupSucceeded = true;
            bool hostStopped = false;
            try
            {
                using var cleanup = new CancellationTokenSource(startupTimeout);
                await application.StopAsync(cleanup.Token).ConfigureAwait(false);
                hostStopped = true;
            }
            catch (Exception)
            {
                cleanupSucceeded = false;
                // The original startup/readiness failure remains the stable run outcome.
            }

            try
            {
                await application.DisposeAsync().AsTask().WaitAsync(startupTimeout).ConfigureAwait(false);
            }
            catch (Exception)
            {
                cleanupSucceeded = false;
            }

            bool portClosed = baseAddress is not null
                && await IsPortClosedAsync(baseAddress, startupTimeout).ConfigureAwait(false);
            if (baseAddress is not null && !portClosed)
            {
                cleanupSucceeded = false;
            }

            timeline.MarkHostCleanup(timeline.HostBound && hostStopped, timeline.HostBound && portClosed);
            timeline.CompleteCleanup(cleanupSucceeded ? "cleanup.succeeded" : "cleanup.failed");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _application.DisposeAsync().ConfigureAwait(false);
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        await _application.StopAsync(cancellation.Token).ConfigureAwait(false);
    }

    internal static async Task<bool> IsPortClosedAsync(Uri address, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        do
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                await client.ConnectAsync(address.Host, address.Port)
                    .WaitAsync(TimeSpan.FromMilliseconds(200))
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SocketException or TimeoutException)
            {
                return true;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return false;
    }

    private static void ConfigureOverrides(IServiceCollection services, ProviderStateCoordinator coordinator)
    {
        services.AddSingleton(coordinator);
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ProviderVerificationAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = ProviderVerificationAuthenticationHandler.SchemeName;
                options.DefaultScheme = ProviderVerificationAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, ProviderVerificationAuthenticationHandler>(
                ProviderVerificationAuthenticationHandler.SchemeName,
                _ => { });

        services.RemoveAll<ITenantValidator>();
        services.RemoveAll<IRbacValidator>();
        services.AddSingleton<StatefulAuthorizationValidator>();
        services.AddSingleton<ITenantValidator>(provider => provider.GetRequiredService<StatefulAuthorizationValidator>());
        services.AddSingleton<IRbacValidator>(provider => provider.GetRequiredService<StatefulAuthorizationValidator>());

        services.RemoveAll<ICommandRouter>();
        services.AddSingleton<ICommandRouter, StatefulCommandRouter>();
        services.RemoveAll<IQueryRouter>();
        services.AddSingleton<IQueryRouter, StatefulQueryRouter>();
        services.RemoveAll<IETagService>();
        services.AddSingleton<IETagService, StatefulETagService>();
        services.AddSingleton<IValidator<SubmitCommand>, StatefulCommandValidator>();

        services.RemoveAll<ICommandStatusStore>();
        services.AddSingleton<ICommandStatusStore, InMemoryCommandStatusStore>();
        services.RemoveAll<ICommandArchiveStore>();
        services.AddSingleton<ICommandArchiveStore, InMemoryCommandArchiveStore>();
        services.RemoveAll<ICommandCorrelationIndex>();
        services.AddSingleton<ICommandCorrelationIndex, InMemoryCommandCorrelationIndex>();
        services.RemoveAll<ICommandActivityTracker>();
        services.AddSingleton<ICommandActivityTracker, NoOpCommandActivityTracker>();
        services.RemoveAll<IStreamActivityTracker>();
        services.AddSingleton<IStreamActivityTracker, NoOpStreamActivityTracker>();
        _ = services.AddHealthChecks().AddCheck(
            "provider-verification-ready",
            static () => HealthCheckResult.Healthy(),
            tags: ["ready"]);
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            HealthCheckRegistration[] externalReadinessChecks = options.Registrations
                .Where(registration => registration.Tags.Contains("ready")
                    && registration.Name != "provider-verification-ready")
                .ToArray();
            foreach (HealthCheckRegistration registration in externalReadinessChecks)
            {
                _ = options.Registrations.Remove(registration);
            }
        });
        services.RemoveAll<IHostedService>();
    }

    private static void MapProviderStateEndpoint(WebApplication application, ProviderStateCoordinator coordinator)
    {
        application.MapPost("/__provider-state", async (HttpContext context) =>
        {
            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    new JsonDocumentOptions { MaxDepth = 8 },
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                string? state = root.TryGetProperty("state", out JsonElement stateElement)
                    ? stateElement.GetString()
                    : null;
                string action = root.TryGetProperty("action", out JsonElement actionElement)
                    ? actionElement.GetString() ?? "setup"
                    : "setup";
                if (string.IsNullOrWhiteSpace(state))
                {
                    return Results.BadRequest();
                }

                string code = await coordinator.ApplyAsync(state, action, context.RequestAborted).ConfigureAwait(false);
                return code is "state.setup.succeeded" or "state.teardown.succeeded"
                    ? Results.Ok()
                    : Results.BadRequest();
            }
        });
    }
}
