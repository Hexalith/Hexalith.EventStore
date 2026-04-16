using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminConsistencyApiClientTests {
    private static AdminConsistencyApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminConsistencyApiClient(factory, NullLogger<AdminConsistencyApiClient>.Instance);
    }

    [Fact]
    public async Task GetChecksAsync_ReturnsChecks_WhenApiResponds() {
        string json = """[{"checkId":"chk-1","status":2,"tenantId":"t1","domain":null,"checkTypes":[0],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T01:00:00Z","timeoutUtc":"2026-01-01T02:00:00Z","streamsChecked":50,"anomaliesFound":0}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminConsistencyApiClient client = CreateClient(httpClient);

        IReadOnlyList<ConsistencyCheckSummary> result = await client.GetChecksAsync();

        result.Count.ShouldBe(1);
        result[0].CheckId.ShouldBe("chk-1");
        result[0].Status.ShouldBe(ConsistencyCheckStatus.Completed);
        result[0].StreamsChecked.ShouldBe(50);
        result[0].AnomaliesFound.ShouldBe(0);
    }

    [Fact]
    public async Task GetChecksAsync_ThrowsServiceUnavailable_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminConsistencyApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetChecksAsync());
    }
}
