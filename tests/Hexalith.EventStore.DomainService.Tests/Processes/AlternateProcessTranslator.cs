using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Distinct adapter identity for process registration conflict tests.</summary>
internal sealed class AlternateProcessTranslator : IProcessTranslator
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ProcessEffect>> TranslateAsync(ProcessSource source, StreamReadEvent streamEvent, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<ProcessEffect>>([]);
}
