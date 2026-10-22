namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Charges simultaneously live token, decoded name and payload capacities before allocation.</summary>
internal sealed class DomainResponseBufferBudget
{
    private const long Maximum = 128L * 1024 * 1024;
    private long _live;

    /// <summary>Gets currently charged private capacity for local admission evidence.</summary>
    internal long LiveBytes => _live;

    /// <summary>Reserves exact capacity under the one response admission owner.</summary>
    internal void Reserve(long bytes)
    {
        if (bytes < 0 || bytes > Maximum - _live)
        {
            throw new InvalidOperationException("ScratchLimit: response admission exceeds 128 MiB live capacity.");
        }
        _live += bytes;
    }

    /// <summary>Releases exclusively owned capacity after its contents have been cleared.</summary>
    internal void Release(long bytes)
    {
        if (bytes < 0 || bytes > _live) { throw new InvalidOperationException("Response capacity accounting is inconsistent."); }
        _live -= bytes;
    }
}
