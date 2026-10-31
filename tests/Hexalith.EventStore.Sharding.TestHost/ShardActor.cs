using System.Text;

using Dapr.Actors.Runtime;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>Test-only serialized authority for one exact pair and its durable ceiling.</summary>
/// <param name="host">The synthetic actor host.</param>
/// <param name="fence">The exclusive run fence.</param>
/// <param name="attempts">Optional durable execution observations.</param>
public sealed class ShardActor(ActorHost host, RunFence fence, AttemptJournal? attempts = null) : Actor(host), IShardActor
{
    /// <summary>The isolated actor type.</summary>
    public const string ActorType = "IsolatedShardActor";

    /// <summary>The single saved admission and ceiling key.</summary>
    public const string StateKey = "reservation";

    /// <summary>The optional test-only second key selecting the provider's explicit transaction path.</summary>
    public const string CommitWitnessStateKey = "commit-witness";

    /// <inheritdoc/>
    public async Task<ShardReply> ApplyAsync(ShardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        attempts?.Record(request, Id.GetId(), "actor-entry");
        string outcome = "failed";
        try
        {
            ShardReply reply = await ApplyCoreAsync(request);
            outcome = reply.Code;
            return reply;
        }
        finally
        {
            // A caller timeout is not actor termination. This flushed observation follows
            // the complete turn body, including failed-save cache eviction.
            attempts?.Record(request, Id.GetId(), "actor-exit", outcome);
        }
    }

    private async Task<ShardReply> ApplyCoreAsync(ShardRequest request)
    {
        if (fence.IsExpired)
        {
            return new(false, "expired");
        }

        try
        {
            if (ShardIdentity.Encode(request.TenantId, request.Domain) != Id.GetId())
            {
                return new(false, "routing-mismatch");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or EncoderFallbackException)
        {
            return new(false, "invalid-identity");
        }

        if (request.Generation <= 0)
        {
            return new(false, "invalid-generation");
        }

        ConditionalValue<ShardState> saved = await StateManager.TryGetStateAsync<ShardState>(StateKey, fence.Token);
        ShardState? current = saved.HasValue ? saved.Value : null;
        if (saved.HasValue && (current is null || current.TenantId != request.TenantId || current.Domain != request.Domain
            || current.Generation <= 0 || current.Ceiling < 0))
        {
            return new(false, "invalid-state");
        }

        if (current is not null && current.Generation != request.Generation)
        {
            return new(false, "generation-conflict", current);
        }

        ShardState next;
        long first = 0;
        int count = 0;
        switch (request.Operation)
        {
            case "Inspect":
                return new(true, "inspected", current);
            case "Admit":
                if (current is not null)
                {
                    return new(true, "readmitted", current);
                }

                next = new(request.TenantId, request.Domain, request.Generation, 0);
                break;
            case "Reserve":
                if (request.Count <= 0)
                {
                    return new(false, "invalid-count", current);
                }

                if (current is null)
                {
                    return new(false, "not-admitted");
                }

                try
                {
                    first = checked(current.Ceiling + 1);
                    next = current with { Ceiling = checked(current.Ceiling + request.Count) };
                }
                catch (OverflowException)
                {
                    return new(false, "overflow", current);
                }

                count = request.Count;
                break;
            case "Seed":
                if (current is null || current.Ceiling != 0 || request.SeedCeiling < 0)
                {
                    return new(false, "invalid-seed", current);
                }

                next = current with { Ceiling = request.SeedCeiling };
                break;
            default:
                return new(false, "invalid-operation", current);
        }

        // All validation and checked arithmetic precede staging. The host and fixture
        // share the same exclusive real-UTC deadline; cancellation drains in-flight work.
        if (fence.IsExpired)
        {
            return new(false, "expired", current);
        }

        try
        {
            await StateManager.SetStateAsync(StateKey, next, fence.Token);
            if (attempts?.CommitWitnessEnabled == true)
            {
                if (request.OperationId is null) throw new InvalidOperationException("The two-key COMMIT experiment requires an operation identity.");
                await StateManager.SetStateAsync(CommitWitnessStateKey, new ShardCommitWitness(request.OperationId, next), fence.Token);
            }
            if (fence.IsExpired)
            {
                await StateManager.ClearCacheAsync(CancellationToken.None);
                return new(false, "expired", current);
            }
        }
        catch (OperationCanceledException) when (fence.IsExpired)
        {
            // Evict this turn's staged update so runtime turn completion cannot save it.
            await StateManager.ClearCacheAsync(CancellationToken.None);
            return new(false, "expired", current);
        }
        catch
        {
            await StateManager.ClearCacheAsync(CancellationToken.None);
            throw;
        }

        try
        {
            attempts?.Record(request, Id.GetId(), "save-start");
            await StateManager.SaveStateAsync(fence.Token);
            attempts?.Record(request, Id.GetId(), "save-complete");
        }
        catch (Exception exception)
        {
            // Once saving starts, cancellation or transport failure is ambiguous:
            // the provider may have committed. Never report an unchanged-state denial,
            // and never let a later turn flush or allocate from this pending cache.
            await StateManager.ClearCacheAsync(CancellationToken.None);
            attempts?.Record(request, Id.GetId(), "save-failed", exception.GetType().Name);
            throw;
        }

        return new(true, "saved", next, first, count);
    }
}
