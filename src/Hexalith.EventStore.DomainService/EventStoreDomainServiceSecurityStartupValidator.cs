using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Fails domain-service startup, before the server listens, when the route inventory or the internal-credential
/// configuration would leave a protected route weaker than its contract.
/// </summary>
/// <remarks>
/// Route-inventory violations always fail startup. Missing credential configuration (application-channel token,
/// shared JWT contract, workload audience or callers) fails startup outside Development; in Development it is
/// reported and every protected request is denied at runtime instead.
/// </remarks>
internal sealed partial class EventStoreDomainServiceSecurityStartupValidator(
    EventStoreDomainServiceEndpointSource endpointSource,
    IOptions<AuthorizationOptions> authorizationOptions,
    IOptionsMonitor<WorkloadAuthenticationOptions> workloadOptions,
    IOptionsMonitor<JwtBearerAuthenticationOptions> contractOptions,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<EventStoreDomainServiceSecurityStartupValidator> logger) : IHostedLifecycleService
{
    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        IEndpointRouteBuilder? endpoints = endpointSource.Endpoints;
        if (endpoints is not null)
        {
            var inventory = endpoints.DataSources.SelectMany(static source => source.Endpoints).ToArray();
            List<string> violations = [.. EventStoreDomainServiceEndpointInventory.Validate(inventory, authorizationOptions.Value.FallbackPolicy)];
            foreach (RouteEndpoint endpoint in inventory.OfType<RouteEndpoint>())
            {
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
                if (EventStoreDomainServiceRoutes.TryGet(endpoint.RoutePattern.RawText, out _)
                    && (methods is null || methods.HttpMethods.Count == 0 || methods.HttpMethods.Contains("POST", StringComparer.OrdinalIgnoreCase))
                    && endpoint.Metadata.GetMetadata<DomainRouteCatalogGuardMetadata>() is null)
                    violations.Add($"{endpoint.RoutePattern.RawText}: canonical POST overrides must install the receiver catalog guard via RequireEventStoreDomainServicePolicy().");
            }
            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    "Domain-service route inventory violates the internal trust boundary: " + string.Join(" ", violations));
            }
        }

        List<string> failures = [];
        if (string.IsNullOrWhiteSpace(configuration[DaprAppChannelToken.ConfigurationKey]))
        {
            failures.Add($"{DaprAppChannelToken.ConfigurationKey} must be configured.");
        }

        string? workloadFailure = workloadOptions.Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme).GetConfigurationFailure();
        if (workloadFailure is not null)
        {
            failures.Add("Authentication:Workload: " + workloadFailure);
        }

        ValidateOptionsResult contract = JwtBearerAuthenticationContract.Validate(
            contractOptions.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName),
            environment,
            EventStoreWorkloadAuthenticationDefaults.JwtContractSection);
        if (contract.Failed)
        {
            failures.Add(contract.FailureMessage);
        }

        if (failures.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Domain-service internal credentials are not configured: " + string.Join(" ", failures));
        }

        LogDevelopmentCredentialGap(logger, failures.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 5531,
        Level = LogLevel.Warning,
        Message = "Domain-service internal credential configuration is incomplete in Development ({FailureCount} gap(s)); configure APP_API_TOKEN, Authentication:JwtBearer, and Authentication:Workload before exposing this host. Protected requests the configuration cannot verify are denied.")]
    private static partial void LogDevelopmentCredentialGap(ILogger logger, int failureCount);
}
