// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>One frozen registration in the shared policy identity namespace.</summary>
/// <param name="Order">The captured evaluation order.</param>
/// <param name="PolicyId">The captured stable identifier.</param>
/// <param name="Policy">An ordinary policy, when present.</param>
/// <param name="PathPolicy">A canonical path policy, when present.</param>
internal sealed record PersonalDataPolicyRegistration(int Order, string PolicyId, IPersonalDataPolicy? Policy, ICanonicalPersonalDataPathPolicy? PathPolicy);
