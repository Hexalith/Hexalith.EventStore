using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.ServiceDefaults.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

/// <summary>Exercises the owning query registration, workload filter and real catalog transport guard.</summary>
public sealed class DomainQueryResilienceTests
{
    [Fact]
    public void ProductionQueryClientHasScopedValidatedPolicyAndPreservesCommandAndDefaultClients()
    {
        var capture = new BoundedInvocationResilienceCapture((_, _) => Task.FromResult(Success()));
        using ServiceProvider services = Services(capture);
        services.GetRequiredService<IDomainQueryInvoker>().ShouldBeOfType<DaprDomainQueryInvoker>();
        IHttpClientFactory clients = services.GetRequiredService<IHttpClientFactory>();
        using HttpClient query = clients.CreateClient(DaprDomainQueryInvoker.HttpClientName);
        capture.AssertPolicy(services, DaprDomainQueryInvoker.HttpClientName, "", "query-unrelated-control", JwtWorkloadAssertionIssuer.HttpClientName);
        capture.HandlerTypes[DaprDomainQueryInvoker.HttpClientName][^1].ShouldBe(typeof(DomainServiceWorkloadAssertionHandler));
        capture.HandlerTypes[JwtWorkloadAssertionIssuer.HttpClientName].ShouldNotContain(typeof(DomainServiceWorkloadAssertionHandler));
        using HttpClient command = clients.CreateClient(DaprDomainServiceInvoker.HttpClientName);
        command.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
        capture.HandlerTypes[DaprDomainServiceInvoker.HttpClientName].ShouldNotContain(typeof(Microsoft.Extensions.Http.Resilience.ResilienceHandler));
    }

    [Fact(Timeout = 25000)]
    public async Task HeldNativeQuerySurvivesElevenSecondsInOneAttemptAndReleasesEveryFence()
    {
        int attempts = 0;
        var capture = new BoundedInvocationResilienceCapture(async (request, token) =>
        {
            AssertOwnedQuery(request);
            Interlocked.Increment(ref attempts);
            await Task.Delay(TimeSpan.FromSeconds(11), token);
            return Success();
        });
        using ServiceProvider services = Services(capture);
        QueryResult result = await services.GetRequiredService<IDomainQueryInvoker>().InvokeAsync(Query(), TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue();
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(0);
    }

    [Theory(Timeout = 15000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyFailuresRetryAndOnlyAmbiguousTransportFailuresRetainTheirFence(bool ambiguous)
    {
        int attempts = 0;
        var capture = new BoundedInvocationResilienceCapture((request, _) =>
        {
            AssertOwnedQuery(request);
            if (Interlocked.Increment(ref attempts) == 1)
            {
                if (ambiguous) throw new HttpRequestException("isolated ambiguous transport");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(Success());
        });
        using ServiceProvider services = Services(capture);
        QueryResult result = await services.GetRequiredService<IDomainQueryInvoker>().InvokeAsync(Query(), TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue();
        attempts.ShouldBe(2);
        capture.OutstandingLeases.ShouldBe(ambiguous ? 1 : 0);
    }

    [Fact(Timeout = 10000)]
    public async Task CallerCancellationReachesTransportAndRetainsAmbiguousFence()
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
        using ServiceProvider services = Services(capture);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(TimeSpan.FromSeconds(5));
        Task<QueryResult> pending = services.GetRequiredService<IDomainQueryInvoker>().InvokeAsync(Query(), caller.Token);
        await started.Task.WaitAsync(caller.Token);
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(1);
    }

    [Fact(Timeout = 40000)]
    public async Task TotalBudgetStillCancelsOneAttemptAndPreservesFailureMappingAndFence()
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
        using ServiceProvider services = Services(capture);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(35));
        long startedAt = Stopwatch.GetTimestamp();
        QueryResult result = await services.GetRequiredService<IDomainQueryInvoker>().InvokeAsync(Query(), deadline.Token);
        Stopwatch.GetElapsedTime(startedAt).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(29));
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.QueryExecutionFailure);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        attempts.ShouldBe(1);
        capture.OutstandingLeases.ShouldBe(1);
    }

