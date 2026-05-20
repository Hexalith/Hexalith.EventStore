
using System.Net;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

/// <summary>
/// Coverage for the typed-error mapping introduced in the
/// admin-ui-state-inspection-cluster-fix story. Asserts that upstream 404 surfaces as
/// <see cref="KeyNotFoundException"/> and upstream 400 surfaces as <see cref="ArgumentException"/>
/// for state/diff/causation, matching the recently-fixed event-detail proxy pattern.
/// </summary>
public class DaprStreamQueryServiceStateMappingTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprStreamQueryService Service, TestHttpMessageHandler Handler) CreateService() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = "statestore",
            EventStoreAppId = EventStoreAppId,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprStreamQueryService(
            daprClient,
            httpClientFactory,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprStreamQueryService>.Instance);

        return (service, handler);
    }

    // -------- /state mapping --------

    [Fact]
    public async Task GetAggregateStateAtPositionAsync_Upstream404_ThrowsKeyNotFound() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        _ = await Should.ThrowAsync<KeyNotFoundException>(
            () => service.GetAggregateStateAtPositionAsync("t", "d", "a", 5));
    }

    [Fact]
    public async Task GetAggregateStateAtPositionAsync_Upstream400_ThrowsArgumentException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.GetAggregateStateAtPositionAsync("t", "d", "a", -1));
    }

    [Fact]
    public async Task GetAggregateStateAtPositionAsync_HappyPath_ReturnsSnapshot() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        AggregateStateSnapshot expected = new("t", "d", "a", 5, DateTimeOffset.UtcNow, "{\"v\":1}");
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(expected),
        });

        AggregateStateSnapshot actual = await service.GetAggregateStateAtPositionAsync("t", "d", "a", 5);

        actual.SequenceNumber.ShouldBe(5);
        actual.StateJson.ShouldBe("{\"v\":1}");
        handler.LastRequest!.RequestUri!.ToString().ShouldContain("at=5");
    }

    // -------- /diff mapping --------

    [Fact]
    public async Task DiffAggregateStateAsync_Upstream404_ThrowsKeyNotFound() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        _ = await Should.ThrowAsync<KeyNotFoundException>(
            () => service.DiffAggregateStateAsync("t", "d", "a", 1, 5));
    }

    [Fact]
    public async Task DiffAggregateStateAsync_Upstream400_ThrowsArgumentException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.DiffAggregateStateAsync("t", "d", "a", 5, 5));
    }

    [Fact]
    public async Task DiffAggregateStateAsync_HappyPath_PreservesAtFromToQueryNames() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        AggregateStateDiff expected = new(1, 3, []);
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(expected),
        });

        AggregateStateDiff actual = await service.DiffAggregateStateAsync("t", "d", "a", 1, 3);

        actual.FromSequence.ShouldBe(1);
        actual.ToSequence.ShouldBe(3);
        // The EventStore-side query names must remain `from` and `to`, not the Admin Server
        // facade names `fromSequence`/`toSequence`.
        string url = handler.LastRequest!.RequestUri!.ToString();
        url.ShouldContain("from=1");
        url.ShouldContain("to=3");
    }

    // -------- /causation mapping --------

    [Fact]
    public async Task TraceCausationChainAsync_Upstream404_ThrowsKeyNotFound() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

        _ = await Should.ThrowAsync<KeyNotFoundException>(
            () => service.TraceCausationChainAsync("t", "d", "a", 1));
    }

    [Fact]
    public async Task TraceCausationChainAsync_Upstream400_ThrowsArgumentException() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.TraceCausationChainAsync("t", "d", "a", 0));
    }

    [Fact]
    public async Task TraceCausationChainAsync_HappyPath_UsesAtQueryName() {
        (DaprStreamQueryService service, TestHttpMessageHandler handler) = CreateService();
        CausationChain expected = new("CounterIncremented", "evt-1", "corr-1", null, [], []);
        handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(expected),
        });

        CausationChain actual = await service.TraceCausationChainAsync("t", "d", "a", 4);

        actual.OriginatingCommandId.ShouldBe("evt-1");
        handler.LastRequest!.RequestUri!.ToString().ShouldContain("at=4");
    }
}
