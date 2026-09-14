namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Selects serialized values for personal-data protection using monotonic positive decisions.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public interface IPersonalDataPolicy {
    /// <summary>Gets the exact public contract version.</summary>
    int ContractVersion => 1;

    /// <summary>Gets the unique printable ASCII kebab-case policy identifier.</summary>
    string PolicyId { get; }

    /// <summary>Gets the positive policy implementation version.</summary>
    int PolicyVersion { get; }

    /// <summary>Gets the deterministic primary ordering value.</summary>
    int Order { get; }

    /// <summary>Evaluates one exact serialized value.</summary>
    /// <param name="context">The call-scoped typed and serialized value context.</param>
    /// <returns>A monotonic personal-data policy decision.</returns>
    PersonalDataPolicyDecision Evaluate(PersonalDataPolicyContext context);
}
