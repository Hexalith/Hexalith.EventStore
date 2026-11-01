using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Concurrent fixture operations share coherent opaque outcomes and output snapshots.</summary>
public sealed class LifecycleQualificationBackendTests
{
    [Fact]
    public async Task ConcurrentWrapReconcileAndUnwrapRetainExactBytesAndSafeOutputSnapshots()
    {
        using var backend = new LifecycleQualificationBackend("concurrent-fixture");
        byte[] key = Enumerable.Repeat((byte)0x53, 32).ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] operations = Enumerable.Range(0, 128).Select(index => Task.Run(async () =>
        {
            await start.Task;
            string operation = index.ToString("D26");
            using var wrap = await backend.WrapAsync(operation, key, default);
            byte[] wrapped = wrap.TakeBytes();
            try
            {
                using var reconciled = await backend.ReconcileAsync(operation, default);
                byte[] opaque = reconciled.TakeBytes();
                try { opaque.ShouldBe(wrapped); }
                finally { CryptographicOperations.ZeroMemory(opaque); }
                using var unwrapped = await backend.UnwrapAsync(operation, backend.OldVersion, wrapped, default);
                byte[] plain = unwrapped.TakeBytes();
                try { plain.ShouldBe(key); }
                finally { CryptographicOperations.ZeroMemory(plain); }
                _ = backend.Outputs.Select(output => output.Length).ToArray();
            }
            finally { CryptographicOperations.ZeroMemory(wrapped); }
        })).ToArray();
        start.SetResult();
        try { await Task.WhenAll(operations); }
        finally { CryptographicOperations.ZeroMemory(key); }
        backend.WrapCount.ShouldBe(128);
        backend.Outputs.Count.ShouldBe(384);
        backend.Outputs.ShouldAllBe(output => output.All(value => value == 0));
        backend.Dispose();
        await Should.ThrowAsync<ObjectDisposedException>(() => backend.ReconcileAsync("0", default).AsTask());
    }

    [Fact]
    public async Task DisposalDuringSuspendedUnwrapCannotPublishANewPlaintextOutput()
    {
        using var backend = new LifecycleQualificationBackend("dispose-fixture");
        using var wrap = await backend.WrapAsync("01J00000000000000000000001", new byte[32], default);
        byte[] wrapped = wrap.TakeBytes();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.Unwrapping = async (_, _) => { entered.TrySetResult(); await release.Task; };
        Task<PayloadBackendResult> pending = backend.UnwrapAsync("01J00000000000000000000002", backend.OldVersion, wrapped, default).AsTask();
        await entered.Task;
        backend.Dispose(); release.SetResult();
        await Should.ThrowAsync<ObjectDisposedException>(() => pending);
        backend.Outputs.Count.ShouldBe(1);
        CryptographicOperations.ZeroMemory(wrapped);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CleanupAttemptsEveryLaterOwnedResourceBeforeReportingASafeFailure(int failureIndex)
    {
        var attempted = new List<int>();
        string directory = Path.Combine(Path.GetTempPath(), "pp-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "credential.yaml"), "fixture-private-canary");
        try
        {
            Func<ValueTask>[] cleanups = Enumerable.Range(0, 4).Select(index => (Func<ValueTask>)(() =>
            {
                attempted.Add(index);
                if (index == 3) { Directory.Delete(directory, recursive: true); }
                return index == failureIndex ? ValueTask.FromException(new InvalidOperationException("fixture-private-canary")) : ValueTask.CompletedTask;
            })).ToArray();
            var failure = await Should.ThrowAsync<InvalidOperationException>(() => PayloadProtectionDaprFixture.CleanupAsync(cleanups).AsTask());
            attempted.ShouldBe([0, 1, 2, 3]); Directory.Exists(directory).ShouldBeFalse();
            failure.InnerException.ShouldBeNull(); failure.ToString().ShouldNotContain("fixture-private-canary");
        }
        finally { if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); } }
    }
}
