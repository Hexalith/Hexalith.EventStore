using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes protected legacy inventory and migration redirect phases for one tenant.</summary>
public sealed class IdempotencyLegacyInventoryActor(
    ActorHost host,
    IOptions<IdempotencyAdmissionOptions> options)
    : Actor(host), IIdempotencyLegacyInventoryActor
{
    /// <summary>Gets the fixed tenant inventory manifest state name.</summary>
    public const string ManifestStateName = "manifest";

    /// <summary>Gets the Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(IdempotencyLegacyInventoryActor);

    private bool RequireInventory { get; } = options.Value.RequireLegacyInventory;

    /// <inheritdoc/>
    public async Task InventoryAsync(IdempotencyLegacyInventoryEntry entry)
    {
        Validate(entry);
        if (!string.Equals(entry.TenantPartition, Host.Id.GetId(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy inventory tenant does not match its actor partition.");
        }

        ConditionalValue<IdempotencyLegacyInventoryManifest> storedManifest = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryManifest>(ManifestStateName)
            .ConfigureAwait(false);
        IdempotencyLegacyInventoryManifest manifest = storedManifest.HasValue
            ? storedManifest.Value
            : new IdempotencyLegacyInventoryManifest(
                IdempotencyLegacyInventoryManifest.CurrentSchemaVersion,
                entry.TenantPartition,
                entry.InventoryId,
                entry.InventoryVersion,
                Closed: false,
                [],
                []);
        ValidateManifest(manifest);
        if (manifest.Closed
            || !string.Equals(manifest.InventoryId, entry.InventoryId, StringComparison.Ordinal)
            || manifest.InventoryVersion != entry.InventoryVersion)
        {
            throw new InvalidOperationException("Legacy inventory is closed or belongs to a different versioned manifest.");
        }

        string stateName = BuildStateName(entry.DigestKeyVersion, entry.KeyDigest);
        ConditionalValue<IdempotencyLegacyInventoryEntry> existing = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(stateName)
            .ConfigureAwait(false);
        if (existing.HasValue)
        {
            if (!Equals(existing.Value, entry))
            {
                throw new InvalidOperationException("Different legacy evidence is already inventoried for this protected key.");
            }

            string existingDigest = IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(entry);
            if (!manifest.Entries.Any(binding =>
                string.Equals(binding.DigestKeyVersion, entry.DigestKeyVersion, StringComparison.Ordinal)
                && string.Equals(binding.KeyDigest, entry.KeyDigest, StringComparison.Ordinal)
                && string.Equals(binding.EntryDigest, existingDigest, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Legacy inventory manifest omits an existing protected entry.");
            }

            return;
        }

        string entryDigest = IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(entry);
        IdempotencyLegacyInventoryManifestEntry[] entries = manifest.Entries.Append(
                new IdempotencyLegacyInventoryManifestEntry(
                    entry.DigestKeyVersion,
                    entry.KeyDigest,
                    entryDigest))
            .OrderBy(binding => binding.EntryDigest, StringComparer.Ordinal)
            .ToArray();
        await StateManager.SetStateAsync(stateName, entry).ConfigureAwait(false);
        await StateManager.SetStateAsync(
            ManifestStateName,
            manifest with { Entries = entries }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CloseAsync(IdempotencyLegacyInventoryClosure closure)
    {
        ArgumentNullException.ThrowIfNull(closure);
        if (closure.SchemaVersion != IdempotencyLegacyInventoryClosure.CurrentSchemaVersion
            || !string.Equals(closure.TenantPartition, Host.Id.GetId(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(closure.InventoryId)
            || closure.InventoryVersion <= 0
            || closure.DigestKeyVersions is null
            || closure.DigestKeyVersions.Length == 0
            || closure.DigestKeyVersions.Any(string.IsNullOrWhiteSpace)
            || closure.DigestKeyVersions.Distinct(StringComparer.Ordinal).Count()
                != closure.DigestKeyVersions.Length
            || closure.EntryCount < 0
            || string.IsNullOrWhiteSpace(closure.ManifestDigest))
        {
            throw new InvalidOperationException("Legacy inventory closure is invalid.");
        }

        ConditionalValue<IdempotencyLegacyInventoryManifest> stored = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryManifest>(ManifestStateName)
            .ConfigureAwait(false);
        IdempotencyLegacyInventoryManifest manifest = stored.HasValue
            ? stored.Value
            : new IdempotencyLegacyInventoryManifest(
                IdempotencyLegacyInventoryManifest.CurrentSchemaVersion,
                closure.TenantPartition,
                closure.InventoryId,
                closure.InventoryVersion,
                Closed: false,
                [],
                []);
        ValidateManifest(manifest);
        string[] digestKeyVersions = closure.DigestKeyVersions
            .Order(StringComparer.Ordinal)
            .ToArray();
        string computedDigest = IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
            closure.SchemaVersion,
            closure.TenantPartition,
            closure.InventoryId,
            closure.InventoryVersion,
            manifest.Entries.Select(binding => binding.EntryDigest),
            digestKeyVersions);
        if (!string.Equals(manifest.InventoryId, closure.InventoryId, StringComparison.Ordinal)
            || manifest.InventoryVersion != closure.InventoryVersion
            || manifest.Entries.Length != closure.EntryCount
            || manifest.Entries.Any(binding => !digestKeyVersions.Contains(
                binding.DigestKeyVersion,
                StringComparer.Ordinal))
            || !string.Equals(computedDigest, closure.ManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy inventory closure does not match the exact manifest.");
        }

        if (manifest.Closed)
        {
            if (!manifest.DigestKeyVersions.SequenceEqual(digestKeyVersions, StringComparer.Ordinal)
                || !string.Equals(manifest.ManifestDigest, closure.ManifestDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A different legacy inventory closure is already durable.");
            }

            return;
        }

        await StateManager.SetStateAsync(
            ManifestStateName,
            manifest with
            {
                Closed = true,
                DigestKeyVersions = digestKeyVersions,
                ManifestDigest = closure.ManifestDigest,
            }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyLegacyInventoryInspection> InspectAsync(
        IdempotencyAdmissionDirectoryAlias[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        ValidateAliases(aliases);
        ConditionalValue<IdempotencyLegacyInventoryManifest> storedManifest = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryManifest>(ManifestStateName)
            .ConfigureAwait(false);
        if (!storedManifest.HasValue)
        {
            return new IdempotencyLegacyInventoryInspection(
                RequireInventory
                    ? IdempotencyLegacyInventoryDecision.Uninventoried
                    : IdempotencyLegacyInventoryDecision.NoLegacy);
        }

        IdempotencyLegacyInventoryManifest manifest = storedManifest.Value;
        ValidateManifest(manifest);
        if (!manifest.Closed)
        {
            return new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.Uninventoried);
        }

        if (aliases.Any(alias => !manifest.DigestKeyVersions.Contains(
            alias.DigestKeyVersion,
            StringComparer.Ordinal)))
        {
            return new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.Uninventoried);
        }

        var matches = new List<IdempotencyLegacyInventoryEntry>();
        foreach (IdempotencyAdmissionDirectoryAlias alias in aliases)
        {
            IdempotencyLegacyInventoryManifestEntry? binding = manifest.Entries.SingleOrDefault(candidate =>
                string.Equals(candidate.DigestKeyVersion, alias.DigestKeyVersion, StringComparison.Ordinal)
                && string.Equals(candidate.KeyDigest, alias.KeyDigest, StringComparison.Ordinal));
            if (binding is null)
            {
                continue;
            }

            ConditionalValue<IdempotencyLegacyInventoryEntry> stored = await StateManager
                .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(
                    BuildStateName(alias.DigestKeyVersion, alias.KeyDigest))
                .ConfigureAwait(false);
            if (!stored.HasValue)
            {
                throw new InvalidOperationException("A closed legacy inventory entry is unavailable.");
            }

            Validate(stored.Value);
            if (!string.Equals(stored.Value.InventoryId, manifest.InventoryId, StringComparison.Ordinal)
                || stored.Value.InventoryVersion != manifest.InventoryVersion
                || !string.Equals(
                    binding.EntryDigest,
                    IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(stored.Value),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Legacy inventory entry is not bound to the closed manifest.");
            }

            matches.Add(stored.Value);
        }

        if (matches.Count == 0)
        {
            return new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy);
        }

        IdempotencyLegacyInventoryEntry[] distinct = matches
            .Distinct()
            .ToArray();
        if (distinct.Length != 1)
        {
            return new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.Unsafe);
        }

        IdempotencyLegacyInventoryEntry entry = distinct[0];
        return new IdempotencyLegacyInventoryInspection(
            entry.Phase switch
            {
                IdempotencyLegacyMigrationPhase.Inventoried
                    or IdempotencyLegacyMigrationPhase.TargetPrepared
                    or IdempotencyLegacyMigrationPhase.TargetAcknowledged
                    or IdempotencyLegacyMigrationPhase.SourceRedirected
                    or IdempotencyLegacyMigrationPhase.AuthorityFlipped
                    => IdempotencyLegacyInventoryDecision.Migrate,
                IdempotencyLegacyMigrationPhase.Migrated => IdempotencyLegacyInventoryDecision.Migrated,
                _ => IdempotencyLegacyInventoryDecision.Unsafe,
            },
            entry);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyLegacyInventoryEntry> AdvanceAsync(
        IdempotencyLegacyMigrationAdvanceRequest request)
    {
        Validate(request);
        IdempotencyLegacyInventoryManifest manifest = await EnsureClosedManifestAsync(
            request.InventoryId).ConfigureAwait(false);
        string stateName = BuildStateName(request.DigestKeyVersion, request.KeyDigest);
        ConditionalValue<IdempotencyLegacyInventoryEntry> stored = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(stateName)
            .ConfigureAwait(false);
        IdempotencyLegacyInventoryEntry entry = stored.HasValue
            ? stored.Value
            : throw new InvalidOperationException("Legacy migration inventory entry is missing.");
        Validate(entry);
        ReproveManifestBinding(manifest, entry, request.DigestKeyVersion, request.KeyDigest);
        if (entry.Phase != request.ExpectedPhase
            || !string.Equals(entry.InventoryId, request.InventoryId, StringComparison.Ordinal)
            || !string.Equals(entry.MigrationId, request.MigrationId, StringComparison.Ordinal)
            || (entry.TargetAdmissionActorId is not null
                && !string.Equals(entry.TargetAdmissionActorId, request.TargetAdmissionActorId, StringComparison.Ordinal))
            || (entry.TargetImportDigest is not null
                && !string.Equals(entry.TargetImportDigest, request.TargetImportDigest, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Legacy migration phase is stale or targets different authority.");
        }

        IdempotencyLegacyMigrationPhase next = request.ExpectedPhase switch
        {
            IdempotencyLegacyMigrationPhase.Inventoried => IdempotencyLegacyMigrationPhase.TargetPrepared,
            IdempotencyLegacyMigrationPhase.TargetPrepared => IdempotencyLegacyMigrationPhase.TargetAcknowledged,
            IdempotencyLegacyMigrationPhase.TargetAcknowledged => IdempotencyLegacyMigrationPhase.SourceRedirected,
            IdempotencyLegacyMigrationPhase.SourceRedirected => IdempotencyLegacyMigrationPhase.AuthorityFlipped,
            IdempotencyLegacyMigrationPhase.AuthorityFlipped => IdempotencyLegacyMigrationPhase.Migrated,
            _ => throw new InvalidOperationException("Legacy migration cannot advance from its current phase."),
        };
        bool redirectTransition = request.ExpectedPhase == IdempotencyLegacyMigrationPhase.TargetAcknowledged;
        if ((!redirectTransition && request.ExpectedPhase < IdempotencyLegacyMigrationPhase.TargetAcknowledged
                && request.SourceRedirectDigest is not null)
            || (redirectTransition && string.IsNullOrWhiteSpace(request.SourceRedirectDigest))
            || (request.ExpectedPhase > IdempotencyLegacyMigrationPhase.TargetAcknowledged
                && !string.Equals(
                    request.SourceRedirectDigest,
                    entry.SourceRedirectDigest,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Legacy source redirect acknowledgement is early, missing, or changed.");
        }

        IdempotencyLegacyInventoryEntry updated = entry with
        {
            Phase = next,
            TargetAdmissionActorId = request.TargetAdmissionActorId,
            TargetImportDigest = request.TargetImportDigest,
            SourceRedirectDigest = request.SourceRedirectDigest ?? entry.SourceRedirectDigest,
            LastRolledBackTargetActorId = null,
            LastRolledBackTargetImportDigest = null,
        };
        Validate(updated);
        await StateManager.SetStateAsync(stateName, updated).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async Task<IdempotencyLegacyInventoryEntry> RollbackAsync(
        IdempotencyLegacyMigrationRollbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedPhase is not (IdempotencyLegacyMigrationPhase.TargetPrepared
            or IdempotencyLegacyMigrationPhase.TargetAcknowledged))
        {
            throw new InvalidOperationException("Legacy migration rollback is forbidden after the source redirect boundary.");
        }

        if (string.IsNullOrWhiteSpace(request.InventoryId)
            || string.IsNullOrWhiteSpace(request.MigrationId)
            || string.IsNullOrWhiteSpace(request.DigestKeyVersion)
            || string.IsNullOrWhiteSpace(request.KeyDigest)
            || string.IsNullOrWhiteSpace(request.TargetAdmissionActorId)
            || string.IsNullOrWhiteSpace(request.TargetImportDigest))
        {
            throw new InvalidOperationException("Legacy migration rollback request is invalid.");
        }

        IdempotencyLegacyInventoryManifest manifest = await EnsureClosedManifestAsync(
            request.InventoryId).ConfigureAwait(false);
        string stateName = BuildStateName(request.DigestKeyVersion, request.KeyDigest);
        ConditionalValue<IdempotencyLegacyInventoryEntry> stored = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(stateName)
            .ConfigureAwait(false);
        IdempotencyLegacyInventoryEntry entry = stored.HasValue
            ? stored.Value
            : throw new InvalidOperationException("Legacy migration inventory entry is missing.");
        Validate(entry);
        ReproveManifestBinding(manifest, entry, request.DigestKeyVersion, request.KeyDigest);
        if (!string.Equals(entry.InventoryId, request.InventoryId, StringComparison.Ordinal)
            || !string.Equals(entry.MigrationId, request.MigrationId, StringComparison.Ordinal)
            || (entry.Phase == IdempotencyLegacyMigrationPhase.Inventoried
                && (!string.Equals(
                        entry.LastRolledBackTargetActorId,
                        request.TargetAdmissionActorId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        entry.LastRolledBackTargetImportDigest,
                        request.TargetImportDigest,
                        StringComparison.Ordinal)))
            || (entry.Phase != IdempotencyLegacyMigrationPhase.Inventoried
                && entry.Phase != request.ExpectedPhase)
            || (!string.Equals(entry.TargetAdmissionActorId, request.TargetAdmissionActorId, StringComparison.Ordinal)
                && entry.Phase != IdempotencyLegacyMigrationPhase.Inventoried)
            || (!string.Equals(entry.TargetImportDigest, request.TargetImportDigest, StringComparison.Ordinal)
                && entry.Phase != IdempotencyLegacyMigrationPhase.Inventoried)
            || entry.SourceRedirectDigest is not null)
        {
            throw new InvalidOperationException("Legacy migration rollback checkpoint is stale or unsafe.");
        }

        if (entry.Phase == IdempotencyLegacyMigrationPhase.Inventoried)
        {
            return entry;
        }

        IdempotencyLegacyInventoryEntry rolledBack = entry with
        {
            Phase = IdempotencyLegacyMigrationPhase.Inventoried,
            TargetAdmissionActorId = null,
            TargetImportDigest = null,
            LastRolledBackTargetActorId = request.TargetAdmissionActorId,
            LastRolledBackTargetImportDigest = request.TargetImportDigest,
        };
        Validate(rolledBack);
        await StateManager.SetStateAsync(stateName, rolledBack).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return rolledBack;
    }

    /// <inheritdoc/>
    public async Task PurgeAsync(IdempotencyAdmissionDirectoryAlias alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        ValidateAliases([alias]);
        string stateName = BuildStateName(alias.DigestKeyVersion, alias.KeyDigest);
        _ = await StateManager.TryRemoveStateAsync(stateName).ConfigureAwait(false);
        ConditionalValue<IdempotencyLegacyInventoryManifest> storedManifest = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryManifest>(ManifestStateName)
            .ConfigureAwait(false);
        if (storedManifest.HasValue)
        {
            ValidateManifest(storedManifest.Value);
            IdempotencyLegacyInventoryManifestEntry[] remaining = storedManifest.Value.Entries
                .Where(candidate => !(
                    string.Equals(
                        candidate.DigestKeyVersion,
                        alias.DigestKeyVersion,
                        StringComparison.Ordinal)
                    && string.Equals(candidate.KeyDigest, alias.KeyDigest, StringComparison.Ordinal)))
                .ToArray();
            if (remaining.Length != storedManifest.Value.Entries.Length && remaining.Length == 0)
            {
                _ = await StateManager.TryRemoveStateAsync(ManifestStateName).ConfigureAwait(false);
            }
            else if (remaining.Length != storedManifest.Value.Entries.Length)
            {
                await StateManager.SetStateAsync(
                    ManifestStateName,
                    storedManifest.Value with
                    {
                        Entries = remaining,
                        ManifestDigest = IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                            storedManifest.Value.SchemaVersion,
                            storedManifest.Value.TenantPartition,
                            storedManifest.Value.InventoryId,
                            storedManifest.Value.InventoryVersion,
                            remaining.Select(binding => binding.EntryDigest),
                            storedManifest.Value.DigestKeyVersions),
                    }).ConfigureAwait(false);
            }
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    private static string BuildStateName(string digestKeyVersion, string keyDigest)
        => string.Concat("legacy:", digestKeyVersion, ":", keyDigest);

    private async Task<IdempotencyLegacyInventoryManifest> EnsureClosedManifestAsync(string inventoryId)
    {
        ConditionalValue<IdempotencyLegacyInventoryManifest> stored = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryManifest>(ManifestStateName)
            .ConfigureAwait(false);
        if (!stored.HasValue)
        {
            throw new InvalidOperationException("Closed legacy inventory manifest is missing.");
        }

        ValidateManifest(stored.Value);
        if (!stored.Value.Closed
            || !string.Equals(stored.Value.InventoryId, inventoryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy migration is not bound to the closed inventory.");
        }

        return stored.Value;
    }

    private static void ReproveManifestBinding(
        IdempotencyLegacyInventoryManifest manifest,
        IdempotencyLegacyInventoryEntry entry,
        string digestKeyVersion,
        string keyDigest)
    {
        IdempotencyLegacyInventoryManifestEntry? binding = manifest.Entries.SingleOrDefault(candidate =>
            string.Equals(candidate.DigestKeyVersion, digestKeyVersion, StringComparison.Ordinal)
            && string.Equals(candidate.KeyDigest, keyDigest, StringComparison.Ordinal));
        if (binding is null
            || !string.Equals(entry.InventoryId, manifest.InventoryId, StringComparison.Ordinal)
            || entry.InventoryVersion != manifest.InventoryVersion
            || !string.Equals(entry.DigestKeyVersion, digestKeyVersion, StringComparison.Ordinal)
            || !string.Equals(entry.KeyDigest, keyDigest, StringComparison.Ordinal)
            || !string.Equals(
                binding.EntryDigest,
                IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(entry),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy inventory entry is not bound to the closed manifest.");
        }
    }

    private void ValidateManifest(IdempotencyLegacyInventoryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != IdempotencyLegacyInventoryManifest.CurrentSchemaVersion
            || !string.Equals(manifest.TenantPartition, Host.Id.GetId(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(manifest.InventoryId)
            || manifest.InventoryVersion <= 0
            || manifest.Entries is null
            || manifest.DigestKeyVersions is null
            || (manifest.Closed && manifest.DigestKeyVersions.Length == 0)
            || (!manifest.Closed && manifest.DigestKeyVersions.Length != 0)
            || manifest.DigestKeyVersions.Any(string.IsNullOrWhiteSpace)
            || !manifest.DigestKeyVersions.SequenceEqual(
                manifest.DigestKeyVersions.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            || manifest.DigestKeyVersions.Distinct(StringComparer.Ordinal).Count()
                != manifest.DigestKeyVersions.Length
            || manifest.Entries.Any(binding => binding is null
                || string.IsNullOrWhiteSpace(binding.DigestKeyVersion)
                || string.IsNullOrWhiteSpace(binding.KeyDigest)
                || string.IsNullOrWhiteSpace(binding.EntryDigest))
            || manifest.Entries.Select(binding => binding.EntryDigest)
                .Distinct(StringComparer.Ordinal).Count() != manifest.Entries.Length
            || manifest.Entries.Select(binding => string.Concat(
                    binding.DigestKeyVersion,
                    "\0",
                    binding.KeyDigest))
                .Distinct(StringComparer.Ordinal).Count() != manifest.Entries.Length
            || (manifest.Closed && manifest.Entries.Any(binding => !manifest.DigestKeyVersions.Contains(
                binding.DigestKeyVersion,
                StringComparer.Ordinal)))
            || (manifest.Closed != !string.IsNullOrWhiteSpace(manifest.ManifestDigest))
            || (manifest.Closed
                && !string.Equals(
                    manifest.ManifestDigest,
                    IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                        manifest.SchemaVersion,
                        manifest.TenantPartition,
                        manifest.InventoryId,
                        manifest.InventoryVersion,
                        manifest.Entries.Select(binding => binding.EntryDigest),
                        manifest.DigestKeyVersions),
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Legacy inventory manifest is corrupt.");
        }
    }

    private static void ValidateAliases(IReadOnlyList<IdempotencyAdmissionDirectoryAlias> aliases)
    {
        if (aliases.Count == 0
            || aliases.Any(alias => alias is null
                || string.IsNullOrWhiteSpace(alias.DigestKeyVersion)
                || string.IsNullOrWhiteSpace(alias.ActorId)
                || string.IsNullOrWhiteSpace(alias.KeyDigest)))
        {
            throw new InvalidOperationException("Protected legacy inventory aliases are invalid.");
        }
    }

    private static void Validate(IdempotencyLegacyMigrationAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.InventoryId)
            || string.IsNullOrWhiteSpace(request.MigrationId)
            || string.IsNullOrWhiteSpace(request.DigestKeyVersion)
            || string.IsNullOrWhiteSpace(request.KeyDigest)
            || string.IsNullOrWhiteSpace(request.TargetAdmissionActorId)
            || string.IsNullOrWhiteSpace(request.TargetImportDigest)
            || !Enum.IsDefined(request.ExpectedPhase))
        {
            throw new InvalidOperationException("Legacy migration checkpoint request is invalid.");
        }
    }

    private void Validate(IdempotencyLegacyInventoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.SchemaVersion != IdempotencyLegacyInventoryEntry.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(entry.TenantPartition)
            || string.IsNullOrWhiteSpace(entry.SourceAggregateActorId)
            || string.IsNullOrWhiteSpace(entry.SourceEvidenceDigest)
            || entry.LegacySchemaVersion != 1
            || string.IsNullOrWhiteSpace(entry.DigestKeyVersion)
            || string.IsNullOrWhiteSpace(entry.KeyDigest)
            || string.IsNullOrWhiteSpace(entry.VerificationTag)
            || string.IsNullOrWhiteSpace(entry.IntentDigest)
            || entry.ReplayResult is null
            || string.IsNullOrWhiteSpace(entry.ExecutionMessageId)
            || string.IsNullOrWhiteSpace(entry.ExecutionCorrelationId)
            || string.IsNullOrWhiteSpace(entry.InventoryId)
            || entry.InventoryVersion <= 0
            || string.IsNullOrWhiteSpace(entry.MigrationId)
            || !Enum.IsDefined(entry.RetentionTier)
            || !Enum.IsDefined(entry.Phase)
            || !string.Equals(entry.TenantPartition, Host.Id.GetId(), StringComparison.Ordinal)
            || entry.FirstConsumedAt > entry.LastObservedAt
            || entry.FirstConsumedAt > entry.ReplayExpiresAt
            || !HasValidPhaseShape(entry)
            || entry.SourceAggregateActorId.Split(':').Length != 3
            || !entry.SourceAggregateActorId.StartsWith(
                string.Concat(entry.TenantPartition, ":"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Legacy idempotency inventory entry is corrupt.");
        }
    }

    private static bool HasValidPhaseShape(IdempotencyLegacyInventoryEntry entry)
        => entry.Phase switch
        {
            IdempotencyLegacyMigrationPhase.Inventoried or IdempotencyLegacyMigrationPhase.Unsafe
                => entry.TargetAdmissionActorId is null
                    && entry.TargetImportDigest is null
                    && entry.SourceRedirectDigest is null
                    && (entry.Phase == IdempotencyLegacyMigrationPhase.Inventoried
                        ? (entry.LastRolledBackTargetActorId is null)
                            == (entry.LastRolledBackTargetImportDigest is null)
                        : entry.LastRolledBackTargetActorId is null
                            && entry.LastRolledBackTargetImportDigest is null),
            IdempotencyLegacyMigrationPhase.TargetPrepared or IdempotencyLegacyMigrationPhase.TargetAcknowledged
                => !string.IsNullOrWhiteSpace(entry.TargetAdmissionActorId)
                    && !string.IsNullOrWhiteSpace(entry.TargetImportDigest)
                    && entry.SourceRedirectDigest is null
                    && entry.LastRolledBackTargetActorId is null
                    && entry.LastRolledBackTargetImportDigest is null,
            IdempotencyLegacyMigrationPhase.SourceRedirected
                or IdempotencyLegacyMigrationPhase.AuthorityFlipped
                or IdempotencyLegacyMigrationPhase.Migrated
                => !string.IsNullOrWhiteSpace(entry.TargetAdmissionActorId)
                    && !string.IsNullOrWhiteSpace(entry.TargetImportDigest)
                    && !string.IsNullOrWhiteSpace(entry.SourceRedirectDigest)
                    && entry.LastRolledBackTargetActorId is null
                    && entry.LastRolledBackTargetImportDigest is null,
            _ => false,
        };
}
