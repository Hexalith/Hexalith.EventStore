using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Immutable Apply-method registry for one aggregate state or read-model type, keyed by runtime CLR type,
/// by event full name, by event short name, and by the union of the two name forms used for the anchored
/// suffix scan.
/// </summary>
/// <remarks>
/// Instances are published from a process-wide static cache and read concurrently, so every member is a
/// genuinely immutable type (<see cref="FrozenDictionary{TKey, TValue}"/> / <see cref="ImmutableArray{T}"/>)
/// rather than a mutable collection behind a read-only-looking property.
/// </remarks>
internal sealed class ApplyMethodTable {
    internal ApplyMethodTable(
        Type stateType,
        FrozenDictionary<Type, ApplyMethodCandidates> byType,
        FrozenDictionary<string, ApplyMethodCandidates> byFullName,
        FrozenDictionary<string, ApplyMethodCandidates> byShortName,
        FrozenDictionary<string, ApplyMethodCandidates> bySuffixKey) {
        StateType = stateType;
        ByType = byType;
        ByFullName = byFullName;
        ByShortName = byShortName;
        BySuffixKey = bySuffixKey;

        // Longest key first, then ordinal, so the suffix scan is deterministic across processes.
        SuffixKeys = [.. bySuffixKey.Keys
            .OrderByDescending(static key => key.Length)
            .ThenBy(static key => key, StringComparer.Ordinal)];
    }

    /// <summary>Gets the aggregate state or read-model type this table was built from.</summary>
    internal Type StateType { get; }

    /// <summary>Gets the number of distinct event types with a registered Apply method.</summary>
    internal int Count => ByType.Count;

    /// <summary>Gets the exact runtime-CLR-type lookup.</summary>
    internal FrozenDictionary<Type, ApplyMethodCandidates> ByType { get; }

    /// <summary>Gets the exact normalized-full-name lookup, tried first during name resolution.</summary>
    internal FrozenDictionary<string, ApplyMethodCandidates> ByFullName { get; }

    /// <summary>Gets the exact short-name compatibility lookup, tried after the full-name lookup.</summary>
    internal FrozenDictionary<string, ApplyMethodCandidates> ByShortName { get; }

    /// <summary>Gets the union of both name forms, used exclusively by the anchored suffix scan.</summary>
    internal FrozenDictionary<string, ApplyMethodCandidates> BySuffixKey { get; }

    /// <summary>Gets every suffix-scan key, ordered longest-first.</summary>
    internal ImmutableArray<string> SuffixKeys { get; }

    /// <summary>Gets the unioned candidate set for a suffix-scan key.</summary>
    /// <param name="key">A key taken from <see cref="SuffixKeys"/>.</param>
    /// <returns>The candidates registered under the key.</returns>
    internal ApplyMethodCandidates GetSuffixCandidates(string key) => BySuffixKey[key];
}
