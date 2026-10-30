namespace Hexalith.EventStore.SignalR;

/// <summary>Revokes callback authority, including snapshots captured before removal.</summary>
/// <typeparam name="T">The signal payload.</typeparam>
/// <param name="original">The caller's callback identity.</param>
/// <param name="invoke">The callback invocation.</param>
internal sealed class ProjectionCallback<T>(Delegate original, Action<T> invoke)
{
    private int _revoked;

    /// <summary>Gets the original callback identity used for removal.</summary>
    public Delegate Original { get; } = original;

    /// <summary>Invokes only while this registration retains authority.</summary>
    public void Invoke(T value)
    {
        if (Volatile.Read(ref _revoked) == 0)
        {
            invoke(value);
        }
    }

    /// <summary>Revokes this registration before transport teardown.</summary>
    public void Revoke()
    {
        Interlocked.Exchange(ref _revoked, 1);
    }
}
