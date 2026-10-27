#if SELECTED
namespace PublishedConsumer;

/// <summary>Counts requests reaching the separately hosted SDK query transport boundary.</summary>
internal static class QueryTransportCounters
{
    private static int _requests;
    /// <summary>Gets the actual number of HTTP query requests received.</summary>
    internal static int Requests => Volatile.Read(ref _requests);
    /// <summary>Records a request before handing it to the SDK domain dispatcher.</summary>
    internal static void Record() => Interlocked.Increment(ref _requests);
}
#endif
