using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public class EventStoreGatewayClientTests {
    [Fact]
    public async Task SubmitCommandAsync_PostsContractRequestAndReturnsCorrelationId() {
        HttpRequestMessage? observedRequest = null;
        string? observedBody = null;
        using HttpClient httpClient = CreateClient(async request => {
            observedRequest = request;
            observedBody = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return Json(HttpStatusCode.Accepted, "{\"correlationId\":\"corr-1\"}");
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));
        SubmitCommandRequest request = CreateCommandRequest();

        SubmitCommandResponse response = await client.SubmitCommandAsync(request);

        response.CorrelationId.ShouldBe("corr-1");
        observedRequest.ShouldNotBeNull();
        observedRequest.Method.ShouldBe(HttpMethod.Post);
        observedRequest.RequestUri!.AbsolutePath.ShouldBe("/api/v1/commands");
        observedBody.ShouldNotBeNull();
        observedBody.ShouldContain("\"messageId\":\"message-1\"");
        observedBody.ShouldContain("\"aggregateId\":\"party-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithNotModified_ReturnsCacheResultAndETag() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-1\"");
            return Task.FromResult(response);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), "\"etag-1\"");

        result.IsNotModified.ShouldBeTrue();
        result.Payload.ShouldBeNull();
        result.ETag.ShouldBe("etag-1");
        observedRequest.ShouldNotBeNull();
        observedRequest.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.Single().ShouldBe("\"etag-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithPayload_ReturnsTypedPayloadAndETag() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-2\"");
            return Task.FromResult(response);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult<CounterDto> result = await client.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        result.IsNotModified.ShouldBeFalse();
        result.CorrelationId.ShouldBe("corr-2");
        result.ETag.ShouldBe("etag-2");
        result.Payload.ShouldNotBeNull();
        result.Payload.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SubmitCommandAsync_WithProblemDetails_ThrowsGatewayException() {
        const string ProblemJson = """
            {
              "type": "https://hexalith.io/problems/insufficient-permissions",
              "title": "Insufficient Permissions",
              "status": 403,
              "detail": "Missing role.",
              "correlationId": "corr-denied",
              "tenantId": "tenant-a",
              "reason": "missing-role",
              "retryAfter": "PT30S",
              "errors": {
                "permissions": "commands:* is required"
              },
              "traceCode": "trace-123"
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.Forbidden, ProblemJson, "application/problem+json")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(403);
        ex.Title.ShouldBe("Insufficient Permissions");
        ex.Type.ShouldBe("https://hexalith.io/problems/insufficient-permissions");
        ex.Detail.ShouldBe("Missing role.");
        ex.CorrelationId.ShouldBe("corr-denied");
        ex.TenantId.ShouldBe("tenant-a");
        ex.Reason.ShouldBe("missing-role");
        ex.RetryAfter.ShouldBe("PT30S");
        ex.Errors.ShouldContainKey("permissions");
        ex.Errors["permissions"].ShouldBe("commands:* is required");
        ex.Extensions.ShouldContainKey("traceCode");
        ex.Extensions["traceCode"].GetString().ShouldBe("trace-123");
    }

    [Fact]
    public async Task SubmitCommandAsync_WithRawPayloadTooLargeResponse_ThrowsStatusOnlyGatewayException() {
        using HttpClient httpClient = CreateClient(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge) {
                ReasonPhrase = "Payload Too Large",
                Content = new StringContent("too large", Encoding.UTF8, "text/plain"),
            }));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(413);
        ex.Title.ShouldBe("Payload Too Large");
        ex.CorrelationId.ShouldBeNull();
        ex.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitCommandAsync_WithCanceledToken_ThrowsOperationCanceledException() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        using HttpClient httpClient = CreateClient(_ =>
            Task.FromResult(Json(HttpStatusCode.Accepted, "{\"correlationId\":\"corr-1\"}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SubmitCommandAsync(CreateCommandRequest(), cancellationSource.Token));
    }

    [Fact]
    public async Task SubmitQueryAsync_WithSuccessFalse_ThrowsSemanticGatewayException() {
        const string QueryJson = """
            {
              "correlationId": "corr-semantic",
              "success": false,
              "errorMessage": "Projection denied.",
              "payload": {
                "reason": "forbidden"
              }
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, QueryJson)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Title.ShouldBe("Query semantic failure");
        ex.Detail.ShouldBe("Projection denied.");
        ex.CorrelationId.ShouldBe("corr-semantic");
    }

    [Fact]
    public async Task SubmitQueryAsyncTyped_WithSuccessFalse_ThrowsSemanticGatewayException() {
        const string QueryJson = """
            {
              "correlationId": "corr-semantic",
              "success": false,
              "errorMessage": "Projection denied.",
              "payload": {
                "reason": "forbidden"
              }
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, QueryJson)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync<CounterDto>(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Title.ShouldBe("Query semantic failure");
        ex.Detail.ShouldBe("Projection denied.");
        ex.CorrelationId.ShouldBe("corr-semantic");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithUnquotedIfNoneMatch_SendsQuotedHeader() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}"));
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await client.SubmitQueryAsync(CreateQueryRequest(), "etag-1");

        observedRequest.ShouldNotBeNull();
        observedRequest.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.Single().ShouldBe("\"etag-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithEmptyIfNoneMatch_OmitsHeader() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}"));
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await client.SubmitQueryAsync(CreateQueryRequest(), " ");

        observedRequest.ShouldNotBeNull();
        observedRequest.Headers.Contains("If-None-Match").ShouldBeFalse();
    }

    [Theory]
    [InlineData("W/\"etag-1\"")]
    [InlineData("\"unterminated")]
    [InlineData("*")]
    public async Task SubmitQueryAsync_WithUnsupportedIfNoneMatch_ThrowsArgumentException(string ifNoneMatch) {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), ifNoneMatch));
    }

    [Fact]
    public async Task SubmitQueryAsync_WithWeakResponseETag_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-2\"", isWeak: true);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response contained an unsupported weak ETag.");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithMalformedBody_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response body could not be parsed.");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithEmptyBody_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, string.Empty)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response body could not be parsed.");
    }

    [Fact]
    public async Task SubmitQueryAsyncTyped_WithInvalidPayloadShape_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":\"invalid\"}}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync<CounterDto>(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response payload could not be deserialized.");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithCanceledToken_ThrowsOperationCanceledException() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        using HttpClient httpClient = CreateClient(_ =>
            Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), cancellationToken: cancellationSource.Token));
    }

    private static SubmitCommandRequest CreateCommandRequest() {
        JsonElement payload = JsonSerializer.SerializeToElement(new { name = "Demo" });
        return new SubmitCommandRequest(
            "message-1",
            "tenant-a",
            "party",
            "party-1",
            "CreateParty",
            payload,
            "message-1");
    }

    private static SubmitQueryRequest CreateQueryRequest()
        => new(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            QueryType: "GetParty",
            EntityId: "party-1");

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => CreateClient((request, _) => handler(request));

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new RecordingHandler(handler)) { BaseAddress = new Uri("https://eventstore.local/") };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string contentType = "application/json")
        => new(statusCode) {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };

    private sealed record CounterDto(int Count);

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
