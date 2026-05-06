namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// DW5 ATDD scaffold helper — locates the repository root from the test assembly path so
/// governance / source-content tests can read evidence artefacts, story files, and razor
/// sources without hardcoding absolute paths.
/// </summary>
internal static class Dw5TestPaths {
    private const string _repoMarker = "Hexalith.EventStore.slnx";
    private const string _envOverride = "HEXALITH_EVENTSTORE_REPO_ROOT";

    public static string RepoRoot() {
        // Honour an explicit override so CI / shadow-copy / unusual output paths can pin
        // the root without relying on a fixed depth from the test assembly.
        string? overrideRoot = Environment.GetEnvironmentVariable(_envOverride);
        if (!string.IsNullOrWhiteSpace(overrideRoot) && Directory.Exists(overrideRoot)) {
            return Path.GetFullPath(overrideRoot);
        }

        // Walk upward from the test assembly until we find the solution marker. This is
        // robust to refactors that move the test project deeper or shallower in the tree
        // and to runners that shadow-copy assemblies under a different bin layout.
        string startDir = Path.GetDirectoryName(typeof(Dw5TestPaths).Assembly.Location)!;
        DirectoryInfo? cursor = new(startDir);
        while (cursor is not null) {
            if (File.Exists(Path.Combine(cursor.FullName, _repoMarker))) {
                return cursor.FullName;
            }
            cursor = cursor.Parent;
        }

        throw new InvalidOperationException(
            $"Unable to locate repository root from '{startDir}'. "
            + $"Set the {_envOverride} environment variable or ensure {_repoMarker} exists in an ancestor directory.");
    }
}
