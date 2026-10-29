using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Resolves registered server-trusted adapters and produces canonical descriptors.</summary>
public interface IIdempotencyIntentAdapterRegistry
{
    /// <summary>Resolves and invokes the trusted adapter for a command.</summary>
    /// <param name="command">The authorized and validated command.</param>
    /// <returns>A trusted canonical descriptor with fixed server-owned policy metadata.</returns>
    TrustedIdempotencyDescriptor Resolve(SubmitCommand command);

    /// <summary>Resolves under the committed route/admission generation before invoking the trusted adapter.</summary>
    Task<TrustedIdempotencyDescriptor> ResolveAsync(SubmitCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Resolve(command));
}
