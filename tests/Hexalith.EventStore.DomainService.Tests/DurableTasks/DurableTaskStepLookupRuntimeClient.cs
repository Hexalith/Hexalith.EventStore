using System.Net.Http.Json;

using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Calls the isolated G-1 host by app ID with an assertion from the configured HTTPS issuer.</summary>
internal sealed class DurableTaskStepLookupRuntimeClient : IDisposable
{
    private const string AppId = "g1-tasks";
    private readonly Dictionary<string, IHost> _clients = new(StringComparer.Ordinal);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Uri _ownerSidecar;
    private readonly Uri _lookupHost;

    /// <summary>Gets whether the caller sidecar was configured by the runtime launcher.</summary>
    internal static bool IsConfigured => Environment.GetEnvironmentVariable("HEXALITH_G1_OWNER_HTTP") is not null;

    /// <summary>Reads the owner sidecar and the lookup host addresses from the runtime launcher.</summary>
    internal DurableTaskStepLookupRuntimeClient()
    {
        _ownerSidecar = new Uri(Required("HEXALITH_G1_OWNER_HTTP"));
        _lookupHost = new Uri(Required("HEXALITH_G1_HOST_HTTP"));
    }

    /// <summary>Sends a scoped request through Dapr, or directly for the missing-channel control.</summary>
    internal async Task<HttpResponseMessage> SendAsync(DurableTaskStepLookupRequest request, string? tenant, string workload,
        string? domain, string operation, bool channel, bool assertion)
    {
        string route = DurableTaskStepLookupExtensions.StepLookupPath;
        Uri destination = channel ? new(_ownerSidecar, $"/v1.0/invoke/{AppId}/method{route}") : new(_lookupHost, route);
        using var message = new HttpRequestMessage(HttpMethod.Post, destination) { Content = JsonContent.Create(request) };
        if (assertion)
        {
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant.ShouldNotBeNull(),
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain.ShouldNotBeNull(),
            };
            IWorkloadAssertionIssuer issuer = GetIssuer(workload);
            string? token = await issuer.IssueAsync(new(AppId, operation, bindings), TestContext.Current.CancellationToken);
            token.ShouldNotBeNullOrWhiteSpace($"the configured issuer must authenticate fixture workload {workload}");
            message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, token);
        }

        return await _http.SendAsync(message, TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
        foreach (IHost client in _clients.Values)
        {
            client.Dispose();
        }
    }

    private IWorkloadAssertionIssuer GetIssuer(string workload)
    {
        if (!_clients.TryGetValue(workload, out IHost? client))
        {
            string secretVariable = $"HEXALITH_G1_{workload.Replace('-', '_').ToUpperInvariant()}_SECRET";
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Services.AddEventStoreWorkloadAssertionIssuer(builder.Configuration, options =>
            {
                options.Workload = workload;
                options.ClientId = workload;
                options.ClientSecret = Required(secretVariable);
                options.EnableResourceBindingRequests = true;
            });
            client = builder.Build();
            _clients.Add(workload, client);
        }

        return client.Services.GetRequiredService<IWorkloadAssertionIssuer>();
    }

    private static string Required(string variable)
        => Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException($"The runtime launcher must configure {variable}.");
}
