// Approved Dapr Kubernetes cryptography supplement: e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034; sections 2-5 and 7.
// Profile authority: 2c7c509921cf611cae5ec394c9ba093951d9e3693e8740f9a5894b6df915c20d.
// The owner accepted the Alpha cryptography posture for this profile only on 2026-11-10. It is a recorded limitation, not a readiness claim,
// so the DAPR_CRYPTOGRAPHY experimental diagnostic is suppressed in this source file only.
#pragma warning disable DAPR_CRYPTOGRAPHY
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Grpc.Core;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>One-attempt gRPC Encrypt/Decrypt wrap backend over the stock crypto.dapr.kubernetes.secrets component.</summary>
/// <remarks>
/// Dapr Encrypt and Decrypt create no provider-side state, so Reconcile is always Missing without transport and durable state alone decides
/// recovery; wrap, unwrap and transport outcomes never produce Missing. Decrypt always names the recorded
/// trusted address; a key name embedded in an envelope never selects a key. Dapr.Client 1.19.0 completes these byte overloads with whatever output
/// arrived before its stream failed (often none), so a wrap is accepted only after the same address decrypts it back to the exact DEK within the same
/// attempt and budget. Retry, breaker and budget authority remain with the existing executor.
/// </remarks>
internal sealed class DaprCryptographyPayloadProtectionBackend : PayloadProtectionBackend
{
    private readonly DaprClient _client;
    private readonly DaprCryptographyPayloadProtectionOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Binds one validated component and closed address inventory; no Kubernetes client or default authority is constructed.</summary>
    internal DaprCryptographyPayloadProtectionBackend(DaprClient client, DaprCryptographyPayloadProtectionOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        // Validate the private copy, so the admitted inventory is exactly the validated one.
        _options = options with { RetainedKeys = options.RetainedKeys?.ToArray()! };
        _options.Validate();
        _client = client;
        _clock = timeProvider ?? TimeProvider.System;
        EndpointFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(WrappedDekV2.ClosedBackendId + ":" + _options.ComponentName)));
    }

    /// <summary>Gets the closed selected backend identity.</summary>
    internal override string BackendId => WrappedDekV2.ClosedBackendId;
    /// <summary>Gets the full hash used solely to isolate breaker state.</summary>
    internal override string EndpointFingerprint { get; }
    /// <summary>Gets the closed Dapr encryption-scheme v1 RSA-OAEP-256/AES-GCM identity.</summary>
    internal override string WrapAlgorithmId => WrappedDekV2.ClosedWrapAlgorithmId;
    /// <summary>Gets the closed WrappedDekV2 record schema.</summary>
    internal override int KeyRecordSchemaVersion => 2;
    /// <summary>Gets that Reconcile uses no transport: Encrypt and Decrypt create no provider-side mutation state.</summary>
    internal override bool ReconcilesWithoutTransport => true;

    /// <summary>Wraps a fresh DEK under the current write address only.</summary>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
        => RunAsync(token => EncryptCoreAsync(_options.CurrentWriteKey, dek, token), dek.Length == 32, cancellationToken);

    /// <summary>Wraps under the exact retained target; an address outside the inventory is refused before transport.</summary>
    internal override ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
        => RunAsync(token => EncryptCoreAsync(providerKeyVersionId, dek, token), dek.Length == 32 && _options.Admits(providerKeyVersionId), cancellationToken);

    /// <summary>Decrypts with the recorded address only; an address outside the inventory or a malformed envelope is refused before transport.</summary>
    internal override ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken)
        => RunAsync(token => DecryptCoreAsync(providerKeyVersionId, wrappedDek, token),
            _options.Admits(providerKeyVersionId) && WrappedDekV2.IsEnvelope(wrappedDek.Span), cancellationToken);

    /// <summary>Encrypt and Decrypt create no provider-side state, so the exact stable mutation never exists natively; no transport is used.</summary>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Absence is affirmed only for an exact stable operation identity, never for a missing or noncanonical one.
        if (!CanonicalUlid.IsValid(operationId)) { throw new PayloadProtectionFormatException(); }
        return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Missing));
    }

    private async ValueTask<PayloadBackendResult> RunAsync(Func<CancellationToken, ValueTask<PayloadBackendResult>> operation,
        bool admitted, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!admitted) { return new(PayloadBackendFailure.ConsistencyMismatch); }
        using var budget = new PayloadOperationBudget(_clock, cancellationToken);
        PayloadBackendResult? result = null;
        try
        {
            result = await operation(budget.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result?.Dispose();
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            result?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            return new(Classify(exception));
        }
    }

    private async ValueTask<PayloadBackendResult> EncryptCoreAsync(string address, ReadOnlyMemory<byte> dek, CancellationToken token)
    {
        byte[] plaintext = dek.ToArray();
        byte[]? owned = null;
        ReadOnlyMemory<byte> envelope = default;
        ReadOnlyMemory<byte> verified = default;
        try
        {
            PayloadOperationBudget.RequireCurrent();
            envelope = await _client.EncryptAsync(_options.ComponentName, plaintext, address,
                new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256) { EncryptionCipher = DataEncryptionCipher.AesGcm }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (!WrappedDekV2.IsEnvelope(envelope.Span)) { return new(PayloadBackendFailure.ConsistencyMismatch); }
            owned = envelope.ToArray();
            // A completed round trip under the same trusted address is the only format-agnostic proof that the envelope is whole.
            verified = await _client.DecryptAsync(_options.ComponentName, owned, address, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (verified.Length != 32 || !CryptographicOperations.FixedTimeEquals(verified.Span, plaintext)) { return new(PayloadBackendFailure.ConsistencyMismatch); }
            var result = new PayloadBackendResult(PayloadBackendFailure.None, owned, address, keyRecordSchemaVersion: 2);
            owned = null;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (owned is not null) { CryptographicOperations.ZeroMemory(owned); }
            Clear(envelope);
            Clear(verified);
        }
    }

    private async ValueTask<PayloadBackendResult> DecryptCoreAsync(string address, ReadOnlyMemory<byte> wrapped, CancellationToken token)
    {
        byte[] envelope = wrapped.ToArray();
        ReadOnlyMemory<byte> plaintext = default;
        try
        {
            PayloadOperationBudget.RequireCurrent();
            plaintext = await _client.DecryptAsync(_options.ComponentName, envelope, address, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (plaintext.Length != 32) { return new(PayloadBackendFailure.ConsistencyMismatch); }
            return new(PayloadBackendFailure.None, plaintext.ToArray(), address, keyRecordSchemaVersion: 2);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            Clear(plaintext);
        }
    }

    // Expiry of the enclosing budget is transient; every other outcome is classified by structural status only, never by message text.
    private static PayloadBackendFailure Classify(Exception exception)
    {
        if (exception is OperationCanceledException or PayloadProtectionStateException) { return PayloadBackendFailure.Unavailable; }
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is RpcException rpc)
            {
                return rpc.StatusCode switch
                {
                    StatusCode.PermissionDenied or StatusCode.Unauthenticated => PayloadBackendFailure.Denied,
                    StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted or StatusCode.Aborted
                        or StatusCode.Cancelled => PayloadBackendFailure.Unavailable,
                    _ => PayloadBackendFailure.ConsistencyMismatch,
                };
            }
        }

        return PayloadBackendFailure.ConsistencyMismatch;
    }

    // SDK-returned buffers reachable as arrays are cleared before ownership is released; SDK-internal and sidecar copies are limitations.
    private static void Clear(ReadOnlyMemory<byte> value)
    {
        if (MemoryMarshal.TryGetArray(value, out ArraySegment<byte> segment)) { CryptographicOperations.ZeroMemory(segment.AsSpan()); }
    }
}
