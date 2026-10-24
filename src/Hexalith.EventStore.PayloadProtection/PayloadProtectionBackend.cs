// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 5, 10 and 15; amendment section 4; approved review correction section 8.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Opaque provider-neutral key wrapping SPI; it grants no provider administration or custody authority.</summary>
internal abstract class PayloadProtectionBackend
{
    /// <summary>Gets the exact registered ASCII kebab backend identity.</summary>
    internal abstract string BackendId { get; }
    /// <summary>Gets a full SHA-256 endpoint identity for breaker isolation, never the endpoint itself.</summary>
    internal abstract string EndpointFingerprint { get; }
    /// <summary>Wraps a fresh owned DEK after its durable intent has been confirmed.</summary>
    internal abstract ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken);
    /// <summary>Wraps the existing DEK under the exact retained Rewrap target, independently of the provider's current version.</summary>
    internal abstract ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> dek, CancellationToken cancellationToken);
    /// <summary>Unwraps only the exact version recorded with the bounded wrapped bytes.</summary>
    internal abstract ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken);
    /// <summary>Reconciles a possibly completed mutation by stable identity; an unknown outcome remains pending.</summary>
    /// <remarks>Missing affirms absence of this exact stable mutation. A missing KEK, query failure or unknowable outcome must not return Missing.</remarks>
    internal abstract ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken);
}
