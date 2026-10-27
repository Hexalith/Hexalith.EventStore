using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Runtime;

namespace PublishedConsumer;

/// <summary>Adapts Dapr-owned addressed fixture reads to the published reader; every mutation refuses.</summary>
public class DaprStateReaderProxy : DispatchProxy
{
    /// <summary>Gets or sets the exact actor whose fixture state may be read.</summary>
    public string ActorId { get; set; } = string.Empty;
    /// <summary>Gets the number of refused mutation attempts.</summary>
    public int StagingCalls { get; private set; }

    /// <summary>Creates a read-only adapter for the addressed Dapr actor fixture.</summary>
    public static IActorStateManager Create(string actor)
    {
        IActorStateManager manager = Create<IActorStateManager, DaprStateReaderProxy>();
        ((DaprStateReaderProxy)manager).ActorId = actor;
        return manager;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? method, object?[]? arguments)
    {
        string key = (string)arguments![0]!;
        if (!key.StartsWith(ActorId + ":", StringComparison.Ordinal)) throw new InvalidOperationException("Foreign fixture state read refused.");
        if (method!.Name == "ContainsStateAsync") return ContainsAsync(key);
        if (method.Name == "TryGetStateAsync") return typeof(DaprStateReaderProxy).GetMethod(nameof(ReadAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, [key]);
        StagingCalls++;
        throw new InvalidOperationException("Fixture reader has no mutation authority: " + method.Name);
    }

    private async Task<bool> ContainsAsync(string key) => await RawAsync(key) is not null;

    private async Task<ConditionalValue<T>> ReadAsync<T>(string key)
    {
        string? raw = await RawAsync(key);
        return raw is null ? new ConditionalValue<T>(false, default!)
            : new ConditionalValue<T>(true, JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
    }

    private async Task<string?> RawAsync(string key)
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT")), Timeout = TimeSpan.FromSeconds(20) };
        using var response = await http.GetAsync("/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(ActorId) + "/state/" + Uri.EscapeDataString(key));
        if (response.StatusCode is System.Net.HttpStatusCode.NoContent or System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
