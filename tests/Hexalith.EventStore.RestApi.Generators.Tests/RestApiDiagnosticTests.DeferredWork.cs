using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed partial class RestApiDiagnosticTests
{
    [Theory]
    [InlineData("(RestTenantSource)999", "Inherit")]
    [InlineData("(RestTenantSource)(-2)", "Claims")]
    [InlineData("Claims", "(RestTenantSource)999")]
    [InlineData("Route", "(RestTenantSource)(-2)")]
    public void Run_UndefinedTenantSource_ReportsDiagnosticAndNoController(string assemblySource, string routeSource)
    {
        ArgumentNullException.ThrowIfNull(assemblySource);
        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(CreateDeferredMetadataSource(assemblySource, routeSource));

        Diagnostic diagnostic = ShouldContainError(result, "HESREST014");

        diagnostic.GetMessage().ShouldContain("tenant source");
        diagnostic.GetMessage().ShouldContain(assemblySource.StartsWith('(') ? "Hexalith.EventStore.RestApi.Generators.Tests.Smoke" : "Smoke.CounterStatus");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Fact]
    public void Run_ReferencedUndefinedRouteTenantSource_ReportsContractIdentityAndNoController()
    {
        string source = CreateDeferredMetadataSource(routeSource: "(RestTenantSource)999");
        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(RestApiGeneratorTestHarness.CreateCompilation(source));
        CSharpCompilation host = CreateDeferredHost(reference);

        GeneratorDriverRunResult result = RestApiGeneratorTestHarness.Run(host, out _);

        ShouldContainError(result, "HESREST014").GetMessage().ShouldContain("Smoke.CounterStatus");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData("Inherit")]
    [InlineData("Claims")]
    [InlineData("Route")]
    [InlineData("System")]
    [InlineData("Platform")]
    public void Run_DefinedTenantSources_PreserveControllerGeneration(string tenantSource)
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(CreateDeferredMetadataSource(tenantSource, tenantSource));

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(compilation, out GeneratorDriverRunResult result, out ImmutableArray<Diagnostic> diagnostics);

        diagnostics.ShouldBeEmpty();
        RestApiGeneratorTestHarness.GetDiagnostics(result).ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeTrue();
    }

    [Theory]
    [InlineData("RestQueryBindingSource.Route, \" counterId\", RestQueryBindingSource.None, null")]
    [InlineData("RestQueryBindingSource.Constant, \"index \", RestQueryBindingSource.None, null")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.Route, \"counterId \"")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.Constant, \" entity\"")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.None, \"\"")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.None, \" \"")]
    [InlineData("RestQueryBindingSource.Route, \"\\tcounterId\", RestQueryBindingSource.None, null")]
    [InlineData("RestQueryBindingSource.Constant, \"index\\u00a0\", RestQueryBindingSource.None, null")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.Route, \"counterId\\u00a0\"")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.Constant, \"\\tentity\"")]
    public void Run_InvalidBindingValues_RejectsSourceAndReferencedContracts(string binding)
    {
        string source = CreateDeferredMetadataSource(binding: binding);
        GeneratorDriverRunResult sourceResult = RestApiGeneratorTestHarness.Run(source);

        _ = ShouldContainError(sourceResult, "HESREST012");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(sourceResult, ".Controller.g.cs").ShouldBeFalse();

        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(RestApiGeneratorTestHarness.CreateCompilation(source));
        CSharpCompilation host = CreateDeferredHost(reference);
        GeneratorDriverRunResult referencedResult = RestApiGeneratorTestHarness.Run(host, out _);

        _ = ShouldContainError(referencedResult, "HESREST012");
        RestApiGeneratorTestHarness.ContainsGeneratedSource(referencedResult, ".Controller.g.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData("RestQueryBindingSource.Constant, \"index\"")]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.None, null")]
    [InlineData("RestQueryBindingSource.Constant, \"aggregate name\", RestQueryBindingSource.Constant, \"entity name\"")]
    public void Run_ValidBindings_PreserveOmittedNullAndLiteralValues(string binding)
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(CreateDeferredMetadataSource(binding: binding));

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(compilation, out GeneratorDriverRunResult result, out ImmutableArray<Diagnostic> diagnostics);

        diagnostics.ShouldBeEmpty();
        RestApiGeneratorTestHarness.GetDiagnostics(result).ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        RestApiGeneratorTestHarness.ContainsGeneratedSource(result, ".Controller.g.cs").ShouldBeTrue();

        MetadataReference reference = RestApiGeneratorTestHarness.EmitToMetadataReference(compilation);
        CSharpCompilation referencedOutput = RestApiGeneratorTestHarness.RunAndUpdateCompilation(CreateDeferredHost(reference), out GeneratorDriverRunResult referencedResult, out ImmutableArray<Diagnostic> referencedDiagnostics);

        referencedDiagnostics.ShouldBeEmpty();
        RestApiGeneratorTestHarness.GetDiagnostics(referencedResult).ShouldBeEmpty();
        referencedOutput.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        RestApiGeneratorTestHarness.ContainsGeneratedSource(referencedResult, ".Controller.g.cs").ShouldBeTrue();
    }

    private static CSharpCompilation CreateDeferredHost(MetadataReference reference)
        => RestApiGeneratorTestHarness.CreateCompilation([reference], """
            using Hexalith.EventStore.Contracts.Rest;
            [assembly: RestApi("api/counter", "counter")]
            """);

    private static string CreateDeferredMetadataSource(string assemblySource = "Claims", string routeSource = "Inherit", string? binding = null)
        => $$"""
            using Hexalith.EventStore.Contracts.Queries;
            using Hexalith.EventStore.Contracts.Rest;
            [assembly: RestApi("api/counter", "counter", RestTenantSource.{{assemblySource}})]
            namespace Smoke;
            [RestRoute(RestVerb.Get, "{tenantId}/{counterId}", ApiScope = "counter", TenantSource = RestTenantSource.{{routeSource}})]
            {{(binding is null ? string.Empty : "[RestQueryBinding(" + binding + ")]")}}
            public sealed record CounterStatus : IQueryContract
            {
                public static string Domain => "counter";
                public static string QueryType => "counter-status";
                public static string ProjectionType => "counter";
            }
            """
            .Replace("RestTenantSource.(RestTenantSource)", "(RestTenantSource)", StringComparison.Ordinal);
}
