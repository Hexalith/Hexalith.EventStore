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
        source.ShouldContain("IEventStoreGatewayClient");
        source.ShouldContain(".SubmitCommandAsync(__hexalithRequest, cancellationToken)");
        source.ShouldContain(".SubmitQueryAsync(__hexalithRequest, ifNoneMatch, cancellationToken)");
        source.ShouldContain(".ConfigureAwait(false)");
        source.ShouldContain("UniqueIdHelper.GenerateSortableUniqueStringId()");
        source.ShouldContain("[HttpPost(\"{counterId}/increment\")]");
        source.ShouldContain("[HttpPost(\"~/api/counter/{counterId}/reset\")]");
        source.ShouldContain("[HttpGet(\"{counterId}/history\")]");
        source.ShouldContain("[HttpGet(\"\")]");
        source.ShouldContain("[FromRoute(Name = \"counterId\")] string counterId");
        source.ShouldContain("[FromBody] global::Smoke.IncrementCounter? body");
        source.ShouldContain("CancellationToken cancellationToken");
        source.ShouldContain("Request body is required.");
        source.ShouldContain("Route value 'counterId' does not match the command body.");
        source.ShouldContain("new SubmitCommandRequest(");
        source.ShouldContain("Response.Headers[\"Retry-After\"] = \"1\";");
        source.ShouldContain("Response.Headers[\"Location\"] = \"/api/v1/commands/status/\"");
        source.ShouldContain("[FromQuery(Name = \"Page\")]");
        source.ShouldContain(" page,");
        source.ShouldContain("[FromHeader(Name = \"If-None-Match\")] string? ifNoneMatch");
        source.ShouldContain("new SubmitQueryRequest(");
        source.ShouldContain("StatusCodes.Status304NotModified");
        source.ShouldContain("Response.Headers[\"ETag\"] = FormatStrongETag(__hexalithResult.ETag);");
        source.ShouldContain("return Ok(__hexalithResult.Payload);");

        source.ShouldNotContain("Type.GetType");
        source.ShouldNotContain("Assembly.Load");
        source.ShouldNotContain("Activator.CreateInstance");
        source.ShouldNotContain("DomainQueryDispatcher");
        source.ShouldNotContain("Dapr");
        source.ShouldNotContain("ProjectionActor");
        source.ShouldNotContain("StateStore");
        source.ShouldNotContain("IMediator");
        source.ShouldNotContain("MediatR");
    }

    [Fact]
    public void Run_CommandWithoutRestRoute_DefaultsToPostAtPrefixRoot()
    {
        GeneratorDriverRunResult runResult = RestApiGeneratorTestHarness.Run(ConventionCommandSource);

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
        GeneratorDriverRunResult runResult = RestApiGeneratorTestHarness.Run(ClaimsTenantSource);

        string source = RestApiGeneratorTestHarness.GetGeneratedSource(runResult, ".Controller.g.cs");

        source.ShouldContain("User.FindAll(\"eventstore:tenant\")");
        source.ShouldNotContain("JwtSecurityToken");
        source.ShouldNotContain("ReadJwtToken");
        source.ShouldNotContain("Bearer");
        source.ShouldNotContain("Request.Headers[\"Authorization\"]");
    }

    private static void ShouldHaveNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostic[] errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
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
}
