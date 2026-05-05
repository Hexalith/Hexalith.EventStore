namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// DW5 ATDD scaffold helper — locates the repository root from the test assembly path so
/// governance / source-content tests can read evidence artefacts, story files, and razor
/// sources without hardcoding absolute paths.
/// </summary>
internal static class Dw5TestPaths {
    public static string RepoRoot() {
        // tests/Hexalith.EventStore.Admin.UI.Tests/bin/<config>/<tfm>/ → repo root
        string testDir = Path.GetDirectoryName(typeof(Dw5TestPaths).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
    }
}
