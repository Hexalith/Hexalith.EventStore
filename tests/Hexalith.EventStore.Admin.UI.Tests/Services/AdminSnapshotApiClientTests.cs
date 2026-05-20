using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminSnapshotApiClientTests {
    private static AdminSnapshotApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminSnapshotApiClient(factory, NullLogger<AdminSnapshotApiClient>.Instance);
    }

    [Fact]
    public async Task GetSnapshotPoliciesAsync_ReturnsPolicies_WhenApiResponds() {
        string json = """[{"tenantId":"t1","domain":"Counter","aggregateType":"CounterAggregate","intervalEvents":100,"createdAtUtc":"2026-01-01T00:00:00Z"}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminSnapshotApiClient client = CreateClient(httpClient);

        IReadOnlyList<SnapshotPolicy> result = await client.GetSnapshotPoliciesAsync();

        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe("t1");
        result[0].Domain.ShouldBe("Counter");
        result[0].IntervalEvents.ShouldBe(100);
    }

    [Fact]
    public async Task GetSnapshotPoliciesAsync_ThrowsServiceUnavailable_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminSnapshotApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetSnapshotPoliciesAsync());
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsDeferredResult_WhenApiReturnsTypedOutcome() {
        string json = """{"success":false,"operationId":"deferred-snapshot-policy-set","message":"Snapshot policy changes are deferred.","errorCode":"Deferred"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminSnapshotApiClient client = CreateClient(httpClient);

        AdminOperationResult? result = await client.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        _ = result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("deferred");
    }

    [Fact]
    public async Task CreateSnapshotAsync_ReturnsDeferredResult_WhenApiReturnsTypedOutcome() {
        string json = """{"success":false,"operationId":"deferred-manual-snapshot","message":"Manual snapshot creation is deferred.","errorCode":"Deferred"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminSnapshotApiClient client = CreateClient(httpClient);

        AdminOperationResult? result = await client.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        _ = result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("deferred");
    }

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsDeferredResult_WhenApiReturnsTypedOutcome() {
        string json = """{"success":false,"operationId":"deferred-snapshot-policy-delete","message":"Snapshot policy deletion is deferred.","errorCode":"Deferred"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminSnapshotApiClient client = CreateClient(httpClient);

        AdminOperationResult? result = await client.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        _ = result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("deferred");
    }
}
