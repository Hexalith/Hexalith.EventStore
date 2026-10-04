using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies declared checkout layouts and source repository ownership fail closed.
/// </summary>
public sealed class PackagingRepositoryPathsTests
{
    /// <summary>
    /// Verifies standalone, umbrella sibling, and containing-domain layouts.
    /// </summary>
    /// <param name="layout">The declared checkout layout.</param>
    [Theory]
    [InlineData("standalone")]
    [InlineData("umbrella-sibling")]
    [InlineData("umbrella-root-submodule")]
    [InlineData("umbrella-no-origin")]
    [InlineData("containing-tenants")]
    public void DeclaredLayoutsResolveIntendedRepositories(string layout)
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            string host = Path.Combine(temporary, "workspace");
            string owner = layout == "standalone" ? host
                : Path.Combine(host, layout == "umbrella-root-submodule" ? "Hexalith.EventStore" : "references/Hexalith.EventStore");
            string dependencyName = layout == "containing-tenants" ? "Hexalith.Tenants" : "Hexalith.Builds";
            string dependency = layout == "containing-tenants" ? host : Path.Combine(host, "references", dependencyName);
            InitializeRepository(host, layout == "standalone" ? "Hexalith.EventStore" : "Hexalith.Tenants");
            if (layout == "umbrella-no-origin")
            {
                Git(host, "remote", "remove", "origin");
            }

            if (owner != host)
            {
                InitializeRepository(owner, "Hexalith.EventStore");
                Declare(host, "Hexalith.EventStore", Path.GetRelativePath(host, owner));
            }

            if (dependency != host)
            {
                InitializeRepository(dependency, dependencyName);
                Declare(host, dependencyName, Path.GetRelativePath(host, dependency));
            }

            PackagingRepositoryPaths.ResolveDependency(owner, dependencyName).ShouldBe(dependency);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an initialized standalone declaration takes precedence over the umbrella source.
    /// </summary>
    [Fact]
    public void StandaloneDeclarationTakesPrecedenceAndUninitializedNestedSourceFallsBack()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.Tenants");
            string owner = Path.Combine(temporary, "references", "Hexalith.EventStore");
            string workspaceBuilds = Path.Combine(temporary, "references", "Hexalith.Builds");
            string nestedBuilds = Path.Combine(owner, "references", "Hexalith.Builds");
            InitializeRepository(owner, "Hexalith.EventStore");
            InitializeRepository(workspaceBuilds, "Hexalith.Builds");
            Declare(temporary, "Hexalith.EventStore", "references/Hexalith.EventStore");
            Declare(temporary, "Hexalith.Builds", "references/Hexalith.Builds");
            Declare(owner, "Hexalith.Builds", "references/Hexalith.Builds");
            Directory.CreateDirectory(nestedBuilds);
            PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds").ShouldBe(workspaceBuilds);

            InitializeRepository(nestedBuilds, "Hexalith.Builds");
            PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds").ShouldBe(nestedBuilds);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies explicit overrides are checked without requiring a workspace declaration.
    /// </summary>
    [Fact]
    public void ExplicitSourceOverrideRequiresTheIntendedRepository()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            string owner = Path.Combine(temporary, "owner");
            string builds = Path.Combine(temporary, "override");
            InitializeRepository(owner, "Hexalith.EventStore");
            InitializeRepository(builds, "Hexalith.Builds");
            PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds", builds).ShouldBe(builds);

