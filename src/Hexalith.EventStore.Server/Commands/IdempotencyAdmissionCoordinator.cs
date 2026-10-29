using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Routes trusted descriptors to the protected tenant/key admission actor.</summary>
public sealed class IdempotencyAdmissionCoordinator : IIdempotencyAdmissionCoordinator
{
    private IActorProxyFactory actorProxyFactory { get; }
    private IdempotencyKeyProtector keyProtector { get; }
    private IIdempotencyIntentAdapterRegistry intentAdapterRegistry { get; }
    private IdempotencyExecutionContextProtector? executionContextProtector { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }
    private Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.Server.Configuration.IdempotencyAdmissionOptions>? admissionOptions { get; }
    private Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogHostOptions>? catalogOptions { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public IdempotencyAdmissionCoordinator(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        IActorProxyFactory actorProxyFactory,
        IdempotencyKeyProtector keyProtector,
        IIdempotencyIntentAdapterRegistry intentAdapterRegistry,
        IdempotencyExecutionContextProtector? executionContextProtector = null,
        Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.Server.Configuration.IdempotencyAdmissionOptions>? admissionOptions = null,
        Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogHostOptions>? catalogOptions = null)
        : this(actorProxyFactory, keyProtector, intentAdapterRegistry, executionContextProtector, authority, admissionOptions, catalogOptions)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal IdempotencyAdmissionCoordinator(
        IActorProxyFactory actorProxyFactory,
        IdempotencyKeyProtector keyProtector,
        IIdempotencyIntentAdapterRegistry intentAdapterRegistry,
        IdempotencyExecutionContextProtector? executionContextProtector = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null,
        Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.Server.Configuration.IdempotencyAdmissionOptions>? admissionOptions = null,
        Microsoft.Extensions.Options.IOptions<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogHostOptions>? catalogOptions = null)
    {
        this.actorProxyFactory = actorProxyFactory;
        this.keyProtector = keyProtector;
        this.intentAdapterRegistry = intentAdapterRegistry;
        this.executionContextProtector = executionContextProtector;
        this.catalogAuthority = catalogAuthority;
        this.admissionOptions = admissionOptions;
        this.catalogOptions = catalogOptions;

    }

    /// <inheritdoc/>
    public async Task<IdempotencyAdmissionSession?> AdmitAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        if (catalogLease is not null)
        {
            var selected = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
            var admission = catalogLease.Snapshot.Admission(selected.Declaration);
            var configured = admissionOptions?.Value;
            if (configured is null || !configured.Enabled || admission.ConsumerAppId != catalogOptions?.Value.Identity?.AppId
                || configured.ActiveDigestKeyVersion != admission.ActiveDigestKeyGeneration
                || !new[] { configured.ActiveDigestKeyVersion }.Concat(configured.ReaderDigestKeyVersions).SequenceEqual(admission.ReaderDigestKeyGenerations, StringComparer.Ordinal)
                || configured.DigestKeySource == Hexalith.EventStore.Server.Configuration.IdempotencyDigestKeySource.DaprSecret
                    && (configured.DigestKeySecretStoreName != admission.SecretStore || configured.DigestKeySecretName != admission.LogicalSecretName
                        || configured.DigestKeySecretGeneration != admission.SecretMapGeneration || admission.KeyMap.Any(k => k.MapKey != k.Generation)))
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("Idempotency key policy differs from the committed admission facet.");
        }
        if (command.IdempotencyKey is null)
        {
            return null;
        }

        TrustedIdempotencyDescriptor descriptor = await intentAdapterRegistry.ResolveAsync(command, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The trusted intent registry returned no descriptor.");
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
        IdempotencyAdmissionDirectoryAlias[] aliases = identities.Aliases
            .Select(identity => new IdempotencyAdmissionDirectoryAlias(
                identity.DigestKeyVersion,
                identity.ActorId,
                identity.KeyDigest))
            .ToArray();
        {
            IIdempotencyLegacyInventoryActor legacyInventory = CreateLegacyInventoryActor(command.Tenant);
            IdempotencyLegacyInventoryInspection legacy = await RouteCatalogRemoteCall.InvokeAsync(() => legacyInventory
                .InspectAsync(aliases)).ConfigureAwait(false);
            if (legacy.Decision is IdempotencyLegacyInventoryDecision.Uninventoried
                or IdempotencyLegacyInventoryDecision.Unsafe)
            {
                return new IdempotencyAdmissionSession(
                    identities.Active.ActorId,
                    0,
                    IdempotencyAdmissionDecision.UnsafeLegacy);
            }

            await RouteCatalogRemoteCall.InvokeAsync(CreateLifecycleActor(command.Tenant), catalogRemoteProxy => catalogRemoteProxy.RegisterAsync(
                aliases.Select(alias => new IdempotencyTenantLifecycleReference(
                    alias.ActorId,
                    alias.DigestKeyVersion,
                    alias.KeyDigest)).ToArray())).ConfigureAwait(false);
            string? existingActorId;

            if (legacy.Decision is IdempotencyLegacyInventoryDecision.Migrate
                or IdempotencyLegacyInventoryDecision.Migrated)
            {
                IdempotencyLegacyMigrationResult migration = await CompleteLegacyMigrationAsync(
                    command.Tenant,
                    legacy.Entry
                        ?? throw new InvalidOperationException("Legacy migration classification omitted its protected entry."),
                    identities).ConfigureAwait(false);
                if (migration.DeniedDecision is not null)
                {
                    return new IdempotencyAdmissionSession(
                        identities.Active.ActorId,
                        0,
                        migration.DeniedDecision.Value);
                }

                existingActorId = migration.TargetAdmissionActorId;
            }
            else
            {
                existingActorId = await DiscoverExistingAuthorityAsync(identities, cancellationToken)
                    .ConfigureAwait(false);
            }

            IIdempotencyAdmissionDirectoryActor directory = CreateDirectoryActor(command.Tenant);
            IdempotencyAdmissionDirectoryResult directoryResult = await RouteCatalogRemoteCall.InvokeAsync(() => directory.ResolveAsync(
                new IdempotencyAdmissionDirectoryRequest(
                    IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
                    identities.Active.ActorId,
                    aliases,
                    existingActorId))).ConfigureAwait(false);
            if (directoryResult.PromotionPhase != IdempotencyAdmissionPromotionPhase.Stable)
            {
                string sourceActorId = directoryResult.PromotionSourceActorId
                    ?? throw new InvalidOperationException("The idempotency promotion source is missing.");
                IdempotencyProtectedIdentity sourceIdentity = identities.Aliases.SingleOrDefault(candidate =>
                    string.Equals(candidate.ActorId, sourceActorId, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException("The idempotency promotion source is not a protected alias.");
                IdempotencyAdmissionResult sourceResult = await AdmitThroughLifecycleAsync(
                    command.Tenant,
                    sourceIdentity,
                    CreateRequest(sourceIdentity, command.MessageId, command.CorrelationId)).ConfigureAwait(false);
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
            IdempotencyAdmissionRequest request = CreateRequest(identity, command.MessageId, command.CorrelationId);
            IdempotencyAdmissionResult result = await AdmitThroughLifecycleAsync(
                command.Tenant,
                identity,
                request).ConfigureAwait(false);
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
                executionCorrelationId,
                CatalogRootDigest: catalogLease?.Snapshot.RootDigest,
                CatalogEpoch: catalogLease?.Snapshot.Epoch);
        }
    }

    /// <inheritdoc/>
    public async Task BeginAsync(
        IdempotencyAdmissionSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalogLease = await AcquireSessionLeaseAsync(session, cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        {
            await RouteCatalogRemoteCall.InvokeAsync(CreateActor(session.ActorId), catalogRemoteProxy => catalogRemoteProxy
                .BeginAsync(new IdempotencyAdmissionTransitionRequest(session.FencingToken))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public async Task ValidateExecutionCapabilityAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalogLease = await AcquireSessionLeaseAsync(session, cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(command);
        if (catalogLease is not null)
        {
            var selected = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
            _ = catalogLease.Snapshot.Admission(selected.Declaration);
        }
        if (session.Decision is not (IdempotencyAdmissionDecision.Execute
            or IdempotencyAdmissionDecision.Recoverable))
        {
            throw new InvalidOperationException("The idempotency admission session is not executable.");
        }

        IdempotencyExecutionContext context = RequireSessionBoundContext(session);
        {
            await (executionContextProtector
                ?? throw new InvalidOperationException("Idempotency execution-context protection is unavailable."))
                .ValidateCapabilityAsync(context, command, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task ValidateExecutionAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalogLease = await AcquireSessionLeaseAsync(session, cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(command);
        if (catalogLease is not null)
        {
            var selected = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
            _ = catalogLease.Snapshot.Admission(selected.Declaration);
        }
        IdempotencyExecutionContext context = RequireSessionBoundContext(session);
        IdempotencyExecutionContextProtector protector = executionContextProtector
            ?? throw new InvalidOperationException("Idempotency execution-context protection is unavailable.");
        {
            if (session.Decision == IdempotencyAdmissionDecision.UnknownProviderOutcome)
            {
                await protector.ValidateReconciliationAsync(context, command, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (session.Decision is not (IdempotencyAdmissionDecision.Execute
                or IdempotencyAdmissionDecision.Recoverable))
            {
                throw new InvalidOperationException("The idempotency admission session is not executable.");
            }

            await protector.ValidateAsync(context, command, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(
        IdempotencyAdmissionSession session,
        CommandProcessingResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalogLease = await AcquireSessionLeaseAsync(session, cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        {
            await RouteCatalogRemoteCall.InvokeAsync(CreateActor(session.ActorId), catalogRemoteProxy => catalogRemoteProxy
                .CompleteAsync(new IdempotencyAdmissionCompletionRequest(session.FencingToken, result))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public async Task MarkRecoveryAsync(
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var catalogLease = await AcquireSessionLeaseAsync(session, cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        {
            await RouteCatalogRemoteCall.InvokeAsync(CreateActor(session.ActorId), catalogRemoteProxy => catalogRemoteProxy
                .MarkRecoveryAsync(new IdempotencyAdmissionRecoveryRequest(session.FencingToken, state))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogDispatchLease?> AcquireSessionLeaseAsync(
        IdempotencyAdmissionSession session, CancellationToken cancellationToken)
    {
        if (catalogAuthority is null) return null;
        var lease = await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (session.CatalogRootDigest == lease.Snapshot.RootDigest && session.CatalogEpoch == lease.Snapshot.Epoch) return lease;
        await lease.DisposeAsync().ConfigureAwait(false);
        throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The admission session belongs to a different catalog generation or epoch.");
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

    private Task<IdempotencyAdmissionResult> AdmitThroughLifecycleAsync(
        string tenant,
        IdempotencyProtectedIdentity identity,
        IdempotencyAdmissionRequest request)
        => RouteCatalogRemoteCall.InvokeAsync(CreateLifecycleActor(tenant), catalogRemoteProxy => catalogRemoteProxy.AdmitAsync(
            new IdempotencyTenantLifecycleAdmissionRequest(
                new IdempotencyTenantLifecycleReference(
                    identity.ActorId,
                    identity.DigestKeyVersion,
                    identity.KeyDigest),
                request)));

    private IIdempotencyLegacyInventoryActor CreateLegacyInventoryActor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return actorProxyFactory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
            new ActorId(tenant),
            IdempotencyLegacyInventoryActor.ActorTypeName);
    }

    private IIdempotencyTenantLifecycleMigrationActor CreateLifecycleMigrationActor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return actorProxyFactory.CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
            new ActorId(tenant),
            IdempotencyTenantLifecycleActor.ActorTypeName);
    }

    private static IdempotencyExecutionContext RequireSessionBoundContext(IdempotencyAdmissionSession session)
    {
        IdempotencyExecutionContext context = session.ExecutionContext
            ?? throw new InvalidOperationException("Executable idempotency admission returned no execution fence.");
        if (!string.Equals(context.AdmissionActorId, session.ActorId, StringComparison.Ordinal)
            || context.FencingToken != session.FencingToken
            || !string.Equals(context.MessageId, session.ExecutionMessageId, StringComparison.Ordinal)
            || !string.Equals(context.CorrelationId, session.ExecutionCorrelationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution session does not match its signed capability.");
        }

        return context;
    }

    private async Task<IdempotencyLegacyMigrationResult> CompleteLegacyMigrationAsync(
        string tenant,
        IdempotencyLegacyInventoryEntry entry,
        IdempotencyProtectedIdentitySet identities)
    {
        IdempotencyProtectedIdentity sourceIdentity = identities.Aliases.SingleOrDefault(alias =>
            string.Equals(alias.DigestKeyVersion, entry.DigestKeyVersion, StringComparison.Ordinal)
            && string.Equals(alias.KeyDigest, entry.KeyDigest, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Legacy inventory references an unavailable digest-key alias.");
        string pinnedTargetActorId = entry.TargetAdmissionActorId ?? identities.Active.ActorId;
        IdempotencyProtectedIdentity target = identities.Aliases.SingleOrDefault(alias =>
            string.Equals(alias.ActorId, pinnedTargetActorId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Legacy migration pinned target is not a retained digest-key alias.");
        IdempotencyLegacyMigrationResult result = await RouteCatalogRemoteCall.InvokeAsync(CreateLifecycleMigrationActor(tenant), catalogRemoteProxy => catalogRemoteProxy
            .MigrateLegacyAsync(
                new IdempotencyLegacyMigrationRequest(
                    identities.Aliases.Select(identity => new IdempotencyAdmissionDirectoryAlias(
                        identity.DigestKeyVersion,
                        identity.ActorId,
                        identity.KeyDigest)).ToArray(),
                    new IdempotencyTenantLifecycleReference(
                        target.ActorId,
                        target.DigestKeyVersion,
                        target.KeyDigest),
                    target.VerificationTag,
                    target.IntentDigest,
                    target.RetentionTier,
                    sourceIdentity.VerificationTag,
                    sourceIdentity.IntentDigest,
                    sourceIdentity.RetentionTier))).ConfigureAwait(false);
        if (!identities.Aliases.Any(alias => string.Equals(
            result.TargetAdmissionActorId,
            alias.ActorId,
            StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Legacy migration returned an authority outside the retained alias set.");
        }

        return result;
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
            IdempotencyAdmissionInspection inspection = await RouteCatalogRemoteCall.InvokeAsync(CreateActor(identity.ActorId), catalogRemoteProxy => catalogRemoteProxy
                .InspectAsync()).ConfigureAwait(false)
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
                    IdempotencyAdmissionInspection source = await RouteCatalogRemoteCall.InvokeAsync(CreateActor(sourceActorId), catalogRemoteProxy => catalogRemoteProxy
                        .InspectAsync()).ConfigureAwait(false)
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
                    string migrationId = IdempotencyAdmissionPromotionEvidence
                        .BuildConventionalMigrationId(sourceActorId, targetActorId);
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
                            Record: imported,
                            MigrationId: migrationId);
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
                            Tombstone: imported,
                            MigrationId: migrationId);
                    }

                    string importDigest = IdempotencyAdmissionPromotionEvidence.Compute(
                        importRequest.Record,
                        importRequest.Tombstone);
                    importRequest = importRequest with { SourceEvidenceDigest = importDigest };
                    IIdempotencyAdmissionActor targetActor = CreateActor(targetActorId);
                    await RouteCatalogRemoteCall.InvokeAsync(() => targetActor.PreparePromotionAsync(importRequest)).ConfigureAwait(false);
                    IdempotencyAdmissionPromotionAcknowledgement acknowledgement = await RouteCatalogRemoteCall.InvokeAsync(() => targetActor
                        .AcknowledgePromotionAsync(
                            new IdempotencyAdmissionPromotionAcknowledgementRequest(
                                sourceActorId,
                                migrationId,
                                importDigest,
                                importDigest))).ConfigureAwait(false);
                    if (acknowledgement.Activated)
                    {
                        throw new InvalidOperationException("The promotion target activated before source redirect.");
                    }
                    break;
                case IdempotencyAdmissionPromotionPhase.RedirectSource:
                    _ = await RequireOrdinaryPromotionAcknowledgementAsync(
                        sourceActorId,
                        targetActorId,
                        activated: false).ConfigureAwait(false);
                    await RouteCatalogRemoteCall.InvokeAsync(CreateActor(sourceActorId), catalogRemoteProxy => catalogRemoteProxy.SetRedirectAsync(
                        new IdempotencyAdmissionRedirectRequest(targetActorId))).ConfigureAwait(false);
                    break;
                case IdempotencyAdmissionPromotionPhase.FlipDirectory:
                    break;
                case IdempotencyAdmissionPromotionPhase.ActivateTarget:
                    IdempotencyAdmissionPromotionAcknowledgement prepared
                        = await RequireOrdinaryPromotionAcknowledgementAsync(
                            sourceActorId,
                            targetActorId,
                            activated: null).ConfigureAwait(false);
                    await RouteCatalogRemoteCall.InvokeAsync(CreateActor(targetActorId), catalogRemoteProxy => catalogRemoteProxy.ActivatePromotionAsync(
                        new IdempotencyAdmissionPromotionActivationRequest(
                            sourceActorId,
                            prepared.MigrationId,
                            prepared.ImportDigest))).ConfigureAwait(false);
                    _ = await RequireOrdinaryPromotionAcknowledgementAsync(
                        sourceActorId,
                        targetActorId,
                        activated: true).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("The idempotency promotion phase is invalid.");
            }

            current = await RouteCatalogRemoteCall.InvokeAsync(() => directory.AdvanceAsync(
                new IdempotencyAdmissionDirectoryAdvanceRequest(aliases, current.PromotionPhase))).ConfigureAwait(false);
        }

        return current.PromotionPhase == IdempotencyAdmissionPromotionPhase.Stable
            ? current
            : throw new InvalidOperationException("The idempotency promotion did not reach stable authority.");
    }

    private async Task<IdempotencyAdmissionPromotionAcknowledgement>
        RequireOrdinaryPromotionAcknowledgementAsync(
            string sourceActorId,
            string targetActorId,
            bool? activated)
    {
        IdempotencyAdmissionInspection inspection = await RouteCatalogRemoteCall.InvokeAsync(CreateActor(targetActorId), catalogRemoteProxy => catalogRemoteProxy
            .InspectAsync()).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The idempotency promotion target returned no inspection.");
        IdempotencyAdmissionPromotionAcknowledgement acknowledgement = inspection.Promotion
            ?? throw new InvalidOperationException("The idempotency promotion target acknowledgement is missing.");
        string migrationId = IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
            sourceActorId,
            targetActorId);
        if (!string.Equals(acknowledgement.SourceActorId, sourceActorId, StringComparison.Ordinal)
            || !string.Equals(acknowledgement.MigrationId, migrationId, StringComparison.Ordinal)
            || !string.Equals(
                acknowledgement.SourceEvidenceDigest,
                acknowledgement.ImportDigest,
                StringComparison.Ordinal)
            || (activated is not null && acknowledgement.Activated != activated.Value))
        {
            throw new InvalidOperationException("The idempotency promotion target proof is stale or corrupt.");
        }

        return acknowledgement;
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
