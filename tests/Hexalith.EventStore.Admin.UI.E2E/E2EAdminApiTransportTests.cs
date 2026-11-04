using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>Verifies the standalone backend through the real UI handlers and its refusal boundary.</summary>
public class E2EAdminApiTransportTests
{
    [Fact]
    public async Task RealBearerAndDaprHandlersReachOnlyTheOwnedEmptyBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(E2EAuthenticationSettings.Create());
        builder.AddAdminUI();
        using var backend = new E2EAdminApiHandler(builder.Configuration);
        builder.Services.AddHttpClient("AdminApi").ConfigurePrimaryHttpMessageHandler(() => backend);
        await using WebApplication app = builder.Build();
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");

        SystemHealthReport? report = await client.GetFromJsonAsync<SystemHealthReport>("api/v1/admin/health", TestContext.Current.CancellationToken);

        report.ShouldNotBeNull();
        report.TotalEventCount.ShouldBe(0);
        using IServiceScope scope = app.Services.CreateScope();
        PagedResult<StreamSummary> streams = await scope.ServiceProvider.GetRequiredService<AdminStreamApiClient>()
            .GetRecentlyActiveStreamsAsync(null, null, 100, TestContext.Current.CancellationToken);
        streams.Items.ShouldBeEmpty();
        streams.TotalCount.ShouldBe(0);
        streams.ContinuationToken.ShouldBeNull();
        backend.VerifyNoRejectedRequests();
    }

    [Theory]
    [InlineData(null, "/api/v1/admin/streams/GetRecentlyActiveStreams?count=1000", AdminPlatformScopeCatalog.Namespace)]
    [InlineData("Acme", "/api/v1/admin/streams/GetRecentlyActiveStreams?count=1000&tenantId=acme", null)]
    public async Task RealStreamClientUsesCompiledListRouteAndExplicitScope(string? tenant, string expectedPath, string? expectedScope)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(E2EAuthenticationSettings.Create());
        builder.AddAdminUI();
        using var backend = new E2EAdminApiHandler(builder.Configuration);
        builder.Services.AddHttpClient("AdminApi").ConfigurePrimaryHttpMessageHandler(() => backend);
        await using WebApplication app = builder.Build();
        using IServiceScope scope = app.Services.CreateScope();

        var streams = await scope.ServiceProvider.GetRequiredService<AdminStreamApiClient>()
            .GetRecentlyActiveStreamsAsync(tenant, null, ct: TestContext.Current.CancellationToken);

        streams.Items.ShouldBeEmpty();
        streams.TotalCount.ShouldBe(0);
        backend.Requests.ShouldHaveSingleItem().ShouldBe((expectedPath, expectedScope));
        backend.VerifyNoRejectedRequests();
    }

    [Theory]
    [InlineData("method", HttpStatusCode.MethodNotAllowed)]
    [InlineData("route", HttpStatusCode.NotFound)]
    [InlineData("route-prefix", HttpStatusCode.NotFound)]
    [InlineData("legacy-stream-list", HttpStatusCode.NotFound)]
    [InlineData("endpoint", HttpStatusCode.Forbidden)]
    [InlineData("target", HttpStatusCode.Forbidden)]
    [InlineData("sidecar-token", HttpStatusCode.Unauthorized)]
    [InlineData("bearer", HttpStatusCode.Unauthorized)]
    public async Task UndeclaredTransportOrCredentialIsRefusedAndCannotBeSwallowed(string mutation, HttpStatusCode expected)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(E2EAuthenticationSettings.Create());
        builder.AddAdminUI();
        await using WebApplication app = builder.Build();
        using IServiceScope scope = app.Services.CreateScope();
        string bearer = await scope.ServiceProvider.GetRequiredService<AdminApiAccessTokenProvider>()
            .GetAccessTokenAsync(TestContext.Current.CancellationToken);
        using var backend = new E2EAdminApiHandler(builder.Configuration);
        using var invoker = new HttpMessageInvoker(backend, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, E2EAdminApiHandler.Endpoint + "/api/v1/admin/health");
        request.Headers.Add("dapr-app-id", E2EAdminApiHandler.AppId);
        request.Headers.Add("dapr-api-token", builder.Configuration["DAPR_API_TOKEN"]);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        switch (mutation)
        {
            case "method": request.Method = HttpMethod.Post; break;
            case "route": request.RequestUri = new Uri(E2EAdminApiHandler.Endpoint + "/undeclared"); break;
            case "route-prefix": request.RequestUri = new Uri(E2EAdminApiHandler.Endpoint + "/api/v1/admin/streams/GetRecentlyActiveStreams/undeclared"); break;
            case "legacy-stream-list": request.RequestUri = new Uri(E2EAdminApiHandler.Endpoint + "/api/v1/admin/streams"); break;
            case "endpoint": request.RequestUri = new Uri("http://127.0.0.1:3500/api/v1/admin/health"); break;
            case "target": request.Headers.Remove("dapr-app-id"); request.Headers.Add("dapr-app-id", "foreign-app"); break;
            case "sidecar-token": request.Headers.Remove("dapr-api-token"); break;
            case "bearer": request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToHexString(RandomNumberGenerator.GetBytes(32))); break;
        }

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
        Should.Throw<InvalidOperationException>(backend.VerifyNoRejectedRequests);
    }
}
