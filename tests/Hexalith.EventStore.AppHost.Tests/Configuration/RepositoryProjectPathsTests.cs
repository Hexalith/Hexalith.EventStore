namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using Hexalith.EventStore.Aspire;

public class RepositoryProjectPathsTests {
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
    public void EventStoreProjectMetadata_ProjectPath_UsesReferencesSubmoduleLayout() {
        string path = new EventStoreProjectMetadata().ProjectPath;

        path.ShouldBe(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "references",
            "Hexalith.EventStore",
            "src",
            "Hexalith.EventStore",
            "Hexalith.EventStore.csproj"));
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
}
