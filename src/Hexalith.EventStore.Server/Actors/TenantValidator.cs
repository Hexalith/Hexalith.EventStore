namespace Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

/// <summary>
/// Validates that a command's tenant matches the actor's tenant identity.
/// SEC-2: Defense-in-depth tenant isolation at actor level.
/// </summary>
public partial class TenantValidator(ILogger<TenantValidator> logger) : ITenantValidator {
    /// <inheritdoc/>
    public void Validate(string commandTenantId, string actorId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        // Actor ID format guaranteed by AggregateIdentity validation
        // (regex: lowercase alphanumeric + hyphens, no colons).
        // If this assumption changes, update this parser. (F-PM1)
        string[] parts = actorId.Split(':');
        if (parts.Length != 3) {
            throw new InvalidOperationException(
                $"Malformed actor ID '{actorId}': expected 3 colon-separated segments, got {parts.Length}");
        }

        string actorTenant = parts[0];

        if (!string.Equals(commandTenantId, actorTenant, StringComparison.Ordinal)) {
            Log.TenantMismatch(logger, commandTenantId, actorTenant, actorId);
            throw new TenantMismatchException(commandTenantId, actorTenant);
        }

        Log.TenantValidationPassed(logger, commandTenantId, actorId);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 5000,
            Level = LogLevel.Warning,
            Message = "Tenant mismatch: CommandTenant={CommandTenant}, ActorTenant={ActorTenant}, ActorId={ActorId}, FailureLayer=ActorTenantValidation, Stage=TenantValidationFailed")]
        public static partial void TenantMismatch(
            ILogger logger,
            string commandTenant,
            string actorTenant,
            string actorId);

        [LoggerMessage(
            EventId = 5001,
            Level = LogLevel.Debug,
            Message = "Tenant validation passed: TenantId={TenantId}, ActorId={ActorId}, Stage=TenantValidationPassed")]
        public static partial void TenantValidationPassed(
            ILogger logger,
            string tenantId,
            string actorId);
    }
}
