using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class RestApiDiagnosticTests
{
    [Fact]
    public void Run_RouteTenantSourceWithoutTenantRouteParameter_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(RouteTenantWithoutTenantParameterSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST001");

        diagnostic.GetMessage().ShouldContain("has no '{tenant}' or '{tenantId}' route parameter");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_AmbiguousQueryRouteShape_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(AmbiguousQueryRouteSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST002");

        diagnostic.GetMessage().ShouldContain("multiple non-tenant route parameters");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_UnsupportedQueryParameterShape_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(UnsupportedQueryParameterSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST005");

        diagnostic.GetMessage().ShouldContain("cannot be bound from query string");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_InvalidShapeAlongsideValidShape_EmitsOnlyValidAction()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(ValidCommandAndInvalidQuerySource);

        _ = ShouldContainError(result, "HESREST002");
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(result, ".Controller.g.cs");

        source.ShouldContain("IncrementCounterCommandAsync");
        source.ShouldNotContain("AmbiguousCounterQueryAsync");
        source.ShouldNotContain("global::Smoke.AmbiguousCounter");
    }

    private static Diagnostic ShouldContainError(GeneratorDriverRunResult result, string id)
    {
        ImmutableArray<Diagnostic> diagnostics = RestApiGeneratorTestHarness.GetDiagnostics(result);
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldContain(id);
        Diagnostic diagnostic = diagnostics.Single(diagnostic => string.Equals(diagnostic.Id, id, StringComparison.Ordinal));
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        return diagnostic;
    }

    private const string RouteTenantWithoutTenantParameterSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.Route)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string AmbiguousQueryRouteSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{region}/{counterId}")]
        public sealed record AmbiguousCounter(string Region, string CounterId) : IQueryContract
        {
            public static string QueryType => "ambiguous-counter";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string UnsupportedQueryParameterSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record SearchCounters(string[] Tags) : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string ValidCommandAndInvalidQuerySource = """
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

        [RestRoute(RestVerb.Get, "{region}/{counterId}")]
        public sealed record AmbiguousCounter(string Region, string CounterId) : IQueryContract
        {
            public static string QueryType => "ambiguous-counter";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;
}
