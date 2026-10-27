namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Human admission metadata, independent of the workload credential on the target hop.</summary>
/// <param name="Namespace">An explicit platform namespace matching <c>platform:[a-z0-9]+(-[a-z0-9]+)*</c>, disjoint from tenant identifiers.</param>
/// <param name="RequiredHumanPermission">One exact human permission. Wildcards and administrator bypass are not part of this declaration.</param>
public sealed record PlatformAdmissionDeclaration(string Namespace, string RequiredHumanPermission);
