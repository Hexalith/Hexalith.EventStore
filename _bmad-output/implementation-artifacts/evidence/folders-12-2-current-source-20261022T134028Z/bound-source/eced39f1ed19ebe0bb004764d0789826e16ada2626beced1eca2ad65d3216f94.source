using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Supplies immutable G-001 inputs and focused core helpers for Story 8.3 tests.
/// </summary>
internal static class TestFixture
{
    /// <summary>Gets the frozen G-001 AAD bytes as lowercase hexadecimal.</summary>
    internal const string AadHex = "4858414401010b0001010000000874656e616e742d610201000000077061727469657303010000000870617274792d303104010000002e486578616c6974682e506172746965732e436f6e7472616374732e4576656e74732e5061727479437265617465640501000000062f656d61696c06010000001a30314a30303030303030303030303030303030303030303030300702000000040000000108010000000d6a736f6e2b7064656e632d7632090200000004000000000a030000000800000000000000010b0400000020c9eb5924af88fb4ae2d03028e0e6365c5a7e9e3c3f8e1bb228be946541e794f8";
    /// <summary>Gets the frozen G-001 ciphertext as lowercase hexadecimal.</summary>
    internal const string CiphertextHex = "2cddd9b7d649c3d870c9c4457449bffabd2e74";
    /// <summary>Gets the frozen G-001 unpadded carrier.</summary>
    internal const string EnvelopeBase64Url = "SFhQMgIBAQEAHAAaAAAAAQAAAAAMEAAAAAAAEzAxSjAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAAAAAAAAALN3Zt9ZJw9hwycRFdEm_-r0udNR5a-23crKl9i2qHDSSAVk";
    /// <summary>Gets the frozen G-001 envelope as lowercase hexadecimal.</summary>
    internal const string EnvelopeHex = "4858503202010101001c001a00000001000000000c1000000000001330314a30303030303030303030303030303030303030303030300000000000000000000000002cddd9b7d649c3d870c9c4457449bffabd2e74d4796bedb772b2a5f62daa1c34920159";
    /// <summary>Gets the frozen G-001 key reference.</summary>
    internal const string KeyReference = "01J00000000000000000000000";
    /// <summary>Gets the frozen G-001 manifest bytes as lowercase hexadecimal.</summary>
    internal const string ManifestHex = "4858504d0100000001000000062f656d61696c";
    /// <summary>Gets the frozen G-001 tag as lowercase hexadecimal.</summary>
    internal const string TagHex = "d4796bedb772b2a5f62daa1c34920159";

    /// <summary>Creates the canonical G-001 event context.</summary>
    internal static PayloadProtectionContext Context(ulong sequence = 1)
        => new(
            new AggregateIdentity("tenant-a", "parties", "party-01"),
            "Hexalith.Parties.Contracts.Events.PartyCreated",
            PayloadProtectionPayloadKind.Event,
            sequence);

    /// <summary>Creates the canonical root-manifest snapshot context.</summary>
    internal static PayloadProtectionContext SnapshotContext(string payloadTypeId = "hx-snapshot-v1:party-state")
        => new(
            new AggregateIdentity("tenant-a", "parties", "party-01"),
            payloadTypeId,
            PayloadProtectionPayloadKind.Snapshot,
            1);

    /// <summary>Creates a fresh copy of the frozen G-001 DEK.</summary>
    internal static byte[] Dek() => [.. Enumerable.Range(0, 32).Select(static value => (byte)value)];

    /// <summary>Creates fresh G-001 cryptographic material.</summary>
    internal static PayloadProtectionMaterial Material() => new(KeyReference, 1, Dek());

    /// <summary>Creates the frozen G-001 plaintext bytes.</summary>
    internal static byte[] Plaintext() => Encoding.UTF8.GetBytes("\"alice@example.com\"");

    /// <summary>Parses a fresh copy of the G-001 envelope.</summary>
    internal static PayloadProtectionEnvelope Envelope() => EnvelopeCodec.Read(Convert.FromHexString(EnvelopeHex));

