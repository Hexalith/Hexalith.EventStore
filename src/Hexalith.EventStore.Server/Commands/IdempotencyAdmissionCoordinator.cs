using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Routes trusted descriptors to the protected tenant/key admission actor.</summary>
public sealed class IdempotencyAdmissionCoordinator(
    IActorProxyFactory actorProxyFactory,
    IdempotencyKeyProtector keyProtector,
    IIdempotencyIntentAdapterRegistry intentAdapterRegistry,
    IdempotencyExecutionContextProtector? executionContextProtector = null) : IIdempotencyAdmissionCoordinator
{
    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionSession?> AdmitAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.IdempotencyKey is null)
        {
            return null;
        }

        TrustedIdempotencyDescriptor descriptor = intentAdapterRegistry.Resolve(command);
        IdempotencyProtectedIdentitySet identities;
        try
        {
            identities = await keyProtector
                .ProtectAsync(
                    command.Tenant,
                    command.IdempotencyKey,
                    descriptor,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(descriptor.CanonicalIntent);
        }
        string? existingActorId = await DiscoverExistingAuthorityAsync(identities, cancellationToken)
            .ConfigureAwait(false);
        IdempotencyAdmissionDirectoryAlias[] aliases = identities.Aliases
            .Select(identity => new IdempotencyAdmissionDirectoryAlias(
                identity.DigestKeyVersion,
                identity.ActorId,
                identity.KeyDigest))
            .ToArray();
        await CreateLifecycleActor(command.Tenant).RegisterAsync(
            aliases.Select(alias => new IdempotencyTenantLifecycleReference(
                alias.ActorId,
                alias.DigestKeyVersion,
                alias.KeyDigest)).ToArray()).ConfigureAwait(false);
        IIdempotencyLegacyInventoryActor legacyInventory = CreateLegacyInventoryActor(command.Tenant);
        IdempotencyLegacyInventoryInspection legacy = await legacyInventory
            .InspectAsync(aliases).ConfigureAwait(false);
        if (legacy.Decision is IdempotencyLegacyInventoryDecision.Uninventoried
            or IdempotencyLegacyInventoryDecision.Unsafe)
        {
            return new IdempotencyAdmissionSession(
                identities.Active.ActorId,
                0,
                IdempotencyAdmissionDecision.UnsafeLegacy);
        }

        if (legacy.Decision == IdempotencyLegacyInventoryDecision.Migrate)
        {
            IdempotencyAdmissionDecision? denied = await CompleteLegacyMigrationAsync(
                legacyInventory,
                legacy.Entry
                    ?? throw new InvalidOperationException("Legacy migration classification omitted its protected entry."),
                identities).ConfigureAwait(false);
            if (denied is not null)
            {
                return new IdempotencyAdmissionSession(
                    identities.Active.ActorId,
                    0,
                    denied.Value);
            }
        }

        IIdempotencyAdmissionDirectoryActor directory = CreateDirectoryActor(command.Tenant);
        IdempotencyAdmissionDirectoryResult directoryResult = await directory.ResolveAsync(
            new IdempotencyAdmissionDirectoryRequest(
                IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
                identities.Active.ActorId,
                aliases,
                existingActorId)).ConfigureAwait(false);
        if (directoryResult.PromotionPhase != IdempotencyAdmissionPromotionPhase.Stable)
        {
            string sourceActorId = directoryResult.PromotionSourceActorId
                ?? throw new InvalidOperationException("The idempotency promotion source is missing.");
            IdempotencyProtectedIdentity sourceIdentity = identities.Aliases.SingleOrDefault(candidate =>
                string.Equals(candidate.ActorId, sourceActorId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The idempotency promotion source is not a protected alias.");
            IdempotencyAdmissionResult sourceResult = await CreateActor(sourceActorId)
                .AdmitAsync(CreateRequest(sourceIdentity, command.MessageId, command.CorrelationId)).ConfigureAwait(false);
            if (sourceResult.Decision is IdempotencyAdmissionDecision.Conflict
                or IdempotencyAdmissionDecision.Collision
                or IdempotencyAdmissionDecision.Corrupt)
            {
                return new IdempotencyAdmissionSession(
                    sourceActorId,
                    sourceResult.FencingToken,
                    sourceResult.Decision,
                    sourceResult.ReplayResult,
                    ExecutionMessageId: sourceResult.ExecutionMessageId,
                    ExecutionCorrelationId: sourceResult.ExecutionCorrelationId);
            }
        }

        directoryResult = await CompletePromotionAsync(
            directory,
            directoryResult,
            aliases,
            identities,
            cancellationToken).ConfigureAwait(false);
        IdempotencyProtectedIdentity identity = identities.Aliases.SingleOrDefault(candidate =>
            string.Equals(candidate.ActorId, directoryResult.CanonicalActorId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The idempotency directory selected an unknown protected actor alias.");
        IIdempotencyAdmissionActor actor = CreateActor(identity.ActorId);
        IdempotencyAdmissionRequest request = CreateRequest(identity, command.MessageId, command.CorrelationId);
        IdempotencyAdmissionResult result = await actor.AdmitAsync(request).ConfigureAwait(false);
        if (result.Decision == IdempotencyAdmissionDecision.Redirect)
        {
            throw new InvalidOperationException("The canonical idempotency authority redirected unexpectedly.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string? executionMessageId = result.ExecutionMessageId;
        string? executionCorrelationId = result.ExecutionCorrelationId;
        IdempotencyExecutionContext? executionContext = result.Decision is
            IdempotencyAdmissionDecision.Execute
                or IdempotencyAdmissionDecision.Recoverable
                or IdempotencyAdmissionDecision.UnknownProviderOutcome
            ? await (executionContextProtector
                ?? throw new InvalidOperationException("Idempotency execution-context protection is unavailable."))
                .ProtectAsync(
                    identity.ActorId,
                    result.FencingToken,
                    identity.DigestKeyVersion,
                    command with
                    {
                        MessageId = executionMessageId
                            ?? throw new InvalidOperationException("Live idempotency state has no execution identity."),
                        CorrelationId = executionCorrelationId
                            ?? throw new InvalidOperationException("Live idempotency state has no checkpoint identity."),
                    },
                    cancellationToken)
                .ConfigureAwait(false)
            : null;
        return new IdempotencyAdmissionSession(
            identity.ActorId,
            result.FencingToken,
            result.Decision,
            result.ReplayResult,
            executionContext,
            executionMessageId,
            executionCorrelationId);
    }

    /// <inheritdoc/>
    public async Task BeginAsync(
        IdempotencyAdmissionSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .BeginAsync(new IdempotencyAdmissionTransitionRequest(session.FencingToken))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    public async Task ValidateExecutionAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        IdempotencyExecutionContext context = session.ExecutionContext
            ?? throw new InvalidOperationException("Executable idempotency admission returned no execution fence.");
        await (executionContextProtector
            ?? throw new InvalidOperationException("Idempotency execution-context protection is unavailable."))
            .ValidateAsync(context, command, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(
        IdempotencyAdmissionSession session,
        CommandProcessingResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .CompleteAsync(new IdempotencyAdmissionCompletionRequest(session.FencingToken, result))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    public async Task MarkRecoveryAsync(
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        await CreateActor(session.ActorId)
            .MarkRecoveryAsync(new IdempotencyAdmissionRecoveryRequest(session.FencingToken, state))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private IIdempotencyAdmissionActor CreateActor(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return actorProxyFactory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
    }

    private IIdempotencyAdmissionDirectoryActor CreateDirectoryActor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return actorProxyFactory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            new ActorId(tenant),
            IdempotencyAdmissionDirectoryActor.ActorTypeName);
    }

    private IIdempotencyTenantLifecycleActor CreateLifecycleActor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return actorProxyFactory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
            new ActorId(tenant),
            IdempotencyTenantLifecycleActor.ActorTypeName);
    }

    private IIdempotencyLegacyInventoryActor CreateLegacyInventoryActor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return actorProxyFactory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
            new ActorId(tenant),
            IdempotencyLegacyInventoryActor.ActorTypeName);
    }

    private async Task<IdempotencyAdmissionDecision?> CompleteLegacyMigrationAsync(
        IIdempotencyLegacyInventoryActor inventory,
        IdempotencyLegacyInventoryEntry entry,
        IdempotencyProtectedIdentitySet identities)
    {
        IdempotencyProtectedIdentity sourceIdentity = identities.Aliases.SingleOrDefault(alias =>
            string.Equals(alias.DigestKeyVersion, entry.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(alias.KeyDigest, entry.KeyDigest, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Legacy inventory references an unavailable digest-key alias.");
        if (!FixedTimeEquals(sourceIdentity.VerificationTag, entry.VerificationTag))
        {
            return IdempotencyAdmissionDecision.Collision;
        }

        if (!FixedTimeEquals(sourceIdentity.IntentDigest, entry.IntentDigest)
            || sourceIdentity.RetentionTier != entry.RetentionTier)
        {
            return IdempotencyAdmissionDecision.Conflict;
        }

        IdempotencyProtectedIdentity target = identities.Active;
        string migrationSourceId = string.Concat("legacy:", entry.SourceEvidenceDigest);
        if (entry.Phase == IdempotencyLegacyMigrationPhase.Inventoried)
        {
            var imported = new IdempotencyAdmissionRecord(
                IdempotencyAdmissionRecord.CurrentSchemaVersion,
                IdempotencyAdmissionState.Terminal,
                target.TenantPartition,
                target.DigestKeyVersion,
                target.KeyDigest,
                target.VerificationTag,
                target.IntentDigest,
                entry.RetentionTier,
                entry.FirstConsumedAt,
                entry.LastObservedAt,
                entry.ReplayExpiresAt,
                FencingToken: 1,
                entry.ReplayResult,
                entry.ExecutionMessageId,
                entry.ExecutionCorrelationId);
            await CreateActor(target.ActorId).PreparePromotionAsync(
                new IdempotencyAdmissionPromotionImportRequest(
                    migrationSourceId,
                    Record: imported)).ConfigureAwait(false);
            entry = await inventory.AdvanceAsync(
                entry.DigestKeyVersion,
                entry.KeyDigest,
                IdempotencyLegacyMigrationPhase.Inventoried,
                target.ActorId).ConfigureAwait(false);
        }

        if (entry.Phase == IdempotencyLegacyMigrationPhase.TargetPrepared)
        {
            await CreateActor(target.ActorId).ActivatePromotionAsync(
                new IdempotencyAdmissionPromotionActivationRequest(migrationSourceId)).ConfigureAwait(false);
            _ = await inventory.AdvanceAsync(
                entry.DigestKeyVersion,
                entry.KeyDigest,
                IdempotencyLegacyMigrationPhase.TargetPrepared,
                target.ActorId).ConfigureAwait(false);
        }

        return null;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        try
        {
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private async Task<string?> DiscoverExistingAuthorityAsync(
        IdempotencyProtectedIdentitySet identities,
        CancellationToken cancellationToken)
    {
        var existing = new List<(IdempotencyProtectedIdentity Identity, IdempotencyAdmissionInspection Inspection)>();
        IEnumerable<IdempotencyProtectedIdentity> readerFirst = identities.Aliases.Skip(1)
            .Concat(identities.Aliases.Take(1));
        foreach (IdempotencyProtectedIdentity identity in readerFirst)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IdempotencyAdmissionInspection inspection = await CreateActor(identity.ActorId)
                .InspectAsync().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Idempotency admission inspection returned no result.");
            if (inspection.Exists)
            {
                existing.Add((identity, inspection));
            }
        }

        if (existing.Count == 0)
        {
            return null;
        }

        if (existing.Count == 1)
        {
            string selected = existing[0].Inspection.RedirectActorId ?? existing[0].Identity.ActorId;
            return identities.Aliases.Any(alias =>
                string.Equals(alias.ActorId, selected, StringComparison.Ordinal))
                ? selected
                : throw new InvalidOperationException("An idempotency admission redirect targets an unknown digest alias.");
        }

        string[] authorities = existing.Select(item => item.Inspection.RedirectActorId ?? item.Identity.ActorId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return authorities.Length == 1
            ? authorities[0]
            : throw new InvalidOperationException("Multiple digest-key versions contain competing idempotency authority.");
    }

    private async Task<IdempotencyAdmissionDirectoryResult> CompletePromotionAsync(
        IIdempotencyAdmissionDirectoryActor directory,
        IdempotencyAdmissionDirectoryResult initial,
        IdempotencyAdmissionDirectoryAlias[] aliases,
        IdempotencyProtectedIdentitySet identities,
        CancellationToken cancellationToken)
    {
        IdempotencyAdmissionDirectoryResult current = initial;
        for (int step = 0; step < 4 && current.PromotionPhase != IdempotencyAdmissionPromotionPhase.Stable; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sourceActorId = current.PromotionSourceActorId
                ?? throw new InvalidOperationException("The idempotency promotion source is missing.");
            string targetActorId = current.PromotionTargetActorId
                ?? throw new InvalidOperationException("The idempotency promotion target is missing.");
            switch (current.PromotionPhase)
            {
                case IdempotencyAdmissionPromotionPhase.PrepareTarget:
                    IdempotencyAdmissionInspection source = await CreateActor(sourceActorId)
                        .InspectAsync().ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The idempotency promotion source returned no inspection.");
                    IdempotencyProtectedIdentity target = identities.Aliases.SingleOrDefault(candidate =>
                        string.Equals(candidate.ActorId, targetActorId, StringComparison.Ordinal))
                        ?? throw new InvalidOperationException("The idempotency promotion target is not a protected alias.");
                    IdempotencyReplayRetentionTier sourceTier = source.Record?.RetentionTier
                        ?? source.Tombstone?.RetentionTier
                        ?? throw new InvalidOperationException("The idempotency promotion source state is missing.");
                    if (sourceTier != target.RetentionTier)
                    {
                        throw new InvalidOperationException("The idempotency promotion retention tier is inconsistent.");
                    }

                    IdempotencyAdmissionPromotionImportRequest importRequest;
                    if (source.Record is not null)
                    {
                        IdempotencyAdmissionRecord imported = source.Record with
                        {
                            DigestKeyVersion = target.DigestKeyVersion,
                            KeyDigest = target.KeyDigest,
                            VerificationTag = target.VerificationTag,
                            IntentDigest = target.IntentDigest,
                        };
                        importRequest = new IdempotencyAdmissionPromotionImportRequest(
                            sourceActorId,
                            Record: imported);
                    }
                    else
                    {
                        IdempotencyAdmissionTombstone imported = source.Tombstone! with
                        {
                            DigestKeyVersion = target.DigestKeyVersion,
                            KeyDigest = target.KeyDigest,
                            VerificationTag = target.VerificationTag,
                        };
                        importRequest = new IdempotencyAdmissionPromotionImportRequest(
                            sourceActorId,
                            Tombstone: imported);
                    }

                    await CreateActor(targetActorId).PreparePromotionAsync(
                        importRequest).ConfigureAwait(false);
                    break;
                case IdempotencyAdmissionPromotionPhase.RedirectSource:
                    await CreateActor(sourceActorId).SetRedirectAsync(
                        new IdempotencyAdmissionRedirectRequest(targetActorId)).ConfigureAwait(false);
                    break;
                case IdempotencyAdmissionPromotionPhase.FlipDirectory:
                    break;
                case IdempotencyAdmissionPromotionPhase.ActivateTarget:
                    await CreateActor(targetActorId).ActivatePromotionAsync(
                        new IdempotencyAdmissionPromotionActivationRequest(sourceActorId)).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("The idempotency promotion phase is invalid.");
            }

            current = await directory.AdvanceAsync(
                new IdempotencyAdmissionDirectoryAdvanceRequest(aliases, current.PromotionPhase))
                .ConfigureAwait(false);
        }

        return current.PromotionPhase == IdempotencyAdmissionPromotionPhase.Stable
            ? current
            : throw new InvalidOperationException("The idempotency promotion did not reach stable authority.");
    }

    private static IdempotencyAdmissionRequest CreateRequest(
        IdempotencyProtectedIdentity identity,
        string executionMessageId,
        string executionCorrelationId)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            identity.TenantPartition,
            identity.DigestKeyVersion,
            identity.KeyDigest,
            identity.VerificationTag,
            identity.IntentDigest,
            identity.RetentionTier,
            executionMessageId,
            executionCorrelationId);
}