    [Fact(Timeout = 10000)]
    public async Task FailedTargetCircuitDoesNotBlockAnotherNativeQueryTarget()
    {
        int failedAttempts = 0, healthyAttempts = 0, outstandingLeases = 0;
        QueryEnvelope healthyQuery = Query() with { QueryType = "get-tenant" };
        var capture = new BoundedInvocationResilienceCapture((request, _) =>
        {
            request.RequestUri!.Authority.ShouldBe("localhost:3500");
            if (request.RequestUri.AbsolutePath == "/v1.0/invoke/tenants/method/query")
            {
                AssertOwnedQuery(request);
                Interlocked.Increment(ref failedAttempts);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            AssertOwnedQuery(request, healthyQuery, "healthy-query-target");
            Interlocked.Increment(ref healthyAttempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { id = "healthy" }), "tenants")),
            });
        });
        RouteCatalogEnvelope envelope = new RouteCatalogTestFixture().Envelope;
        envelope = RouteCatalogCodec.Seal(envelope with
        {
            Routes = [.. envelope.Routes.Select(route => route.RouteId == "tenants-get-tenant"
                ? route with { AppId = "healthy-query-target" } : route)],
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1, localAppId: "eventstore");
        using ServiceProvider services = Services(capture, registrations =>
        {
            IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
            authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Interlocked.Increment(ref outstandingLeases);
                return new RouteCatalogDispatchLease(snapshot, () =>
                {
                    Interlocked.Decrement(ref outstandingLeases);
                    return Task.CompletedTask;
                });
            });
            registrations.AddSingleton(authority);
            registrations.Configure<HttpStandardResilienceOptions>(DaprDomainQueryInvoker.HttpClientName + "-standard", static options =>
            {
                // Open a real circuit after two completed failures without retry delays or changing product policy.
                options.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
                options.CircuitBreaker.MinimumThroughput = 2;
                options.CircuitBreaker.FailureRatio = 1;
            });
        });
        IDomainQueryInvoker invoker = services.GetRequiredService<IDomainQueryInvoker>();
        (await invoker.InvokeAsync(Query(), TestContext.Current.CancellationToken)).Success.ShouldBeFalse();
        (await invoker.InvokeAsync(Query(), TestContext.Current.CancellationToken)).Success.ShouldBeFalse();
        failedAttempts.ShouldBe(2);

        QueryResult blocked = await invoker.InvokeAsync(Query(), TestContext.Current.CancellationToken);
        blocked.Success.ShouldBeFalse();
        blocked.ErrorMessage.ShouldBe(QueryAdapterFailureReason.QueryExecutionFailure);
        failedAttempts.ShouldBe(2, "the open target circuit must refuse a third transport attempt");
        QueryResult healthy = await invoker.InvokeAsync(healthyQuery, TestContext.Current.CancellationToken);
        healthy.Success.ShouldBeTrue("another target on the same sidecar must retain its independent circuit");
        healthyAttempts.ShouldBe(1);
        outstandingLeases.ShouldBe(0);
        capture.PrimaryTypes[DaprDomainQueryInvoker.HttpClientName].ShouldBe(typeof(RouteCatalogInvocationHandler));
        capture.HandlerTypes[DaprDomainQueryInvoker.HttpClientName][0].ShouldBe(typeof(ResilienceHandler));
        capture.HandlerTypes[DaprDomainQueryInvoker.HttpClientName][^1].ShouldBe(typeof(DomainServiceWorkloadAssertionHandler));
    }

    [Theory]
    [InlineData("http://localhost:3500/v1.0/invoke/tenants/method/query", "http://localhost:3500/v1.0/invoke/tenants")]
    [InlineData("http://localhost:3500/v1.0/invoke/projects/method/query", "http://localhost:3500/v1.0/invoke/projects")]
    [InlineData("http://localhost:3501/v1.0/invoke/tenants/method/query", "http://localhost:3501/v1.0/invoke/tenants")]
    [InlineData("https://service.example/read", "https://service.example")]
    public void SharedCircuitSelectorPreservesAuthorityAndDaprTargetPartition(string address, string expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        Hexalith.EventStore.ServiceDefaults.Extensions.GetResiliencePipelineKey(request).ShouldBe(expected);
    }

    private static ServiceProvider Services(BoundedInvocationResilienceCapture capture, Action<IServiceCollection>? configure = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        capture.Observe(builder.Services);
        builder.AddServiceDefaults();
        builder.Services.AddDaprClient();
        builder.Services.AddEventStoreServer(builder.Configuration);
        builder.Services.AddEventStoreDomainQueryRouting();
        capture.IsolateCatalog(builder.Services);
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns("isolated-query-workload");
        builder.Services.AddSingleton(issuer);
        configure?.Invoke(builder.Services);
        return builder.Services.BuildServiceProvider();
    }

    private static void AssertOwnedQuery(HttpRequestMessage request, QueryEnvelope? query = null, string appId = "tenants")
    {
        request.RequestUri!.AbsolutePath.ShouldEndWith($"/invoke/{appId}/method/query");
        request.Headers.Authorization.ShouldBeNull();
        request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).ShouldBe(["isolated-query-workload"]);
        request.Options.TryGetValue(QueryCallerAuthorityBinding.RequestBindings, out var bindings).ShouldBeTrue();
        bindings.ShouldBe(QueryCallerAuthorityBinding.Bind(query ?? Query()));
        request.Headers.GetValues(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.EpochHeader).ShouldBe(["1"]);
    }

    private static QueryEnvelope Query() => new("system", "tenants", "index", "list-tenants", "{}"u8.ToArray(), "query-budget", "user")
    {
        CallerAuthority = new QueryCallerAuthority("authenticated-caller", [], ["query:read"]),
    };

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { items = Array.Empty<object>() }), "tenant-index")),
    };
}
