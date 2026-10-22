using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Exercises production DomainService dispatch/proof and store operations over the same serialized carrier.</summary>
internal sealed class GuardedTailProductionHttpHandler(IServiceProvider services, DomainProjectionCatalogRegistry catalog) : HttpMessageHandler
{
    internal long? LoseResponseAfterSequence { get; set; }
    internal bool RejectDispatch { get; set; }
    internal bool RejectReplayCleanup { get; set; }
    internal string? LoseReplayResponseAfterAction { get; set; }
    internal List<int> ReplayPartSizes { get; } = [];
    internal HashSet<string> ReplayPreparationKeys { get; } = new(StringComparer.Ordinal);
    internal int MaximumReplayContextHeaderBytes { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValues(ProjectionReplayTransportContext.HeaderName, out IEnumerable<string>? contexts))
            MaximumReplayContextHeaderBytes = Math.Max(MaximumReplayContextHeaderBytes, Encoding.UTF8.GetByteCount(contexts.Single()));
        byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        string path = request.RequestUri!.AbsolutePath;
        object response;
        if (path.EndsWith("/project/rebuild/reclaim-guarded/v1", StringComparison.Ordinal))
        {
            if (RejectReplayCleanup) return new(HttpStatusCode.ServiceUnavailable);
            ProjectionDispatchRequest input = JsonSerializer.Deserialize<ProjectionDispatchRequest>(body, JsonSerializerOptions.Web)!;
            input = Bind(request, input);
            bool reclaimed = await DomainProjectionGuardedReplay.ReclaimAsync(services, input, new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "sample", ServiceVersion = "v1" }, cancellationToken).ConfigureAwait(false);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(reclaimed), Encoding.UTF8, "application/json") };
        }
        if (path.EndsWith("/project/rebuild/part/v1", StringComparison.Ordinal))
        {
            ProjectionRebuildTransportPartRequest part = JsonSerializer.Deserialize<ProjectionRebuildTransportPartRequest>(body, JsonSerializerOptions.Web)!;
            part = part with { Header = Bind(request, part.Header) };
            ReplayPartSizes.Add(part.Data.Length);
            await DomainProjectionRebuildTransport.StoreAsync(services, part, new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "sample", ServiceVersion = "v1" }, cancellationToken).ConfigureAwait(false);
            return new(HttpStatusCode.OK);
        }
        if (path.Contains("/project/rebuild/", StringComparison.Ordinal))
        {
            ProjectionDispatchRequest input = JsonSerializer.Deserialize<ProjectionDispatchRequest>(body, JsonSerializerOptions.Web)!;
            input = Bind(request, input);
            if (path.EndsWith("/stage/v1", StringComparison.Ordinal))
            {
                ReplayPreparationKeys.Add(DomainProjectionRebuildTransport.Key(input, -2));
                ReplayPreparationKeys.Add(DomainProjectionRebuildTransport.Key(input, -3));
            }
            var options = new ProjectionDispatchOptions();
            var identity = new DomainProjectionIdentityOptions { AppId = "sample", ServiceVersion = "v1" };
            response = path.EndsWith("/stage/v1", StringComparison.Ordinal)
                ? await DomainProjectionDispatcher.StageRebuildAsync(services, input, options, identity, cancellationToken).ConfigureAwait(false)
                : path.EndsWith("/commit/v1", StringComparison.Ordinal)
                    ? await DomainProjectionDispatcher.CommitRebuildAsync(services, input, options, identity, cancellationToken).ConfigureAwait(false)
                    : path.EndsWith("/verify/v1", StringComparison.Ordinal)
                        ? await DomainProjectionDispatcher.VerifyRebuildAsync(services, input, options, identity, cancellationToken).ConfigureAwait(false)
                        : await DomainProjectionDispatcher.AbortRebuildAsync(services, input, options, identity, cancellationToken).ConfigureAwait(false);
            if (LoseReplayResponseAfterAction is { } lost && path.EndsWith("/" + lost + "/v1", StringComparison.Ordinal))
            {
                LoseReplayResponseAfterAction = null;
                return new(HttpStatusCode.ServiceUnavailable);
            }
        }
        else if (path.EndsWith("/project/v2", StringComparison.Ordinal))
        {
            if (RejectDispatch) return new(HttpStatusCode.ServiceUnavailable);
            ProjectionDispatchRequest input = JsonSerializer.Deserialize<ProjectionDispatchRequest>(body, JsonSerializerOptions.Web)!;
            response = await DomainProjectionDispatcher.DispatchAsync(services, input, new ProjectionDispatchOptions(), catalog, cancellationToken).ConfigureAwait(false);
            if (input.Request.Events[0].SequenceNumber == LoseResponseAfterSequence)
            {
                LoseResponseAfterSequence = null;
                return new(HttpStatusCode.ServiceUnavailable);
            }
        }
        else if (path.EndsWith("/owner-proof/v1", StringComparison.Ordinal))
            response = await DomainProjectionOwnerProof.ReadAsync(services,
                JsonSerializer.Deserialize<ProjectionOwnerProofRequest>(body, JsonSerializerOptions.Web)!, catalog, cancellationToken).ConfigureAwait(false);
        else if (path.EndsWith("/reconcile-applied-receipt/v1", StringComparison.Ordinal))
            response = await DomainProjectionOwnerProof.ReconcileAppliedReceiptAsync(services,
                JsonSerializer.Deserialize<ProjectionAppliedReceiptRequest>(body, JsonSerializerOptions.Web)!, catalog, cancellationToken).ConfigureAwait(false);
        else if (path.EndsWith("/applied-receipt/v1", StringComparison.Ordinal))
            response = await DomainProjectionOwnerProof.ReadAppliedReceiptAsync(services,
                JsonSerializer.Deserialize<ProjectionAppliedReceiptRequest>(body, JsonSerializerOptions.Web)!, catalog, cancellationToken).ConfigureAwait(false);
        else return new(HttpStatusCode.NotFound);
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response, JsonSerializerOptions.Web), Encoding.UTF8, "application/json") };
    }

    private static ProjectionDispatchRequest Bind(HttpRequestMessage transport, ProjectionDispatchRequest request)
        => ProjectionReplayTransportContext.Bind(request, transport.Headers.TryGetValues(ProjectionReplayTransportContext.HeaderName,
            out IEnumerable<string>? values) ? values.Single() : null);
}
