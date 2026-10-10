using System.Collections.Frozen;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Validates immutable JSON upcast chains and resolves known stored events before deserialization.</summary>
public sealed class EventPayloadEvolutionRegistry
{
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private static readonly JsonNodeOptions s_nodeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonDocumentOptions s_documentOptions = new() { MaxDepth = 64 };
    private static readonly ConcurrentDictionary<Type, EventPayloadEvolutionRegistry> s_applyRegistries = new();
    private readonly FrozenDictionary<string, Type> _knownTypes;
    private readonly IEventPayloadUpcaster[] _steps;

    /// <summary>Creates and validates a registry from registered event types and pure upcasters.</summary>
    public EventPayloadEvolutionRegistry(IEnumerable<Type> knownEventTypes, IEnumerable<IEventPayloadUpcaster> upcasters)
    {
        ArgumentNullException.ThrowIfNull(knownEventTypes);
        ArgumentNullException.ThrowIfNull(upcasters);
        var known = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (Type type in knownEventTypes.Distinct())
        {
            if (!typeof(IEventPayload).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                continue;
            }
            _ = EventPayloadVersionResolver.GetDeclaredVersion(type);
            known.Add(type.FullName ?? type.Name, type);
        }
        _knownTypes = known.ToFrozenDictionary(StringComparer.Ordinal);
        _steps = upcasters.GroupBy(static step => (step.GetType(), step.EventTypeName,
            step.FromVersion, step.TargetEventTypeName)).Select(static group => group.First()).ToArray();
        ValidateRegistration();
    }

    /// <summary>Discovers public and non-public upcasters with parameterless constructors in an assembly.</summary>
    public static IReadOnlyList<IEventPayloadUpcaster> DiscoverUpcasters(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetTypes()
            .Where(static type => typeof(IEventPayloadUpcaster).IsAssignableFrom(type)
                && type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters
                && type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(static constructor => constructor.GetParameters().Length == 0))
            .Select(static type => Activator.CreateInstance(type, nonPublic: true) as IEventPayloadUpcaster
                ?? throw new InvalidOperationException($"Upcaster {type.FullName} has no parameterless constructor."))
            .ToArray();
    }

    /// <summary>Builds the convention registry for a state type used without dependency injection.</summary>
    public static EventPayloadEvolutionRegistry ForApplyState(Type stateType)
    {
        ArgumentNullException.ThrowIfNull(stateType);
        return s_applyRegistries.GetOrAdd(stateType, static type =>
        {
            Type[] knownTypes = [.. ApplyMethodResolver.GetOrBuildTable(type).ByType.Keys];
            IEventPayloadUpcaster[] candidates = [.. DiscoverUpcasters(type.Assembly)];
            var relevantNames = new HashSet<string>(knownTypes.Select(static known => known.FullName ?? known.Name), StringComparer.Ordinal);
            var selected = new HashSet<IEventPayloadUpcaster>();
            bool added;
            do
            {
                added = false;
                foreach (IEventPayloadUpcaster step in candidates)
                {
                    if (!selected.Contains(step) && relevantNames.Any(name => NamesMatch(name, step.TargetEventTypeName ?? step.EventTypeName)))
                    {
                        selected.Add(step);
                        relevantNames.Add(step.EventTypeName);
                        added = true;
                    }
                }
            }
            while (added);
            return new EventPayloadEvolutionRegistry(knownTypes, selected);
        });
    }

    /// <summary>Resolves and validates a known event, running each required step once in ascending order.</summary>
    public ResolvedEventPayload Read(string eventTypeName, int? storedPayloadVersion, byte[] payload, long sequenceNumber = 0,
        bool validateDeserialization = true)
        => ReadCore(eventTypeName, storedPayloadVersion, payload, sequenceNumber, validateDeserialization,
            deferCurrentPayloadValidation: false);

    /// <summary>Resolves replay metadata while leaving payload validation to the replay batch preflight.</summary>
    internal ResolvedEventPayload ReadForReplay(string eventTypeName, int? storedPayloadVersion, byte[] payload,
        long sequenceNumber = 0)
        => ReadCore(eventTypeName, storedPayloadVersion, payload, sequenceNumber, validateDeserialization: false,
            deferCurrentPayloadValidation: true);

