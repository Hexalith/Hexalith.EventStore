#if SELECTED
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace PublishedConsumer;

/// <summary>Retains one whitelisted fixture refusal cause without changing production exception handling or diagnostics.</summary>
/// <param name="context">The accessor binding an observed exception to its actor request.</param>
public sealed class ProductionRefusalObserver(IHttpContextAccessor context) : IDisposable
{
    private const string MissingCallable = "CapabilityMismatch: an admitted edge lacks an allow-listed callable.";
    private readonly ConcurrentDictionary<string, JsonElement> _observations = new(StringComparer.Ordinal);

    /// <summary>Starts the bounded observer for the exact missing-callable production exception.</summary>
    public void Start() => AppDomain.CurrentDomain.FirstChanceException += Observe;

    /// <summary>Returns the whitelisted cause observed for the exact fixture actor.</summary>
    public JsonElement? Read(string actorId) => _observations.TryGetValue(actorId, out JsonElement value) ? value : null;

    /// <inheritdoc/>
    public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= Observe;

    private void Observe(object? sender, FirstChanceExceptionEventArgs args)
    {
        if (args.Exception is not InvalidOperationException || args.Exception.Message != MissingCallable) return;
        string? declaringType = args.Exception.TargetSite?.DeclaringType?.FullName;
        if (declaringType?.StartsWith("Hexalith.EventStore.Client.Events.EventUpcastChainExecutor", StringComparison.Ordinal) != true) return;
        string[]? path = context.HttpContext?.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path is null || path.Length < 5 || path[0] != "actors" || path[1] != "AggregateActor" || path[^1] != "ProcessCommandAsync") return;
        string actor = Uri.UnescapeDataString(path[2]);
        _observations.TryAdd(actor, JsonSerializer.SerializeToElement(new
        {
            code = "missing-allow-listed-callable", stage = "production-logical-event-upcast-binding", actor,
            declaringType, method = args.Exception.TargetSite?.Name, message = MissingCallable,
            assembly = args.Exception.TargetSite?.DeclaringType?.Assembly.FullName, observedAt = DateTimeOffset.UtcNow,
        }));
    }
}
#endif
