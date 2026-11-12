// Approved Dapr Kubernetes cryptography supplement: 3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd; sections 2 and 7.
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
using Grpc.Core;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>In-memory stand-in for the crypto component behind a fake gRPC call invoker; it is never custody or qualification evidence.</summary>
/// <remarks>
/// Like daprd, a failed operation ends its call with a non-OK status, and a request that is not exactly one completed sequence-0 message with the
/// Dapr.Client 1.19.0 options ends with InvalidArgument. <see cref="End"/> ends every call at write, at read, mid-stream after partial output, or
/// locally with a recorded status.
/// </remarks>
internal sealed class DaprCryptoTestSidecar : CallInvoker
{
    /// <summary>The write fails as a racing client write does (Cancelled); reading the response raises the call's real status.</summary>
    internal const int AtWrite = 0;
    /// <summary>The call ends with the status before any output.</summary>
    internal const int AtRead = 1;
    /// <summary>The call ends with the status after one partial output chunk.</summary>
    internal const int MidStream = 2;
    /// <summary>The call ends locally, as Grpc.Net.Client ends a failed connection: reading raises OperationCanceledException and the call records the status.</summary>
    internal const int Interrupted = 3;

    // Header padding makes a 32-byte DEK envelope 666 bytes, the size measured on daprd 1.19.1, so every test crosses the 512-byte V1 bound.
    private const int HeaderPadding = 591;
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>Creates fresh per-address test KEKs.</summary>
    internal DaprCryptoTestSidecar(params string[] addresses)
    {
        foreach (string address in addresses) { _keys[address] = RandomNumberGenerator.GetBytes(32); }
    }

    /// <summary>Gets a transport over this invoker, sending <see cref="ApiToken"/> when set and recording every output buffer it allocates.</summary>
    internal DaprCryptographyGrpcTransport Transport => new(this, ApiToken, Allocate);
    /// <summary>Gets the API token the transport sends, if any.</summary>
    internal string? ApiToken { get; init; }
    /// <summary>Gets the expected component name.</summary>
    internal string Component { get; init; } = "kubernetes-crypto";
    /// <summary>Gets every (operation, key name) request, in order.</summary>
    internal List<(bool Encrypt, string KeyName)> Calls { get; } = [];
    /// <summary>Gets every request message, in order.</summary>
    internal List<IMessage> Requests { get; } = [];
    /// <summary>Gets the API token header of every call, in order (null when absent).</summary>
    internal List<string?> Tokens { get; } = [];
    /// <summary>Gets every caller-supplied input array, for clearing assertions after the call.</summary>
    internal List<byte[]> Inputs { get; } = [];
    /// <summary>Gets every delivered response chunk array, for clearing assertions after ownership is released.</summary>
    internal List<byte[]> Outputs { get; } = [];
    /// <summary>Gets every output buffer the transport allocated, for clearing assertions on returned and discarded output.</summary>
    internal List<byte[]> Allocated { get; } = [];
    /// <summary>Gets or sets an optional fault applied when reading: return a replacement output or throw instead of the honest response.</summary>
    internal Func<bool, string, byte[], CancellationToken, Task<byte[]?>>? Fault { get; set; }
    /// <summary>Gets or sets the phase and status that end every call; an OK status after a failed write still returns the honest output.</summary>
    internal (int Phase, StatusCode Status)? End { get; set; }
    /// <summary>Gets or sets the response chunk size; output is delivered as consecutive chunks of at most this many bytes.</summary>
    internal int ChunkSize { get; set; } = int.MaxValue;
    /// <summary>Gets or sets the status detail and message of every fault, for no-leak sentinels.</summary>
    internal string Detail { get; set; } = "";

    /// <summary>Adds an alias address holding exactly the same key bytes, as a test-only override demonstration.</summary>
    internal void Alias(string alias, string address) => _keys[alias] = _keys[address].ToArray();

    /// <summary>Encrypts outside the backend, as another writer would.</summary>
    internal byte[] Seal(string address, ReadOnlySpan<byte> plaintext) => Envelope(_keys[address], plaintext);

