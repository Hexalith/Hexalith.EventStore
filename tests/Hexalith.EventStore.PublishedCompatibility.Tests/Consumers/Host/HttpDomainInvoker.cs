using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

namespace PublishedConsumer;

/// <summary>Transports the real actor's replay input to a physically separate versioned SDK domain process.</summary>
public sealed class HttpDomainInvoker : IDomainServiceInvoker
{
    public ConcurrentDictionary<string, int> Observations { get; } = new(StringComparer.Ordinal);

    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("P1R_DOMAIN_URL")!), Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await client.PostAsJsonAsync("/process", new DomainServiceRequest(command, currentState), cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(json.RootElement.GetProperty("error").GetString() + ":" + json.RootElement.GetProperty("message").GetString());
        Observations[command.AggregateIdentity.ActorId] = json.RootElement.GetProperty("count").GetInt32();
        return json.RootElement.GetProperty("append").GetInt32() == 0 ? DomainResult.NoOp()
            : DomainResult.Success(json.RootElement.GetProperty("events").Deserialize<CounterIncremented[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
    }

#if SELECTED
    public Task<DomainResult> InvokeTrustedEffectAsync(CommandEnvelope command, object? state, bool includeState,
        string? profile, Hexalith.EventStore.Contracts.Effects.TrustedEffectOrigin origin, CancellationToken cancellationToken = default)
        => InvokeAsync(command, state, cancellationToken);
#endif
}
