// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; sections 2 and 7.
// Profile authority: 29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44.
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dapr;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using DaprGrpcClient = Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Calls the generated Dapr.Protos 1.19.0 EncryptAlpha1/DecryptAlpha1 client on the calling task, so each call's gRPC status is its outcome.</summary>
/// <remarks>
/// It starts no background task and has no retry policy. Each call writes its whole input as one sequence-0 message, completes the request stream and
/// reads the response stream to its end before it returns. Output received before a non-OK status is discarded and cleared, and every received
/// protobuf buffer is cleared once copied. The request message (options and payload) is the one Dapr.Client 1.19.0 EncryptAsync/DecryptAsync send.
/// </remarks>
internal sealed class DaprCryptographyGrpcTransport : IDisposable
{
    /// <summary>Gets the header Dapr.Client 1.19.0 sends the sidecar API token in.</summary>
    internal const string ApiTokenHeader = "dapr-api-token";
    /// <summary>Gets the largest accepted Encrypt output: the closed WrappedDekV2 envelope bound.</summary>
    internal const int MaximumEncryptOutputBytes = WrappedDekV2.MaximumEnvelopeBytes;
    /// <summary>Gets the largest accepted Decrypt output; one byte over a DEK, so the backend's exact-length check stays observable.</summary>
    internal const int MaximumDecryptOutputBytes = 33;
    private readonly DaprGrpcClient _client;
    private readonly string? _apiToken;
    private readonly GrpcChannel? _channel;
    private readonly Func<int, byte[]> _allocate;

    /// <summary>Creates a transport over a caller-owned invoker, sending the API token header only when a token is supplied.</summary>
    /// <remarks>The optional allocator supplies each call's output buffer, so tests can observe that every buffer is cleared.</remarks>
    internal DaprCryptographyGrpcTransport(CallInvoker invoker, string? apiToken = null, Func<int, byte[]>? allocate = null)
        : this(invoker, apiToken, channel: null, allocate)
    {
    }

