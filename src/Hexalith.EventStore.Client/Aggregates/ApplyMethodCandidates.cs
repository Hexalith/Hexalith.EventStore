using System.Reflection;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// The set of Apply methods registered under one <see cref="ApplyMethodTable"/> key. A key claimed by two
/// or more distinct declarations is ambiguous, and <see cref="Method"/> is <see langword="null"/> so callers
/// fail loudly instead of silently picking a winner.
/// </summary>
internal sealed class ApplyMethodCandidates {
    internal ApplyMethodCandidates(MethodInfo? method, int candidateCount, IReadOnlyList<string> candidateEventTypeNames) {
        Method = method;
        CandidateCount = candidateCount;
        CandidateEventTypeNames = candidateEventTypeNames;
    }

    /// <summary>Gets the single matching Apply method, or <see langword="null"/> when the key is ambiguous.</summary>
    internal MethodInfo? Method { get; }

    /// <summary>Gets the number of distinct Apply declarations registered under the key.</summary>
    internal int CandidateCount { get; }

    /// <summary>Gets the distinct, ordinally sorted full CLR names of the candidate event types.</summary>
    internal IReadOnlyList<string> CandidateEventTypeNames { get; }
}