    /// <summary>Protects the complete G-001 event payload.</summary>
    internal static CoreProtectionResult Protect(ISensitiveBufferObserver? observer = null)
        => new PayloadProtectionCore(observer).ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"alice@example.com\",\"name\":\"Alice\"}"),
            ["/email"],
            Context(),
            Material);

    /// <summary>Unprotects an event through the complete core reader.</summary>
    internal static async ValueTask<CoreUnprotectionResult> UnprotectAsync(
        byte[] payload,
        PayloadProtectionContext? context = null,
        byte[]? dek = null,
        ISensitiveBufferObserver? observer = null,
        CancellationToken cancellationToken = default,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>>? keyResolver = null,
        Action<int>? traversalCheckpoint = null)
        => await new PayloadProtectionCore(observer).TryUnprotectEventAsync(
            payload,
            context ?? Context(),
            keyResolver ?? ((_, _, _) => ValueTask.FromResult<byte[]?>([.. (dek ?? Dek())])),
            cancellationToken,
            traversalCheckpoint);

    /// <summary>Protects one canonical snapshot payload.</summary>
    internal static ProtectedSnapshotPayloadV2 ProtectSnapshot(
        byte[]? payload = null,
        PayloadProtectionContext? context = null,
        Func<PayloadProtectionMaterial>? materialFactory = null,
        ISensitiveBufferObserver? observer = null,
        CancellationToken cancellationToken = default)
        => new PayloadProtectionCore(observer).ProtectSnapshot(
            payload ?? "{\"name\":\"Alice\"}"u8.ToArray(),
            context ?? SnapshotContext(),
            materialFactory ?? Material,
            cancellationToken: cancellationToken);

    /// <summary>Unprotects a snapshot through the complete core reader.</summary>
    internal static async ValueTask<CoreUnprotectionResult> UnprotectSnapshotAsync(
        ProtectedSnapshotPayloadV2 payload,
        PayloadProtectionContext? context = null,
        ISensitiveBufferObserver? observer = null,
        CancellationToken cancellationToken = default,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>>? keyResolver = null)
        => await new PayloadProtectionCore(observer).TryUnprotectSnapshotAsync(
            payload,
            context ?? SnapshotContext(),
            keyResolver ?? ((_, _, _) => ValueTask.FromResult<byte[]?>(Dek())),
            cancellationToken);

    /// <summary>Creates a JSON event containing one G-001 wrapper.</summary>
    internal static string WrapperPayload(string envelope = EnvelopeBase64Url)
        => "{\"email\":{\"$pdenc\":\"" + envelope + "\"},\"name\":\"Alice\"}";

    /// <summary>Creates UTF-8 JSON bytes containing one G-001 wrapper.</summary>
    internal static byte[] WrapperPayloadBytes(string envelope = EnvelopeBase64Url)
        => Encoding.UTF8.GetBytes(WrapperPayload(envelope));

    /// <summary>Writes the canonical G-001 AAD with optional field substitutions.</summary>
    internal static byte[] Aad(
        PayloadProtectionContext? context = null,
        string path = "/email",
        string keyReference = KeyReference,
        uint version = 1,
        uint ordinal = 0,
        byte[]? commitment = null)
        => AadCodec.Write(
            context ?? Context(),
            path,
            keyReference,
            version,
            ordinal,
            commitment ?? SHA256.HashData(Convert.FromHexString(ManifestHex)));

    /// <summary>Reads the event wrapper carrier from a protection result.</summary>
    internal static string ReadWrapper(CoreProtectionResult result)
    {
        using JsonDocument document = JsonDocument.Parse(result.PayloadBytes);
        return document.RootElement.GetProperty("email").GetProperty("$pdenc").GetString()!;
    }

    /// <summary>Opens one linked immutable Story 8.2 fixture.</summary>
    internal static JsonDocument ReadFrozenFixture(string fileName)
        => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "PayloadProtectionV2",
            fileName)));
}
