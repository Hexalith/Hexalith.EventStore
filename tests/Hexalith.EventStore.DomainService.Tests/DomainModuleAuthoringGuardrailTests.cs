// <copyright file="DomainModuleAuthoringGuardrailTests.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.DomainService.Tests;

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

/// <summary>
/// Epic C3 guardrail: structural checks that enforce the domain-centric authoring rule documented in the root
/// <c>CLAUDE.md</c> ("Domain-Module Authoring"). A domain module must contain only domain code; all platform
/// boilerplate is supplied by the EventStore domain-service SDK. Concretely a domain module must <b>not</b>:
/// <list type="bullet">
///   <item>ship its own <c>*.AppHost</c>, <c>*.Aspire</c>, or <c>*.ServiceDefaults</c> project (orchestration /
///   DAPR wiring / shared service config belong to the platform), or</item>
///   <item>re-declare a projection/query actor (the platform owns
///   <c>EventReplayProjectionActor</c>/<c>CachingProjectionActor</c>/<c>IProjectionActor</c>; a domain serves
///   queries via <c>IDomainQueryHandler</c>, A7).</item>
/// </list>
/// </summary>
public sealed class DomainModuleAuthoringGuardrailTests
{
    private const string CSharpStringLiteralPattern = "[$@]*\"{3,}[\\s\\S]*?\"{3,}|[$@]*\"(?:\\\\.|[^\"]|\"\")*\"";
    private const string CSharpStringExpressionPattern = "(?:" + CSharpStringLiteralPattern + "|@?\\w+(?:\\s*\\+\\s*(?:" + CSharpStringLiteralPattern + "|@?\\w+))*)";

    private static readonly string[] CanonicalDomainServiceEndpointRoutes =
    [
        "/process",
        "/replay-state",
        "/query",
        "/project",
        "/admin/operational-index-metadata",
    ];

