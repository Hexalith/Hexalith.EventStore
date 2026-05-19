using System;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Security;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Security;

public class ProtectedDataLeakSentinelTests {
    [Fact]
    public void All_ReturnsEveryDefinedSentinel() {
        IReadOnlyList<string> sentinels = ProtectedDataLeakSentinel.All();
        sentinels.Count.ShouldBe(7);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedPayloadPlaintext);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedSnapshotPlaintext);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedKeyAlias);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedStateStoreKey);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedConnectionString);
        sentinels.ShouldContain(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);
    }

    [Fact]
    public void AssertNoLeak_PassesWhenNoSentinelPresent() => Should.NotThrow(() => ProtectedDataLeakSentinel.AssertNoLeak([
            "log line 1",
            "log line 2 with safe metadata",
            "tenant-a:billing:agg-001",
            null,
            string.Empty,
        ]));

    [Fact]
    public void AssertNoLeak_ThrowsWhenSentinelPresent() {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => ProtectedDataLeakSentinel.AssertNoLeak([
            "ok",
            $"oops: {ProtectedDataLeakSentinel.ProtectedKeyAlias}",
        ]));

        ex.Message.ShouldContain("index 2");
        ex.Message.ShouldNotContain(ProtectedDataLeakSentinel.ProtectedKeyAlias);
    }

    [Fact]
    public void HasNoLeak_ReturnsTrueWhenSafe() => ProtectedDataLeakSentinel.HasNoLeak(["safe", "metadata"]).ShouldBeTrue();

    [Fact]
    public void HasNoLeak_ReturnsFalseWhenSentinelPresent() => ProtectedDataLeakSentinel.HasNoLeak(["safe", $"leak: {ProtectedDataLeakSentinel.ProtectedProviderExceptionText}"]).ShouldBeFalse();

    [Fact]
    public void AssertNoLeakInFile_PassesWhenFileIsSafe() {
        string path = Path.Combine(Path.GetTempPath(), $"sentinel-safe-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "tenant-a:billing:agg-001 safe metadata\nReasonCode=protected-data-diagnostic-redacted");
        try {
            Should.NotThrow(() => ProtectedDataLeakSentinel.AssertNoLeakInFile(path));
            ProtectedDataLeakSentinel.HasNoLeakInFile(path).ShouldBeTrue();
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void AssertNoLeakInFile_ThrowsAndDoesNotEchoSentinel() {
        string path = Path.Combine(Path.GetTempPath(), $"sentinel-leak-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, $"some safe text\nthen: {ProtectedDataLeakSentinel.ProtectedKeyAlias}\nmore text");
        try {
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(
                () => ProtectedDataLeakSentinel.AssertNoLeakInFile(path));
            ex.Message.ShouldContain("index");
            ex.Message.ShouldContain(path);
            ex.Message.ShouldNotContain(ProtectedDataLeakSentinel.ProtectedKeyAlias);
            ProtectedDataLeakSentinel.HasNoLeakInFile(path).ShouldBeFalse();
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void AssertNoLeakInFile_ThrowsFileNotFoundForMissingPath() {
        string missing = Path.Combine(Path.GetTempPath(), $"sentinel-missing-{Guid.NewGuid():N}.txt");
        _ = Should.Throw<FileNotFoundException>(() => ProtectedDataLeakSentinel.AssertNoLeakInFile(missing));
        ProtectedDataLeakSentinel.HasNoLeakInFile(missing).ShouldBeFalse();
    }

    [Fact]
    public void AssertNoLeakInDirectory_FindsLeakAcrossPatternMatchedFiles() {
        string dir = Path.Combine(Path.GetTempPath(), $"sentinel-dir-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(dir);
        string safeFile = Path.Combine(dir, "evidence-safe.md");
        string leakyFile = Path.Combine(dir, "evidence-leak.md");
        File.WriteAllText(safeFile, "all good");
        File.WriteAllText(leakyFile, $"oops: {ProtectedDataLeakSentinel.ProtectedProviderExceptionText}");
        try {
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(
                () => ProtectedDataLeakSentinel.AssertNoLeakInDirectory(dir, "*.md"));
            ex.Message.ShouldContain(dir);
            ex.Message.ShouldNotContain(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);
        }
        finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AssertNoLeakInDirectory_PassesWhenAllFilesAreSafe() {
        string dir = Path.Combine(Path.GetTempPath(), $"sentinel-dir-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.md"), "safe");
        File.WriteAllText(Path.Combine(dir, "b.md"), "Protected data diagnostic details were redacted. ReasonCode=missing-key; Stage=replay.");
        try {
            Should.NotThrow(() => ProtectedDataLeakSentinel.AssertNoLeakInDirectory(dir, "*.md"));
        }
        finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AssertNoLeakInDirectory_ThrowsDirectoryNotFoundForMissingPath() {
        string missing = Path.Combine(Path.GetTempPath(), $"sentinel-dir-missing-{Guid.NewGuid():N}");
        _ = Should.Throw<DirectoryNotFoundException>(
            () => ProtectedDataLeakSentinel.AssertNoLeakInDirectory(missing, "*.md"));
    }

    [Fact]
    public void AssertNoLeakInDirectory_EmptyPatternsScansEverything() {
        string dir = Path.Combine(Path.GetTempPath(), $"sentinel-dir-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "evidence.json"), $"\"detail\":\"{ProtectedDataLeakSentinel.ProtectedStateStoreKey}\"");
        try {
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(
                () => ProtectedDataLeakSentinel.AssertNoLeakInDirectory(dir));
            ex.Message.ShouldNotContain(ProtectedDataLeakSentinel.ProtectedStateStoreKey);
        }
        finally {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class FakeUnreadableProtectionServiceTests {
    private static readonly AggregateIdentity TestIdentity = new("tenant-a", "billing", "agg-001");

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_DefaultBehavior_ReturnsReadable() {
        var service = new FakeUnreadableProtectionService();

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);

        outcome.IsReadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Theory]
    [InlineData(UnreadableProtectedDataReason.MissingKey)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted)]
    [InlineData(UnreadableProtectedDataReason.ProviderUnavailable)]
    [InlineData(UnreadableProtectedDataReason.ConsistencyMismatch)]
    public async Task TryUnprotectEventPayloadAsync_Queued_ReturnsConfiguredReason(UnreadableProtectedDataReason reason) {
        var service = new FakeUnreadableProtectionService();
        service.ConfigureEventUnreadable(reason);

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(reason);
    }

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_Queued_OnlyAffectsOneCall() {
        var service = new FakeUnreadableProtectionService();
        service.ConfigureEventUnreadable(UnreadableProtectedDataReason.MissingKey);

        PayloadUnprotectionOutcome first = await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);
        PayloadUnprotectionOutcome second = await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);

        first.IsUnreadable.ShouldBeTrue();
        second.IsReadable.ShouldBeTrue();
    }

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_Persistent_AffectsEveryCall() {
        var service = new FakeUnreadableProtectionService();
        service.ConfigureEventUnreadablePersistent(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);

        for (int i = 0; i < 3; i++) {
            PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
                TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);
            outcome.IsUnreadable.ShouldBeTrue();
            outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        }
    }

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_PropagatesCancellation() {
        var service = new FakeUnreadableProtectionService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () => await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "Type", [1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected(), cts.Token));
    }

    [Fact]
    public async Task TryUnprotectSnapshotAsync_Queued_ReturnsConfiguredReason() {
        var service = new FakeUnreadableProtectionService();
        service.ConfigureSnapshotUnreadable(UnreadableProtectedDataReason.MissingKey);

        SnapshotUnprotectionOutcome outcome = await service.TryUnprotectSnapshotAsync(
            TestIdentity, new { value = 1 }, EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MissingKey);
    }

    [Fact]
    public async Task EventUnprotectInvocations_AreCapturedForTestAssertions() {
        var service = new FakeUnreadableProtectionService();
        await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "TypeA", [1], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);
        await service.TryUnprotectEventPayloadAsync(
            TestIdentity, "TypeB", [2], "json", null, CancellationToken.None);

        service.EventUnprotectInvocations.Count.ShouldBe(2);
        service.EventUnprotectInvocations[0].EventTypeName.ShouldBe("TypeA");
        service.EventUnprotectInvocations[1].Metadata.ShouldBeNull();
    }

    [Fact]
    public void Reset_ClearsConfiguration() {
        var service = new FakeUnreadableProtectionService();
        service.ConfigureEventUnreadable(UnreadableProtectedDataReason.MissingKey);
        service.ConfigureEventUnreadablePersistent(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        service.ResetEventBehavior();
        service.ConfigureSnapshotUnreadable(UnreadableProtectedDataReason.ConsistencyMismatch);
        service.ResetSnapshotBehavior();

        Should.NotThrow(async () => {
            PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
                TestIdentity, "Type", [1], "json", EventStorePayloadProtectionMetadata.Unprotected(), CancellationToken.None);
            outcome.IsReadable.ShouldBeTrue();
        });
    }
}
