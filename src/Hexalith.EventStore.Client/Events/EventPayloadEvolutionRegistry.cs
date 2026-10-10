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
            known.Add(ApplyMethodResolver.NormalizeTypeName(type.FullName ?? type.Name), type);
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
                    if (!selected.Contains(step) && relevantNames.Any(name => RelevantStepName(name, step.TargetEventTypeName ?? step.EventTypeName)))
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

    /// <summary>Reads a subscription event without binding unrelated full names by a CLR short name.</summary>
    internal ResolvedEventPayload ReadForSubscription(string eventTypeName, int? storedPayloadVersion, byte[] payload,
        long sequenceNumber = 0)
        => ReadCore(eventTypeName, storedPayloadVersion, payload, sequenceNumber, validateDeserialization: true,
            deferCurrentPayloadValidation: false, subscription: true);

    private ResolvedEventPayload ReadCore(string eventTypeName, int? storedPayloadVersion, byte[] payload,
        long sequenceNumber, bool validateDeserialization, bool deferCurrentPayloadValidation, bool subscription = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);
        int version = storedPayloadVersion ?? 1;
        string normalizedName = ApplyMethodResolver.NormalizeTypeName(eventTypeName);
        if (subscription && !_knownTypes.ContainsKey(normalizedName)
            && !_steps.Any(step => StepNameMatchesSubscriptionAlias(normalizedName, step.EventTypeName)))
        {
            return new ResolvedEventPayload(eventTypeName, null, payload, version);
        }
        IEventPayloadUpcaster? initialStep = FindStep(eventTypeName, version, eventTypeName, version, sequenceNumber);
        Type? knownType = initialStep is null ? ResolveType(eventTypeName, version, sequenceNumber) : null;
        bool historicalAlias = _steps.Any(step => NameMatchTier(eventTypeName, step.EventTypeName) != 0);
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
                    catch (Exception error)
                    {
                        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            "upcast payload serialization failed", lastUpcasterType, error.GetType().Name);
                    }
                    if (deferCurrentPayloadValidation && effective.Length > MaximumPayloadBytes)
                    {
                        throw Failure(eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            "payload exceeds the readable limit", lastUpcasterType);
                    }
                    if (!deferCurrentPayloadValidation)
                    {
                        ValidateCurrentJson(effective, terminal, eventTypeName, storedPayloadVersion ?? 1, sequenceNumber,
                            validateDeserialization, lastUpcasterType);
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
                catch (Exception error)
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
                throw new InvalidOperationException($"Upcaster {step.GetType().FullName} for event type {step.EventTypeName} has invalid version {step.FromVersion}.");
            }
            if (admitted.Any(prior => prior.FromVersion == step.FromVersion
                && NameMatchTier(prior.EventTypeName, step.EventTypeName) != 0))
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
            if (target is null && FindStep(outputName, outputVersion, outputName, outputVersion, 0) is null)
            {
                throw new InvalidOperationException($"Dangling upcaster {step.EventTypeName} output version {outputVersion}.");
            }
            if (target is not null && outputVersion != EventPayloadVersionResolver.GetDeclaredVersion(target)
                && FindStep(outputName, outputVersion, outputName, outputVersion, 0) is null)
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
            IEventPayloadUpcaster? step = FindStep(name, version, name, version, 0);
            if (step is null) { return false; }
            name = step.TargetEventTypeName ?? name;
            version++;
            string output = ApplyMethodResolver.NormalizeTypeName(name);
            string target = ApplyMethodResolver.NormalizeTypeName(targetName);
            bool uniqueShortTarget = _knownTypes.TryGetValue(target, out Type? targetType)
                && string.Equals(output, targetType.Name, StringComparison.Ordinal)
                && _knownTypes.Values.Count(type => string.Equals(type.Name, output, StringComparison.Ordinal)) == 1;
            if (version == targetVersion && (string.Equals(output, target, StringComparison.Ordinal)
                || uniqueShortTarget))
            {
                return true;
            }
            if (version >= targetVersion) { return false; }
        }
        return false;
    }

    private IEventPayloadUpcaster? FindStep(string name, int version, string storedName, int storedVersion, long sequence)
    {
        string normalized = ApplyMethodResolver.NormalizeTypeName(name);
        IEventPayloadUpcaster[] versionSteps = _steps.Where(step => step.FromVersion == version).ToArray();
        IEventPayloadUpcaster[] matches = versionSteps.Where(step => NameMatchTier(normalized, step.EventTypeName) == 1).ToArray();
        if (matches.Length == 0)
        {
            matches = versionSteps.Where(step => NameMatchTier(normalized, step.EventTypeName) == 2).ToArray();
        }
        bool shortName = !normalized.Contains('.') && !normalized.Contains('+');
        Type[] knownShortMatches = shortName
            ? _knownTypes.Values.Where(type => string.Equals(type.Name, normalized, StringComparison.Ordinal)).Distinct().ToArray()
            : [];
        if (matches.Length == 0 && knownShortMatches.Length == 1)
        {
            string knownName = ApplyMethodResolver.NormalizeTypeName(knownShortMatches[0].FullName ?? knownShortMatches[0].Name);
            matches = versionSteps.Where(step => string.Equals(
                knownName, ApplyMethodResolver.NormalizeTypeName(step.EventTypeName), StringComparison.Ordinal)).ToArray();
        }
        if (matches.Length == 0 && knownShortMatches.Length > 1)
        {
            IEventPayloadUpcaster[] renames = versionSteps.Where(step => NameMatchTier(normalized, step.EventTypeName) == 3
                && step.TargetEventTypeName is not null).ToArray();
            if (renames.Length == 1) { matches = renames; }
            else { throw Failure(storedName, storedVersion, sequence, "ambiguous event type"); }
        }
        if (matches.Length == 0 && knownShortMatches.Length == 0 && !_knownTypes.ContainsKey(normalized))
        {
            matches = versionSteps.Where(step => NameMatchTier(normalized, step.EventTypeName) == 3).ToArray();
        }
        if (matches.Length == 0 && knownShortMatches.Length == 1
            && version < EventPayloadVersionResolver.GetDeclaredVersion(knownShortMatches[0]))
        {
            matches = versionSteps.Where(step => NameMatchTier(normalized, step.EventTypeName) == 3).ToArray();
        }
        if (matches.Length > 1)
        {
            throw Failure(storedName, storedVersion, sequence, "ambiguous upcaster step");
        }
        return matches.Length == 0 ? null : matches[0];
    }

    private Type? ResolveType(string name, int version, long sequence)
    {
        string normalized = ApplyMethodResolver.NormalizeTypeName(name);
        if (_knownTypes.TryGetValue(normalized, out Type? exact))
        {
            return exact;
        }
        Type[] shortMatches = _knownTypes.Values.Where(type => string.Equals(type.Name, normalized, StringComparison.Ordinal))
            .Distinct().ToArray();
        if (shortMatches.Length == 1)
        {
            return shortMatches[0];
        }
        if (shortMatches.Length > 1)
        {
            throw Failure(name, version, sequence, "ambiguous event type");
        }
        var suffixes = _knownTypes.SelectMany(entry => new[]
        {
            (Key: entry.Key, Type: entry.Value),
            (Key: entry.Value.Name, Type: entry.Value),
        }).Where(entry => Anchored(normalized, entry.Key)).ToArray();
        if (suffixes.Length == 0)
        {
            return null;
        }
        int longest = suffixes.Max(static entry => entry.Key.Length);
        Type[] matches = suffixes.Where(entry => entry.Key.Length == longest)
            .Select(static entry => entry.Type).Distinct().ToArray();
        if (matches.Length > 1)
        {
            throw Failure(name, version, sequence, "ambiguous event type");
        }
        return matches[0];
    }

    private static int NameMatchTier(string storedName, string stepName)
    {
        storedName = ApplyMethodResolver.NormalizeTypeName(storedName);
        stepName = ApplyMethodResolver.NormalizeTypeName(stepName);
        if (string.Equals(storedName, stepName, StringComparison.Ordinal)) { return 1; }
        if (Anchored(storedName, stepName)) { return 2; }
        return Anchored(stepName, storedName) ? 3 : 0;
    }

    private static bool StepNameMatchesSubscriptionAlias(string storedName, string stepName)
        => NameMatchTier(storedName, stepName) is 1 or 3;

    private static bool RelevantStepName(string knownName, string stepName)
    {
        knownName = ApplyMethodResolver.NormalizeTypeName(knownName);
        stepName = ApplyMethodResolver.NormalizeTypeName(stepName);
        return string.Equals(knownName, stepName, StringComparison.Ordinal) || Anchored(knownName, stepName);
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
        bool validateDeserialization, string? upcasterTypeName)
    {
        if (payload.Length > MaximumPayloadBytes)
        {
            throw Failure(name, version, sequence, "payload exceeds the readable limit", upcasterTypeName);
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
            throw Failure(name, version, sequence, "current payload cannot deserialize", upcasterTypeName,
                error.GetType().Name);
        }
    }

    private static EventPayloadEvolutionException Failure(string name, int version, long sequence, string reason,
        string? upcasterTypeName = null, string? innerExceptionTypeName = null)
        => new(name, version, sequence, reason, upcasterTypeName, innerExceptionTypeName);
}
