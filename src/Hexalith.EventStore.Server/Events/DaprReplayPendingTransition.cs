namespace Hexalith.EventStore.Server.Events;

/// <summary>Retains exact expected participants and charged private evidence while a save remains indeterminate.</summary>
/// <param name="Prior">The last proven operation state.</param>
/// <param name="Expected">The prepared successor pointer.</param>
/// <param name="Ledger">The exact expected page ledger.</param>
/// <param name="Prepared">The retained private response/page/cache staging capacity.</param>
internal sealed record DaprReplayPendingTransition(DaprReplayOperationRecord Prior, DaprReplayOperationRecord Expected,
    DaprReplayPageLedger Ledger, DaprReplayPreparedTransition Prepared) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Prepared.Dispose();
}
