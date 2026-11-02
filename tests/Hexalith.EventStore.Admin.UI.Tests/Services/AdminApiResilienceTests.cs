using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.ServiceDefaults.Tests;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Polly.Timeout;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

/// <summary>Exercises the actual Admin API registration with catalog fencing and owned bearer/Dapr handlers.</summary>
public sealed class AdminApiResilienceTests
{
    [Fact]
    public async Task ProductionAdminPolicyPreservesHandlerOrderOuterTimeoutAndTokenClientIsolation()
    {
        var capture = new BoundedInvocationResilienceCapture((_, _) => Task.FromResult(Success()));
        await using WebApplication app = Application(capture);
        IHttpClientFactory clients = app.Services.GetRequiredService<IHttpClientFactory>();
        using HttpClient admin = clients.CreateClient("AdminApi");
        admin.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        capture.AssertPolicy(app.Services, "AdminApi", "", "admin-unrelated-control", nameof(AdminApiAccessTokenProvider));
        Type[] handlers = capture.HandlerTypes["AdminApi"];
        handlers[^2].ShouldBe(typeof(AdminApiAuthorizationHandler));
        handlers[^1].Name.ShouldBe("DaprServiceInvocationHandler");
        foreach (string name in new[] { "", "admin-unrelated-control", nameof(AdminApiAccessTokenProvider) })
        {
            capture.HandlerTypes[name].ShouldNotContain(typeof(AdminApiAuthorizationHandler));
            capture.HandlerTypes[name].ShouldNotContain(type => type.Name == "DaprServiceInvocationHandler");
        }
    }

    [Fact(Timeout = 25000)]
    public async Task HeldAdminReadSurvivesElevenSecondsInOneAttemptAndReleasesItsFence()
    {
        int attempts = 0;
        var capture = new BoundedInvocationResilienceCapture(async (request, token) =>
        {
            AssertOwnedAdmin(request);
            Interlocked.Increment(ref attempts);
            await Task.Delay(TimeSpan.FromSeconds(11), token);
            return Success();
        });
        await using WebApplication app = Application(capture);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
        using HttpResponseMessage response = await client.GetAsync("api/v1/admin/dapr/components", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(0);
    }

    [Theory(Timeout = 15000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyAdminFailuresRetryWithoutForgivingAnAmbiguousPriorAttempt(bool ambiguous)
    {
        int attempts = 0;
        var capture = new BoundedInvocationResilienceCapture((request, _) =>
        {
            AssertOwnedAdmin(request);
            if (Interlocked.Increment(ref attempts) == 1)
            {
                if (ambiguous) throw new HttpRequestException("isolated ambiguous transport");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(Success());
        });
        await using WebApplication app = Application(capture);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
        using HttpResponseMessage response = await client.GetAsync("api/v1/admin/dapr/components", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        attempts.ShouldBe(2);
        capture.OutstandingLeases.ShouldBe(ambiguous ? 1 : 0);
    }

    [Fact(Timeout = 10000)]
    public async Task AdminCallerCancellationReachesTransportAndRetainsItsAmbiguousFence()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        var capture = new BoundedInvocationResilienceCapture(async (_, token) =>
        {
            Interlocked.Increment(ref attempts);
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { canceled.TrySetResult(); throw; }
            return Success();
        });
        await using WebApplication app = Application(capture);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(TimeSpan.FromSeconds(5));
        Task<HttpResponseMessage> pending = client.GetAsync("api/v1/admin/dapr/components", caller.Token);
        await started.Task.WaitAsync(caller.Token);
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(1);
    }

    [Fact(Timeout = 40000)]
    public async Task AdminThirtySecondBudgetCancelsOneAttemptAndRetainsItsAmbiguousFence()
    {
        int attempts = 0;
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new BoundedInvocationResilienceCapture(async (_, token) =>
        {
            Interlocked.Increment(ref attempts);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { canceled.TrySetResult(); throw; }
            return Success();
        });
        await using WebApplication app = Application(capture);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(35));
        long startedAt = Stopwatch.GetTimestamp();
        Exception? failure = await Record.ExceptionAsync(() => client.GetAsync("api/v1/admin/dapr/components", deadline.Token));
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
        // HttpClient's unchanged outer timeout and standard total timeout share the same deadline.
        (failure is OperationCanceledException or TimeoutRejectedException).ShouldBeTrue();
        deadline.IsCancellationRequested.ShouldBeFalse("the thirty-five-second caller fallback must not satisfy the transport budget assertion");
        elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(29));
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(35));
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(1);
    }

    private static WebApplication Application(BoundedInvocationResilienceCapture capture)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3500", ["DAPR_API_TOKEN"] = "[redacted]",
            ["EventStore:Authentication:Issuer"] = "isolated-issuer", ["EventStore:Authentication:Audience"] = "isolated-audience",
            ["EventStore:Authentication:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Subject"] = "isolated-user", ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter", ["EventStore:Authentication:Permissions:0"] = "admin:read",
        });
        capture.Observe(builder.Services);
        builder.AddServiceDefaults();
        builder.AddAdminUI();
        builder.Services.AddEventStoreRouteCatalog(builder.Configuration);
        capture.IsolateCatalog(builder.Services);
        return builder.Build();
    }

    private static void AssertOwnedAdmin(HttpRequestMessage request)
    {
        request.RequestUri.ShouldBe(new Uri("http://127.0.0.1:3500/api/v1/admin/dapr/components"));
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldNotBeNullOrWhiteSpace();
        request.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore-admin"]);
        request.Headers.GetValues("dapr-api-token").ShouldBe(["[redacted]"]);
        request.Headers.GetValues(RouteCatalogInvocationHandler.EpochHeader).ShouldBe(["1"]);
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK) { Content = new StringContent("[]") };
}
