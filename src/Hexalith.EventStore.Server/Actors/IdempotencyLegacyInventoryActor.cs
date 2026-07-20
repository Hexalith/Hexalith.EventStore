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

            return;
        }

        await StateManager.SetStateAsync(stateName, entry).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyLegacyInventoryInspection> InspectAsync(
        IdempotencyAdmissionDirectoryAlias[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        var matches = new List<IdempotencyLegacyInventoryEntry>();
        foreach (IdempotencyAdmissionDirectoryAlias alias in aliases)
        {
            ConditionalValue<IdempotencyLegacyInventoryEntry> stored = await StateManager
                .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(
                    BuildStateName(alias.DigestKeyVersion, alias.KeyDigest))
                .ConfigureAwait(false);
            if (stored.HasValue)
            {
                Validate(stored.Value);
                matches.Add(stored.Value);
            }
        }

        if (matches.Count == 0)
        {
            return new IdempotencyLegacyInventoryInspection(
                RequireInventory
                    ? IdempotencyLegacyInventoryDecision.Uninventoried
                    : IdempotencyLegacyInventoryDecision.NoLegacy);
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
                IdempotencyLegacyMigrationPhase.Inventoried or IdempotencyLegacyMigrationPhase.TargetPrepared
                    => IdempotencyLegacyInventoryDecision.Migrate,
                IdempotencyLegacyMigrationPhase.Migrated => IdempotencyLegacyInventoryDecision.Migrated,
                _ => IdempotencyLegacyInventoryDecision.Unsafe,
            },
            entry);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyLegacyInventoryEntry> AdvanceAsync(
        string digestKeyVersion,
        string keyDigest,
        IdempotencyLegacyMigrationPhase expectedPhase,
        string targetAdmissionActorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digestKeyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAdmissionActorId);
        string stateName = BuildStateName(digestKeyVersion, keyDigest);
        ConditionalValue<IdempotencyLegacyInventoryEntry> stored = await StateManager
            .TryGetStateAsync<IdempotencyLegacyInventoryEntry>(stateName)
            .ConfigureAwait(false);
        IdempotencyLegacyInventoryEntry entry = stored.HasValue
            ? stored.Value
            : throw new InvalidOperationException("Legacy migration inventory entry is missing.");
        if (entry.Phase != expectedPhase
            || (entry.TargetAdmissionActorId is not null
                && !string.Equals(entry.TargetAdmissionActorId, targetAdmissionActorId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Legacy migration phase is stale or targets different authority.");
        }

        IdempotencyLegacyMigrationPhase next = expectedPhase switch
        {
            IdempotencyLegacyMigrationPhase.Inventoried => IdempotencyLegacyMigrationPhase.TargetPrepared,
            IdempotencyLegacyMigrationPhase.TargetPrepared => IdempotencyLegacyMigrationPhase.Migrated,
            _ => throw new InvalidOperationException("Legacy migration cannot advance from its current phase."),
        };
        IdempotencyLegacyInventoryEntry updated = entry with
        {
            Phase = next,
            TargetAdmissionActorId = targetAdmissionActorId,
        };
        await StateManager.SetStateAsync(stateName, updated).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return updated;
    }

    private static string BuildStateName(string digestKeyVersion, string keyDigest)
        => string.Concat("legacy:", digestKeyVersion, ":", keyDigest);

    private static void Validate(IdempotencyLegacyInventoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.SchemaVersion != IdempotencyLegacyInventoryEntry.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(entry.TenantPartition)
            || string.IsNullOrWhiteSpace(entry.SourceAggregateActorId)
            || string.IsNullOrWhiteSpace(entry.SourceEvidenceDigest)
            || entry.LegacySchemaVersion <= 0
            || string.IsNullOrWhiteSpace(entry.DigestKeyVersion)
            || string.IsNullOrWhiteSpace(entry.KeyDigest)
            || string.IsNullOrWhiteSpace(entry.VerificationTag)
            || string.IsNullOrWhiteSpace(entry.IntentDigest)
            || entry.ReplayResult is null
            || string.IsNullOrWhiteSpace(entry.ExecutionMessageId)
            || string.IsNullOrWhiteSpace(entry.ExecutionCorrelationId)
            || !Enum.IsDefined(entry.RetentionTier)
            || !Enum.IsDefined(entry.Phase))
        {
            throw new InvalidOperationException("Legacy idempotency inventory entry is corrupt.");
        }
    }
}
