namespace Hexalith.EventStore.Server.Control;

/// <summary>Repeatable already-charged immutable payload source for bounded SQL streaming passes.</summary>
/// <remarks>The owner must retain and authenticate the original source; this wrapper grants no source authority.</remarks>
internal sealed class PostgreSqlControlPayloadSource
{
    private readonly Func<Stream> _openRead;

    internal PostgreSqlControlPayloadSource(int length, Func<Stream> openRead)
    {
        if (length is < 1 or > 104857600) { throw new ArgumentOutOfRangeException(nameof(length)); }
        ArgumentNullException.ThrowIfNull(openRead);
        Length = length;
        _openRead = openRead;
    }

    internal int Length { get; }

    internal Stream OpenRead()
    {
        Stream stream = _openRead() ?? throw new InvalidOperationException("EvidenceHold: payload source is unavailable.");
        if (!stream.CanRead) { stream.Dispose(); throw new InvalidOperationException("EvidenceHold: payload source is unreadable."); }
        return stream;
    }

    internal async ValueTask RequireExactLengthAsync(CancellationToken cancellationToken)
    {
        using Stream stream = OpenRead();
        byte[] window = new byte[64 * 1024];
        try
        {
            int total = 0;
            while (total < Length)
            {
                int read = await stream.ReadAsync(window.AsMemory(0, Math.Min(window.Length, Length - total)),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0) { throw new InvalidOperationException("EvidenceHold: payload source ended before its admitted length."); }
                total = checked(total + read);
            }
            if (await stream.ReadAsync(window.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new InvalidOperationException("EvidenceHold: payload source exceeds its admitted length.");
            }
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(window); }
    }
}
