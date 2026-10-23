namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Typed provider fault fixture without a production-custody claim.</summary>
internal sealed class LifecycleTestBackend : PayloadProtectionBackend
{
    /// <summary>Gets or sets an explicit wrap boundary fault hook.</summary>
    internal Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask<PayloadBackendResult>>? Wrap { get; set; }
    /// <summary>Gets the number of observed wraps.</summary>
    internal int WrapCalls { get; private set; }
    /// <summary>Gets or sets an explicit exact-version unwrap hook.</summary>
    internal Func<string, string, ReadOnlyMemory<byte>, CancellationToken, ValueTask<PayloadBackendResult>>? Unwrap { get; set; }
    /// <summary>Gets the number of exact-version unwraps.</summary>
    internal int UnwrapCalls { get; private set; }
    /// <summary>Gets the fixture backend identity.</summary>
    internal override string BackendId => "lifecycle-test";
    /// <summary>Gets a full fixture endpoint fingerprint.</summary>
    internal override string EndpointFingerprint => new('a', 64);
    /// <summary>Rejects accidental use outside explicitly injected requests.</summary>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
    {
        WrapCalls++;
        return Wrap?.Invoke(operationId, dek, cancellationToken) ?? ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01"));
    }
    /// <summary>Rejects accidental use outside explicitly injected requests.</summary>
    internal override ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken)
    {
        UnwrapCalls++;
        return Unwrap?.Invoke(operationId, providerKeyVersionId, wrappedDek, cancellationToken) ?? throw new NotSupportedException();
    }
    /// <summary>Preserves ambiguous state by default.</summary>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
}
