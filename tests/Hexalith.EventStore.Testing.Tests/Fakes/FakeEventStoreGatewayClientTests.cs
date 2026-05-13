using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Testing.Builders;
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
    public async Task ConfigureCommandAccepted_ConfiguresTypedAcceptedResponse() {
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureCommandAccepted("corr-accepted");

        SubmitCommandResponse response = await fake.SubmitCommandAsync(CreateCommandRequest());

        response.CorrelationId.ShouldBe("corr-accepted");
    }

    [Fact]
    public async Task ConfigureCommandFailure_ThrowsConfiguredProblemDetailsException() {
        EventStoreGatewayException exception = EventStoreGatewayExceptionBuilder
            .Validation("corr-validation", "tenant-a", new Dictionary<string, string> { ["tenant"] = "Tenant is required." })
            .Build();
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureCommandFailure(exception);

        EventStoreGatewayException thrown = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest()));

        thrown.StatusCode.ShouldBe(400);
        thrown.Errors["tenant"].ShouldBe("Tenant is required.");
    }

    [Fact]
    public async Task ConfigureQuerySuccess_ConfiguresPayloadAndNormalizedETag() {
        JsonElement payload = JsonSerializer.SerializeToElement(new CounterDto(11));
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureQuerySuccess(payload, "corr-query", "etag-query");

        EventStoreQueryResult<CounterDto> response = await fake.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        response.CorrelationId.ShouldBe("corr-query");
        response.Payload.ShouldNotBeNull();
        response.Payload.Count.ShouldBe(11);
        response.ETag.ShouldBe("etag-query");
    }

    [Fact]
    public async Task ConfigureQuerySemanticFailure_ThrowsGatewayExceptionWithCorrelationId() {
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureQuerySemanticFailure("corr-semantic", "Projection denied.");

        EventStoreGatewayException thrown = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest()));

        thrown.StatusCode.ShouldBe(200);
        thrown.Title.ShouldBe("Query semantic failure");
        thrown.Detail.ShouldBe("Projection denied.");
        thrown.CorrelationId.ShouldBe("corr-semantic");
    }

    [Fact]
    public async Task ConfigureQueryFailure_ThrowsConfiguredUnavailableException() {
        EventStoreGatewayException exception = EventStoreGatewayExceptionBuilder
            .Unavailable("corr-down", "tenant-a", "PT30S")
            .Build();
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureQueryFailure(exception);

        EventStoreGatewayException thrown = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest()));

        thrown.StatusCode.ShouldBe(503);
        thrown.RetryAfter.ShouldBe("PT30S");
    }

    [Fact]
    public async Task ConfigureQueryNotModified_ConfiguresCacheResult() {
        var fake = new FakeEventStoreGatewayClient()
            .ConfigureQueryNotModified("etag-cache");

        EventStoreQueryResult response = await fake.SubmitQueryAsync(CreateQueryRequest());

        response.IsNotModified.ShouldBeTrue();
        response.Payload.ShouldBeNull();
        response.ETag.ShouldBe("etag-cache");
    }

    [Fact]
    public async Task SubmitCommandAsync_WithCanceledToken_DoesNotRecordRequest() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var fake = new FakeEventStoreGatewayClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest(), cancellationSource.Token));

        fake.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitQueryAsync_WithCanceledToken_DoesNotRecordRequest() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var fake = new FakeEventStoreGatewayClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest(), cancellationToken: cancellationSource.Token));

        fake.SubmittedQueries.ShouldBeEmpty();
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
