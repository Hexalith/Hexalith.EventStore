using System.Net;
using System.Text;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Security;

public sealed class ProjectionDeliveryWriterProtocolTestLeaseTests {
    private const string MarkerKey = "projection-delivery-writer-protocol";
    private const string RedisEndpoint = "localhost:6379";

    [Fact]
    public async Task ActivateAndRestoreAsync_CleanStore_UsesProductionCutoverAndRestoresState() {
        const string sourceCommit = "0123456789abcdef0123456789abcdef01234567";
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(true);
        IDatabase database = redis.GetDatabase();
        byte[]? originalState = await database.KeyDumpAsync(MarkerKey).ConfigureAwait(true);
        var lease = new ProjectionDeliveryWriterProtocolTestLease();

        try {
            using var client = new HttpClient(new CutoverHandler(database, sourceCommit)) {
                BaseAddress = new Uri("https://eventstore.test"),
            };
            await lease.ActivateAsync(
                client,
                "admin-token",
                sourceCommit,
                "fixture-test-backup",
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            RedisValue activated = await database.HashGetAsync(MarkerKey, "data").ConfigureAwait(true);
            activated.ToString().ShouldContain(sourceCommit);

            await lease.RestoreAsync().ConfigureAwait(true);
            byte[]? restoredState = await database.KeyDumpAsync(MarkerKey).ConfigureAwait(true);
            if (originalState is null) {
                restoredState.ShouldBeNull();
            }
            else {
                restoredState.ShouldBe(originalState);
            }
        }
        finally {
            try {
                await lease.RestoreAsync().ConfigureAwait(true);
            }
            finally {
                await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(true);
            }
        }
    }

    private sealed class CutoverHandler(IDatabase database, string sourceCommit) : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            if (request.Method == HttpMethod.Get
                && string.Equals(request.RequestUri?.AbsolutePath, "/health", StringComparison.Ordinal)) {
                return JsonResponse(
                    HttpStatusCode.ServiceUnavailable,
                    "{\"results\":{\"projection-delivery-writer-protocol\":{\"status\":\"Unhealthy\"},"
                    + "\"redis\":{\"status\":\"Healthy\"}}}");
            }

            if (request.Method == HttpMethod.Post
                && string.Equals(
                    request.RequestUri?.AbsolutePath,
                    "/api/v1/admin/projections/delivery-writer-protocol/activate",
                    StringComparison.Ordinal)) {
                request.Headers.Authorization?.Parameter.ShouldBe("admin-token");
                _ = await database.HashSetAsync(
                    MarkerKey,
                    "data",
                    $"{{\"schemaVersion\":1,\"writerProtocolVersion\":2,\"cutoverCommit\":\"{sourceCommit}\"}}")
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return JsonResponse(HttpStatusCode.OK, "{\"status\":\"Activated\"}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
            => new(statusCode) {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }
}
