namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainPartitionedSharedProjectionRebuildTests
{
    /// <summary>Matches the existing Folders buffered-owner, streamed-output SDK contract.</summary>
    private sealed class OutputOnlyPartitionedHandler : PartitionedHandler, IAsyncDomainSharedProjectionStreamingRebuildHandler;
}
