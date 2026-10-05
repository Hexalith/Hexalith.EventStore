using System.Collections;
using System.Collections.ObjectModel;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Throws after one valid extension has entered the reader's private metadata snapshot.</summary>
internal sealed class ThrowingSnapshotExtensions : ReadOnlyDictionary<string, string>, IDictionary<string, string>
{
    private readonly EventBufferBudget _budget;
    private readonly Exception _failure;

    /// <summary>Creates a read-only source that records the live charge when its injected enumeration failure occurs.</summary>
    internal ThrowingSnapshotExtensions(EventBufferBudget budget, Exception failure)
        : base(new Dictionary<string, string>
        {
            ["application-note"] = "valid-first-entry",
            ["later-entry"] = "never-copied",
        })
    {
        _budget = budget;
        _failure = failure;
    }

    /// <summary>Gets the number of source enumerators requested by the reader.</summary>
    internal int EnumeratorCalls { get; private set; }

    /// <summary>Gets the number of valid entries yielded before the injected failure.</summary>
    internal int YieldedEntries { get; private set; }

    /// <summary>Gets the composed budget charge observed immediately before throwing.</summary>
    internal int LiveBytesAtFailure { get; private set; }

    /// <summary>Returns an enumerator that enters the private snapshot and then fails.</summary>
    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        => EnumerateUntilFailure().GetEnumerator();

    /// <summary>Routes nongeneric enumeration through the same injected snapshot failure.</summary>
    IEnumerator IEnumerable.GetEnumerator() => EnumerateUntilFailure().GetEnumerator();

    private IEnumerable<KeyValuePair<string, string>> EnumerateUntilFailure()
    {
        EnumeratorCalls++;
        YieldedEntries++;
        yield return new KeyValuePair<string, string>("application-note", "valid-first-entry");
        LiveBytesAtFailure = _budget.LiveBytes;
        throw _failure;
    }
}
