using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Text;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// The single shared discovery and resolution engine for the runtime
/// <c>public void Apply(TEvent)</c> convention. Both the aggregate/rehydrate path
/// (<c>DomainProcessorStateRehydrator</c>, <see cref="AggregateReplayer"/>) and the projection path
/// (<see cref="EventStoreProjection{TReadModel}"/>) resolve through this type, so the two former
/// copy-pasted resolvers cannot drift apart again.
/// </summary>
/// <remarks>
/// <para>
/// Persisters record <c>Type.FullName</c>, so every event type is registered under <em>both</em> its
/// full name and its short name. Resolution order is: exact full-name key, then exact short-name key,
/// then a boundary-anchored suffix scan where the longest anchored candidate wins.
/// </para>
/// <para>
/// A candidate key <c>k</c> matches a stored name <c>n</c> only when <c>n == k</c> or <c>n</c> ends
/// with <c>"." + k</c> or <c>"+" + k</c>. The <c>+</c> boundary is how <see cref="Type.FullName"/>
/// renders nested types. Unanchored matching (the previous behaviour) let <c>Billing.SubOrderCreated</c>
/// bind <c>Apply(OrderCreated)</c>.
/// </para>
/// <para>
/// A key claimed by more than one event type is never resolved by picking one: it is recorded as
/// ambiguous and reported at resolution time with <see cref="AmbiguousApplyMethodException"/>. Reporting
/// at resolution rather than registration keeps an aggregate whose events collide only by short name
/// working whenever those events are addressed by their full names.
/// </para>
/// <para>
/// The suffix scan reads from a dedicated key map that <em>unions</em> the full-name and short-name
/// aliases. Selecting one map over the other would hide a real collision: a global-namespace event type
/// registers key <c>K</c> as its full name while a namespaced <c>Ns.K</c> registers the same key as its
/// short name, and a stored <c>X.K</c> anchors on both.
/// </para>
/// </remarks>
internal static class ApplyMethodResolver {
    private const string ApplyMethodName = "Apply";

    private static readonly ConcurrentDictionary<Type, ApplyMethodTable> _tableCache = new();

    /// <summary>
    /// Gets the cached Apply table for <paramref name="stateType"/>, building it on first use.
    /// </summary>
    /// <param name="stateType">The aggregate state or read-model type declaring the Apply methods.</param>
    /// <returns>The shared, immutable Apply table.</returns>
    internal static ApplyMethodTable GetOrBuildTable(Type stateType) {
        ArgumentNullException.ThrowIfNull(stateType);
        return _tableCache.GetOrAdd(stateType, static type => BuildTable(type));
    }

    /// <summary>
    /// Builds an Apply table for <paramref name="stateType"/> without consulting or populating the cache.
    /// </summary>
    /// <param name="stateType">The aggregate state or read-model type declaring the Apply methods.</param>
    /// <returns>A new Apply table.</returns>
    internal static ApplyMethodTable BuildTable(Type stateType) {
        ArgumentNullException.ThrowIfNull(stateType);

        var byType = new Dictionary<Type, ApplyMethodCandidateBuilder>();
        var byFullName = new Dictionary<string, ApplyMethodCandidateBuilder>(StringComparer.Ordinal);
        var byShortName = new Dictionary<string, ApplyMethodCandidateBuilder>(StringComparer.Ordinal);
        var bySuffixKey = new Dictionary<string, ApplyMethodCandidateBuilder>(StringComparer.Ordinal);

        foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
            if (!method.Name.Equals(ApplyMethodName, StringComparison.Ordinal)) {
                continue;
            }

            if (method.ReturnType != typeof(void) || method.IsGenericMethodDefinition || method.ContainsGenericParameters) {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1) {
                continue;
            }

            Type eventType = parameters[0].ParameterType;

            // A by-ref, pointer or open-generic parameter has no stable persisted name — registering it
            // would key the table on something like "T" and let unrelated events bind to it.
            if (eventType.IsByRef || eventType.IsPointer || eventType.IsGenericParameter || eventType.ContainsGenericParameters) {
                continue;
            }

            if (string.IsNullOrEmpty(eventType.FullName)) {
                continue;
            }

            // Constructed generics render their arguments assembly-qualified inside the full name, so both
            // the registered key and the stored name are normalized through the same function. Doing it on
            // one side only would make a generic event unmatchable across an assembly version bump.
            string fullName = NormalizeTypeName(eventType.FullName);
            string shortName = eventType.Name;

            Register(byType, eventType, fullName, method);
            Register(byFullName, fullName, fullName, method);

            // Short names are registered as compatibility aliases for legacy streams that recorded the
            // CLR short name. The full name always wins on exact match, so a short-name collision never
            // hides an event that is addressed precisely.
            if (!string.Equals(shortName, fullName, StringComparison.Ordinal)) {
                Register(byShortName, shortName, fullName, method);
            }

            // Unconditionally register both alias forms for the suffix scan. When shortName == fullName
            // (a type declared outside any namespace) the second call resolves to the same declaration and
            // the candidate builder de-duplicates it.
            Register(bySuffixKey, fullName, fullName, method);
            Register(bySuffixKey, shortName, fullName, method);
        }

