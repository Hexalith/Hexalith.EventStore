
using System.Net;
using System.Text.Json;

using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;

using Google.Protobuf;

using Grpc.Core;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class QueryRouterTests {
    private static SubmitQuery CreateTestQuery(
        string queryType = "GetOrderStatus",
        string domain = "orders",
        string aggregateId = "order-1",
        string? entityId = null,
        byte[]? payload = null,
        string? projectionType = null,
        string? projectionActorType = null,
        string tenant = "test-tenant",
        QueryPagingOptions? paging = null) =>
        new(
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            QueryType: queryType,
            Payload: payload ?? [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            EntityId: entityId,
            ProjectionType: projectionType,
            ProjectionActorType: projectionActorType,
            Paging: paging);

    private static (IProjectionActorInvoker invoker, QueryRouter router) CreateRouterWithInvoker(QueryResult? result = null) {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        if (result is not null) {
            _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        return (invoker, router);
    }

    private static RpcException CreateRpcExceptionWithDaprErrorInfo(
        string errorCode,
        StatusCode statusCode = StatusCode.NotFound) {
        var statusDetails = new Google.Rpc.Status {
            Code = (int)statusCode,
            Message = "DAPR actor invocation failed.",
        };
        statusDetails.Details.Add(Google.Protobuf.WellKnownTypes.Any.Pack(new Google.Rpc.ErrorInfo {
            Reason = errorCode,
            Domain = "dapr.io",
        }));

        return new RpcException(
            new Status(statusCode, "DAPR actor invocation failed."),
            new Metadata {
                { "grpc-status-details-bin", statusDetails.ToByteArray() },
            });
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessfulQuery_ReturnsResultWithPayload() {
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeTrue();
        result.NotFound.ShouldBeFalse();
        _ = result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public void QueryRouterResult_PublicCompatibility_MaintainsOriginalConstructorShape() {
        typeof(QueryRouterResult)
            .GetConstructor([typeof(bool), typeof(JsonElement?), typeof(bool), typeof(string), typeof(string)])
            .ShouldNotBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessfulQuery_ReturnsResultWithMetadata() {
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        var metadata = new QueryResponseMetadata(
            IsStale: false,
            ProjectionVersion: "orders-v2",
            WarningCodes: [QueryWarningCodes.DegradedSearch]) {
            Provenance = QueryResponseProvenance.HandlerComputed,
            Lifecycle = ProjectionLifecycleState.Rebuilding,
        };
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(
            QueryResult.FromPayload(resultPayload, "orders", metadata));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeTrue();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Rebuilding);
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata!.ProjectionVersion.ShouldBe("orders-v2");
        _ = result.Metadata.WarningCodes.ShouldNotBeNull();
        result.Metadata.WarningCodes.ShouldContain(QueryWarningCodes.DegradedSearch);
    }

    [Fact]
    public async Task RouteQueryAsync_PersistedRebuildPhaseOverridesProjectionBackedCurrentEvidence() {
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<QueryEnvelope>(),
                Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(
                payload,
                "orders",
                new QueryResponseMetadata(IsStale: false, ProjectionVersion: "2") {
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _ = lifecycle.ReadPhaseAsync(
                Arg.Any<AggregateIdentity>(),
                "orders",
                Arg.Any<CancellationToken>())
            .Returns(ProjectionLifecyclePhase.Rebuilding);
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, lifecycle);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Metadata.ShouldNotBeNull().Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Rebuilding);
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata.ProjectionVersion.ShouldBe("2");
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public async Task RouteQueryAsync_OperationalLifecycle_PreservesExactValue(
        ProjectionLifecycleState lifecycle) {
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });
        var metadata = new QueryResponseMetadata {
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = lifecycle,
        };
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(
            QueryResult.FromPayload(payload, "counter", metadata));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(lifecycle);
    }

    [Fact]
    public async Task RouteQueryAsync_InvalidCursorFailure_DoesNotLogRawCursorDetail() {
        const string RawCursor = "protected.cursor.payload";
        var logs = new List<LogEntry>();
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.Failure(QueryAdapterFailureReason.InvalidCursor + ": " + RawCursor));
        var router = new QueryRouter(invoker, new TestLogger<QueryRouter>(logs));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain(RawCursor);
        LogEntry warning = logs.Single(e => e.Level == LogLevel.Warning);
        warning.Message.ShouldContain(QueryAdapterFailureReason.InvalidCursor);
        warning.Message.ShouldNotContain(RawCursor);
    }

    [Fact]
    public async Task RouteQueryAsync_RoutesToCorrectActor() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("actor type not registered", new InvalidOperationException(), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_LegacyAddressMarkerFallback_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("did not find address for actor 'ProjectionActor/GetOrderStatus:test-tenant'"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMissingDaprErrorCodeWithoutLegacyMessage_ReturnsNotFoundAndLogsProjectionActorNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new DaprApiException("adresse de projection absente", "ERR_ACTOR_NO_ADDRESS", false),
                false));
        ILogger<QueryRouter> logger = Substitute.For<ILogger<QueryRouter>>();
        _ = logger.IsEnabled(LogLevel.Warning).Returns(true);
        var router = new QueryRouter(invoker, logger);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
        logger.ReceivedCalls()
            .Any(call => call.GetMethodInfo().Name == nameof(ILogger.Log)
                && call.GetArguments()[0] is LogLevel.Warning
                && call.GetArguments()[1] is EventId { Id: 1203 })
            .ShouldBeTrue();
    }

    [Fact]
    public async Task RouteQueryAsync_DirectRuntimeMissingDaprErrorCode_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DaprApiException("runtime projection indisponible", "ERR_ACTOR_RUNTIME_NOT_FOUND", false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_GrpcRichDaprActorMissingErrorInfo_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                CreateRpcExceptionWithDaprErrorInfo("ERR_ACTOR_INSTANCE_MISSING"),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_PlainGrpcNotFoundStatusWithoutDaprErrorCode_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new RpcException(new Status(StatusCode.NotFound, "projection absente")),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_NonNotFoundDaprActorErrorCode_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new DaprApiException("projection method failed", "ERR_ACTOR_INVOKE_METHOD", false),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_PlacementDaprActorErrorCode_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor placement unavailable",
                new DaprApiException("placement service unavailable", "ERR_ACTOR_NO_PLACEMENT", true),
                true));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_DaprErrorCodeContradictsNotFoundStatus_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        RpcException statusException = CreateRpcExceptionWithDaprErrorInfo("ERR_ACTOR_NO_ADDRESS");
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new DaprApiException("method failed", statusException, "ERR_ACTOR_INVOKE_METHOD", false),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_LocalizedMessageWithoutTypedSignal_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "acteur de projection introuvable",
                new InvalidOperationException("aucune adresse pour cet acteur"),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_NonDaprNotFoundLookingMessageWithoutLegacyMarker_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("projection actor was not found in the local registry"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.PermissionDenied)]
    public async Task RouteQueryAsync_NonNotFoundGrpcStatus_ReturnsActorExceptionCategory(StatusCode statusCode) {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new RpcException(new Status(statusCode, "typed status failure")),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_HttpNotFoundStatusWithoutDaprErrorCode_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new HttpRequestException("projection absente", null, HttpStatusCode.NotFound),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RouteQueryAsync_NonNotFoundHttpStatus_ReturnsActorExceptionCategory(HttpStatusCode statusCode) {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor invocation failed",
                new HttpRequestException("typed status failure", null, statusCode),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationExceptionWithoutNotFoundPattern_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("projection query failed", new InvalidOperationException("boom"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionActorReturnsFailure_ReturnsFailedResult() {
        var metadata = new QueryResponseMetadata(IsDegraded: true, WarningCodes: [QueryWarningCodes.DegradedSearch]);
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.Failure("projection unavailable", metadata));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe("projection unavailable");
        result.Metadata.ShouldBe(metadata);
    }

    [Fact]
    public async Task RouteQueryAsync_GenericNotRegisteredMessage_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("serializer dependency not registered", new InvalidOperationException("boom"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_WithEntityId_RoutesToTier1ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery(entityId: "order-123"));

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant:order-123",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_WithNonEmptyPayloadAndNoEntityId_RoutesToTier2ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));
        byte[] payload = [0x01, 0x02, 0x03];

        _ = await router.RouteQueryAsync(CreateTestQuery(payload: payload));

        _ = await invoker.Received(1).InvokeAsync(
            Arg.Is<string>(id => id.StartsWith("GetOrderStatus:test-tenant:", StringComparison.Ordinal) && id.Split(':')[2].Length == 11),
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_WithEmptyPayloadAndNoEntityId_RoutesToTier3ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionType_PassesThroughFromQueryResult() {
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "order-list"));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("order-list");
    }

    [Fact]
    public async Task RouteQueryAsync_NullProjectionType_PassesThroughNull() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.ProjectionType.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ConstructsCorrectQueryEnvelope() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<QueryEnvelope>(e =>
                e.TenantId == "test-tenant" &&
                e.Domain == "orders" &&
                e.AggregateId == "order-1" &&
                e.QueryType == "GetOrderStatus" &&
                e.CorrelationId == "corr-1" &&
                e.UserId == "user-1" &&
                e.EntityId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ForwardsPagingToQueryEnvelope() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery(paging: new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor")));

        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<QueryEnvelope>(e =>
                e.Paging != null &&
                e.Paging.PageSize == 25 &&
                e.Paging.Cursor == "opaque-cursor"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_GetParty_UsesEntityScopedPublicAdapterRoute() {
        JsonElement resultPayload = JsonDocument.Parse("{\"id\":\"party-42\"}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party"));

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "get-party",
            domain: "parties",
            aggregateId: "party",
            entityId: "party-42",
            projectionType: "party"));

        _ = await invoker.Received(1).InvokeAsync(
            "party:test-tenant:party-42",
            QueryRouter.ProjectionActorTypeName,
            Arg.Is<QueryEnvelope>(e =>
                e.QueryType == "get-party" &&
                e.Domain == "parties" &&
                e.EntityId == "party-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ListParties_UsesTenantWideRouteAndActorTypeOverride() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party-list"));

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "list-parties",
            domain: "parties",
            aggregateId: "party",
            projectionType: "party-list",
            projectionActorType: "PartiesProjectionActor"));

        _ = await invoker.Received(1).InvokeAsync(
            "party-list:test-tenant",
            "PartiesProjectionActor",
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_SearchParties_UsesPayloadChecksumRouteWithoutPayloadData() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party-search"));
        byte[] payload = [1, 2, 3];

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "search-parties",
            domain: "parties",
            aggregateId: "party",
            payload: payload,
            projectionType: "party-search"));

        _ = await invoker.Received(1).InvokeAsync(
            "party-search:test-tenant:A5BYxvLAy0k",
            QueryRouter.ProjectionActorTypeName,
            Arg.Is<QueryEnvelope>(e =>
                e.QueryType == "search-parties" &&
                e.Payload.SequenceEqual(payload)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessWithoutPayload_ReturnsMissingPayloadCategory() {
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(new QueryResult(true));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.MissingPayload);
    }

    [Fact]
    public async Task RouteQueryAsync_InvalidPayloadBytes_ReturnsSerializationFailureCategory() {
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(new QueryResult(true, [0xFF]));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.SerializationFailure);
    }

    [Fact]
    public async Task RouteQueryAsync_NullActorResult_ReturnsActorResponseMismatchCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns((QueryResult)null!);

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorResponseMismatch);
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new ActorMethodInvocationException("actor invocation failed", new InvalidOperationException("inner"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_GenericException_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected failure"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_NullReferenceFromActorInvocation_ReturnsActorExceptionCategory() {
        // R22A1: the original NRE-on-cast bug surfaced as NullReferenceException from
        // Dapr.Actors.Client.ActorProxy.InvokeMethodAsync<TRequest,TResponse>. The router
        // must classify this as ActorException (fail-closed) and not leak the NRE.
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NullReferenceException("Object reference not set to an instance of an object."));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_CancellationRequested_ThrowsOperationCanceledException() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_PreCancelledToken_ThrowsBeforeInvokerCalled() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery(), cts.Token));

        _ = await invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_OperationCanceledException_IsNotConvertedToAdapterFailure() {
        // Story AC #3: when the weak actor invocation observes an OperationCanceledException,
        // the same cancellation category must propagate — it must NOT be flattened into a
        // QueryAdapterFailureReason.ActorException result.
        //
        // ROOT CAUSE (investigated 2026-05-19): The .NET async state machine catches any
        // OperationCanceledException thrown inside an async method and transitions the returned
        // Task to TaskStatus.Canceled. When that canceled Task is later awaited, the runtime
        // surfaces a fresh System.Threading.Tasks.TaskCanceledException with the default
        // "A task was canceled." message — the original OCE instance, its concrete subclass,
        // its custom Message, and its inner exception are all lost. This is .NET behavior, not
        // NSubstitute or the DAPR actor SDK: a hand-rolled test double that throws a sentinel
        // OCE subclass inline produces the same fresh TaskCanceledException after the await.
        // Therefore message-text assertions on the surfaced exception cannot succeed for any
        // OCE that flows through async/await; the meaningful assertion is type identity plus
        // the absence of conversion to a QueryAdapterFailureReason (AC9). The structured-log
        // assertion below (no QueryExecutionFailed log emitted) hardens this further: had the
        // production code converted OCE to ActorException, that log would fire with a
        // QueryAdapterFailureReason and the test would fail.
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException("abandoned request"));

        ILogger<QueryRouter> logger = Substitute.For<ILogger<QueryRouter>>();
        var router = new QueryRouter(invoker, logger);

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));

        // The exception is OCE-derived (catches conversion to an adapter failure type).
        _ = exception.ShouldBeAssignableTo<OperationCanceledException>();

        // It is not converted to a Hexalith adapter failure type. The surfaced OCE is exactly
        // TaskCanceledException (runtime-minted on Task.Canceled awaits) or
        // OperationCanceledException — never a wrapped/derived adapter exception type.
        Type exceptionType = exception.GetType();
        bool isExpectedCancellationType = exceptionType == typeof(OperationCanceledException)
            || exceptionType == typeof(TaskCanceledException);
        isExpectedCancellationType.ShouldBeTrue(
            $"Expected OperationCanceledException or TaskCanceledException but got {exceptionType.FullName}");

        // AC9: cancellation must not be logged as a QueryAdapterFailureReason. If the
        // production code's catch (OperationCanceledException) { throw; } block were removed
        // or reordered after the generic catch, QueryExecutionFailed (EventId 1204) or
        // ActorInvocationFailed (EventId 1202) would fire with an adapter failure reason.
        // Assert that no such log was emitted.
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Is<EventId>(id => id.Id == 1204 || id.Id == 1202),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>()!);
    }

    [Fact]
    public async Task RouteQueryAsync_OperationCanceledExceptionWithNotFoundMessage_IsNotConvertedToNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("did not find address for actor before cancellation completed"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_WrappedOperationCanceledExceptionWithNotFoundMessage_PropagatesCancellation() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException(
                "actor type not registered",
                new OperationCanceledException("did not find address for actor before cancellation completed"),
                false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_PassesCallerCancellationTokenIntoInvoker() {
        // Story AC #3: the same request-scope CancellationToken must reach the weak
        // ActorProxy invocation path. The invoker call captures the token so we can
        // assert identity (not just default(CancellationToken)).
        CancellationToken capturedToken = default;
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<QueryEnvelope>(),
                Arg.Do<CancellationToken>(ct => capturedToken = ct))
            .Returns(QueryResult.FromPayload(JsonDocument.Parse("{}").RootElement));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        using var cts = new CancellationTokenSource();

        _ = await router.RouteQueryAsync(CreateTestQuery(), cts.Token);

        capturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task RouteQueryAsync_ListTenants_UsesTenantsProjectionActorAtSystemIndex() {
        // Story AC #2: list-tenants regression pin. The Admin UI Tenants page request shape
        // (Tenant=system, ProjectionType=tenants, EntityId=index, ProjectionActorType=TenantsProjectionActor)
        // must derive ActorId="tenants:system:index" and route to actor type "TenantsProjectionActor",
        // and preserve every QueryEnvelope field the public contract carries.
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        byte[] requestPayload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?> {
            ["cursor"] = null,
            ["pageSize"] = 100,
        });
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(resultPayload, "tenants"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery(
            tenant: "system",
            queryType: "list-tenants",
            domain: "tenants",
            aggregateId: "index",
            entityId: "index",
            payload: requestPayload,
            projectionType: "tenants",
            projectionActorType: "TenantsProjectionActor"));

        _ = await invoker.Received(1).InvokeAsync(
            "tenants:system:index",
            "TenantsProjectionActor",
            Arg.Is<QueryEnvelope>(e =>
                e.TenantId == "system" &&
                e.Domain == "tenants" &&
                e.AggregateId == "index" &&
                e.QueryType == "list-tenants" &&
                e.CorrelationId == "corr-1" &&
                e.UserId == "user-1" &&
                e.EntityId == "index" &&
                e.Payload.SequenceEqual(requestPayload)),
            Arg.Any<CancellationToken>());
        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("tenants");
        _ = result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task QueryRouter_ConstructedFromIActorProxyFactory_DoesNotCreateTypedDispatchProxyOnRoute() {
        // Regression-sensitive guard for the original NRE bug: the public DI constructor
        // accepts IActorProxyFactory, but the production path must NEVER call
        // CreateActorProxy<IDaprProjectionActor>(...) on it (that returned the typed dispatch
        // proxy whose weak-state was not initialized, producing the runtime NRE).
        // This test would fail against the old implementation, where the first query called
        // CreateActorProxy<IDaprProjectionActor>(...) before trying the weak invocation path.
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        _ = factory.Received(1).Create(
            Arg.Is<ActorId>(id => id.ToString() == "GetOrderStatus:test-tenant"),
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<ActorProxyOptions?>());
        _ = factory.DidNotReceive().CreateActorProxy<IDaprProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>());
        _ = factory.DidNotReceive().CreateActorProxy<IDaprProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
