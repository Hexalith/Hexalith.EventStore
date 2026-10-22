using System.Net;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Indexes;

/// <summary>Verifies an absent metadata target cannot open the ordinary delivery client's circuit.</summary>
public sealed class AdminMetadataCircuitIsolationTests
{
    /// <summary>Opens one real resilience circuit while Works metadata and legacy delivery remain available.</summary>
    [Fact]
    public async Task FailedTargetCircuitDoesNotBlockWorksMetadataOrLegacyDelivery()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        _ = builder.AddServiceDefaults();
        IServiceCollection registrations = builder.Services;
        _ = registrations.ConfigureHttpClientDefaults(http =>
            http.ConfigurePrimaryHttpMessageHandler(static () => new MetadataHandler()));
        _ = registrations.ConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(1);
            options.CircuitBreaker.MinimumThroughput = 4;
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
        });
        await using ServiceProvider services = registrations.BuildServiceProvider();
        IHttpClientFactory clients = services.GetRequiredService<IHttpClientFactory>();
        using DaprClient dapr = new DaprClientBuilder().UseHttpEndpoint("http://localhost:3500").Build();
        var service = new AdminOperationalIndexHostedService(dapr, clients,
            Options.Create(new CommandStatusOptions()), Options.Create(new DomainServiceOptions()),
            Options.Create(new ProjectionOptions()), new NamedProjectionRouteCatalog(),
            NullLogger<AdminOperationalIndexHostedService>.Instance);
        var missing = new DomainServiceRegistration("missing", "process", "*", "missing", "v1");
        var works = new DomainServiceRegistration("works", "process", "*", "work", "v1");
        CancellationToken cancellation = TestContext.Current.CancellationToken;

        _ = await Should.ThrowAsync<HttpRequestException>(() => service.RefreshAsync(missing, cancellation));
        _ = await Should.ThrowAsync<HttpRequestException>(() => service.RefreshAsync(missing, cancellation));
        _ = await Should.ThrowAsync<BrokenCircuitException>(() => service.RefreshAsync(missing, cancellation));

        (await service.RefreshAsync(works, cancellation)).ShouldBeFalse("Works metadata succeeded and has no named routes.");
        using HttpResponseMessage delivery = await clients.CreateClient().PostAsync(
            "http://localhost:3500/v1.0/invoke/works/method/project", new StringContent("{}"), cancellation);
        delivery.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Partitions two targets through exactly one client and one Dapr sidecar authority.</summary>
    [Fact]
    public async Task SameClientAndSidecarKeepHealthyTargetOutsideFailedTargetCircuit()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        _ = builder.AddServiceDefaults();
        _ = builder.Services.ConfigureHttpClientDefaults(http =>
            http.ConfigurePrimaryHttpMessageHandler(static () => new MetadataHandler()));
        _ = builder.Services.ConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(1);
            options.CircuitBreaker.MinimumThroughput = 4;
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
        });
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient("same-dapr-client");
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        const string failedTarget = "http://localhost:3500/v1.0/invoke/missing/method/project";
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage failed = await client.PostAsync(failedTarget, new StringContent("{}"), cancellation);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        _ = await Should.ThrowAsync<BrokenCircuitException>(() => client.PostAsync(failedTarget, new StringContent("{}"), cancellation));
        using HttpResponseMessage healthy = await client.PostAsync(
            "http://localhost:3500/v1.0/invoke/works/method/project", new StringContent("{}"), cancellation);
        healthy.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class MetadataHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(request.RequestUri!.AbsolutePath.Contains("/invoke/missing/", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AdminOperationalIndexMetadataResponse([
                        new AdminOperationalIndexDomainMetadata("work", [], [], [], [], []),
                    ])),
                });
    }
}
