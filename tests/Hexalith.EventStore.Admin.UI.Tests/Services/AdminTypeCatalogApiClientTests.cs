using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminTypeCatalogApiClientTests {
    private static AdminTypeCatalogApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminTypeCatalogApiClient(factory, NullLogger<AdminTypeCatalogApiClient>.Instance);
    }

    // === ListEventTypesAsync ===

    [Fact]
    public async Task ListEventTypesAsync_ReturnsTypes_WhenApiResponds() {
        string json = """[{"typeName":"CounterIncremented","domain":"Counter","isRejection":false,"schemaVersion":1}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminTypeCatalogApiClient client = CreateClient(httpClient);

        IReadOnlyList<EventTypeInfo> result = await client.ListEventTypesAsync(null);

        result.Count.ShouldBe(1);
        result[0].TypeName.ShouldBe("CounterIncremented");
        result[0].Domain.ShouldBe("Counter");
    }

    [Fact]
    public async Task ListEventTypesAsync_ReturnsEmpty_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminTypeCatalogApiClient client = CreateClient(httpClient);

        IReadOnlyList<EventTypeInfo> result = await client.ListEventTypesAsync(null);

        result.ShouldBeEmpty();
    }

    // === ListCommandTypesAsync ===

    [Fact]
    public async Task ListCommandTypesAsync_ReturnsTypes_WhenApiResponds() {
        string json = """[{"typeName":"IncrementCounter","domain":"Counter","targetAggregateType":"CounterAggregate"}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminTypeCatalogApiClient client = CreateClient(httpClient);

        IReadOnlyList<CommandTypeInfo> result = await client.ListCommandTypesAsync(null);

        result.Count.ShouldBe(1);
        result[0].TypeName.ShouldBe("IncrementCounter");
        result[0].TargetAggregateType.ShouldBe("CounterAggregate");
    }

    // === ListAggregateTypesAsync ===

    [Fact]
    public async Task ListAggregateTypesAsync_ReturnsTypes_WhenApiResponds() {
        string json = """[{"typeName":"CounterAggregate","domain":"Counter","eventCount":3,"commandCount":2,"hasProjections":true}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminTypeCatalogApiClient client = CreateClient(httpClient);

        IReadOnlyList<AggregateTypeInfo> result = await client.ListAggregateTypesAsync(null);

        result.Count.ShouldBe(1);
        result[0].TypeName.ShouldBe("CounterAggregate");
        result[0].Domain.ShouldBe("Counter");
        result[0].HasProjections.ShouldBeTrue();
    }
}
