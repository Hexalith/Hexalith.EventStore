using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns the tenant-scoped directory and crash-resumable digest-version promotion pointer.</summary>
public sealed partial class IdempotencyAdmissionDirectoryActor(
    ActorHost host,
    ILogger<IdempotencyAdmissionDirectoryActor> logger)
    : Actor(host), IIdempotencyAdmissionDirectoryActor
{
    /// <summary>Gets the Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(IdempotencyAdmissionDirectoryActor);

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionDirectoryResult> ResolveAsync(
        IdempotencyAdmissionDirectoryRequest request)
    {
        ValidateRequest(request);
        IdempotencyAdmissionDirectoryEntry? entry = await LoadConsistentAsync(request.Aliases)
            .ConfigureAwait(false);
        string[] aliases = request.Aliases.Select(alias => alias.ActorId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (entry is null)
        {
            string canonical = request.ExistingActorId ?? request.ActiveActorId;
            if (!aliases.Contains(canonical, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("The inspected idempotency authority is not a protected directory alias.");
            }

            entry = CreateResolvedEntry(canonical, request.ActiveActorId, aliases);
            await PersistAsync(entry, request.Aliases).ConfigureAwait(false);
            Log.Resolved(logger, entry.PromotionPhase.ToString());
            return ToResult(entry);
        }

        ValidateEntry(entry, aliases);
        string[] mergedAliases = entry.Aliases.Concat(aliases)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        IdempotencyAdmissionDirectoryEntry updated = entry with { Aliases = mergedAliases };
        if (entry.PromotionPhase == IdempotencyAdmissionPromotionPhase.Stable
            && !string.Equals(entry.CanonicalActorId, request.ActiveActorId, StringComparison.Ordinal))
        {
            updated = CreateResolvedEntry(entry.CanonicalActorId, request.ActiveActorId, mergedAliases);
        }
        else if (entry.PromotionPhase != IdempotencyAdmissionPromotionPhase.Stable
            && !string.Equals(entry.PromotionTargetActorId, request.ActiveActorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The active digest-key version changed during an idempotency promotion.");
        }

        await PersistAsync(updated, request.Aliases).ConfigureAwait(false);
        Log.Resolved(logger, updated.PromotionPhase.ToString());
        return ToResult(updated);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionDirectoryResult> AdvanceAsync(
        IdempotencyAdmissionDirectoryAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAliases(request.Aliases);
        IdempotencyAdmissionDirectoryEntry entry = await LoadConsistentAsync(request.Aliases)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The idempotency admission directory entry is missing.");
        if (entry.PromotionPhase != request.ExpectedPhase)
        {
            throw new InvalidOperationException("The idempotency admission promotion phase is stale or invalid.");
        }

        IdempotencyAdmissionDirectoryEntry advanced = request.ExpectedPhase switch
        {
            IdempotencyAdmissionPromotionPhase.PrepareTarget => entry with
            {
                PromotionPhase = IdempotencyAdmissionPromotionPhase.RedirectSource,
            },
            IdempotencyAdmissionPromotionPhase.RedirectSource => entry with
            {
                PromotionPhase = IdempotencyAdmissionPromotionPhase.FlipDirectory,
            },
            IdempotencyAdmissionPromotionPhase.FlipDirectory => entry with
            {
                CanonicalActorId = entry.PromotionTargetActorId
                    ?? throw new InvalidOperationException("The idempotency promotion target is missing."),
                PromotionPhase = IdempotencyAdmissionPromotionPhase.ActivateTarget,
            },
            IdempotencyAdmissionPromotionPhase.ActivateTarget => entry with
            {
                ActiveActorId = entry.PromotionTargetActorId
                    ?? throw new InvalidOperationException("The idempotency promotion target is missing."),
                PromotionPhase = IdempotencyAdmissionPromotionPhase.Stable,
                PromotionSourceActorId = null,
                PromotionTargetActorId = null,
            },
            _ => throw new InvalidOperationException("A stable idempotency admission directory cannot advance."),
        };
        await PersistAsync(advanced, request.Aliases).ConfigureAwait(false);
        Log.Advanced(logger, advanced.PromotionPhase.ToString());
        return ToResult(advanced);
    }

    /// <inheritdoc/>
    public async Task PurgeAliasAsync(IdempotencyAdmissionDirectoryAlias alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        ValidateAliases([alias]);
        _ = await StateManager.TryRemoveStateAsync(BuildStateName(alias)).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <summary>Builds the protected directory state name for one alias.</summary>
    public static string BuildStateName(IdempotencyAdmissionDirectoryAlias alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        return string.Concat("alias:", alias.DigestKeyVersion, ":", alias.KeyDigest);
    }

    private static IdempotencyAdmissionDirectoryEntry CreateResolvedEntry(
        string canonicalActorId,
        string activeActorId,
        IReadOnlyList<string> aliases)
        => string.Equals(canonicalActorId, activeActorId, StringComparison.Ordinal)
            ? new IdempotencyAdmissionDirectoryEntry(
                IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
                canonicalActorId,
                activeActorId,
                aliases,
                IdempotencyAdmissionPromotionPhase.Stable)
            : new IdempotencyAdmissionDirectoryEntry(
                IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
                canonicalActorId,
                activeActorId,
                aliases,
                IdempotencyAdmissionPromotionPhase.PrepareTarget,
                canonicalActorId,
                activeActorId);

    private async Task<IdempotencyAdmissionDirectoryEntry?> LoadConsistentAsync(
        IReadOnlyList<IdempotencyAdmissionDirectoryAlias> aliases)
    {
        IdempotencyAdmissionDirectoryEntry? selected = null;
        foreach (IdempotencyAdmissionDirectoryAlias alias in aliases)
        {
            ConditionalValue<IdempotencyAdmissionDirectoryEntry> stored = await StateManager
                .TryGetStateAsync<IdempotencyAdmissionDirectoryEntry>(BuildStateName(alias))
                .ConfigureAwait(false);
            if (!stored.HasValue)
            {
                continue;
            }

            if (selected is not null && !SameAuthority(selected, stored.Value))
            {
                throw new InvalidOperationException("Protected idempotency directory aliases disagree on canonical authority.");
            }

            selected = stored.Value;
        }

        return selected;
    }

    private async Task PersistAsync(
        IdempotencyAdmissionDirectoryEntry entry,
        IReadOnlyList<IdempotencyAdmissionDirectoryAlias> aliases)
    {
        foreach (IdempotencyAdmissionDirectoryAlias alias in aliases)
        {
            await StateManager.SetStateAsync(BuildStateName(alias), entry).ConfigureAwait(false);
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private static bool SameAuthority(
        IdempotencyAdmissionDirectoryEntry left,
        IdempotencyAdmissionDirectoryEntry right)
        => left.SchemaVersion == right.SchemaVersion
            && string.Equals(left.CanonicalActorId, right.CanonicalActorId, StringComparison.Ordinal)
            && string.Equals(left.ActiveActorId, right.ActiveActorId, StringComparison.Ordinal)
            && left.PromotionPhase == right.PromotionPhase
            && string.Equals(left.PromotionSourceActorId, right.PromotionSourceActorId, StringComparison.Ordinal)
            && string.Equals(left.PromotionTargetActorId, right.PromotionTargetActorId, StringComparison.Ordinal);

    private static void ValidateRequest(IdempotencyAdmissionDirectoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(request.ActiveActorId))
        {
            throw new InvalidOperationException("The idempotency admission directory request is invalid.");
        }

        ValidateAliases(request.Aliases);
        if (!request.Aliases.Any(alias =>
            string.Equals(alias.ActorId, request.ActiveActorId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The active idempotency actor is not a directory alias.");
        }
    }

    private static void ValidateAliases(IReadOnlyList<IdempotencyAdmissionDirectoryAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        if (aliases.Count == 0
            || aliases.Any(alias =>
                alias is null
                || string.IsNullOrWhiteSpace(alias.DigestKeyVersion)
                || string.IsNullOrWhiteSpace(alias.ActorId)
                || string.IsNullOrWhiteSpace(alias.KeyDigest))
            || aliases.Select(alias => alias.ActorId).Distinct(StringComparer.Ordinal).Count() != aliases.Count)
        {
            throw new InvalidOperationException("Protected idempotency directory aliases are invalid.");
        }
    }

    private static void ValidateEntry(
        IdempotencyAdmissionDirectoryEntry entry,
        IReadOnlyList<string> requestAliases)
    {
        if (entry.SchemaVersion != IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(entry.CanonicalActorId)
            || !entry.Aliases.Contains(entry.CanonicalActorId, StringComparer.Ordinal)
            || !entry.Aliases.Any(requestAliases.Contains))
        {
            throw new InvalidOperationException("The idempotency admission directory entry is corrupt.");
        }
    }

    private static IdempotencyAdmissionDirectoryResult ToResult(IdempotencyAdmissionDirectoryEntry entry)
        => new(
            entry.CanonicalActorId,
            entry.PromotionPhase,
            entry.PromotionSourceActorId,
            entry.PromotionTargetActorId);

    private static partial class Log
    {
        [LoggerMessage(EventId = 5060, Level = LogLevel.Debug, Message = "Idempotency directory resolved. PromotionPhase={PromotionPhase}, Stage=IdempotencyDirectoryResolved")]
        public static partial void Resolved(ILogger logger, string promotionPhase);

        [LoggerMessage(EventId = 5061, Level = LogLevel.Information, Message = "Idempotency directory promotion advanced. PromotionPhase={PromotionPhase}, Stage=IdempotencyDirectoryPromotion")]
        public static partial void Advanced(ILogger logger, string promotionPhase);
    }
}
