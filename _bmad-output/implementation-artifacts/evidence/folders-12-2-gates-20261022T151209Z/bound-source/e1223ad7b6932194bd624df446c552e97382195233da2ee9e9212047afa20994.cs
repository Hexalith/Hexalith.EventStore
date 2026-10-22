namespace Hexalith.EventStore.Admin.Cli.Tests;

/// <summary>
/// Serializes tests that replace process-wide console writers against every other test collection.
/// </summary>
[CollectionDefinition("ConsoleTests", DisableParallelization = true)]
public sealed class ConsoleTestCollection {
}
