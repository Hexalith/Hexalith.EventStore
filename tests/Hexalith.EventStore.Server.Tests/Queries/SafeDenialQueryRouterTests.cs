
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

/// <summary>
/// Covers the I/O &amp; edge-case matrix for the opt-in safe-denial boundary (Story 6.1-P2):
/// same-tenant success, Forbidden vs. not-found unification on opted-in routes, cross-tenant
/// negative control, and unchanged behavior on non-opted-in routes.
/// </summary>
public class SafeDenialQueryRouterTests {
    private static SubmitQuery CreateQuery(
        string domain = "orders",
        string queryType = "list-orders",
        string tenant = "test-tenant") =>
        new(
            Tenant: tenant,
            Domain: domain,
            AggregateId: "order-index",
            QueryType: queryType,
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1");

    private static SafeDenialQueryRouter CreateSut(
        IQueryRouter inner,
        params (string Domain, string QueryType)[] optedInRoutes) {
        var policy = new SafeDenialQueryRouteRegistry(optedInRoutes);
        return new SafeDenialQueryRouter(inner, policy, NullLogger<SafeDenialQueryRouter>.Instance);
    }

    // Matrix row: "Same-tenant list query, opted-in route" -> normal payload, N/A error handling.
    [Fact]
    public async Task RouteQueryAsync_SuccessfulResult_OptedInRoute_PassesThroughUnchanged() {
        JsonElement payload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        var successResult = new QueryRouterResult(Success: true, Payload: payload, NotFound: false, ProjectionType: "order-list");
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(successResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(successResult);
    }

    // Matrix row: "Forbidden (wrong tenant/scope), opted-in route" -> same denial shape as NotFound.
    [Fact]
    public async Task RouteQueryAsync_ForbiddenResult_OptedInRoute_UnifiesToNotFoundShape() {
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        result.ProjectionType.ShouldBeNull();

        // Byte-identical to a genuine not-found result's shape.
        result.ShouldBe(new QueryRouterResult(Success: false, Payload: null, NotFound: true));
    }

    // Matrix row: "Nonexistent resource, opted-in route" -> same denial shape as Forbidden; a
    // genuine not-found result is already the canonical shape, so it passes through unchanged.
    [Fact]
    public async Task RouteQueryAsync_GenuineNotFoundResult_OptedInRoute_PassesThroughUnchanged() {
        var notFoundResult = new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(notFoundResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(notFoundResult);
    }

    // Nothing guarantees an arbitrary wrapped IQueryRouter already omits ErrorMessage/Metadata/
    // ProjectionType on a NotFound=true result -- the not-found short-circuit must canonicalize
    // (reconstruct the clean shape) rather than pass such a "dirty" not-found result through as-is.
    [Fact]
    public async Task RouteQueryAsync_DirtyNotFoundResult_OptedInRoute_IsCanonicalizedToCleanShape() {
        JsonElement payload = JsonDocument.Parse("{\"leaked\":true}").RootElement;
        var dirtyNotFoundResult = new QueryRouterResult(
            Success: false,
            Payload: payload,
            NotFound: true,
            ErrorMessage: "leaked-detail",
            ProjectionType: "leaked-projection-type",
            Metadata: new QueryResponseMetadata(ETag: "leaked-etag"));
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(dirtyNotFoundResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(new QueryRouterResult(Success: false, Payload: null, NotFound: true));
    }

    // Matrix row: "Forbidden vs. Nonexistent on an opted-in route are indistinguishable" —
    // proves the two denial categories collapse onto exactly the same QueryRouterResult shape.
    [Fact]
    public async Task RouteQueryAsync_ForbiddenAndGenuineNotFound_OptedInRoute_ProduceIdenticalShape() {
        IQueryRouter forbiddenInner = Substitute.For<IQueryRouter>();
        _ = forbiddenInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));
        IQueryRouter notFoundInner = Substitute.For<IQueryRouter>();
        _ = notFoundInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: true));

        SafeDenialQueryRouter forbiddenSut = CreateSut(forbiddenInner, ("orders", "list-orders"));
        SafeDenialQueryRouter notFoundSut = CreateSut(notFoundInner, ("orders", "list-orders"));

        QueryRouterResult forbiddenResult = await forbiddenSut.RouteQueryAsync(CreateQuery());
        QueryRouterResult notFoundResult = await notFoundSut.RouteQueryAsync(CreateQuery());

        forbiddenResult.ShouldBe(notFoundResult);
    }

