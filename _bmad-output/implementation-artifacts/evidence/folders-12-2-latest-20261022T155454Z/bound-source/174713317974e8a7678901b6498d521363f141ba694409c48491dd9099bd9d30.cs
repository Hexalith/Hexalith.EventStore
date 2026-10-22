using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Owns one captured plaintext buffer, retiring it only after every started read-only borrower terminates.</summary>
internal sealed class DirectoryAtomicAppendPayloadLifetime : IDisposable
{
    private byte[]? _payload;
    private int _borrowers;
    private int _retired;
    private int _cleared;

    internal byte[]? Payload => _payload;

    internal byte[] Capture(byte[] original)
    {
        _payload = original.ToArray();
        return _payload;
    }

    internal void BorrowUntil(Task pending)
    {
        Interlocked.Increment(ref _borrowers);
        _ = pending.ContinueWith(static (_, state) =>
        {
            var lifetime = (DirectoryAtomicAppendPayloadLifetime)state!;
            Interlocked.Decrement(ref lifetime._borrowers);
            lifetime.TryClear();
        }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public void Dispose()
    {
        Volatile.Write(ref _retired, 1);
        TryClear();
    }

    private void TryClear()
    {
        if (Volatile.Read(ref _retired) != 0 && Volatile.Read(ref _borrowers) == 0
            && Interlocked.Exchange(ref _cleared, 1) == 0 && _payload is not null)
        {
            CryptographicOperations.ZeroMemory(_payload);
        }
    }
}
