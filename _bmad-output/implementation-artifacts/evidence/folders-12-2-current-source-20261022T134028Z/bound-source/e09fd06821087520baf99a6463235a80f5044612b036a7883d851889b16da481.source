using System.Security.Claims;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

public sealed partial class AdminProjectionRebuildController
{
    /// <summary>Qualifies the existing same-C source/model binding without enabling guarded delivery.</summary>
    [HttpPost("{tenantId}/source-guard/{domain}/{aggregateId}/{projectionName}/qualify")]
    public Task<IActionResult> QualifySourceGuardAsync(string tenantId, string domain, string aggregateId, string projectionName,
        [FromBody] ProjectionSourceGuardQualificationRequestBody? body, CancellationToken cancellationToken)
        => MaintainSourceGuardAsync(tenantId, domain, aggregateId, projectionName, body, false, cancellationToken);

    /// <summary>Explicitly admits only an inventoried qualified scope using first-write/CAS maintenance authority.</summary>
    [HttpPost("{tenantId}/source-guard/{domain}/{aggregateId}/{projectionName}/activate")]
    public Task<IActionResult> ActivateSourceGuardAsync(string tenantId, string domain, string aggregateId, string projectionName,
        [FromBody] ProjectionSourceGuardQualificationRequestBody? body, CancellationToken cancellationToken)
        => MaintainSourceGuardAsync(tenantId, domain, aggregateId, projectionName, body, true, cancellationToken);

    private async Task<IActionResult> MaintainSourceGuardAsync(string tenantId, string domain, string aggregateId, string projectionName,
        ProjectionSourceGuardQualificationRequestBody? body, bool activate, CancellationToken cancellationToken)
    {
        IActionResult? denied = EnsureGlobalAdministrator();
        if (denied is not null) return denied;
        string? operatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(operatorId))
            return ProblemWithReason(403, "Forbidden", "An attributable operator identity is required.", StreamReplayReasonCodes.ForbiddenRole);
        AggregateIdentity identity;
        ProjectionSourceGuardMaintenance maintenance;
        try
        {
            identity = new(tenantId, domain, aggregateId);
            _ = CreateScope(tenantId, domain);
            ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
            if (body is null) throw new ArgumentException("Maintenance attestations are required.");
            maintenance = new(operatorId, body.CutoverCommit, body.BackupReference, body.SpecSha256,
                body.TrafficStopped, body.BackupRestoreVerified, body.DowngradeProhibited, body.WriterInventory);
            if (!maintenance.IsValid) throw new ArgumentException("Maintenance attestations are incomplete.");
        }
        catch (ArgumentException)
        {
            return ProblemWithReason(400, "Bad Request", "Exact scope and complete maintenance attestations are required.", StreamReplayReasonCodes.MissingRequiredField);
        }
        IServiceProvider services = serviceProvider ?? HttpContext.RequestServices;
        ProjectionSourceGuardQualification? qualification = services.GetService<ProjectionSourceGuardQualification>();
        if (qualification is null)
            return ProblemWithReason(503, "Service Unavailable", "Source guard maintenance is unavailable.", StreamReplayReasonCodes.ServiceUnavailable);
        ProjectionSourceGuardQualificationResult result = await qualification.QualifyAsync(identity, projectionName,
            body.ExpectedHead, body.ExpectedIncarnation, maintenance, activate, body.ExpectedPreviousIncarnation, cancellationToken).ConfigureAwait(false);
        if (result.Qualified || result.ReasonCode == "empty_no_work")
            return Ok(new { result.Qualified, result.Activated, result.ReasonCode, result.PreservedSequence });
        return ProblemWithReason(result.ReasonCode == "delivery_state_unavailable" ? 503 : 409,
            "Source guard maintenance incomplete", "The inventoried source and selected model could not be qualified.", result.ReasonCode);
    }
}
