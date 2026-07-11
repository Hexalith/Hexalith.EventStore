using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
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

        _ = response.Payload.ShouldNotBeNull();
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
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.ETag.ShouldBe("etag-1");
        response.Metadata.IsNotModified.ShouldBe(true);
    }

    [Fact]
    public async Task ConfigureCommandAccepted_ConfiguresTypedAcceptedResponse() {
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureCommandAccepted("corr-accepted");

        SubmitCommandResponse response = await fake.SubmitCommandAsync(CreateCommandRequest());

        response.CorrelationId.ShouldBe("corr-accepted");
    }

    [Fact]
    public async Task ConfigureCommandFailure_ThrowsConfiguredProblemDetailsException() {
        EventStoreGatewayException exception = EventStoreGatewayExceptionBuilder
            .Validation("corr-validation", "tenant-a", new Dictionary<string, string> { ["tenant"] = "Tenant is required." })
            .Build();
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureCommandFailure(exception);

        EventStoreGatewayException thrown = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest()));

        thrown.StatusCode.ShouldBe(400);
        thrown.Errors["tenant"].ShouldBe("Tenant is required.");
    }

    [Fact]
    public async Task ConfigureQuerySuccess_ConfiguresPayloadAndNormalizedETag() {
        JsonElement payload = JsonSerializer.SerializeToElement(new CounterDto(11));
        var metadata = new QueryResponseMetadata(
            ETag: "etag-query",
            IsNotModified: false,
            Paging: new QueryPagingMetadata(25, Offset: 50)) {
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = ProjectionLifecycleState.Degraded,
        };
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureQuerySuccess(payload, "corr-query", "etag-query", metadata);

        EventStoreQueryResult<CounterDto> response = await fake.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        response.CorrelationId.ShouldBe("corr-query");
        _ = response.Payload.ShouldNotBeNull();
        response.Payload.Count.ShouldBe(11);
        response.ETag.ShouldBe("etag-query");
        response.Metadata.ShouldBe(metadata);
        response.Metadata!.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
    }

    [Fact]
    public async Task ConfigureQuerySemanticFailure_ThrowsGatewayExceptionWithCorrelationId() {
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
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
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureQueryFailure(exception);

        EventStoreGatewayException thrown = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => fake.SubmitQueryAsync(CreateQueryRequest()));

        thrown.StatusCode.ShouldBe(503);
        thrown.RetryAfter.ShouldBe("PT30S");
    }

    [Fact]
    public async Task ConfigureQueryNotModified_ConfiguresCacheResult() {
        var metadata = new QueryResponseMetadata(ETag: "different-etag", IsNotModified: false) {
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = ProjectionLifecycleState.Current,
        };
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureQueryNotModified("etag-cache", metadata);

        EventStoreQueryResult response = await fake.SubmitQueryAsync(CreateQueryRequest());

        response.IsNotModified.ShouldBeTrue();
        response.Payload.ShouldBeNull();
        response.ETag.ShouldBe("etag-cache");
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.ETag.ShouldBe("etag-cache");
        response.Metadata.IsNotModified.ShouldBe(true);
        response.Metadata.ETag.ShouldBe(response.ETag);
        response.Metadata.IsNotModified.ShouldBe(response.IsNotModified);
        response.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);

        EventStoreQueryResult<CounterDto> typed = await fake.SubmitQueryAsync<CounterDto>(CreateQueryRequest());
        _ = typed.Metadata.ShouldNotBeNull();
        typed.Metadata.Lifecycle.ShouldBe(metadata.Lifecycle);
        typed.Metadata.Provenance.ShouldBe(metadata.Provenance);
        typed.Metadata.ETag.ShouldBe(typed.ETag);
        typed.Metadata.IsNotModified.ShouldBe(typed.IsNotModified);
    }

    [Fact]
    public async Task ReadStreamAsyncRecordsRequestAndReturnsConfiguredPage() {
        var page = new StreamReadPage(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            Events: [],
            Metadata: new StreamReadMetadata(0, null, null, 0, 0, false, null));
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureStreamReadSuccess(page);
        var request = new StreamReadRequest("tenant-a", "party", "party-1", PageSize: 25);

        StreamReadPage response = await fake.ReadStreamAsync(request);

        response.ShouldBe(page);
        fake.SubmittedStreamReads.Single().ShouldBe(request);
    }

    [Fact]
    public async Task ConfigureStreamReadFailureThrowsConfiguredGatewayException() {
        EventStoreGatewayException exception = EventStoreGatewayExceptionBuilder
            .Conflict("corr-conflict", "tenant-a")
            .WithReasonCode(StreamReplayReasonCodes.CheckpointConflict)
            .Build();
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureStreamReadFailure(exception);

        EventStoreGatewayException thrown = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.ReadStreamAsync(new StreamReadRequest("tenant-a", "party", "party-1")));

        thrown.ReasonCode.ShouldBe(StreamReplayReasonCodes.CheckpointConflict);
    }

    [Fact]
    public void StreamReadPageBuilderBuildsContinuationPage() {
        StreamReadPage page = StreamReadPageBuilder
            .Create()
            .ForStream("tenant-a", "party", "party-1")
            .WithRange(10, 20)
            .AddEvent(11, "PartyRenamed", [1])
            .WithNextContinuation("next-token")
            .Build();

        page.Events.Single().SequenceNumber.ShouldBe(11);
        page.Metadata.IsTruncated.ShouldBeTrue();
        _ = page.Metadata.NextContinuationToken.ShouldNotBeNull();
        page.Metadata.NextContinuationToken.Value.ShouldBe("next-token");
    }

    [Fact]
    public void StreamReadPageBuilderDeterministicFactoryGeneratesDistinctEventIds() {
        StreamReadEvent item = StreamReadPageBuilder
            .Create()
            .WithDeterministicIds(sequence => $"id-{sequence}")
            .AddEvent(11)
            .Build()
            .Events
            .Single();

        item.MessageId.ShouldBe("id-11-message");
        item.CorrelationId.ShouldBe("id-11-correlation");
        item.CausationId.ShouldBe("id-11-causation");
    }

    [Fact]
    public void StreamReadPageBuilderRoleAwareFactoryReceivesEachIdKind() {
        StreamReadEvent item = StreamReadPageBuilder
            .Create()
            .WithDeterministicIds((sequence, kind) => $"{kind}:{sequence}")
            .AddEvent(11)
            .Build()
            .Events
            .Single();

        item.MessageId.ShouldBe("Message:11");
        item.CorrelationId.ShouldBe("Correlation:11");
        item.CausationId.ShouldBe("Causation:11");
    }

    [Fact]
    public async Task ConfigureStreamReadContinuationNormalizesMetadataFromEvents() {
        StreamReadPage inconsistent = new(
            "tenant-a",
            "party",
            "party-1",
            [
                new StreamReadEvent(12, "PartyRenamed", [], "json", 1, "msg-12", null, null, DateTimeOffset.UnixEpoch, null),
                new StreamReadEvent(13, "PartyRenamed", [], "json", 1, "msg-13", null, null, DateTimeOffset.UnixEpoch, null),
            ],
            new StreamReadMetadata(10, null, 99, 99, 99, false, null));
        FakeEventStoreGatewayClient fake = new FakeEventStoreGatewayClient()
            .ConfigureStreamReadContinuation(inconsistent, "next-token");

        StreamReadPage page = await fake.ReadStreamAsync(new StreamReadRequest("tenant-a", "party", "party-1"));

        page.Metadata.EventCount.ShouldBe(2);
        page.Metadata.LastSequenceReturned.ShouldBe(13);
        page.Metadata.LatestSequence.ShouldBe(99);
        page.Metadata.IsTruncated.ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadInvalidRange), StreamReplayReasonCodes.InvalidRange, 400)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadInvalidContinuation), StreamReplayReasonCodes.InvalidContinuation, 400)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadUnauthorizedTenant), StreamReplayReasonCodes.UnauthorizedTenant, 403)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadMissingStream), StreamReplayReasonCodes.MissingStream, 404)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadCheckpointConflict), StreamReplayReasonCodes.CheckpointConflict, 409)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadPausedRebuild), StreamReplayReasonCodes.RebuildPaused, 409)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadCanceledRebuild), StreamReplayReasonCodes.RebuildCanceled, 409)]
    [InlineData(nameof(FakeEventStoreGatewayClient.ConfigureStreamReadUnavailable), StreamReplayReasonCodes.ServiceUnavailable, 503)]
    public async Task StreamReadFailureHelpersExposeStableReasonCodes(string methodName, string reasonCode, int statusCode) {
        var fake = new FakeEventStoreGatewayClient();
        _ = typeof(FakeEventStoreGatewayClient)
            .GetMethod(methodName, [typeof(string)])!
            .Invoke(fake, ["tenant-a"]);

        EventStoreGatewayException thrown = await Should.ThrowAsync<EventStoreGatewayException>(
            () => fake.ReadStreamAsync(new StreamReadRequest("tenant-a", "party", "party-1")));

        thrown.StatusCode.ShouldBe(statusCode);
        thrown.ReasonCode.ShouldBe(reasonCode);
    }

    [Fact]
    public async Task SubmitCommandAsync_WithCanceledToken_DoesNotRecordRequest() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var fake = new FakeEventStoreGatewayClient();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fake.SubmitCommandAsync(CreateCommandRequest(), cancellationSource.Token));

        fake.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitQueryAsync_WithCanceledToken_DoesNotRecordRequest() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var fake = new FakeEventStoreGatewayClient();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
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
