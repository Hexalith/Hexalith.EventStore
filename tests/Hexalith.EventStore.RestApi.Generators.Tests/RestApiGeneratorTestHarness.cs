using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

internal static class RestApiGeneratorTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Preview);

    internal static CSharpCompilation CreateCompilation(params string[] sources)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release);

        return CSharpCompilation.Create(
            "Hexalith.EventStore.RestApi.Generators.Tests.Smoke",
            sources.Select((source, index) => CreateSyntaxTree(source, "Test" + index + ".cs", cancellationToken)),
            CreateMetadataReferences(),
            options);
    }

    internal static GeneratorDriverRunResult Run(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);
        return Run(compilation, out _);
    }

    internal static GeneratorDriverRunResult Run(
        CSharpCompilation compilation,
        out GeneratorDriver driver)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        driver = CreateDriver();
        driver = driver.RunGenerators(compilation, cancellationToken);
        return driver.GetRunResult();
    }

    internal static CSharpCompilation RunAndUpdateCompilation(
        CSharpCompilation compilation,
        out GeneratorDriverRunResult runResult,
        out ImmutableArray<Diagnostic> updateDiagnostics)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out updateDiagnostics,
            cancellationToken);
        runResult = driver.GetRunResult();
        return (CSharpCompilation)outputCompilation;
    }

    internal static string GetGeneratedSource(
        GeneratorDriverRunResult runResult,
        string hintNameSuffix)
    {
        foreach (GeneratedSourceResult source in runResult.Results.SelectMany(static result => result.GeneratedSources))
        {
            if (source.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal))
            {
                return source.SourceText.ToString();
            }
        }

        throw new InvalidOperationException("Generated source ending with '" + hintNameSuffix + "' was not found.");
    }

    internal static bool ContainsGeneratedSource(
        GeneratorDriverRunResult runResult,
        string hintNameSuffix)
        => runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .Any(source => source.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal));

    internal static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult runResult)
        => runResult.Results.SelectMany(static result => result.Diagnostics).ToImmutableArray();

    private static GeneratorDriver CreateDriver()
        => CSharpGeneratorDriver.Create(
            [new RestApiGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    private static SyntaxTree CreateSyntaxTree(
        string source,
        string path,
        CancellationToken cancellationToken)
        => CSharpSyntaxTree.ParseText(
            SourceText.From(source, Encoding.UTF8),
            ParseOptions,
            path,
            cancellationToken: cancellationToken);

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences()
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        AddReference(paths, typeof(object).Assembly);
        AddReference(paths, typeof(Attribute).Assembly);
        AddReference(paths, typeof(Enumerable).Assembly);
        AddReference(paths, typeof(CancellationToken).Assembly);
        AddReference(paths, typeof(Dictionary<,>).Assembly);
        AddReference(paths, typeof(JsonSerializer).Assembly);
        AddReference(paths, typeof(System.Security.Claims.ClaimsPrincipal).Assembly);
        AddReference(paths, typeof(ICommandContract).Assembly);
        AddReference(paths, typeof(IQueryContract).Assembly);
        AddReference(paths, typeof(RestApiAttribute).Assembly);
        AddReference(paths, typeof(IEventStoreGatewayClient).Assembly);
        AddReference(paths, typeof(EventStoreGatewayException).Assembly);
        AddReference(paths, typeof(UniqueIdHelper).Assembly);
        AddReference(paths, typeof(ControllerBase).Assembly);
        AddReference(paths, typeof(ApiControllerAttribute).Assembly);
        AddReference(paths, typeof(IActionResult).Assembly);
        AddReference(paths, typeof(ProblemDetails).Assembly);
        AddReference(paths, typeof(StatusCodes).Assembly);
        AddReference(paths, typeof(TagsAttribute).Assembly);
        AddReference(paths, typeof(IHeaderDictionary).Assembly);
        AddReference(paths, typeof(AuthorizeAttribute).Assembly);
        AddReference(paths, typeof(StringValues).Assembly);
        AddReference(paths, typeof(MediaTypeHeaderValue).Assembly);

        AddRuntimeAssembly(paths, "netstandard.dll");
        AddRuntimeAssembly(paths, "System.Collections.dll");
        AddRuntimeAssembly(paths, "System.Collections.Concurrent.dll");
        AddRuntimeAssembly(paths, "System.Collections.Frozen.dll");
        AddRuntimeAssembly(paths, "System.Collections.Immutable.dll");
        AddRuntimeAssembly(paths, "System.ComponentModel.Annotations.dll");
        AddRuntimeAssembly(paths, "System.ComponentModel.Primitives.dll");
        AddRuntimeAssembly(paths, "System.Linq.dll");
        AddRuntimeAssembly(paths, "System.Linq.Expressions.dll");
        AddRuntimeAssembly(paths, "System.Private.Uri.dll");
        AddRuntimeAssembly(paths, "System.Runtime.dll");
        AddRuntimeAssembly(paths, "System.Runtime.Extensions.dll");
        AddRuntimeAssembly(paths, "System.Security.Claims.dll");
        AddRuntimeAssembly(paths, "System.Text.Json.dll");
        AddRuntimeAssembly(paths, "System.Text.RegularExpressions.dll");
        AddRuntimeAssembly(paths, "System.Threading.Tasks.dll");

        return paths.Select(static path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    private static void AddRuntimeAssembly(SortedSet<string> paths, string fileName)
    {
        string? runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDirectory is null)
        {
            return;
        }

        string path = Path.Combine(runtimeDirectory, fileName);
        if (File.Exists(path))
        {
            _ = paths.Add(path);
        }
    }

    private static void AddReference(SortedSet<string> paths, Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
        {
            _ = paths.Add(assembly.Location);
        }
    }
}
