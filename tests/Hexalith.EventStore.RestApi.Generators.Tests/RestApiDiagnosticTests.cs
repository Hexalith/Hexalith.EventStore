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

    [Fact]
    public void Run_GeneratedActionParameterCollision_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(ParameterCollisionSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST004");

        diagnostic.GetMessage().ShouldContain("duplicate action identifier 'body'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData(GenericContractSource)]
    [InlineData(AbstractContractSource)]
    [InlineData(InternalContractSource)]
    public void Run_UnsupportedContractShape_ReportsStableDiagnosticAndNoController(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST006");

        diagnostic.GetMessage().ShouldContain("cannot be emitted");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData(LeadingSlashRouteSource)]
    [InlineData(UnclosedRouteTemplateSource)]
    [InlineData(DuplicateRouteParameterSource)]
    public void Run_InvalidRouteTemplate_ReportsStableDiagnosticAndNoController(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST007");

        diagnostic.GetMessage().ShouldContain("invalid route template");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_EquivalentRouteShapes_ReportsDuplicateRouteDiagnostic()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(EquivalentRouteShapeSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST003");
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(result, ".Controller.g.cs");

        diagnostic.GetMessage().ShouldContain("api/counter/{}");
        source.ShouldContain("CounterByIdQueryAsync");
        source.ShouldContain("global::Smoke.CounterById");
        source.ShouldNotContain("CounterByNameQueryAsync");
        source.ShouldNotContain("global::Smoke.CounterByName");
    }

    [Fact]
    public void Run_UnsupportedRestVerb_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(UnsupportedRestVerbSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST008");

        diagnostic.GetMessage().ShouldContain("unsupported route verb '999'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_UnmappedCommandRouteParameter_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(UnmappedCommandRouteParameterSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST009");

        diagnostic.GetMessage().ShouldContain("route parameter 'region'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_RejectedRouteDoesNotReserveMethodNameForLaterValidEndpoint()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(RouteReservationFailureSource);

        _ = ShouldContainError(result, "HESREST003");
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(result, ".Controller.g.cs");

        source.ShouldContain("global::Smoke.Z.SharedName");
        source.ShouldNotContain("global::Smoke.M.SharedName");
    }

    private static Diagnostic ShouldContainError(GeneratorDriverRunResult result, string id)
    {
        ImmutableArray<Diagnostic> diagnostics = RestApiGeneratorTestHarness.GetDiagnostics(result);
        Diagnostic[] errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors
            .Select(static diagnostic => diagnostic.Id)
            .ShouldBe(new[] { id }, string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return errors.Single();
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

    private const string ParameterCollisionSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{body}/increment")]
        public sealed record IncrementCounter(string Body, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => Body;
        }
        """;

    private const string GenericContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record GenericCommand<T>(string CounterId, T Value) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "generic-command";
            public string AggregateId => CounterId;
        }
        """;

    private const string AbstractContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public abstract record AbstractCommand(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "abstract-command";
            public string AggregateId => CounterId;
        }
        """;

    private const string InternalContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        sealed record InternalCommand(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "internal-command";
            public string AggregateId => CounterId;
        }
        """;

    private const string LeadingSlashRouteSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "/absolute")]
        public sealed record AbsoluteCounterQuery() : IQueryContract
        {
            public static string QueryType => "absolute-counter-query";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string UnclosedRouteTemplateSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counterId")]
        public sealed record BrokenCounterQuery(string CounterId) : IQueryContract
        {
            public static string QueryType => "broken-counter-query";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string DuplicateRouteParameterSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/{counterId}")]
        public sealed record DuplicateRouteCommand(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "duplicate-route-command";
            public string AggregateId => CounterId;
        }
        """;

    private const string EquivalentRouteShapeSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counterId}")]
        public sealed record CounterById(string CounterId) : IQueryContract
        {
            public static string QueryType => "counter-by-id";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }

        [RestRoute(RestVerb.Get, "~/api/counter/{name}")]
        public sealed record CounterByName(string Name) : IQueryContract
        {
            public static string QueryType => "counter-by-name";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string UnsupportedRestVerbSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute((RestVerb)999, "counter")]
        public sealed record UnsupportedVerbQuery() : IQueryContract
        {
            public static string QueryType => "unsupported-verb-query";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string UnmappedCommandRouteParameterSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{region}/{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string RouteReservationFailureSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke.A
        {
            [RestRoute(RestVerb.Get, "same")]
            public sealed record OtherQuery() : IQueryContract
            {
                public static string QueryType => "other-query";
                public static string Domain => "counter";
                public static string ProjectionType => "counter";
            }
        }

        namespace Smoke.M
        {
            [RestRoute(RestVerb.Get, "same")]
            public sealed record SharedName() : IQueryContract
            {
                public static string QueryType => "invalid-shared-name";
                public static string Domain => "counter";
                public static string ProjectionType => "counter";
            }
        }

        namespace Smoke.Z
        {
            [RestRoute(RestVerb.Get, "unique")]
            public sealed record SharedName() : IQueryContract
            {
                public static string QueryType => "valid-shared-name";
                public static string Domain => "counter";
                public static string ProjectionType => "counter";
            }
        }
        """;
}
