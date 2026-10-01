using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Options for the EventStore typed-reminder runtime, bound from <c>EventStore:Reminders</c>.
/// </summary>
public sealed partial class EventStoreReminderOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "EventStore:Reminders";

    /// <summary>
    /// The longest delay <see cref="Task.Delay(TimeSpan)"/> and the reminder timers accept, in milliseconds.
    /// </summary>
    private const double MaxDelayMilliseconds = uint.MaxValue - 1d;

    /// <summary>
    /// Gets or sets the Dapr actor type name that hosts reminders. It is required and must be unique to one
    /// application: actor types are global under Dapr placement, so two applications sharing a name would route
    /// each other's reminders. It also scopes every persisted reminder key, so changing it on a live deployment
    /// abandons the previous scheduler and index state.
    /// </summary>
    public string ActorTypeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Dapr state-store component that holds reminder witnesses, index, and dispositions.</summary>
    public string StateStoreName { get; set; } = "statestore";

    /// <summary>
    /// Gets or sets the workload named in trusted-effect submissions. Defaults to <c>DAPR_APP_ID</c>, then the
    /// host application name.
    /// </summary>
    public string? Workload { get; set; }

    /// <summary>
    /// Gets the named delegated purpose for each reminder kind. A kind without a purpose is denied at callback
    /// admission and its work is retained; there are no defaults.
    /// </summary>
    public Dictionary<string, string> Purposes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether the periodic reconciler runs.</summary>
    public bool ReconciliationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between reconciliation passes that are complete or incomplete only because
    /// of capacity. Retained unresolved outcomes also keep this cadence. The first pass starts at startup.
    /// </summary>
    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the first retry delay after an uncertain submission. A pass with incomplete scans or
    /// failed candidate convergence waits the minimum of this delay and <see cref="ReconciliationInterval"/>;
    /// capacity alone and retained unresolved outcomes keep the normal interval.
    /// </summary>
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the longest retry delay, which is also the scheduler period of every armed reminder.</summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets the most candidates one tenant index may hold before registration fails closed.</summary>
    public int MaxCandidatesPerTenant { get; set; } = 10_000;

    /// <summary>Gets or sets the bounded number of compare-and-swap attempts for one index update.</summary>
    public int IndexWriteAttempts { get; set; } = 8;

    /// <summary>Returns the validation errors of the current values.</summary>
    /// <returns>An empty list when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ActorTypeName))
        {
            errors.Add("ActorTypeName is required and must be unique to this application.");
        }
        else if (!ActorTypeNamePattern().IsMatch(ActorTypeName))
        {
            errors.Add("ActorTypeName must start with a letter and contain at most 64 letters, digits, '_' or '-'.");
        }

        if (string.IsNullOrWhiteSpace(StateStoreName))
        {
            errors.Add("StateStoreName is required.");
        }

        if (ReconciliationInterval <= TimeSpan.Zero)
        {
            errors.Add("ReconciliationInterval must be positive.");
        }

        if (RetryInitialDelay <= TimeSpan.Zero || RetryMaxDelay < RetryInitialDelay)
        {
            errors.Add("RetryInitialDelay must be positive and no longer than RetryMaxDelay.");
        }

        if (ReconciliationInterval.TotalMilliseconds > MaxDelayMilliseconds
            || RetryInitialDelay.TotalMilliseconds > MaxDelayMilliseconds
            || RetryMaxDelay.TotalMilliseconds > MaxDelayMilliseconds)
        {
            errors.Add("ReconciliationInterval, RetryInitialDelay, and RetryMaxDelay must not exceed 4294967294 milliseconds.");
        }

        if (MaxCandidatesPerTenant <= 0)
        {
            errors.Add("MaxCandidatesPerTenant must be positive.");
        }

        if (IndexWriteAttempts is < 1 or > 100)
        {
            errors.Add("IndexWriteAttempts must be between 1 and 100.");
        }

        foreach (KeyValuePair<string, string> purpose in Purposes)
        {
            if (!ReminderIdentityCodec.IsSupportedKind(purpose.Key) || string.IsNullOrWhiteSpace(purpose.Value))
            {
                errors.Add("Purposes may name only version-one reminder kinds, each with a non-empty purpose.");
                break;
            }
        }

        return errors;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ActorTypeNamePattern();
}
