using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminCompactionApiClientTests
{
    private static AdminCompactionApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminCompactionApiClient(factory, NullLogger<AdminCompactionApiClient>.Instance);
    }

    [Fact]
    public async Task GetCompactionJobsAsync_ReturnsJobs_WhenApiResponds()
    {
        string json = """[{"operationId":"op-1","tenantId":"t1","domain":null,"status":2,"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T01:00:00Z","eventsCompacted":500,"spaceReclaimedBytes":1024,"errorMessage":null}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminCompactionApiClient client = CreateClient(httpClient);

        IReadOnlyList<CompactionJob> result = await client.GetCompactionJobsAsync();

        result.Count.ShouldBe(1);
        result[0].OperationId.ShouldBe("op-1");
        result[0].TenantId.ShouldBe("t1");
        result[0].Status.ShouldBe(CompactionJobStatus.Completed);
    }

    [Fact]
    public async Task GetCompactionJobsAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminCompactionApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetCompactionJobsAsync());
    }
}
