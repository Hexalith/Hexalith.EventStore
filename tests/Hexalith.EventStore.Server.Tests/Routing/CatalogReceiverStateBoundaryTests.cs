using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogReceiverStateBoundaryTests
{
    [Theory]
    [InlineData("write-fault", false)]
    [InlineData("write-fault", true)]
    [InlineData("write-cancel", false)]
    [InlineData("write-cancel", true)]
    [InlineData("transaction-fault", false)]
    [InlineData("transaction-fault", true)]
    [InlineData("transaction-cancel", false)]
    [InlineData("transaction-cancel", true)]
    [InlineData("write-success", false)]
    [InlineData("write-success", true)]
    [InlineData("transaction-success", false)]
    [InlineData("transaction-success", true)]
    [InlineData("local-rejection", false)]
    [InlineData("local-rejection", true)]
    [InlineData("local-cancel", false)]
    [InlineData("local-cancel", true)]
    public async Task ConcreteSdkStateRpcAmbiguitySurvivesNormalIndeterminateHttpResponse(string mode, bool deferred)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        int index = Array.FindIndex(fixture.Hosts, h => h.AppId == "sample");
        var guard = new DomainRouteCatalogGuard(fixture.Authorities[index], Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[index] }));
        bool ambiguous = mode != "local-cancel" && (mode.EndsWith("fault", StringComparison.Ordinal) || mode.EndsWith("cancel", StringComparison.Ordinal));
        Exception? failure = mode.EndsWith("cancel", StringComparison.Ordinal) ? new OperationCanceledException("state response canceled")
            : mode.EndsWith("fault", StringComparison.Ordinal) ? new IOException("state response lost") : null;
        using var dapr = new RecordingDaprClient();
        if (failure is not null)
        {
            if (mode.StartsWith("transaction", StringComparison.Ordinal)) dapr.AfterStateTransaction = _ => throw failure;
            else dapr.AfterTrySaveByteState = (_, _) => throw failure;
        }
        var accessor = new DaprReadModelBatchStateAccessor(dapr, "statestore");
        var handler = Substitute.For<IAsyncDomainProjectionHandler>(); handler.Domain.Returns("counter"); handler.ProjectionType.Returns("counter"); handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (mode == "local-cancel")
            {
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                await accessor.TryWriteAsync("detail", "{}"u8.ToArray(), "", canceled.Token).ConfigureAwait(false);
            }
            if (mode.StartsWith("transaction", StringComparison.Ordinal))
                await accessor.ExecuteTransactionAsync([new("detail", "{\"value\":7}"u8.ToArray(), false, "", false)], call.Arg<CancellationToken>()).ConfigureAwait(false);
            else await accessor.TryWriteAsync("detail", "{\"value\":7}"u8.ToArray(), "",
                mode == "local-rejection" ? TimeSpan.Zero : null, call.Arg<CancellationToken>()).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(new { value = 7 }));
        });
        var registry = new DomainProjectionCatalogRegistry(); registry.Register("state-fixture", [new ProjectionDispatchRoute("counter", "counter")]);
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Services.AddSingleton(handler);
        await using var app = builder.Build();
        app.MapPost("/project", async (ProjectionRequest body, HttpContext context) =>
        {
            Task<ProjectionDispatchResponse> Dispatch() => DomainProjectionDispatcher.DispatchAsync(context.RequestServices,
                new(body, ["counter"], "state-dispatch", "state-fixture"), new(), registry, context.RequestAborted);
            return deferred ? (IResult)new CatalogStateDispatchResult(Dispatch) : Results.Json(await Dispatch().ConfigureAwait(false));
        }).AddEndpointFilter(new DomainRouteCatalogFilter(guard));
        await app.StartAsync();
        using var client = new HttpClient(); using var request = new HttpRequestMessage(HttpMethod.Post, app.Urls.Single() + "/project")
        { Content = JsonContent.Create(new ProjectionRequest("tenant-a", "counter", "aggregate", [])) };
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, first.RootDigest); request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "1");
        using var response = await client.SendAsync(request); response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>();
        result!.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ambiguous || mode is "local-rejection" or "local-cancel" ? (mode.EndsWith("fault", StringComparison.Ordinal) ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.Indeterminate) : ProjectionDispatchStatus.Completed);
        dapr.ByteStoreContains("detail").ShouldBe(mode is not ("local-rejection" or "local-cancel"));
        if (mode is not ("local-rejection" or "local-cancel")) JsonDocument.Parse(dapr.ByteStoreValue("detail")).RootElement.GetProperty("value").GetInt32().ShouldBe(7);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(ambiguous ? 1 : 0);
        RouteCatalogRemoteExecutionScopeRestored();
        var next = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(next);
        if (ambiguous) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(next.AttemptId));
        else await fixture.Authorities[0].ReadyAsync(next.AttemptId);
    }

    private static void RouteCatalogRemoteExecutionScopeRestored() => Hexalith.EventStore.Contracts.Routing.RouteCatalogRemoteExecution.Current.ShouldBeNull();
}
