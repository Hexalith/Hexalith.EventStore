// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 5, 10 and 15; amendment section 4; approved review correction section 8.
// Approved Dapr Kubernetes cryptography supplement: e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034; sections 5 and 7.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Opaque provider-neutral key wrapping SPI; it grants no provider administration or custody authority.</summary>
internal abstract class PayloadProtectionBackend
{
    /// <summary>Gets the exact registered ASCII kebab backend identity.</summary>
    internal abstract string BackendId { get; }
    /// <summary>Gets a full SHA-256 endpoint identity for breaker isolation, never the endpoint itself.</summary>
    internal abstract string EndpointFingerprint { get; }
    /// <summary>Gets the exact wrap algorithm identity recorded with keys this backend wraps.</summary>
    internal virtual string WrapAlgorithmId => "RSA-OAEP-256";
    /// <summary>Gets the closed wrapped-key record schema this backend writes; only the Dapr Kubernetes backend writes V2.</summary>
    internal virtual int KeyRecordSchemaVersion => 1;
    /// <summary>Gets whether Reconcile answers without provider transport because this backend keeps no provider-side mutation state.</summary>
    /// <remarks>Only such a backend's Missing answer leaves the breaker and provider-failure measurement untouched; every other answer is observed.</remarks>
    internal virtual bool ReconcilesWithoutTransport => false;
    /// <summary>Wraps a fresh owned DEK after its durable intent has been confirmed.</summary>
    internal abstract ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken);
    /// <summary>Wraps the existing DEK under the exact retained Rewrap target, independently of the provider's current version.</summary>
    internal abstract ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> dek, CancellationToken cancellationToken);
    /// <summary>Unwraps only the exact version recorded with the bounded wrapped bytes.</summary>
    internal abstract ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken);
    /// <summary>Reconciles a possibly completed mutation by stable identity; an unknown outcome remains pending.</summary>
    /// <remarks>Missing affirms absence of this exact stable mutation. A backend that queries provider state must not return Missing for a missing
    /// KEK, a query failure or an unknowable outcome. A backend that creates no provider-side state, such as the Dapr Kubernetes cryptography
    /// backend, returns Missing without transport because no native mutation can exist; durable state alone then decides recovery.</remarks>
    internal abstract ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken);
}
