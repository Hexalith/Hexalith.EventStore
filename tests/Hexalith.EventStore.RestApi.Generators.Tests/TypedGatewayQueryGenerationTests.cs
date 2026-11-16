using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class TypedGatewayQueryGenerationTests
{
    [Fact]
    public void ReferencedContractsExportTypedBindingsWithoutRestEnrollment()
    {
        CSharpCompilation contracts = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            public sealed record Result(string OpaqueId);
            [QueryResultContract(typeof(Result), "owner", "Owner.Queries.Read.v1")]
            public sealed record Query(string OpaqueId);
            """).WithAssemblyName("Owner.Contracts");
        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(contracts);
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation([reference], "namespace Consumer; public sealed class Marker;");
        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);
        diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs")
            .ShouldContain("SubmitQueryAsync<global::Owner.Result>");
    }

    [Fact]
    public void TypedComputedQueryCompilesWithoutCreatingOrChangingRestRoutes()
    {
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Smoke;
            public sealed record Result(string Id);
            [QueryResultContract(typeof(Result), "counter", "existing-wire-query.v1")]
            public sealed record Query(string Id);
            """);
        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);
        diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs");
        source.ShouldContain("SubmitQueryAsync<global::Smoke.Result>");
        source.ShouldContain("\"existing-wire-query.v1\"");
        source.ShouldNotContain("HttpGet");
        source.ShouldNotContain("Controller");
        source.ShouldNotContain("catch");
    }
}
