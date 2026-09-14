using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers inherited V001-V003 and cryptographic mutation vectors V010-V017.
/// </summary>
public sealed class CryptographyTests {
    /// <summary>V001 reproduces every G-001 core artifact from atomic fields.</summary>
    [Fact]
    [Trait("Vector", "V001")]
    public void V001_G001Encryption_MatchesFrozenBytes() {
        ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(["/email"]);
        Convert.ToHexString(manifest.Encoded).ToLowerInvariant().ShouldBe(TestFixture.ManifestHex);
        byte[] aad = TestFixture.Aad(commitment: manifest.Commitment);
        Convert.ToHexString(aad).ToLowerInvariant().ShouldBe(TestFixture.AadHex);
        PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
            TestFixture.Plaintext(), aad, TestFixture.Dek(), TestFixture.KeyReference, 1, 0);
        Convert.ToHexString(envelope.Ciphertext).ToLowerInvariant().ShouldBe(TestFixture.CiphertextHex);
        Convert.ToHexString(envelope.Tag).ToLowerInvariant().ShouldBe(TestFixture.TagHex);
        byte[] encoded = EnvelopeCodec.Write(envelope);
        Convert.ToHexString(encoded).ToLowerInvariant().ShouldBe(TestFixture.EnvelopeHex);
        Base64UrlCodec.Encode(encoded).ShouldBe(TestFixture.EnvelopeBase64Url);
        TestFixture.ReadWrapper(TestFixture.Protect()).ShouldBe(TestFixture.EnvelopeBase64Url);
    }

    /// <summary>V002 returns the complete G-001 plaintext only after tag verification.</summary>
    [Fact]
    [Trait("Vector", "V002")]
    public async Task V002_G001Decryption_ReturnsCompleteAuthenticatedPayloadAsync() {
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes());
        result.IsReadable.ShouldBeTrue();
        Encoding.UTF8.GetString(result.PayloadBytes!).ShouldBe("{\"email\":\"alice@example.com\",\"name\":\"Alice\"}");
    }

    /// <summary>V003 reproduces the named NIST CAVP AES-256-GCM Count 0 tag.</summary>
    [Fact]
    [Trait("Vector", "V003")]
    public void V003_NistAes256GcmCountZero_MatchesFrozenTag() {
        byte[] key = Convert.FromHexString("b52c505a37d78eda5dd34f20c22540ea1b58963cf8e5bf8ffa85f9f2492505b4");
        byte[] nonce = Convert.FromHexString("516c33929df5a3284ff463d7");
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, ReadOnlySpan<byte>.Empty);
        Convert.ToHexString(tag).ToLowerInvariant().ShouldBe("bdc1ac884d332457a1d2664f168c76f0");
    }

    /// <summary>V010 rejects every independently flipped nonce bit.</summary>
    [Fact]
    [Trait("Vector", "V010")]
    public void V010_NonceBitMatrix_AuthenticatesEveryBit() {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < 96; bit++) {
            byte[] nonce = baseline.Nonce.ToArray();
            nonce[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            var mutated = baseline with { Nonce = nonce };
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(mutated, TestFixture.Aad(), TestFixture.Dek()));
        }
    }

    /// <summary>V011 rejects every independently flipped tag bit.</summary>
    [Fact]
    [Trait("Vector", "V011")]
    public void V011_TagBitMatrix_AuthenticatesEveryBit() {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < 128; bit++) {
            byte[] tag = baseline.Tag.ToArray();
            tag[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(baseline with { Tag = tag }, TestFixture.Aad(), TestFixture.Dek()));
        }
    }

    /// <summary>V012 rejects every independently flipped G-001 ciphertext bit.</summary>
    [Fact]
    [Trait("Vector", "V012")]
    public void V012_CiphertextBitMatrix_AuthenticatesEveryBit() {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < baseline.Ciphertext.Length * 8; bit++) {
            byte[] ciphertext = baseline.Ciphertext.ToArray();
            ciphertext[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(baseline with { Ciphertext = ciphertext }, TestFixture.Aad(), TestFixture.Dek()));
        }
    }

    /// <summary>V013 rejects algorithm substitution locally.</summary>
    [Fact]
    [Trait("Vector", "V013")]
    public async Task V013_AlgorithmSubstitution_IsRejectedLocallyWithoutLookupAsync() {
        byte[] bytes = Convert.FromHexString(TestFixture.EnvelopeHex);
        bytes[5] = 2;
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(bytes));
        await AssertLocalMismatchWithoutLookupAsync(bytes);
    }

    /// <summary>V014 rejects envelope version substitution locally.</summary>
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    [Trait("Vector", "V014")]
    public async Task V014_VersionSubstitution_IsRejectedLocallyWithoutLookupAsync(byte version) {
        byte[] bytes = Convert.FromHexString(TestFixture.EnvelopeHex);
        bytes[4] = version;
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(bytes));
        await AssertLocalMismatchWithoutLookupAsync(bytes);
    }

    /// <summary>V015 authenticates a canonical substituted key reference after exact lookup.</summary>
    [Fact]
    [Trait("Vector", "V015")]
    public async Task V015_KeyReferenceSubstitution_FailsAuthenticationAsync() {
        const string substitutedReference = "01J00000000000000000000001";
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with { KeyReference = substitutedReference };
        string wrapper = Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope));
        var lookups = new List<(string KeyReference, uint Version)>();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(wrapper),
            keyResolver: (keyReference, version, _) => {
                lookups.Add((keyReference, version));
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        lookups.ShouldBe([(substitutedReference, (uint)1)]);
    }

    /// <summary>V016 authenticates independently changed DEK-version and ordinal fields.</summary>
    [Theory]
    [InlineData((uint)2, (uint)0)]
    [InlineData((uint)1, (uint)1)]
    [Trait("Vector", "V016")]
    public async Task V016_KeyVersionAndOrdinalSubstitution_FailsAuthenticationAsync(uint version, uint ordinal) {
        byte[] nonce = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), ordinal);
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with {
            DekVersion = version,
            FieldOrdinal = ordinal,
            Nonce = nonce,
        };
        var lookups = new List<(string KeyReference, uint Version)>();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope))),
            keyResolver: (keyReference, resolvedVersion, _) => {
                lookups.Add((keyReference, resolvedVersion));
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        if (ordinal == 0) {
            lookups.ShouldBe([(TestFixture.KeyReference, version)]);
        }
        else {
            lookups.ShouldBeEmpty();
        }
    }

    /// <summary>V017 proves every one of the eleven encoded AAD fields is independently authenticated.</summary>
    [Theory]
    [InlineData(14)]
    [InlineData(28)]
    [InlineData(41)]
    [InlineData(55)]
    [InlineData(107)]
    [InlineData(119)]
    [InlineData(151)]
    [InlineData(161)]
    [InlineData(180)]
    [InlineData(193)]
    [InlineData(215)]
    [Trait("Vector", "V017")]
    public void V017_AadFieldMatrix_AuthenticatesEveryField(int byteOffset) {
        byte[] aad = TestFixture.Aad();
        aad[byteOffset] ^= 1;
        Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()));
    }

    private static async Task AssertLocalMismatchWithoutLookupAsync(byte[] envelope) {
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(envelope)),
            keyResolver: (_, _, _) => {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }
}
