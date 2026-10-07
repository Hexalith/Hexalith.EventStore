namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Describes the workload assertion an outbound internal call needs.
/// </summary>
/// <param name="Audience">The receiving workload audience, normally the receiver's Dapr application id.</param>
/// <param name="Operation">The single operation the call performs.</param>
/// <param name="Bindings">
/// Optional resource bindings (claim types starting with <see cref="EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix"/>).
/// A signing issuer embeds them; an issuer whose tokens cannot carry per-request claims refuses the request
/// (see <see cref="IWorkloadAssertionIssuer.CanBindResources"/>), so a bound request never yields an unbound assertion.
/// </param>
public sealed record WorkloadAssertionRequest(
    string Audience,
    string Operation,
    IReadOnlyDictionary<string, string>? Bindings = null);
