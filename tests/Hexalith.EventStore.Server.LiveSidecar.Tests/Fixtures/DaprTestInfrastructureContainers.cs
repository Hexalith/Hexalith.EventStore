using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Routes product diagnostics to an explicitly owned infrastructure container.</summary>
internal static class DaprTestInfrastructureContainers
{
    /// <summary>Resolves an optional exact container ID while preserving local-development defaults.</summary>
    /// <param name="name">The conventional infrastructure container name.</param>
    /// <param name="read">The environment reader.</param>
    /// <returns>The selected exact ID or the unchanged conventional name.</returns>
    internal static string Resolve(string name, Func<string, string?> read)
    {
        string? variable = name switch
        {
            "dapr_redis" => "EVENTSTORE_TEST_REDIS_CONTAINER",
            "dapr_placement" => "EVENTSTORE_TEST_PLACEMENT_CONTAINER",
            "dapr_scheduler" => "EVENTSTORE_TEST_SCHEDULER_CONTAINER",
            _ => null,
        };
        string? selected = variable is null ? null : read(variable);
        if (selected is null)
        {
            return name;
        }

        if (!Regex.IsMatch(selected, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException("An isolated infrastructure diagnostic must select an exact container ID.");
        }

        return selected;
    }
}
