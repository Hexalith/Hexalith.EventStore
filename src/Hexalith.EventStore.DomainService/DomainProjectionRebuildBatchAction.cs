namespace Hexalith.EventStore.DomainService;

internal enum DomainProjectionRebuildBatchAction {
    Execute,
    Stage,
    Commit,
    Abort,
    Verify,
}
