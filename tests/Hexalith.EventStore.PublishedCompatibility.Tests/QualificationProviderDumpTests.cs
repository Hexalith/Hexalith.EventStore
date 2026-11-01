using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises actual owner-private file reads and destruction without starting any external resource.</summary>
public sealed class QualificationProviderDumpTests
{
    /// <summary>Safe observations retain actual bytes and absence while never retaining the sensitive body.</summary>
    [Fact]
    public async Task RealPrivateDumpFingerprintsReadActualBytesAndObserveDestruction()
    {
        await using var runtime = new QualificationRuntime();
        string path = Path.Combine(runtime.Root, "backup-" + Guid.NewGuid().ToString("N") + ".rdb");
        byte[] sensitive = "sensitive-test-provider-dump-body"u8.ToArray();
        runtime.RegisterSensitivePath(path);
        await File.WriteAllBytesAsync(path, sensitive, TestContext.Current.CancellationToken);
        var copied = await runtime.ObserveProviderDumpAsync(path, "provider-backup-copied", TestContext.Current.CancellationToken);
        copied.Bytes.ShouldBe(sensitive.Length); copied.Sha256.ShouldBe(QualificationRuntime.Hash(sensitive));
        JsonSerializer.Serialize(copied).ShouldNotContain(Encoding.UTF8.GetString(sensitive));
        var restored = await runtime.ObserveProviderDumpAsync(path, "provider-backup-before-restore", TestContext.Current.CancellationToken);
        restored.Sha256.ShouldBe(copied.Sha256); restored.Bytes.ShouldBe(copied.Bytes);
        File.Delete(path);
        var destroyed = await runtime.ObserveProviderDumpAsync(path, "provider-backup-destroyed", TestContext.Current.CancellationToken);
        destroyed.Exists.ShouldBeFalse(); destroyed.Bytes.ShouldBe(0); destroyed.Sha256.ShouldBeNull();
        runtime.Files.Select(file => file.Purpose).ShouldBe(["provider-backup-copied", "provider-backup-before-restore", "provider-backup-destroyed"]);
    }

    /// <summary>Missing or replaced bytes cannot be declared equal by a supplied digest.</summary>
    [Fact]
    public async Task RealPrivateDumpMutationAndUnknownFileStateDoNotReuseOldEvidence()
    {
        await using var runtime = new QualificationRuntime();
        string path = Path.Combine(runtime.Root, "backup-" + Guid.NewGuid().ToString("N") + ".rdb");
        runtime.RegisterSensitivePath(path);
        await Should.ThrowAsync<FileNotFoundException>(() => runtime.ObserveProviderDumpAsync(path, "provider-backup-copied"));
        runtime.Files.ShouldBeEmpty();
        await File.WriteAllTextAsync(path, "first actual dump", TestContext.Current.CancellationToken);
        var before = await runtime.ObserveProviderDumpAsync(path, "provider-backup-copied");
        await File.WriteAllTextAsync(path, "different actual dump", TestContext.Current.CancellationToken);
        var changed = await runtime.ObserveProviderDumpAsync(path, "provider-backup-before-restore");
        changed.Sha256.ShouldNotBe(before.Sha256);
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.ObserveProviderDumpAsync(path, "provider-backup-destroyed"));
        runtime.Files.Count.ShouldBe(2);
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.ObserveProviderDumpAsync(Path.Combine(runtime.Root, "unregistered.rdb"), "provider-backup-copied"));
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.ObserveProviderDumpAsync(path, "arbitrary-read"));
    }
}
