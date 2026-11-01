namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using Hexalith.EventStore.Aspire;

public class RepositoryProjectPathsTests {
    private const string Module = "Hexalith.EventStore";

    private static readonly string[] _gatewayProject = ["src", "Hexalith.EventStore", "Hexalith.EventStore.csproj"];

    [Fact]
    public void GetProjectPath_WhenValidSegments_ReturnsPathUnderRepositoryRoot() {
        string path = RepositoryProjectPaths.GetProjectPath(
            "src",
            "Hexalith.EventStore.Aspire",
            "Hexalith.EventStore.Aspire.csproj");

        path.ShouldBe(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.Aspire",
            "Hexalith.EventStore.Aspire.csproj"));
    }

    [Fact]
    public void EventStoreProjectMetadata_ProjectPath_UsesCurrentRepositoryLayoutWhenPresent() {
        string path = new EventStoreProjectMetadata().ProjectPath;

        path.ShouldBe(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore",
            "Hexalith.EventStore.csproj"));
    }

    [Fact]
    public void GetReferencedModuleProjectPath_WhenModuleIsNotMapped_ThrowsNamingDependencyAndExpectedPath() {
        string expected = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "references",
            "Hexalith.DoesNotExist",
            "src",
            "Hexalith.DoesNotExist",
            "Hexalith.DoesNotExist.csproj");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            static () => RepositoryProjectPaths.GetReferencedModuleProjectPath(
                "Hexalith.DoesNotExist",
                "src",
                "Hexalith.DoesNotExist",
                "Hexalith.DoesNotExist.csproj"));

        exception.Message.ShouldContain("'Hexalith.DoesNotExist'");
        exception.Message.ShouldContain(expected);
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenRootIsTheDependency_ReturnsProjectUnderRoot() {
        using TemporaryLayout layout = new();
        _ = layout.CreateFile(Module + ".slnx");
        string project = layout.CreateFile(_gatewayProject);

        string path = RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject);

        path.ShouldBe(project);
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenRootIsTheDependency_IgnoresReferencesCopy() {
        using TemporaryLayout layout = new();
        _ = layout.CreateFile(Module + ".slnx");
        string project = layout.CreateFile(_gatewayProject);
        _ = layout.CreateFile(["references", Module, .. _gatewayProject]);

        string path = RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject);

        path.ShouldBe(project);
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenRootIsTheDependencyAndProjectIsMissing_ThrowsNamingDependencyAndExpectedPath() {
        using TemporaryLayout layout = new();
        _ = layout.CreateFile(Module + ".slnx");
        _ = layout.CreateFile(["references", Module, .. _gatewayProject]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain($"'{Module}'");
        exception.Message.ShouldContain(layout.PathOf(_gatewayProject));
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenDependencyIsMappedUnderReferences_ReturnsReferencesProject() {
        using TemporaryLayout layout = new();
        string project = layout.CreateFile(["references", Module, .. _gatewayProject]);

        string path = RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject);

        path.ShouldBe(project);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveReferencedModuleProjectPath_WhenWorkspaceDeclaresConsumerAndSibling_ReturnsSiblingProject(bool quotedPaths) {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, quotedPaths, "consumer", Module);
        string project = layout.CreateFile(["..", Module, .. _gatewayProject]);

        string path = RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject);

        path.ShouldBe(project);
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenDirectReferenceAndDeclaredSiblingExist_PrefersDirectReference() {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, false, "consumer", Module);
        string project = layout.CreateFile(["references", Module, .. _gatewayProject]);
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        string path = RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject);

        path.ShouldBe(project);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ResolveReferencedModuleProjectPath_WhenConsumerOrSiblingIsUndeclared_Throws(bool declaresConsumer, bool declaresSibling) {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, false, declaresConsumer ? "consumer" : "other-consumer", declaresSibling ? Module : Module + ".Other");
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain($"'{Module}'");
        exception.Message.ShouldContain(layout.PathOf(["references", Module, .. _gatewayProject]));
        exception.Message.ShouldNotContain("git submodule");
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenDeclaredSiblingProjectIsMissing_ThrowsNamingBothExpectedLocations() {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, false, "consumer", Module);
        _ = layout.CreateFile("..", Module, Module + ".slnx");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain(layout.PathOf(["references", Module, .. _gatewayProject]));
        exception.Message.ShouldContain(layout.PathOf(["..", Module, .. _gatewayProject]));
        exception.Message.ShouldNotContain("git submodule");
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenOwnProjectIsMissingWithDeclaredSibling_ThrowsInsteadOfSelectingSibling() {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, false, "consumer", Module);
        _ = layout.CreateFile(Module + ".slnx");
        _ = layout.CreateFile(["references", Module, .. _gatewayProject]);
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain(layout.PathOf(_gatewayProject));
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenWorkspaceFileIsMissing_ThrowsInsteadOfSelectingSibling() {
        using TemporaryLayout layout = new(workspace: true);
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain(layout.PathOf(["references", Module, .. _gatewayProject]));
    }

    [Theory]
    [InlineData("other-section")]
    [InlineData("commented-path")]
    [InlineData("wrong-path")]
    [InlineData("overridden-path")]
    [InlineData("empty-unquoted-path")]
    [InlineData("empty-quoted-path")]
    public void ResolveReferencedModuleProjectPath_WhenWorkspaceDoesNotDeclareExactSubmodulePaths_Throws(string declaration) {
        using TemporaryLayout layout = new(workspace: true);
        DeclareWorkspace(layout, false, "consumer");
        string gitmodules = layout.PathOf("..", "..", ".gitmodules");
        string invalidDeclaration = declaration switch {
            "other-section" => $"[other \"dependency\"]\npath = references/{Module}\n",
            "commented-path" => $"[submodule \"dependency\"]\n# path = references/{Module}\n",
            "wrong-path" => $"[submodule \"dependency\"]\npath = other/{Module}\n",
            "overridden-path" => $"[submodule \"dependency\"]\npath = references/{Module}\npath = other/{Module}\n",
            "empty-unquoted-path" => $"[submodule \"dependency\"]\npath = references/{Module}\npath =\n",
            "empty-quoted-path" => $"[submodule \"dependency\"]\npath = references/{Module}\npath = \"\"\n",
            _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration, null),
        };
        File.AppendAllText(gitmodules, invalidDeclaration);
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        _ = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));
    }

    [Fact]
    public void ResolveReferencedModuleProjectPath_WhenConsumerIsNotUnderReferences_ThrowsEvenWithDeclarations() {
        using TemporaryLayout layout = new();
        DeclareWorkspace(layout, false, "consumer", Module);
        _ = layout.CreateFile(["..", Module, .. _gatewayProject]);

        _ = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));
    }

    [Theory]
    [InlineData("sibling")]
    [InlineData("parent-references")]
    [InlineData("ancestor")]
    [InlineData("grand-ancestor")]
    [InlineData("root-level")]
    public void ResolveReferencedModuleProjectPath_WhenDependencyIsOnlyOutsideTheMapping_ThrowsNamingDependencyAndExpectedPath(string unmappedLayout) {
        using TemporaryLayout layout = new();
        string[] unmapped = unmappedLayout switch {
            "sibling" => ["..", Module, .. _gatewayProject],
            "parent-references" => ["..", "references", Module, .. _gatewayProject],
            "ancestor" => ["..", .. _gatewayProject],
            "grand-ancestor" => ["..", "..", .. _gatewayProject],
            "root-level" => [Module, .. _gatewayProject],
            _ => throw new ArgumentOutOfRangeException(nameof(unmappedLayout), unmappedLayout, null),
        };
        _ = layout.CreateFile(unmapped);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => RepositoryProjectPaths.ResolveReferencedModuleProjectPath(layout.Root, Module, _gatewayProject));

        exception.Message.ShouldContain($"'{Module}'");
        exception.Message.ShouldContain(layout.PathOf(["references", Module, .. _gatewayProject]));
    }

    [Fact]
    public void GetProjectPath_WhenNoSegments_ThrowsArgumentException() {
        ArgumentException exception = Should.Throw<ArgumentException>(
            static () => RepositoryProjectPaths.GetProjectPath());

        exception.ParamName.ShouldBe("path");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("src/../Hexalith.EventStore.Aspire")]
    public void GetProjectPath_WhenSegmentIsInvalid_ThrowsArgumentException(string segment) {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => RepositoryProjectPaths.GetProjectPath(segment));

        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void GetProjectPath_WhenSegmentIsRooted_ThrowsArgumentException() {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => RepositoryProjectPaths.GetProjectPath(Path.GetPathRoot(Environment.CurrentDirectory)!));

        exception.ParamName.ShouldBe("path");
    }

    private static void DeclareWorkspace(TemporaryLayout layout, bool quotedPaths, params string[] repositories) {
        string gitmodules = layout.CreateFile("..", "..", ".gitmodules");
        File.WriteAllText(gitmodules, string.Join(Environment.NewLine, repositories.Select(
            repository => quotedPaths
                ? $"[submodule \"{repository}\"] # workspace dependency\n\tpath = \"references/{repository}\" ; declared path\n"
                : $"[submodule \"{repository}\"]\n\tpath = references/{repository}\n")));
    }

    /// <summary>
    /// A disposable tree with optional workspace <c>references/</c> layout, allowing files in the root and in each
    /// of its parent and grandparent directories without leaving the temporary base.
    /// </summary>
    private sealed class TemporaryLayout : IDisposable {
        private readonly string _base = Path.Combine(Path.GetTempPath(), "hexalith-repository-paths-" + Guid.NewGuid().ToString("N"));

        public TemporaryLayout(bool workspace = false) {
            Root = Path.Combine(_base, "a", workspace ? "references" : "b", "consumer");
            _ = Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PathOf(params string[] segments) => Path.GetFullPath(Path.Combine([Root, .. segments]));

        public string CreateFile(params string[] segments) {
            string path = PathOf(segments);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose() {
            if (Directory.Exists(_base)) {
                Directory.Delete(_base, recursive: true);
            }
        }
    }
}
