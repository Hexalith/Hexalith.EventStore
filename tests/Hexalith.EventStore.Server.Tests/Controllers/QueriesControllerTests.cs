
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using ProjectionLifecyclePhase = Hexalith.EventStore.Server.Actors.ProjectionLifecyclePhase;
using ProjectionLifecycleSnapshot = Hexalith.EventStore.Server.Actors.ProjectionLifecycleSnapshot;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public partial class QueriesControllerTests {
    /// <summary>Verifies that an additional tenant header is rejected before query authorization or lookup.</summary>
    /// <param name="headerTenant">The additional header value alongside the body tenant.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("test-tenant")]
    [InlineData("other-tenant")]
    [InlineData("")]
    public async Task Submit_AdditionalTenantHeader_RejectsBeforeAuthorizationOrLookup(string headerTenant)
    {
        IMediator mediator = Substitute.For<IMediator>();
        IETagService etag = Substitute.For<IETagService>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        QueriesController controller = CreateController(mediator, etag, tenantValidator: tenantValidator, rbacValidator: rbacValidator);
        controller.Request.Headers["X-Hexalith-Tenant"] = headerTenant;

        (await controller.Submit(CreateTestRequest(), null, CancellationToken.None)).ShouldBeOfType<BadRequestObjectResult>();

        tenantValidator.ReceivedCalls().ShouldBeEmpty();
        rbacValidator.ReceivedCalls().ShouldBeEmpty();
        etag.ReceivedCalls().ShouldBeEmpty();
        mediator.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies that malformed or duplicate grants are rejected before query authorization or lookup.</summary>
    /// <param name="extraGrant">The malformed or normalized-duplicate grant.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("system")]
    [InlineData(" tenant-a")]
    [InlineData("TEST-TENANT")]
    public async Task Submit_MalformedOrDuplicateGrant_RejectsBeforeAuthorizationOrLookup(string extraGrant)
    {
        IMediator mediator = Substitute.For<IMediator>();
        IETagService etag = Substitute.For<IETagService>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "test-user"), new Claim("eventstore:tenant", "test-tenant"), new Claim("eventstore:tenant", extraGrant),
        ], "test"));
        QueriesController controller = CreateController(mediator, etag, principal, tenantValidator, rbacValidator);

        (await controller.Submit(CreateTestRequest(), null, CancellationToken.None)).ShouldBeOfType<ForbidResult>();

        tenantValidator.ReceivedCalls().ShouldBeEmpty();
        rbacValidator.ReceivedCalls().ShouldBeEmpty();
        etag.ReceivedCalls().ShouldBeEmpty();
        mediator.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies that query authorization, dispatch and rate-limit context use the canonical tenant.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Submit_MixedCaseScope_CanonicalizesBeforeAuthorizationAndRateLimitContext()
    {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(CreateProjectionResult());
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), "test-tenant", Arg.Any<CancellationToken>(), "order-1")
            .Returns(TenantValidationResult.AllowedFor("test-tenant"));
        QueriesController controller = CreateController(mediator, tenantValidator: tenantValidator);

        (await controller.Submit(CreateTestRequest() with { Tenant = "Test-Tenant" }, null, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>();

        controller.HttpContext.Items["RequestTenantId"].ShouldBe("test-tenant");
        _ = await tenantValidator.Received(1).ValidateAsync(Arg.Any<ClaimsPrincipal>(), "test-tenant", Arg.Any<CancellationToken>(), "order-1");
        _ = await mediator.Received(1).Send(Arg.Is<SubmitQuery>(query => query.Tenant == "test-tenant"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Generates a self-routing ETag for the "orders" projection type (matches CreateTestRequest).
    /// </summary>
    private static string GenerateTestETag(string projectionType = "orders") =>
        SelfRoutingETag.GenerateNew(projectionType);

    private static SubmitQueryRequest CreateTestRequest(JsonElement? payload = null) =>
        new(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: payload);

    private static SubmitQueryResult CreateProjectionResult(
        JsonElement? payload = null,
        string projectionType = "orders",
        QueryResponseMetadata? metadata = null)
        => new(
            "corr-1",
            payload ?? JsonSerializer.SerializeToElement(new { }),
            projectionType,
            (metadata ?? new QueryResponseMetadata()) with
            {
                Provenance = QueryResponseProvenance.ProjectionBacked,
            });

    private static QueriesController CreateController(
        IMediator mediator,
        IETagService? eTagService = null,
        ClaimsPrincipal? principal = null,
        ITenantValidator? tenantValidator = null,
        IRbacValidator? rbacValidator = null,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null,
        Hexalith.EventStore.Server.Identity.IIdentityGatewayAdmission? identityAdmission = null) {
        eTagService ??= Substitute.For<IETagService>();
        if (tenantValidator is null) {
            tenantValidator = Substitute.For<ITenantValidator>();
            _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
                .Returns(TenantValidationResult.Allowed);
        }

        if (rbacValidator is null) {
            rbacValidator = Substitute.For<IRbacValidator>();
            _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
                .Returns(RbacValidationResult.Allowed);
        }
        var controller = catalogAuthority is null
            ? new QueriesController(mediator, eTagService, tenantValidator, rbacValidator, NullLogger<QueriesController>.Instance, identityAdmission: identityAdmission, configuration: configuration)
            : new QueriesController(catalogAuthority, mediator, eTagService, tenantValidator, rbacValidator, NullLogger<QueriesController>.Instance, identityAdmission: identityAdmission, configuration: configuration);
        var httpContext = new DefaultHttpContext {
            User = principal ?? new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "test-user")], "test")),
        };
        httpContext.Items["CorrelationId"] = "corr-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Submit_ValidRequest_Returns200WithPayload() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest();

        // Act
        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        // Assert
        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.StatusCode.ShouldBe(200);
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        response.CorrelationId.ShouldBe("corr-1");
        response.Payload.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task Submit_CorrelationIdExtractedFromHttpContext() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — verify mediator received query with correlationId from HttpContext
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.CorrelationId == "corr-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_StoresRequestTenantIdInHttpContext() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        controller.HttpContext.Items["RequestTenantId"].ShouldBe("test-tenant");
    }

    [Fact]
    public async Task Submit_UserIdExtractedFromJwtSubClaim() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "jwt-user-id")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.UserId == "jwt-user-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_GlobalAdministratorPrincipal_SetsSubmitQueryIsGlobalAdminTrue() {
        // Arrange — the IsGlobalAdmin flag is minted here from the JWT, never from client request input.
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "admin-user"), new Claim(ClaimTypes.Role, "GlobalAdministrator")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.IsGlobalAdmin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NonAdministratorPrincipal_SetsSubmitQueryIsGlobalAdminFalse() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "plain-user")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => !q.IsGlobalAdmin),
            Arg.Any<CancellationToken>());
    }

    // --- Dual-principal identity claim extraction (Story 6.1-P2) ---

    [Fact]
    public async Task Submit_LegacyPrincipal_OnlySubClaim_PopulatesOriginalActorIdOnlyFromSub() {
        // Matrix row: "Legacy caller, no dual-principal fields populated" -> behaves exactly as
        // today. OriginalActorId always mirrors the sub claim (same source as the legacy UserId
        // field), but the remaining dual-principal fields stay unpopulated absent their claims —
        // no new validation failure occurs for a claims-minimal token.
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "plain-user")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q =>
                q.OriginalActorId == "plain-user" &&
                q.UserId == q.OriginalActorId &&
                q.AuthenticatedWorkloadId == null &&
                !q.IsDelegated &&
                q.Scopes == null &&
                q.Audience == null &&
                q.DelegationId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ValidActSubject_SetsDelegationIdSeparatelyFromActorAndWorkload() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "end-user-1"),
                new Claim("azp", "gateway-client"),
                new Claim("act", "{\"sub\":\"delegate-service\"}"),
            ],
            "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q =>
                q.OriginalActorId == "end-user-1" &&
                q.AuthenticatedWorkloadId == "gateway-client" &&
                q.DelegationId == "delegate-service"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_DualPrincipalToken_ExtractsOriginalActorIdFromSubClaim() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "end-user-1")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.OriginalActorId == "end-user-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_AzpClaimPresent_SetsAuthenticatedWorkloadId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "end-user-1"), new Claim("azp", "gateway-client")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.AuthenticatedWorkloadId == "gateway-client"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_AzpDiffersFromClientId_SetsIsDelegatedTrue() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "end-user-1"),
                new Claim("azp", "gateway-client"),
                new Claim("client_id", "backend-client"),
            ],
            "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.IsDelegated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ScopeClaim_SetsSubmitQueryScopes() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "end-user-1"), new Claim("scope", "orders.read orders.write")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Scopes != null && q.Scopes.SequenceEqual(new[] { "orders.read", "orders.write" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_AudClaims_SetSubmitQueryAudience() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "end-user-1"), new Claim("aud", "eventstore-api")], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Audience != null && q.Audience.SequenceEqual(new[] { "eventstore-api" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_MissingSubClaim_ReturnsUnauthorizedAndDoesNotCallMediator() {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<UnauthorizedResult>();
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NullPayload_PassesEmptyByteArray() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest(payload: null);

        // Act
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithEntityId_ForwardsEntityIdToSubmitQuery() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            EntityId: "entity-42");

        // Act
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert — EntityId from request forwarded to SubmitQuery
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == "entity-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithNullEntityId_FallsBackToAggregateIdForRouting() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — aggregate-scoped queries fall back to AggregateId when EntityId is omitted
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == "order-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithPayload_SerializesToUtf8Bytes() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));

        QueriesController controller = CreateController(mediator);
        JsonElement requestPayload = JsonDocument.Parse("{\"filter\":\"active\"}").RootElement;
        SubmitQueryRequest request = CreateTestRequest(payload: requestPayload);

        // Act
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert — payload bytes should be non-empty
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length > 0),
            Arg.Any<CancellationToken>());
    }

    // ===== Conditional query tests =====

    [Theory]
    [InlineData("persisted-opaque-validator")]
    [InlineData("persisted,opaque,validator")]
    public async Task Submit_OpaqueProducerETagMatches_RoutesBefore304AndPreservesIndependentMetadata(string producerETag) {
        DateTimeOffset servedAt = new(2026, 10, 25, 8, 30, 0, TimeSpan.FromHours(2));
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(
                ETag: producerETag,
                IsStale: true,
                IsDegraded: true,
                ProjectionVersion: "model-version-42",
                ServedAt: servedAt,
                WarningCodes: [QueryWarningCodes.DegradedSearch, "source.warning-2"]) {
                Lifecycle = ProjectionLifecycleState.Current,
            }));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);

        IActionResult result = await controller.Submit(CreateTestRequest(), $"\"{producerETag}\"", CancellationToken.None);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{producerETag}\"");
        controller.Response.Headers["X-Hexalith-Projection-Version"].ToString().ShouldBe("model-version-42");
        controller.Response.Headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        controller.Response.Headers["X-Hexalith-Is-Stale"].ToString().ShouldBe("false");
        controller.Response.Headers["X-Hexalith-Is-Degraded"].ToString().ShouldBe("true");
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Current");
        controller.Response.Headers["X-Hexalith-Warning-Codes"].ToString().ShouldBe("degraded_search,source.warning-2");
    }

    [Theory]
    [InlineData("W/", "v1")]
    [InlineData("w/", "v1")]
    [InlineData("W/", "v1.opaque")]
    [InlineData("w/", "v1.opaque")]
    public async Task Submit_WeakPrefixInsideQuotedOpaqueValidator_AllowsOnlyStrongRevalidation(string prefix, string suffix) {
        string producerETag = prefix + suffix;
        string quotedETag = $"\"{producerETag}\"";
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: producerETag)));
        SubmitQueryRequest request = CreateTestRequest();
        QueriesController controller = CreateController(mediator);

        SubmitQueryResponse initial = (await controller.Submit(request, null, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        controller.Response.Headers.ETag.ToString().ShouldBe(quotedETag);
        initial.Metadata.ShouldNotBeNull().ETag.ShouldBe(producerETag);

        (await CreateController(mediator).Submit(request, quotedETag, CancellationToken.None))
            .ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        (await CreateController(mediator).Submit(request, prefix + quotedETag, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>();
        (await CreateController(mediator).Submit(request, producerETag, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("weak")]
    [InlineData("wildcard")]
    [InlineData("malformed")]
    [InlineData("malformed-list-member")]
    [InlineData("trailing-control")]
    [InlineData("unquoted")]
    [InlineData("too-many")]
    [InlineData("oversized-list-member")]
    [InlineData("payload")]
    [InlineData("policy")]
    public async Task Submit_OpaqueProducerETagWithUnsupportedConditionalShape_Returns200(string kind) {
        const string ProducerETag = "opaque-producer";
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: ProducerETag)));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = kind switch {
            "payload" => CreateTestRequest(JsonSerializer.SerializeToElement(new { filter = "active" })),
            "policy" => CreateTestRequest() with { Paging = new QueryPagingOptions(PageSize: 25) },
            _ => CreateTestRequest(),
        };
        string header = kind switch {
            "miss" => "\"other-producer\"",
            "weak" => $"W/\"{ProducerETag}\"",
            "wildcard" => "*",
            "malformed" => $"\"{ProducerETag}",
            "malformed-list-member" => $"\"{ProducerETag}\", \"invalid tag\"",
            "trailing-control" => $"\"{ProducerETag}\"\r",
            "unquoted" => ProducerETag,
            "too-many" => string.Join(",", Enumerable.Repeat($"\"{ProducerETag}\"", 11)),
            "oversized-list-member" => $"\"{ProducerETag}\", \"{new string('x', 4097)}\"",
            _ => $"\"{ProducerETag}\"",
        };

        (await controller.Submit(request, header, CancellationToken.None)).ShouldBeOfType<OkObjectResult>();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_TenOpaqueProducerValidatorsWithMatch_Returns304() {
        const string ProducerETag = "opaque-producer";
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: ProducerETag)));
        QueriesController controller = CreateController(mediator);
        string header = string.Join(",", Enumerable.Repeat("\"other\"", 9).Append($"\"{ProducerETag}\""));

        (await controller.Submit(CreateTestRequest(), header, CancellationToken.None))
            .ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("\"unterminated")]
    [InlineData("*")]
    [InlineData("W/\"weak\"")]
    [InlineData("")]
    [InlineData("\"other\" trailing")]
    public async Task Submit_MatchingValidatorBeforeMalformedTail_Returns200(string tail) {
        string validator = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: validator)));
        QueriesController controller = CreateController(mediator);

        (await controller.Submit(CreateTestRequest(), $"\"{validator}\",{tail}", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Submit_OverLimitMixedListWithMatchingValidator_Returns200() {
        string validator = GenerateTestETag("orders");
        string otherProjection = GenerateTestETag("counter");
        string header = string.Join(",", new[] { $"\"{validator}\"", $"\"{otherProjection}\"" }
            .Concat(Enumerable.Repeat("\"opaque\"", 9)));
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: validator)));
        QueriesController controller = CreateController(mediator);

        (await controller.Submit(CreateTestRequest(), header, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("token")]
    [InlineData("count")]
    [InlineData("size")]
    public async Task Submit_WarningsWithoutSafeHeaderRepresentation_Retains200Evidence(string kind) {
        string[] warnings = kind switch {
            "count" => Enumerable.Repeat("warning", 17).ToArray(),
            "size" => [new string('x', 4097)],
            _ => ["invalid,warning"],
        };
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: "opaque", WarningCodes: warnings)));
        QueriesController controller = CreateController(mediator);

        SubmitQueryResponse response = (await controller.Submit(CreateTestRequest(), "\"opaque\"", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();

        response.Metadata.ShouldNotBeNull().WarningCodes.ShouldBe(warnings);
        controller.Response.Headers["X-Hexalith-Warning-Codes"].ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData((QueryResponseProvenance)99)]
    public async Task Submit_OpaqueValidatorWithoutProjectionProvenance_CannotCreateAuthority(QueryResponseProvenance provenance) {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", JsonSerializer.SerializeToElement(new { }), "orders",
                new QueryResponseMetadata(ETag: "opaque-producer", ProjectionVersion: "v42", IsStale: false) {
                    Provenance = provenance,
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        QueriesController controller = CreateController(mediator);

        IActionResult result = await controller.Submit(CreateTestRequest(), "\"opaque-producer\"", CancellationToken.None);

        SubmitQueryResponse response = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBeNull();
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        controller.Response.Headers["X-Hexalith-Projection-Version"].ToString().ShouldBeEmpty();
        controller.Response.Headers["X-Hexalith-Is-Stale"].ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Submit_ProducerETagWithoutProjectionTarget_IsOmitted(string? projectionType) {
        string producerETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: producerETag, ProjectionVersion: "v42"))
                with { ProjectionType = projectionType });
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);

        IActionResult result = await controller.Submit(
            CreateTestRequest() with { ProjectionType = "orders" }, $"\"{producerETag}\"", CancellationToken.None);

        SubmitQueryResponse response = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Metadata.ShouldNotBeNull().ETag.ShouldBeNull();
        response.Metadata.ProjectionVersion.ShouldBe("v42");
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("oversized")]
    [InlineData("control")]
    [InlineData("unicode")]
    [InlineData("leading-space")]
    [InlineData("trailing-space")]
    public async Task Submit_NonRoundTrippableProducerVersion_Retains200PayloadAndVersion(string kind) {
        string version = kind switch {
            "oversized" => new string('x', 4097),
            "control" => "version\r\ninjected",
            "unicode" => "modèle-v42",
            "leading-space" => " version-42",
            "trailing-space" => "version-42 ",
            _ => " ",
        };
        DateTimeOffset servedAt = DateTimeOffset.Parse("2026-10-25T08:30:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(JsonSerializer.SerializeToElement(new { value = 42 }), metadata: new QueryResponseMetadata(
                ETag: "opaque-producer", ProjectionVersion: version, ServedAt: servedAt)));
        QueriesController controller = CreateController(mediator);

        SubmitQueryResponse response = (await controller.Submit(CreateTestRequest(), "\"opaque-producer\"", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Payload.GetProperty("value").GetInt32().ShouldBe(42);
        response.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBe(version);
        response.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        response.Metadata.IsNotModified.ShouldBe(false);
        controller.Response.Headers["X-Hexalith-Projection-Version"].ToString().ShouldBeEmpty();
        controller.Response.Headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("empty")]
    [InlineData("max-length")]
    [InlineData("comma")]
    [InlineData("internal-space")]
    public async Task Submit_AbsentOrLosslessProducerVersion_Preserves304Behavior(string kind) {
        string? version = kind switch {
            "absent" => null,
            "empty" => string.Empty,
            "max-length" => new string('x', 4096),
            "comma" => "model,version-42",
            _ => "model version-42",
        };
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: "opaque-producer", ProjectionVersion: version)));
        QueriesController controller = CreateController(mediator);

        (await controller.Submit(CreateTestRequest(), "\"opaque-producer\"", CancellationToken.None))
            .ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Response.Headers["X-Hexalith-Projection-Version"].ToString().ShouldBe(version ?? string.Empty);
    }

    [Fact]
    public async Task Submit_IfNoneMatchMatches_Returns304() {
        // Arrange — use self-routing ETag so decode succeeds
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(ETag: testETag) {
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{testETag}\"", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{testETag}\"");
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Current");
        // The bodyless 304 must still carry projection provenance: EventStoreGatewayClient rejects
        // a not-modified response lacking X-Hexalith-Query-Provenance: ProjectionBacked as a 502.
        controller.Response.Headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("ProjectionBacked");
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4096)]
    public async Task Submit_ProducerBoundValidator_Returns200Then304ThenChanged200(int validatorLength) {
        string revisionOneTag = "v1".PadRight(validatorLength, 'x');
        const string RevisionTwoTag = "v2";
        var metadata = new QueryResponseMetadata(ETag: revisionOneTag) { Lifecycle = ProjectionLifecycleState.Current };
        SubmitQueryResult revisionOne = CreateProjectionResult(JsonSerializer.SerializeToElement(new { revision = 1 }), metadata: metadata);
        SubmitQueryResult revisionTwo = CreateProjectionResult(JsonSerializer.SerializeToElement(new { revision = 2 }),
            metadata: metadata with { ETag = RevisionTwoTag });
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(revisionOne, revisionOne, revisionTwo);
        IETagService eTagService = Substitute.For<IETagService>();
        SubmitQueryRequest request = CreateTestRequest();
        QueriesController controller = CreateController(mediator, eTagService);

        SubmitQueryResponse initial = (await controller.Submit(request, null, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        initial.Payload.GetProperty("revision").GetInt32().ShouldBe(1);
        initial.Metadata.ShouldNotBeNull().ETag.ShouldBe(revisionOneTag);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{revisionOneTag}\"");

        controller = CreateController(mediator, eTagService);
        (await controller.Submit(request, $"\"{revisionOneTag}\"", CancellationToken.None))
            .ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{revisionOneTag}\"");
        controller.Response.Headers[QueryResponseProvenancePolicy.HeaderName].ToString().ShouldBe("ProjectionBacked");
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Current");

        controller = CreateController(mediator, eTagService);
        SubmitQueryResponse changed = (await controller.Submit(request, $"\"{revisionOneTag}\"", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        changed.Payload.GetProperty("revision").GetInt32().ShouldBe(2);
        changed.Metadata.ShouldNotBeNull().ETag.ShouldBe(RevisionTwoTag);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{RevisionTwoTag}\"");
        _ = await mediator.Received(3).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        eTagService.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_UntaggedReadWhileProjectionAdvances_DoesNotAttachLaterValidator() {
        string revisionTwoTag = GenerateTestETag();
        string? serviceTag = null;
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(_ => serviceTag);
        var metadata = new QueryResponseMetadata(
            ProjectionVersion: "revision-1",
            IsStale: true,
            IsDegraded: true,
            ServedAt: new DateTimeOffset(2026, 11, 28, 8, 0, 0, TimeSpan.Zero),
            Paging: new QueryPagingMetadata(PageSize: 10, TotalCount: 3),
            WarningCodes: [QueryWarningCodes.DegradedSearch]) {
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = ProjectionLifecycleState.Stale,
        };
        SubmitQueryResult revisionOne = CreateProjectionResult(JsonSerializer.SerializeToElement(new { revision = 1 }), metadata: metadata);
        SubmitQueryResult revisionTwo = CreateProjectionResult(JsonSerializer.SerializeToElement(new { revision = 2 }),
            metadata: metadata with { ProjectionVersion = "revision-2" });
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                SubmitQueryResult read = serviceTag is null ? revisionOne : revisionTwo;
                // Advance the projection after selecting the snapshot and before returning it to the controller.
                serviceTag = revisionTwoTag;
                return Task.FromResult(read);
            });
        SubmitQueryRequest request = CreateTestRequest();
        QueriesController controller = CreateController(mediator, eTagService);

        SubmitQueryResponse initial = (await controller.Submit(request, null, CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        initial.Payload.GetProperty("revision").GetInt32().ShouldBe(1);
        initial.Metadata.ShouldNotBeNull().WarningCodes.ShouldBe(metadata.WarningCodes);
        (initial.Metadata with { WarningCodes = null }).ShouldBe(metadata with { IsNotModified = false, WarningCodes = null });
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        serviceTag.ShouldBe(revisionTwoTag);

        controller = CreateController(mediator, eTagService);
        SubmitQueryResponse retry = (await controller.Submit(request, $"\"{revisionTwoTag}\"", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        retry.Payload.GetProperty("revision").GetInt32().ShouldBe(2);
        retry.Metadata.ShouldNotBeNull().WarningCodes.ShouldBe(metadata.WarningCodes);
        (retry.Metadata with { WarningCodes = null }).ShouldBe(metadata with {
            IsNotModified = false, ProjectionVersion = "revision-2", WarningCodes = null,
        });
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        _ = await mediator.Received(2).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        eTagService.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("W/\"v1\"")]
    [InlineData("*")]
    [InlineData("\"quoted\"")]
    [InlineData("bad\"q")]
    [InlineData("a\r\nb")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("inner space")]
    [InlineData("modèle")]
    [InlineData(null)]
    public async Task Submit_InvalidProducerValidator_OmitsValidatorAndPreservesIndependentMetadata(string? invalidETag) {
        string invalid = invalidETag ?? new string('x', 4097);
        JsonElement payload = JsonSerializer.SerializeToElement(new { data = "revision one" });
        var metadata = new QueryResponseMetadata(
            ETag: invalid,
            IsStale: true,
            IsDegraded: true,
            ProjectionVersion: "revision-1",
            ServedAt: new DateTimeOffset(2026, 11, 28, 8, 0, 0, TimeSpan.Zero),
            Paging: new QueryPagingMetadata(PageSize: 10, TotalCount: 3),
            WarningCodes: ["degraded_search"]) {
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = ProjectionLifecycleState.Stale,
        };
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(payload, metadata: metadata));
        IETagService eTagService = Substitute.For<IETagService>();
        string serviceTag = GenerateTestETag();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serviceTag);
        QueriesController controller = CreateController(mediator, eTagService);

        SubmitQueryResponse response = (await controller.Submit(CreateTestRequest(), $"\"{serviceTag}\"", CancellationToken.None))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();

        response.Payload.GetProperty("data").GetString().ShouldBe("revision one");
        response.Metadata.ShouldNotBeNull().WarningCodes.ShouldBe(metadata.WarningCodes);
        (response.Metadata with { WarningCodes = null }).ShouldBe(metadata with {
            ETag = null, IsNotModified = false, WarningCodes = null,
        });
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        eTagService.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public async Task Submit_IfNoneMatchMatches_OperationalLifecycleEmitsCanonicalHeader(
        ProjectionLifecycleState lifecycle) {
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(ETag: testETag, IsStale: false, IsDegraded: false) {
                    Lifecycle = lifecycle,
                }));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Current";

        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(),
            $"\"{testETag}\"",
            CancellationToken.None);

        actionResult.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe(lifecycle.ToString());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_IfNoneMatchMatchesWithEntityIdentityPayload_Returns304() {
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: testETag)));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest request = CreateTestRequest(JsonDocument.Parse("{\"id\":\"order-1\"}").RootElement);

        IActionResult actionResult = await controller.Submit(request, $"\"{testETag}\"", CancellationToken.None);

        actionResult.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(304);
        controller.Response.Headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("ProjectionBacked");
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_MatchingProjectionValidatorForHandlerRoute_ExecutesAndSanitizesProjectionEvidence() {
        string matchingETag = GenerateTestETag();
        JsonElement resultPayload = JsonSerializer.SerializeToElement(new { data = 1 });
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult(
                "corr-1",
                resultPayload,
                ProjectionType: null,
                Metadata: new QueryResponseMetadata(
                    ETag: matchingETag,
                    IsNotModified: true,
                    IsStale: false,
                    IsDegraded: true,
                    ProjectionVersion: "projection-v9",
                    WarningCodes: [QueryWarningCodes.DegradedSearch])
                {
                    Provenance = QueryResponseProvenance.HandlerComputed,
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(matchingETag);
        QueriesController controller = CreateController(mediator, eTagService);

        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(),
            $"\"{matchingETag}\"",
            CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        controller.Response.Headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("HandlerComputed");
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBeEmpty();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
        response.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        response.Metadata.ETag.ShouldBeNull();
        response.Metadata.IsNotModified.ShouldBeNull();
        response.Metadata.IsStale.ShouldBeNull();
        response.Metadata.ProjectionVersion.ShouldBeNull();
        response.Metadata.IsDegraded.ShouldBe(true);
        response.Metadata.WarningCodes.ShouldBe([QueryWarningCodes.DegradedSearch]);
    }

    [Fact]
    public async Task Submit_ErasingProjectionReturnsPayloadWithoutValidator()
    {
        string matchingETag = GenerateTestETag();
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { data = 1 }), "orders",
                new QueryResponseMetadata(ETag: matchingETag, ProjectionVersion: "v9", IsStale: false)
                {
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(Arg.Any<AggregateIdentity>(), "orders", Arg.Any<CancellationToken>())
            .Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Erasing, 2));
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, lifecycle);
        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => handler.Handle(call.Arg<SubmitQuery>(), call.Arg<CancellationToken>()));
        IETagService eTagService = Substitute.For<IETagService>();
        eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>()).Returns(matchingETag);
        QueriesController controller = CreateController(mediator, eTagService);

        IActionResult actionResult = await controller.Submit(
            CreateTestRequest() with { ProjectionType = "orders" }, $"\"{matchingETag}\"", TestContext.Current.CancellationToken);

        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Payload.GetProperty("data").GetInt32().ShouldBe(1);
        response.Metadata!.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
        response.Metadata.ProjectionVersion.ShouldBeNull();
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_UnrecognizedProvenance_NormalizesToUnknownAndDoesNotFetchETag() {
        JsonElement resultPayload = JsonSerializer.SerializeToElement(new { data = 1 });
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult(
                "corr-1",
                resultPayload,
                ProjectionType: "orders",
                Metadata: new QueryResponseMetadata(IsStale: false, ProjectionVersion: "v9")
                {
                    Provenance = (QueryResponseProvenance)99,
                }));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Stale";

        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        controller.Response.Headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("Unknown");
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
        response.Metadata.IsNotModified.ShouldBeNull();
        response.Metadata.ProjectionVersion.ShouldBeNull();
        response.Metadata.IsStale.ShouldBeNull();
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_ProjectionBackedOperationalLifecycle_EmitsCanonicalHeader() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(IsStale: false, IsDegraded: false) {
                    Lifecycle = ProjectionLifecycleState.Degraded,
                }));
        QueriesController controller = CreateController(mediator);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Current";

        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
        response.Metadata.IsStale.ShouldBeNull();
        response.Metadata.IsDegraded.ShouldBe(true);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Degraded");
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData((ProjectionLifecycleState)99)]
    public async Task Submit_UnknownOrInvalidLifecycle_NormalizesToUnknownAndOmitsHeader(
        ProjectionLifecycleState lifecycle) {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(IsStale: false) {
                    Lifecycle = lifecycle,
                }));
        QueriesController controller = CreateController(mediator);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Current";

        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        response.Metadata.IsStale.ShouldBe(false);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_IfNoneMatchMatchesButNonIdentityPayloadPresent_SkipsPreCheckAndReturns200() {
        string testETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: testETag)));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest request = CreateTestRequest(JsonDocument.Parse("{\"filter\":\"active\"}").RootElement);

        IActionResult actionResult = await controller.Submit(request, $"\"{testETag}\"", CancellationToken.None);

        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("paging")]
    [InlineData("search")]
    [InlineData("filters")]
    [InlineData("order-by")]
    [InlineData("freshness")]
    public async Task Submit_IfNoneMatchMatchesButPolicyInputsPresent_SkipsPreCheckAndReturns200(string policy) {
        string testETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: testETag) { Lifecycle = ProjectionLifecycleState.Current }));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest baseRequest = CreateTestRequest();
        SubmitQueryRequest request = policy switch {
            "paging" => baseRequest with { Paging = new QueryPagingOptions(PageSize: 10, Offset: 20) },
            "search" => baseRequest with { Search = "active" },
            "filters" => baseRequest with { Filters = [new QueryFilter("status", "eq", JsonSerializer.SerializeToElement("active"))] },
            "order-by" => baseRequest with { OrderBy = [new QuerySort("status")] },
            "freshness" => baseRequest with { Freshness = new QueryFreshnessPolicy(RequireFresh: true) },
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

        IActionResult actionResult = await controller.Submit(request, $"\"{testETag}\"", CancellationToken.None);

        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Metadata.ShouldNotBeNull().ETag.ShouldBe(testETag);
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_UnauthorizedTenant_DoesNotReadETagOrInvokeMediator() {
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), "test-tenant", Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Denied("Tenant disabled.", AuthorizationFailureReason.TenantDisabled));
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        QueriesController controller = CreateController(
            mediator,
            eTagService,
            tenantValidator: tenantValidator,
            rbacValidator: rbacValidator);

        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => controller.Submit(CreateTestRequest(), $"\"{GenerateTestETag()}\"", CancellationToken.None));

        ex.ReasonCode.ShouldBe(AuthorizationFailureReason.TenantDisabled);
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await rbacValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task Submit_UnauthorizedRbac_ThrowsWithCorrectReasonCode() {
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Denied("Insufficient permission.", AuthorizationFailureReason.InsufficientPermission));

        QueriesController controller = CreateController(
            mediator, eTagService,
            tenantValidator: tenantValidator,
            rbacValidator: rbacValidator);

        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => controller.Submit(CreateTestRequest(), null, CancellationToken.None));

        ex.ReasonCode.ShouldBe(AuthorizationFailureReason.InsufficientPermission);
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_IfNoneMatchDoesNotMatch_Returns200WithETagHeader() {
        // Arrange — client sends old self-routing ETag, server has newer one
        string clientETag = GenerateTestETag();
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: serverETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{clientETag}\"", CancellationToken.None);

        // Assert
        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.StatusCode.ShouldBe(200);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{serverETag}\"");
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submit_MultipleETagsOneMatches_Returns304(bool quoteTags) {
        // Arrange — self-routing ETags in comma-separated list
        string matchingETag = GenerateTestETag();
        string otherETag1 = GenerateTestETag();
        string otherETag2 = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: matchingETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);
        string header = string.Join(", ", new[] { otherETag1, matchingETag, otherETag2 }
            .Select(tag => quoteTags ? $"\"{tag}\"" : tag));

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), header, CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submit_MultiValueHeaderWithMixedProjectionTypes_Returns200(bool hasProducerETag) {
        // Arrange — mixed projections in a multi-value header must fail open to avoid false 304s
        string ordersETag = GenerateTestETag("orders");
        string countersETag = GenerateTestETag("counter");
        string? responseETag = hasProducerETag ? ordersETag : null;
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: responseETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(),
            $"\"{ordersETag}\", \"{countersETag}\"",
            CancellationToken.None);

        // Assert
        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>();
        response.Metadata.ShouldNotBeNull().ETag.ShouldBe(responseETag);
        controller.Response.Headers.ETag.ToString().ShouldBe(hasProducerETag ? $"\"{ordersETag}\"" : string.Empty);
        eTagService.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_MultipleETagsNoneMatch_Returns200() {
        // Arrange
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: serverETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);
        string etagA = GenerateTestETag();
        string etagB = GenerateTestETag();

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"\"{etagA}\", \"{etagB}\"", CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    // ===== Backward compatibility and self-routing decode tests =====

    [Fact]
    public async Task Submit_OldFormatETag_CacheMissAndReturnsNewFormatETag() {
        // Arrange — old-format ETag (plain GUID, no dot) → decode fails → cache miss
        string oldFormatETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: serverETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{oldFormatETag}\"", CancellationToken.None);

        // Assert — old format → cache miss → 200, response has new self-routing ETag
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        string responseETag = controller.Response.Headers.ETag.ToString();
        responseETag.ShouldContain(".");  // New self-routing format
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_NonAsciiProjectionType_UsesAuthoritativeSelectedRouteProjectionType() {
        string projectionType = "données";
        string nonAsciiETag = GenerateTestETag(projectionType);
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(projectionType: projectionType, metadata: new QueryResponseMetadata(ETag: nonAsciiETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"\"{nonAsciiETag}\"", CancellationToken.None);

        // Assert — the selected route is authoritative and conditional evaluation occurs afterward.
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WeakETagPrefix_CacheMissReturns200() {
        // Arrange — W/ prefix causes decode failure → cache miss → full query
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, metadata: new QueryResponseMetadata(ETag: serverETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act — W/ prefix on If-None-Match
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"W/\"{serverETag}\"", CancellationToken.None);

        // Assert — W/ causes decode failure → cache miss → 200 with new ETag
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{serverETag}\"");
        // Verify no W/ prefix in response ETag
        controller.Response.Headers.ETag.ToString().ShouldNotStartWith("W/");
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_ETagPreCheckPerformance_P99UnderFiveMilliseconds() {
        // Exercise the conditional endpoint with a routed representation and a matching validator.
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: testETag)));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest request = CreateTestRequest();
        string header = $"\"{testETag}\"";

        // Warm-up
        for (int i = 0; i < 25; i++) {
            _ = await controller.Submit(request, header, CancellationToken.None);
        }

        List<double> durationsMs = [];

        // Act
        for (int i = 0; i < 200; i++) {
            long started = Stopwatch.GetTimestamp();
            IActionResult result = await controller.Submit(request, header, CancellationToken.None);
            result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(304);
            durationsMs.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        // Assert
        double p99 = durationsMs.OrderBy(x => x).ElementAt((int)Math.Ceiling(durationsMs.Count * 0.99) - 1);
        p99.ShouldBeLessThan(5d);
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    // ===== Story 18-8: ProjectionType passthrough to ETag response =====

    [Fact]
    public async Task Submit_ProjectionTypeDiffersFromDomain_ReturnsProducerETag() {
        // Arrange — SubmitQueryResult has ProjectionType="order-list"
        string orderListETag = GenerateTestETag("order-list");
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload, "order-list", new QueryResponseMetadata(ETag: orderListETag)));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{orderListETag}\"");
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submit_RequestPagingWithoutProducerMetadata_DoesNotCreatePagingEvidence() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Paging = new QueryPagingOptions(),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Paging.ShouldBeNull();
        response.Metadata.IsStale.ShouldBeNull();
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Paging == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_CursorOnlyPaging_ForwardsPagingToQueryPipeline() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Paging = new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor"),
        };

        _ = await controller.Submit(request, null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q =>
                q.Paging != null &&
                q.Paging.PageSize == 25 &&
                q.Paging.Cursor == "opaque-cursor"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WhitespaceCursorWithOffset_ForwardsOffsetWithoutCursor() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Paging = new QueryPagingOptions(PageSize: 25, Offset: 50, Cursor: "   "),
        };

        _ = await controller.Submit(request, null, CancellationToken.None);

        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q =>
                q.Paging != null &&
                q.Paging.PageSize == 25 &&
                q.Paging.Offset == 50 &&
                q.Paging.Cursor == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ProjectionBackedProducerMetadata_PreservesReadConsistentETagAndMergesOtherFields() {
        string producerETag = GenerateTestETag("producer");
        string gatewayETag = GenerateTestETag("orders");
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        var servedAt = new DateTimeOffset(2026, 7, 6, 8, 15, 0, TimeSpan.Zero);
        var producerMetadata = new QueryResponseMetadata(
            ETag: producerETag,
            IsNotModified: true,
            IsStale: true,
            IsDegraded: true,
            ProjectionVersion: "orders-v9",
            ServedAt: servedAt,
            Paging: new QueryPagingMetadata(PageSize: 10, Offset: 20, NextCursor: "next", TotalCount: 99, HasMore: true),
            WarningCodes: [QueryWarningCodes.DegradedSearch])
        {
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload, "orders", producerMetadata));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(gatewayETag);
        QueriesController controller = CreateController(mediator, eTagService);

        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.ETag.ShouldBe(producerETag);
        response.Metadata.IsNotModified.ShouldBe(false);
        response.Metadata.IsStale.ShouldBe(true);
        response.Metadata.IsDegraded.ShouldBe(true);
        response.Metadata.ProjectionVersion.ShouldBe("orders-v9");
        response.Metadata.ServedAt.ShouldBe(servedAt);
        _ = response.Metadata.Paging.ShouldNotBeNull();
        response.Metadata.Paging.PageSize.ShouldBe(10);
        response.Metadata.Paging.Offset.ShouldBe(20);
        response.Metadata.Paging.NextCursor.ShouldBe("next");
        response.Metadata.Paging.TotalCount.ShouldBe(99);
        response.Metadata.Paging.HasMore.ShouldBe(true);
        _ = response.Metadata.WarningCodes.ShouldNotBeNull();
        response.Metadata.WarningCodes.ShouldContain(QueryWarningCodes.DegradedSearch);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{producerETag}\"");
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ProducerPagingMetadata_IsPreservedWithoutInventingRequestPagingFields() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        var producerMetadata = new QueryResponseMetadata(
            Paging: new QueryPagingMetadata(PageSize: 25, Offset: 50, NextCursor: "producer-next", TotalCount: 250, HasMore: false));
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload, Metadata: producerMetadata));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Paging = new QueryPagingOptions(PageSize: 10, Offset: 20),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        _ = response.Metadata.Paging.ShouldNotBeNull();
        response.Metadata.Paging.PageSize.ShouldBe(25);
        response.Metadata.Paging.Offset.ShouldBe(50);
        response.Metadata.Paging.NextCursor.ShouldBe("producer-next");
        response.Metadata.Paging.TotalCount.ShouldBe(250);
        response.Metadata.Paging.HasMore.ShouldBe(false);
    }

    [Fact]
    public async Task Submit_FreshnessPolicyWithUnknownFreshness_FailsClosed() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(resultPayload));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(RequireFresh: true),
        };

        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => controller.Submit(request, null, CancellationToken.None));

        ex.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ex.ReasonCode.ShouldBe(QueryProblemReasonCodes.ProjectionStale);
    }

    [Fact]
    public async Task Submit_FreshnessPolicyWithStaleMetadata_FailsClosed() {
        string producerETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                resultPayload,
                metadata: new QueryResponseMetadata(ETag: producerETag, IsStale: true)));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(MaxStaleness: TimeSpan.FromSeconds(5)),
        };

        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => controller.Submit(request, $"\"{producerETag}\"", CancellationToken.None));

        ex.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ex.ReasonCode.ShouldBe(QueryProblemReasonCodes.ProjectionStale);
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        eTagService.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_MaxStalenessWithOldProducerServedAt_FailsClosed() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                resultPayload,
                metadata: new QueryResponseMetadata(
                    IsStale: false,
                    ServedAt: DateTimeOffset.UtcNow.AddMinutes(-10))));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(MaxStaleness: TimeSpan.FromSeconds(5)),
        };

        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => controller.Submit(request, null, CancellationToken.None));

        ex.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ex.ReasonCode.ShouldBe(QueryProblemReasonCodes.ProjectionStale);
    }

    [Fact]
    public async Task Submit_MaxStalenessWithRecentProducerServedAt_ReturnsResponse() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                resultPayload,
                metadata: new QueryResponseMetadata(
                    IsStale: false,
                    ServedAt: DateTimeOffset.UtcNow.AddSeconds(-1))));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(MaxStaleness: TimeSpan.FromMinutes(1)),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.IsStale.ShouldBe(false);
    }

    [Fact]
    public async Task Submit_MaxStalenessExtremelyLarge_DoesNotOverflow_ReturnsResponse() {
        // A caller-supplied MaxStaleness large enough to underflow DateTimeOffset when subtracted from
        // the served-at timestamp must not throw; the freshness age is compared as a bounded TimeSpan.
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                resultPayload,
                metadata: new QueryResponseMetadata(
                    IsStale: false,
                    ServedAt: DateTimeOffset.UtcNow.AddSeconds(-1))));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(MaxStaleness: TimeSpan.MaxValue),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.IsStale.ShouldBe(false);
    }

    [Fact]
    public async Task Submit_FreshnessPolicyWithFreshMetadata_ReturnsResponse() {
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                resultPayload,
                metadata: new QueryResponseMetadata(IsStale: false)));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(RequireFresh: true),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.IsStale.ShouldBe(false);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData(QueryResponseProvenance.Unknown)]
    public async Task Submit_RequireFreshWithoutProjectionProvenance_RejectsApparentFreshness(QueryResponseProvenance provenance) {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", JsonSerializer.SerializeToElement(new { count = 1 }), Metadata:
                new QueryResponseMetadata(IsStale: false, ProjectionVersion: "v1") {
                    Provenance = provenance,
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(RequireFresh: true),
        };

        QueryExecutionFailedException exception = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => controller.Submit(request, null, CancellationToken.None));

        exception.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        exception.ReasonCode.ShouldBe(QueryProblemReasonCodes.ProjectionStale);
        controller.Response.Headers.ContainsKey(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
    }

    [Fact]
    public async Task Submit_FreshnessPolicyWithCurrentLifecycle_IgnoresContradictoryLegacyStale() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(IsStale: true) {
                    Lifecycle = ProjectionLifecycleState.Current,
                }));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(RequireFresh: true),
        };

        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

        SubmitQueryResponse response = actionResult.ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<SubmitQueryResponse>();
        _ = response.Metadata.ShouldNotBeNull();
        response.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        response.Metadata.IsStale.ShouldBe(false);
    }

    [Fact]
    public async Task Submit_FreshnessPolicyWithRebuildingLifecycle_FailsDespiteLegacyCurrent() {
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(
                metadata: new QueryResponseMetadata(IsStale: false) {
                    Lifecycle = ProjectionLifecycleState.Rebuilding,
                }));
        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest() with {
            Freshness = new QueryFreshnessPolicy(RequireFresh: true),
        };

        QueryExecutionFailedException exception = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => controller.Submit(request, null, CancellationToken.None));

        exception.ReasonCode.ShouldBe(QueryProblemReasonCodes.ProjectionStale);
        controller.Response.Headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_EmptyPagingObject_DoesNotSkipETagPreCheck() {
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateProjectionResult(metadata: new QueryResponseMetadata(ETag: testETag)));
        IETagService eTagService = Substitute.For<IETagService>();
        QueriesController controller = CreateController(mediator, eTagService);
        // Paging object with all-null fields (no meaningful policy) must not skip the ETag pre-check
        SubmitQueryRequest request = CreateTestRequest() with {
            Paging = new QueryPagingOptions(),
        };

        IActionResult actionResult = await controller.Submit(request, $"\"{testETag}\"", CancellationToken.None);

        // An empty paging object allows revalidation after the mediator returns.
        actionResult.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(304);
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);
    }
}
