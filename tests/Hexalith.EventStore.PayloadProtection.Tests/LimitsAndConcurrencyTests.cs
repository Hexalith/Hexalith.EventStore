using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers nonce-budget, collision, immutable-size, traversal, concurrency, and cancellation vectors V041-V048/V135-V136/V138.
/// </summary>
public sealed class LimitsAndConcurrencyTests(ITestOutputHelper output)
{
    /// <summary>V041 returns the original bytes and creates no ciphertext when no non-null path is selected.</summary>
    [Fact]
    [Trait("Vector", "V041")]
    public void V041_ZeroSelectedValues_ReturnsOriginalBytes()
    {
        byte[] original = "{\"value\":null}"u8.ToArray();
        int materialCalls = 0;
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            original,
            ["/value"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            });
        result.PayloadBytes.ShouldNotBeSameAs(original);
        result.PayloadBytes.ShouldBe(original);
        result.ProtectedPathCount.ShouldBe(0);
        result.SerializationFormat.ShouldBe("json");
        materialCalls.ShouldBe(0);
    }

    /// <summary>V042 assigns ordinal zero and the all-zero nonce to one selected value.</summary>
    [Fact]
    [Trait("Vector", "V042")]
    public void V042_OneSelectedValue_UsesOrdinalZeroNonce()
    {
        CoreProtectionResult result = TestFixture.Protect();
        result.SerializationFormat.ShouldBe("json+pdenc-v2");
        PayloadProtectionEnvelope envelope = EnvelopeCodec.Read(Base64UrlCodec.Decode(TestFixture.ReadWrapper(result)));
        envelope.FieldOrdinal.ShouldBe((uint)0);
        envelope.Nonce.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Caller-owned event bytes and paths cannot change the snapshots used after material creation starts.</summary>
    [Fact]
    public async Task EventProtection_UsesStableInputAndPathSnapshotsAcrossFactoryMutationAsync()
    {
        byte[] payload = "{\"value\":1}"u8.ToArray();
        byte[] expected = [.. payload];
        string[] paths = ["/value"];

        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            payload,
            paths,
            TestFixture.Context(),
            () =>
            {
                payload.AsSpan().Fill((byte)' ');
                paths[0] = "/missing";
                return TestFixture.Material();
            });
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(expected);
        payload.ShouldAllBe(static value => value == (byte)' ');
        paths.ShouldBe(["/missing"]);
    }

    /// <summary>V043 accepts 4,095 selected values with unique ordinal nonces under one DEK.</summary>
    [Fact]
    [Trait("Vector", "V043")]
    public void V043_SelectedCount4095_UsesUniqueOrdinals()
        => AssertSelectedCountAccepted(4095);

    /// <summary>V044 accepts 4,096 selected values with ordinals zero through 4,095.</summary>
    [Fact]
    [Trait("Vector", "V044")]
    public void V044_SelectedCount4096_UsesCompleteInvocationBudget()
        => AssertSelectedCountAccepted(4096);

    /// <summary>V045 rejects 4,097 paths before encryption.</summary>
    [Fact]
    [Trait("Vector", "V045")]
    public void V045_SelectedCount4097_IsRejectedBeforeEncryption()
    {
        (byte[] payload, string[] paths) = CreateSelectedPayload(4097);
        RecordingBufferObserver observer = new();
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            payload,
            paths,
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.DataEncryptionKey);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.SelectedPlaintext);
    }

    /// <summary>V046 regenerates both the key reference and DEK after an observable collision.</summary>
    [Fact]
    [Trait("Vector", "V046")]
    public async Task V046_ParallelWriterCollision_RegeneratesWhollyFreshMaterialAsync()
    {
        const string collision = "01J00000000000000000000000";
        var firstEntropy = new SequenceEntropy([collision, "01J00000000000000000000001"]);
        var secondEntropy = new SequenceEntropy([collision, "01J00000000000000000000002"]);
        RecordingBufferObserver firstObserver = new();
        RecordingBufferObserver secondObserver = new();
        var reserved = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<PayloadProtectionMaterial> first = Task.Run(async () =>
        {
            await start.Task;
            return new PayloadProtectionMaterialGenerator(firstEntropy, firstObserver)
                .Generate(keyReference => reserved.TryAdd(keyReference, 0));
        });
        Task<PayloadProtectionMaterial> second = Task.Run(async () =>
        {
            await start.Task;
            return new PayloadProtectionMaterialGenerator(secondEntropy, secondObserver)
                .Generate(keyReference => reserved.TryAdd(keyReference, 0));
        });

        start.SetResult();
        PayloadProtectionMaterial[] materials = await Task.WhenAll(first, second);
        materials.Select(static material => material.KeyReference).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        materials.ShouldContain(static material => material.KeyReference == collision);
        materials.ShouldContain(static material => material.KeyReference != collision);
        (firstEntropy.FillCount + secondEntropy.FillCount).ShouldBe(3);
        (firstObserver.Observed.Count + secondObserver.Observed.Count).ShouldBe(1);
        materials.Single(static material => material.KeyReference != collision)
            .DataEncryptionKey.ShouldAllBe(static value => value == 2);
    }

    /// <summary>V047 rejects a duplicate ordinal before resolving any key.</summary>
    [Fact]
    [Trait("Vector", "V047")]
    public async Task V047_DuplicateOrdinal_IsRejectedBeforeLookupAsync()
    {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject payload = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();
        JsonObject nameWrapper = payload["name"]!.AsObject();
        PayloadProtectionEnvelope nameEnvelope = EnvelopeCodec.Read(
            Base64UrlCodec.Decode(nameWrapper["$pdenc"]!.GetValue<string>()));
        nameWrapper["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(nameEnvelope with
        {
            FieldOrdinal = 0,
            Nonce = new byte[PayloadProtectionLimits.NonceBytes],
        }));
        byte[] duplicateOrdinalPayload = Encoding.UTF8.GetBytes(payload.ToJsonString());
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            duplicateOrdinalPayload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>V047 retries a reservation conflict with a fresh reference and a fresh DEK.</summary>
    [Fact]
    [Trait("Vector", "V047")]
    public void V047_ReservationConflict_RetriesWithWhollyFreshMaterial()
    {
        var entropy = new SequenceEntropy([
            "01J00000000000000000000000",
            "01J00000000000000000000001",
        ]);
        RecordingBufferObserver observer = new();
        int attempts = 0;

        PayloadProtectionMaterial material = new PayloadProtectionMaterialGenerator(entropy, observer)
            .Generate(_ => ++attempts == 2);

        attempts.ShouldBe(2);
        entropy.FillCount.ShouldBe(2);
        material.KeyReference.ShouldBe("01J00000000000000000000001");
        material.DataEncryptionKey.ShouldAllBe(static value => value == 2);
        observer.Observed.ShouldBe([SensitiveBufferKind.DataEncryptionKey]);
        CryptographicOperations.ZeroMemory(material.DataEncryptionKey);
    }

    /// <summary>V047 bounds repeated reservation collisions.</summary>
    [Fact]
    [Trait("Vector", "V047")]
    public void V047_RepeatedReservationConflicts_FailClosed()
    {
        string[] collisions = [.. Enumerable.Repeat(TestFixture.KeyReference, 16)];
        var entropy = new SequenceEntropy(collisions);
        Should.Throw<PayloadProtectionCryptographicException>(
            () => new PayloadProtectionMaterialGenerator(entropy).Generate(_ => false));
        entropy.FillCount.ShouldBe(16);
    }

    /// <summary>Verifies the default material generator executes the production CSPRNG entropy implementation.</summary>
    [Fact]
    public void ProductionEntropy_GeneratesDistinctCanonicalReferencesAndKeys()
    {
        var generator = new PayloadProtectionMaterialGenerator();
        PayloadProtectionMaterial first = generator.Generate(_ => true);
        PayloadProtectionMaterial second = generator.Generate(_ => true);
        try
        {
            CanonicalUlid.IsValid(first.KeyReference).ShouldBeTrue();
            CanonicalUlid.IsValid(second.KeyReference).ShouldBeTrue();
            first.KeyReference.ShouldNotBe(second.KeyReference);
            first.DekVersion.ShouldBe((uint)1);
            second.DekVersion.ShouldBe((uint)1);
            first.DataEncryptionKey.Length.ShouldBe(32);
            second.DataEncryptionKey.Length.ShouldBe(32);
            first.DataEncryptionKey.ShouldNotBe(second.DataEncryptionKey);
            first.DataEncryptionKey.ShouldContain(static value => value != 0);
            second.DataEncryptionKey.ShouldContain(static value => value != 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first.DataEncryptionKey);
            CryptographicOperations.ZeroMemory(second.DataEncryptionKey);
        }
    }

    /// <summary>Verifies a throwing observer cannot change a successful protection or stop later cleanup.</summary>
    [Fact]
    public void ThrowingObserver_DoesNotChangeProtectionOrLaterCleanup()
    {
        RecordingBufferObserver observer = new(_ => throw new InvalidOperationException());

        CoreProtectionResult result = TestFixture.Protect(observer);

        result.ProtectedPathCount.ShouldBe(1);
        observer.Observed.ShouldContain(SensitiveBufferKind.InputSnapshot);
        observer.Observed.ShouldContain(SensitiveBufferKind.SelectedPlaintext);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        observer.Observed.ShouldContain(SensitiveBufferKind.ProtectedOutput);
        observer.Observed.ShouldContain(SensitiveBufferKind.AuthenticatedData);
    }

    /// <summary>Verifies a throwing observer cannot change authenticated reconstruction or stop later cleanup.</summary>
    [Fact]
    public async Task ThrowingObserver_DoesNotChangeUnprotectionOrLaterCleanupAsync()
    {
        RecordingBufferObserver observer = new(_ => throw new InvalidOperationException());

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            observer: observer);

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe("{\"email\":\"alice@example.com\",\"name\":\"Alice\"}"u8.ToArray());
        observer.Observed.ShouldContain(SensitiveBufferKind.InputSnapshot);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        observer.Observed.ShouldContain(SensitiveBufferKind.ProtectedOutput);
        observer.Observed.ShouldContain(SensitiveBufferKind.AuthenticatedData);
    }

    /// <summary>Verifies a throwing observer cannot alter parsing or disposal of the owned input snapshot.</summary>
    [Fact]
    public void ThrowingObserver_DoesNotChangeParseOrOwnedSnapshotCleanup()
    {
        RecordingBufferObserver observer = new(_ => throw new InvalidOperationException());

        using (BoundedJsonDocument document = BoundedJsonDocument.Parse(
            "{\"value\":1}"u8,
            default,
            observer: observer))
        {
            document.Resolve("/value").ValueKind.ShouldBe(JsonValueKind.Number);
        }

        observer.Observed.ShouldBe([SensitiveBufferKind.InputSnapshot]);
    }

    /// <summary>Verifies throwing collision observers cannot stop fresh-material retries or subsequent cleanup.</summary>
    [Fact]
    public void ThrowingObserver_DoesNotChangeMaterialGenerationOrLaterCleanup()
    {
        var entropy = new SequenceEntropy([
            "01J00000000000000000000000",
            "01J00000000000000000000001",
            "01J00000000000000000000002",
            "01J00000000000000000000003",
        ]);
        RecordingBufferObserver observer = new(_ => throw new InvalidOperationException());
        int attempts = 0;

        PayloadProtectionMaterial material = new PayloadProtectionMaterialGenerator(entropy, observer)
            .Generate(_ => ++attempts == 4);

        attempts.ShouldBe(4);
        entropy.FillCount.ShouldBe(4);
        observer.Observed.ShouldBe([
            SensitiveBufferKind.DataEncryptionKey,
            SensitiveBufferKind.DataEncryptionKey,
            SensitiveBufferKind.DataEncryptionKey,
        ]);
        material.KeyReference.ShouldBe("01J00000000000000000000003");
        material.DataEncryptionKey.ShouldAllBe(static value => value == 4);
        CryptographicOperations.ZeroMemory(material.DataEncryptionKey);
    }

    /// <summary>V048 keeps restart/clone attempts state-free and records no repeated-DEK detector claim.</summary>
    [Fact]
    [Trait("Vector", "V048")]
    public void V048_RestartCloneGenerators_RelyOnObservableReferenceUniqueness()
    {
        var first = new PayloadProtectionMaterialGenerator(new SequenceEntropy(["01J00000000000000000000001"]));
        var second = new PayloadProtectionMaterialGenerator(new SequenceEntropy(["01J00000000000000000000002"]));
        PayloadProtectionMaterial left = first.Generate(_ => true);
        PayloadProtectionMaterial right = second.Generate(_ => true);
        left.KeyReference.ShouldNotBe(right.KeyReference);
        left.DataEncryptionKey.ShouldBe(right.DataEncryptionKey);
    }

    /// <summary>V135 reads the immutable maximum and checks the exact configured write maximum and maximum-plus-one.</summary>
    [Fact]
    [Trait("Vector", "V135")]
    public async Task V135_ImmutableReadMaximum_IsIndependentOfWriteConfigurationAsync()
    {
        string exactValue = "\"" + new string('x', PayloadProtectionLimits.CiphertextBytes - 2) + "\"";
        byte[] payload = Encoding.UTF8.GetBytes("{\"value\":" + exactValue + "}");
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            payload,
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material);
        CoreUnprotectionResult read = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);
        read.IsReadable.ShouldBeTrue();

        const int writeMaximum = 1024;
        string exactWriteValue = "\"" + new string('w', writeMaximum - 2) + "\"";
        Encoding.UTF8.GetByteCount(exactWriteValue).ShouldBe(writeMaximum);
        byte[] exactWritePayload = Encoding.UTF8.GetBytes("{\"value\":" + exactWriteValue + "}");
        CoreProtectionResult exactWrite = new PayloadProtectionCore().ProtectEvent(
            exactWritePayload,
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material,
            maximumProtectedValueBytes: writeMaximum);
        exactWrite.ProtectedPathCount.ShouldBe(1);
        exactWritePayload.ShouldBe(Encoding.UTF8.GetBytes("{\"value\":" + exactWriteValue + "}"));

        string overWriteValue = "\"" + new string('w', writeMaximum - 1) + "\"";
        Encoding.UTF8.GetByteCount(overWriteValue).ShouldBe(writeMaximum + 1);
        byte[] overWritePayload = Encoding.UTF8.GetBytes("{\"value\":" + overWriteValue + "}");
        byte[] originalOverWritePayload = [.. overWritePayload];
        RecordingBufferObserver observer = new();
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            overWritePayload,
            ["/value"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            },
            maximumProtectedValueBytes: writeMaximum));
        overWritePayload.ShouldBe(originalOverWritePayload);
        materialCalls.ShouldBe(0);
        observer.Observed.ShouldBe([
            SensitiveBufferKind.InputSnapshot,
            SensitiveBufferKind.AuthenticatedData,
            SensitiveBufferKind.AuthenticatedData,
        ]);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.SelectedPlaintext);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.DataEncryptionKey);

        byte[] oversizedEnvelope = new byte[82 + PayloadProtectionLimits.CiphertextBytes + 1];
        Convert.FromHexString(TestFixture.EnvelopeHex).AsSpan(0, 66).CopyTo(oversizedEnvelope);
        BinaryPrimitives.WriteUInt32BigEndian(oversizedEnvelope.AsSpan(24), PayloadProtectionLimits.CiphertextBytes + 1);
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(oversizedEnvelope));
    }

    /// <summary>Cancellation after one encryption returns no partial output and clears all owned inputs.</summary>
    [Fact]
    [Trait("Vector", "V039")]
    public void CancellationAfterPartialProtectMutation_IsAtomicAndClearsOwnedBuffers()
    {
        byte[] payload = "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray();
        byte[] original = [.. payload];
        PayloadProtectionMaterial material = TestFixture.Material();
        RecordingBufferObserver observer = new();
        using var source = new CancellationTokenSource();

        Should.Throw<OperationCanceledException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            payload,
            ["/email", "/name"],
            TestFixture.Context(),
            () => material,
            cancellationToken: source.Token,
            encryptionCheckpoint: index =>
            {
                if (index == 1)
                {
                    source.Cancel();
                }
            }));

        payload.ShouldBe(original);
        material.DataEncryptionKey.ShouldAllBe(static value => value == 0);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.SelectedPlaintext).ShouldBe(2);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey).ShouldBe(1);
    }

    /// <summary>Authentication failure after one decryption returns no plaintext and clears all owned buffers.</summary>
    [Fact]
    public async Task AuthenticationFailureAfterPartialUnprotectMutation_IsAtomicAndClearsOwnedBuffersAsync()
    {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject payload = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();
        JsonObject nameWrapper = payload["name"]!.AsObject();
        PayloadProtectionEnvelope nameEnvelope = EnvelopeCodec.Read(
            Base64UrlCodec.Decode(nameWrapper["$pdenc"]!.GetValue<string>()));
        byte[] changedTag = [.. nameEnvelope.Tag];
        changedTag[0] ^= 1;
        nameWrapper["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(nameEnvelope with { Tag = changedTag }));
        byte[] changedPayload = Encoding.UTF8.GetBytes(payload.ToJsonString());
        byte[] original = [.. changedPayload];
        byte[] resolvedDek = TestFixture.Dek();
        RecordingBufferObserver observer = new();

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            changedPayload,
            observer: observer,
            keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(resolvedDek));

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        changedPayload.ShouldBe(original);
        resolvedDek.ShouldAllBe(static value => value == 0);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.DecryptedPlaintext).ShouldBe(2);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey).ShouldBe(1);
    }

    /// <summary>Cancellation between decrypted wrappers returns no plaintext and clears the transferred key.</summary>
    [Fact]
    public async Task CancellationAfterPartialUnprotectMutation_IsAtomicAndClearsOwnedBuffersAsync()
    {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        byte[] original = [.. protectedResult.PayloadBytes];
        byte[] resolvedDek = TestFixture.Dek();
        using var source = new CancellationTokenSource();
        RecordingBufferObserver observer = new(kind =>
        {
            if (kind == SensitiveBufferKind.DecryptedPlaintext)
            {
                source.Cancel();
            }
        });

        await Should.ThrowAsync<OperationCanceledException>(async () => await TestFixture.UnprotectAsync(
            protectedResult.PayloadBytes,
            observer: observer,
            cancellationToken: source.Token,
            keyResolver: (_, _, _) => ValueTask.FromResult<byte[]?>(resolvedDek)));

        protectedResult.PayloadBytes.ShouldBe(original);
        resolvedDek.ShouldAllBe(static value => value == 0);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.DecryptedPlaintext).ShouldBe(2);
        observer.Observed.Count(static kind => kind == SensitiveBufferKind.DataEncryptionKey).ShouldBe(1);
    }

    /// <summary>V136 checks exact/max+1 payload, depth, node, selected-count, and total-plaintext boundaries.</summary>
    [Fact]
    [Trait("Vector", "V136")]
    public async Task V136_AllTraversalAndPlaintextLimits_AreCheckedAsync()
    {
        byte[] exactPayload = new byte[PayloadProtectionLimits.PayloadBytes];
        "{\"v\":null}"u8.CopyTo(exactPayload);
        exactPayload.AsSpan(10).Fill((byte)' ');
        CoreProtectionResult passThrough = new PayloadProtectionCore().ProtectEvent(
            exactPayload, [], TestFixture.Context(), TestFixture.Material);
        passThrough.PayloadBytes.ShouldNotBeSameAs(exactPayload);
        passThrough.PayloadBytes.ShouldBe(exactPayload);

        byte[] oversizedPayload = [.. exactPayload, (byte)' '];
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            oversizedPayload, [], TestFixture.Context(), TestFixture.Material));

        string depth64 = new string('[', 64) + "0" + new string(']', 64);
        using (BoundedJsonDocument accepted = BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(depth64), default))
        {
            accepted.NodeCount.ShouldBe(65);
        }

        string depth65 = new string('[', 65) + "0" + new string(']', 65);
        Should.Throw<PayloadProtectionFormatException>(() => BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(depth65), default));

        string nodes65536 = "[" + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 1)) + "]";
        using (BoundedJsonDocument accepted = BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(nodes65536), default))
        {
            accepted.NodeCount.ShouldBe(PayloadProtectionLimits.JsonNodes);
        }

        string nodes65537 = nodes65536.Insert(nodes65536.Length - 1, ",0");
        Should.Throw<PayloadProtectionFormatException>(() => BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(nodes65537), default));

        AssertSelectedCountAccepted(4096);
        (byte[] overCountPayload, string[] overCountPaths) = CreateSelectedPayload(4097);
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            overCountPayload, overCountPaths, TestFixture.Context(), TestFixture.Material));

        string oneMiB = "\"" + new string('p', PayloadProtectionLimits.CiphertextBytes - 2) + "\"";
        string exactSelectedJson = "{" + string.Join(',', Enumerable.Range(0, 8).Select(index => $"\"p{index}\":{oneMiB}")) + "}";
        string[] exactSelectedPaths = [.. Enumerable.Range(0, 8).Select(index => $"/p{index}")];
        CoreProtectionResult exactSelected = new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(exactSelectedJson),
            exactSelectedPaths,
            TestFixture.Context(),
            TestFixture.Material);
        exactSelected.ProtectedPathCount.ShouldBe(8);
        CoreUnprotectionResult exactSelectedRead = await TestFixture.UnprotectAsync(exactSelected.PayloadBytes);
        exactSelectedRead.IsReadable.ShouldBeTrue();

        string overSelectedJson = exactSelectedJson[..^1] + ",\"extra\":0}";
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(overSelectedJson),
            [.. exactSelectedPaths, "/extra"],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>Verifies the complete 4,096-wrapper event is accepted by the full reader.</summary>
    [Fact]
    public async Task EventReader_AcceptsComplete4096WrapperMaximumAsync()
    {
        (byte[] payload, string[] paths) = CreateSelectedPayload(PayloadProtectionLimits.ProtectedPaths);
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            payload,
            paths,
            TestFixture.Context(),
            TestFixture.Material);

        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            protectedResult.PayloadBytes,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(payload);
        resolverCalls.ShouldBe(1);
    }

    /// <summary>Verifies successful protections share no material or ordinal state through one concurrent core instance.</summary>
    [Fact]
    public async Task ConcurrentProtections_OnOneCoreKeepPerCallKeysAndNoncesIsolatedAsync()
    {
        const int invocationCount = 4;
        var core = new PayloadProtectionCore();
        using var barrier = new Barrier(invocationCount);
        Task<(byte[] Original, string KeyReference, byte[] Key, CoreProtectionResult Protected, CoreUnprotectionResult Unprotected)>[] tasks =
            [.. Enumerable.Range(0, invocationCount).Select(index => Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                    byte[] original = Encoding.UTF8.GetBytes($"{{\"left\":{index},\"right\":{index + 1}}}");
                    string keyReference = TestFixture.KeyReference[..^1]
                        + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    byte[] key = [.. Enumerable.Repeat(checked((byte)(index + 1)), 32)];
                    CoreProtectionResult protectedResult = core.ProtectEvent(
                        original,
                        ["/left", "/right"],
                        TestFixture.Context(),
                        () => new PayloadProtectionMaterial(keyReference, 1, [.. key]));
                    CoreUnprotectionResult unprotected = core.TryUnprotectEventAsync(
                        protectedResult.PayloadBytes,
                        TestFixture.Context(),
                        (resolvedReference, version, _) =>
                        {
                            resolvedReference.ShouldBe(keyReference);
                            version.ShouldBe((uint)1);
                            return ValueTask.FromResult<byte[]?>([.. key]);
                        }).AsTask().GetAwaiter().GetResult();
                    return (original, keyReference, key, protectedResult, unprotected);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))];

        var results = await Task.WhenAll(tasks);

        results.Select(static result => result.KeyReference).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(invocationCount);
        foreach ((byte[] original, string keyReference, byte[] key, CoreProtectionResult protectedResult, CoreUnprotectionResult unprotected) in results)
        {
            unprotected.IsReadable.ShouldBeTrue();
            unprotected.PayloadBytes.ShouldBe(original);
            using JsonDocument document = JsonDocument.Parse(protectedResult.PayloadBytes);
            PayloadProtectionEnvelope left = EnvelopeCodec.Read(Base64UrlCodec.Decode(
                document.RootElement.GetProperty("left").GetProperty("$pdenc").GetString()));
            PayloadProtectionEnvelope right = EnvelopeCodec.Read(Base64UrlCodec.Decode(
                document.RootElement.GetProperty("right").GetProperty("$pdenc").GetString()));
            left.KeyReference.ShouldBe(keyReference);
            right.KeyReference.ShouldBe(keyReference);
            left.FieldOrdinal.ShouldBe((uint)0);
            right.FieldOrdinal.ShouldBe((uint)1);
            BinaryPrimitives.ReadUInt64BigEndian(left.Nonce.AsSpan(4)).ShouldBe((ulong)0);
            BinaryPrimitives.ReadUInt64BigEndian(right.Nonce.AsSpan(4)).ShouldBe((ulong)1);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Verifies aggregate reader ciphertext above 8 MiB is rejected before resolving a key.</summary>
    [Fact]
    public async Task EventReader_RejectsCumulativeCiphertextOverMaximumBeforeLookupAsync()
    {
        const int wrapperCount = 9;
        const int plaintextLength = 932_068;
        string[] paths = [.. Enumerable.Range(0, wrapperCount).Select(index => $"/p{index}")];
        ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(paths);
        var wrappers = new string[wrapperCount];
        for (int index = 0; index < wrapperCount; index++)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("\"" + new string((char)('a' + index), plaintextLength - 2) + "\"");
            byte[] aad = TestFixture.Aad(path: paths[index], ordinal: checked((uint)index), commitment: manifest.Commitment);
            PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
                plaintext,
                aad,
                TestFixture.Dek(),
                TestFixture.KeyReference,
                1,
                checked((uint)index));
            wrappers[index] = "\"p" + index + "\":{\"$pdenc\":\""
                + Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope)) + "\"}";
        }

        byte[] payload = Encoding.UTF8.GetBytes("{" + string.Join(',', wrappers) + "}");
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            payload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>Authenticated plaintext cannot make the reconstructed event exceed reader depth or node limits.</summary>
    [Theory]
    [InlineData("depth")]
    [InlineData("nodes")]
    public async Task EventReader_RejectsReconstructedTraversalLimitViolationsAsync(string boundary)
    {
        string path;
        byte[] plaintext;
        string protectedTemplate;
        if (boundary == "depth")
        {
            path = string.Concat(Enumerable.Repeat("/0", 63));
            plaintext = "[[0]]"u8.ToArray();
            protectedTemplate = new string('[', 63) + "{0}" + new string(']', 63);
        }
        else
        {
            path = "/0";
            plaintext = "[0,0]"u8.ToArray();
            protectedTemplate = "[{0},"
                + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 3))
                + "]";
        }

        ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create([path]);
        byte[] aad = TestFixture.Aad(path: path, commitment: manifest.Commitment);
        PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
            plaintext,
            aad,
            TestFixture.Dek(),
            TestFixture.KeyReference,
            1,
            0);
        string wrapper = "{\"$pdenc\":\"" + Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope)) + "\"}";
        byte[] protectedPayload = Encoding.UTF8.GetBytes(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            protectedTemplate,
            wrapper));
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            protectedPayload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(1);
    }

    /// <summary>Verifies the reader accepts the exact protected byte maximum and exact reconstructed traversal limits.</summary>
    [Theory]
    [InlineData("bytes")]
    [InlineData("nodes")]
    [InlineData("depth")]
    public async Task EventReader_AcceptsExactReconstructionLimitsAsync(string boundary)
    {
        byte[] protectedPayload;
        int expectedNodes = 0;
        int expectedDepth = 0;
        if (boundary == "bytes")
        {
            byte[] canonical = TestFixture.Protect().PayloadBytes;
            protectedPayload = new byte[PayloadProtectionLimits.PayloadBytes];
            canonical.CopyTo(protectedPayload, 0);
            protectedPayload.AsSpan(canonical.Length).Fill((byte)' ');
        }
        else
        {
            string path;
            byte[] plaintext;
            string protectedTemplate;
            if (boundary == "nodes")
            {
                path = "/0";
                plaintext = "[0,0]"u8.ToArray();
                protectedTemplate = "[{0},"
                    + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 4))
                    + "]";
                expectedNodes = PayloadProtectionLimits.JsonNodes;
            }
            else
            {
                path = string.Concat(Enumerable.Repeat("/0", 63));
                plaintext = "[0]"u8.ToArray();
                protectedTemplate = new string('[', 63) + "{0}" + new string(']', 63);
                expectedDepth = PayloadProtectionLimits.JsonDepth;
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create([path]);
            byte[] aad = TestFixture.Aad(path: path, commitment: manifest.Commitment);
            PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
                plaintext,
                aad,
                TestFixture.Dek(),
                TestFixture.KeyReference,
                1,
                0);
            string wrapper = "{\"$pdenc\":\"" + Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope)) + "\"}";
            protectedPayload = Encoding.UTF8.GetBytes(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                protectedTemplate,
                wrapper));
        }

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedPayload);

        result.IsReadable.ShouldBeTrue();
        if (boundary == "bytes")
        {
            protectedPayload.Length.ShouldBe(PayloadProtectionLimits.PayloadBytes);
        }
        else
        {
            using BoundedJsonDocument reconstructed = BoundedJsonDocument.Inspect(result.PayloadBytes!, default);
            if (boundary == "nodes")
            {
                reconstructed.NodeCount.ShouldBe(expectedNodes);
            }
            else
            {
                reconstructed.MaximumDepth.ShouldBe(expectedDepth);
            }
        }
    }

    /// <summary>Verifies predictable wrapper byte, node, and depth expansion fails before material creation.</summary>
    [Theory]
    [InlineData("bytes")]
    [InlineData("nodes")]
    [InlineData("depth")]
    public void PredictableProtectedOutputExpansion_IsRejectedBeforeMaterialCreation(string boundary)
    {
        byte[] payload;
        string path;
        if (boundary == "bytes")
        {
            payload = new byte[PayloadProtectionLimits.PayloadBytes];
            "{\"v\":0}"u8.CopyTo(payload);
            payload.AsSpan(7).Fill((byte)' ');
            path = "/v";
        }
        else if (boundary == "nodes")
        {
            payload = Encoding.UTF8.GetBytes(
                "[" + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 1)) + "]");
            path = "/0";
        }
        else
        {
            payload = Encoding.UTF8.GetBytes(new string('[', 64) + "0" + new string(']', 64));
            path = string.Concat(Enumerable.Repeat("/0", 64));
        }

        int materialCalls = 0;
        RecordingBufferObserver observer = new();
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            payload,
            [path],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.SelectedPlaintext);
        observer.Observed.ShouldNotContain(SensitiveBufferKind.DataEncryptionKey);
    }

    /// <summary>Verifies predictable wrapper byte, node, and depth projection accepts each exact maximum.</summary>
    [Theory]
    [InlineData("bytes")]
    [InlineData("nodes")]
    [InlineData("depth")]
    public void PredictableProtectedOutputExpansion_AcceptsExactMaximum(string boundary)
    {
        byte[] payload;
        string path;
        if (boundary == "bytes")
        {
            int wrapperLength = Encoding.UTF8.GetByteCount(
                "{\"$pdenc\":\"" + Base64UrlCodec.Encode(new byte[83]) + "\"}");
            int inputLength = PayloadProtectionLimits.PayloadBytes - wrapperLength + 1;
            payload = new byte[inputLength];
            "{\"v\":0}"u8.CopyTo(payload);
            payload.AsSpan(7).Fill((byte)' ');
            path = "/v";
        }
        else if (boundary == "nodes")
        {
            payload = Encoding.UTF8.GetBytes(
                "[" + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 2)) + "]");
            path = "/0";
        }
        else
        {
            payload = Encoding.UTF8.GetBytes(new string('[', 63) + "0" + new string(']', 63));
            path = string.Concat(Enumerable.Repeat("/0", 63));
        }

        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            payload,
            [path],
            TestFixture.Context(),
            TestFixture.Material);

        using BoundedJsonDocument protectedDocument = BoundedJsonDocument.Inspect(result.PayloadBytes, default);
        if (boundary == "bytes")
        {
            result.PayloadBytes.Length.ShouldBe(PayloadProtectionLimits.PayloadBytes);
        }
        else if (boundary == "nodes")
        {
            protectedDocument.NodeCount.ShouldBe(PayloadProtectionLimits.JsonNodes);
        }
        else
        {
            protectedDocument.MaximumDepth.ShouldBe(PayloadProtectionLimits.JsonDepth);
        }
    }

    /// <summary>Verifies event missing and wrong-length keys map to closed outcomes and clear transferred bytes.</summary>
    [Theory]
    [InlineData("missing", UnreadableProtectedDataReason.MissingKey)]
    [InlineData("wrong-length", UnreadableProtectedDataReason.ConsistencyMismatch)]
    public async Task Event_KeyOutcome_IsClosedAndClearsTransferredMaterialAsync(
        string outcome,
        UnreadableProtectedDataReason expected)
    {
        byte[]? transferred = outcome == "wrong-length" ? new byte[31] : null;
        RecordingBufferObserver observer = new();

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            observer: observer,
            keyResolver: (_, _, _) => ValueTask.FromResult(transferred));

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(expected);
        if (transferred is not null)
        {
            transferred.ShouldAllBe(static value => value == 0);
            observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        }
    }

    /// <summary>Verifies caller cancellation immediately after key-reference entropy prevents DEK generation.</summary>
    [Fact]
    public void MaterialGeneration_CancellationAfterKeyReference_WinsBeforeDekEntropy()
    {
        using var source = new CancellationTokenSource();
        var entropy = new SequenceEntropy([TestFixture.KeyReference], source.Cancel);

        Should.Throw<OperationCanceledException>(() => new PayloadProtectionMaterialGenerator(entropy)
            .Generate(_ => true, source.Token));

        entropy.FillCount.ShouldBe(0);
    }

    /// <summary>Verifies partially filled DEK material is cleared when entropy throws.</summary>
    [Fact]
    public void MaterialGeneration_EntropyExceptionClearsPartialDek()
    {
        var entropy = new SequenceEntropy(
            [TestFixture.KeyReference],
            fillException: new InvalidOperationException());
        RecordingBufferObserver observer = new();

        Should.Throw<InvalidOperationException>(() => new PayloadProtectionMaterialGenerator(entropy, observer)
            .Generate(_ => true));

        entropy.FillCount.ShouldBe(1);
        observer.Observed.ShouldBe([SensitiveBufferKind.DataEncryptionKey]);
    }

    /// <summary>Verifies cancellation after a material factory return wins and clears the transferred DEK.</summary>
    [Fact]
    public void Protect_CancellationAfterFactoryReturnClearsTransferredDek()
    {
        using var source = new CancellationTokenSource();
        PayloadProtectionMaterial material = TestFixture.Material();

        Should.Throw<OperationCanceledException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"value\":1}"u8.ToArray(),
            ["/value"],
            TestFixture.Context(),
            () =>
            {
                source.Cancel();
                return material;
            },
            cancellationToken: source.Token));

        material.DataEncryptionKey.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Verifies cancellation after a resolver return wins and clears the transferred DEK.</summary>
    [Fact]
    public async Task Unprotect_CancellationAfterResolverReturnClearsTransferredDekAsync()
    {
        using var source = new CancellationTokenSource();
        byte[] transferred = TestFixture.Dek();

        await Should.ThrowAsync<OperationCanceledException>(async () => await TestFixture.UnprotectAsync(
            TestFixture.WrapperPayloadBytes(),
            cancellationToken: source.Token,
            keyResolver: (_, _, _) =>
            {
                source.Cancel();
                return ValueTask.FromResult<byte[]?>(transferred);
            }));

        transferred.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Verifies cancellation is observable during manifest enumeration, sorting, encoding, and hashing.</summary>
    [Theory]
    [InlineData("enumeration", 256)]
    [InlineData("decoding", 256)]
    [InlineData("sorting", 256)]
    [InlineData("encoding", 512)]
    [InlineData("hashing", 768)]
    public void Manifest_CancellationCheckpointsCoverEveryBoundedPhase(string phase, int target)
    {
        string[] paths = phase is "decoding" or "encoding" or "hashing"
            ? ["/" + new string('a', 767)]
            : [.. Enumerable.Range(0, 768).Select(index => $"/p{767 - index:D4}")];
        using var source = new CancellationTokenSource();
        void cancel(int count)
        {
            if (count == target)
            {
                source.Cancel();
            }
        }

        Should.Throw<OperationCanceledException>(() => ProtectedPathManifestCodec.Create(
            paths,
            cancellationToken: source.Token,
            checkpoint: phase is "enumeration" or "decoding" ? cancel : null,
            sortCheckpoint: phase == "sorting" ? cancel : null,
            encodingCheckpoint: phase == "encoding" ? cancel : null,
            hashCheckpoint: phase == "hashing" ? cancel : null));
    }

    /// <summary>Caller cancellation remains authoritative when enumerator disposal also fails.</summary>
    [Fact]
    public void Manifest_CallerCancellationWinsOverEnumeratorDisposalFailure()
    {
        using var source = new CancellationTokenSource();
        static IEnumerable<string> paths()
        {
            try
            {
                yield return "/value";
            }
            finally
            {
                throw new InvalidOperationException();
            }
        }

        Should.Throw<OperationCanceledException>(() => ProtectedPathManifestCodec.Create(
            paths(),
            cancellationToken: source.Token,
            checkpoint: count =>
            {
                if (count == 1)
                {
                    source.Cancel();
                }
            }));
    }

    /// <summary>Cancellation raised by an external enumeration outcome wins immediately.</summary>
    [Fact]
    public void Manifest_ExternalEnumerationCancellationWinsAfterMoveNext()
    {
        using var source = new CancellationTokenSource();
        IEnumerable<string> paths = Enumerable.Range(0, 1).Select(_ =>
        {
            source.Cancel();
            return "/value";
        });

        Should.Throw<OperationCanceledException>(() => ProtectedPathManifestCodec.Create(
            paths,
            cancellationToken: source.Token));
    }

    /// <summary>Verifies cancellation during discovered-path decoding and replacement copy/sort work.</summary>
    [Theory]
    [InlineData("path", 256)]
    [InlineData("pointer-decode", 256)]
    [InlineData("replacement-copy", 256)]
    [InlineData("replacement-sort", 256)]
    public void JsonTransformation_CancellationCoversBoundedInnerWork(string phase, int target)
    {
        using var source = new CancellationTokenSource();
        void cancel(int count)
        {
            if (count == target)
            {
                source.Cancel();
            }
        }

        if (phase == "path")
        {
            byte[] payload = Encoding.UTF8.GetBytes(
                "{\"" + new string('p', 768) + "\":{\"$pdenc\":\""
                + TestFixture.EnvelopeBase64Url + "\"}}");
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(payload, default);
            Should.Throw<OperationCanceledException>(() => document.ReadProtectedWrappers(source.Token, cancel));
            return;
        }

        if (phase == "pointer-decode")
        {
            string member = new('p', PayloadProtectionLimits.PathBytes - 1);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                Encoding.UTF8.GetBytes("{\"" + member + "\":0}"),
                default);
            Should.Throw<OperationCanceledException>(() => document.Resolve(
                "/" + member,
                cancellationToken: source.Token,
                checkpoint: cancel));
            return;
        }

        if (phase == "replacement-copy")
        {
            using BoundedJsonDocument document = BoundedJsonDocument.Parse("{\"v\":0}"u8, default);
            BoundedJsonNode value = document.Resolve("/v");
            var replacements = new[] {
                new JsonReplacement(value.Start, value.Length, Encoding.UTF8.GetBytes("\"" + new string('x', 1022) + "\"")),
            };
            Should.Throw<OperationCanceledException>(() => document.Rewrite(
                replacements,
                source.Token,
                checkpoint: cancel));
            return;
        }

        string json = "[" + string.Join(',', Enumerable.Repeat("0", 768)) + "]";
        using (BoundedJsonDocument document = BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(json), default))
        {
            var replacements = new JsonReplacement[768];
            for (int index = 0; index < replacements.Length; index++)
            {
                BoundedJsonNode node = document.Resolve($"/{index}");
                replacements[replacements.Length - index - 1] = new JsonReplacement(node.Start, node.Length, "1"u8.ToArray());
            }

            Should.Throw<OperationCanceledException>(() => document.Rewrite(
                replacements,
                source.Token,
                sortCheckpoint: cancel));
        }
    }

    /// <summary>V138 keeps concurrent hostile input bounded and cancellation observable at checkpoints.</summary>
    [Fact]
    [Trait("Vector", "V138")]
    public async Task V138_HostileConcurrentLoad_IsBoundedAndCancellableAsync()
    {
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string malformedEnvelope = new('A', PayloadProtectionLimits.EnvelopeTextCharacters + 1);
        int resolverCalls = 0;
        using var coreBarrier = new Barrier(16);
        Task<CoreUnprotectionResult>[] hostile = [.. Enumerable.Range(0, 16)
            .Select(_ => Task.Factory.StartNew(
                () => TestFixture.UnprotectAsync(
                    TestFixture.WrapperPayloadBytes(malformedEnvelope),
                    traversalCheckpoint: count =>
                    {
                        if (count == 1)
                        {
                            coreBarrier.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                        }
                    },
                    keyResolver: (_, _, _) =>
                    {
                        Interlocked.Increment(ref resolverCalls);
                        return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                    }).AsTask(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())];
        CoreUnprotectionResult[] results = await Task.WhenAll(hostile);
        results.ShouldAllBe(static result => result.UnreadableReason == UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);

        string checkpointJson = "[" + string.Join(',', Enumerable.Repeat("0", 768)) + "]";
        foreach (int target in new[] { 1, 256, 512 })
        {
            using var source = new CancellationTokenSource();
            Should.Throw<OperationCanceledException>(() => BoundedJsonDocument.Parse(
                Encoding.UTF8.GetBytes(checkpointJson),
                source.Token,
                count =>
                {
                    if (count == target)
                    {
                        source.Cancel();
                    }
                }));
        }

        stopwatch.Stop();
        output.WriteLine(
            "V138 hostileCalls={0}; payloadCharacters={1}; cancellationCheckpoints=1,256,512; allocatedBytes={2}; elapsedMilliseconds={3:F3}",
            hostile.Length,
            malformedEnvelope.Length,
            GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private static void AssertSelectedCountAccepted(int count)
    {
        (byte[] payload, string[] paths) = CreateSelectedPayload(count);
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            payload, paths, TestFixture.Context(), TestFixture.Material);
        result.ProtectedPathCount.ShouldBe(count);
        using JsonDocument document = JsonDocument.Parse(result.PayloadBytes);
        var ordinals = new HashSet<uint>();
        string? keyReference = null;
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            PayloadProtectionEnvelope envelope = EnvelopeCodec.Read(Base64UrlCodec.Decode(property.Value.GetProperty("$pdenc").GetString()));
            ordinals.Add(envelope.FieldOrdinal).ShouldBeTrue();
            keyReference ??= envelope.KeyReference;
            envelope.KeyReference.ShouldBe(keyReference);
            ulong nonceOrdinal = BinaryPrimitives.ReadUInt64BigEndian(envelope.Nonce.AsSpan(4));
            nonceOrdinal.ShouldBe(envelope.FieldOrdinal);
        }

        ordinals.Count.ShouldBe(count);
        ordinals.Min().ShouldBe((uint)0);
        ordinals.Max().ShouldBe(checked((uint)(count - 1)));
    }

    private static (byte[] Payload, string[] Paths) CreateSelectedPayload(int count)
    {
        string json = "{" + string.Join(',', Enumerable.Range(0, count).Select(index => $"\"p{index:D4}\":{index}")) + "}";
        string[] paths = [.. Enumerable.Range(0, count).Select(index => $"/p{index:D4}")];
        return (Encoding.UTF8.GetBytes(json), paths);
    }
}
