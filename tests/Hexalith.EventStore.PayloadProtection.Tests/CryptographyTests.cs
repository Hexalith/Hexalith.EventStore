using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers inherited V001-V003 and cryptographic mutation vectors V010-V017.
/// </summary>
public sealed class CryptographyTests
{
    /// <summary>V001 reproduces every G-001 core artifact from atomic fields.</summary>
    [Fact]
    [Trait("Vector", "V001")]
    public void V001_G001Encryption_MatchesFrozenBytes()
    {
        using JsonDocument fixture = TestFixture.ReadFrozenFixture("g-001.json");
        JsonElement input = fixture.RootElement.GetProperty("input");
        JsonElement expected = fixture.RootElement.GetProperty("expected");
        input.GetProperty("keyReference").GetProperty("text").GetString().ShouldBe(TestFixture.KeyReference);
        input.GetProperty("plaintext").GetProperty("utf8Hex").GetString().ShouldBe(
            Convert.ToHexString(TestFixture.Plaintext()).ToLowerInvariant());
        expected.GetProperty("aadHex").GetString().ShouldBe(TestFixture.AadHex);
        expected.GetProperty("envelopeHex").GetString().ShouldBe(TestFixture.EnvelopeHex);
        expected.GetProperty("envelopeBase64Url").GetString().ShouldBe(TestFixture.EnvelopeBase64Url);

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
        CoreProtectionResult protectedResult = TestFixture.Protect();
        TestFixture.ReadWrapper(protectedResult).ShouldBe(TestFixture.EnvelopeBase64Url);
        string expectedEvent = "{\"email\":" + expected.GetProperty("wrapper").GetString() + ",\"name\":\"Alice\"}";
        protectedResult.PayloadBytes.ShouldBe(Encoding.UTF8.GetBytes(expectedEvent));
    }

