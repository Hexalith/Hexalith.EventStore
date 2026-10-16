using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>Fully decodes each state-affecting envelope and derives deterministic effects.</summary>
/// <remarks>
/// Host adapters may acquire bounded, cancellable, read-only authoritative context before invoking pure domain
/// translation. Writes and command submission are forbidden. Transient context acquisition failures must throw
/// <see cref="ProcessEvidenceUnavailableException"/>; unknown, malformed, foreign, or unsupported evidence must
/// throw a different exception. Returning an empty list is an explicit decoded no-op.
/// </remarks>
public interface IProcessTranslator
{
    /// <summary>Decodes one envelope and returns every effect in stable ordinal order.</summary>
    ValueTask<IReadOnlyList<ProcessEffect>> TranslateAsync(ProcessSource source, StreamReadEvent streamEvent, CancellationToken cancellationToken = default);
}
