// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>One frozen registration in the shared policy identity namespace.</summary>
/// <param name="Order">The captured evaluation order.</param>
/// <param name="PolicyId">The captured stable identifier.</param>
/// <param name="Policy">An ordinary policy, when present.</param>
/// <param name="PathPolicy">A canonical path policy, when present.</param>
internal sealed record PersonalDataPolicyRegistration(int Order, string PolicyId, IPersonalDataPolicy? Policy, ICanonicalPersonalDataPathPolicy? PathPolicy);
