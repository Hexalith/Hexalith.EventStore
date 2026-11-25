using Hexalith.EventStore.Client.Gateway;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>Tracks the sample counter's cached value and recent projection responses.</summary>
internal sealed class CounterHistory
{
    private const int MaxEntries = 20;
    private readonly List<CounterHistoryEntry> _entries = [];
    private CounterStatusResult? _lastResult;

    /// <summary>Gets the validator to send with the next projection query.</summary>
    internal string? ETag => _lastResult?.ETag;

    /// <summary>Gets the newest history entries first.</summary>
    internal IReadOnlyList<CounterHistoryEntry> Entries => _entries;

    /// <summary>Records a valid projection response or confirms the cached state on a 304.</summary>
    /// <param name="response">The projection query response.</param>
    /// <param name="timestamp">The local history timestamp.</param>
    /// <returns>Whether the response confirms a valid counter state.</returns>
    internal bool TryApply(EventStoreQueryResult response, DateTime timestamp)
    {
        if (!CounterStatusResult.TryFromQueryResult(response, _lastResult, out CounterStatusResult state))
        {
            return false;
        }

        if (!response.IsNotModified)
        {
            Record(state, timestamp);
        }

        return true;
    }

    /// <summary>Records the initial empty state when the projection is missing.</summary>
    /// <param name="timestamp">The local history timestamp.</param>
    internal void RecordMissingProjection(DateTime timestamp)
    {
        if (!ReferenceEquals(_lastResult, CounterStatusResult.Empty))
        {
            Record(CounterStatusResult.Empty, timestamp);
        }
    }

    private void Record(CounterStatusResult state, DateTime timestamp)
    {
        _lastResult = state;
        string? etag = state.ETag;
        _entries.Insert(0, new CounterHistoryEntry(timestamp, state.Count, etag?[..Math.Min(8, etag.Length)] ?? "n/a"));
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
    }
}
