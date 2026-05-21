using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using EventStoreTenantValidator = Hexalith.EventStore.Authorization.ITenantValidator;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// EventStore-owned admin storage write routes.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/storage")]
[Tags("Admin - Storage Writes")]
public sealed class AdminStorageCommandController(
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    EventStoreTenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    IOptions<CommandStatusOptions> commandStatusOptions,
    ILogger<AdminStorageCommandController> logger) : ControllerBase {
    private const int MaxJobsPerIndex = 100;
    private const int MaxEtagRetries = 3;
    private const string SnapshotJobIndexPrefix = "admin:storage-snapshot-jobs:";

    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;

    /// <summary>
    /// Creates a manual aggregate snapshot inside the target aggregate actor boundary.
    /// </summary>
    /// <param name="request">Manual snapshot request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A typed admin operation result.</returns>
    [HttpPost("snapshot")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminOperationResult>> CreateSnapshot(
        [FromBody] ManualSnapshotRequest? request,
        CancellationToken ct = default) {
        if (request is null) {
            return Ok(new AdminOperationResult(
                false,
                "manual-snapshot-request-invalid",
                "Manual snapshot request body is required.",
                "RejectedValidation"));
        }

        string requestOperationId = CreateRequestOperationId(request);
        if (!HasRequiredFields(request)) {
            return Ok(new AdminOperationResult(
                false,
                requestOperationId,
                "Tenant, domain, and aggregate identifier are required.",
                "RejectedValidation"));
        }

        TenantValidationResult tenantResult = await tenantValidator
            .ValidateAsync(User, request.TenantId, ct, request.AggregateId)
            .ConfigureAwait(false);
        if (!tenantResult.IsAuthorized) {
            return Ok(new AdminOperationResult(
                false,
                requestOperationId,
                tenantResult.Reason ?? "Tenant access denied.",
                "RejectedUnauthorized"));
        }

        if (!HasOperatorOrAdminAccess(User)) {
            return Ok(new AdminOperationResult(
                false,
                requestOperationId,
                "Operator permission is required.",
                "RejectedUnauthorized"));
        }

        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(User, request.TenantId, request.Domain, "command:replay", "command", ct, request.AggregateId)
            .ConfigureAwait(false);
        if (!rbacResult.IsAuthorized) {
            return Ok(new AdminOperationResult(
                false,
                requestOperationId,
                rbacResult.Reason ?? "Operator permission is required.",
                "RejectedUnauthorized"));
        }

        AggregateIdentity identity;
        try {
            identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        }
        catch (ArgumentException ex) {
            logger.LogWarning(
                ex,
                "Manual snapshot request rejected by tuple validation: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                request.TenantId,
                request.Domain,
                request.AggregateId);
            return Ok(new AdminOperationResult(
                false,
                requestOperationId,
                "Invalid tenant, domain, or aggregate identifier.",
                "RejectedValidation"));
        }

        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            "AggregateActor");

        ManualSnapshotResult actorResult = await actor
            .CreateManualSnapshotAsync(request.CorrelationId)
            .ConfigureAwait(false);

        string operationId = actorResult.SequenceNumber > 0
            ? CreateSequenceOperationId(identity, actorResult.SequenceNumber)
            : requestOperationId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        SnapshotJob? job = CreateJob(operationId, identity, actorResult, now);
        if (job is not null) {
            bool evidenceWritten = await WriteJobEvidenceAsync(job, ct).ConfigureAwait(false);
            if (!evidenceWritten) {
                return Ok(new AdminOperationResult(
                    false,
                    operationId,
                    "Manual snapshot completed but job evidence could not be recorded.",
                    "JobEvidenceWriteFailed"));
            }
        }

        return Ok(CreateOperationResult(operationId, actorResult));
    }

    private static AdminOperationResult CreateOperationResult(string operationId, ManualSnapshotResult result)
        => result.Outcome switch {
            ManualSnapshotOutcome.Created => new(true, operationId, "Manual snapshot created.", null),
            ManualSnapshotOutcome.AlreadyCurrent => new(true, operationId, "Snapshot is already current.", null),
            ManualSnapshotOutcome.NotFound => new(false, operationId, "Aggregate stream was not found.", "NotFound"),
            ManualSnapshotOutcome.UnreadableProtected => new(false, operationId, result.Message ?? "Snapshot data cannot be safely read.", result.ReasonCode ?? "UnreadableProtected"),
            _ => new(false, operationId, result.Message ?? "Manual snapshot creation failed.", result.ReasonCode ?? "InfrastructureFailure"),
        };

    private static SnapshotJob? CreateJob(
        string operationId,
        AggregateIdentity identity,
        ManualSnapshotResult result,
        DateTimeOffset now) {
        SnapshotJobStatus status = result.Outcome switch {
            ManualSnapshotOutcome.Created => SnapshotJobStatus.Done,
            ManualSnapshotOutcome.AlreadyCurrent => SnapshotJobStatus.AlreadyCurrent,
            _ => SnapshotJobStatus.Failed,
        };

        return new SnapshotJob(
            operationId,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            result.SequenceNumber,
            status,
            now,
            now,
            result.SnapshotKey ?? identity.SnapshotKey,
            status == SnapshotJobStatus.Failed ? result.ReasonCode : null,
            status == SnapshotJobStatus.Failed ? result.Message : null);
    }

    private async Task<bool> WriteJobEvidenceAsync(SnapshotJob job, CancellationToken ct) {
        bool tenantWritten = await UpsertJobIndexAsync(job.TenantId, job, ct).ConfigureAwait(false);
        bool globalWritten = await UpsertJobIndexAsync("all", job, ct).ConfigureAwait(false);
        return tenantWritten && globalWritten;
    }

    private async Task<bool> UpsertJobIndexAsync(string scope, SnapshotJob job, CancellationToken ct) {
        string key = $"{SnapshotJobIndexPrefix}{scope}";
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (List<SnapshotJob>? existing, string etag) = await daprClient
                .GetStateAndETagAsync<List<SnapshotJob>>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);

            List<SnapshotJob> updated = [.. (existing ?? [])
                .Where(j => !string.Equals(j.OperationId, job.OperationId, StringComparison.Ordinal))
                .Append(job)
                .OrderByDescending(j => j.CompletedAtUtc ?? j.StartedAtUtc)
                .ThenBy(j => j.OperationId, StringComparer.Ordinal)
                .Take(MaxJobsPerIndex)];

            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return true;
            }
        }

        logger.LogWarning(
            "Failed to update manual snapshot job index after retries: IndexKey={IndexKey}, OperationId={OperationId}",
            key,
            job.OperationId);
        return false;
    }

    private static string CreateRequestOperationId(ManualSnapshotRequest request)
        => $"manual-snapshot-request-{Hash($"{request.TenantId ?? string.Empty}|{request.Domain ?? string.Empty}|{request.AggregateId ?? string.Empty}|{request.CorrelationId ?? string.Empty}")}";

    private static string CreateSequenceOperationId(AggregateIdentity identity, long sequence)
        => $"manual-snapshot-{Hash($"{identity.TenantId}|{identity.Domain}|{identity.AggregateId}|{sequence}")}";

    private static bool HasRequiredFields(ManualSnapshotRequest request)
        => !string.IsNullOrWhiteSpace(request.TenantId)
        && !string.IsNullOrWhiteSpace(request.Domain)
        && !string.IsNullOrWhiteSpace(request.AggregateId);

    private static bool HasOperatorOrAdminAccess(System.Security.Claims.ClaimsPrincipal user)
        => GlobalAdministratorHelper.IsGlobalAdministrator(user)
        || user.HasClaim("eventstore:admin-role", "Operator")
        || user.HasClaim("eventstore:admin-role", "Admin")
        || user.HasClaim("eventstore:permission", "command:replay");

    private static string Hash(string value) {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
