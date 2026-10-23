// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Freezes explicitly registered, monotonic policies in normative section 9.2 order.</summary>
internal sealed class PersonalDataPolicyRegistry
{
    /// <summary>The reserved built-in attribute policy identifier.</summary>
    internal const string AttributePolicyId = "eventstore-personal-data-attribute-v1";

    /// <summary>Validates and snapshots the two policy kinds in one identity namespace.</summary>
    internal PersonalDataPolicyRegistry(IEnumerable<IPersonalDataPolicy> policies, IEnumerable<ICanonicalPersonalDataPathPolicy> pathPolicies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(pathPolicies);
        var identities = new HashSet<string>(StringComparer.Ordinal) { AttributePolicyId };
        var entries = new List<PersonalDataPolicyRegistration> { new(0, AttributePolicyId, null, null) };
        foreach (IPersonalDataPolicy policy in policies)
        {
            if (policy is null || policy.ContractVersion != 1 || policy.PolicyVersion < 1)
            {
                throw new PayloadProtectionFormatException();
            }

            Add(policy.Order, policy.PolicyId, policy, null);
        }

        foreach (ICanonicalPersonalDataPathPolicy policy in pathPolicies)
        {
            if (policy is null || policy.ContractVersion != 1 || policy.PolicyVersion < 1)
            {
                throw new PayloadProtectionFormatException();
            }

            Add(policy.Order, policy.PolicyId, null, policy);
        }

        Entries = Array.AsReadOnly(entries.OrderBy(e => e.Order).ThenBy(e => e.PolicyId, StringComparer.Ordinal).ToArray());

        void Add(int order, string id, IPersonalDataPolicy? policy, ICanonicalPersonalDataPathPolicy? paths)
        {
            ValidateId(id);
            if (!identities.Add(id))
            {
                throw new PayloadProtectionFormatException();
            }

            entries.Add(new(order, id, policy, paths));
        }
    }

    /// <summary>Gets the immutable evaluation order, including the built-in marker.</summary>
    internal IReadOnlyList<PersonalDataPolicyRegistration> Entries { get; }

    /// <summary>Validates a printable lowercase ASCII kebab identifier.</summary>
    internal static void ValidateId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id[0] == '-' || id[^1] == '-')
        {
            throw new PayloadProtectionFormatException();
        }

        bool dash = false;
        foreach (char c in id)
        {
            if (c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') && c != '-' || (c == '-' && dash))
            {
                throw new PayloadProtectionFormatException();
            }

            dash = c == '-';
        }
    }
}
