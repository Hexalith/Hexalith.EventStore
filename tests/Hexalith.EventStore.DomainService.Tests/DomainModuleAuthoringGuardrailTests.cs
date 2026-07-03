// <copyright file="DomainModuleAuthoringGuardrailTests.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.DomainService.Tests;

using System.Text.RegularExpressions;

using Shouldly;

/// <summary>
/// Epic C3 guardrail: structural checks that enforce the domain-centric authoring rule documented in the root
/// <c>CLAUDE.md</c> ("Domain-Module Authoring"). A domain module must contain only domain code; all platform
/// boilerplate is supplied by the EventStore domain-service SDK. Concretely a domain module must <b>not</b>:
/// <list type="bullet">
///   <item>ship its own <c>*.Aspire</c> or <c>*.ServiceDefaults</c> project (DAPR wiring / shared service config
///   belong to the platform), or</item>
///   <item>re-declare a projection/query actor (the platform owns
///   <c>EventReplayProjectionActor</c>/<c>CachingProjectionActor</c>/<c>IProjectionActor</c>; a domain serves
///   queries via <c>IDomainQueryHandler</c>, A7).</item>
/// </list>
/// Per the B4 direction change, a domain module MAY keep a thin <c>*.AppHost</c> that consumes the platform
/// Aspire extensions (<c>AddHexalithEventStore</c> + <c>AddEventStoreDomainModule</c>), so AppHost ownership is
/// intentionally not flagged here.
/// </summary>
public sealed class DomainModuleAuthoringGuardrailTests
{
    private static readonly string[] ProjectionActorInheritanceMarkers =
    [
        ": IProjectionActor",
        ", IProjectionActor",
        ": CachingProjectionActor",
        ": EventReplayProjectionActor",
    ];

    private static readonly string[] InteractiveUiHostForbiddenMarkers =
    [
        "[assembly: RestApi(",
        "AddControllers(",
        "MapControllers(",
        "ControllerBase",
        "[ApiController]",
        "Hexalith.EventStore.RestApi.Generators",
    ];

    [Fact]
    public void DomainModules_DoNotShipOwnAspireOrServiceDefaultsProject()
    {
        foreach ((string name, string path) in DomainModuleRoots())
        {
            string[] violations = Directory
                .EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
                .Where(p => !IsBuildArtifact(p))
                .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                .Where(n => n.EndsWith(".Aspire", StringComparison.Ordinal)
                         || n.EndsWith(".ServiceDefaults", StringComparison.Ordinal))
                .ToArray();

            violations.ShouldBeEmpty(
                $"Domain module '{name}' must not ship its own *.Aspire or *.ServiceDefaults project — that "
                + "boilerplate is provided by the EventStore platform (CLAUDE.md domain-centric rule). Found: "
                + string.Join(", ", violations));
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
    public void SampleReferenceModule_ReferencesOnlyTheDomainServiceSdkAndDomainContracts()
    {
        string csproj = Path.Combine(
            FindRepositoryRoot(), "samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj");
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
