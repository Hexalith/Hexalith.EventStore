using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionRebuildProductionHttpMessageHandler(
    IServiceProvider services,
    ProjectionDispatchOptions dispatchOptions,
    DomainProjectionIdentityOptions identityOptions) : HttpMessageHandler {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        string requestJson = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.Contains("/project/rebuild/", StringComparison.Ordinal)) {
            ProjectionDispatchRequest dispatchRequest = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                requestJson,
                SerializerOptions) ?? throw new InvalidOperationException("Rebuild request body was missing.");
            ProjectionDispatchResponse response = path switch {
                var value when value.EndsWith("/project/rebuild/stage/v1", StringComparison.Ordinal) =>
                    await DomainProjectionDispatcher.StageRebuildAsync(services, dispatchRequest, dispatchOptions, identityOptions, cancellationToken).ConfigureAwait(false),
                var value when value.EndsWith("/project/rebuild/commit/v1", StringComparison.Ordinal) =>
                    await DomainProjectionDispatcher.CommitRebuildAsync(services, dispatchRequest, dispatchOptions, identityOptions, cancellationToken).ConfigureAwait(false),
                var value when value.EndsWith("/project/rebuild/abort/v1", StringComparison.Ordinal) =>
                    await DomainProjectionDispatcher.AbortRebuildAsync(services, dispatchRequest, dispatchOptions, identityOptions, cancellationToken).ConfigureAwait(false),
                var value when value.EndsWith("/project/rebuild/verify/v1", StringComparison.Ordinal) =>
                    await DomainProjectionDispatcher.VerifyRebuildAsync(services, dispatchRequest, dispatchOptions, identityOptions, cancellationToken).ConfigureAwait(false),
                _ => await DomainProjectionDispatcher.RebuildAsync(services, dispatchRequest, dispatchOptions, identityOptions, cancellationToken).ConfigureAwait(false),
            };
            return Json(response);
        }

        if (path.EndsWith("/project", StringComparison.Ordinal)) {
            ProjectionRequest projectionRequest = JsonSerializer.Deserialize<ProjectionRequest>(
                requestJson,
                SerializerOptions) ?? throw new InvalidOperationException("Projection request body was missing.");
            ProjectionResponse? response = DomainProjectionDispatcher.Project(services, projectionRequest);
            return response is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json(response);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T value)
        => new(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(value, SerializerOptions),
                Encoding.UTF8,
                "application/json"),
        };
}
