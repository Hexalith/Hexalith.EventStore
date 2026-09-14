using System.Buffers.Binary;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers inherited envelope goldens and Story 8.3 vectors V004-V009.
/// </summary>
public sealed class EnvelopeTests {
    /// <summary>V004 flips every fixed-header bit and obtains only a bounded mismatch.</summary>
    [Fact]
    [Trait("Vector", "V004")]
    public async Task V004_HeaderBitMatrix_IsAlwaysRejectedOrAuthenticatedAsMismatchAsync() {
        byte[] original = Convert.FromHexString(TestFixture.EnvelopeHex);
        for (int offset = 0; offset < PayloadProtectionLimits.HeaderBytes; offset++) {
            for (int bit = 0; bit < 8; bit++) {
                byte[] mutated = original.ToArray();
                mutated[offset] ^= checked((byte)(1 << bit));
                int resolverCalls = 0;
                CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
                    TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(mutated)),
                    keyResolver: (_, _, _) => {
                        resolverCalls++;
                        return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                    });
                result.IsReadable.ShouldBeFalse($"offset={offset}, bit={bit}");
                result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
                int expectedResolverCalls = offset is >= 12 and <= 15 && BinaryPrimitives.ReadUInt32BigEndian(mutated.AsSpan(12)) != 0
                    ? 1
                    : 0;
                resolverCalls.ShouldBe(expectedResolverCalls, $"offset={offset}, bit={bit}");
            }
        }
    }

    /// <summary>V005 rejects every truncation of the fixed G-001 envelope.</summary>
    [Fact]
    [Trait("Vector", "V005")]
    public async Task V005_TruncationMatrix_RejectsEveryBoundaryWithoutLookupAsync() {
        byte[] original = Convert.FromHexString(TestFixture.EnvelopeHex);
        for (int length = 0; length < original.Length; length++) {
            byte[] truncated = original.AsSpan(0, length).ToArray();
            Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(truncated));
            await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(truncated));
        }
    }

    /// <summary>V006 rejects a trailing byte.</summary>
    [Fact]
    [Trait("Vector", "V006")]
    public async Task V006_TrailingByte_IsRejectedWithoutLookupAsync() {
        byte[] appended = [.. Convert.FromHexString(TestFixture.EnvelopeHex), 0];
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(appended));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(appended));
    }

    /// <summary>V007 rejects forbidden, padded, impossible, and non-canonical base64url spellings.</summary>
    [Theory]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("/")]
    [InlineData("A")]
    [InlineData("AA==")]
    [InlineData("AA A")]
    [InlineData("_x")]
    [Trait("Vector", "V007")]
    public async Task V007_Base64UrlSpelling_IsStrictAndPerformsNoLookupAsync(string value) {
        Should.Throw<PayloadProtectionFormatException>(() => Base64UrlCodec.Decode(value));
        await AssertLocalCarrierMismatchAsync(value);
    }

    /// <summary>V008 rejects every closed identifier and reserved-flag mutation.</summary>
    [Theory]
    [InlineData(4, (byte)0x02, true)]
    [InlineData(4, (byte)0x03, false)]
    [InlineData(4, (byte)0xff, false)]
    [InlineData(5, (byte)0x02, false)]
    [InlineData(5, (byte)0xff, false)]
    [InlineData(6, (byte)0x02, false)]
    [InlineData(6, (byte)0xff, false)]
    [InlineData(7, (byte)0x02, false)]
    [InlineData(7, (byte)0xff, false)]
    [InlineData(22, (byte)0x02, false)]
    [InlineData(22, (byte)0xff, false)]
    [InlineData(23, (byte)0x02, false)]
    [InlineData(23, (byte)0xff, false)]
    [Trait("Vector", "V008")]
    public async Task V008_ClosedIdentifiers_ExerciseRepresentativesWithoutInvalidLookupAsync(
        int offset,
        byte value,
        bool supported) {
        byte[] mutated = Convert.FromHexString(TestFixture.EnvelopeHex);
        mutated[offset] = value;
        if (supported) {
            EnvelopeCodec.Write(EnvelopeCodec.Read(mutated)).ShouldBe(mutated);
            return;
        }

        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(mutated));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(mutated));
    }

    /// <summary>V009 validates zero, exact maximum, maximum-plus-one, uint maximum, and actual-length disagreement.</summary>
    [Fact]
    [Trait("Vector", "V009")]
    public async Task V009_CiphertextLengthBoundaries_AreCheckedBeforeSlicingOrLookupAsync() {
        byte[] baseline = Convert.FromHexString(TestFixture.EnvelopeHex);
        foreach (uint invalid in new uint[] { 0, 18, 20, 1_048_576, 1_048_577, uint.MaxValue }) {
            byte[] mutated = baseline.ToArray();
            BinaryPrimitives.WriteUInt32BigEndian(mutated.AsSpan(24), invalid);
            Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(mutated));
            await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(mutated));
        }

        byte[] actualMinusOne = baseline[..^1];
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(actualMinusOne));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(actualMinusOne));

        byte[] actualPlusOne = [.. baseline, 0];
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(actualPlusOne));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(actualPlusOne));

        var maximum = new PayloadProtectionEnvelope(
            TestFixture.KeyReference,
            1,
            0,
            new byte[12],
            new byte[PayloadProtectionLimits.CiphertextBytes],
            new byte[16]);
        EnvelopeCodec.Read(EnvelopeCodec.Write(maximum)).Ciphertext.Length.ShouldBe(PayloadProtectionLimits.CiphertextBytes);
    }

    private static async Task AssertLocalCarrierMismatchAsync(string envelope) {
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(envelope),
            keyResolver: (_, _, _) => {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }
}
