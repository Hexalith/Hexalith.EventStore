// Approved Dapr Kubernetes cryptography supplement: e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034; sections 2 and 7.
// Test double of the Alpha cryptography API; the production suppression stays confined to the backend source file.
#pragma warning disable DAPR_CRYPTOGRAPHY
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dapr.Client;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>In-memory stand-in for the crypto component behind a substituted DaprClient; it is never custody or qualification evidence.</summary>
/// <remarks>Like Dapr.Client 1.19.0 byte overloads, a failed operation completes with empty output instead of an exception.</remarks>
internal sealed class DaprCryptoTestSidecar
{
    // Header padding makes a 32-byte DEK envelope 666 bytes, the size measured on daprd 1.19.1, so every test crosses the 512-byte V1 bound.
    private const int HeaderPadding = 591;
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>Creates fresh per-address test KEKs and a substituted client.</summary>
    internal DaprCryptoTestSidecar(params string[] addresses)
    {
        foreach (string address in addresses) { _keys[address] = RandomNumberGenerator.GetBytes(32); }
        Client = Substitute.For<DaprClient>();
        Client.EncryptAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<EncryptionOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Respond(true, call.ArgAt<string>(0), call.ArgAt<ReadOnlyMemory<byte>>(1), call.ArgAt<string>(2), call.ArgAt<EncryptionOptions>(3), call.ArgAt<CancellationToken>(4)));
        Client.DecryptAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Respond(false, call.ArgAt<string>(0), call.ArgAt<ReadOnlyMemory<byte>>(1), call.ArgAt<string>(2), null, call.ArgAt<CancellationToken>(3)));
    }

    /// <summary>Gets the substituted client.</summary>
    internal DaprClient Client { get; }
    /// <summary>Gets the expected component name.</summary>
    internal string Component { get; init; } = "kubernetes-crypto";
    /// <summary>Gets every (operation, key name) request, in order.</summary>
    internal List<(bool Encrypt, string KeyName)> Calls { get; } = [];
    /// <summary>Gets every caller-supplied input array, for clearing assertions after the call.</summary>
    internal List<byte[]> Inputs { get; } = [];
    /// <summary>Gets every returned array, for clearing assertions after ownership is released.</summary>
    internal List<byte[]> Outputs { get; } = [];
    /// <summary>Gets or sets an optional fault: return a replacement output or throw instead of the honest response.</summary>
    internal Func<bool, string, byte[], CancellationToken, Task<byte[]?>>? Fault { get; set; }

    /// <summary>Adds an alias address holding exactly the same key bytes, as a test-only override demonstration.</summary>
    internal void Alias(string alias, string address) => _keys[alias] = _keys[address].ToArray();

    /// <summary>Encrypts outside the backend, as another writer would.</summary>
    internal byte[] Seal(string address, ReadOnlySpan<byte> plaintext) => Envelope(_keys[address], plaintext);

    private async Task<ReadOnlyMemory<byte>> Respond(bool encrypt, string component, ReadOnlyMemory<byte> input, string keyName,
        EncryptionOptions? options, CancellationToken token)
    {
        Calls.Add((encrypt, keyName));
        MemoryMarshal.TryGetArray(input, out ArraySegment<byte> segment).ShouldBeTrue();
        Inputs.Add(segment.Array!);
        byte[] copy = input.ToArray();
        if (encrypt)
        {
            options!.KeyWrapAlgorithm.ShouldBe(KeyWrapAlgorithm.RsaOaep256);
            options.EncryptionCipher.ShouldBe(DataEncryptionCipher.AesGcm);
            options.DecryptionKeyName.ShouldBeNull();
        }

        byte[]? faulted = Fault is null ? null : await Fault(encrypt, keyName, copy, token);
        byte[] output = faulted ?? (component != Component || !_keys.TryGetValue(keyName, out byte[]? key) ? []
            : encrypt ? Envelope(key, copy) : Open(key, copy));
        CryptographicOperations.ZeroMemory(copy);
        Outputs.Add(output);
        return output;
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

    private static byte[] Open(byte[] key, byte[] envelope)
    {
        int nonce = WrappedDekV2.SchemePrefix.Length + HeaderPadding;
        int header = nonce + 12;
        if (envelope.Length < header + 16 || !envelope.AsSpan().StartsWith(WrappedDekV2.SchemePrefix)) { return []; }
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
            return [];
        }
    }
}
