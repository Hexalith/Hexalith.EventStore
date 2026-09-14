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
public sealed class LimitsAndConcurrencyTests(ITestOutputHelper output) {
    /// <summary>V041 returns the original bytes and creates no ciphertext when no non-null path is selected.</summary>
    [Fact]
    [Trait("Vector", "V041")]
    public void V041_ZeroSelectedValues_ReturnsOriginalBytes() {
        byte[] original = "{\"value\":null}"u8.ToArray();
        int materialCalls = 0;
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            original,
            ["/value"],
            TestFixture.Context(),
            () => {
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
    public void V042_OneSelectedValue_UsesOrdinalZeroNonce() {
        CoreProtectionResult result = TestFixture.Protect();
        PayloadProtectionEnvelope envelope = EnvelopeCodec.Read(Base64UrlCodec.Decode(TestFixture.ReadWrapper(result)));
        envelope.FieldOrdinal.ShouldBe((uint)0);
        envelope.Nonce.ShouldAllBe(static value => value == 0);
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
    public void V045_SelectedCount4097_IsRejectedBeforeEncryption() {
        (byte[] payload, string[] paths) = CreateSelectedPayload(4097);
        RecordingBufferObserver observer = new();
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            payload,
            paths,
            TestFixture.Context(),
            () => {
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
    public async Task V046_ParallelWriterCollision_RegeneratesWhollyFreshMaterialAsync() {
        const string collision = "01J00000000000000000000000";
        var firstEntropy = new SequenceEntropy([collision, "01J00000000000000000000001"]);
        var secondEntropy = new SequenceEntropy([collision, "01J00000000000000000000002"]);
        RecordingBufferObserver firstObserver = new();
        RecordingBufferObserver secondObserver = new();
        var reserved = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<PayloadProtectionMaterial> first = Task.Run(async () => {
            await start.Task;
            return new PayloadProtectionMaterialGenerator(firstEntropy, firstObserver)
                .Generate(keyReference => reserved.TryAdd(keyReference, 0));
        });
        Task<PayloadProtectionMaterial> second = Task.Run(async () => {
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
    public async Task V047_DuplicateOrdinal_IsRejectedBeforeLookupAsync() {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject payload = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();
        JsonObject nameWrapper = payload["name"]!.AsObject();
        PayloadProtectionEnvelope nameEnvelope = EnvelopeCodec.Read(
            Base64UrlCodec.Decode(nameWrapper["$pdenc"]!.GetValue<string>()));
        nameWrapper["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(nameEnvelope with {
            FieldOrdinal = 0,
            Nonce = new byte[PayloadProtectionLimits.NonceBytes],
        }));
        byte[] duplicateOrdinalPayload = Encoding.UTF8.GetBytes(payload.ToJsonString());
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            duplicateOrdinalPayload,
            keyResolver: (_, _, _) => {
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
    public void V047_ReservationConflict_RetriesWithWhollyFreshMaterial() {
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
    public void V047_RepeatedReservationConflicts_FailClosed() {
        string[] collisions = Enumerable.Repeat(TestFixture.KeyReference, 16).ToArray();
        var entropy = new SequenceEntropy(collisions);
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionMaterialGenerator(entropy).Generate(_ => false));
        entropy.FillCount.ShouldBe(16);
    }

    /// <summary>V048 keeps restart/clone attempts state-free and records no repeated-DEK detector claim.</summary>
    [Fact]
    [Trait("Vector", "V048")]
    public void V048_RestartCloneGenerators_RelyOnObservableReferenceUniqueness() {
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
    public async Task V135_ImmutableReadMaximum_IsIndependentOfWriteConfigurationAsync() {
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
        byte[] originalOverWritePayload = overWritePayload.ToArray();
        RecordingBufferObserver observer = new();
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            overWritePayload,
            ["/value"],
            TestFixture.Context(),
            () => {
                materialCalls++;
                return TestFixture.Material();
            },
            maximumProtectedValueBytes: writeMaximum));
        overWritePayload.ShouldBe(originalOverWritePayload);
        materialCalls.ShouldBe(0);
        observer.Observed.ShouldBe([SensitiveBufferKind.InputSnapshot]);

        byte[] oversizedEnvelope = new byte[82 + PayloadProtectionLimits.CiphertextBytes + 1];
        Convert.FromHexString(TestFixture.EnvelopeHex).AsSpan(0, 66).CopyTo(oversizedEnvelope);
        BinaryPrimitives.WriteUInt32BigEndian(oversizedEnvelope.AsSpan(24), PayloadProtectionLimits.CiphertextBytes + 1);
        Should.Throw<PayloadProtectionFormatException>(() => EnvelopeCodec.Read(oversizedEnvelope));
    }

    /// <summary>Cancellation after one encryption returns no partial output and clears all owned inputs.</summary>
    [Fact]
    [Trait("Vector", "V039")]
    public void CancellationAfterPartialProtectMutation_IsAtomicAndClearsOwnedBuffers() {
        byte[] payload = "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray();
        byte[] original = payload.ToArray();
        PayloadProtectionMaterial material = TestFixture.Material();
        RecordingBufferObserver observer = new();
        using var source = new CancellationTokenSource();

        Should.Throw<OperationCanceledException>(() => new PayloadProtectionCore(observer).ProtectEvent(
            payload,
            ["/email", "/name"],
            TestFixture.Context(),
            () => material,
            cancellationToken: source.Token,
            encryptionCheckpoint: index => {
                if (index == 1) {
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
    public async Task AuthenticationFailureAfterPartialUnprotectMutation_IsAtomicAndClearsOwnedBuffersAsync() {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject payload = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();
        JsonObject nameWrapper = payload["name"]!.AsObject();
        PayloadProtectionEnvelope nameEnvelope = EnvelopeCodec.Read(
            Base64UrlCodec.Decode(nameWrapper["$pdenc"]!.GetValue<string>()));
        byte[] changedTag = nameEnvelope.Tag.ToArray();
        changedTag[0] ^= 1;
        nameWrapper["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(nameEnvelope with { Tag = changedTag }));
        byte[] changedPayload = Encoding.UTF8.GetBytes(payload.ToJsonString());
        byte[] original = changedPayload.ToArray();
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
    public async Task CancellationAfterPartialUnprotectMutation_IsAtomicAndClearsOwnedBuffersAsync() {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            "{\"email\":\"a\",\"name\":\"b\"}"u8.ToArray(),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        byte[] original = protectedResult.PayloadBytes.ToArray();
        byte[] resolvedDek = TestFixture.Dek();
        using var source = new CancellationTokenSource();
        RecordingBufferObserver observer = new(kind => {
            if (kind == SensitiveBufferKind.DecryptedPlaintext) {
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
    public void V136_AllTraversalAndPlaintextLimits_AreChecked() {
        byte[] exactPayload = new byte[PayloadProtectionLimits.PayloadBytes];
        "{\"v\":null}"u8.CopyTo(exactPayload);
        exactPayload.AsSpan(10).Fill((byte)' ');
        CoreProtectionResult passThrough = new PayloadProtectionCore().ProtectEvent(
            exactPayload, Array.Empty<string>(), TestFixture.Context(), TestFixture.Material);
        passThrough.PayloadBytes.ShouldNotBeSameAs(exactPayload);
        passThrough.PayloadBytes.ShouldBe(exactPayload);

        byte[] oversizedPayload = [.. exactPayload, (byte)' '];
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            oversizedPayload, Array.Empty<string>(), TestFixture.Context(), TestFixture.Material));

        string depth64 = new string('[', 64) + "0" + new string(']', 64);
        using (BoundedJsonDocument accepted = BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(depth64), default)) {
            accepted.NodeCount.ShouldBe(65);
        }

        string depth65 = new string('[', 65) + "0" + new string(']', 65);
        Should.Throw<PayloadProtectionFormatException>(() => BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(depth65), default));

        string nodes65536 = "[" + string.Join(',', Enumerable.Repeat("0", PayloadProtectionLimits.JsonNodes - 1)) + "]";
        using (BoundedJsonDocument accepted = BoundedJsonDocument.Parse(Encoding.UTF8.GetBytes(nodes65536), default)) {
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
        string[] exactSelectedPaths = Enumerable.Range(0, 8).Select(index => $"/p{index}").ToArray();
        new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(exactSelectedJson),
            exactSelectedPaths,
            TestFixture.Context(),
            TestFixture.Material).ProtectedPathCount.ShouldBe(8);

        string overSelectedJson = exactSelectedJson[..^1] + ",\"extra\":0}";
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(overSelectedJson),
            [.. exactSelectedPaths, "/extra"],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V138 keeps concurrent hostile input bounded and cancellation observable at checkpoints.</summary>
    [Fact]
    [Trait("Vector", "V138")]
    public async Task V138_HostileConcurrentLoad_IsBoundedAndCancellableAsync() {
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string malformedEnvelope = new('A', PayloadProtectionLimits.EnvelopeTextCharacters + 1);
        Task<CoreUnprotectionResult>[] hostile = Enumerable.Range(0, 16)
            .Select(_ => TestFixture.UnprotectAsync(TestFixture.WrapperPayloadBytes(malformedEnvelope)).AsTask())
            .ToArray();
        CoreUnprotectionResult[] results = await Task.WhenAll(hostile);
        results.ShouldAllBe(static result => result.UnreadableReason == UnreadableProtectedDataReason.BytesMetadataMismatch);

        string checkpointJson = "[" + string.Join(',', Enumerable.Repeat("0", 768)) + "]";
        foreach (int target in new[] { 1, 256, 512 }) {
            using var source = new CancellationTokenSource();
            Should.Throw<OperationCanceledException>(() => BoundedJsonDocument.Parse(
                Encoding.UTF8.GetBytes(checkpointJson),
                source.Token,
                count => {
                    if (count == target) {
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

    private static void AssertSelectedCountAccepted(int count) {
        (byte[] payload, string[] paths) = CreateSelectedPayload(count);
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(
            payload, paths, TestFixture.Context(), TestFixture.Material);
        result.ProtectedPathCount.ShouldBe(count);
        using JsonDocument document = JsonDocument.Parse(result.PayloadBytes);
        var ordinals = new HashSet<uint>();
        string? keyReference = null;
        foreach (JsonProperty property in document.RootElement.EnumerateObject()) {
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

    private static (byte[] Payload, string[] Paths) CreateSelectedPayload(int count) {
        string json = "{" + string.Join(',', Enumerable.Range(0, count).Select(index => $"\"p{index:D4}\":{index}")) + "}";
        string[] paths = Enumerable.Range(0, count).Select(index => $"/p{index:D4}").ToArray();
        return (Encoding.UTF8.GetBytes(json), paths);
    }
}