    private ResolvedEventPayload ReadCore(string eventTypeName, int? storedPayloadVersion, byte[] payload,
        long sequenceNumber, bool validateDeserialization, bool deferCurrentPayloadValidation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);
        int version = storedPayloadVersion ?? 1;
        Type? knownType = ResolveType(eventTypeName, version, sequenceNumber);
        bool historicalAlias = _steps.Any(step => NamesMatch(eventTypeName, step.EventTypeName));
        if (knownType is null && !historicalAlias)
        {
            return new ResolvedEventPayload(eventTypeName, null, payload, version);
        }
        if (version is < 1 or > 1024)
        {
            throw Failure(eventTypeName, version, sequenceNumber, "stored version is outside 1 through 1024");
        }

        string name = eventTypeName;
        JsonObject? json = null;
        string? lastUpcasterType = null;
        try
        {
            for (int hops = 0; hops < 1024; hops++)
            {
                IEventPayloadUpcaster? step = FindStep(name, version, eventTypeName, storedPayloadVersion ?? 1, sequenceNumber);
                if (step is null)
                {
                    Type? terminal = ResolveType(name, storedPayloadVersion ?? 1, sequenceNumber);
                    if (terminal is null || version != EventPayloadVersionResolver.GetDeclaredVersion(terminal))
                    {
                        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            $"missing step from {name} version {version}");
                    }
                    byte[] effective;
                    try
                    {
                        effective = json is null ? payload : JsonSerializer.SerializeToUtf8Bytes(json, EventStorePayloadSerialization.Options);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            "upcast payload serialization failed", lastUpcasterType, error.GetType().Name);
                    }
                    if (deferCurrentPayloadValidation && effective.Length > MaximumPayloadBytes)
                    {
                        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            "payload exceeds the readable limit");
                    }
                    if (!deferCurrentPayloadValidation)
                    {
                        ValidateCurrentJson(effective, terminal, eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            validateDeserialization);
                    }
                    return new ResolvedEventPayload(terminal.FullName ?? terminal.Name, terminal, effective, version);
                }

                json ??= ParseObject(payload, eventTypeName, storedPayloadVersion ?? 1, sequenceNumber);
                string stepType = step.GetType().FullName ?? step.GetType().Name;
                lastUpcasterType = stepType;
                try
                {
                    json = step.Upcast(json)
                        ?? throw new InvalidOperationException("An upcaster returned null.");
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                        "upcaster failed", stepType, error.GetType().Name);
                }
                name = step.TargetEventTypeName ?? name;
                version++;
            }
        }
        catch (EventPayloadEvolutionException)
        {
            throw;
        }
        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber, "chain exceeded 1024 steps");
    }

    private void ValidateRegistration()
    {
        var admitted = new List<IEventPayloadUpcaster>();
        foreach (IEventPayloadUpcaster step in _steps)
        {
            if (string.IsNullOrWhiteSpace(step.EventTypeName) || step.FromVersion is < 1 or >= 1024)
            {
                throw new InvalidOperationException($"Upcaster {step.GetType().FullName} has invalid event type or version {step.FromVersion}.");
            }
            if (admitted.Any(prior => prior.FromVersion == step.FromVersion
                && NamesMatch(prior.EventTypeName, step.EventTypeName)))
            {
                throw new InvalidOperationException($"Overlapping upcaster for {step.EventTypeName} version {step.FromVersion}.");
            }
            admitted.Add(step);
            if (step.TargetEventTypeName is { } target && ResolveType(target, step.FromVersion, 0) is null)
            {
                throw new InvalidOperationException($"Upcaster {step.GetType().FullName} version {step.FromVersion} targets unknown event type {target}.");
            }
        }
        foreach (IEventPayloadUpcaster step in _steps)
        {
            string outputName = step.TargetEventTypeName ?? step.EventTypeName;
            int outputVersion = step.FromVersion + 1;
            Type? target = ResolveType(outputName, outputVersion, 0);
            if (target is null && !_steps.Any(next => next.FromVersion == outputVersion && NamesMatch(next.EventTypeName, outputName)))
            {
                throw new InvalidOperationException($"Dangling upcaster {step.EventTypeName} output version {outputVersion}.");
            }
            if (target is not null && outputVersion != EventPayloadVersionResolver.GetDeclaredVersion(target)
                && !_steps.Any(next => next.FromVersion == outputVersion && NamesMatch(next.EventTypeName, outputName)))
            {
                throw new InvalidOperationException($"Incomplete upcaster {step.EventTypeName} version {outputVersion}.");
            }
        }
        foreach (Type type in _knownTypes.Values)
        {
            int current = EventPayloadVersionResolver.GetDeclaredVersion(type);
            if (current == 1) { continue; }
            string currentName = type.FullName ?? type.Name;
            string[] origins = [currentName, .. _steps.Where(static step => step.FromVersion == 1).Select(static step => step.EventTypeName)];
            if (!origins.Any(origin => Reaches(origin, 1, currentName, current)))
            {
                throw new InvalidOperationException($"Event type {currentName} version {current} has no continuous chain from version 1.");
            }
        }
    }

    private bool Reaches(string name, int version, string targetName, int targetVersion)
    {
        for (int hops = 0; hops < 1024; hops++)
        {
            IEventPayloadUpcaster[] candidates = _steps.Where(step => step.FromVersion == version && NamesMatch(name, step.EventTypeName)).ToArray();
            if (candidates.Length != 1) { return false; }
            name = candidates[0].TargetEventTypeName ?? name;
            version++;
            if (version == targetVersion && NamesMatch(name, targetName)) { return true; }
            if (version >= targetVersion) { return false; }
        }
        return false;
    }

    private IEventPayloadUpcaster? FindStep(string name, int version, string storedName, int storedVersion, long sequence)
    {
        IEventPayloadUpcaster[] matches = _steps.Where(step => step.FromVersion == version && NamesMatch(name, step.EventTypeName)).ToArray();
        if (matches.Length > 1)
        {
            throw Failure(storedName, storedVersion, sequence, "ambiguous upcaster step");
        }
        return matches.Length == 0 ? null : matches[0];
    }

    private Type? ResolveType(string name, int version, long sequence)
    {
        Type[] matches = _knownTypes.Where(entry => NamesMatch(name, entry.Key)).Select(static entry => entry.Value).Distinct().ToArray();
        if (matches.Length > 1)
        {
            throw Failure(name, version, sequence, "ambiguous event type");
        }
        return matches.Length == 0 ? null : matches[0];
    }

    private static bool NamesMatch(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal)) { return true; }
        return Anchored(left, right) || Anchored(right, left);
    }

    private static bool Anchored(string full, string suffix)
        => full.Length > suffix.Length && full.EndsWith(suffix, StringComparison.Ordinal)
            && full[full.Length - suffix.Length - 1] is '.' or '+';

    private static JsonObject ParseObject(byte[] payload, string name, int version, long sequence)
    {
        if (payload.Length > MaximumPayloadBytes)
        {
            throw Failure(name, version, sequence, "payload exceeds the readable limit");
        }
        try
        {
            return JsonNode.Parse(payload.AsSpan(), s_nodeOptions, s_documentOptions) as JsonObject
                ?? throw new JsonException("A versioned event must be a JSON object.");
        }
        catch (JsonException error)
        {
            throw Failure(name, version, sequence, "invalid JSON", innerExceptionTypeName: error.GetType().Name);
        }
    }

    private static void ValidateCurrentJson(byte[] payload, Type type, string name, int version, long sequence,
        bool validateDeserialization)
    {
        if (payload.Length > MaximumPayloadBytes)
        {
            throw Failure(name, version, sequence, "payload exceeds the readable limit");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, s_documentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || (validateDeserialization
                    && JsonSerializer.Deserialize(document.RootElement, type, EventStorePayloadSerialization.Options) is null))
            {
                throw new JsonException("A known event must deserialize from a JSON object.");
            }
        }
        catch (Exception error) when (error is not OperationCanceledException and not EventPayloadEvolutionException)
        {
            throw Failure(name, version, sequence, "current payload cannot deserialize", innerExceptionTypeName: error.GetType().Name);
        }
    }

    private static EventPayloadEvolutionException Failure(string name, int version, long sequence, string reason,
        string? upcasterTypeName = null, string? innerExceptionTypeName = null)
        => new(name, version, sequence, reason, upcasterTypeName, innerExceptionTypeName);
}