        return new ApplyMethodTable(
            stateType,
            Seal(byType),
            Seal(byFullName),
            Seal(byShortName),
            Seal(bySuffixKey));
    }

    /// <summary>
    /// Resolves the Apply method for a persisted event type name.
    /// </summary>
    /// <param name="table">The Apply table for the target state or read-model type.</param>
    /// <param name="storedEventTypeName">The event type name as recorded in the persisted stream entry.</param>
    /// <param name="messageId">Optional message identifier used only for diagnostics.</param>
    /// <param name="aggregateId">Optional aggregate identifier used only for diagnostics.</param>
    /// <returns>The matching Apply method, or <see langword="null"/> when no candidate matches.</returns>
    /// <exception cref="AmbiguousApplyMethodException">More than one Apply method matches.</exception>
    internal static MethodInfo? TryResolve(
        ApplyMethodTable table,
        string storedEventTypeName,
        string? messageId = null,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(table);

        if (string.IsNullOrWhiteSpace(storedEventTypeName)) {
            return null;
        }

        string name = NormalizeTypeName(storedEventTypeName);

        if (table.ByFullName.TryGetValue(name, out ApplyMethodCandidates? exact)) {
            return Single(table, storedEventTypeName, exact, messageId, aggregateId);
        }

        if (table.ByShortName.TryGetValue(name, out ApplyMethodCandidates? shortHit)) {
            return Single(table, storedEventTypeName, shortHit, messageId, aggregateId);
        }

        // SuffixKeys is ordered longest-first, and two distinct keys of equal length can never both be
        // anchored suffixes of the same stored name (they would have to be the same substring). The first
        // anchored hit is therefore the unique longest match; any remaining tie lives inside that one key's
        // unioned candidate set and is genuine ambiguity.
        foreach (string key in table.SuffixKeys) {
            if (IsBoundaryAnchoredSuffix(name, key)) {
                return Single(table, storedEventTypeName, table.GetSuffixCandidates(key), messageId, aggregateId);
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the Apply method for a runtime event instance type. Exact CLR-type identity is tried first
    /// so this entry point cannot return <see langword="null"/> where the name-based one would throw;
    /// otherwise a caller that silently skips unresolved events would drop the event without a diagnostic.
    /// </summary>
    /// <param name="table">The Apply table for the target state or read-model type.</param>
    /// <param name="eventType">The runtime CLR type of the event instance.</param>
    /// <param name="messageId">Optional message identifier used only for diagnostics.</param>
    /// <param name="aggregateId">Optional aggregate identifier used only for diagnostics.</param>
    /// <returns>The matching Apply method, or <see langword="null"/> when no candidate matches.</returns>
    /// <exception cref="AmbiguousApplyMethodException">More than one Apply method matches.</exception>
    internal static MethodInfo? TryResolve(
        ApplyMethodTable table,
        Type eventType,
        string? messageId = null,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(eventType);

        if (table.ByType.TryGetValue(eventType, out ApplyMethodCandidates? candidates)) {
            return Single(table, eventType.FullName ?? eventType.Name, candidates, messageId, aggregateId);
        }

        MethodInfo? resolved = TryResolve(table, eventType.FullName ?? eventType.Name, messageId, aggregateId);

        // A name-based fallback can land on a different CLR type that merely shares a short name. Invoking
        // it would surface as an opaque reflection ArgumentException, so treat a non-assignable match as no
        // match and let the caller apply its own not-found behaviour.
        return resolved is not null && resolved.GetParameters()[0].ParameterType.IsAssignableFrom(eventType)
            ? resolved
            : null;
    }

    /// <summary>
    /// Determines whether <paramref name="candidateKey"/> is a CLR-name-boundary-anchored suffix of
    /// <paramref name="storedName"/>. This is the only event-type suffix comparison in the project.
    /// </summary>
    private static bool IsBoundaryAnchoredSuffix(string storedName, string candidateKey) {
        if (candidateKey.Length == 0 || storedName.Length <= candidateKey.Length) {
            return false;
        }

        // The character immediately before the candidate must be a CLR name boundary:
        // '.' separates namespace/type segments and '+' separates a nested type from its declaring type.
        char boundary = storedName[storedName.Length - candidateKey.Length - 1];
        return (boundary is '.' or '+')
            && storedName.EndsWith(candidateKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes assembly qualification from a CLR type name at every bracket level, so
    /// "Ns.Type, Asm, Version=1.0.0.0" becomes "Ns.Type" and
    /// "Ns.Outer`1[[Ns.Arg, Asm, Version=1.0.0.0]]" becomes "Ns.Outer`1[[Ns.Arg]]".
    /// </summary>
    /// <remarks>
    /// Splitting on the first comma (the previous behaviour) truncated a constructed generic mid-argument,
    /// producing a fragment such as "Ns.Outer`1[[Ns.Arg" that could then anchor a suffix match against
    /// <c>Apply(Arg)</c> — a silent wrong bind. Because both the registered key and the stored name pass
    /// through this same function, matching stays consistent even for shapes it normalizes only partially.
    /// </remarks>
    private static string NormalizeTypeName(string typeName) {
        if (typeName.IndexOf(',', StringComparison.Ordinal) < 0) {
            return typeName;
        }

        var builder = new StringBuilder(typeName.Length);
        int depth = 0;
        int index = 0;

        while (index < typeName.Length) {
            char current = typeName[index];
            switch (current) {
                case '[':
                    depth++;
                    _ = builder.Append(current);
                    index++;
                    continue;
                case ']':
                    depth--;
                    _ = builder.Append(current);
                    index++;
                    continue;
                case ',': {
                    // Inside a bracket group, a ',' right after ']' separates two generic arguments and a
                    // ',' right before ']' or ',' is an array-rank separator; both are kept. At depth 0 the
                    // only comma is the outermost assembly qualification, which must always be dropped even
                    // though it can follow the ']]' that closes a generic argument list. Everything dropped
                    // runs to the current element's closing bracket (or end of string).
                    char? previous = builder.Length > 0 ? builder[^1] : null;
                    char? next = index + 1 < typeName.Length ? typeName[index + 1] : null;
                    if (depth > 0 && (previous == ']' || next is ']' or ',')) {
                        _ = builder.Append(current);
                        index++;
                        continue;
                    }

                    index = SkipToElementEnd(typeName, index, depth);
                    continue;
                }

                default:
                    _ = builder.Append(current);
                    index++;
                    continue;
            }
        }

        return builder.ToString();
    }

    private static int SkipToElementEnd(string typeName, int index, int depth) {
        int localDepth = depth;
        while (index < typeName.Length) {
            char current = typeName[index];
            if (current == '[') {
                localDepth++;
            }
            else if (current == ']') {
                if (localDepth == depth) {
                    return index;
                }

                localDepth--;
            }

            index++;
        }

        return index;
    }

    private static MethodInfo Single(
        ApplyMethodTable table,
        string storedEventTypeName,
        ApplyMethodCandidates candidates,
        string? messageId,
        string? aggregateId)
        => candidates.Method
        ?? throw new AmbiguousApplyMethodException(
            table.StateType,
            storedEventTypeName,
            candidates.CandidateEventTypeNames,
            candidates.CandidateCount,
            messageId,
            aggregateId);

    private static void Register<TKey>(
        Dictionary<TKey, ApplyMethodCandidateBuilder> registry,
        TKey key,
        string candidateFullName,
        MethodInfo method)
        where TKey : notnull {
        if (registry.TryGetValue(key, out ApplyMethodCandidateBuilder? builder)) {
            builder.Add(candidateFullName, method);
            return;
        }

        registry[key] = new ApplyMethodCandidateBuilder(candidateFullName, method);
    }

    private static FrozenDictionary<TKey, ApplyMethodCandidates> Seal<TKey>(
        Dictionary<TKey, ApplyMethodCandidateBuilder> registry)
        where TKey : notnull
        => registry.ToFrozenDictionary(
            static entry => entry.Key,
            static entry => entry.Value.Build(),
            registry.Comparer);

    private sealed class ApplyMethodCandidateBuilder {
        private readonly List<string> _candidateFullNames = [];
        private readonly List<MethodInfo> _methods = [];

        internal ApplyMethodCandidateBuilder(string candidateFullName, MethodInfo method)
            => Add(candidateFullName, method);

        internal void Add(string candidateFullName, MethodInfo method) {
            // The suffix-key map registers both the full name and the short name, which are the same string
            // for a type declared outside any namespace. Only genuinely distinct declarations are ambiguous.
            foreach (MethodInfo existing in _methods) {
                if (existing.MethodHandle == method.MethodHandle && existing.DeclaringType == method.DeclaringType) {
                    return;
                }
            }

            _methods.Add(method);
            _candidateFullNames.Add(candidateFullName);
        }

        internal ApplyMethodCandidates Build()
            => new(
                _methods.Count == 1 ? _methods[0] : null,
                _methods.Count,
                [.. _candidateFullNames.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal)]);
    }
}
