// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 5, 10 and 15; amendment section 4.
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
    /// <summary>Unwraps only the exact version recorded with the bounded wrapped bytes.</summary>
    internal abstract ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken);
    /// <summary>Reconciles a possibly completed mutation by stable identity; an unknown outcome remains pending.</summary>
    internal abstract ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken);
}
