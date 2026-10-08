using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Retains the private pinned response's charge until every caller releases its owned lifetime.</summary>
internal sealed class DaprLogicalResponseOwner : IDisposable
{
    private byte[]? _bytes;
    private EventBufferReservation? _charge;

    /// <summary>Takes exclusive ownership of admitted response storage.</summary>
    internal DaprLogicalResponseOwner(byte[] bytes, EventBufferReservation charge) { _bytes = bytes; _charge = charge; }

    /// <summary>Gets exact retained private response bytes.</summary>
    internal ReadOnlyMemory<byte> Bytes => _bytes ?? throw new ObjectDisposedException(nameof(DaprLogicalResponseOwner));

    /// <summary>Captures a detached private response only after retaining source/copy capacity.</summary>
    internal static DaprLogicalResponseOwner Capture(ReadOnlySpan<byte> bytes, EventBufferBudget budget)
    {
        if (bytes.Length > 64 * 1024 * 1024) { throw new InvalidOperationException("ReadableLimit: retained response exceeds 64 MiB."); }
        EventBufferReservation charge = budget.Reserve(checked(bytes.Length * 2 + 256));
        try { return new DaprLogicalResponseOwner(bytes.ToArray(), charge); }
        catch { charge.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
