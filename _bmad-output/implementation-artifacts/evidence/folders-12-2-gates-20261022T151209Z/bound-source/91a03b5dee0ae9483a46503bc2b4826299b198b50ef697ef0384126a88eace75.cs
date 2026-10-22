using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Log event ids must be unique within the projection subsystem.
/// </summary>
/// <remarks>
/// A duplicate id is invisible in code review and silently defeats filtering and alerting: two messages with
/// different severities and meanings become indistinguishable to anything keying on the id. This guard exists
/// because a newly added refusal reason reused an id already carried by a poller-conflict warning in the same
/// subsystem.
/// </remarks>
public sealed class ProjectionLogEventIdUniquenessTests
{
    [Fact]
    public void ProjectionLogEventIdsAreUniqueAcrossTheSubsystem()
    {
        string projectionsDirectory = Path.Combine(RepositoryRoot(), "src", "Hexalith.EventStore.Server", "Projections");
        Directory.Exists(projectionsDirectory).ShouldBeTrue(projectionsDirectory);

        Dictionary<string, List<string>> owners = new(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(projectionsDirectory, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(
                File.ReadAllText(file),
                @"EventId\s*=\s*(?<id>\d+)",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)))
            {
                string id = match.Groups["id"].Value;
                if (!owners.TryGetValue(id, out List<string>? files))
                {
                    files = [];
                    owners[id] = files;
                }

                files.Add(Path.GetFileName(file));
            }
        }

        owners.Count.ShouldBeGreaterThan(0);
        string[] duplicates =
        [
            .. owners
                .Where(static entry => entry.Value.Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(static entry => entry.Key)
                .Order(StringComparer.Ordinal)
        ];

        // These three collisions predate this guard. Renumbering an id that is already shipped changes what
        // operators' filters and alerts match, which is a decision for the EventStore owners rather than a
        // drive-by fix — so they are pinned here, visible, instead of being silently tolerated or silently
        // changed. Any NEW collision fails this test.
        string[] knownPreExisting = ["1120", "1121", "4660"];

        duplicates.ShouldBe(knownPreExisting, ignoreOrder: true);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Server")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the EventStore repository root.");
    }
}
