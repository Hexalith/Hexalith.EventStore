using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminBackupApiClientTests
{
    private static AdminBackupApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminBackupApiClient(factory, NullLogger<AdminBackupApiClient>.Instance);
    }

    [Fact]
    public async Task GetBackupJobsAsync_ReturnsJobs_WhenApiResponds()
    {
        string json = """[{"backupId":"bk-1","tenantId":"t1","streamId":null,"description":"Daily backup","jobType":0,"status":2,"includeSnapshots":true,"createdAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T01:00:00Z","eventCount":1000,"sizeBytes":50000,"isValidated":true,"errorMessage":null}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminBackupApiClient client = CreateClient(httpClient);

        IReadOnlyList<BackupJob> result = await client.GetBackupJobsAsync();

        result.Count.ShouldBe(1);
        result[0].BackupId.ShouldBe("bk-1");
        result[0].TenantId.ShouldBe("t1");
        result[0].Status.ShouldBe(BackupJobStatus.Completed);
        result[0].IsValidated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetBackupJobsAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminBackupApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetBackupJobsAsync());
    }
}
