using Hexalith.EventStore.ServiceDefaults.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Diagnostics;

/// <summary>Exposes the admin index's fixed observation points through existing assembly friendships.</summary>
internal sealed class AdminIndexShutdownTrace : IDisposable
{
    private readonly ShutdownStageScope _scope;

    private AdminIndexShutdownTrace(ShutdownStageScope scope) => _scope = scope;

    /// <summary>Observes the admin index stop operation.</summary>
    internal static AdminIndexShutdownTrace? StartStop(ILogger logger, CancellationToken token)
        => Wrap(ShutdownStageScope.TryStart(logger, ShutdownStage.AdminIndexStop, token));

    /// <summary>Observes the existing refresh cancellation callback wait.</summary>
    internal static AdminIndexShutdownTrace? StartCancellation(ILogger logger, CancellationToken token)
        => Wrap(ShutdownStageScope.TryStart(logger, ShutdownStage.AdminIndexCancelCallbacks, token));

    /// <summary>Observes the existing refresh-task wait.</summary>
    internal static AdminIndexShutdownTrace? StartRefresh(ILogger logger, CancellationToken token)
        => Wrap(ShutdownStageScope.TryStart(logger, ShutdownStage.AdminIndexRefreshDrain, token));

    /// <summary>Records return without changing the observed operation's result.</summary>
    internal void Complete() => _scope.Complete();

    /// <inheritdoc/>
    /// <summary>Verifies dispose.</summary>
    public void Dispose() => _scope.Dispose();

    private static AdminIndexShutdownTrace? Wrap(ShutdownStageScope? scope) => scope is null ? null : new(scope);
}
