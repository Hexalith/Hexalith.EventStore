using System.Net;

using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public sealed class DefaultHealthEndpointResponseWriterTests {
    [Fact]
    public async Task Development_UsesCustomWriterForHealthAndReadyEndpoints() {
        await using WebApplication app = CreateApplication(
            Environments.Development,
            static options => options.DevelopmentHealthResponseWriter = static async (context, _) =>
                await context.Response.WriteAsync("custom-health-response").ConfigureAwait(false));
        await app.StartAsync().ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();

        foreach (string path in new[] { "/health", "/ready" }) {
            using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            body.ShouldBe("custom-health-response");
        }
    }

    [Fact]
    public async Task Development_UsesDefaultJsonWriterForHealthAndReadyEndpoints() {
        await using WebApplication app = CreateApplication(Environments.Development);
        await app.StartAsync().ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();

        foreach (string path in new[] { "/health", "/ready" }) {
            using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
            body.ShouldContain("\"status\": \"Unhealthy\"");
            body.ShouldContain("\"ready-check\"");
        }
    }

    [Fact]
    public async Task Development_NullWriterLeavesHealthAndReadyResponsesUndetailed() {
        await using WebApplication app = CreateApplication(
            Environments.Development,
            static options => options.DevelopmentHealthResponseWriter = null);
        await app.StartAsync().ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();

        foreach (string path in new[] { "/health", "/ready" }) {
            using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            body.ShouldBe("Unhealthy");
        }
    }

    [Fact]
    public async Task Production_DoesNotUseConfiguredDevelopmentWriter() {
        await using WebApplication app = CreateApplication(
            Environments.Production,
            static options => options.DevelopmentHealthResponseWriter = static async (context, _) =>
                await context.Response.WriteAsync("must-not-be-written").ConfigureAwait(false));
        await app.StartAsync().ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();

        foreach (string path in new[] { "/health", "/ready" }) {
            using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            body.ShouldBe("Unhealthy");
            body.ShouldNotContain("must-not-be-written");
        }
    }

    private static WebApplication CreateApplication(
        string environment,
        Action<EventStoreServiceDefaultsOptions>? configure = null) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            EnvironmentName = environment,
        });
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services
            .AddHealthChecks()
            .AddCheck(
                "ready-check",
                static () => HealthCheckResult.Unhealthy("Dependency unavailable."),
                tags: ["ready"]);
        if (configure is not null) {
            _ = builder.Services.Configure(configure);
        }

        WebApplication app = builder.Build();
        _ = app.MapDefaultEndpoints();
        return app;
    }
}
