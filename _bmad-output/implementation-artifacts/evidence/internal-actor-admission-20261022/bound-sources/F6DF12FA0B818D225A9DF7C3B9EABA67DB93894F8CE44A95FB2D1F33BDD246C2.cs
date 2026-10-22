using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes legacy migration phases with active managed-tenant lifecycle authority.</summary>
internal interface IIdempotencyTenantLifecycleMigrationActor : IActor
{
    /// <summary>Completes or resumes the exact lifecycle-bound migration.</summary>
    Task<IdempotencyLegacyMigrationResult> MigrateLegacyAsync(IdempotencyLegacyMigrationRequest request);

    /// <summary>Rolls back only the exact prepared target before the source redirect boundary.</summary>
    Task RollbackLegacyAsync(IdempotencyLegacyLifecycleRollbackRequest request);
}
