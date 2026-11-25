namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Maps a reserved task identifier to its admission row.</summary>
/// <param name="TaskId">The reserved task identifier.</param>
/// <param name="AdmissionKey">The storage key of the scoped admission row.</param>
/// <remarks>The reservation is written before the admission commit. A reservation whose admission row names another task is an orphan and resolves to no task.</remarks>
internal sealed record DurableTaskIndexState(string TaskId, string AdmissionKey);
