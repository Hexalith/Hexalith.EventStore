using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed partial class RestApiManifestGenerationTests
{
    [Fact]
    public void Run_NoRestApiAttribute_EmitsNoSources()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CommandSourceWithoutRestApiAttribute);

        ShouldHaveNoGeneratorDiagnostics(result);
        result.GeneratedTrees.Length.ShouldBe(0);
        RestApiGeneratorTestHarness.ContainsGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_MixedCommandAndQuery_ManifestRecordsOptionsCountsTypesAndRoutes()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(MixedCommandAndQuerySource);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const bool RestApiAttributeFound = true;");
        manifest.ShouldContain("internal const string RoutePrefix = \"api/counter\";");
        manifest.ShouldContain("internal const string Tag = \"counter\";");
        manifest.ShouldContain("internal const string TenantSource = \"System\";");
        manifest.ShouldContain("internal const int CommandCount = 1;");
        manifest.ShouldContain("internal const int QueryCount = 1;");
        manifest.ShouldContain("internal const int RouteOverrideCount = 2;");
        manifest.ShouldContain("\"Smoke.GetCounterStatus\"");
        manifest.ShouldContain("\"Smoke.IncrementCounter\"");
        manifest.ShouldContain("\"Smoke.GetCounterStatus|Get|{counterId}\"");
        manifest.ShouldContain("\"Smoke.IncrementCounter|Post|{counterId}/increment\"");
    }

    [Fact]
    public void Run_PaddedRestApiTag_NormalizesManifestTag()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(PaddedTagSource);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const string Tag = \"counter\";");
        manifest.ShouldNotContain("internal const string Tag = \" counter \";");
    }

    [Fact]
    public void Run_ReferencedContracts_FiltersManifestByMatchingApiScope()
    {
        var contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedMixedApiScopeSource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        var hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            ReferencedContractHostSource);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(hostCompilation, out _);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = 1;");
        manifest.ShouldContain("internal const int QueryCount = 0;");
        manifest.ShouldContain("internal const int RouteOverrideCount = 1;");
        manifest.ShouldContain("\"Smoke.Contracts.IncrementCounter\"");
        manifest.ShouldNotContain("GetUserTenantsQuery");
    }

    [Fact]
    public void Run_TaglessHost_ExcludesReferencedContractsFromManifest()
    {
        var contractCompilation = RestApiGeneratorTestHarness.CreateCompilation(ReferencedMixedApiScopeSource);
        MetadataReference contractReference = RestApiGeneratorTestHarness.EmitToMetadataReference(contractCompilation);
        var hostCompilation = RestApiGeneratorTestHarness.CreateCompilation(
            [contractReference],
            TaglessReferencedContractHostSource);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(hostCompilation, out _);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = 0;");
        manifest.ShouldContain("internal const int QueryCount = 0;");
        manifest.ShouldContain("internal const int RouteOverrideCount = 0;");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_NonMarkerClassWithRoute_IsIgnored()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(NonMarkerRouteSource);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = 1;");
        manifest.ShouldContain("internal const int QueryCount = 0;");
        manifest.ShouldContain("internal const int RouteOverrideCount = 1;");
        manifest.ShouldContain("\"Smoke.ValidCommand\"");
        manifest.ShouldNotContain("IgnoredRouteOnlyType");
    }

    [Theory]
    [InlineData(CommandOnlySource, 1, 0)]
    [InlineData(QueryOnlySource, 0, 1)]
    [InlineData(MixedCommandAndQuerySource, 1, 1)]
    public void Run_CommandOnlyQueryOnlyAndMixedSources_ProduceDeterministicCounts(
        string source,
        int commandCount,
        int queryCount)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = " + commandCount + ";");
        manifest.ShouldContain("internal const int QueryCount = " + queryCount + ";");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_EmptyAndWhitespaceRouteTemplates_AreAcceptedByDiscovery(string template)
    {
        string source = RouteTemplateSource(template);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);

        ShouldHaveNoGeneratorDiagnostics(result);
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = 1;");
        manifest.ShouldContain("internal const int RouteOverrideCount = 1;");
        manifest.ShouldContain("\"Smoke.TemplateCommand|Post|" + template + "\"");
    }

    [Fact]
    public void Run_IdenticalInputTwice_ProducesByteIdenticalGeneratedOutput()
    {
        string[] first = GetGeneratedSources(MixedCommandAndQuerySource);
        string[] second = GetGeneratedSources(MixedCommandAndQuerySource);

        first.ShouldBe(second);
        foreach (string source in first)
        {
            source.ShouldNotContain(DateTime.UtcNow.Year.ToString());
            source.ShouldNotContain(AppContext.BaseDirectory);
            source.ShouldNotContain(Path.GetFullPath(Environment.CurrentDirectory));
            source.ShouldNotContain(Path.GetTempPath());
            source.ShouldNotContain(Environment.MachineName);
            source.ShouldNotContain("Test0.cs");
            ContainsGuidLiteral(source).ShouldBeFalse("Generated output should not include random GUID values.");
            ContainsUlidLiteral(source).ShouldBeFalse("Generated output should not include random ULID values.");
        }
    }

    private static string[] GetGeneratedSources(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);
        ShouldHaveNoGeneratorDiagnostics(result);
        return result.Results
            .SelectMany(static generator => generator.GeneratedSources)
            .Select(static generated => generated.SourceText.ToString())
            .OrderBy(static generated => generated, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ShouldHaveNoGeneratorDiagnostics(GeneratorDriverRunResult result)
    {
        ImmutableArray<Diagnostic> diagnostics = RestApiGeneratorTestHarness.GetDiagnostics(result);
        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    private static bool ContainsGuidLiteral(string source)
        => GuidPattern().IsMatch(source);

    private static bool ContainsUlidLiteral(string source)
        => UlidPattern().IsMatch(source);

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\b[0-9A-HJKMNP-TV-Z]{26}\b")]
    private static partial Regex UlidPattern();

    private const string CommandSourceWithoutRestApiAttribute = """
        using Hexalith.EventStore.Contracts.Commands;

        namespace Smoke;

        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string CommandOnlySource = """
        using Hexalith.EventStore.Contracts.Commands;
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
        """;

    private const string QueryOnlySource = """
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "{counterId}")]
        public sealed record GetCounterStatus(string CounterId) : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-status";
        }
        """;

    private const string MixedCommandAndQuerySource = """
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

        [RestRoute(RestVerb.Get, "{counterId}")]
        public sealed record GetCounterStatus(string CounterId) : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter-status";
        }
        """;

    private const string PaddedTagSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", " counter ", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }
        """;

    private const string ReferencedMixedApiScopeSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Smoke.Contracts;

        [RestRoute(RestVerb.Post, "{counterId}/increment", ApiScope = "counter")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Get, "~/api/users/{userId}/tenants", ApiScope = "tenants")]
        public sealed record GetUserTenantsQuery(string UserId) : IQueryContract
        {
            public static string QueryType => "get-user-tenants";
            public static string Domain => "tenants";
            public static string ProjectionType => "tenant-user-list";
        }
        """;

    private const string ReferencedContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;

    private const string TaglessReferencedContractHostSource = """
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", null, RestTenantSource.System)]

        namespace Smoke.Host;

        public sealed class HostMarker;
        """;

    private const string NonMarkerRouteSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

        namespace Smoke;

        [RestRoute(RestVerb.Get, "ignored")]
        public sealed record IgnoredRouteOnlyType(string CounterId);

        [RestRoute(RestVerb.Post, "{counterId}/valid")]
        public sealed record ValidCommand(string CounterId) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "valid-command";
            public string AggregateId => CounterId;
        }
        """;

    private static string RouteTemplateSource(string template)
        => $$"""
            using Hexalith.EventStore.Contracts.Commands;
            using Hexalith.EventStore.Contracts.Rest;

            [assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

            namespace Smoke;

            [RestRoute(RestVerb.Post, "{{template}}")]
            public sealed record TemplateCommand(string CounterId) : ICommandContract
            {
                public static string Domain => "counter";
                public static string CommandType => "template-command";
                public string AggregateId => CounterId;
            }
            """;
}
