using System.Net;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

public class AspireContractHttpResilienceTests {
    [Fact]
    public async Task Configure_TransientPostResponse_DoesNotRetryUnsafeRequestAsync() {
        using var handler = new CountingTransientHandler();
        using ServiceProvider provider = BuildProvider(handler);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("contract-proof");
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://contract.test/command") {
            Content = new StringContent("{}"),
        };

        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        handler.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task Configure_TransientGetResponse_RetriesSafeRequestAsync() {
        using var handler = new CountingTransientHandler();
        using ServiceProvider provider = BuildProvider(handler);
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("contract-proof");

        using HttpResponseMessage response = await client
            .GetAsync("http://contract.test/status")
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.AttemptCount.ShouldBe(2);
    }

    private static ServiceProvider BuildProvider(CountingTransientHandler handler) {
        var services = new ServiceCollection();
        _ = services
            .AddHttpClient("contract-proof")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(options => {
                AspireContractHttpResilience.Configure(options);
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
            });
        return services.BuildServiceProvider();
    }

    private sealed class CountingTransientHandler : HttpMessageHandler {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            AttemptCount++;
            HttpStatusCode statusCode = AttemptCount == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
