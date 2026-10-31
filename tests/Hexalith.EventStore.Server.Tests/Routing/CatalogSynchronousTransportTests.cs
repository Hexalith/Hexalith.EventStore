using System.Net;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogSynchronousTransportTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("active")]
    [InlineData("pending")]
    public async Task SynchronousClientsKeepOrdinaryHttpAndTheActualInvocationFence(string phase)
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        if (phase != "empty") await fixture.ActivateAsync(candidate);
        if (phase == "pending") await fixture.Authorities[0].PrepareAsync(fixture.Candidate(2, candidate.RootDigest));
        var target = fixture.Envelope.Routes.First(route => route.Method == "query");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        int calls = 0;
        app.MapGet("/ordinary", () => Results.NoContent());
        app.MapPost($"/v1.0/invoke/{target.AppId}/method/{target.Method}", (HttpContext context) =>
        {
            calls++;
            context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(candidate.RootDigest);
            context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].ToString().ShouldBe("1");
            return Results.NoContent();
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Authorities[0], new SocketsHttpHandler()))
        { BaseAddress = new Uri(app.Urls.Single()) };
        using var ordinary = new HttpRequestMessage(HttpMethod.Get, "/ordinary");
        using var ordinaryResponse = client.Send(ordinary, TestContext.Current.CancellationToken);
        ordinaryResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var invocation = new HttpRequestMessage(HttpMethod.Post, $"/v1.0/invoke/{target.AppId}/method/{target.Method}");
        invocation.Headers.Add(RouteCatalogInvocationHandler.RootHeader, "forged");
        invocation.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, "999");
        if (phase == "active")
        {
            using var response = client.Send(invocation, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        else Should.Throw<RouteCatalogUnavailableException>(() => client.Send(invocation, TestContext.Current.CancellationToken));
        calls.ShouldBe(phase == "active" ? 1 : 0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task SynchronousCancellationAfterDispatchRetainsTheUncertainDurableFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var target = fixture.Envelope.Routes.First(route => route.Method == "query");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.MapPost($"/v1.0/invoke/{target.AppId}/method/{target.Method}", async (HttpContext context) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { aborted.TrySetResult(); }
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Authorities[0], new SocketsHttpHandler()))
        { BaseAddress = new Uri(app.Urls.Single()) };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1.0/invoke/{target.AppId}/method/{target.Method}");
        var sending = Task.Run(() => client.Send(request, cancellation.Token), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => { using var response = await sending; });
        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
    }
}
