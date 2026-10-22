namespace Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>Human claims captured from the authenticated gateway principal, never from command data.</summary>
/// <param name="Tenant">The verified tenant.</param>
/// <param name="Subject">The verified subject.</param>
/// <param name="Permissions">The verified permissions.</param>
public sealed record HumanAuthoritySource(string Tenant, string Subject, string[] Permissions);
