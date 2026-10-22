using System.Net;
using System.Text;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public class EventStoreGatewayClientStreamTests {
    [Fact]
    public async Task ReadStreamAsyncPostsPublicStreamReadRouteAndReturnsPage() {
        HttpRequestMessage? observedRequest = null;
        string? observedBody = null;
        using HttpClient httpClient = CreateClient(async request => {
            observedRequest = request;
            observedBody = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return Json(HttpStatusCode.OK, """
                {
                  "tenant": "tenant-a",
                  "domain": "party",
                  "aggregateId": "party-1",
                  "events": [],
                  "metadata": {
                    "fromSequence": 0,
                    "toSequence": null,
                    "lastSequenceReturned": null,
                    "latestSequence": 0,
                    "eventCount": 0,
                    "isTruncated": false,
                    "nextContinuationToken": null
                  }
                }
                """);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        StreamReadPage page = await client.ReadStreamAsync(new StreamReadRequest(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            FromSequence: 0,
            PageSize: 25));

        page.Metadata.EventCount.ShouldBe(0);
        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Method.ShouldBe(HttpMethod.Post);
        observedRequest.RequestUri!.AbsolutePath.ShouldBe("/api/v1/streams/read");
        _ = observedBody.ShouldNotBeNull();
        observedBody.ShouldContain("\"tenant\":\"tenant-a\"");
        observedBody.ShouldContain("\"pageSize\":25");
    }

    [Fact]
    public async Task ReadStreamAsyncWithProblemDetailsThrowsGatewayExceptionWithReasonCode() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.BadRequest, """
            {
              "type": "https://hexalith.io/problems/validation-error",
              "title": "Bad Request",
              "status": 400,
              "detail": "Continuation token does not match the request.",
              "reasonCode": "token-request-mismatch"
            }
            """, "application/problem+json")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.ReadStreamAsync(new StreamReadRequest("tenant-a", "party", "party-1")));

        ex.StatusCode.ShouldBe(400);
        ex.ReasonCode.ShouldBe(StreamReplayReasonCodes.TokenRequestMismatch);
    }

    [Fact]
    public void ConstructorAppliesConfiguredStreamReadResponseSizeCap() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        _ = new EventStoreGatewayClient(
            httpClient,
            Options.Create(new EventStoreGatewayClientOptions { MaxStreamReadResponseBytes = 1024 }));

        httpClient.MaxResponseContentBufferSize.ShouldBe(1024);
    }

    // P16-6P: validate the constructor's MaxStreamReadResponseBytes guards.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ConstructorRejectsNonPositiveMaxStreamReadResponseBytes(long invalid) {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EventStoreGatewayClient(
            httpClient,
            Options.Create(new EventStoreGatewayClientOptions { MaxStreamReadResponseBytes = invalid })));
    }

    [Fact]
    public void ConstructorRejectsMaxStreamReadResponseBytesGreaterThanIntMaxValue() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EventStoreGatewayClient(
            httpClient,
            Options.Create(new EventStoreGatewayClientOptions { MaxStreamReadResponseBytes = (long)int.MaxValue + 1 })));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new RecordingHandler((request, _) => handler(request))) { BaseAddress = new Uri("https://eventstore.local/") };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string contentType = "application/json")
        => new(statusCode) {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
