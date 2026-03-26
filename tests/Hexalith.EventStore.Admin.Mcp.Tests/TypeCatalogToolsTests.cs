namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class TypeCatalogToolsTests
{
    [Fact]
    public async Task ListTypes_ReturnsCombinedJson_OnSuccess()
    {
        var handler = new QueuedMockHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """[{"typeName":"OrderPlaced","domain":"Orders","isRejection":false,"schemaVersion":1}]""")
            .EnqueueJson(HttpStatusCode.OK, """[{"typeName":"PlaceOrder","domain":"Orders","targetAggregateType":"Order"}]""")
            .EnqueueJson(HttpStatusCode.OK, """[{"typeName":"Order","domain":"Orders","eventCount":3,"commandCount":2,"hasProjections":true}]""");
        using HttpClient httpClient = handler.ToHttpClient();
        var client = new AdminApiClient(httpClient);

        string result = await TypeCatalogTools.ListTypes(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("eventTypes").GetArrayLength().ShouldBe(1);
        doc.RootElement.GetProperty("commandTypes").GetArrayLength().ShouldBe(1);
        doc.RootElement.GetProperty("aggregateTypes").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ListTypes_HandlesPartialFailure()
    {
        var handler = new QueuedMockHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """[{"typeName":"OrderPlaced","domain":"Orders","isRejection":false,"schemaVersion":1}]""")
            .EnqueueException(new HttpRequestException("Connection refused"))
            .EnqueueJson(HttpStatusCode.OK, """[{"typeName":"Order","domain":"Orders","eventCount":3,"commandCount":2,"hasProjections":true}]""");
        using HttpClient httpClient = handler.ToHttpClient();
        var client = new AdminApiClient(httpClient);

        string result = await TypeCatalogTools.ListTypes(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        // Successful categories still have their data
        doc.RootElement.GetProperty("eventTypes").GetArrayLength().ShouldBe(1);
        doc.RootElement.GetProperty("aggregateTypes").GetArrayLength().ShouldBe(1);
        // Failed category has error info
        doc.RootElement.GetProperty("commandTypes").GetProperty("error").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ListTypes_ReturnsErrorJson_WhenAllCallsFail()
    {
        var handler = new QueuedMockHttpMessageHandler()
            .EnqueueException(new HttpRequestException("Connection refused"))
            .EnqueueException(new HttpRequestException("Connection refused"))
            .EnqueueException(new HttpRequestException("Connection refused"));
        using HttpClient httpClient = handler.ToHttpClient();
        var client = new AdminApiClient(httpClient);

        string result = await TypeCatalogTools.ListTypes(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task ListTypes_PassesDomainFilterParameter()
    {
        List<Uri?> capturedUris = [];
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUris.Add(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);

        _ = await TypeCatalogTools.ListTypes(client, domain: "Orders");

        capturedUris.Count.ShouldBe(3);
        capturedUris.ShouldAllBe(u => u!.PathAndQuery.Contains("domain=Orders"));
    }
}
