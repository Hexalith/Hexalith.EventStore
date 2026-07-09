// Preserve the historical serialized Server.Tests execution after the
// live-sidecar split so this change does not also alter in-process test timing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
