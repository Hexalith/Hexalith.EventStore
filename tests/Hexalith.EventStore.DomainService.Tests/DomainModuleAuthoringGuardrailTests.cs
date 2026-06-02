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
    public void SampleReferenceModule_ReferencesOnlyTheDomainServiceSdk()
    {
        string csproj = Path.Combine(
            FindRepositoryRoot(), "samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj");
        File.Exists(csproj).ShouldBeTrue($"Expected the reference Sample project at {csproj}.");

        string[] references = Regex
            .Matches(File.ReadAllText(csproj), "ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => Path.GetFileName(m.Groups[1].Value.Replace('\\', '/')))
            .ToArray();

        references.ShouldNotBeEmpty("The Sample must reference the domain-service SDK.");
        references.ShouldAllBe(
            r => r == "Hexalith.EventStore.DomainService.csproj",
            "The reference Sample domain module must reference ONLY Hexalith.EventStore.DomainService "
            + "(Client/ServiceDefaults/Contracts flow transitively) — proving the zero-boilerplate authoring "
            + "model. Found references: " + string.Join(", ", references));
    }

    /// <summary>
    /// Enumerates the domain-module source roots to validate. The reference Sample is always present; the
    /// Tenants submodule is included only when initialized (so CI runs that skip submodules do not fail).
    /// </summary>
    private static IEnumerable<(string Name, string Path)> DomainModuleRoots()
    {
        string root = FindRepositoryRoot();

        yield return ("Sample", Path.Combine(root, "samples", "Hexalith.EventStore.Sample"));

        string tenants = Path.Combine(root, "Hexalith.Tenants");
        if (Directory.Exists(Path.Combine(tenants, "src")))
        {
            yield return ("Tenants", tenants);
        }
    }

    private static bool IsBuildArtifact(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool IsTestSource(string path)
        => path.Contains(".Tests", StringComparison.Ordinal);

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
