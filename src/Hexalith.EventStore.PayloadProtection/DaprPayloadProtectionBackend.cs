// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>One-attempt neutral Dapr transport; current authority is mandatory and Dapr receipt never proves native completion.</summary>
internal sealed class DaprPayloadProtectionBackend : PayloadProtectionBackend
{
    private readonly DaprClient _client;
    private readonly PlatformCustodyPayloadProtectionOptions _options;
    private readonly PayloadKeyBackendTransportAuthority _authority;
    private readonly IPayloadProtectionEntropy _entropy;
    private readonly TimeProvider _clock;

    /// <summary>Resolves one installed logical binding without constructing any provider client or default authority.</summary>
    internal DaprPayloadProtectionBackend(DaprClient client, PlatformCustodyPayloadProtectionOptions options,
        PayloadKeyBackendTransportAuthority authority, IPayloadProtectionEntropy? entropy = null, TimeProvider? timeProvider = null)
    {
        options.Validate();
        _client = client; _options = options; _authority = authority; _entropy = entropy ?? new CryptographicPayloadProtectionEntropy();
        _clock = timeProvider ?? TimeProvider.System;
        EndpointFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(options.BindingName + ":" + options.InstallationId + ":" + options.ProfileSha256)));
    }

    /// <summary>Gets the closed selected backend identity.</summary>
    internal override string BackendId => "platform-custody-rsa-software-v1";
    /// <summary>Gets the full hash used solely to isolate breaker state.</summary>
    internal override string EndpointFingerprint { get; }

    /// <summary>Freshly authenticates the software profile and selects its explicit current version before any wrap.</summary>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
        => RunBudgetAsync(async token =>
        {
            if (dek.Length != 32) { return new(PayloadBackendFailure.ConsistencyMismatch); }
            using var request = Request(PayloadKeyBackendOperation.Profile, operationId);
            using PayloadKeyBackendResponse? response = await InvokeAsync(request, token).ConfigureAwait(false);
            if (response is null) { return new(PayloadBackendFailure.Unavailable); }
            if (response.Outcome != PayloadKeyBackendOutcome.Succeeded) { return Failure(response); }
            return await KeyOperationAsync(PayloadKeyBackendOperation.Wrap, operationId, response.Profile!.CurrentVersionId, dek, token).ConfigureAwait(false);
        }, PayloadBackendFailure.Ambiguous, cancellationToken);

    /// <summary>Preserves the retained exact rewrap target through rotation races.</summary>
    internal override ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
        => RunBudgetAsync(token => KeyOperationAsync(PayloadKeyBackendOperation.WrapExact, operationId, providerKeyVersionId, dek, token),
            PayloadBackendFailure.Ambiguous, cancellationToken);

    /// <summary>Unwraps only the historical exact recorded version and bounded RSA ciphertext.</summary>
    internal override ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId,
        ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken)
        => RunBudgetAsync(token => KeyOperationAsync(PayloadKeyBackendOperation.Unwrap, operationId, providerKeyVersionId, wrappedDek, token),
            PayloadBackendFailure.Unavailable, cancellationToken);

    /// <summary>Looks up exact original evidence only; missing-key responses cannot stand in for original-operation absence.</summary>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken)
        => RunBudgetAsync(async token =>
        {
            using var request = Request(PayloadKeyBackendOperation.Lookup, operationId);
            using PayloadKeyBackendResponse? response = await InvokeAsync(request, token).ConfigureAwait(false);
            // Lookup never delivers key bytes. Confirmed existence without wrapped output remains pending.
            if (response is not null && response.Outcome == PayloadKeyBackendOutcome.Missing
                && await _authority.ConfirmsOriginalMutationAbsentAsync(operationId, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
                return new(PayloadBackendFailure.Missing);
            }
            return new(PayloadBackendFailure.Ambiguous);
        }, PayloadBackendFailure.Ambiguous, cancellationToken);

    private async ValueTask<PayloadBackendResult> RunBudgetAsync(Func<CancellationToken, ValueTask<PayloadBackendResult>> operation,
        PayloadBackendFailure uncertain, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        catch
        {
            result?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            return new(uncertain);
        }
    }

    private async ValueTask<PayloadBackendResult> KeyOperationAsync(PayloadKeyBackendOperation operation, string operationId,
        string version, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (input.Length != (operation == PayloadKeyBackendOperation.Unwrap ? 384 : 32)) { return new(PayloadBackendFailure.ConsistencyMismatch); }
        using var request = Request(operation, operationId, version, input.ToArray());
        using PayloadKeyBackendResponse? response = await InvokeAsync(request, token).ConfigureAwait(false);
        if (response is null) { return new(operation == PayloadKeyBackendOperation.Unwrap ? PayloadBackendFailure.Unavailable : PayloadBackendFailure.Ambiguous); }
        if (response.Outcome != PayloadKeyBackendOutcome.Succeeded) { return Failure(response); }
        return new(PayloadBackendFailure.None, response.Bytes!.ToArray(), response.ProviderKeyVersionId);
    }

    private PayloadKeyBackendRequest Request(PayloadKeyBackendOperation operation, string operationId, string? version = null, byte[]? bytes = null)
    {
        try { return new(1, operation, operationId, _entropy.CreateKeyReference(), _options.InstallationId, _options.ProfileSha256, version, bytes); }
        catch
        {
            if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
            throw;
        }
    }

    private async ValueTask<PayloadKeyBackendResponse?> InvokeAsync(PayloadKeyBackendRequest request, CancellationToken token)
    {
        byte[]? encoded = null;
        byte[]? received = null;
        BindingResponse? transport = null;
        PayloadKeyBackendResponse? response = null;
        bool sent = false;
        bool transferred = false;
        try
        {
            token.ThrowIfCancellationRequested();
            PayloadOperationBudget.RequireCurrent();
            encoded = PayloadKeyBackendCodec.EncodeRequest(request);
            IReadOnlyDictionary<string, string>? metadata = await _authority.PrepareAsync(request, encoded, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (!ValidMetadata(metadata, request.Operation)) { return Refused(request, encoded); }
            var binding = new BindingRequest(_options.BindingName, "payload-key-v1") { Data = encoded };
            foreach (var pair in metadata!) { binding.Metadata.Add(pair.Key, pair.Value); }
            sent = true;
            // Await actual I/O completion even after cancellation before clearing any request/response owner.
            transport = await _client.InvokeBindingAsync(binding, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (transport.Data.Length is < 2 or > PayloadKeyBackendCodec.MaximumMessageBytes) { throw new PayloadKeyBackendProtocolException(); }
            received = transport.Data.ToArray();
            response = PayloadKeyBackendCodec.DecodeResponse(received);
            PayloadKeyBackendCodec.ValidateBinding(request, encoded, response);
            bool authenticated = await _authority.ValidateResponseAsync(request, encoded, response, received, transport.Metadata, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
            if (!authenticated) { throw new PayloadKeyBackendProtocolException(); }
            transferred = true;
            return response;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (PayloadKeyBackendProtocolException) { return Consistency(request, encoded); }
        catch
        {
            token.ThrowIfCancellationRequested();
            // No exception status/message can establish native absence after dispatch.
            return sent ? null : Refused(request, encoded);
        }
        finally
        {
            if (!transferred) { response?.Dispose(); }
            if (encoded is not null) { CryptographicOperations.ZeroMemory(encoded); }
            if (received is not null) { CryptographicOperations.ZeroMemory(received); }
            if (transport is not null && MemoryMarshal.TryGetArray(transport.Data, out ArraySegment<byte> segment))
            { CryptographicOperations.ZeroMemory(segment.AsSpan()); }
        }
    }

    private static bool ValidMetadata(IReadOnlyDictionary<string, string>? metadata, PayloadKeyBackendOperation operation)
        => metadata is { Count: >= 2 and <= 16 } && metadata.ContainsKey("custody-operation-proof") && metadata.ContainsKey("custody-control-proof")
            && (operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup || metadata.ContainsKey("custody-key-admission"))
            && metadata.All(p => p.Key.Length is >= 1 and <= 128 && p.Value.Length is >= 1 and <= 8192)
            && metadata.Sum(p => (long)Encoding.UTF8.GetByteCount(p.Key) + Encoding.UTF8.GetByteCount(p.Value)) <= 16384;

    private static PayloadKeyBackendResponse Refused(PayloadKeyBackendRequest request, byte[]? encoded)
        => Outcome(request, encoded, PayloadKeyBackendOutcome.Unavailable);
    private static PayloadKeyBackendResponse Consistency(PayloadKeyBackendRequest request, byte[]? encoded)
        => Outcome(request, encoded, PayloadKeyBackendOutcome.ConsistencyMismatch);
    private static PayloadKeyBackendResponse Outcome(PayloadKeyBackendRequest request, byte[]? encoded, PayloadKeyBackendOutcome outcome)
        => new(1, request.Operation, request.OperationId, request.RequestId, request.InstallationId, request.ProfileSha256,
            Convert.ToHexStringLower(SHA256.HashData(encoded ?? [])), outcome);
    private static PayloadBackendResult Failure(PayloadKeyBackendResponse response)
        => new(response.Outcome switch
        {
            PayloadKeyBackendOutcome.Unavailable => PayloadBackendFailure.Unavailable,
            PayloadKeyBackendOutcome.Denied => PayloadBackendFailure.Denied,
            PayloadKeyBackendOutcome.Missing => PayloadBackendFailure.Missing,
            PayloadKeyBackendOutcome.Invalidated => PayloadBackendFailure.Invalidated,
            PayloadKeyBackendOutcome.ConsistencyMismatch => PayloadBackendFailure.ConsistencyMismatch,
            _ => PayloadBackendFailure.Ambiguous,
        }, retryAfter: response.RetryAfterMilliseconds is int delay ? TimeSpan.FromMilliseconds(delay) : null);
}