    /// <summary>V002 returns the complete G-001 plaintext only after tag verification.</summary>
    [Fact]
    [Trait("Vector", "V002")]
    public async Task V002_G001Decryption_ReturnsCompleteAuthenticatedPayloadAsync()
    {
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes());
        result.IsReadable.ShouldBeTrue();
        Encoding.UTF8.GetString(result.PayloadBytes!).ShouldBe("{\"email\":\"alice@example.com\",\"name\":\"Alice\"}");
    }

    /// <summary>Verifies the root-manifest snapshot seam round-trips complete authenticated JSON.</summary>
    [Fact]
    public async Task Snapshot_RoundTripsCompleteAuthenticatedJsonAsync()
    {
        byte[] original = "{\"name\":\"Alice\",\"items\":[1,2]}"u8.ToArray();
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot(original);

        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(protectedSnapshot);

        protectedSnapshot.Format.ShouldBe("json+pdenc-v2");
        protectedSnapshot.SnapshotTypeId.ShouldBe("hx-snapshot-v1:party-state");
        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(original);
    }

    /// <summary>Verifies snapshot protection encrypts the stable pre-callback input snapshot.</summary>
    [Fact]
    public async Task SnapshotProtection_UsesStableInputSnapshotAcrossFactoryMutationAsync()
    {
        byte[] payload = "{\"name\":\"Alice\",\"items\":[1,2]}"u8.ToArray();
        byte[] expected = [.. payload];

        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot(
            payload,
            materialFactory: () =>
            {
                payload.AsSpan().Fill((byte)' ');
                return TestFixture.Material();
            });
        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(protectedSnapshot);

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(expected);
        payload.ShouldAllBe(static value => value == (byte)' ');
    }

    /// <summary>Verifies snapshot carrier tampering returns one atomic authenticated mismatch.</summary>
    [Fact]
    public async Task Snapshot_TamperedTag_ReturnsNoPlaintextAsync()
    {
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot();
        byte[] envelope = Base64UrlCodec.Decode(protectedSnapshot.Envelope);
        envelope[^1] ^= 1;

        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot with { Envelope = Base64UrlCodec.Encode(envelope) });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
    }

    /// <summary>Verifies snapshot format and stable-type carrier mismatches are local and perform no lookup.</summary>
    [Theory]
    [InlineData("format")]
    [InlineData("type")]
    public async Task Snapshot_CarrierMetadataMismatch_IsRejectedBeforeLookupAsync(string component)
    {
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot();
        ProtectedSnapshotPayloadV2 changed = component == "format"
            ? protectedSnapshot with { Format = "json" }
            : protectedSnapshot with { SnapshotTypeId = "hx-snapshot-v1:other-state" };
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(
            changed,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>Verifies a snapshot envelope with a non-root ordinal is rejected before key lookup.</summary>
    [Fact]
    public async Task Snapshot_NonZeroFieldOrdinal_IsRejectedBeforeLookupAsync()
    {
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with
        {
            FieldOrdinal = 1,
            Nonce = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1],
        };
        var protectedSnapshot = new ProtectedSnapshotPayloadV2(
            PayloadProtectionWireFormat.ProtectedSerializationFormat,
            TestFixture.SnapshotContext().PayloadTypeId,
            Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope)));
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>Verifies snapshot key outcomes use the closed missing, consistency, and unavailable taxonomy.</summary>
    [Theory]
    [InlineData("missing", UnreadableProtectedDataReason.MissingKey)]
    [InlineData("wrong-length", UnreadableProtectedDataReason.ConsistencyMismatch)]
    [InlineData("unavailable", UnreadableProtectedDataReason.ProviderUnavailable)]
    public async Task Snapshot_KeyOutcome_IsClosedAndClearsTransferredMaterialAsync(
        string outcome,
        UnreadableProtectedDataReason expected)
    {
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot();
        RecordingBufferObserver observer = new();
        byte[]? transferred = outcome == "wrong-length" ? new byte[31] : null;

        CoreUnprotectionResult result = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            observer: observer,
            keyResolver: (_, _, _) => outcome switch
            {
                "missing" => ValueTask.FromResult<byte[]?>(null),
                "wrong-length" => ValueTask.FromResult<byte[]?>(transferred),
                _ => throw new InvalidOperationException(),
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(expected);
        if (transferred is not null)
        {
            transferred.ShouldAllBe(static value => value == 0);
            observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        }
    }

    /// <summary>Verifies snapshot success and authentication failure clear every owned sensitive buffer.</summary>
    [Fact]
    public async Task Snapshot_SuccessAndAuthenticationFailure_ClearOwnedBuffersAsync()
    {
        RecordingBufferObserver successObserver = new();
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot(observer: successObserver);
        CoreUnprotectionResult success = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            observer: successObserver);
        success.IsReadable.ShouldBeTrue();
        successObserver.Observed.ShouldContain(SensitiveBufferKind.SelectedPlaintext);
        successObserver.Observed.ShouldNotContain(SensitiveBufferKind.DecryptedPlaintext);
        successObserver.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey).ShouldBe(2);
        successObserver.Observed.ShouldContain(SensitiveBufferKind.ProtectedOutput);

        byte[] envelope = Base64UrlCodec.Decode(protectedSnapshot.Envelope);
        envelope[^1] ^= 1;
        RecordingBufferObserver failureObserver = new();
        CoreUnprotectionResult failure = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot with { Envelope = Base64UrlCodec.Encode(envelope) },
            observer: failureObserver);
        failure.PayloadBytes.ShouldBeNull();
        failure.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        failureObserver.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
        failureObserver.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        failureObserver.Observed.ShouldContain(SensitiveBufferKind.ProtectedOutput);
    }

    /// <summary>Verifies configured snapshot bytes accept the exact maximum and reject maximum-plus-one before material.</summary>
    [Fact]
    public void Snapshot_ConfiguredMaximum_IsEnforcedBeforeMaterialCreation()
    {
        const int maximum = 1024;
        byte[] exact = Encoding.UTF8.GetBytes("\"" + new string('x', maximum - 2) + "\"");
        int exactMaterialCalls = 0;
        _ = new PayloadProtectionCore().ProtectSnapshot(
            exact,
            TestFixture.SnapshotContext(),
            () =>
            {
                exactMaterialCalls++;
                return TestFixture.Material();
            },
            maximumProtectedValueBytes: maximum);
        exactMaterialCalls.ShouldBe(1);

        byte[] over = Encoding.UTF8.GetBytes("\"" + new string('x', maximum - 1) + "\"");
        int overMaterialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectSnapshot(
            over,
            TestFixture.SnapshotContext(),
            () =>
            {
                overMaterialCalls++;
                return TestFixture.Material();
            },
            maximumProtectedValueBytes: maximum));
        overMaterialCalls.ShouldBe(0);
    }

    /// <summary>Verifies snapshot resolver cancellation wins after every outcome and clears transferred material.</summary>
    [Fact]
    public async Task Snapshot_ResolverCancellation_WinsAndClearsTransferredDekAsync()
    {
        ProtectedSnapshotPayloadV2 protectedSnapshot = TestFixture.ProtectSnapshot();
        using var returnedCancellation = new CancellationTokenSource();
        byte[] transferred = TestFixture.Dek();
        await Should.ThrowAsync<OperationCanceledException>(async () => await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            cancellationToken: returnedCancellation.Token,
            keyResolver: (_, _, _) =>
            {
                returnedCancellation.Cancel();
                return ValueTask.FromResult<byte[]?>(transferred);
            }));
        transferred.ShouldAllBe(static value => value == 0);

        using var exceptionalCancellation = new CancellationTokenSource();
        await Should.ThrowAsync<OperationCanceledException>(async () => await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            cancellationToken: exceptionalCancellation.Token,
            keyResolver: (_, _, _) =>
            {
                exceptionalCancellation.Cancel();
                throw new InvalidOperationException();
            }));
    }

    /// <summary>Verifies every invalid factory-material shape fails closed for event and snapshot writers.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("reference")]
    [InlineData("version")]
    [InlineData("dek-null")]
    [InlineData("dek-length")]
    public void InvalidFactoryMaterial_IsRejectedByEventAndSnapshotWriters(string shape)
    {
        var eventKeys = new List<byte[]>();
        RecordingBufferObserver eventObserver = new();
        Should.Throw<PayloadProtectionCryptographicException>(() => new PayloadProtectionCore(eventObserver).ProtectEvent(
            "{\"value\":1}"u8.ToArray(),
            ["/value"],
            TestFixture.Context(),
            () => CreateInvalidMaterial(shape, eventKeys)));
        eventKeys.ShouldAllBe(static key => key.All(static value => value == 0));
        eventObserver.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey)
            .ShouldBe(eventKeys.Count);

        var snapshotKeys = new List<byte[]>();
        RecordingBufferObserver snapshotObserver = new();
        Should.Throw<PayloadProtectionCryptographicException>(() => TestFixture.ProtectSnapshot(
            materialFactory: () => CreateInvalidMaterial(shape, snapshotKeys),
            observer: snapshotObserver));
        snapshotKeys.ShouldAllBe(static key => key.All(static value => value == 0));
        snapshotObserver.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey)
            .ShouldBe(snapshotKeys.Count);
    }

    /// <summary>Verifies each protected payload requests material exactly once.</summary>
    [Fact]
    public void Writers_InvokeMaterialFactoryExactlyOncePerPayload()
    {
        int eventCalls = 0;
        int snapshotCalls = 0;
        _ = new PayloadProtectionCore().ProtectEvent(
            "{\"left\":1,\"right\":2}"u8.ToArray(),
            ["/left", "/right"],
            TestFixture.Context(),
            () =>
            {
                eventCalls++;
                return TestFixture.Material();
            });
        _ = TestFixture.ProtectSnapshot(materialFactory: () =>
        {
            snapshotCalls++;
            return TestFixture.Material();
        });

        eventCalls.ShouldBe(1);
        snapshotCalls.ShouldBe(1);
    }

    /// <summary>Verifies ordinary factory faults are closed and caller cancellation wins for both writers.</summary>
    [Theory]
    [InlineData("event", false)]
    [InlineData("event", true)]
    [InlineData("snapshot", false)]
    [InlineData("snapshot", true)]
    public void ThrowingMaterialFactory_IsClosedAndPreservesCancellationPrecedence(
        string payloadKind,
        bool cancelBeforeThrow)
    {
        using var source = new CancellationTokenSource();
        int factoryCalls = 0;
        PayloadProtectionMaterial Factory()
        {
            factoryCalls++;
            if (cancelBeforeThrow)
            {
                source.Cancel();
            }

            throw new InvalidOperationException();
        }

        void Protect()
        {
            if (payloadKind == "event")
            {
                _ = new PayloadProtectionCore().ProtectEvent(
                    "{\"value\":1}"u8.ToArray(),
                    ["/value"],
                    TestFixture.Context(),
                    Factory,
                    cancellationToken: source.Token);
            }
            else
            {
                _ = TestFixture.ProtectSnapshot(
                    materialFactory: Factory,
                    cancellationToken: source.Token);
            }
        }

        if (cancelBeforeThrow)
        {
            Should.Throw<OperationCanceledException>(Protect);
        }
        else
        {
            Should.Throw<PayloadProtectionCryptographicException>(Protect);
        }

        factoryCalls.ShouldBe(1);
    }

    /// <summary>Verifies resolver-owned cancellation maps to provider unavailability while the caller token remains active.</summary>
    [Theory]
    [InlineData("event")]
    [InlineData("snapshot")]
    public async Task ResolverOwnedCancellation_IsProviderUnavailableAsync(string payloadKind)
    {
        using var source = new CancellationTokenSource();
        int resolverCalls = 0;
        ValueTask<byte[]?> Resolver(string keyReference, uint version, CancellationToken cancellationToken)
        {
            _ = keyReference;
            _ = version;
            _ = cancellationToken;
            resolverCalls++;
            throw new OperationCanceledException(source.Token);
        }

        CoreUnprotectionResult result = payloadKind == "event"
            ? await TestFixture.UnprotectAsync(
                TestFixture.WrapperPayloadBytes(),
                cancellationToken: source.Token,
                keyResolver: Resolver)
            : await TestFixture.UnprotectSnapshotAsync(
                TestFixture.ProtectSnapshot(),
                cancellationToken: source.Token,
                keyResolver: Resolver);

        source.IsCancellationRequested.ShouldBeFalse();
        resolverCalls.ShouldBe(1);
        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    /// <summary>V003 reproduces the named NIST CAVP AES-256-GCM Count 0 tag.</summary>
    [Fact]
    [Trait("Vector", "V003")]
    public void V003_NistAes256GcmCountZero_MatchesFrozenTag()
    {
        using JsonDocument fixture = TestFixture.ReadFrozenFixture("nist-gcm-256-count0.json");
        JsonElement root = fixture.RootElement;
        byte[] key = Convert.FromHexString(root.GetProperty("keyHex").GetString()!);
        byte[] nonce = Convert.FromHexString(root.GetProperty("ivHex").GetString()!);
        byte[] tag = new byte[16];
        PayloadCryptography.EncryptAesGcm(
            key,
            nonce,
            [],
            [],
            tag,
            []);
        Convert.ToHexString(tag).ToLowerInvariant().ShouldBe(root.GetProperty("tagHex").GetString());
    }

    /// <summary>V010 rejects every independently flipped nonce bit.</summary>
    [Fact]
    [Trait("Vector", "V010")]
    public async Task V010_NonceBitMatrix_AuthenticatesEveryBitAsync()
    {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < 96; bit++)
        {
            byte[] nonce = [.. baseline.Nonce];
            nonce[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            var mutated = baseline with { Nonce = nonce };
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(mutated, TestFixture.Aad(), TestFixture.Dek()));

            byte[] encoded = Convert.FromHexString(TestFixture.EnvelopeHex);
            encoded[54 + (bit / 8)] ^= checked((byte)(1 << (bit % 8)));
            int resolverCalls = 0;
            CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
                TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(encoded)),
                keyResolver: (_, _, _) =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                });
            result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
            result.PayloadBytes.ShouldBeNull();
            resolverCalls.ShouldBe(1);
        }
    }

    /// <summary>V010 rejects valid-tag event and snapshot envelopes whose nonce prefix is noncanonical.</summary>
    [Fact]
    [Trait("Vector", "V010")]
    public async Task V010_AuthenticatedNoncanonicalNonce_IsRejectedAfterLookupAsync()
    {
        byte[] noncanonicalNonce = new byte[PayloadProtectionLimits.NonceBytes];
        noncanonicalNonce[0] = 1;
        PayloadProtectionEnvelope eventEnvelope = EncryptWithNonce(
            TestFixture.Plaintext(),
            TestFixture.Aad(),
            noncanonicalNonce);
        int eventLookups = 0;

        CoreUnprotectionResult eventResult = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(EncodeWithUncheckedNonce(eventEnvelope)),
            keyResolver: (_, _, _) =>
            {
                eventLookups++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        eventResult.PayloadBytes.ShouldBeNull();
        eventResult.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        eventLookups.ShouldBe(1);

        ProtectedPathManifest snapshotManifest = ProtectedPathManifestCodec.Create([string.Empty], snapshot: true);
        byte[] snapshotAad = AadCodec.Write(
            TestFixture.SnapshotContext(),
            string.Empty,
            TestFixture.KeyReference,
            1,
            0,
            snapshotManifest.Commitment);
        PayloadProtectionEnvelope snapshotEnvelope = EncryptWithNonce(
            "{\"name\":\"Alice\"}"u8,
            snapshotAad,
            noncanonicalNonce);
        var protectedSnapshot = new ProtectedSnapshotPayloadV2(
            PayloadProtectionWireFormat.ProtectedSerializationFormat,
            TestFixture.SnapshotContext().PayloadTypeId,
            EncodeWithUncheckedNonce(snapshotEnvelope));
        int snapshotLookups = 0;

        CoreUnprotectionResult snapshotResult = await TestFixture.UnprotectSnapshotAsync(
            protectedSnapshot,
            keyResolver: (_, _, _) =>
            {
                snapshotLookups++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        snapshotResult.PayloadBytes.ShouldBeNull();
        snapshotResult.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        snapshotLookups.ShouldBe(1);
    }

    /// <summary>V011 rejects every independently flipped tag bit.</summary>
    [Fact]
    [Trait("Vector", "V011")]
    public async Task V011_TagBitMatrix_AuthenticatesEveryBitAsync()
    {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < 128; bit++)
        {
            byte[] tag = [.. baseline.Tag];
            tag[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(baseline with { Tag = tag }, TestFixture.Aad(), TestFixture.Dek()));
            CoreUnprotectionResult result = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes(
                Base64UrlCodec.Encode(EnvelopeCodec.Write(baseline with { Tag = tag }))));
            result.PayloadBytes.ShouldBeNull();
            result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
    }

    /// <summary>V012 rejects every independently flipped G-001 ciphertext bit.</summary>
    [Fact]
    [Trait("Vector", "V012")]
    public async Task V012_CiphertextBitMatrix_AuthenticatesEveryBitAsync()
    {
        PayloadProtectionEnvelope baseline = TestFixture.Envelope();
        for (int bit = 0; bit < baseline.Ciphertext.Length * 8; bit++)
        {
            byte[] ciphertext = [.. baseline.Ciphertext];
            ciphertext[bit / 8] ^= checked((byte)(1 << (bit % 8)));
            Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(baseline with { Ciphertext = ciphertext }, TestFixture.Aad(), TestFixture.Dek()));
            CoreUnprotectionResult result = await TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes(
                Base64UrlCodec.Encode(EnvelopeCodec.Write(baseline with { Ciphertext = ciphertext }))));
            result.PayloadBytes.ShouldBeNull();
            result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
    }

    /// <summary>V013 rejects algorithm substitution locally.</summary>
    [Fact]
    [Trait("Vector", "V013")]
    public async Task V013_AlgorithmSubstitution_IsRejectedLocallyWithoutLookupAsync()
    {
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
    public async Task V014_VersionSubstitution_IsRejectedLocallyWithoutLookupAsync(byte version)
    {
        byte[] bytes = Convert.FromHexString(TestFixture.EnvelopeHex);
        bytes[4] = version;
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(bytes));
        await AssertLocalMismatchWithoutLookupAsync(bytes);
    }

    /// <summary>V015 authenticates a canonical substituted key reference after exact lookup.</summary>
    [Fact]
    [Trait("Vector", "V015")]
    public async Task V015_KeyReferenceSubstitution_FailsAuthenticationAsync()
    {
        const string substitutedReference = "01J00000000000000000000001";
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with { KeyReference = substitutedReference };
        string wrapper = Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope));
        var lookups = new List<(string KeyReference, uint Version)>();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(wrapper),
            keyResolver: (keyReference, version, _) =>
            {
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
    public async Task V016_KeyVersionAndOrdinalSubstitution_FailsAuthenticationAsync(uint version, uint ordinal)
    {
        byte[] nonce = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), ordinal);
        PayloadProtectionEnvelope envelope = TestFixture.Envelope() with
        {
            DekVersion = version,
            FieldOrdinal = ordinal,
            Nonce = nonce,
        };
        var lookups = new List<(string KeyReference, uint Version)>();
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope))),
            keyResolver: (keyReference, resolvedVersion, _) =>
            {
                lookups.Add((keyReference, resolvedVersion));
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        if (ordinal == 0)
        {
            lookups.ShouldBe([(TestFixture.KeyReference, version)]);
        }
        else
        {
            lookups.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies mixed wrapper key references and versions are rejected before the shared-key lookup.</summary>
    [Theory]
    [InlineData("reference")]
    [InlineData("version")]
    public async Task MixedWrapperKeyIdentity_IsRejectedBeforeLookupAsync(string component)
    {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"left\":1,\"right\":2}"u8.ToArray(),
            ["/left", "/right"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject payload = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();
        JsonObject rightWrapper = payload["right"]!.AsObject();
        PayloadProtectionEnvelope rightEnvelope = EnvelopeCodec.Read(Base64UrlCodec.Decode(
            rightWrapper["$pdenc"]!.GetValue<string>()));
        rightEnvelope = component == "reference"
            ? rightEnvelope with { KeyReference = "01J00000000000000000000001" }
            : rightEnvelope with { DekVersion = 2 };
        rightWrapper["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(rightEnvelope));
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            Encoding.UTF8.GetBytes(payload.ToJsonString()),
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
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
    public void V017_AadFieldMatrix_AuthenticatesEveryField(int byteOffset)
    {
        byte[] aad = TestFixture.Aad();
        aad[byteOffset] ^= 1;
        Should.Throw<PayloadProtectionAuthenticationException>(
                () => PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek()));
    }

    private static async Task AssertLocalMismatchWithoutLookupAsync(byte[] envelope)
    {
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(envelope)),
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    private static PayloadProtectionEnvelope EncryptWithNonce(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> aad,
        byte[] nonce)
    {
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[PayloadProtectionLimits.TagBytes];
        PayloadCryptography.EncryptAesGcm(TestFixture.Dek(), nonce, plaintext, ciphertext, tag, aad);
        return new PayloadProtectionEnvelope(TestFixture.KeyReference, 1, 0, nonce, ciphertext, tag);
    }

    private static string EncodeWithUncheckedNonce(PayloadProtectionEnvelope envelope)
    {
        byte[] canonicalNonce = new byte[PayloadProtectionLimits.NonceBytes];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            canonicalNonce.AsSpan(4),
            envelope.FieldOrdinal);
        byte[] encoded = EnvelopeCodec.Write(envelope with { Nonce = canonicalNonce });
        envelope.Nonce.CopyTo(encoded, PayloadProtectionWireFormat.NonceOffset);
        return Base64UrlCodec.Encode(encoded);
    }

    private static PayloadProtectionMaterial CreateInvalidMaterial(
        string shape,
        ICollection<byte[]> transferredKeys)
    {
        byte[]? key = shape switch
        {
            "null" or "dek-null" => null,
            "dek-length" => new byte[31],
            _ => TestFixture.Dek(),
        };
        if (key is not null)
        {
            transferredKeys.Add(key);
        }

        return shape switch
        {
            "null" => null!,
            "reference" => new PayloadProtectionMaterial("invalid", 1, key!),
            "version" => new PayloadProtectionMaterial(TestFixture.KeyReference, 0, key!),
            "dek-null" => new PayloadProtectionMaterial(TestFixture.KeyReference, 1, null!),
            _ => new PayloadProtectionMaterial(TestFixture.KeyReference, 1, key!),
        };
    }
}
