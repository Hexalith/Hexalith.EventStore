namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

internal static class Dw6TestPaths {
    public const string DeferredWorkPath = "_bmad-output/implementation-artifacts/deferred-work.md";
    public const string StoryPath = "_bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md";
    public const string SprintStatusPath = "_bmad-output/implementation-artifacts/sprint-status.yaml";
    public const string ArtifactRoot = "_bmad-output/test-artifacts/deferred-work-governance";
    public const string EntrypointPath = ArtifactRoot + "/entrypoint.txt";
    public const string SnapshotPath = ArtifactRoot + "/deferred-work-snapshot.md";
    public const string ChecklistPath = "_bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw6-deferred-work-governance.md";

    public static string LocateRepoRoot() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            if (File.Exists(Path.Combine(dir, "Hexalith.EventStore.slnx"))) {
                return dir;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null) {
                break;
            }

            dir = parent.FullName;
        }

        throw new InvalidOperationException("Repo root (Hexalith.EventStore.slnx) not found.");
    }

    public static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relativePath));

    public static IReadOnlyList<string> ExtractMarkdownBullets(string content)
        => content
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => {
                string trimmed = line.TrimStart();
                return trimmed.StartsWith("- ", StringComparison.Ordinal)
                    || trimmed.StartsWith("* ", StringComparison.Ordinal);
            })
            .ToList();

    public static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";
}
