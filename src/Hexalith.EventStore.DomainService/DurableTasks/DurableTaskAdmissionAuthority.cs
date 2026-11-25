namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Current authority for a specific task intent, supplied by the host's authenticated policy.</summary>
/// <param name="Allowed">Whether the current caller may act as the scope's original actor on the exact intent.</param>
/// <param name="RequiresConfirmation">Whether the configured operation requires interactive confirmation.</param>
/// <param name="ConfirmationBindingSha256">The authoritative hash of the current interactive session, surface, authorization revision, target and version evidence, Preview and confirmation schema. Absent for a caller without current interactive evidence.</param>
public sealed record DurableTaskAdmissionAuthority(bool Allowed, bool RequiresConfirmation, string? ConfirmationBindingSha256);
