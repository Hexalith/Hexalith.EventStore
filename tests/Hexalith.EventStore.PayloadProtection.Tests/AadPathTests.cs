using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers authenticated identity and canonical RFC 6901 vectors V018-V033.
/// </summary>
public sealed class AadPathTests
{
    /// <summary>V018 binds tenant identity and rejects absent context.</summary>
    [Fact]
    [Trait("Vector", "V018")]
    public void V018_Tenant_IsRequiredAndAuthenticated()
    {
        Should.Throw<ArgumentNullException>(() => AadCodec.Write(null!, "/email", TestFixture.KeyReference, 1, 0, new byte[32]));
        Should.Throw<ArgumentNullException>(() => TestFixture.Aad(TestFixture.Context() with { Identity = null! }));
        Should.Throw<ArgumentNullException>(() => new AggregateIdentity(null!, "parties", "party-01"));
        Should.Throw<ArgumentException>(() => new AggregateIdentity(string.Empty, "parties", "party-01"));
        TestFixture.Aad(TestFixture.Context() with
        {
            Identity = new AggregateIdentity(new string('t', 64), "parties", "party-01"),
        }).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => new AggregateIdentity(new string('t', 65), "parties", "party-01"));
        AssertAadSubstitutionFails(new AggregateIdentity("tenant-b", "parties", "party-01"));
    }

    /// <summary>V019 binds domain identity.</summary>
    [Fact]
    [Trait("Vector", "V019")]
    public void V019_Domain_IsRequiredBoundedAndAuthenticated()
    {
        Should.Throw<ArgumentNullException>(() => new AggregateIdentity("tenant-a", null!, "party-01"));
        Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant-a", string.Empty, "party-01"));
        TestFixture.Aad(TestFixture.Context() with
        {
            Identity = new AggregateIdentity("tenant-a", new string('d', 64), "party-01"),
        }).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant-a", new string('d', 65), "party-01"));
        AssertAadSubstitutionFails(new AggregateIdentity("tenant-a", "parties-x", "party-01"));
    }

    /// <summary>V020 binds aggregate identity.</summary>
    [Fact]
    [Trait("Vector", "V020")]
    public void V020_Aggregate_IsRequiredBoundedAndAuthenticated()
    {
        Should.Throw<ArgumentNullException>(() => new AggregateIdentity("tenant-a", "parties", null!));
        Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant-a", "parties", string.Empty));
        TestFixture.Aad(TestFixture.Context() with
        {
            Identity = new AggregateIdentity("tenant-a", "parties", new string('a', 256)),
        }).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant-a", "parties", new string('a', 257)));
        AssertAadSubstitutionFails(new AggregateIdentity("tenant-a", "parties", "party-02"));
    }

    /// <summary>V021 rejects absent payload type and authenticates case-sensitive substitutions.</summary>
    [Fact]
    [Trait("Vector", "V021")]
    public void V021_PayloadType_IsRequiredAndAuthenticated()
    {
        PayloadProtectionContext missing = TestFixture.Context() with { PayloadTypeId = null! };
        PayloadProtectionContext empty = TestFixture.Context() with { PayloadTypeId = string.Empty };
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(missing));
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(empty));
        TestFixture.Aad(TestFixture.Context() with { PayloadTypeId = new string('t', 1024) }).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(
            () => TestFixture.Aad(TestFixture.Context() with { PayloadTypeId = new string('t', 1025) }));
        AssertAadSubstitutionFails(
            TestFixture.Context() with { PayloadTypeId = "Hexalith.Parties.Contracts.Events.partyCreated" });
    }

    /// <summary>Verifies the complete stable snapshot type suffix grammar.</summary>
    [Theory]
    [InlineData("hx-snapshot-v1:a", true)]
    [InlineData("hx-snapshot-v1:party-state1", true)]
    [InlineData("hx-snapshot-v1:", false)]
    [InlineData("hx-snapshot-v1:-party", false)]
    [InlineData("hx-snapshot-v1:party-", false)]
    [InlineData("hx-snapshot-v1:party--state", false)]
    [InlineData("hx-snapshot-v1:Party-state", false)]
    [InlineData("snapshot-v1:party-state", false)]
    public void SnapshotTypeId_UsesCanonicalLowercaseKebabSuffix(string typeId, bool accepted)
    {
        Action validate = () => AadCodec.ValidateContext(
            TestFixture.SnapshotContext(typeId),
            PayloadProtectionPayloadKind.Snapshot);

        if (accepted)
        {
            validate.ShouldNotThrow();
        }
        else
        {
            Should.Throw<PayloadProtectionFormatException>(validate);
        }
    }

    /// <summary>Verifies invalid event context cannot bypass validation through empty-selection pass-through.</summary>
    [Fact]
    public void EmptySelection_InvalidEventContext_IsRejectedWithoutMaterialCreation()
    {
        int materialCalls = 0;
        PayloadProtectionContext invalid = TestFixture.Context() with { PayloadTypeId = string.Empty };

        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"value\":1}"u8.ToArray(),
            [],
            invalid,
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
    }

    /// <summary>V022 rejects missing/root event paths and authenticates exact case.</summary>
    [Fact]
    [Trait("Vector", "V022")]
    public void V022_PropertyPath_IsRequiredAndAuthenticated()
    {
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(path: null!));
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(path: string.Empty));
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(path: "email"));
        TestFixture.Aad(path: "/" + new string('p', 2047)).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(path: "/" + new string('p', 2048)));
        byte[] changed = TestFixture.Aad(path: "/Email");
        Should.Throw<PayloadProtectionAuthenticationException>(
            () => PayloadCryptography.Decrypt(TestFixture.Envelope(), changed, TestFixture.Dek()));
    }

    /// <summary>V023 pins the exact serialization-format field.</summary>
    [Fact]
    [Trait("Vector", "V023")]
    public void V023_Format_IsExactAndAuthenticated()
    {
        byte[] baseline = TestFixture.Aad();
        int fieldHeaderOffset = FindAadFieldHeader(baseline, 8);
        int formatOffset = fieldHeaderOffset + 6;
        baseline.AsSpan(formatOffset, 13).SequenceEqual("json+pdenc-v2"u8).ShouldBeTrue();

        byte[] missing = [.. baseline.AsSpan(0, fieldHeaderOffset), .. baseline.AsSpan(formatOffset + 13)];
        AssertRawAadSubstitutionFails(missing);
        byte[] empty = [.. baseline.AsSpan(0, formatOffset), .. baseline.AsSpan(formatOffset + 13)];
        empty.AsSpan(fieldHeaderOffset + 2, 4).Clear();
        AssertRawAadSubstitutionFails(empty);
        byte[] v1 = [.. baseline];
        v1[formatOffset + 12] = (byte)'1';
        AssertRawAadSubstitutionFails(v1);
    }

    /// <summary>V024 validates key-reference/version bounds and authenticates valid substitutions.</summary>
    [Theory]
    [InlineData(null, (uint)1)]
    [InlineData("", (uint)1)]
    [InlineData("01J0000000000000000000000", (uint)1)]
    [InlineData("01j00000000000000000000000", (uint)1)]
    [InlineData("81J00000000000000000000000", (uint)1)]
    [InlineData("01I00000000000000000000000", (uint)1)]
    [InlineData("01J00000000000000000000000", (uint)0)]
    [Trait("Vector", "V024")]
    public void V024_KeyReferenceAndVersion_InvalidSourcesAreRejected(string? keyReference, uint version)
    {
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(keyReference: keyReference, version: version));
    }

    /// <summary>V024 performs exact lookup of valid substituted DEK versions before authentication.</summary>
    [Theory]
    [InlineData((uint)2)]
    [InlineData(uint.MaxValue)]
    [Trait("Vector", "V024")]
    public async Task V024_ValidVersionSubstitution_PerformsExactLookupBeforeAuthenticationAsync(uint version)
    {
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with { DekVersion = version };
        var lookups = new List<(string KeyReference, uint Version)>();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope))),
            keyResolver: (keyReference, resolvedVersion, _) =>
            {
                lookups.Add((keyReference, resolvedVersion));
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        lookups.ShouldBe([(TestFixture.KeyReference, version)]);
    }

    /// <summary>V025 accepts only ordinals 0..4095 and authenticates disagreement.</summary>
    [Theory]
    [InlineData((uint)0, true)]
    [InlineData((uint)1, true)]
    [InlineData((uint)4095, true)]
    [InlineData((uint)4096, false)]
    [Trait("Vector", "V025")]
    public void V025_OrdinalBounds_AreClosed(uint ordinal, bool valid)
    {
        if (!valid)
        {
            Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(ordinal: ordinal));
            return;
        }

        byte[] aad = TestFixture.Aad(ordinal: ordinal);
        if (ordinal == 0)
        {
            PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()).ShouldBe(TestFixture.Plaintext());
        }
        else
        {
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()));
        }
    }

    /// <summary>V026 accepts the full u64 sequence range and binds the stored occurrence.</summary>
    [Theory]
    [InlineData((ulong)0)]
    [InlineData((ulong)1)]
    [InlineData((ulong)2)]
    [InlineData(ulong.MaxValue)]
    [Trait("Vector", "V026")]
    public void V026_RecordSequence_UsesTheFullUnsignedRange(ulong sequence)
    {
        byte[] aad = TestFixture.Aad(TestFixture.Context(sequence));
        if (sequence == 1)
        {
            PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()).ShouldBe(TestFixture.Plaintext());
        }
        else
        {
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()));
        }
    }

    /// <summary>V027 authenticates independent manifest count, length, path, and digest changes.</summary>
    [Theory]
    [InlineData("count")]
    [InlineData("length")]
    [InlineData("path")]
    [InlineData("digest")]
    [Trait("Vector", "V027")]
    public void V027_PathManifestComponents_AreAuthenticated(string component)
    {
        byte[] commitment = component switch
        {
            "count" => ProtectedPathManifestCodec.Create(["/email", "/name"]).Commitment,
            "length" => ProtectedPathManifestCodec.Create(["/emails"]).Commitment,
            "path" => ProtectedPathManifestCodec.Create(["/Email"]).Commitment,
            "digest" => FlipFirstByte(ProtectedPathManifestCodec.Create(["/email"]).Commitment),
            _ => throw new InvalidOperationException(),
        };
        AssertRawAadSubstitutionFails(TestFixture.Aad(commitment: commitment));
    }

    /// <summary>V027 rejects a duplicate path before requesting cryptographic material.</summary>
    [Fact]
    [Trait("Vector", "V027")]
    public void V027_DuplicateManifestPath_IsRejectedBeforeMaterialCreation()
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\"}"u8.ToArray(),
            ["/email", "/email"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>V028 keeps delimiter and case-bearing strings injective through length-delimited fields.</summary>
    [Theory]
    [InlineData("Type:With/Slash|Pipe\u2400")]
    [InlineData("type:with/slash|pipe\u2400")]
    [Trait("Vector", "V028")]
    public void V028_DelimiterCaseAndNulLikePrintableInputs_AreAuthenticated(string payloadType)
    {
        PayloadProtectionContext changed = TestFixture.Context() with { PayloadTypeId = payloadType };
        byte[] aad = TestFixture.Aad(changed);
        aad.ShouldNotBe(TestFixture.Aad());
        AssertRawAadSubstitutionFails(aad);
    }

    /// <summary>V029 accepts NFC and rejects decomposed or unpaired-surrogate sources.</summary>
    [Theory]
    [InlineData("T\u00e9", true)]
    [InlineData("Te\u0301", false)]
    [Trait("Vector", "V029")]
    public void V029_UnicodeCanonicalization_IsRejectNotNormalize(string value, bool valid)
    {
        PayloadProtectionContext changed = TestFixture.Context() with { PayloadTypeId = value };
        if (valid)
        {
            TestFixture.Aad(changed).Length.ShouldBeGreaterThan(0);
        }
        else
        {
            Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(changed));
            PayloadProtectionContext surrogate = TestFixture.Context() with { PayloadTypeId = "T\ud800" };
            Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(surrogate));
        }
    }

    /// <summary>V029 accepts printable boundaries and rejects every frozen control boundary.</summary>
    [Theory]
    [InlineData("T\u0020", true)]
    [InlineData("T\u007e", true)]
    [InlineData("T\u0000", false)]
    [InlineData("T\u001f", false)]
    [InlineData("T\u007f", false)]
    [Trait("Vector", "V029")]
    public void V029_ControlCharacterBoundaries_AreClosed(string value, bool valid)
    {
        PayloadProtectionContext changed = TestFixture.Context() with { PayloadTypeId = value };
        if (valid)
        {
            TestFixture.Aad(changed).ShouldNotBeEmpty();
        }
        else
        {
            Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(changed));
        }
    }

    /// <summary>V030 accepts the maximum constructible valid AAD and rejects a field maximum-plus-one.</summary>
    [Fact]
    [Trait("Vector", "V030")]
    public void V030_AadTotalLength_IsBoundedAfterEveryFieldBound()
    {
        string payloadType = new('p', 1024);
        string path = "/" + new string('x', 2047);
        var identity = new AggregateIdentity(new string('t', 64), new string('d', 64), new string('a', 256));
        PayloadProtectionContext context = TestFixture.Context() with { Identity = identity, PayloadTypeId = payloadType };
        byte[] maximum = TestFixture.Aad(context, path);
        maximum.Length.ShouldBe(3617);
        maximum.Length.ShouldBeLessThan(PayloadProtectionLimits.AadBytes);
        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.Aad(context, path + "x"));
    }

    /// <summary>V031 escapes tilde and slash in serialized member names.</summary>
    [Theory]
    [InlineData("a~b", "/a~0b")]
    [InlineData("a/b", "/a~1b")]
    [Trait("Vector", "V031")]
    public void V031_PathEscaping_IsCanonical(string member, string expected)
    {
        ("/" + JsonPointer.Escape(member)).ShouldBe(expected);
        JsonPointer.Decode(expected, allowRoot: false).Single().ShouldBe(member);
    }

    /// <summary>V031 assigns ordinals by unsigned UTF-8 path order, independent of input order.</summary>
    [Fact]
    [Trait("Vector", "V031")]
    public void V031_UnsignedUtf8Ordering_DeterminesManifestAndOrdinals()
    {
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"é\":1,\"z\":2}"),
            ["/é", "/z"],
            TestFixture.Context(),
            TestFixture.Material);
        ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(["/é", "/z"]);
        manifest.Paths.ShouldBe(["/z", "/é"]);

        using JsonDocument document = JsonDocument.Parse(result.PayloadBytes);
        PayloadProtectionEnvelope z = ReadEnvelope(document.RootElement, "z");
        PayloadProtectionEnvelope eAcute = ReadEnvelope(document.RootElement, "é");
        z.FieldOrdinal.ShouldBe((uint)0);
        eAcute.FieldOrdinal.ShouldBe((uint)1);
    }

    /// <summary>V032 rejects invalid tilde escapes.</summary>
    [Theory]
    [InlineData("/a~2b")]
    [InlineData("/a~")]
    [InlineData("/a~~0b")]
    [Trait("Vector", "V032")]
    public void V032_InvalidPointerEscapes_AreRejected(string pointer)
    {
        Should.Throw<PayloadProtectionFormatException>(() => JsonPointer.Decode(pointer, allowRoot: false));
    }

    /// <summary>V033 permits canonical array indices and rejects ambiguous forms.</summary>
    [Theory]
    [InlineData("/items/0", true)]
    [InlineData("/items/10", true)]
    [InlineData("/items/00", false)]
    [InlineData("/items/-", false)]
    [InlineData("/items/+1", false)]
    [Trait("Vector", "V033")]
    public void V033_ArrayIndices_UseCanonicalDecimal(string pointer, bool valid)
    {
        using BoundedJsonDocument document = BoundedJsonDocument.Parse(
            "{\"items\":[0,1,2,3,4,5,6,7,8,9,10]}"u8,
            default);
        if (valid)
        {
            _ = document.Resolve(pointer);
        }
        else
        {
            Should.Throw<PayloadProtectionFormatException>(() => document.Resolve(pointer));
        }
    }

    private static void AssertAadSubstitutionFails(AggregateIdentity identity)
        => AssertAadSubstitutionFails(TestFixture.Context() with { Identity = identity });

    private static void AssertAadSubstitutionFails(PayloadProtectionContext changed)
        => AssertRawAadSubstitutionFails(TestFixture.Aad(changed));

    private static void AssertRawAadSubstitutionFails(byte[] aad)
    {
        Should.Throw<PayloadProtectionAuthenticationException>(
            () => PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()));
    }

    private static byte[] FlipFirstByte(byte[] value)
    {
        value[0] ^= 1;
        return value;
    }

    private static int FindAadFieldHeader(ReadOnlySpan<byte> aad, byte fieldIdentifier)
    {
        int offset = 8;
        while (offset < aad.Length)
        {
            if (aad[offset] == fieldIdentifier)
            {
                return offset;
            }

            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(aad[(offset + 2)..]);
            offset = checked(offset + 6 + length);
        }

        throw new InvalidOperationException();
    }

    private static PayloadProtectionEnvelope ReadEnvelope(JsonElement root, string propertyName)
        => EnvelopeCodec.Read(Base64UrlCodec.Decode(root.GetProperty(propertyName).GetProperty("$pdenc").GetString()));
}