    private static readonly Regex CustomCursorCodecDeclaration = new(
        @"\b(?:record\s+struct|class|struct|record)\s+\w+(?:\s*<[^>{}]*>)?(?:\s*\([^;{}]*\))?\s*:\s*[^{;]*\b(?:[\w.]+\.)?IQueryCursorCodec\b",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CustomHealthCheckDeclaration = new(
        @"\b(?:record\s+struct|class|struct|record)\s+\w+(?:\s*<[^>{}]*>)?(?:\s*\([^;{}]*\))?\s*:\s*[^{;]*\b(?:[\w.]+\.)?IHealthCheck\b",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CustomReadModelStoreDeclaration = new(
        @"\b(?:record\s+struct|class|struct|record)\s+\w+(?:\s*<[^>{}]*>)?(?:\s*\([^;{}]*\))?\s*:\s*[^{;]*\b(?:[\w.]+\.)?IReadModelStore\b",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NewActivitySourceDeclaration = new(
        @"\bnew\s+(?:[\w.]+\.)?ActivitySource\s*\(|\bActivitySource\b[^;\r\n=]*=\s*new\s*(?:\(|(?:[\w.]+\.)?ActivitySource\s*\()",
        RegexOptions.Compiled);

    private static readonly Regex NewMeterDeclaration = new(
        @"\bnew\s+(?:[\w.]+\.)?Meter\s*\(|\bMeter\b[^;\r\n=]*=\s*new\s*(?:\(|(?:[\w.]+\.)?Meter\s*\()",
        RegexOptions.Compiled);

    private static readonly string[] ProjectionActorInheritanceMarkers =
    [
        ": IProjectionActor",
        ", IProjectionActor",
        ": CachingProjectionActor",
        ": EventReplayProjectionActor",
    ];

    private static readonly Regex StateStoreWrapperDeclaration = new(
        @"\b(?:record\s+struct|class|struct|record)\s+\w*(?:ReadModelStore|Repository|StateStore|ProjectionStore|Persistence|Gateway)\w*(?:\s*<[^>{}]*>)?(?:\s*\([^;{}]*\))?",
        RegexOptions.Compiled);

    private static readonly string[] DaprStateAccessMarkers =
    [
        "GetStateAsync",
        "GetStateAndETagAsync",
        "GetStateEntryAsync",
        "GetBulkStateAsync",
        "QueryStateAsync",
        "SaveStateAsync",
        "SaveBulkStateAsync",
        "TryGetStateAsync",
        "TrySaveStateAsync",
        "DeleteStateAsync",
        "ExecuteStateTransactionAsync",
    ];

    private static readonly string[] ActorStateAccessMarkers =
    [
        "AddStateAsync",
        "ClearCacheAsync",
        "ContainsStateAsync",
        "GetOrAddStateAsync",
        "GetStateAsync",
        "RemoveStateAsync",
        "SaveStateAsync",
        "SetStateAsync",
        "TryAddStateAsync",
        "TryGetStateAsync",
    ];

    private static readonly string[] InteractiveUiHostForbiddenMarkers =
    [
        "[assembly: RestApi(",
        "AddMvc(",
        "AddMvcCore(",
        "AddControllers(",
        "AddControllersWithViews(",
        "MapControllers(",
        "MapControllerRoute(",
        ": Controller",
        ", Controller",
        "ControllerBase",
        "[ApiController]",
        "Hexalith.EventStore.RestApi.Generators",
    ];

    private static readonly string[] SampleProgramForbiddenNormalModeMarkers =
    [
        "DomainServiceRequestRouter",
        "AddDaprClient(",
        "AddEventStore(",
        "AddEventStoreDomainTelemetry(",
        "AddServiceDefaults(",
        "AddControllers(",
        "MapDefaultEndpoints(",
        "MapHealthChecks(",
        "MapEventStoreDomainService(",
        "MapControllers(",
        "MapSubscribeHandler(",
        "UseEventStore(",
        "UseCloudEvents(",
        "AdminOperationalIndexMetadata",
        "MapPost(\"/process\"",
        "MapPost(\"/replay-state\"",
        "MapPost(\"/query\"",
        "MapPost(\"/admin/operational-index-metadata\"",
        "MapGet(\"/health\"",
        "MapGet(\"/alive\"",
        "MapGet(\"/ready\"",
    ];

    [Fact]
    public void DomainModules_DoNotShipOwnAppHostAspireOrServiceDefaultsProject()
    {
        foreach ((string name, string path) in DomainModuleRoots())
        {
            string[] violations = Directory
                .EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
                .Where(p => !IsBuildArtifact(p))
                .Select(p => (RelativePath: Path.GetRelativePath(path, p), Reason: ProhibitedDomainPlatformProjectReason(p)))
                .Where(p => p.Reason is not null)
                .Select(p => $"{p.RelativePath} ({p.Reason})")
                .ToArray();

            violations.ShouldBeEmpty(
                $"Domain module '{name}' must not ship its own *.AppHost, *.Aspire, or *.ServiceDefaults "
                + "project — orchestration, Aspire/DAPR wiring, and service defaults are provided by the "
                + "EventStore platform (CLAUDE.md domain-centric rule). Found: " + string.Join(", ", violations));
        }
    }

    [Fact]
    public void DomainModules_DoNotReDeclareProjectionActor()
    {
        List<string> offenders = [];
        foreach ((string name, string path) in DomainModuleRoots())
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildArtifact(file) || IsTestSource(file))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                if (ProjectionActorInheritanceMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal)))
                {
                    offenders.Add($"{name}: {Path.GetFileName(file)}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "Domain modules must not re-declare a projection/query actor — the platform owns "
            + "EventReplayProjectionActor/CachingProjectionActor/IProjectionActor; serve queries via "
            + "IDomainQueryHandler (A7). Offending files: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DomainModules_DoNotReImplementPlatformOwnedStateCursorTelemetryHealthOrEndpointSeams()
    {
        List<string> offenders = [];
        foreach ((string name, string path) in DomainModuleRoots())
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildArtifact(file) || IsTestSource(file))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                string codeWithoutComments = RemoveCSharpComments(text);
                string codeWithoutCommentsOrStrings = RemoveCSharpCommentsAndStringLiterals(text);
                AddPlatformBoilerplateViolations(
                    offenders,
                    name,
                    path,
                    file,
                    codeWithoutComments,
                    codeWithoutCommentsOrStrings);
            }
        }

        offenders.ShouldBeEmpty(
            "Domain modules must use EventStore platform seams instead of re-implementing hosting "
            + "boilerplate: use AddEventStoreDomainService/UseEventStoreDomainService for canonical "
            + "endpoints, IReadModelStore + ReadModelWritePolicy for persisted read models, "
            + "IQueryCursorCodec/QueryCursorScope via AddEventStoreQueryCursorCodec for cursors, "
            + "EventStoreDomainDiagnostics/AddEventStoreDomainTelemetry for telemetry, and "
            + "AddEventStoreDomainStateStoreHealthCheck for health. Offending files: "
            + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("DaprClient client = default!; _ = client.QueryStateAsync<object>(\"store\", \"query\");")]
    [InlineData("DaprClient client = default!; _ = client?.SaveStateAsync(\"store\", \"key\", state);")]
    [InlineData("DaprClient client = default!; _ = client!.SaveStateAsync(\"store\", \"key\", state);")]
    [InlineData("_ = services.GetRequiredService<DaprClient>().SaveStateAsync(\"store\", \"key\", state);")]
    [InlineData("_ = CreateDaprClient().SaveStateAsync(\"store\", \"key\", state);")]
    public void GuardrailHelpers_DetectDaprStateAccessByReceiverShape(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        ContainsDaprStateAccess(source).ShouldBeTrue();
    }

    [Theory]
    [InlineData("IActorStateManager stateManager = default!; _ = stateManager?.GetStateAsync<object>(\"key\");")]
    [InlineData("IStateManager stateManager = default!; _ = stateManager!.SaveStateAsync();")]
    [InlineData("_ = services.GetRequiredService<IActorStateManager>().GetOrAddStateAsync(\"key\", state);")]
    public void GuardrailHelpers_DetectActorStateAccessByReceiverShape(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        ContainsActorStateAccess(source).ShouldBeTrue();
    }

    [Theory]
    [InlineData("app.MapPost(pattern: \"/process\", () => Results.Ok());", "/process")]
    [InlineData("const string Process = \"/process\", Query = \"/query\"; app.MapPost(pattern: Query, () => Results.Ok());", "/query")]
    [InlineData("const string Project = \"\"\"/project\"\"\"; app.MapPost(Project, () => Results.Ok());", "/project")]
    [InlineData("var admin = app.MapGroup(\"/admin\"); var operational = admin.MapGroup(\"/operational-index-metadata\"); operational.MapGet(\"\", () => Results.Ok());", "/admin/operational-index-metadata")]
    [InlineData("app.MapGroup(\"/admin\").MapGroup(\"/operational-index-metadata\").MapGet(\"\", () => Results.Ok());", "/admin/operational-index-metadata")]
    public void GuardrailHelpers_DetectCanonicalEndpointRouteShapes(string source, string route)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(route);

        RouteMappingSnippets(source, route).ShouldNotBeEmpty($"Expected route {route} to be detected in snippet: {source}");
    }

