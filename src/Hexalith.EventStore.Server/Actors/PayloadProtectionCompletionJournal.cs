using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Content-free original completion identities committed atomically with their source events or snapshot.</summary>
/// <param name="Originals">Exact original call contexts retained as separate actor coordination state.</param>
/// <param name="Completed">Whether independently verified original completions have all returned their exact active outcomes.</param>
internal sealed record PayloadProtectionCompletionJournal(PayloadProtectionCompletionContext[] Originals, bool Completed = false)
{
    /// <summary>Gets the actor-local coordination key, never an event payload or metadata field.</summary>
    internal const string StateName = "payload_protection_completion_v1";
}
