using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    public void Run_DuplicateJsonPropertyNames_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(DuplicateJsonPropertyNamesSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST010");

        diagnostic.GetMessage().ShouldContain("JSON name 'cursor'");
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
    [InlineData(RecordStructContractSource)]
    [InlineData(StructContractSource)]
    public void Run_UnsupportedContractShape_ReportsStableDiagnosticAndNoController(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST006");

        diagnostic.GetMessage().ShouldContain("cannot be emitted");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_ReferencedUnsupportedStructContractShape_ReportsStableDiagnosticAndNoController()
    {
        CSharpCompilation contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedStructContractSource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        CSharpCompilation hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedContractHostSource);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(hostCompilation, out _);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST006");

        diagnostic.GetMessage().ShouldContain("cannot be emitted");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData(LeadingSlashRouteSource)]
    [InlineData(TildeWithoutSlashRouteSource)]
    [InlineData(UnclosedRouteTemplateSource)]
    [InlineData(UnescapedBraceRouteTemplateSource)]
    [InlineData(EscapedBraceInRouteParameterNameSource)]
    [InlineData(CatchAllRouteParameterNotFinalSource)]
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
    public void Run_CommandDuplicateJsonPropertyNames_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CommandDuplicateJsonPropertyNamesSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST010");

        diagnostic.GetMessage().ShouldContain("JSON name 'counterId'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_CaseOnlyDuplicateJsonPropertyNames_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CaseOnlyDuplicateJsonPropertyNamesSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST010");

        diagnostic.GetMessage().ShouldContain("JSON name 'cursor'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_CommandCaseOnlyDuplicateJsonPropertyNames_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CommandCaseOnlyDuplicateJsonPropertyNamesSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST010");

        diagnostic.GetMessage().ShouldContain("JSON name 'counterId'");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData(InvalidAggregateBindingSource)]
    [InlineData(OutOfRangeAggregateBindingSource)]
    [InlineData(OutOfRangeEntityBindingSource)]
    [InlineData(EmptyConstantAggregateBindingSource)]
    [InlineData(WhitespaceConstantEntityBindingSource)]
    [InlineData(EntitySourceNoneWithValueBindingSource)]
    [InlineData(EmptyRouteAggregateBindingSource)]
    public void Run_InvalidRestQueryBindingMetadata_ReportsStableDiagnosticAndNoController(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST012");

        diagnostic.GetMessage().ShouldContain("RestQueryBinding");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_AmbiguousRoutePropertyMatch_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(AmbiguousRoutePropertyMatchSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST013");

        diagnostic.GetMessage().ShouldContain("route parameter 'counterId'");
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
    public void Run_NonScalarCommandRouteParameter_ReportsStableDiagnosticAndNoController()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(NonScalarCommandRouteParameterSource);

        Diagnostic diagnostic = ShouldContainError(result, "HESREST009");

        diagnostic.GetMessage().ShouldContain("route parameter 'filters'");
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

    private const string DuplicateJsonPropertyNamesSource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record SearchCounters(
            [property: JsonPropertyName("cursor")] string? Cursor,
            [property: JsonPropertyName("cursor")] string? NextCursor) : IQueryContract
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

    private const string RecordStructContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public readonly record struct StructCommand(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "struct-command";
            public string AggregateId => CounterId;
        }
        """;

    private const string StructContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public readonly struct StructCommand : ICommandContract
        {
            public StructCommand(string counterId)
            {
                CounterId = counterId;
            }

            public static string Domain => "counter";
            public static string CommandType => "struct-command";
            public string AggregateId => CounterId;
            public string CounterId { get; }
        }
        """;

    private const string ReferencedStructContractSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Smoke.Contracts;

        [RestRoute(RestVerb.Post, "{counterId}/increment", ApiScope = "counter")]
        public readonly struct StructCommand : ICommandContract
        {
            public StructCommand(string counterId)
            {
                CounterId = counterId;
            }

            public static string Domain => "counter";
            public static string CommandType => "struct-command";
            public string AggregateId => CounterId;
            public string CounterId { get; }
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

    private const string TildeWithoutSlashRouteSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "~absolute")]
        public sealed record TildeCounterQuery() : IQueryContract
        {
            public static string QueryType => "tilde-counter-query";
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

    private const string UnescapedBraceRouteTemplateSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counter{Id}}")]
        public sealed record BrokenCounterQuery(string CounterId) : IQueryContract
        {
            public static string QueryType => "broken-counter-query";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string EscapedBraceInRouteParameterNameSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counter{{Id}:int}")]
        public sealed record BrokenCounterQuery(string CounterId) : IQueryContract
        {
            public static string QueryType => "broken-counter-query";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string CatchAllRouteParameterNotFinalSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{*path}/tail")]
        public sealed record BrokenCounterQuery(string Path) : IQueryContract
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

    private const string CommandDuplicateJsonPropertyNamesSource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/rename")]
        public sealed record RenameCounter(
            [property: JsonPropertyName("counterId")] string CounterId,
            [property: JsonPropertyName("counterId")] string TargetCounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "rename-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string CaseOnlyDuplicateJsonPropertyNamesSource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        public sealed record SearchCounters(
            [property: JsonPropertyName("cursor")] string? Cursor,
            [property: JsonPropertyName("Cursor")] string? NextCursor) : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string CommandCaseOnlyDuplicateJsonPropertyNamesSource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/rename")]
        public sealed record RenameCounter(
            [property: JsonPropertyName("counterId")] string CounterId,
            [property: JsonPropertyName("CounterId")] string TargetCounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "rename-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string InvalidAggregateBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.None, "index")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string OutOfRangeAggregateBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding((RestQueryBindingSource)999, "index")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string OutOfRangeEntityBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.Constant, "index", (RestQueryBindingSource)999, "counter-1")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string EmptyConstantAggregateBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.Constant, "")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string WhitespaceConstantEntityBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.Constant, "index", RestQueryBindingSource.Constant, " ")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string EntitySourceNoneWithValueBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.Constant, "index", RestQueryBindingSource.None, "counter-1")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string EmptyRouteAggregateBindingSource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestQueryBinding(RestQueryBindingSource.Route, "")]
        public sealed record SearchCounters() : IQueryContract
        {
            public static string QueryType => "search-counters";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;

    private const string AmbiguousRoutePropertyMatchSource = """
        using System.Text.Json.Serialization;

        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/merge")]
        public sealed record MergeCounter(
            [property: JsonPropertyName("counter_id")] string CounterId,
            [property: JsonPropertyName("counterId")] string Alias) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "merge-counter";
            public string AggregateId => CounterId;
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

    private const string NonScalarCommandRouteParameterSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{filters}/update")]
        public sealed record UpdateCounterFilters(string CounterId, string[] Filters) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "update-counter-filters";
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

    private const string ReferencedContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;
}
