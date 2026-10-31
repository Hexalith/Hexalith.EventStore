using System.Diagnostics;
using System.Security.Cryptography;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

/// <summary>Actual named factory-client timeout still covers a metadata body after its headers arrive.</summary>
public sealed class DevelopmentActorStateMetadataTimeoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataBodyRemainsInsideTheOriginalFiveSecondTimeout(bool privateCatalog)
    {
        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var headers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var owner = builder.Build();
        owner.MapGet("/v1.0/metadata", async (HttpContext context) =>
        {
            context.Request.Headers["dapr-api-token"].ToString().ShouldBe(privateCatalog ? token : string.Empty);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted);
            headers.TrySetResult();
            await release.Task.WaitAsync(context.RequestAborted);
            await context.Response.WriteAsync("\"actors\":[],\"components\":[]}", context.RequestAborted);
        });
        await owner.StartAsync(TestContext.Current.CancellationToken);
        string endpoint = owner.Urls.Single();
        await using var host = new TechnicalAdminHost();
        ServiceProvider? provider = null; IServiceScope? scope = null;
        try
        {
            if (privateCatalog)
            {
                await host.StartAsync(ownerEndpoint: endpoint, ownerToken: token); scope = host.CreateScope();
            }
            else
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["AdminServer:EventStoreDaprHttpEndpoint"] = endpoint, ["AdminServer:HealthHistoryEnabled"] = "false" }).Build();
                var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(Substitute.For<DaprClient>()); services.AddAdminApi(configuration);
                provider = services.BuildServiceProvider(); scope = provider.CreateScope();
            }
            using var client = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("DaprSidecar");
            client.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
            var service = scope.ServiceProvider.GetRequiredService<IDaprInfrastructureQueryService>();
            var timer = Stopwatch.StartNew();
            Task<DaprActorRuntimeInfo> read = service.GetActorRuntimeInfoAsync(TestContext.Current.CancellationToken);
            await headers.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Task completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(7), TestContext.Current.CancellationToken));
            release.TrySetResult();
            completed.ShouldBeSameAs(read, "The existing five-second named-client timeout must cover the response body.");
            (await read).RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
            timer.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(7));
        }
        finally
        {
            release.TrySetResult(); scope?.Dispose(); if (provider is not null) await provider.DisposeAsync();
        }
    }
}
