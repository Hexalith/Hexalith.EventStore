namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Serializes the test that intentionally exhausts the shared adapter-worker pool.</summary>
[CollectionDefinition("Process read slots", DisableParallelization = true)]
public sealed class ProcessReadSlotsCollection;
