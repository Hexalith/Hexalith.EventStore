namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Supplies independent backend objects with explicit circuit identity components.</summary>
internal sealed class IdentityTestBackend(string backendId, string endpoint) : PayloadProtectionBackend
{
    /// <inheritdoc/>
    internal override string BackendId => backendId;
    /// <inheritdoc/>
    internal override string EndpointFingerprint => endpoint;
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken) => throw new NotSupportedException();
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken) => throw new NotSupportedException();
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken) => throw new NotSupportedException();
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
