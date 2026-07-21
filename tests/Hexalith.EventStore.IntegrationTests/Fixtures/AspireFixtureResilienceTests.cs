using System.Net;

using Hexalith.EventStore.IntegrationTests.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

public class AspireFixtureResilienceTests {
    [Fact]
    public async Task ContractClientRetriesSafeReadsButNotCommandPostsAsync() {
        var handler = new CountingFailureHandler();
        var services = new ServiceCollection();
        _ = services.ConfigureHttpClientDefaults(clientBuilder =>
            _ = clientBuilder.AddStandardResilienceHandler(options => {
                AspireContractTestFixture.ConfigureTestClientResilience(options);
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.MaxRetryAttempts = 3;
            }));
        _ = services.AddHttpClient("contract-proof")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("contract-proof");

        using HttpResponseMessage postResponse = await client
            .PostAsync("https://eventstore.test/api/v1/commands", content: null)
            .ConfigureAwait(true);

        postResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        handler.AttemptCount.ShouldBe(1);

        using HttpResponseMessage getResponse = await client
            .GetAsync("https://eventstore.test/health")
            .ConfigureAwait(true);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        handler.AttemptCount.ShouldBe(5);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Conflict, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public void WriterProtocolActivationResponseHasDeterministicRetryDecision(
        HttpStatusCode statusCode,
        bool expected) {
        AspireTopologyFixture.ShouldRetryActivationResponse(statusCode).ShouldBe(expected);
    }

    private sealed class CountingFailureHandler : HttpMessageHandler {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            AttemptCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
