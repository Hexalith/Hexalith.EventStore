using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.Client.Reminders;

/// <summary>
/// Domain-implemented source of durable reminder intents. EventStore owns registration, callbacks, and
/// reconciliation; the domain module owns what its committed events mean.
/// </summary>
/// <remarks>
/// Both members must be deterministic and clock-free. EventStore calls them inside the target's serialized
/// reminder turn, both when converging and again before submitting a callback, so the stream, never a
/// persisted index or callback payload, decides which intents are current. An instance can live as long as a
/// reminder actor activation, so it must read the stream on every call rather than cache it.
/// <list type="bullet">
/// <item><description>Stop reporting an intent once its target has handled the submitted command; otherwise every
/// convergence resubmits it and replays the receipt.</description></item>
/// <item><description>Keep <c>(source domain, source aggregate, source sequence, kind, target)</c> unique among
/// current intents. Intents that share it share one effect identity and are quarantined as
/// <c>effect-collision</c>.</description></item>
/// <item><description>Retain the source coordinates when retrying the same logical submission. For the same kind
/// and target, a distinct logical submission needs distinct committed source-event coordinates: changing only
/// the due instant or schedule revision retains the previous effect identity and replays its receipt or
/// conflicts with changed command semantics.</description></item>
/// <item><description>Distinct current witnesses must derive distinct reminder names. The name binds the tenant,
/// target aggregate, kind, due instant, and schedule revision. Persist a new schedule revision when the source
/// coordinates, payload type, or payload change; different evidence under the same name is quarantined as
/// <c>witness-collision</c>.</description></item>
/// <item><description>Keep aggregate identifiers unique across every domain that shares one reminder actor type:
/// the AD-11 actor tuple omits the domain, so a second domain with the same aggregate identifier is
/// quarantined as <c>actor-collision</c>.</description></item>
/// </list>
/// </remarks>
public interface IReminderIntentSource
{
    /// <summary>Re-folds the target stream and returns every reminder intent it currently holds.</summary>
    /// <param name="target">The target stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The current intents. An empty list means the stream holds none. A null result is not an empty stream
    /// and must not be used to cancel stored reminders.
    /// </returns>
    Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(
        ReminderTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>Mechanically translates a due, current intent into the command submitted to its target.</summary>
    /// <param name="intent">An intent that this source reported as current.</param>
    /// <returns>The target command type and serialized payload.</returns>
    ReminderCommand TranslateDueIntent(ReminderIntent intent);
}
