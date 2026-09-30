using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The Dapr actor that owns one tenant item's typed reminders. It is a thin shell over the reminder
/// coordinator: the actor turn serializes registration and callbacks for the item, the actor registers and
/// cancels scheduler reminders, and every decision stays in the coordinator and the domain intent source.
/// Its type name is configured by <see cref="EventStoreReminderOptions.ActorTypeName"/>.
/// </summary>
public sealed class ReminderActor : Actor, IReminderActor, IRemindable, IReminderScheduler
{
    private readonly ReminderCoordinator _coordinator;

    /// <summary>Initializes a new instance of the <see cref="ReminderActor"/> class.</summary>
    /// <param name="host">The Dapr actor host.</param>
    /// <param name="services">The actor activation's service scope.</param>
    public ReminderActor(ActorHost host, IServiceProvider services)
        : base(host)
    {
        ArgumentNullException.ThrowIfNull(services);
        _coordinator = services.GetRequiredService<ReminderCoordinator>();
    }

    /// <inheritdoc/>
    public Task<ReminderConvergenceResult> ConvergeAsync(ReminderTarget target)
        => _coordinator.ConvergeAsync(Id.GetId(), target, this, CancellationToken.None);

    /// <inheritdoc/>
    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        => _ = await _coordinator
            .HandleCallbackAsync(Id.GetId(), reminderName, this, CancellationToken.None)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    Task IReminderScheduler.ArmAsync(string reminderName, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RegisterReminderAsync(reminderName, null, dueTime, period);
    }

    /// <inheritdoc/>
    async Task<bool> IReminderScheduler.IsArmedAsync(string reminderName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await GetReminderAsync(reminderName).ConfigureAwait(false) is not null;
    }

    /// <inheritdoc/>
    Task IReminderScheduler.CancelAsync(string reminderName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return UnregisterReminderAsync(reminderName);
    }
}
