namespace Hexalith.EventStore.Testing.Fakes;

internal enum ReadModelBatchStagingAction {
    Stage,
    Commit,
    Abort,
    Verify,
}