    // Matrix row: "Cross-tenant negative control" -> denied via safe-denial shape; no tenant data
    // or existence leaked. The underlying router is the authority on cross-tenant detection (it
    // returns Forbidden because the envelope's TenantId does not match the resource's tenant);
    // the safe-denial adapter must still unify that outcome to the generic not-found shape with
    // no tenant identifier, error text, or resource-existence signal surviving in the result.
    [Fact]
    public async Task RouteQueryAsync_CrossTenantForbidden_OptedInRoute_UnifiesWithoutLeakingTenantOrExistenceDetails() {
        const string CrossTenantErrorDetail = "Forbidden";
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(
                Arg.Is<SubmitQuery>(q => q.Tenant == "attacker-tenant"),
                Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: CrossTenantErrorDetail));
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery(tenant: "attacker-tenant"));

        // No tenant identifier, error text, or existence signal survives in the returned shape —
        // the only externally observable state is the generic not-found shape itself.
        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        result.ProjectionType.ShouldBeNull();
        result.ShouldBe(new QueryRouterResult(Success: false, Payload: null, NotFound: true));
    }

    // Matrix row: "Non-opted-in route, existing Forbidden case" -> unchanged: distinct
    // Forbidden (403); the safe-denial adapter must be a strict no-op for routes that never
    // opted in, regardless of what routes are registered elsewhere.
    [Fact]
    public async Task RouteQueryAsync_ForbiddenResult_NonOptedInRoute_PassesThroughUnchanged() {
        var forbiddenResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(forbiddenResult);

        // Route registered for a different domain/query type than the query under test.
        SafeDenialQueryRouter sut = CreateSut(inner, ("parties", "list-parties"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery(domain: "orders", queryType: "list-orders"));

        result.ShouldBe(forbiddenResult);
    }

    [Fact]
    public async Task RouteQueryAsync_ForbiddenResult_NoOptedInRoutesAtAll_PassesThroughUnchanged() {
        var forbiddenResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(forbiddenResult);
        SafeDenialQueryRouter sut = CreateSut(inner);

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(forbiddenResult);
    }

    // Matrix row: "Nonexistent resource, opted-in route" -> the second existing not-found shape
    // (a projection actor's "no projection state available" error message, see
    // SubmitQueryHandlerTests.Handle_MissingProjectionState_ThrowsQueryNotFoundException) must be
    // unified identically to Forbidden and hard not-found -- otherwise this genuinely-absent
    // shape stays distinguishable from a safe-denied Forbidden result on an opted-in route.
    [Fact]
    public async Task RouteQueryAsync_MissingProjectionStateResult_OptedInRoute_UnifiesToNotFoundShape() {
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: QueryAdapterFailureReason.MissingProjectionState));
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(new QueryRouterResult(Success: false, Payload: null, NotFound: true));
    }

    // Proves all three denial causes (Forbidden, hard not-found, and the second "missing
    // projection state" not-found shape) collapse onto exactly the same QueryRouterResult shape
    // on an opted-in route.
    [Fact]
    public async Task RouteQueryAsync_MissingProjectionStateAndForbidden_OptedInRoute_ProduceIdenticalShape() {
        IQueryRouter forbiddenInner = Substitute.For<IQueryRouter>();
        _ = forbiddenInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));
        IQueryRouter missingProjectionInner = Substitute.For<IQueryRouter>();
        _ = missingProjectionInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.MissingProjectionState));

        SafeDenialQueryRouter forbiddenSut = CreateSut(forbiddenInner, ("orders", "list-orders"));
        SafeDenialQueryRouter missingProjectionSut = CreateSut(missingProjectionInner, ("orders", "list-orders"));

        QueryRouterResult forbiddenResult = await forbiddenSut.RouteQueryAsync(CreateQuery());
        QueryRouterResult missingProjectionResult = await missingProjectionSut.RouteQueryAsync(CreateQuery());

        forbiddenResult.ShouldBe(missingProjectionResult);
    }

    // Non-opted-in route: the "missing projection state" shape must remain unchanged, identically
    // to Forbidden's non-opted-in behavior.
    [Fact]
    public async Task RouteQueryAsync_MissingProjectionStateResult_NonOptedInRoute_PassesThroughUnchanged() {
        var missingProjectionResult = new QueryRouterResult(
            Success: false,
            Payload: null,
            NotFound: false,
            ErrorMessage: QueryAdapterFailureReason.MissingProjectionState);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(missingProjectionResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("parties", "list-parties"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery(domain: "orders", queryType: "list-orders"));

        result.ShouldBe(missingProjectionResult);
    }

    // Guard: a non-validating inner router could return a result for a SubmitQuery with a blank
    // Domain/QueryType. SafeDenialQueryRouteRegistry.IsOptedIn throws ArgumentException for
    // blank inputs -- the decorator must fail safe to an unmodified pass-through instead of
    // letting that become an unhandled exception.
    [Theory]
    [InlineData(null, "list-orders")]
    [InlineData("", "list-orders")]
    [InlineData("   ", "list-orders")]
    [InlineData("orders", null)]
    [InlineData("orders", "")]
    [InlineData("orders", "   ")]
    public async Task RouteQueryAsync_BlankDomainOrQueryType_DoesNotThrow_PassesThroughUnchanged(string? domain, string? queryType) {
        var forbiddenResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(forbiddenResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));
        var query = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: domain!,
            AggregateId: "order-index",
            QueryType: queryType!,
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1");

        QueryRouterResult result = await sut.RouteQueryAsync(query);

        result.ShouldBe(forbiddenResult);
    }

    // The Forbidden/MissingProjectionState sentinel comparison must be StringComparison.Ordinal
    // (case-sensitive), matching DaprTenantQueryService and the exact-casing constants the actor
    // always emits. A differently-cased ErrorMessage is a distinct value under Ordinal and must
    // NOT be unified -- this test would fail if the comparison ever regressed to
    // OrdinalIgnoreCase, since a differently-cased value would then be treated as a match.
    [Theory]
    [InlineData("forbidden")]
    [InlineData("FORBIDDEN")]
    [InlineData("Forbidden ")]
    public async Task RouteQueryAsync_DifferentlyCasedForbiddenSentinel_OptedInRoute_IsNotUnified(string differentlyCasedErrorMessage) {
        var failureResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: differentlyCasedErrorMessage);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(failureResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(failureResult);
    }

    [Theory]
    [InlineData("no projection state available for this aggregate")]
    [InlineData("NO PROJECTION STATE AVAILABLE FOR THIS AGGREGATE")]
    [InlineData("No projection state available for this aggregate ")]
    public async Task RouteQueryAsync_DifferentlyCasedMissingProjectionStateSentinel_OptedInRoute_IsNotUnified(string differentlyCasedErrorMessage) {
        var failureResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: differentlyCasedErrorMessage);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(failureResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(failureResult);
    }

    // Non-Forbidden failures (e.g. actor exceptions) on an opted-in route must not be unified —
    // only Forbidden and genuine not-found collapse together.
    [Fact]
    public async Task RouteQueryAsync_NonForbiddenFailure_OptedInRoute_PassesThroughUnchanged() {
        var failureResult = new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.ActorException);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(failureResult);
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        QueryRouterResult result = await sut.RouteQueryAsync(CreateQuery());

        result.ShouldBe(failureResult);
    }

    [Fact]
    public async Task RouteQueryAsync_NullQuery_ThrowsArgumentNullException() {
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        SafeDenialQueryRouter sut = CreateSut(inner, ("orders", "list-orders"));

        _ = await Should.ThrowAsync<ArgumentNullException>(() => sut.RouteQueryAsync(null!));
    }

    // End-to-end observable-behavior check: on an opted-in route, SubmitQueryHandler throws the
    // exact same exception type for a safe-denied Forbidden result as it does for a genuine
    // not-found result — the caller-visible outcome (404 QueryNotFoundException) is identical.
    [Fact]
    public async Task RouteQueryAsync_ForbiddenOptedInRoute_ThenSubmitQueryHandler_ThrowsSameExceptionTypeAsGenuineNotFound() {
        IQueryRouter forbiddenInner = Substitute.For<IQueryRouter>();
        _ = forbiddenInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));
        var forbiddenHandler = new SubmitQueryHandler(
            CreateSut(forbiddenInner, ("orders", "list-orders")),
            NullLogger<SubmitQueryHandler>.Instance);

        IQueryRouter notFoundInner = Substitute.For<IQueryRouter>();
        _ = notFoundInner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: true));
        var notFoundHandler = new SubmitQueryHandler(
            CreateSut(notFoundInner, ("orders", "list-orders")),
            NullLogger<SubmitQueryHandler>.Instance);

        QueryNotFoundException fromForbidden = await Should.ThrowAsync<QueryNotFoundException>(
            () => forbiddenHandler.Handle(CreateQuery(), CancellationToken.None));
        QueryNotFoundException fromNotFound = await Should.ThrowAsync<QueryNotFoundException>(
            () => notFoundHandler.Handle(CreateQuery(), CancellationToken.None));

        fromForbidden.Tenant.ShouldBe(fromNotFound.Tenant);
        fromForbidden.Domain.ShouldBe(fromNotFound.Domain);
        fromForbidden.AggregateId.ShouldBe(fromNotFound.AggregateId);
        fromForbidden.QueryType.ShouldBe(fromNotFound.QueryType);
        fromForbidden.Message.ShouldBe(fromNotFound.Message);
    }

    // Never boundary: on a route that never opted in, Forbidden still throws
    // QueryExecutionFailedException with 403 — identical to today's behavior even though the
    // safe-denial adapter is present and wired for other routes.
    [Fact]
    public async Task RouteQueryAsync_ForbiddenNonOptedInRoute_ThenSubmitQueryHandler_StillThrows403() {
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        _ = inner.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));

        // The adapter has routes registered, but not this one.
        var handler = new SubmitQueryHandler(
            CreateSut(inner, ("parties", "list-parties")),
            NullLogger<SubmitQueryHandler>.Instance);

        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => handler.Handle(CreateQuery(domain: "orders", queryType: "list-orders"), CancellationToken.None));

        ex.StatusCode.ShouldBe(403);
        ex.Detail.ShouldBe(QueryAdapterFailureReason.Forbidden);
    }
}
