namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Monotonic decision returned by a personal-data policy.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public enum PersonalDataPolicyDecision {
    /// <summary>The policy makes no positive selection.</summary>
    Abstain = 0,

    /// <summary>The policy selects the serialized value for protection.</summary>
    Protect = 1,
}
