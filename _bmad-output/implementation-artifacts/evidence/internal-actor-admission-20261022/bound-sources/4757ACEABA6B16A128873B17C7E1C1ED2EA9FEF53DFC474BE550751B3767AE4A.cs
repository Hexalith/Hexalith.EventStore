using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Uses the owning SDK read-only operation, followed by the owning lifecycle seam.</summary>
internal sealed class DaprProjectionSelectedOwnerProofGateway(DaprClient daprClient, IHttpClientFactory clients,
    IProjectionLifecycleGateway lifecycle) : IProjectionSelectedOwnerProofGateway
{
    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReadAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        string projectionName, ProjectionLifecycleSnapshot snapshot, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = daprClient.CreateInvokeMethodRequest(catalog.AppId,
            "project/source-guard/owner-proof/v1", new ProjectionOwnerProofRequest(identity.TenantId,
                identity.Domain, identity.AggregateId, projectionName, catalog.CatalogFingerprint));
        using HttpResponseMessage response = await clients.CreateClient().SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 16_384)
            return new(null, "delivery_state_unavailable", 0, 0);
        using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[16_385];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await input.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            length += read;
        }

        if (length == 0 || length > 16_384) return new(null, "delivery_proof_limit_exceeded", 0, 0);
        ProjectionOwnerProofResult result = JsonSerializer.Deserialize<ProjectionOwnerProofResult>(buffer.AsSpan(0, length),
            JsonSerializerOptions.Web) ?? new(null, "delivery_state_unavailable", 0, 0);
        if (result.MetadataKeyReads is < 0 or > 2 || result.ReadBodyBytes < 0 || result.ReadBodyBytes > 32_768)
            return new(null, "delivery_proof_limit_exceeded", result.MetadataKeyReads, result.ReadBodyBytes);
        ProjectionLifecycleSnapshot after = await lifecycle.ReadSnapshotAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
        return result with { MetadataKeyReads = result.MetadataKeyReads + 2,
            Proof = after == snapshot ? result.Proof : null,
            ReasonCode = after == snapshot ? result.ReasonCode : "head_changed" };
    }
}
