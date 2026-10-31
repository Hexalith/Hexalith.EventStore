using System.Net;
using System.Text;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Protects real large actor bodies while retaining replayable unstamped fixture JSON.</summary>
public sealed class LiveAdmissionCatalogStartupFilterTests
{
    /// <summary>Typed actor identity and existing root stamps must not impose a fixture-only body cap.</summary>
    /// <param name="surface">The actual request surface and scope-selection path.</param>
    [Theory]
    [InlineData("actor")]
    [InlineData("stamped")]
    [InlineData("unstamped")]
    public async Task NativeLargeRequestBodySurvivesScopeSelectionWithoutGlobalBufferLimit(string surface)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IStartupFilter, LiveAdmissionCatalogStartupFilter>();
        await using WebApplication app = builder.Build();
        string path = surface == "actor" ? "/actors/AggregateActor/tenant-a:guarded:id/method/SeedAsync" : "/project";
        app.MapPost(path, async (HttpContext context) =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            return Results.Ok(buffer.Length);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        if (surface == "stamped") client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, new string('a', 64));
        byte[] payload = new byte[2 * 1024 * 1024];
        payload.AsSpan().Fill(81);
        using var content = new ByteArrayContent(payload);
        using HttpResponseMessage response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>After model binding consumes ordinary fixture JSON, its exact original bytes remain available.</summary>
    [Fact]
    public async Task UnstampedJsonRemainsReplayableForImmutableTenantSelection()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IStartupFilter, LiveAdmissionCatalogStartupFilter>();
        await using WebApplication app = builder.Build();
        const string payload = "{\"tenant\":\"tenant-a\"}";
        app.MapPost("/api/v1/commands", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            (await reader.ReadToEndAsync(context.RequestAborted)).ShouldBe(payload);
            context.Request.Body.CanSeek.ShouldBeTrue();
            context.Request.Body.Position = 0;
            using var replay = new StreamReader(context.Request.Body, leaveOpen: true);
            return Results.Text(await replay.ReadToEndAsync(context.RequestAborted));
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync("/api/v1/commands", content, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(payload);
    }
}
