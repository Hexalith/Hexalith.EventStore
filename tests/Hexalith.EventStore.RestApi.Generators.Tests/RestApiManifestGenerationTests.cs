using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class RestApiManifestGenerationTests
{
    [Fact]
    public void Run_NoRestApiAttribute_EmitsNoSources()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CommandSourceWithoutRestApiAttribute);

        result.GeneratedTrees.Length.ShouldBe(0);
        RestApiGeneratorTestHarness.ContainsGeneratedSource(
            result,
            "HexalithEventStoreRestApiGeneratorManifest.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_MixedCommandAndQuery_ManifestRecordsOptionsCountsTypesAndRoutes()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(MixedCommandAndQuerySource);

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
    public void Run_NonMarkerClassWithRoute_IsIgnored()
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(NonMarkerRouteSource);

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
            source.ShouldNotContain(Path.GetTempPath());
            source.ShouldNotContain(Environment.MachineName);
            source.ShouldNotContain("Test0.cs");
        }
    }

    private static string[] GetGeneratedSources(string source)
    {
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(source);
        return result.Results
            .SelectMany(static generator => generator.GeneratedSources)
            .Select(static generated => generated.SourceText.ToString())
            .OrderBy(static generated => generated, StringComparer.Ordinal)
            .ToArray();
    }

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
