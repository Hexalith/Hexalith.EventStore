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

    [Theory]
    [InlineData(null, "OwnerClient.Generated")]
    [InlineData("Configured.Root", "Configured.Root.Generated")]
    [InlineData("__arglist", "@__arglist.Generated")]
    public void TypedBindingsUseResolvedNamespace(string? rootNamespace, string expectedNamespace)
    {
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            [QueryResultContract(typeof(string), "owner", "owner.read")]
            public sealed record Query;
            """).WithAssemblyName("Owner-Client");

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics,
            rootNamespace is null ? null : new GeneratorTestOptionsProvider(rootNamespace));

        diagnostics.ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs")
            .ShouldContain($"namespace {expectedNamespace};");
    }

    [Theory]
    [InlineData("null!", "public sealed record", "a result type, domain and wire query type are required")]
    [InlineData("typeof(void)", "public sealed record", "is unsupported")]
    [InlineData("typeof(System.Collections.Generic.List<>)", "public sealed record", "is unsupported")]
    [InlineData("typeof(System.Span<int>)", "public sealed record", "is unsupported")]
    [InlineData("typeof(StaticResult)", "public sealed record", "is unsupported")]
    [InlineData("typeof(InternalResult)", "public sealed record", "is not effectively public")]
    [InlineData("typeof(InternalResult.Nested)", "public sealed record", "is not effectively public")]
    [InlineData("typeof(System.Collections.Generic.List<InternalResult[]>)", "public sealed record", "is not effectively public")]
    [InlineData("typeof(string)", "public static class", "the request type is static")]
    public void InvalidContractReportsDiagnosticAndKeepsValidBindings(string result, string queryDeclaration, string reason)
    {
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation($$"""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            internal sealed class InternalResult { public sealed class Nested; }
            public static class StaticResult { public sealed class Nested; }
            [QueryResultContract({{result}}, "owner", "owner.invalid")]
            {{queryDeclaration}} InvalidQuery;
            [QueryResultContract(typeof(System.Collections.Generic.List<StaticResult.Nested[]>), "owner", "owner.valid")]
            public sealed record ValidQuery;
            """);

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("HESREST015");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Location.IsInSource.ShouldBeTrue();
        diagnostic.GetMessage().ShouldContain("Owner.InvalidQuery");
        diagnostic.GetMessage().ShouldContain(reason);
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(item => item.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs");
        source.ShouldContain("global::Owner.ValidQuery query");
        source.ShouldNotContain("InvalidQuery");
    }

    [Fact]
    public void InvalidReferencedContractReportsDiagnosticWithoutGeneratingAnEmptyBindingClass()
    {
        CSharpCompilation contracts = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            internal sealed record InternalResult;
            [QueryResultContract(typeof(InternalResult), "owner", "owner.invalid")]
            public sealed record InvalidQuery;
            """).WithAssemblyName("Owner.Contracts");
        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(contracts);
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation([reference], "namespace Consumer; public sealed class Marker;");

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("HESREST015");
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage().ShouldContain("Owner.InvalidQuery");
        diagnostic.GetMessage().ShouldContain("is not effectively public");
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(item => item.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        RestApiGeneratorTestHarness.ContainsGeneratedSource(generated, "TypedGatewayQueries.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void MissingAttributeArgumentReportsOnTheAnnotatedPartAndKeepsValidBindings()
    {
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation("""
            namespace Owner;
            public sealed partial record InvalidQuery;
            """, """
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            [QueryResultContract(domain: "owner", queryType: "owner.invalid")]
            public sealed partial record InvalidQuery;
            [QueryResultContract(typeof(string), "owner", "owner.valid")]
            public sealed record ValidQuery;
            """);

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("HESREST015");
        diagnostic.Location.SourceTree?.FilePath.ShouldBe("Test1.cs");
        diagnostic.GetMessage().ShouldContain("a result type, domain and wire query type are required");
        Diagnostic compilerError = output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(item => item.Severity == DiagnosticSeverity.Error).ShouldHaveSingleItem();
        compilerError.Id.ShouldBe("CS7036");
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs");
        source.ShouldContain("global::Owner.ValidQuery query");
        source.ShouldNotContain("InvalidQuery");
    }

    [Fact]
    public void InvalidReferencedContractKeepsValidReferencedAndSameNamedSourceBindings()
    {
        CSharpCompilation contracts = RestApiGeneratorTestHarness.CreateCompilation("""
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            [QueryResultContract(null!, "owner", "owner.invalid")]
            public sealed record Query;
            [QueryResultContract(typeof(string), "owner", "owner.referenced")]
            public sealed record ReferencedQuery;
            """).WithAssemblyName("Owner.Contracts");
        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(contracts);
        CSharpCompilation input = RestApiGeneratorTestHarness.CreateCompilation([reference], """
            using Hexalith.EventStore.Contracts.Queries;
            namespace Owner;
            [QueryResultContract(typeof(string), "owner", "owner.source")]
            public sealed record Query;
            """);

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(input,
            out GeneratorDriverRunResult generated, out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("HESREST015");
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage().ShouldContain("Owner.Query");
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(item => item.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        string source = RestApiGeneratorTestHarness.GetGeneratedSource(generated, "TypedGatewayQueries.g.cs");
        source.ShouldContain("\"owner.source\"");
        source.ShouldContain("\"owner.referenced\"");
        source.ShouldNotContain("\"owner.invalid\"");
    }
}