    /// <inheritdoc/>
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
        string? host, CallOptions options)
    {
        Tokens.Add(options.Headers?.GetValue(DaprCryptographyGrpcTransport.ApiTokenHeader));
        bool encrypt = method.FullName switch
        {
            "/dapr.proto.runtime.v1.Dapr/EncryptAlpha1" => true,
            "/dapr.proto.runtime.v1.Dapr/DecryptAlpha1" => false,
            _ => throw new NotSupportedException(),
        };
        Func<byte[], ulong, TResponse> response = encrypt
            ? (data, seq) => (TResponse)(object)new EncryptResponse { Payload = new StreamPayload { Data = UnsafeByteOperations.UnsafeWrap(data), Seq = seq } }
            : (data, seq) => (TResponse)(object)new DecryptResponse { Payload = new StreamPayload { Data = UnsafeByteOperations.UnsafeWrap(data), Seq = seq } };
        var call = new DaprCryptoTestStream<TRequest, TResponse>(this, encrypt, response);
        return new(call, call, Task.FromResult(new Metadata()), () => call.Status, () => new Metadata(), () => { });
    }

    /// <inheritdoc/>
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host,
        CallOptions options, TRequest request)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
        string? host, CallOptions options)
        => throw new NotSupportedException();

    /// <summary>Records one request message; an at-write fault then fails the write as a racing client write does.</summary>
    internal void Written(bool encrypt, IMessage message)
    {
        Requests.Add(message);
        StreamPayload? payload = (message as EncryptRequest)?.Payload ?? (message as DecryptRequest)?.Payload;
        string keyName = (message as EncryptRequest)?.Options?.KeyName ?? (message as DecryptRequest)?.Options?.KeyName ?? "";
        Calls.Add((encrypt, keyName));
        if (payload is not null)
        {
            MemoryMarshal.TryGetArray(payload.Data.Memory, out ArraySegment<byte> segment).ShouldBeTrue();
            Inputs.Add(segment.Array!);
        }

        if (End is { Phase: AtWrite }) { throw new RpcException(new Status(StatusCode.Cancelled, Detail), Detail); }
    }

    /// <summary>Computes one call's response chunks and terminal exception, if any.</summary>
    internal async Task<(IReadOnlyList<byte[]> Chunks, Exception? End)> RespondAsync(bool encrypt, IReadOnlyList<IMessage> messages, bool completed,
        CancellationToken token)
    {
        Exception? end = End is { Status: not StatusCode.OK } ended ? new RpcException(new Status(ended.Status, Detail), Detail) : null;
        if (End is { Phase: AtWrite or AtRead } && end is not null) { return ([], end); }
        if (End is { Phase: Interrupted } && end is not null) { return ([], new OperationCanceledException(Detail, end)); }
        if (End is { Phase: MidStream } && end is not null)
        {
            byte[] partial = encrypt ? [.. WrappedDekV2.SchemePrefix, .. Enumerable.Repeat((byte)'p', 49)] : Enumerable.Repeat((byte)0xA5, 16).ToArray();
            return ([partial], end);
        }

        // The Dapr.Client 1.19.0 request: one completed sequence-0 message carrying the options.
        StreamPayload? payload = null;
        string component = "", keyName = "";
        // After an at-write fault with an OK status, the server is modeled as having received the whole request anyway.
        bool exact = (completed || End is { Phase: AtWrite }) && messages.Count == 1;
        if (exact && encrypt && messages[0] is EncryptRequest { Options: { } e } encryptRequest)
        {
            exact = e is { KeyWrapAlgorithm: "RSA-OAEP-256", DataEncryptionCipher: "aes-gcm", OmitDecryptionKeyName: true, DecryptionKeyName: "" };
            (payload, component, keyName) = (encryptRequest.Payload, e.ComponentName, e.KeyName);
        }
        else if (exact && !encrypt && messages[0] is DecryptRequest { Options: { } d } decryptRequest)
        {
            (payload, component, keyName) = (decryptRequest.Payload, d.ComponentName, d.KeyName);
        }

        if (!exact || payload is not { Seq: 0 }) { return ([], Failed(StatusCode.InvalidArgument)); }
        byte[] copy = payload.Data.ToByteArray();
        try
        {
            byte[]? faulted = Fault is null ? null : await Fault(encrypt, keyName, copy, token);
            byte[]? output = faulted;
            if (output is null)
            {
                if (component != Component) { return ([], Failed(StatusCode.InvalidArgument)); }
                if (!_keys.TryGetValue(keyName, out byte[]? key)) { return ([], Failed(StatusCode.Internal)); }
                output = encrypt ? Envelope(key, copy) : Open(key, copy);
                if (output is null) { return ([], Failed(StatusCode.Internal)); }
            }

            return (Chunk(output), null);
        }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }

    private RpcException Failed(StatusCode status) => new(new Status(status, Detail), Detail);

    private byte[] Allocate(int length)
    {
        byte[] buffer = new byte[length];
        Allocated.Add(buffer);
        return buffer;
    }

    // Each chunk is its own array, so the clearing of every delivered protobuf buffer is observable; the whole output array is then cleared.
    private List<byte[]> Chunk(byte[] output)
    {
        var chunks = new List<byte[]>();
        for (int offset = 0; offset < output.Length; offset += ChunkSize) { chunks.Add(output.AsSpan(offset, Math.Min(ChunkSize, output.Length - offset)).ToArray()); }
        if (output.Length == 0) { chunks.Add([]); }
        CryptographicOperations.ZeroMemory(output);
        return chunks;
    }

    private static byte[] Envelope(byte[] key, ReadOnlySpan<byte> plaintext)
    {
        int header = WrappedDekV2.SchemePrefix.Length + HeaderPadding;
        byte[] envelope = new byte[header + 12 + plaintext.Length + 16];
        WrappedDekV2.SchemePrefix.CopyTo(envelope);
        envelope.AsSpan(WrappedDekV2.SchemePrefix.Length, HeaderPadding).Fill((byte)'h');
        Span<byte> nonce = envelope.AsSpan(header, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, 16);
        // Like Dapr's MAC-authenticated header, the prefix and padding are associated data, so header tampering fails decryption.
        aes.Encrypt(nonce, plaintext, envelope.AsSpan(header + 12, plaintext.Length), envelope.AsSpan(envelope.Length - 16), envelope.AsSpan(0, header));
        return envelope;
    }

    private static byte[]? Open(byte[] key, byte[] envelope)
    {
        int nonce = WrappedDekV2.SchemePrefix.Length + HeaderPadding;
        int header = nonce + 12;
        if (envelope.Length < header + 16 || !envelope.AsSpan().StartsWith(WrappedDekV2.SchemePrefix)) { return null; }
        byte[] plaintext = new byte[envelope.Length - header - 16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(envelope.AsSpan(nonce, 12), envelope.AsSpan(header, plaintext.Length), envelope.AsSpan(envelope.Length - 16), plaintext, envelope.AsSpan(0, nonce));
            return plaintext;
        }
        catch (AuthenticationTagMismatchException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return null;
        }
    }
}
