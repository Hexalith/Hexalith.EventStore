using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

/// <summary>Guards named human classification in the actual emitted controller.</summary>
public sealed class HttpHostSecurityTests
{
    /// <summary>Verifies every Emitted Controller Names The Shared Human Policy.</summary>
    [Fact]
    public void EveryEmittedControllerNamesTheSharedHumanPolicy()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Commands;
            using Hexalith.EventStore.Contracts.Rest;
            [assembly: RestApi("api/{tenantId}/counter", "counter", RestTenantSource.Route)]
            namespace SecurityFixture;
            [RestRoute(RestVerb.Post, "{counterId}/increment")]
            public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
            {
                public static string Domain => "counter";
                public static string CommandType => "increment-counter";
                public string AggregateId => CounterId;
            }
            """);
        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(compilation, out GeneratorDriverRunResult result, out var diagnostics);
        diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(result, ".Controller.g.cs");
        source.ShouldContain("[Authorize(Policy = \"EventStoreHuman\")]");
        source.ShouldNotContain("[AllowAnonymous]");
        source.ShouldNotContain("[Authorize]");
    }
}
