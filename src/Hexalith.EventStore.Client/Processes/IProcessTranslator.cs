using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>Fully decodes each state-affecting envelope and derives deterministic effects without external I/O.</summary>
/// <remarks>Unknown, malformed, or unsupported evidence must throw. Returning an empty list is an explicit decoded no-op.</remarks>
public interface IProcessTranslator
{
    /// <summary>Decodes one envelope and returns every effect in stable ordinal order.</summary>
    ValueTask<IReadOnlyList<ProcessEffect>> TranslateAsync(ProcessSource source, StreamReadEvent streamEvent, CancellationToken cancellationToken = default);
}
