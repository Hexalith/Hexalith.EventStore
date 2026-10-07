namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Marks one JWT bearer scheme as a workload-assertion scheme governed by the shared JWT contract.
/// </summary>
/// <param name="SchemeName">The authentication scheme name.</param>
public sealed record WorkloadSchemeRegistration(string SchemeName);
