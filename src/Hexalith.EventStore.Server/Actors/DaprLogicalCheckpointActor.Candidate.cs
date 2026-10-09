using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>Captures only privately owned checkpoint candidates under the existing exact read-only decision.</summary>
internal sealed partial class DaprLogicalCheckpointActor
{
    /// <summary>Captures two fresh exact decisions without issuing a claim, serving state or transferring uncertain staging.</summary>
    internal async Task<DaprLogicalCheckpointCandidate> AcquireLogicalCheckpointCandidateAsync(string model, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, DaprReplayOperationOwner operation, int maximumStateBytes, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (model != DaprLogicalCheckpointCandidate.ModelId)
        {
            throw new InvalidOperationException("CheckpointCandidateHold: explicit private candidate model required.");
        }

        async Task<DaprLogicalCheckpointCandidate> Refresh(CancellationToken boundary)
        {
            token.ThrowIfCancellationRequested();
            if (boundary != token)
            {
                throw new InvalidOperationException("CheckpointCandidateHold: original token required before actual reads.");
            }

            DaprLogicalCheckpointCandidate? captured = null;
            bool open = true;
            bool returned = false;
            try
            {
                DaprReplayCommitOutcome outcome = await LogicalCheckpointAsync(DaprLogicalCheckpointCodec.ModelId, true, fold, binding, source, trust, operation, maximumStateBytes, ownerFence, boundary, write =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!open || captured is not null)
                    {
                        throw new InvalidOperationException("CheckpointCandidateHold: capture decision expired or repeated.");
                    }

                    captured = new DaprLogicalCheckpointCandidate(write, fold, binding, trust, Refresh, token);
                }).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (outcome != DaprReplayCommitOutcome.Proven || captured is null)
                {
                    throw new InvalidOperationException("CheckpointCandidateHold: exact fresh readback was not Proven.");
                }

                captured.RequirePins(boundary);
                returned = true;
                return captured;
            }
            finally
            {
                open = false;
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (!returned)
                    {
                        captured?.Dispose();
                    }
                }
                catch
                {
                    captured?.Dispose();
                    throw;
                }
            }
        }

        DaprLogicalCheckpointCandidate? candidate = null;
        bool successfulReturn = false;
        try
        {
            candidate = await Refresh(token).ConfigureAwait(false);
            // Revalidation captures another complete image set after the first decision and its cleanup finish.
            await candidate.RequireCurrentAsync(token).ConfigureAwait(false);
            candidate.RequirePins(token);
            successfulReturn = true;
            return candidate;
        }
        finally
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (!successfulReturn)
                {
                    candidate?.Dispose();
                }
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }
        }
    }
}
