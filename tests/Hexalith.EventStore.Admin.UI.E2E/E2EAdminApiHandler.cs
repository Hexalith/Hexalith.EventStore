using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>Owns the standalone browser fixture's empty Admin data and rejects undeclared transport use.</summary>
/// <param name="configuration">The fixture's disposable credentials.</param>
internal sealed class E2EAdminApiHandler(IConfiguration configuration) : HttpMessageHandler
{
    internal const string Endpoint = "http://127.0.0.1:1";
    internal const string AppId = "e2e-admin";
    private readonly ConcurrentQueue<string> _rejections = new();

    /// <summary>Refuses silently swallowed fixture transport errors after browser assertions finish.</summary>
    internal void VerifyNoRejectedRequests()
    {
        if (!_rejections.IsEmpty)
        {
            throw new InvalidOperationException("Standalone Admin fixture rejected requests: " + string.Join("; ", _rejections));
        }
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri is not { } uri || uri.GetLeftPart(UriPartial.Authority) != Endpoint)
        {
            return Refuse(HttpStatusCode.Forbidden, "foreign endpoint " + (request.RequestUri is { } rejected ? rejected.Scheme + "://" + rejected.IdnHost + ":" + rejected.Port : "missing"));
        }
        if (request.Method != HttpMethod.Get)
        {
            return Refuse(HttpStatusCode.MethodNotAllowed, "undeclared method");
        }
        if (!request.Headers.TryGetValues("dapr-app-id", out var targets) || !targets.SequenceEqual([AppId]))
        {
            return Refuse(HttpStatusCode.Forbidden, "foreign target");
        }
        if (!request.Headers.TryGetValues("dapr-api-token", out var tokens)
            || !tokens.SequenceEqual([configuration["DAPR_API_TOKEN"]]) || !ValidBearer(request))
        {
            return Refuse(HttpStatusCode.Unauthorized, "invalid fixture credential");
        }

        object? body = uri.AbsolutePath switch
        {
            "/api/v1/admin/health" => new SystemHealthReport(HealthStatus.Healthy, 0, 0, 0, [], new(null, null, null)),
            "/api/v1/admin/health/dapr/history" => new DaprComponentHealthTimeline([], false, HistoryStatus: SystemHealthMetricStatus.Available),
            "/api/v1/admin/streams" or "/api/v1/admin/streams/commands" or "/api/v1/admin/dead-letters" => new PagedResult<object>([], 0, null),
            "/api/v1/admin/dead-letters/count" => 0,
            "/api/v1/admin/storage/overview" => new StorageOverview(0, null, [], 0, StorageIndexStatus.Empty),
            "/api/v1/admin/dapr/sidecar" => Sidecar(),
            "/api/v1/admin/dapr/overview" => new DaprInfrastructureOverview(Sidecar(), []),
            "/api/v1/admin/dapr/actors" => new DaprActorRuntimeInfo([], 0, new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, false, false, 0), RemoteMetadataStatus.NotConfigured, null),
            "/api/v1/admin/dapr/pubsub" => new DaprPubSubOverview([], [], RemoteMetadataStatus.NotConfigured, null),
            "/api/v1/admin/dapr/resiliency" => DaprResiliencySpec.Unavailable,
            "/api/v1/admin/tenants" or "/api/v1/admin/projections" or "/api/v1/admin/types/events"
                or "/api/v1/admin/types/commands" or "/api/v1/admin/types/aggregates" or "/api/v1/admin/backups"
                or "/api/v1/admin/consistency/checks" or "/api/v1/admin/storage/snapshot-policies"
                or "/api/v1/admin/storage/compaction-jobs" or "/api/v1/admin/storage/hot-streams"
                or "/api/v1/admin/dapr/components" => Array.Empty<object>(),
            _ => null,
        };
        return body is null
            ? Refuse(HttpStatusCode.NotFound, "undeclared route " + uri.AbsolutePath)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
    }

    private bool ValidBearer(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: { Length: > 0 } token })
        {
            return false;
        }
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["EventStore:Authentication:SigningKey"]!)),
                ValidateIssuer = true, ValidIssuer = configuration["EventStore:Authentication:Issuer"],
                ValidateAudience = true, ValidAudience = configuration["EventStore:Authentication:Audience"],
                ValidateLifetime = true, ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            }, out _);
            return true;
        }
        catch (SecurityTokenException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private Task<HttpResponseMessage> Refuse(HttpStatusCode status, string reason)
    {
        _rejections.Enqueue(reason);
        return Task.FromResult(new HttpResponseMessage(status));
    }

    private static DaprSidecarInfo Sidecar() => new(AppId, "synthetic", 0, 0, 0, RemoteMetadataStatus.NotConfigured, null);
}
