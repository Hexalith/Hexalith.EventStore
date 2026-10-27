#if SELECTED
using System.Net.Http.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

namespace PublishedConsumer;

/// <summary>Uses the production caching actor adapter and the separate SDK domain query dispatcher.</summary>
/// <param name="host">The Dapr actor host.</param>
/// <param name="etags">The SDK ETag service.</param>
/// <param name="logger">The actor logger.</param>
public sealed class FixtureQueryActor(ActorHost host, IETagService etags, ILogger<FixtureQueryActor> logger)
    : CachingProjectionActor(host, etags, logger)
{
    /// <inheritdoc/>
    protected override async Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("P1R_DOMAIN_URL")!), Timeout = TimeSpan.FromSeconds(30) };
        using var response = await http.PostAsJsonAsync("/query", envelope);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QueryResult>())!;
    }
}
#endif
