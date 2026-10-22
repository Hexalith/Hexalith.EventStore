using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

public partial class QueriesController
{
    private async Task<IActionResult> InspectProjectionCurrentAsync(BoundOwnerQueryRequest request,
        QueryCallerAuthority? authority, string correlationId, CancellationToken cancellationToken)
    {
        if (authority?.OriginatingContext is not { } origin || origin.OriginalActorId != request.PrincipalId
            || request.Query.Payload is not { } payload
            || HttpContext.RequestServices.GetService<IProjectionSourceGuardDelivery>() is not { } guard)
            return Forbid();
        try
        {
            InspectProjectionCurrentQuery? inspection = payload.Deserialize<InspectProjectionCurrentQuery>(JsonSerializerOptions.Web);
            ProjectionOwnerSelection? selection = inspection?.Selection;
            if (selection is null || selection.TenantId != request.Query.Tenant || selection.Domain != request.Query.Domain
                || selection.AggregateId != request.Query.AggregateId) return Forbid();
            string? workload = User.FindFirst(Hexalith.EventStore.ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)?.Value;
            string? allowedDomains = workload is null ? null : configuration?[$"Authentication:BoundOwnerQuery:AllowedProjectionDomains:{workload}"];
            if (allowedDomains?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Contains(selection.Domain, StringComparer.Ordinal) != true) return Forbid();
            var identity = new AggregateIdentity(selection.TenantId, selection.Domain, selection.AggregateId);
            ProjectionSourceGuardDeliveryResult result = await guard.InspectCurrentAsync(identity,
                selection.ProjectionName, cancellationToken).ConfigureAwait(false);
            ProjectionCurrentProof? current = result.Owned && result.AlreadyCurrent && result.ProvenSource is { } source
                && result.CatalogFingerprint is { } catalog
                && result.ProvenOwners.TryGetValue(selection.ProjectionName, out ProjectionOwnerProof? owner)
                && owner.Selection == selection
                ? new(SourceProofCodec.ToNative(source), owner, catalog) : null;
            Response.Headers.CacheControl = "no-store";
            return Ok(new SubmitQueryResponse(correlationId, JsonSerializer.SerializeToElement(current, JsonSerializerOptions.Web)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        { return Forbid(); }
    }
}
