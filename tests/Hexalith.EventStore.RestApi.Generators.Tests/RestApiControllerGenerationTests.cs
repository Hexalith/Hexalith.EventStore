using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class RestApiControllerGenerationTests
{
    [Fact]
    public void Run_HappyPathControllerSource_GeneratedControllerCompilesAndUsesGatewayBoundary()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(HappyPathControllerSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[ApiController]");
        source.ShouldContain("[Authorize]");
        source.ShouldContain("[Route(\"api/counter\")]");
        source.ShouldContain("[Tags(\"counter\")]");
        source.ShouldContain("    [Consumes(\"application/json\")]");
        source.ShouldContain("using Hexalith.EventStore.Client.Gateway;");
        source.ShouldContain("IEventStoreGatewayClient");
        source.ShouldContain("public sealed partial class CounterRestController(IEventStoreGatewayClient gateway, ICommandStatusLocationBuilder statusLocationBuilder) : ControllerBase");
        source.ShouldContain(".SubmitCommandAsync(__hexalithRequest, cancellationToken)");
        source.ShouldContain(".SubmitQueryAsync(__hexalithRequest, ifNoneMatch, cancellationToken)");
        source.ShouldContain(".ConfigureAwait(false)");
        source.ShouldContain("UniqueIdHelper.GenerateSortableUniqueStringId()");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented, \"application/problem+json\")]");
        source.ShouldContain("[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, \"application/problem+json\")]");
        CountOccurrences(source, "[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict").ShouldBe(2);
        CountOccurrences(source, "[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity").ShouldBe(2);
        source.ShouldContain("[HttpPost(\"{counterId}/increment\")]");
        source.ShouldContain("[HttpPost(\"~/api/counter/{counterId}/reset\")]");
        source.ShouldContain("[HttpGet(\"{counterId}\")]");
        source.ShouldContain("[HttpGet(\"{counterId}/history\")]");
        source.ShouldContain("[HttpGet(\"\")]");
        source.ShouldContain("GetCounterStatusQueryAsync");
        source.ShouldContain("[FromRoute(Name = \"counterId\")] string counterId");
        source.ShouldContain("[FromBody] global::Smoke.IncrementCounter? body");
        source.ShouldContain("CancellationToken cancellationToken");
        source.ShouldContain("CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Request body is required.\")");
        source.ShouldContain("Request body is required.");
        source.ShouldContain("CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Route value 'counterId' does not match the command body.\")");
        source.ShouldContain("Route value 'counterId' does not match the command body.");
        source.ShouldContain("new SubmitCommandRequest(");
        source.ShouldContain("Response.Headers[\"Retry-After\"] = \"1\";");
        source.ShouldContain("statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, out string? __hexalithStatusLocation)");
        source.ShouldContain("Response.Headers[\"Location\"] = __hexalithStatusLocation;");
        source.ShouldNotContain("\"/api/v1/commands/status/\"");
        source.ShouldContain("[FromQuery(Name = \"page\")]");
        source.ShouldContain(" page,");
        source.ShouldContain("[\"page\"] = page,");
        source.ShouldContain("[FromHeader(Name = \"If-None-Match\")] string? ifNoneMatch");
        source.ShouldContain("new SubmitQueryRequest(");
        source.ShouldContain("StatusCodes.Status304NotModified");
        source.ShouldContain("QueryResponseProvenance __hexalithProvenance = __hexalithResult.Metadata?.Provenance ?? QueryResponseProvenance.Unknown;");
        source.ShouldContain("? __hexalithResult.ETag ?? __hexalithResult.Metadata?.ETag");
        source.ShouldContain("TryFormatStrongETag(__hexalithETag, out string? __hexalithBoundedETag)");
        source.ShouldContain("Response.Headers[\"ETag\"] = __hexalithBoundedETag;");
        source.ShouldContain("Not-modified query response requires projection-backed provenance.");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Query-Provenance\"]");
        source.ShouldContain("Response.Headers[ProjectionLifecyclePolicy.HeaderName]");
        source.ShouldContain("Response.Headers.Remove(ProjectionLifecyclePolicy.HeaderName)");
        source.ShouldContain("ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, metadata.IsStale)");
        source.ShouldContain("ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, metadata.IsDegraded)");
        source.ShouldContain("EntityTagHeaderValue.TryParse(candidate, out EntityTagHeaderValue? parsed)");
        source.ShouldContain("parsed.IsWeak");
        source.ShouldContain("MapGatewayException(ex, includeCommandOnlyStatusCodes: true)");
        source.ShouldContain("MapGatewayException(ex, includeCommandOnlyStatusCodes: false)");
        source.ShouldContain("NormalizeGatewayExceptionStatusCode(exception.StatusCode, includeCommandOnlyStatusCodes)");
        source.ShouldContain("IsRetryableGatewayStatusCode(statusCode)");
        source.ShouldContain("MaxProblemErrorCount");
        source.ShouldContain("MaxRetryAfterSeconds");
        source.ShouldContain("TryGetSafeRetryAfterHeaderValue(exception.RetryAfter, out string? retryAfter)");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Is-Degraded\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Warning-Codes\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Page-Size\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Page-Offset\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Page-Total-Count\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Page-Has-More\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Next-Cursor\"]");
        source.ShouldContain("return Ok(__hexalithResult.Payload);");

        source.ShouldNotContain("Type.GetType");
        source.ShouldNotContain("Assembly.Load");
        source.ShouldNotContain("Activator.CreateInstance");
        source.ShouldNotContain("Hexalith.EventStore.DomainService");
        source.ShouldNotContain("Hexalith.EventStore.Server");
        source.ShouldNotContain("AddEventStoreDomainService");
        source.ShouldNotContain("UseEventStoreDomainService");
        source.ShouldNotContain("DomainQueryDispatcher");
        source.ShouldNotContain("Dapr");
        source.ShouldNotContain("DaprClient");
        source.ShouldNotContain("ProjectionActor");
        source.ShouldNotContain("StateStore");
        source.ShouldNotContain("IMediator");
        source.ShouldNotContain("MediatR");
    }

    [Fact]
    public void Run_CommandWithoutRestRoute_DefaultsToPostAtPrefixRoot()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(ConventionCommandSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[HttpPost(\"\")]");
        source.ShouldContain("[FromBody] global::Smoke.CreateCounter? body");
        source.ShouldContain(".SubmitCommandAsync(__hexalithRequest, cancellationToken)");
    }

    [Fact]
    public void Run_EmptyRouteTemplate_RoutesAtPrefixRoot()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(EmptyRouteQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[HttpGet(\"\")]");
        source.ShouldContain(".SubmitQueryAsync(__hexalithRequest, ifNoneMatch, cancellationToken)");
    }

    [Fact]
    public void Run_RouteTenantSource_ResolvesTenantFromRouteValue()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(RouteTenantSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[Route(\"api/{tenantId}/counter\")]");
        source.ShouldContain("[FromRoute(Name = \"tenantId\")] string tenantId");
        source.ShouldContain("ResolveTenant(null, tenantId);");
    }

    [Fact]
    public void Run_ClaimsTenantSource_UsesClaimsWithoutParsingBearerTokens()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(ClaimsTenantSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("User.FindAll(\"eventstore:tenant\")");
        source.ShouldNotContain("[Consumes(\"application/json\")]");
        source.ShouldNotContain("JwtSecurityToken");
        source.ShouldNotContain("ReadJwtToken");
        source.ShouldNotContain("Bearer");
        source.ShouldNotContain("Request.Headers[\"Authorization\"]");
    }

    [Fact]
    public void Run_SystemTenantSource_ValidatesTenantRouteParamAgainstBody()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(SystemTenantRouteParamSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        // Under System tenant source the partition tenant comes from the platform, not the route,
        // so the {tenantId} segment is a plain aggregate identifier that MUST be reconciled with the
        // command body — otherwise a route/body id mismatch would dispatch against the body id (AC2).
        source.ShouldContain("ResolveTenant(null, null);");
        source.ShouldContain("[FromRoute(Name = \"tenantId\")] string tenantId");
        source.ShouldContain("CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Route value 'tenantId' does not match the command body.\")");
    }

    [Fact]
    public void Run_RouteTenantSource_DoesNotReconcileTenantRouteParamWithBody()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(RouteTenantSourceWithTenantProperty);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        // Under Route tenant source the {tenantId} prefix is the partition tenant (validated by
        // ResolveTenant), so it must NOT be body-reconciled even when the command exposes a matching
        // TenantId property. The genuine aggregate route param is still reconciled.
        source.ShouldContain("ResolveTenant(null, tenantId);");
        source.ShouldNotContain("Route value 'tenantId' does not match the command body.");
        source.ShouldContain("Route value 'counterId' does not match the command body.");
    }

    [Fact]
    public void Run_QueryPayload_UsesJsonContractNamesForPayloadKeys()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(JsonNamedQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[FromQuery(Name = \"cursor_token\")]");
        source.ShouldContain("[FromQuery(Name = \"pageSize\")]");
        source.ShouldContain("[\"cursor_token\"] = cursorToken,");
        source.ShouldContain("[\"pageSize\"] = pageSize,");
    }

    [Fact]
    public void Run_QueryOnlyController_OmitsCommandStatusLocationBuilderConstructorParameter()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(JsonNamedQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        // A query-only controller must NOT gain an unread ICommandStatusLocationBuilder primary-ctor
        // parameter: CS9113 (unread parameter) is promoted to an error under TreatWarningsAsErrors=true.
        // Green diagnostics here are the CS9113 build-break guard for AC 5.
        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("(IEventStoreGatewayClient gateway) : ControllerBase");
        source.ShouldNotContain("ICommandStatusLocationBuilder");
        source.ShouldNotContain("statusLocationBuilder");
    }

    [Fact]
    public void Run_JsonNamedRouteParameter_BindsRouteAndOmitsRoutePropertyFromQueryPayload()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(JsonNamedRouteQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[FromRoute(Name = \"counter_id\")] string counter_id");
        source.ShouldContain("string __hexalithAggregateId = Convert.ToString(counter_id, CultureInfo.InvariantCulture)");
        source.ShouldContain("[FromQuery(Name = \"page_size\")]");
        source.ShouldContain("[\"page_size\"] = pageSize,");
        source.ShouldNotContain("[FromQuery(Name = \"counter_id\")]");
        source.ShouldNotContain("[\"counter_id\"] = counterId,");
    }

    [Fact]
    public void Run_JsonNamedRouteParameter_MatchesJsonNameCaseInsensitively()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(JsonNamedRouteCaseInsensitiveQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[FromRoute(Name = \"COUNTER_ID\")] string cOUNTER_ID");
        source.ShouldContain("string __hexalithAggregateId = Convert.ToString(cOUNTER_ID, CultureInfo.InvariantCulture)");
        source.ShouldContain("[FromQuery(Name = \"page_size\")]");
        source.ShouldNotContain("[FromQuery(Name = \"counter_id\")]");
        source.ShouldNotContain("[\"counter_id\"] = counterId,");
    }

    [Fact]
    public void Run_RouteTemplateWithRegexConstraintContainingEscapedBraces_GeneratesController()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(RegexConstrainedRouteQuerySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[HttpGet(\"{counterId:regex(^\\\\d{{3}}$)}\")]");
        source.ShouldContain("[FromRoute(Name = \"counterId\")] string counterId");
        source.ShouldContain("string __hexalithAggregateId = Convert.ToString(counterId, CultureInfo.InvariantCulture)");
    }

    [Fact]
    public void Run_InheritedPublicProperties_GenerateBindingsAndMismatchChecks()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(InheritedPropertySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("body.CounterId");
        source.ShouldContain("[FromQuery(Name = \"pageSize\")]");
        source.ShouldContain("[\"pageSize\"] = pageSize,");
    }

    [Fact]
    public void Run_CommandRouteKeywordProperty_EscapesGeneratedBodyMemberAccess()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(KeywordPropertySource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[FromRoute(Name = \"event\")] string @event");
        source.ShouldContain("body.@event");
    }

    [Fact]
    public void Run_RouteTenantAliases_GenerateConflictCheck()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(RouteTenantAliasSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("ResolveTenant(tenant, tenantId);");
        source.ShouldContain("Tenant route values differ.");
    }

    [Fact]
    public void Run_ReferencedQueryContract_GeneratesRouteTenantControllerAction()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedCounterQuerySource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedContractHostSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            hostCompilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[Route(\"api/{tenant}/counter\")]");
        source.ShouldContain("[HttpGet(\"{entityId}\")]");
        source.ShouldContain("[FromRoute(Name = \"tenant\")] string tenant");
        source.ShouldContain("[FromRoute(Name = \"entityId\")] string entityId");
        source.ShouldContain("ResolveTenant(tenant, null);");
        source.ShouldContain("string __hexalithAggregateId = Convert.ToString(entityId, CultureInfo.InvariantCulture)");
        source.ShouldContain("string? __hexalithEntityId = Convert.ToString(entityId, CultureInfo.InvariantCulture)");
        source.ShouldContain("JsonElement? __hexalithPayload = null;");
        source.ShouldContain("global::Hexalith.EventStore.Sample.Counter.Queries.GetCounterStatusQuery.Domain");
        source.ShouldContain("global::Hexalith.EventStore.Sample.Counter.Queries.GetCounterStatusQuery.QueryType");
        source.ShouldContain("global::Hexalith.EventStore.Sample.Counter.Queries.GetCounterStatusQuery.ProjectionType");
        source.ShouldContain(".SubmitQueryAsync(__hexalithRequest, ifNoneMatch, cancellationToken)");
        source.ShouldContain("StatusCodes.Status304NotModified");
        source.ShouldContain("TryFormatStrongETag(__hexalithETag, out string? __hexalithBoundedETag)");
        source.ShouldContain("Response.Headers[\"ETag\"] = __hexalithBoundedETag;");
        source.ShouldContain("return Ok(__hexalithResult.Payload);");

        source.ShouldNotContain("DomainQueryDispatcher");
        source.ShouldNotContain("Dapr");
        source.ShouldNotContain("ProjectionActor");
        source.ShouldNotContain("StateStore");
        source.ShouldNotContain("IMediator");
        source.ShouldNotContain("MediatR");
    }

    [Fact]
    public void Run_ReferencedQueryContractWithExplicitBinding_GeneratesQueryEnvelopeAndFreshnessHeaders()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedBoundTenantsQuerySource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedTenantsContractHostSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            hostCompilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[HttpGet(\"~/api/global-administrators\")]");
        source.ShouldContain("[HttpGet(\"~/api/users/{userId}/tenants\")]");
        source.ShouldContain("[FromRoute(Name = \"userId\")] string userId");
        source.ShouldContain("string __hexalithAggregateId = \"global-administrators\";");
        source.ShouldContain("string? __hexalithEntityId = \"global-administrators\";");
        source.ShouldContain("string __hexalithAggregateId = \"index\";");
        source.ShouldContain("string? __hexalithEntityId = Convert.ToString(userId, CultureInfo.InvariantCulture)");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Projection-Version\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Served-At\"]");
        source.ShouldContain("Response.Headers[\"X-Hexalith-Is-Stale\"]");
        source.ShouldContain("ToString(\"O\", CultureInfo.InvariantCulture)");
        source.ShouldContain("StatusCodes.Status304NotModified");
    }

    [Fact]
    public void Run_ReferencedCommandContract_GeneratesRouteTenantPostAction()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedCounterCommandSource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedContractHostSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            hostCompilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[Route(\"api/{tenant}/counter\")]");
        source.ShouldContain("[FromRoute(Name = \"tenant\")] string tenant");
        source.ShouldContain("[FromRoute(Name = \"counterId\")] string counterId");
        source.ShouldContain("Route value 'counterId' does not match the command body.");
        source.ShouldContain("new SubmitCommandRequest(");
        source.ShouldContain("UniqueIdHelper.GenerateSortableUniqueStringId()");
        source.ShouldContain("body.AggregateId");
        source.ShouldContain(".SubmitCommandAsync(__hexalithRequest, cancellationToken)");
        source.ShouldContain("Response.Headers[\"Retry-After\"] = \"1\";");
        source.ShouldContain("statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, out");
        source.ShouldNotContain("\"/api/v1/commands/status/\"");
        source.ShouldContain("StatusCodes.Status202Accepted");
        source.ShouldContain("ConfigureAwait(false)");

        foreach ((string typeName, string verb) in new[]
        {
            ("IncrementCounter", "increment"),
            ("DecrementCounter", "decrement"),
            ("ResetCounter", "reset"),
            ("CloseCounter", "close"),
        })
        {
            string fullName = $"global::Hexalith.EventStore.Sample.Counter.Commands.{typeName}";
            source.ShouldContain($"[HttpPost(\"{{counterId}}/{verb}\")]");
            source.ShouldContain($"[FromBody] {fullName}? body");
            source.ShouldContain($"{fullName}.Domain");
            source.ShouldContain($"{fullName}.CommandType");
        }

        source.ShouldNotContain("DomainQueryDispatcher");
        source.ShouldNotContain("Dapr");
        source.ShouldNotContain("ProjectionActor");
        source.ShouldNotContain("StateStore");
        source.ShouldNotContain("IMediator");
        source.ShouldNotContain("MediatR");
    }

    [Fact]
    public void Run_ReferencedContractsOutsideApiScope_AreExcluded()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedMixedApiScopeSource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedContractHostSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            hostCompilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("[HttpGet(\"{entityId}\")]");
        source.ShouldContain("global::Hexalith.EventStore.Sample.Counter.Queries.GetCounterStatusQuery.QueryType");
        source.ShouldNotContain("~/api/users/{userId}/tenants");
        source.ShouldNotContain("global::Hexalith.Tenants.Contracts.Queries.GetUserTenantsQuery.QueryType");
    }

    [Fact]
    public void Run_TaglessHost_DoesNotPublishUnscopedReferencedContracts()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedUnscopedCounterQuerySource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            TaglessReferencedContractHostSource);

        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            hostCompilation,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> updateDiagnostics);

        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            runResult,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int QueryCount = 0;");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(runResult, ".Controller.g.cs").ShouldBeFalse();
    }

    private static void ShouldHaveNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostic[] errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private const string HappyPathControllerSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Post, "~/api/counter/{counterId}/reset")]
        public sealed record ResetCounter(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "reset-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Get, "{counterId}")]
        public sealed record GetCounterStatus(string CounterId) : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-status";
        }

        [RestRoute(RestVerb.Get, "{counterId}/history")]
        public sealed record GetCounterHistory(string CounterId, int Page) : IQueryContract
        {
            public static string QueryType => "get-counter-history";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-history";
        }

        public sealed record ListCounters() : IQueryContract
        {
            public static string QueryType => "list-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-list";
        }
        """;

    private const string ConventionCommandSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record CreateCounter(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "create-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string EmptyRouteQuerySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "")]
        public sealed record GetCounterRoot() : IQueryContract
        {
            public static string QueryType => "get-counter-root";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-root";
        }
        """;

    private const string RouteTenantSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/{tenantId}/counter", "counter", RestTenantSource.Route)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string SystemTenantRouteParamSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/tenants", "tenants", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{tenantId}/disable")]
        public sealed record DisableTenant(string TenantId) : ICommandContract
        {
            public static string Domain => "tenants";
            public static string CommandType => "disable-tenant";
            public string AggregateId => TenantId;
        }
        """;

    private const string RouteTenantSourceWithTenantProperty = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/{tenantId}/counter", "counter", RestTenantSource.Route)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, string TenantId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string ClaimsTenantSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.Claims)]

        namespace Smoke;

        public sealed record ListCounters() : IQueryContract
        {
            public static string QueryType => "list-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-list";
        }
        """;

    private const string JsonNamedQuerySource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record SearchCounters([property: JsonPropertyName("cursor_token")] string? CursorToken, int PageSize) : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-search";
        }
        """;

    private const string JsonNamedRouteQuerySource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counter_id}/history")]
        public sealed record GetCounterHistory(
            [property: JsonPropertyName("counter_id")] string CounterId,
            [property: JsonPropertyName("page_size")] int PageSize) : IQueryContract
        {
            public static string QueryType => "get-counter-history";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-history";
        }
        """;

    private const string JsonNamedRouteCaseInsensitiveQuerySource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{COUNTER_ID}/history")]
        public sealed record GetCounterHistory(
            [property: JsonPropertyName("counter_id")] string CounterId,
            [property: JsonPropertyName("page_size")] int PageSize) : IQueryContract
        {
            public static string QueryType => "get-counter-history";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-history";
        }
        """;

    private const string RegexConstrainedRouteQuerySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counterId:regex(^\\d{{3}}$)}")]
        public sealed record GetCounterStatus(string CounterId) : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string InheritedPropertySource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public abstract record CounterCommandBase
        {
            public string CounterId { get; init; } = "";
        }

        [RestRoute(RestVerb.Post, "{counterId}/rename")]
        public sealed record RenameCounter(string Name) : CounterCommandBase, ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "rename-counter";
            public string AggregateId => CounterId;
        }

        public abstract record PagedQuery
        {
            public int PageSize { get; init; }
        }

        public sealed record ListCounters(string? Cursor) : PagedQuery, IQueryContract
        {
            public static string QueryType => "list-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-list";
        }
        """;

    private const string KeywordPropertySource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{event}")]
        public sealed record RecordCounterEvent(string CounterId, string @event) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "record-counter-event";
            public string AggregateId => CounterId;
        }
        """;

    private const string RouteTenantAliasSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/{tenant}/{tenantId}/counter", "counter", RestTenantSource.Route)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string ReferencedCounterQuerySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Hexalith.EventStore.Sample.Counter.Queries;

        [RestRoute(RestVerb.Get, "{entityId}", ApiScope = "counter")]
        public sealed record GetCounterStatusQuery : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string ReferencedBoundTenantsQuerySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Hexalith.Tenants.Contracts.Queries;

        [RestRoute(RestVerb.Get, "~/api/global-administrators", ApiScope = "tenants")]
        [RestQueryBinding(RestQueryBindingSource.Constant, "global-administrators", RestQueryBindingSource.Constant, "global-administrators")]
        public sealed record GetGlobalAdministratorsQuery(string? Cursor, int PageSize) : IQueryContract
        {
            public static string QueryType => "get-global-administrators";
            public static string Domain => "global-administrators";
            public static string ProjectionType => "global-administrators";
        }

        [RestRoute(RestVerb.Get, "~/api/users/{userId}/tenants", ApiScope = "tenants")]
        [RestQueryBinding(RestQueryBindingSource.Constant, "index", RestQueryBindingSource.Route, "userId")]
        public sealed record GetUserTenantsQuery(string UserId, string? Cursor, int PageSize) : IQueryContract
        {
            public static string QueryType => "get-user-tenants";
            public static string Domain => "tenants";
            public static string ProjectionType => "tenant";
        }
        """;

    private const string ReferencedCounterCommandSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Hexalith.EventStore.Sample.Counter.Commands;

        [RestRoute(RestVerb.Post, "{counterId}/increment", ApiScope = "counter")]
        public sealed record IncrementCounter(string CounterId = "counter-1") : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Post, "{counterId}/decrement", ApiScope = "counter")]
        public sealed record DecrementCounter(string CounterId = "counter-1") : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "decrement-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Post, "{counterId}/reset", ApiScope = "counter")]
        public sealed record ResetCounter(string CounterId = "counter-1") : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "reset-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Post, "{counterId}/close", ApiScope = "counter")]
        public sealed record CloseCounter(string CounterId = "counter-1") : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "close-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string ReferencedMixedApiScopeSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Hexalith.EventStore.Sample.Counter.Queries
        {
            [RestRoute(RestVerb.Get, "{entityId}", ApiScope = " counter ")]
            public sealed record GetCounterStatusQuery : IQueryContract
            {
                public static string QueryType => "get-counter-status";
                public static string Domain => "counter";
                public static string ProjectionType => "counter";
            }
        }

        namespace Hexalith.Tenants.Contracts.Queries
        {
            [RestRoute(RestVerb.Get, "~/api/users/{userId}/tenants", ApiScope = "tenants")]
            public sealed record GetUserTenantsQuery(string UserId) : IQueryContract
            {
                public static string QueryType => "get-user-tenants";
                public static string Domain => "tenants";
                public static string ProjectionType => "tenant";
            }
        }
        """;

    private const string ReferencedUnscopedCounterQuerySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Hexalith.EventStore.Sample.Counter.Queries;

        [RestRoute(RestVerb.Get, "{entityId}")]
        public sealed record GetCounterStatusQuery : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string ReferencedContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/{tenant}/counter", " counter ", RestTenantSource.Route)]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;

    private const string TaglessReferencedContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter")]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;

    private const string ReferencedTenantsContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/tenants", "tenants", RestTenantSource.Claims)]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;
}
