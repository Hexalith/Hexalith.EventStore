using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class RestApiIncrementalGenerationTests
{
    [Fact]
    public void Run_TracksMessageDiscoveryAndRestApiOptionsSteps()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(ExistingCommandSource);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(compilation, out _);
        GeneratorRunResult generatorResult = result.Results.Single();

        generatorResult.TrackedSteps.ContainsKey("RestApiMessageDiscovery").ShouldBeTrue();
        generatorResult.TrackedSteps.ContainsKey("RestApiOptions").ShouldBeTrue();
    }

    [Fact]
    public void Run_UnrelatedSyntaxTreeEdit_PreservesExistingMessageDiscoveryOutput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CSharpCompilation firstCompilation = RestApiGeneratorTestHarness.CreateCompilation(ExistingCommandSource);
        _ = RestApiGeneratorTestHarness.Run(firstCompilation, out GeneratorDriver firstDriver);

        SyntaxTree unrelatedTree = CreateSyntaxTree(
            firstCompilation,
            "namespace Smoke; public sealed class Unrelated { }",
            "Unrelated.cs",
            cancellationToken);
        CSharpCompilation secondCompilation = firstCompilation.AddSyntaxTrees(unrelatedTree);

        GeneratorDriver secondDriver = firstDriver.RunGenerators(secondCompilation, cancellationToken);
        GeneratorRunResult generatorResult = secondDriver.GetRunResult().Results.Single();

        HasStepReason(
            generatorResult,
            "RestApiMessageDiscovery",
            IncrementalStepRunReason.Cached,
            IncrementalStepRunReason.Unchanged).ShouldBeTrue();
    }

    [Fact]
    public void Run_AddingNewCommandMarker_ProducesNewOutputAndPreservesExistingOutput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CSharpCompilation firstCompilation = RestApiGeneratorTestHarness.CreateCompilation(ExistingCommandSource);
        _ = RestApiGeneratorTestHarness.Run(firstCompilation, out GeneratorDriver firstDriver);

        SyntaxTree newCommandTree = CreateSyntaxTree(
            firstCompilation,
            NewCommandSource,
            "NewCommand.cs",
            cancellationToken);
        CSharpCompilation secondCompilation = firstCompilation.AddSyntaxTrees(newCommandTree);

        GeneratorDriver secondDriver = firstDriver.RunGenerators(secondCompilation, cancellationToken);
        GeneratorRunResult generatorResult = secondDriver.GetRunResult().Results.Single();
        string manifest = RestApiGeneratorTestHarness.GetGeneratedSource(
            secondDriver.GetRunResult(),
            "HexalithEventStoreRestApiGeneratorManifest.g.cs");

        manifest.ShouldContain("internal const int CommandCount = 2;");
        HasStepReason(generatorResult, "RestApiMessageDiscovery", IncrementalStepRunReason.New).ShouldBeTrue();
        HasStepReason(
            generatorResult,
            "RestApiMessageDiscovery",
            IncrementalStepRunReason.Cached,
            IncrementalStepRunReason.Unchanged).ShouldBeTrue();
    }

    private static bool HasStepReason(
        GeneratorRunResult generatorResult,
        string stepName,
        params IncrementalStepRunReason[] reasons)
        => generatorResult.TrackedSteps.TryGetValue(stepName, out ImmutableArray<IncrementalGeneratorRunStep> steps)
            && steps
                .SelectMany(static step => step.Outputs)
                .Any(output => reasons.Contains(output.Reason));

    private static SyntaxTree CreateSyntaxTree(
        CSharpCompilation compilation,
        string source,
        string path,
        CancellationToken cancellationToken)
        => CSharpSyntaxTree.ParseText(
            source,
            (CSharpParseOptions)compilation.SyntaxTrees.First().Options,
            path,
            cancellationToken: cancellationToken);

    private const string ExistingCommandSource = """
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

    private const string NewCommandSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Rest;

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/decrement")]
        public sealed record DecrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "decrement-counter";
            public string AggregateId => CounterId;
        }
        """;
}
