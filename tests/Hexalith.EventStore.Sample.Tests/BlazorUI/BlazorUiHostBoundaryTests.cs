using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

public sealed class BlazorUiHostBoundaryTests
{
    private static readonly Regex[] GeneratedApiHostForbiddenPatterns =
    [
        new(@"\[\s*assembly\s*:\s*(?:global::)?(?:[\w.]+\.)?RestApi(?:Attribute)?\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?AddMvc\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?AddMvcCore\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?AddControllers\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?AddControllersWithViews\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?MapControllers\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?MapControllerRoute\s*\(", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?MapDefaultControllerRoute\s*\(", RegexOptions.Compiled),
        new(@"\[\s*(?:[\w.]+\.)?ApiController\s*\]", RegexOptions.Compiled),
        new(@"\b(?:[\w.]+\.)?ControllerBase\b", RegexOptions.Compiled),
        new(@":\s*(?:[\w.]+\.)?Controller\b", RegexOptions.Compiled),
        new(@",\s*(?:[\w.]+\.)?Controller\b", RegexOptions.Compiled),
    ];

    [Fact]
    public void BlazorUiProject_ReferencesCompiledContractsLibraryWithoutLinkedContractSources()
    {
        XDocument project = XDocument.Load(BlazorUiProjectPath());
        string[] references = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(static element => Path.GetFileName(((string?)element.Attribute("Include"))?.Replace('\\', '/') ?? string.Empty))
            .ToArray();

        references.ShouldContain(
            "Hexalith.EventStore.Sample.Contracts.csproj",
            "The UI should consume the same compiled contract identity as the domain service and Sample.Api.");

        string[] linkedContracts = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Compile", StringComparison.Ordinal))
            .Select(static element => ((string?)element.Attribute("Include"))?.Replace('\\', '/') ?? string.Empty)
            .Where(static include => include.Contains("Hexalith.EventStore.Sample.Contracts", StringComparison.Ordinal)
                || include.Contains("/Counter/Commands/", StringComparison.Ordinal)
                || include.Contains("/Counter/Queries/", StringComparison.Ordinal))
            .ToArray();

        linkedContracts.ShouldBeEmpty("The UI must not compile-link duplicate Counter contract source files.");
    }

    [Fact]
    public void BlazorUiProject_DoesNotReferenceRestGeneratorAnalyzer()
    {
        XDocument project = XDocument.Load(BlazorUiProjectPath());

        string[] restGeneratorReferences = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal)
                || string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal)
                || string.Equals(element.Name.LocalName, "Analyzer", StringComparison.Ordinal))
            .Select(static element => string.Join(
                "|",
                ((string?)element.Attribute("Include")) ?? string.Empty,
                ((string?)element.Attribute("OutputItemType")) ?? string.Empty,
                ((string?)element.Attribute("ReferenceOutputAssembly")) ?? string.Empty))
            .Where(static value => value.Contains("Hexalith.EventStore.RestApi.Generators", StringComparison.Ordinal))
            .ToArray();

        restGeneratorReferences.ShouldBeEmpty("The interactive UI must not opt into generated REST controllers.");
    }

    [Fact]
    public void BlazorUiSource_DoesNotMapMvcControllersOrDeclareRestApiScope()
    {
        string source = ReadBlazorUiSourceAndProject();

        foreach (Regex pattern in GeneratedApiHostForbiddenPatterns)
        {
            pattern.IsMatch(source).ShouldBeFalse($"The UI source must not contain generated or hand-written MVC API host marker matching {pattern}.");
        }
    }

    private static string BlazorUiProjectPath()
        => Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.BlazorUI",
            "Hexalith.EventStore.Sample.BlazorUI.csproj");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hexalith.EventStore.slnx from the test output path.");
    }

    private static string ReadBlazorUiSourceAndProject()
    {
        string blazorUiRoot = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Hexalith.EventStore.Sample.BlazorUI");

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(blazorUiRoot, "*.*", SearchOption.AllDirectories)
                .Where(static file => file.EndsWith(".cs", StringComparison.Ordinal)
                    || file.EndsWith(".csproj", StringComparison.Ordinal)
                    || file.EndsWith(".razor", StringComparison.Ordinal))
                .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Select(File.ReadAllText));
    }
}
