namespace Hexalith.EventStore.ProviderVerification;

internal static class VerificationCompleteness
{
    public static bool IsComplete(
        int requestedInteractionCount,
        IReadOnlyCollection<InteractionVerificationResult> results)
        => requestedInteractionCount > 0
            && results.Count == requestedInteractionCount
            && results.Select(result => result.Index).SequenceEqual(Enumerable.Range(1, requestedInteractionCount))
            && results.Select(result => result.Index).Distinct().Count() == requestedInteractionCount
            && results.All(result => result.StateEvents.Count == 2
                && result.StateEvents[0].State == result.ProviderState
                && result.StateEvents[0].Action == "setup"
                && result.StateEvents[0].ResultCode == "state.setup.succeeded"
                && result.StateEvents[1].State == result.ProviderState
                && result.StateEvents[1].Action == "teardown"
                && result.StateEvents[1].ResultCode is "state.teardown.succeeded" or "state.teardown.forced");
}
