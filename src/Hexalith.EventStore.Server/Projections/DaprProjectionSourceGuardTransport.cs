using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Bounded authenticated Dapr transport over existing platform dispatch and receipt seams.</summary>
internal sealed class DaprProjectionSourceGuardTransport(DaprClient dapr, IHttpClientFactory clients) : IProjectionSourceGuardTransport
{
    /// <inheritdoc/>
    public async Task<ProjectionDispatchOutcome> DispatchEventAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        string projection, ProjectionEventDto item, string dispatchId, CancellationToken cancellationToken)
    {
        var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, [item]),
            [projection], dispatchId, catalog.CatalogFingerprint);
        ProjectionDispatchResponse? result = await InvokeAsync<ProjectionDispatchResponse>(catalog.AppId,
            "project/v2", request, cancellationToken).ConfigureAwait(false);
        return result is { Version: ProjectionDispatchProtocol.Version, Outcomes.Count: 1 }
            && result.Outcomes[0].ProjectionType == projection && result.Outcomes[0].State is null
            ? result.Outcomes[0] : new(projection, ProjectionDispatchStatus.Indeterminate, null, "handler_failure");
    }

    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionAppliedReceiptRequest request, CancellationToken cancellationToken)
    {
        ProjectionOwnerProofResult? result = await InvokeAsync<ProjectionOwnerProofResult>(catalog.AppId,
            "project/source-guard/applied-receipt/v1", request, cancellationToken).ConfigureAwait(false);
        return result is { MetadataKeyReads: 1, ReadBodyBytes: >= 0 and <= 16_384 }
            ? result : new(null, "delivery_reconciliation_required", result?.MetadataKeyReads ?? 0, result?.ReadBodyBytes ?? 0);
    }

    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionAppliedReceiptRequest request, CancellationToken cancellationToken)
        => await InvokeAsync<ProjectionOwnerProofResult>(catalog.AppId,
            "project/source-guard/reconcile-applied-receipt/v1", request, cancellationToken).ConfigureAwait(false)
                ?? new(null, "delivery_reconciliation_required", 0, 0);

    /// <inheritdoc/>
    public Task<ProjectionDispatchResponse?> InvokeReplayAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionDispatchRequest request, ProjectionMappedReplayAction action, CancellationToken cancellationToken)
        => InvokeAsync<ProjectionDispatchResponse>(catalog.AppId, action switch
        {
            ProjectionMappedReplayAction.Stage => "project/rebuild/stage/v1",
            ProjectionMappedReplayAction.Commit => "project/rebuild/commit/v1",
            ProjectionMappedReplayAction.Verify => "project/rebuild/verify/v1",
            _ => "project/rebuild/abort/v1",
        }, request, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> ReclaimReplayAsync(NamedProjectionRouteCatalogEntry catalog, ProjectionDispatchRequest completed,
        CancellationToken cancellationToken)
        => await InvokeAsync<bool>(catalog.AppId, "project/rebuild/reclaim-guarded/v1", completed,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<ProjectionDispatchRequest?> PrepareReplayRequestAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionDispatchRequest request, CancellationToken cancellationToken)
    {
        if (!request.ProjectionTypes.Any(route => catalog.MappedOnlyStreamedProjectionTypes.Contains(route, StringComparer.Ordinal)))
            return request;
        if (request.ProjectionTypes.Any(route => !catalog.MappedOnlyStreamedProjectionTypes.Contains(route, StringComparer.Ordinal)))
            return null;
        using var measure = new ProjectionRebuildHashCountStream();
        try { await ProjectionWireJsonSerializer.SerializeAsync(measure, request.Request.Events, JsonSerializerOptions.Web, cancellationToken).ConfigureAwait(false); }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReplayTransportSerializationBytes, measure.Length); }
        if (measure.Length > 67_108_864) return null;
        int count = checked((int)((measure.Length + ProjectionRebuildTransportReference.PartBytes - 1) / ProjectionRebuildTransportReference.PartBytes));
        string digest = measure.Digest;
        ProjectionDispatchRequest header = request with
        {
            Request = request.Request with { Events = [] },
            RebuildTransport = new(count, digest),
        };
        async Task UploadAsync(int ordinal, byte[] part, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReplayPartUploadAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReplayPartUploadBytes, part.Length);
            using HttpRequestMessage upload = dapr.CreateInvokeMethodRequest(catalog.AppId,
                "project/rebuild/part/v1", new ProjectionRebuildTransportPartRequest(header, ordinal, part));
            AddReplayContext(upload, header);
            upload.Content?.Dispose();
            upload.Content = ProjectionWireJsonSerializer.CreateContent(new ProjectionRebuildTransportPartRequest(header, ordinal, part), JsonSerializerOptions.Web);
            using HttpResponseMessage response = await clients.CreateClient().SendAsync(upload,
                HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new IOException("Guarded replay part upload was rejected.");
        }
        using var parts = new ProjectionRebuildPartUploadStream(UploadAsync);
        try
        {
            await ProjectionWireJsonSerializer.SerializeAsync(parts, request.Request.Events, JsonSerializerOptions.Web, cancellationToken).ConfigureAwait(false);
            await parts.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) { return null; }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReplayTransportSerializationBytes, parts.WrittenLength); }
        if (parts.PartCount != count || parts.WrittenLength != measure.Length || parts.Digest != digest) return null;
        return header;
    }

    private async Task<T?> InvokeAsync<T>(string app, string method, object body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = dapr.CreateInvokeMethodRequest(app, method, body);
        if (body is ProjectionDispatchRequest replay) AddReplayContext(request, replay);
        request.Content?.Dispose();
        // Guarded admission, bootstrap and replay-part digests qualify this fixed wire
        // profile. Dapr's configurable state serializer must not change these bytes.
        request.Content = ProjectionWireJsonSerializer.CreateContent(body, JsonSerializerOptions.Web);
        using HttpResponseMessage response = await clients.CreateClient().SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 16_384) return default;
        using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = new byte[16_385];
        int length = 0;
        while (length < bytes.Length)
        {
            int count = await input.ReadAsync(bytes.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            length += count;
        }
        if (length == 0 || length > 16_384) return default;
        return JsonSerializer.Deserialize<T>(bytes.AsSpan(0, length), JsonSerializerOptions.Web);
    }

    private static void AddReplayContext(HttpRequestMessage request, ProjectionDispatchRequest replay)
    {
        if (ProjectionReplayTransportContext.Header(replay) is { } context)
            request.Headers.Add(ProjectionReplayTransportContext.HeaderName, context);
    }
}
