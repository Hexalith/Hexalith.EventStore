#if FLOOR
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace PublishedConsumer;

/// <summary>Retains only the exact production stale-authority guard lost by nested actor-remoting exception serialization.</summary>
/// <param name="context">The current Dapr actor request accessor.</param>
public sealed class ExecutionAuthorityRefusalObserver(IHttpContextAccessor context) : IDisposable
{
    private readonly ConcurrentQueue<JsonElement> _observations = new();
    private long _sequence;

    /// <summary>Gets a checkpoint preceding one actual negative operation.</summary>
    public long Checkpoint => Interlocked.Read(ref _sequence);

    /// <summary>Starts observation of the one whitelisted production guard.</summary>
    public void Start() => AppDomain.CurrentDomain.FirstChanceException += Observe;

    /// <summary>Reads a subsequent guard bound to this aggregate or its exact admission authority.</summary>
    public JsonElement? Read(long checkpoint, string actorId, string? authorityId)
        => _observations.Where(value => value.GetProperty("sequence").GetInt64() > checkpoint
            && (value.GetProperty("actor").GetString() == actorId || value.GetProperty("actor").GetString() == authorityId))
            .Select(value => (JsonElement?)value).FirstOrDefault();

    /// <inheritdoc/>
    public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= Observe;

    private void Observe(object? sender, FirstChanceExceptionEventArgs args)
    {
        if (args.Exception is not InvalidOperationException || args.Exception.Message != "The idempotency execution authority is no longer current.") return;
        string? type = args.Exception.TargetSite?.DeclaringType?.FullName;
        if (type?.StartsWith("Hexalith.EventStore.Server.Actors.IdempotencyAdmissionActor", StringComparison.Ordinal) != true
            && type?.StartsWith("Hexalith.EventStore.Server.Commands.IdempotencyExecutionContextProtector", StringComparison.Ordinal) != true) return;
        string[]? path = context.HttpContext?.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path is null || path.Length != 5 || path[0] != "actors") return;
        _observations.Enqueue(JsonSerializer.SerializeToElement(new
        {
            sequence = Interlocked.Increment(ref _sequence), actor = Uri.UnescapeDataString(path[2]), actorType = path[1],
            declaringType = type, method = args.Exception.TargetSite?.Name, requestMethod = path[^1],
            exceptionType = args.Exception.GetType().FullName, message = args.Exception.Message,
        }));
        while (_observations.Count > 64) _observations.TryDequeue(out _);
    }
}
#endif
