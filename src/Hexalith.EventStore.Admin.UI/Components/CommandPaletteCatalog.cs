namespace Hexalith.EventStore.Admin.UI.Components;

/// <summary>
/// Provides the placeholder command palette entries and fuzzy filtering logic.
/// </summary>
public static class CommandPaletteCatalog
{
    private static readonly IReadOnlyList<CommandPaletteItem> _allItems =
    [
        new("Actions", "Home", "/"),
        new("Actions", "Commands", "/commands"),
        new("Actions", "Events", "/events"),
        new("Actions", "Health Dashboard", "/health"),
        new("Actions", "Dead Letters", "/health/dead-letters"),
        new("Health", "DAPR Component Status", "/health"),
        new("Health", "Observability Tools", "/health"),
        new("Actions", "Services", "/services"),
        new("Actions", "Tenants", "/tenants"),
        new("Actions", "Streams", "/streams"),
        new("Actions", "Settings", "/settings"),
        new("Streams", "Recent Activity", "/streams"),
        new("Streams", "Inspect stream metadata", "/commands"),
        new("Actions", "Projections", "/projections"),
        new("Projections", "Projection Dashboard", "/projections"),
        new("Actions", "Type Catalog", "/types"),
        new("Types", "Event Types", "/types?tab=events"),
        new("Types", "Command Types", "/types?tab=commands"),
        new("Types", "Aggregate Types", "/types?tab=aggregates"),
        new("Tenants", "Manage Tenants", "/tenants"),
        new("Actions", "Storage", "/storage"),
        new("Storage", "Storage Growth Analyzer", "/storage"),
        new("Actions", "Snapshots", "/snapshots"),
        new("Snapshots", "Snapshot Policies", "/snapshots"),
        new("Actions", "Compaction", "/compaction"),
        new("Compaction", "Compaction Manager", "/compaction"),
        new("Actions", "Backups", "/backups"),
        new("Backups", "Backup & Restore", "/backups"),
        new("Backups", "Export Stream", "/backups"),
        new("Backups", "Import Stream", "/backups"),
    ];

    public static IReadOnlyList<CommandPaletteItem> AllItems => _allItems;

    public static IReadOnlyList<CommandPaletteItem> Filter(string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return _allItems;
        }

        string query = searchQuery.Trim();

        return _allItems
            .Select(item => new
            {
                Item = item,
                Score = GetMatchScore(item, query),
            })
            .Where(result => result.Score is not null)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Item.Category, StringComparer.Ordinal)
            .ThenBy(result => result.Item.Label, StringComparer.Ordinal)
            .Select(result => result.Item)
            .ToList();
    }

    private static int? GetMatchScore(CommandPaletteItem item, string query)
    {
        string normalizedQuery = Normalize(query);
        string[] candidates =
        [
            Normalize(item.Label),
            Normalize(item.Category),
            Normalize($"{item.Category} {item.Label}"),
        ];

        foreach (string candidate in candidates)
        {
            if (candidate.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                return candidate.IndexOf(normalizedQuery, StringComparison.Ordinal);
            }
        }

        int? subsequenceScore = candidates
            .Select(candidate => GetSubsequenceScore(candidate, normalizedQuery))
            .Where(score => score is not null)
            .OrderBy(score => score)
            .FirstOrDefault();

        return subsequenceScore is null ? null : 100 + subsequenceScore.Value;
    }

    private static int? GetSubsequenceScore(string candidate, string query)
    {
        int score = 0;
        int currentIndex = -1;
        foreach (char character in query)
        {
            currentIndex = candidate.IndexOf(character, currentIndex + 1);
            if (currentIndex < 0)
            {
                return null;
            }

            score += currentIndex;
        }

        return score;
    }

    private static string Normalize(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

public sealed record CommandPaletteItem(string Category, string Label, string Href);
