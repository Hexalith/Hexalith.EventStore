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

    /// <summary>
    /// A disposable <c>&lt;base&gt;/a/b/consumer</c> tree, so files can be placed in the consumer root and in each
    /// of its parent and grandparent directories without leaving the temporary base.
    /// </summary>
    private sealed class TemporaryLayout : IDisposable {
        private readonly string _base = Path.Combine(Path.GetTempPath(), "hexalith-repository-paths-" + Guid.NewGuid().ToString("N"));

        public TemporaryLayout() {
            Root = Path.Combine(_base, "a", "b", "consumer");
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
