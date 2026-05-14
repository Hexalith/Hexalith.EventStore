using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeEventStoreGatewayClientTests {
    [Fact]
    public async Task SubmitCommandAsync_RecordsRequestAndReturnsConfiguredResponse() {
        var fake = new FakeEventStoreGatewayClient {
            CommandResponse = new SubmitCommandResponse("corr-1"),
        };
        SubmitCommandRequest request = CreateCommandRequest();

        SubmitCommandResponse response = await fake.SubmitCommandAsync(request);

        response.CorrelationId.ShouldBe("corr-1");
        fake.SubmittedCommands.Single().ShouldBe(request);
    }

    [Fact]
    public async Task SubmitQueryAsync_RecordsIfNoneMatchAndReturnsConfiguredResult() {
        var fake = new FakeEventStoreGatewayClient {
            QueryResult = new EventStoreQueryResult(
                "corr-2",
                JsonSerializer.SerializeToElement(new CounterDto(7)),
                IsNotModified: false,
                ETag: "etag-2"),
        };
        SubmitQueryRequest request = CreateQueryRequest();

        EventStoreQueryResult<CounterDto> response = await fake.SubmitQueryAsync<CounterDto>(request, "\"etag-1\"");

        response.Payload.ShouldNotBeNull();
        response.Payload.Count.ShouldBe(7);
        SubmittedQuery submitted = fake.SubmittedQueries.Single();
        submitted.Request.ShouldBe(request);
        submitted.IfNoneMatch.ShouldBe("\"etag-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WhenConfiguredNotModified_ReturnsTypedNotModified() {
        var fake = new FakeEventStoreGatewayClient {
            QueryResult = new EventStoreQueryResult(null, null, IsNotModified: true, ETag: "etag-1"),
        };

        EventStoreQueryResult<CounterDto> response = await fake.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        response.IsNotModified.ShouldBeTrue();
        response.Payload.ShouldBeNull();
        response.ETag.ShouldBe("etag-1");
    }

    [Fact]
    public async Task DenyCommands_ConfiguresReasonCodeAndTenantId() {
        var fake = new FakeEventStoreGatewayClient();
        fake.DenyCommands(AuthorizationReasonCodes.TenantDisabled, "tenant-a", "Tenant is disabled.");

        EventStoreGatewayException ex = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(403);
        ex.ReasonCode.ShouldBe(AuthorizationReasonCodes.TenantDisabled);
        ex.TenantId.ShouldBe("tenant-a");
        ex.Detail.ShouldBe("Tenant is disabled.");
    }

    [Fact]
    public async Task DenyQueries_ConfiguresReasonCodeAndTenantId() {
        var fake = new FakeEventStoreGatewayClient();
        fake.DenyQueries(AuthorizationReasonCodes.InsufficientPermission, "tenant-a");

        EventStoreGatewayException ex = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(403);
        ex.ReasonCode.ShouldBe(AuthorizationReasonCodes.InsufficientPermission);
        ex.TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public async Task SetAuthorizationUnavailable_ConfiguresRetryableFailureForBothPaths() {
        var fake = new FakeEventStoreGatewayClient();
        fake.SetAuthorizationUnavailable();

        EventStoreGatewayException command = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest()));
        EventStoreGatewayException query = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest()));

        command.StatusCode.ShouldBe(503);
        command.ReasonCode.ShouldBe(AuthorizationReasonCodes.AuthorizationServiceUnavailable);
        command.RetryAfterSeconds.ShouldBe(30);
        query.StatusCode.ShouldBe(503);
        query.ReasonCode.ShouldBe(AuthorizationReasonCodes.AuthorizationServiceUnavailable);
        query.RetryAfterSeconds.ShouldBe(30);
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
        => new("tenant-a", "party", "party-1", "GetParty", EntityId: "party-1");

    private sealed record CounterDto(int Count);
}
