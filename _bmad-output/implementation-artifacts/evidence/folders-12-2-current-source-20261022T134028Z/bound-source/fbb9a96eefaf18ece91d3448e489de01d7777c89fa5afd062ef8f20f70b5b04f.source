using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>A synthetic translator that fully accepts an event with no effects.</summary>
internal sealed class EmptyTranslator : IProcessTranslator
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ProcessEffect>> TranslateAsync(ProcessSource source, StreamReadEvent streamEvent, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<ProcessEffect>>([]);
}
