// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.6; state-only contention recovery.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Bounds fresh-observation state retries after conflicts or reconciliation of an unknown commit; backend mutations never use this path.</summary>
internal static class PayloadStateCasRetry
{
    /// <summary>Yields between at most 64 state-only attempts within the enclosing operation budget.</summary>
    internal static async ValueTask PauseAsync(int attempt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        PayloadOperationBudget.RequireCurrent();
        if (attempt >= 63) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        PayloadOperationBudget.RequireCurrent();
    }
}
