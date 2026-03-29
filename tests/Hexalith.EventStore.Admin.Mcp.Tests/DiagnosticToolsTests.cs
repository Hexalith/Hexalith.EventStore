namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class DiagnosticToolsTests
{
    private static readonly string _diffJson = """{"fromSequence":1,"toSequence":5,"changedFields":[{"fieldPath":"$.status","oldValue":"\"pending\"","newValue":"\"completed\""}]}""";
    private static readonly string _causationJson = """{"originatingCommandType":"PlaceOrder","originatingCommandId":"cmd-1","correlationId":"corr-1","userId":"user-1","events":[{"sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z"}],"affectedProjections":["OrderSummary"]}""";

    [Fact]
    public async Task DiffAggregateState_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _diffJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.DiffAggregateState(client, "t1", "Orders", "o1", 1, 5);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("fromSequence").GetInt64().ShouldBe(1);
        doc.RootElement.GetProperty("toSequence").GetInt64().ShouldBe(5);
    }

    [Fact]
    public async Task DiffAggregateState_ReturnsNotFound_WhenNull()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.DiffAggregateState(client, "t1", "Orders", "o1", 1, 5);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task DiffAggregateState_ReturnsUnreachableError_OnConnectionFailure()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.DiffAggregateState(client, "t1", "Orders", "o1", 1, 5);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task TraceCausationChain_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _causationJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.TraceCausationChain(client, "t1", "Orders", "o1", 1);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("originatingCommandType").GetString().ShouldBe("PlaceOrder");
    }

    [Fact]
    public async Task TraceCausationChain_ReturnsNotFound_WhenNull()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.TraceCausationChain(client, "t1", "Orders", "o1", 999);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task TraceCausationChain_ReturnsTimeoutError_OnTaskCanceled()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new TaskCanceledException("Request timed out"));
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.TraceCausationChain(client, "t1", "Orders", "o1", 1);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
    }

    [Fact]
    public async Task DiffAggregateState_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _diffJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.DiffAggregateState(client, "t1", "Orders", "o1", 1, 5);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Fact]
    public async Task TraceCausationChain_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _causationJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.TraceCausationChain(client, "t1", "Orders", "o1", 1);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Theory]
    [InlineData("", "d1", "a1")]
    [InlineData("t1", "", "a1")]
    [InlineData("t1", "d1", "")]
    [InlineData("   ", "d1", "a1")]
    public async Task DiffAggregateState_ReturnsInvalidInputError_WhenRequiredParamEmpty(string tenantId, string domain, string aggregateId)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _diffJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.DiffAggregateState(client, tenantId, domain, aggregateId, 1, 5);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Theory]
    [InlineData("", "d1", "a1")]
    [InlineData("t1", "", "a1")]
    [InlineData("t1", "d1", "")]
    [InlineData("   ", "d1", "a1")]
    public async Task TraceCausationChain_ReturnsInvalidInputError_WhenRequiredParamEmpty(string tenantId, string domain, string aggregateId)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _causationJson);
        var client = new AdminApiClient(httpClient);

        string result = await DiagnosticTools.TraceCausationChain(client, tenantId, domain, aggregateId, 1);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }
}