    private DaprCryptographyGrpcTransport(CallInvoker invoker, string? apiToken, GrpcChannel? channel, Func<int, byte[]>? allocate)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        _client = new DaprGrpcClient(invoker);
        // Like Dapr.Client 1.19.0, a missing or blank token sends no header.
        _apiToken = string.IsNullOrWhiteSpace(apiToken) ? null : apiToken;
        _channel = channel;
        _allocate = allocate ?? (static length => new byte[length]);
    }

    /// <summary>Gets the owned channel's target, or null when the invoker is caller-owned.</summary>
    internal string? Target => _channel?.Target;

    /// <summary>Resolves the sidecar gRPC endpoint and API token exactly as Dapr.Client 1.19.0 does, and owns the resulting channel.</summary>
    /// <remarks>
    /// Configuration values take precedence over the DAPR_GRPC_ENDPOINT, DAPR_GRPC_PORT and DAPR_API_TOKEN environment variables. As with
    /// Dapr.Client, an endpoint whose scheme is not http or https is refused (here by GrpcChannel itself, with an InvalidOperationException).
    /// </remarks>
    internal static DaprCryptographyGrpcTransport Create(IConfiguration? configuration = null)
    {
        string endpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
        string apiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
        // Dapr.Client 1.19.0 builds its channel with exactly these options. No service config is set, so no retry or hedging policy exists.
        GrpcChannel channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions { ThrowOperationCanceledOnCancellation = true });
        try { return new DaprCryptographyGrpcTransport(channel.CreateCallInvoker(), apiToken, channel, allocate: null); }
        catch { channel.Dispose(); throw; }
    }

    /// <summary>Encrypts with the request message Dapr.Client 1.19.0 EncryptAsync sends; the returned array-backed output belongs to the caller.</summary>
    internal async ValueTask<ReadOnlyMemory<byte>> EncryptAsync(string componentName, string keyName, ReadOnlyMemory<byte> plaintext, CancellationToken token)
    {
        var request = new EncryptRequest
        {
            Options = new EncryptRequestOptions
            {
                ComponentName = componentName,
                KeyName = keyName,
                KeyWrapAlgorithm = "RSA-OAEP-256",
                DataEncryptionCipher = "aes-gcm",
                OmitDecryptionKeyName = true,
            },
            // The caller's buffer is wrapped, not copied, so the caller's clearing covers the only managed request copy.
            Payload = new StreamPayload { Data = UnsafeByteOperations.UnsafeWrap(plaintext), Seq = 0 },
        };
        using AsyncDuplexStreamingCall<EncryptRequest, EncryptResponse> call = _client.EncryptAlpha1(Options(token));
        return await ExchangeAsync(call, request, static response => response.Payload, MaximumEncryptOutputBytes, token).ConfigureAwait(false);
    }

    /// <summary>Decrypts with the request message Dapr.Client 1.19.0 DecryptAsync sends; the returned array-backed output belongs to the caller.</summary>
    internal async ValueTask<ReadOnlyMemory<byte>> DecryptAsync(string componentName, string keyName, ReadOnlyMemory<byte> envelope, CancellationToken token)
    {
        var request = new DecryptRequest
        {
            Options = new DecryptRequestOptions { ComponentName = componentName, KeyName = keyName },
            Payload = new StreamPayload { Data = UnsafeByteOperations.UnsafeWrap(envelope), Seq = 0 },
        };
        using AsyncDuplexStreamingCall<DecryptRequest, DecryptResponse> call = _client.DecryptAlpha1(Options(token));
        return await ExchangeAsync(call, request, static response => response.Payload, MaximumDecryptOutputBytes, token).ConfigureAwait(false);
    }

    /// <summary>Builds per-call options: the caller's token and, when configured, the API token header.</summary>
    internal CallOptions Options(CancellationToken token)
        => new(_apiToken is null ? null : new Metadata { { ApiTokenHeader, _apiToken } }, cancellationToken: token);

    /// <summary>Disposes the owned channel, if any.</summary>
    public void Dispose() => _channel?.Dispose();

    private async ValueTask<ReadOnlyMemory<byte>> ExchangeAsync<TRequest, TResponse>(AsyncDuplexStreamingCall<TRequest, TResponse> call,
        TRequest request, Func<TResponse, StreamPayload?> payload, int maximum, CancellationToken token)
        where TRequest : class
        where TResponse : class
    {
        byte[] output = _allocate(maximum);
        int length = 0;
        bool returned = false;
        try
        {
            bool delivered;
            try
            {
                await call.RequestStream.WriteAsync(request, token).ConfigureAwait(false);
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                delivered = true;
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // A failed write is never the outcome. The call's own status, which reading the response stream raises below, is.
                delivered = false;
            }

            try
            {
                while (await call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                {
                    ReadOnlyMemory<byte> data = payload(call.ResponseStream.Current)?.Data.Memory ?? default;
                    try
                    {
                        if (data.Length > maximum - length) { throw new PayloadProtectionFormatException(); }
                        data.Span.CopyTo(output.AsSpan(length));
                        length += data.Length;
                    }
                    finally { Clear(data); }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && Failed(call) is Status status)
            {
                // The call ended itself, not the caller: Grpc.Net.Client then cancels its own reader, and the call's recorded status is the outcome.
                throw new RpcException(status);
            }

            // An OK status for a request that was never delivered is contradictory, so nothing that arrived is trusted.
            if (!delivered) { throw new PayloadProtectionFormatException(); }
            returned = true;
            return output.AsMemory(0, length);
        }
        finally
        {
            if (!returned) { CryptographicOperations.ZeroMemory(output); }
        }
    }

    private static Status? Failed<TRequest, TResponse>(AsyncDuplexStreamingCall<TRequest, TResponse> call)
    {
        try
        {
            Status status = call.GetStatus();
            return status.StatusCode == StatusCode.OK ? null : status;
        }
        catch (InvalidOperationException)
        {
            // The call has not recorded a status yet.
            return null;
        }
    }

    private static void Clear(ReadOnlyMemory<byte> value)
    {
        if (MemoryMarshal.TryGetArray(value, out ArraySegment<byte> segment)) { CryptographicOperations.ZeroMemory(segment.AsSpan()); }
    }
}
