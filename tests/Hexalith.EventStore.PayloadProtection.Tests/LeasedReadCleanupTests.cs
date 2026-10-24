using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Lease-release faults cannot skip buffer clearing, expose private errors or replace caller cancellation.</summary>
public sealed class LeasedReadCleanupTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task FailedReleasePreservesSafeOutcomesAndClearsEveryOwnedBuffer(bool snapshot, int failureMode)
    {
        var observer = new RecordingBufferObserver();
        var core = new PayloadProtectionCore(observer);
        using var cancellation = new CancellationTokenSource();
        byte[] dek = TestFixture.Dek();
        if (failureMode == 0) { dek[0] ^= 1; }
        int releases = 0;
        PayloadKeyResolution Resolve() => PayloadKeyResolution.Success(dek,
            authenticated: _ =>
            {
                if (failureMode == 1) { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
                return ValueTask.CompletedTask;
            },
            releasedAsync: () =>
            {
                releases++;
                return ValueTask.FromException(new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable));
            });
        Task<CoreUnprotectionResult> read = snapshot
            ? core.TryUnprotectSnapshotAsync(TestFixture.ProtectSnapshot(), TestFixture.SnapshotContext(),
                (_, _, _) => ValueTask.FromResult(Resolve()), cancellation.Token).AsTask()
            : core.TryUnprotectEventAsync(TestFixture.Protect().PayloadBytes, TestFixture.Context(),
                (_, _, _) => ValueTask.FromResult(Resolve()), cancellation.Token).AsTask();
        if (failureMode == 1) { await Should.ThrowAsync<OperationCanceledException>(() => read); }
        else
        {
            CoreUnprotectionResult result = await read;
            result.PayloadBytes.ShouldBeNull();
            result.UnreadableReason.ShouldBe(failureMode == 0
                ? UnreadableProtectedDataReason.BytesMetadataMismatch : UnreadableProtectedDataReason.ProviderUnavailable);
        }
        releases.ShouldBe(1);
        dek.ShouldAllBe(value => value == 0);
        observer.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        observer.Observed.ShouldContain(SensitiveBufferKind.DecryptedPlaintext);
        observer.Observed.ShouldContain(SensitiveBufferKind.ProtectedOutput);
    }
}
