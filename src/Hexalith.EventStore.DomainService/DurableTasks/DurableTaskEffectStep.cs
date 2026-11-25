using System.Globalization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The original admission and stable idempotency details for an owner step.</summary>
/// <param name="Task">The admitted task with the original Tenant, actor, operation and intent.</param>
/// <param name="Number">The one-based step number.</param>
/// <param name="Key">The idempotency key, stable across lease takeovers.</param>
/// <param name="Fence">The task's current lease generation.</param>
public sealed record DurableTaskEffectStep(CommittedDurableTaskAdmission Task, int Number, string Key, long Fence)
{
    private const string Separator = "/step/";

    /// <summary>Formats the stable key of a task step.</summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="number">The one-based step number.</param>
    /// <returns>The key <c>{taskId}/step/{number}</c>.</returns>
    internal static string KeyFor(string taskId, int number)
        => string.Create(CultureInfo.InvariantCulture, $"{taskId}{Separator}{number}");

    /// <summary>Parses a canonical step key of the given task.</summary>
    /// <param name="taskId">The task identifier the key must name.</param>
    /// <param name="key">The presented step key.</param>
    /// <param name="number">The parsed one-based step number.</param>
    /// <returns>Whether the key is the canonical key of a step of this task.</returns>
    internal static bool TryParseKey(string taskId, string key, out int number)
    {
        number = 0;
        string prefix = taskId + Separator;
        return key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number > 0
            && KeyFor(taskId, number) == key;
    }
}