    [Fact]
    public void GuardrailHelpers_TenantsProjectAllowanceRequiresDispatcherCodeReference()
    {
        const string root = "/repo/references/Hexalith.Tenants/src/Hexalith.Tenants";
        string file = Path.Combine(root, "Program.cs");

        IsAllowedProjectPreMap(
            "Tenants",
            root,
            file,
            "/project",
            "app.MapPost(\"/project\", () => \"ProjectionDispatcher\");",
            0,
            "app.MapPost(\"/project\", () => \"ProjectionDispatcher\");").ShouldBeFalse();

        IsAllowedProjectPreMap(
            "Tenants",
            root,
            file,
            "/project",
            "app.MapPost(\"/project\", () => new ProjectionDispatcher().DispatchAsync(request));",
            0,
            "app.MapPost(\"/project\", () => new ProjectionDispatcher().DispatchAsync(request));").ShouldBeTrue();
    }

    [Fact]
    public void SampleReferenceModule_ReferencesOnlyTheDomainServiceSdkAndDomainContracts()
    {
        string csproj = SampleDomainProjectPath();
        File.Exists(csproj).ShouldBeTrue($"Expected the reference Sample project at {csproj}.");

        string[] references = Regex
            .Matches(File.ReadAllText(csproj), "ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => Path.GetFileName(m.Groups[1].Value.Replace('\\', '/')))
            .ToArray();

        references.ShouldContain("Hexalith.EventStore.DomainService.csproj", "The Sample must reference the domain-service SDK.");

        string[] unexpectedReferences = references
            .Where(r => r != "Hexalith.EventStore.DomainService.csproj"
                     && r != "Hexalith.EventStore.Sample.Contracts.csproj")
            .ToArray();

        unexpectedReferences.ShouldBeEmpty(
            "The reference Sample domain module may reference only Hexalith.EventStore.DomainService and "
            + "its own domain contracts library. Client/ServiceDefaults/platform Contracts flow through the "
            + "SDK, and external API/UI hosts own REST or UI wiring. Found unexpected references: "
            + string.Join(", ", unexpectedReferences));
    }

    [Fact]
    public void SampleReferenceModule_UsesCompiledContractsLibraryWithoutGeneratedApiHostLeakage()
    {
        string csproj = SampleDomainProjectPath();
        File.Exists(csproj).ShouldBeTrue($"Expected the reference Sample project at {csproj}.");

        XDocument project = XDocument.Load(csproj);
        string[] references = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(static element => Path.GetFileName(((string?)element.Attribute("Include"))?.Replace('\\', '/') ?? string.Empty))
            .ToArray();

        references.ShouldContain(
            "Hexalith.EventStore.Sample.Contracts.csproj",
            "The Sample domain service should share the compiled contracts library with Sample.Api and BlazorUI.");
        references.ShouldNotContain(
            "Hexalith.EventStore.RestApi.Generators.csproj",
            "Generated REST controller analyzers belong only in dedicated external API hosts.");
        string projectText = File.ReadAllText(csproj);
        projectText
            .Contains("Hexalith.EventStore.RestApi.Generators", StringComparison.Ordinal)
            .ShouldBeFalse("Generated REST controller analyzers belong only in dedicated external API hosts.");

        string[] linkedContracts = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Compile", StringComparison.Ordinal))
            .Select(static element => ((string?)element.Attribute("Include"))?.Replace('\\', '/') ?? string.Empty)
            .Where(static include => include.Contains("Hexalith.EventStore.Sample.Contracts", StringComparison.Ordinal)
                || include.Contains("/Counter/Commands/", StringComparison.Ordinal)
                || include.Contains("/Counter/Queries/", StringComparison.Ordinal))
            .ToArray();

        linkedContracts.ShouldBeEmpty(
            "The Sample domain service must not compile-link duplicate contract source files; use the "
            + "contracts project reference so aggregate handlers, UI metadata, and generated API routes "
            + "share one contract identity.");

        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.GetDirectoryName(csproj)!, "*.*", SearchOption.AllDirectories)
                .Where(static file => !IsBuildArtifact(file)
                    && (file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".csproj", StringComparison.Ordinal)))
                .Select(File.ReadAllText));

        source.ShouldNotContain("[assembly: RestApi(");
        source.ShouldNotContain("AddControllers(");
        source.ShouldNotContain("MapControllers(");
        source.ShouldNotContain("ControllerBase");
        source.ShouldNotContain("[ApiController]");
    }

    [Fact]
    public void SampleReferenceModule_DoesNotReferenceDaprPackagesDirectly()
    {
        string csproj = SampleDomainProjectPath();
        File.Exists(csproj).ShouldBeTrue($"Expected the reference Sample project at {csproj}.");

        string[] daprReferences = XDocument
            .Load(csproj)
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Select(static element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .Where(package => package.StartsWith("Dapr.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        daprReferences.ShouldBeEmpty(
            "The reference Sample domain module must not reference Dapr.* packages directly. DAPR hosting "
            + "dependencies flow through Hexalith.EventStore.DomainService so domain authors do not own "
            + "platform wiring. Found direct references: " + string.Join(", ", daprReferences));
    }

    [Fact]
    public void DomainModules_DoNotDeclareLegacyNonAggregateDomainProcessors()
    {
        Regex processorDeclaration = new(
            @"\b(class|record)\s+\w+(?:\s*<[^>{}]*>)?\s*:[^{;]*\b(?:[\w.]+\.)?IDomainProcessor\b",
            RegexOptions.Compiled | RegexOptions.Singleline);
        string[] offenders = DomainModuleRoots()
            .SelectMany(root => Directory
                .EnumerateFiles(root.Path, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildArtifact(file) && !IsTestSource(file) && processorDeclaration.IsMatch(File.ReadAllText(file)))
                .Select(file => $"{root.Name}: {Path.GetRelativePath(root.Path, file)}"))
            .ToArray();

        offenders.ShouldBeEmpty(
            "Domain modules must prove convention-discovered EventStoreAggregate<TState> handlers, not "
            + "legacy hand-written IDomainProcessor paths. Offending files: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void SampleProgram_UsesCanonicalDomainServiceHostWithoutNormalModeBoilerplate()
    {
        string program = SampleDomainProgramPath();
        File.Exists(program).ShouldBeTrue($"Expected the reference Sample host at {program}.");

        string text = File.ReadAllText(program);
        text.ShouldContain("builder.AddEventStoreDomainService();");
        text.ShouldContain("app.UseEventStoreDomainService();");

        string[] forbiddenMarkers = SampleProgramForbiddenNormalModeMarkers
            .Where(marker => text.Contains(marker, StringComparison.Ordinal))
            .ToArray();

        forbiddenMarkers.ShouldBeEmpty(
            "The reference Sample host must keep normal-mode platform wiring inside "
            + "Hexalith.EventStore.DomainService. Program.cs may keep the opt-in malformed /project fault "
            + "route, but must not hand-map routers, default endpoints, or operational metadata. Found: "
            + string.Join(", ", forbiddenMarkers));
    }

    [Fact]
    public void DomainModuleRoots_UsesInitializedTenantsDomainServiceRoot()
    {
        string root = FindRepositoryRoot();
        string tenantsDomainRoot = Path.Combine(root, "references", "Hexalith.Tenants", "src", "Hexalith.Tenants");
        if (!Directory.Exists(tenantsDomainRoot))
        {
            return;
        }

        DomainModuleRoots()
            .ShouldContain(
                rootInfo => rootInfo.Name == "Tenants"
                         && string.Equals(rootInfo.Path, tenantsDomainRoot, StringComparison.Ordinal),
                "When the Tenants submodule is initialized, guardrails must scan only the Tenants domain-service root under references/Hexalith.Tenants/src/Hexalith.Tenants.");
    }

    [Fact]
    public void InteractiveUiHosts_DoNotHostGeneratedOrHandWrittenCommandQueryControllers()
    {
        List<string> offenders = [];
        foreach ((string name, string path) in InteractiveUiHostRoots())
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (IsBuildArtifact(file) || !IsSourceOrProjectFile(file))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                foreach (string marker in InteractiveUiHostForbiddenMarkers)
                {
                    if (text.Contains(marker, StringComparison.Ordinal))
                    {
                        offenders.Add($"{name}: {Path.GetRelativePath(path, file)} contains '{marker}'");
                    }
                }
            }
        }

        offenders.ShouldBeEmpty(
            "Interactive UI hosts must consume EventStore Client libraries and must not host generated or "
            + "hand-written per-message MVC command/query controllers. Put generated REST controllers in a "
            + "dedicated external API host instead. Offending files: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Enumerates the domain-module source roots to validate. The reference Sample is always present; the
    /// Tenants submodule is included only when initialized (so CI runs that skip submodules do not fail).
    /// </summary>
    private static IEnumerable<(string Name, string Path)> DomainModuleRoots()
    {
        string root = FindRepositoryRoot();

        yield return ("Sample", Path.Combine(root, "samples", "Hexalith.EventStore.Sample"));

        string tenants = Path.Combine(root, "references", "Hexalith.Tenants", "src", "Hexalith.Tenants");
        if (Directory.Exists(tenants))
        {
            yield return ("Tenants", tenants);
        }
    }

    private static IEnumerable<(string Name, string Path)> InteractiveUiHostRoots()
    {
        string root = FindRepositoryRoot();

        string[] roots =
        [
            Path.Combine(root, "samples", "Hexalith.EventStore.Sample.BlazorUI"),
            Path.Combine(root, "src", "Hexalith.EventStore.Admin.UI"),
            Path.Combine(root, "references", "Hexalith.Tenants", "src", "Hexalith.Tenants.UI"),
        ];

        foreach (string path in roots.Where(Directory.Exists))
        {
            yield return (Path.GetFileName(path), path);
        }
    }

    private static void AddPlatformBoilerplateViolations(
        List<string> offenders,
        string name,
        string rootPath,
        string file,
        string codeWithStrings,
        string code)
    {
        AddViolationIf(offenders, CustomCursorCodecDeclaration.IsMatch(code), name, rootPath, file, "implements IQueryCursorCodec");
        AddViolationIf(offenders, CustomReadModelStoreDeclaration.IsMatch(code), name, rootPath, file, "implements IReadModelStore");
        AddViolationIf(offenders, CustomHealthCheckDeclaration.IsMatch(code), name, rootPath, file, "implements IHealthCheck");
        AddViolationIf(offenders, NewActivitySourceDeclaration.IsMatch(code), name, rootPath, file, "declares a new ActivitySource");
        AddViolationIf(offenders, NewMeterDeclaration.IsMatch(code), name, rootPath, file, "declares a new Meter");

        bool usesDaprStateAccess = ContainsDaprStateAccess(code);
        bool usesActorStateAccess = ContainsActorStateAccess(code);
        AddViolationIf(
            offenders,
            StateStoreWrapperDeclaration.IsMatch(code)
            && (usesDaprStateAccess || usesActorStateAccess),
            name,
            rootPath,
            file,
            "declares a custom read-model/state-store wrapper");
        AddViolationIf(
            offenders,
            usesDaprStateAccess,
            name,
            rootPath,
            file,
            "uses custom DAPR state access");
        AddViolationIf(
            offenders,
            usesActorStateAccess,
            name,
            rootPath,
            file,
            "uses custom actor state access");

        foreach (string route in CanonicalDomainServiceEndpointRoutes)
        {
            foreach ((int index, string snippet) in RouteMappingSnippets(codeWithStrings, route))
            {
                if (!IsAllowedProjectPreMap(name, rootPath, file, route, codeWithStrings, index, snippet))
                {
                    AddViolation(offenders, name, rootPath, file, $"hand-maps canonical SDK endpoint {route}");
                }
            }
        }

        AddViolationIf(offenders, code.Contains("MapEventStoreDomainService(", StringComparison.Ordinal), name, rootPath, file, "calls lower-level MapEventStoreDomainService");
        AddViolationIf(offenders, code.Contains("MapDefaultEndpoints(", StringComparison.Ordinal), name, rootPath, file, "maps service-default endpoints");
        AddViolationIf(offenders, code.Contains("MapHealthChecks(", StringComparison.Ordinal), name, rootPath, file, "maps health checks");
    }

    private static void AddViolationIf(
        List<string> offenders,
        bool condition,
        string name,
        string rootPath,
        string file,
        string reason)
    {
        if (condition)
        {
            AddViolation(offenders, name, rootPath, file, reason);
        }
    }

    private static void AddViolation(
        List<string> offenders,
        string name,
        string rootPath,
        string file,
        string reason)
        => offenders.Add($"{name}: {Path.GetRelativePath(rootPath, file)} ({reason})");

    private static bool IsAllowedProjectPreMap(
        string name,
        string rootPath,
        string file,
        string route,
        string text,
        int mappingIndex,
        string snippet)
    {
        if (!string.Equals(route, "/project", StringComparison.Ordinal))
        {
            return false;
        }

        string relativePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
        return (string.Equals(name, "Tenants", StringComparison.Ordinal)
                && string.Equals(relativePath, "Program.cs", StringComparison.Ordinal)
                && RemoveCSharpCommentsAndStringLiterals(snippet).Contains("ProjectionDispatcher", StringComparison.Ordinal))
            || (string.Equals(name, "Sample", StringComparison.Ordinal)
                && string.Equals(relativePath, "Program.cs", StringComparison.Ordinal)
                && IsInsideIfBlock(text, mappingIndex, "malformedProjectionResponse"));
    }

    private static string? ProhibitedDomainPlatformProjectReason(string projectPath)
    {
        string projectName = Path.GetFileNameWithoutExtension(projectPath) ?? string.Empty;
        if (projectName.EndsWith(".AppHost", StringComparison.Ordinal))
        {
            return "own *.AppHost project";
        }

        if (projectName.EndsWith(".Aspire", StringComparison.Ordinal))
        {
            return "own *.Aspire project";
        }

        if (projectName.EndsWith(".ServiceDefaults", StringComparison.Ordinal))
        {
            return "own *.ServiceDefaults project";
        }

        string text = File.ReadAllText(projectPath);
        if (text.Contains("Aspire.AppHost.Sdk", StringComparison.Ordinal)
            || text.Contains("Aspire.Hosting.AppHost", StringComparison.Ordinal))
        {
            return "AppHost-style Aspire SDK/project content";
        }

        if (text.Contains("<PackageReference Include=\"Aspire.Hosting", StringComparison.Ordinal)
            || text.Contains("<PackageReference Include=\"CommunityToolkit.Aspire.Hosting", StringComparison.Ordinal))
        {
            return "Aspire orchestration package reference";
        }

        return null;
    }

    private static bool IsBuildArtifact(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool IsTestSource(string path)
        => path.Contains(".Tests", StringComparison.Ordinal);

    private static bool IsSourceOrProjectFile(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsActorStateAccess(string text)
    {
        string[] receivers = TypedIdentifiers(text, @"(?:[\w.]+\.)?(?:IActorStateManager|IStateManager)")
            .Concat(["StateManager"])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return ContainsInvocationOnAnyReceiver(text, receivers, ActorStateAccessMarkers)
            || ContainsGenericServiceInvocation(text, @"(?:[\w.]+\.)?(?:IActorStateManager|IStateManager)", ActorStateAccessMarkers)
            || ContainsInvocationOnCallResult(text, ActorStateAccessMarkers);
    }

    private static bool ContainsDaprStateAccess(string text)
    {
        string[] receivers = TypedIdentifiers(text, @"(?:[\w.]+\.)?DaprClient")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return ContainsInvocationOnAnyReceiver(text, receivers, DaprStateAccessMarkers)
            || ContainsGenericServiceInvocation(text, @"(?:[\w.]+\.)?DaprClient", DaprStateAccessMarkers)
            || ContainsInvocationOnCallResult(text, DaprStateAccessMarkers);
    }

    private static bool ContainsGenericServiceInvocation(string text, string typePattern, IEnumerable<string> methodNames)
    {
        foreach (string methodName in methodNames)
        {
            if (Regex.IsMatch(
                text,
                $@"\b(?:GetRequiredService|GetService)<\s*{typePattern}\s*>\s*\([^)]*\)\s*(?:!\s*|\?\s*)?\.\s*{Regex.Escape(methodName)}(?:\s*<[^>()]+>)?\s*\(",
                RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInvocationOnAnyReceiver(string text, IEnumerable<string> receivers, IEnumerable<string> methodNames)
    {
        foreach (string receiver in receivers)
        {
            foreach (string methodName in methodNames)
            {
                if (Regex.IsMatch(
                    text,
                    $@"\b{Regex.Escape(receiver)}\s*(?:!\s*|\?\s*)?\.\s*{Regex.Escape(methodName)}(?:\s*<[^>()]+>)?\s*\(",
                    RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsInvocationOnCallResult(string text, IEnumerable<string> methodNames)
    {
        foreach (string methodName in methodNames)
        {
            if (Regex.IsMatch(
                text,
                $@"\)\s*(?:!\s*|\?\s*)?\.\s*{Regex.Escape(methodName)}(?:\s*<[^>()]+>)?\s*\(",
                RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }

    private static string? DecodeStringLiteral(string literal)
    {
        string trimmed = literal.Trim();
        int quoteIndex = trimmed.IndexOf('"', StringComparison.Ordinal);
        if (quoteIndex < 0)
        {
            return null;
        }

        string prefix = trimmed[..quoteIndex];
        string quoted = trimmed[quoteIndex..];
        int openingQuoteCount = CountConsecutiveQuotes(quoted, 0);
        if (openingQuoteCount >= 3)
        {
            int closingQuoteCount = CountTrailingQuotes(quoted);
            int delimiterLength = Math.Min(openingQuoteCount, closingQuoteCount);
            if (delimiterLength < 3 || quoted.Length < delimiterLength * 2)
            {
                return null;
            }

            string rawContent = quoted[delimiterLength..^delimiterLength];
            return prefix.Contains('$', StringComparison.Ordinal) && rawContent.Contains('{', StringComparison.Ordinal)
                ? null
                : rawContent;
        }

        if (quoted.Length < 2)
        {
            return null;
        }

        string content = quoted[1..^1];
        if (prefix.Contains('$', StringComparison.Ordinal) && content.Contains('{', StringComparison.Ordinal))
        {
            return null;
        }

        return prefix.Contains('@', StringComparison.Ordinal)
            ? content.Replace("\"\"", "\"", StringComparison.Ordinal)
            : Regex.Unescape(content);
    }

    private static int CountConsecutiveQuotes(string text, int startIndex)
    {
        int count = 0;
        while (startIndex + count < text.Length && text[startIndex + count] == '"')
        {
            count++;
        }

        return count;
    }

    private static int CountTrailingQuotes(string text)
    {
        int count = 0;
        for (int i = text.Length - 1; i >= 0 && text[i] == '"'; i--)
        {
            count++;
        }

        return count;
    }

    private static string ExtractStatementSnippet(string text, int startIndex)
    {
        int semicolonIndex = text.IndexOf(';', startIndex);
        int length = semicolonIndex < 0
            ? Math.Min(320, text.Length - startIndex)
            : semicolonIndex - startIndex + 1;
        return text.Substring(startIndex, length);
    }

    private static int FindMatchingBrace(string text, int openBraceIndex)
    {
        int depth = 0;
        for (int i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static IEnumerable<(int Index, string Snippet, string Route)> FirstRouteArgumentCalls(
        string text,
        string methodPattern,
        IReadOnlyDictionary<string, string> stringValues,
        bool allowEmpty)
    {
        foreach ((int index, _, string route) in RouteCalls(text, methodPattern, stringValues))
        {
            if (allowEmpty || !string.IsNullOrWhiteSpace(route))
            {
                yield return (index, ExtractStatementSnippet(text, index), route);
            }
        }
    }

    private static IEnumerable<(int Index, string Snippet)> FirstRouteArgumentCallsForRoute(
        string text,
        string methodPattern,
        string route,
        IReadOnlyDictionary<string, string> stringValues)
    {
        foreach ((int index, string snippet, string value) in FirstRouteArgumentCalls(text, methodPattern, stringValues, allowEmpty: true))
        {
            if (string.Equals(value, route, StringComparison.Ordinal))
            {
                yield return (index, snippet);
            }
        }
    }

    private static IEnumerable<(string Name, string Prefix)> GroupRouteAssignments(
        string text,
        IReadOnlyDictionary<string, string> stringValues)
    {
        Match[] assignments = Regex
            .Matches(
                text,
                $@"\b(?<name>@?\w+)\s*=\s*(?:(?<receiver>@?\w+)\s*\.\s*)?MapGroup\s*\(\s*(?:(?:@?\w+)\s*:\s*)?(?<argument>{CSharpStringExpressionPattern})",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Cast<Match>()
            .ToArray();
        Dictionary<string, string> groupPrefixes = new(StringComparer.Ordinal);

        for (int pass = 0; pass < assignments.Length; pass++)
        {
            foreach (Match match in assignments)
            {
                string name = match.Groups["name"].Value;
                if (groupPrefixes.ContainsKey(name))
                {
                    continue;
                }

                string? prefix = ResolveStringExpression(match.Groups["argument"].Value, stringValues);
                if (prefix is null)
                {
                    continue;
                }

                string receiver = match.Groups["receiver"].Value;
                if (!string.IsNullOrWhiteSpace(receiver) && groupPrefixes.TryGetValue(receiver, out string? receiverPrefix))
                {
                    prefix = CombineRoutes(receiverPrefix, prefix);
                }
                else
                {
                    prefix = NormalizeRoute(prefix);
                }

                groupPrefixes.Add(name, prefix);
            }
        }

        return groupPrefixes.Select(static pair => (pair.Key, pair.Value));
    }

    private static IEnumerable<(int Index, string Method, string Route)> RouteCalls(
        string text,
        string methodPattern,
        IReadOnlyDictionary<string, string> stringValues)
    {
        Regex callPattern = new(
            $@"\b(?<method>{methodPattern})\s*\(\s*(?:(?:@?\w+)\s*:\s*)?(?<argument>{CSharpStringExpressionPattern})",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        foreach (Match match in callPattern.Matches(text).Cast<Match>())
        {
            string? route = ResolveStringExpression(match.Groups["argument"].Value, stringValues);
            if (route is not null)
            {
                yield return (match.Index, match.Groups["method"].Value, route);
            }
        }
    }

    private static bool IsInsideIfBlock(string text, int index, string conditionIdentifier)
    {
        string syntaxText = ReplaceCSharpStringLiteralsWithSpaces(text);
        int ifIndex = syntaxText.LastIndexOf($"if ({conditionIdentifier})", index, StringComparison.Ordinal);
        if (ifIndex < 0)
        {
            return false;
        }

        int openBraceIndex = syntaxText.IndexOf('{', ifIndex);
        if (openBraceIndex < 0 || openBraceIndex > index)
        {
            return false;
        }

        int closeBraceIndex = FindMatchingBrace(syntaxText, openBraceIndex);
        return closeBraceIndex > index;
    }

    private static string NormalizeRoute(string route)
        => CombineRoutes(route);

    private static string CombineRoutes(params string[] routeParts)
    {
        string[] segments = routeParts
            .Select(static part => part.Trim().Trim('/').Trim())
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return "/" + string.Join("/", segments);
    }

    private static string RemoveCSharpComments(string text)
    {
        StringBuilder builder = new(text.Length);
        int currentIndex = 0;
        foreach (Match stringLiteral in Regex.Matches(
            text,
            CSharpStringLiteralPattern,
            RegexOptions.CultureInvariant | RegexOptions.Singleline).Cast<Match>())
        {
            builder.Append(RemoveCommentsFromNonStringText(text[currentIndex..stringLiteral.Index]));
            builder.Append(stringLiteral.Value);
            currentIndex = stringLiteral.Index + stringLiteral.Length;
        }

        builder.Append(RemoveCommentsFromNonStringText(text[currentIndex..]));
        return builder.ToString();
    }

    private static string RemoveCSharpCommentsAndStringLiterals(string text)
        => Regex.Replace(
            RemoveCSharpComments(text),
            CSharpStringLiteralPattern,
            "\"\"",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static string RemoveCommentsFromNonStringText(string text)
        => Regex.Replace(
            text,
            @"//.*?$|/\*.*?\*/",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);

    private static string ReplaceCSharpStringLiteralsWithSpaces(string text)
        => Regex.Replace(
            text,
            CSharpStringLiteralPattern,
            match => new string(' ', match.Length),
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static IEnumerable<(int Index, string Snippet)> RouteMappingSnippets(string text, string route)
    {
        IReadOnlyDictionary<string, string> stringValues = StringAssignments(text);
        HashSet<int> emittedIndexes = [];

        foreach ((int index, string snippet) in FirstRouteArgumentCallsForRoute(text, "Map(?!Group\\b)\\w*", route, stringValues))
        {
            if (emittedIndexes.Add(index))
            {
                yield return (index, snippet);
            }
        }

        foreach ((int index, string snippet) in ChainedGroupRouteMappingSnippets(text, route, stringValues))
        {
            if (emittedIndexes.Add(index))
            {
                yield return (index, snippet);
            }
        }

        foreach ((int index, string snippet) in OrderedInlineGroupRouteMappingSnippets(text, route, stringValues))
        {
            if (emittedIndexes.Add(index))
            {
                yield return (index, snippet);
            }
        }
    }

    private static IEnumerable<(int Index, string Snippet)> ChainedGroupRouteMappingSnippets(
        string text,
        string route,
        IReadOnlyDictionary<string, string> stringValues)
    {
        foreach ((int index, string snippet) in InlineGroupRouteMappingSnippets(text, route, stringValues))
        {
            yield return (index, snippet);
        }

        foreach ((int index, string snippet, string prefix) in FirstRouteArgumentCalls(text, "MapGroup", stringValues, allowEmpty: true))
        {
            foreach ((int childIndex, string childSnippet, string childRoute) in FirstRouteArgumentCalls(snippet, "Map(?!Group\\b)\\w*", stringValues, allowEmpty: true))
            {
                if (childIndex == 0)
                {
                    continue;
                }

                if (string.Equals(CombineRoutes(prefix, childRoute), route, StringComparison.Ordinal))
                {
                    yield return (index + childIndex, childSnippet);
                }
            }
        }

        foreach ((string name, string prefix) in GroupRouteAssignments(text, stringValues))
        {
            foreach ((int index, string snippet, string childRoute) in AssignedGroupChildRouteCalls(text, name, stringValues))
            {
                if (string.Equals(CombineRoutes(prefix, childRoute), route, StringComparison.Ordinal))
                {
                    yield return (index, snippet);
                }
            }
        }
    }

    private static IEnumerable<(int Index, string Snippet, string Route)> AssignedGroupChildRouteCalls(
        string text,
        string groupName,
        IReadOnlyDictionary<string, string> stringValues)
    {
        Regex callPattern = new(
            $@"\b{Regex.Escape(groupName)}\s*\.\s*Map(?!Group\b)\w*\s*\(\s*(?:(?:@?\w+)\s*:\s*)?(?<argument>{CSharpStringExpressionPattern})",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        foreach (Match match in callPattern.Matches(text).Cast<Match>())
        {
            string? route = ResolveStringExpression(match.Groups["argument"].Value, stringValues);
            if (route is not null)
            {
                yield return (match.Index, ExtractStatementSnippet(text, match.Index), route);
            }
        }
    }

    private static IEnumerable<(int Index, string Snippet)> InlineGroupRouteMappingSnippets(
        string text,
        string route,
        IReadOnlyDictionary<string, string> stringValues)
    {
        foreach ((int index, string snippet, _) in FirstRouteArgumentCalls(text, "MapGroup", stringValues, allowEmpty: true))
        {
            string prefix = string.Empty;
            foreach ((int callIndex, string method, string routePart) in RouteCalls(snippet, "Map\\w*", stringValues))
            {
                if (method.EndsWith("MapGroup", StringComparison.Ordinal))
                {
                    prefix = CombineRoutes(prefix, routePart);
                    continue;
                }

                if (string.Equals(CombineRoutes(prefix, routePart), route, StringComparison.Ordinal))
                {
                    yield return (index + callIndex, ExtractStatementSnippet(snippet, callIndex));
                }
            }
        }
    }

    private static IEnumerable<(int Index, string Snippet)> OrderedInlineGroupRouteMappingSnippets(
        string text,
        string route,
        IReadOnlyDictionary<string, string> stringValues)
    {
        (int Index, string Method, string Route)[] calls = RouteCalls(text, "Map\\w*", stringValues)
            .OrderBy(static call => call.Index)
            .ToArray();

        for (int start = 0; start < calls.Length; start++)
        {
            if (!calls[start].Method.EndsWith("MapGroup", StringComparison.Ordinal))
            {
                continue;
            }

            int statementEnd = text.IndexOf(';', calls[start].Index);
            if (statementEnd < 0)
            {
                statementEnd = text.Length - 1;
            }

            string prefix = string.Empty;
            for (int current = start; current < calls.Length && calls[current].Index <= statementEnd; current++)
            {
                if (calls[current].Method.EndsWith("MapGroup", StringComparison.Ordinal))
                {
                    prefix = CombineRoutes(prefix, calls[current].Route);
                    continue;
                }

                if (string.Equals(CombineRoutes(prefix, calls[current].Route), route, StringComparison.Ordinal))
                {
                    yield return (calls[current].Index, ExtractStatementSnippet(text, calls[current].Index));
                }
            }
        }
    }

    private static string? ResolveStringExpression(string expression, IReadOnlyDictionary<string, string> stringValues)
    {
        StringBuilder builder = new();
        bool expectValue = true;
        int index = 0;
        while (index < expression.Length)
        {
            while (index < expression.Length && char.IsWhiteSpace(expression[index]))
            {
                index++;
            }

            if (index >= expression.Length)
            {
                break;
            }

            if (!expectValue)
            {
                if (expression[index] != '+')
                {
                    return null;
                }

                expectValue = true;
                index++;
                continue;
            }

            Match literal = Regex.Match(
                expression[index..],
                $"^{CSharpStringLiteralPattern}",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
            if (literal.Success)
            {
                string? value = DecodeStringLiteral(literal.Value);
                if (value is null)
                {
                    return null;
                }

                builder.Append(value);
                index += literal.Length;
                expectValue = false;
                continue;
            }

            Match identifier = Regex.Match(expression[index..], "^@?\\w+", RegexOptions.CultureInvariant);
            if (identifier.Success && stringValues.TryGetValue(identifier.Value, out string? identifierValue))
            {
                builder.Append(identifierValue);
                index += identifier.Length;
                expectValue = false;
                continue;
            }

            return null;
        }

        return expectValue ? null : builder.ToString();
    }

    private static IReadOnlyDictionary<string, string> StringAssignments(string text)
    {
        Match[] declarations = Regex
            .Matches(
                text,
                @"\b(?:(?:private|protected|internal|public|static|readonly|const)\s+)*(?:string|var)\s+(?<declarators>[^;]+);",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Cast<Match>()
            .ToArray();
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        for (int pass = 0; pass < declarations.Length; pass++)
        {
            foreach (Match declaration in declarations)
            {
                foreach ((string name, string expression) in StringDeclarators(declaration.Groups["declarators"].Value))
                {
                    if (values.ContainsKey(name))
                    {
                        continue;
                    }

                    string? value = ResolveStringExpression(expression, values);
                    if (value is not null)
                    {
                        values.Add(name, value);
                    }
                }
            }
        }

        return values;
    }

    private static IEnumerable<(string Name, string Expression)> StringDeclarators(string declarators)
    {
        Regex declaratorPattern = new(
            $@"(?:^|,)\s*(?<name>@?\w+)\s*=\s*(?<expression>(?:{CSharpStringLiteralPattern}|[^,;])+)",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        return declaratorPattern
            .Matches(declarators)
            .Cast<Match>()
            .Select(static match => (match.Groups["name"].Value, match.Groups["expression"].Value.Trim()));
    }

    private static IEnumerable<string> TypedIdentifiers(string text, string typePattern)
    {
        Regex declarationPattern = new(
            $@"\b{typePattern}(?:\s*\?)?\s+(?<name>@?\w+)\b",
            RegexOptions.CultureInvariant);

        return declarationPattern
            .Matches(text)
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value);
    }

    private static string SampleDomainProjectPath()
        => Path.Combine(FindRepositoryRoot(), "samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj");

    private static string SampleDomainProgramPath()
        => Path.Combine(FindRepositoryRoot(), "samples", "Hexalith.EventStore.Sample", "Program.cs");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.DomainService")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }
}