            Git(builds, "remote", "set-url", "origin", "https://github.com/Hexalith/Hexalith.Tenants.git");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds", builds));
            Should.Throw<ArgumentException>(() => PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds", " "));
            Should.Throw<DirectoryNotFoundException>(() => PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds", Path.Combine(temporary, "missing")));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies unrelated siblings and a parent's Git discovery cannot supply missing dependencies.
    /// </summary>
    [Fact]
    public void UndeclaredSiblingAndAccidentalParentGitDiscoveryAreRejected()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.EventStore");
            string unrelated = Path.Combine(temporary, "references", "Hexalith.Builds");
            InitializeRepository(unrelated, "Hexalith.Builds");
            Should.Throw<DirectoryNotFoundException>(() => PackagingRepositoryPaths.ResolveDependency(temporary, "Hexalith.Builds"));

            string unowned = Path.Combine(temporary, "uninitialized");
            Directory.CreateDirectory(unowned);
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ResolveDependency(temporary, "Hexalith.Builds", unowned))
                .Message.ShouldContain("does not own its Git worktree");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(unowned, "Hexalith.EventStore", new string('0', 40), "global.json"))
                .Message.ShouldContain("does not own its Git worktree");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a declared checkout with the wrong identity cannot fall back to another source.
    /// </summary>
    [Fact]
    public void MismatchedDeclaredRepositoryAndMissingPinnedObjectsAreRejected()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.EventStore");
            string builds = Path.Combine(temporary, "references", "Hexalith.Builds");
            InitializeRepository(builds, "Hexalith.Tenants");
            Declare(temporary, "Hexalith.Builds", "references/Hexalith.Builds");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ResolveDependency(temporary, "Hexalith.Builds"));
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(temporary, "Hexalith.EventStore", new string('0', 40), "global.json"));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an ancestor declaring another EventStore checkout cannot supply this checkout's sources.
    /// </summary>
    [Fact]
    public void AncestorDeclaringDifferentEventStoreCheckoutIsRejected()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.Tenants");
            string owner = Path.Combine(temporary, "references", "Hexalith.EventStore");
            InitializeRepository(owner, "Hexalith.EventStore");
            InitializeRepository(Path.Combine(temporary, "other-eventstore"), "Hexalith.EventStore");
            InitializeRepository(Path.Combine(temporary, "references", "Hexalith.Builds"), "Hexalith.Builds");
            Declare(temporary, "Hexalith.EventStore", "other-eventstore");
            Declare(temporary, "Hexalith.Builds", "references/Hexalith.Builds");
            Should.Throw<DirectoryNotFoundException>(() => PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds"));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies both accepted SSH origin forms identify owning and dependency repositories.
    /// </summary>
    /// <param name="prefix">The accepted SSH URL prefix.</param>
    [Theory]
    [InlineData("git@github.com:")]
    [InlineData("ssh://git@github.com/")]
    public void SshOriginsIdentifyOwningAndDependencyRepositories(string prefix)
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.EventStore");
            string builds = Path.Combine(temporary, "references", "Hexalith.Builds");
            InitializeRepository(builds, "Hexalith.Builds");
            Git(temporary, "remote", "set-url", "origin", prefix + "Hexalith/Hexalith.EventStore.git");
            Git(builds, "remote", "set-url", "origin", prefix + "Hexalith/Hexalith.Builds.git");
            Declare(temporary, "Hexalith.Builds", "references/Hexalith.Builds");
            PackagingRepositoryPaths.ResolveDependency(temporary, "Hexalith.Builds").ShouldBe(builds);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies selected catalog sources retain import precedence even when another declared source is initialized.
    /// </summary>
    [Fact]
    public void SelectedDependencyMayUseEitherDeclaredImportSource()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            InitializeRepository(temporary, "Hexalith.Tenants");
            string owner = Path.Combine(temporary, "references", "Hexalith.EventStore");
            string workspaceBuilds = Path.Combine(temporary, "references", "Hexalith.Builds");
            string nestedBuilds = Path.Combine(owner, "references", "Hexalith.Builds");
            InitializeRepository(owner, "Hexalith.EventStore");
            InitializeRepository(workspaceBuilds, "Hexalith.Builds");
            InitializeRepository(nestedBuilds, "Hexalith.Builds");
            Declare(temporary, "Hexalith.EventStore", "references/Hexalith.EventStore");
            Declare(temporary, "Hexalith.Builds", "references/Hexalith.Builds");
            Declare(owner, "Hexalith.Builds", "references/Hexalith.Builds");
            PackagingRepositoryPaths.ResolveDependency(owner, "Hexalith.Builds").ShouldBe(nestedBuilds);
            PackagingRepositoryPaths.VerifyDeclaredDependency(owner, "Hexalith.Builds", workspaceBuilds).ShouldBe(workspaceBuilds);
            PackagingRepositoryPaths.VerifyDeclaredDependency(owner, "Hexalith.Builds", nestedBuilds).ShouldBe(nestedBuilds);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies exact SHA syntax cannot admit annotated tags or non-blob paths.
    /// </summary>
    [Fact]
    public void PinnedReadsRequireCommitAndBlobObjectTypes()
    {
        string temporary = CreateTemporaryDirectory();
        try
        {
            string source = FindRepositoryRoot();
            string clone = Path.Combine(temporary, "source");
            Git(temporary, "clone", "--shared", "--no-checkout", "--quiet", source, clone);
            Git(clone, "remote", "set-url", "origin", "https://github.com/Hexalith/Hexalith.EventStore.git");
            string commit = Git(clone, "rev-parse", "HEAD");
            string tagFile = Path.Combine(temporary, "tag-object.txt");
            File.WriteAllText(tagFile, $"object {commit}\ntype commit\ntag object-type-witness\ntagger Fixture <fixture@example.invalid> 0 +0000\n\nObject type witness.\n");
            string tag = Git(clone, "hash-object", "-w", "-t", "tag", tagFile);
            string tree = Git(clone, "rev-parse", commit + "^{tree}");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(clone, "Hexalith.EventStore", tag, "global.json"))
                .Message.ShouldContain("must be a commit object");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(clone, "Hexalith.EventStore", tree, "global.json"))
                .Message.ShouldContain("must be a commit object");
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(clone, "Hexalith.EventStore", commit, "src"))
                .Message.ShouldContain("must select a blob object");
            PackagingRepositoryPaths.ReadPinnedFile(clone, "Hexalith.EventStore", commit, "global.json")
                .ShouldBe(PackagingRepositoryPaths.ReadPinnedFile(source, "Hexalith.EventStore", commit, "global.json"));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// Verifies inherited Git selectors in isolated child processes cannot fake ownership or redirect pinned reads.
    /// </summary>
    [Fact]
    public async Task InheritedRepositorySelectorsCannotRedirectVerifiedOperations()
    {
        string? witness = Environment.GetEnvironmentVariable("HEXALITH_PACKAGING_SELECTOR_WITNESS");
        if (witness is not null)
        {
            string owned = Path.Combine(witness, "owned");
            string unowned = Path.Combine(witness, "unowned");
            string commit = Environment.GetEnvironmentVariable("HEXALITH_PACKAGING_SELECTOR_COMMIT").ShouldNotBeNull();
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.VerifyRepositoryRoot(unowned, "Hexalith.EventStore"));
            Should.Throw<InvalidDataException>(() => PackagingRepositoryPaths.ReadPinnedFile(unowned, "Hexalith.EventStore", commit, "global.json"));
            PackagingRepositoryPaths.ReadPinnedFile(owned, "Hexalith.EventStore", commit, "global.json")
                .ShouldBe(PackagingRepositoryPaths.ReadPinnedFile(FindRepositoryRoot(), "Hexalith.EventStore", commit, "global.json"));
            return;
        }

        string temporary = CreateTemporaryDirectory();
        try
        {
            string source = FindRepositoryRoot();
            string owned = Path.Combine(temporary, "owned");
            string unowned = Path.Combine(temporary, "unowned");
            string redirect = Path.Combine(temporary, "redirect");
            Git(temporary, "clone", "--shared", "--no-checkout", "--quiet", source, owned);
            Git(owned, "remote", "set-url", "origin", "https://github.com/Hexalith/Hexalith.EventStore.git");
            InitializeRepository(redirect, "Hexalith.EventStore");
            Directory.CreateDirectory(unowned);
            string commit = Git(owned, "rev-parse", "HEAD");
            foreach (bool redirectObjects in new[] { false, true })
            {
                ProcessStartInfo start = new("dotnet") { WorkingDirectory = temporary, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                start.ArgumentList.Add(typeof(PackagingRepositoryPathsTests).Assembly.Location);
                start.ArgumentList.Add("-method");
                start.ArgumentList.Add("*InheritedRepositorySelectorsCannotRedirectVerifiedOperations");
                start.ArgumentList.Add("-parallelMode");
                start.ArgumentList.Add("none");
                start.Environment["HEXALITH_PACKAGING_SELECTOR_WITNESS"] = temporary;
                start.Environment["HEXALITH_PACKAGING_SELECTOR_COMMIT"] = commit;
                start.Environment["GIT_DIR"] = Path.Combine(redirectObjects ? redirect : owned, ".git");
                start.Environment["GIT_WORK_TREE"] = redirectObjects ? owned : unowned;
                start.Environment["GIT_COMMON_DIR"] = start.Environment["GIT_DIR"];
                using Process process = Process.Start(start).ShouldNotBeNull();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                process.WaitForExit(60_000).ShouldBeTrue("The isolated Git selector witness timed out.");
                string[] diagnostics = await Task.WhenAll(output, error).ConfigureAwait(true);
                process.ExitCode.ShouldBe(0, string.Join('\n', diagnostics));
            }
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "packaging-repositories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void InitializeRepository(string path, string repositoryName)
    {
        Directory.CreateDirectory(path);
        Git(path, "init", "--quiet");
        Git(path, "remote", "add", "origin", "https://github.com/Hexalith/" + repositoryName + ".git");
    }

    private static void Declare(string host, string repositoryName, string relativePath) =>
        File.AppendAllText(Path.Combine(host, ".gitmodules"),
            $"[submodule \"{repositoryName}\"]\n\tpath = {relativePath.Replace('\\', '/')}\n\turl = https://github.com/Hexalith/{repositoryName}.git\n");

    private static string Git(string root, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        PackagingRepositoryPaths.RemoveRepositorySelectors(start);
        start.ArgumentList.Add("--no-replace-objects");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30_000).ShouldBeTrue("Temporary repository setup timed out.");
        process.ExitCode.ShouldBe(0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult().Trim();
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return PackagingRepositoryPaths.VerifyRepositoryRoot(directory.FullName, "Hexalith.EventStore");
            }
        }

        throw new DirectoryNotFoundException("Could not locate the EventStore source for an isolated Git fixture.");
    }
}
