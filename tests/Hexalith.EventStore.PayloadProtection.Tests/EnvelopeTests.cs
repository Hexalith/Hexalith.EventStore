using System.Buffers.Binary;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers inherited envelope goldens and Story 8.3 vectors V004-V009.
/// </summary>
public sealed class EnvelopeTests
{
    /// <summary>V004 flips every fixed-header bit and obtains only a bounded mismatch.</summary>
    [Fact]
    [Trait("Vector", "V004")]
    public async Task V004_HeaderBitMatrix_IsAlwaysRejectedOrAuthenticatedAsMismatchAsync()
    {
        byte[] original = Convert.FromHexString(TestFixture.EnvelopeHex);
        for (int offset = 0; offset < PayloadProtectionLimits.HeaderBytes; offset++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                byte[] mutated = [.. original];
                mutated[offset] ^= checked((byte)(1 << bit));
                int resolverCalls = 0;
                CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
                    TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(mutated)),
                    keyResolver: (_, _, _) =>
                    {
                        resolverCalls++;
                        return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                    });
                result.IsReadable.ShouldBeFalse($"offset={offset}, bit={bit}");
                result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
                int expectedResolverCalls = offset is >= PayloadProtectionWireFormat.DekVersionOffset
                    and < PayloadProtectionWireFormat.FieldOrdinalOffset
                    && BinaryPrimitives.ReadUInt32BigEndian(
                        mutated.AsSpan(PayloadProtectionWireFormat.DekVersionOffset)) != 0
                    ? 1
                    : 0;
                resolverCalls.ShouldBe(expectedResolverCalls, $"offset={offset}, bit={bit}");
            }
        }
    }

    /// <summary>V005 rejects every truncation of the fixed G-001 envelope.</summary>
    [Fact]
    [Trait("Vector", "V005")]
    public async Task V005_TruncationMatrix_RejectsEveryBoundaryWithoutLookupAsync()
    {
        byte[] original = Convert.FromHexString(TestFixture.EnvelopeHex);
        for (int length = 0; length < original.Length; length++)
        {
            byte[] truncated = original.AsSpan(0, length).ToArray();
            Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(truncated));
            await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(truncated));
        }
    }

    /// <summary>V006 rejects a trailing byte.</summary>
    [Fact]
    [Trait("Vector", "V006")]
    public async Task V006_TrailingByte_IsRejectedWithoutLookupAsync()
    {
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
    public async Task V007_Base64UrlSpelling_IsStrictAndPerformsNoLookupAsync(string value)
    {
        Should.Throw<PayloadProtectionFormatException>(() => Base64UrlCodec.Decode(value));
        await AssertLocalCarrierMismatchAsync(value);
    }

    /// <summary>V007 permits an empty codec result but the complete reader rejects it locally.</summary>
    [Fact]
    [Trait("Vector", "V007")]
    public async Task V007_EmptyCarrier_IsRejectedByTheCompleteReaderWithoutLookupAsync()
    {
        Base64UrlCodec.Decode(string.Empty).ShouldBeEmpty();
        await AssertLocalCarrierMismatchAsync(string.Empty);
    }

    /// <summary>V008 rejects every closed identifier and reserved-flag mutation.</summary>
    [Theory]
    [InlineData(PayloadProtectionWireFormat.EnvelopeVersionOffset, (byte)0x02, true)]
    [InlineData(PayloadProtectionWireFormat.EnvelopeVersionOffset, (byte)0x03, false)]
    [InlineData(PayloadProtectionWireFormat.EnvelopeVersionOffset, (byte)0xff, false)]
    [InlineData(PayloadProtectionWireFormat.AlgorithmIdentifierOffset, (byte)0x02, false)]
    [InlineData(PayloadProtectionWireFormat.AlgorithmIdentifierOffset, (byte)0xff, false)]
    [InlineData(PayloadProtectionWireFormat.NonceConstructionIdentifierOffset, (byte)0x02, false)]
    [InlineData(PayloadProtectionWireFormat.NonceConstructionIdentifierOffset, (byte)0xff, false)]
    [InlineData(PayloadProtectionWireFormat.KeyReferenceKindOffset, (byte)0x02, false)]
    [InlineData(PayloadProtectionWireFormat.KeyReferenceKindOffset, (byte)0xff, false)]
    [InlineData(PayloadProtectionWireFormat.FlagsOffset, (byte)0x02, false)]
    [InlineData(PayloadProtectionWireFormat.FlagsOffset, (byte)0xff, false)]
    [InlineData(PayloadProtectionWireFormat.FlagsOffset + 1, (byte)0x02, false)]
    [InlineData(PayloadProtectionWireFormat.FlagsOffset + 1, (byte)0xff, false)]
    [Trait("Vector", "V008")]
    public async Task V008_ClosedIdentifiers_ExerciseRepresentativesWithoutInvalidLookupAsync(
        int offset,
        byte value,
        bool supported)
    {
        byte[] mutated = Convert.FromHexString(TestFixture.EnvelopeHex);
        mutated[offset] = value;
        if (supported)
        {
            // AR-20260914-02 requires an explicit positive control for version 02. Because 02 is
            // already the golden value and the only supported value, this row is intentionally
            // a same-value round trip rather than an alternate spelling.
            EnvelopeCodec.Write(EnvelopeCodec.Read(mutated)).ShouldBe(mutated);
            return;
        }

        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(mutated));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(mutated));
    }

    /// <summary>
    /// V009 rejects declared lengths that disagree with an undersized carrier and separately accepts
    /// a genuinely sized exact maximum. The zero and maximum-plus-one field clauses are subsumed
    /// defense in depth because the complete envelope bounds reject those total lengths first.
    /// </summary>
    [Fact]
    [Trait("Vector", "V009")]
    public async Task V009_CiphertextLengthBoundaries_AreCheckedBeforeSlicingOrLookupAsync()
    {
        byte[] baseline = Convert.FromHexString(TestFixture.EnvelopeHex);
        foreach (uint invalid in new uint[] { 0, 18, 20, 1_048_576, 1_048_577, uint.MaxValue })
        {
            byte[] mutated = [.. baseline];
            BinaryPrimitives.WriteUInt32BigEndian(
                mutated.AsSpan(PayloadProtectionWireFormat.CiphertextLengthOffset),
                invalid);
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
            new byte[PayloadProtectionLimits.NonceBytes],
            new byte[PayloadProtectionLimits.CiphertextBytes],
            new byte[PayloadProtectionLimits.TagBytes]);
        EnvelopeCodec.Read(EnvelopeCodec.Write(maximum)).Ciphertext.Length.ShouldBe(PayloadProtectionLimits.CiphertextBytes);
    }

    /// <summary>Verifies the writer's ciphertext defense-in-depth guard rejects both closed boundaries.</summary>
    [Fact]
    public void EnvelopeWriter_RejectsEmptyAndOverLimitCiphertext()
    {
        PayloadProtectionEnvelope golden = TestFixture.Envelope();

        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Write(
            golden with { Ciphertext = [] }));
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Write(
            golden with { Ciphertext = new byte[PayloadProtectionLimits.CiphertextBytes + 1] }));
    }

    /// <summary>Noncanonical 26-byte wire key references fail in the complete reader before key resolution.</summary>
    [Theory]
    [InlineData("lowercase")]
    [InlineData("excluded-i")]
    [InlineData("excluded-l")]
    [InlineData("excluded-o")]
    [InlineData("excluded-u")]
    [InlineData("out-of-range")]
    [InlineData("malformed")]
    public async Task WireKeyReference_NoncanonicalSpellingsAreRejectedBeforeLookupAsync(string spelling)
    {
        byte[] envelope = Convert.FromHexString(TestFixture.EnvelopeHex);
        switch (spelling)
        {
            case "lowercase":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = (byte)'j';
                break;
            case "excluded-i":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = (byte)'I';
                break;
            case "excluded-l":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = (byte)'L';
                break;
            case "excluded-o":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = (byte)'O';
                break;
            case "excluded-u":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = (byte)'U';
                break;
            case "out-of-range":
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset] = (byte)'8';
                break;
            default:
                envelope[PayloadProtectionWireFormat.KeyReferenceOffset + 2] = 0xff;
                break;
        }

        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(envelope));
        await AssertLocalCarrierMismatchAsync(Base64UrlCodec.Encode(envelope));
    }

    /// <summary>Verifies the highest permitted canonical first ULID character reaches exact lookup.</summary>
    [Fact]
    public async Task WireKeyReference_FirstCharacterSeven_IsAcceptedBeforeAuthenticationAsync()
    {
        const string keyReference = "7ZZZZZZZZZZZZZZZZZZZZZZZZZ";
        CanonicalUlid.IsValid(keyReference).ShouldBeTrue();
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with { KeyReference = keyReference };
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope))),
            keyResolver: (resolvedReference, _, _) =>
            {
                resolvedReference.ShouldBe(keyReference);
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(1);
    }

    /// <summary>V004 rejects a written envelope whose nonce does not derive from its field ordinal.</summary>
    [Fact]
    [Trait("Vector", "V004")]
    public void V004_WriterNonceDerivation_IsEnforced()
    {
        PayloadProtectionEnvelope golden = TestFixture.Envelope();
        golden.FieldOrdinal.ShouldBe(0u);
        golden.Nonce.ShouldAllBe(value => value == 0);

        // The golden nonce derives from ordinal 0, so any other ordinal must be rejected.
        Should.Throw<PayloadProtectionFormatException>(
            () => EnvelopeCodec.Write(golden with { FieldOrdinal = 1 }));

        // ... and ordinal 0 must reject any nonce that is not the derived all-zero value.
        byte[] nonce = [.. golden.Nonce];
        nonce[^1] = 1;
        Should.Throw<PayloadProtectionFormatException>(
            () => EnvelopeCodec.Write(golden with { Nonce = nonce }));

        // The unmutated golden still round-trips, so the guard rejects only the mismatch.
        EnvelopeCodec.Write(golden).ShouldBe(Convert.FromHexString(TestFixture.EnvelopeHex));
    }

    private static async Task AssertLocalCarrierMismatchAsync(string envelope)
    {
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(envelope),
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }
}
