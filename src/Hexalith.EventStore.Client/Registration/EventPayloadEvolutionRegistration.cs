using System.Reflection;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Collects host registrations until the immutable evolution registry is constructed.</summary>
internal sealed class EventPayloadEvolutionRegistration
{
    private readonly HashSet<Type> _knownTypes = [];
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly HashSet<Type> _upcasterTypes = [];

    internal void AddKnownType(Type type) => _knownTypes.Add(type);

    internal void AddAssembly(Assembly assembly) => _assemblies.Add(assembly);

    internal void AddUpcaster(Type type) => _upcasterTypes.Add(type);

    /// <summary>Adds a state's Apply event payload types and scans the assemblies that define them.</summary>
    internal void AddApplyEventTypes(Type stateType)
    {
        foreach (Type eventType in ApplyMethodResolver.GetOrBuildTable(stateType).ByType.Keys)
        {
            if (!typeof(IEventPayload).IsAssignableFrom(eventType)) { continue; }
            _ = _knownTypes.Add(eventType);
            _ = _assemblies.Add(eventType.Assembly);
        }
    }

    internal EventPayloadEvolutionRegistry Build()
    {
        IEventPayloadUpcaster[] discovered = _assemblies.SelectMany(EventPayloadEvolutionRegistry.DiscoverUpcasters).ToArray();
        IEventPayloadUpcaster[] explicitSteps = _upcasterTypes.Select(static type =>
            Activator.CreateInstance(type, nonPublic: true) as IEventPayloadUpcaster
                ?? throw new InvalidOperationException($"Upcaster {type.FullName} has no parameterless constructor.")).ToArray();
        var relevantNames = new HashSet<string>(_knownTypes.Select(static type =>
            type.FullName ?? type.Name), StringComparer.Ordinal);
        var selected = new List<IEventPayloadUpcaster>(explicitSteps);
        foreach (IEventPayloadUpcaster step in explicitSteps) { relevantNames.Add(step.EventTypeName); }
        bool changed;
        do
        {
            changed = false;
            foreach (IEventPayloadUpcaster step in discovered)
            {
                if (selected.Contains(step) || !relevantNames.Any(name => RelevantStepName(name, step.EventTypeName)
                    || (step.TargetEventTypeName is { } target && RelevantStepName(name, target)))
                    && !selected.Any(next => next.FromVersion == step.FromVersion + 1
                        && RelevantStepName(step.TargetEventTypeName ?? step.EventTypeName, next.EventTypeName)))
                {
                    continue;
                }
                selected.Add(step);
                relevantNames.Add(step.EventTypeName);
                changed = true;
            }
        }
        while (changed);
        return new EventPayloadEvolutionRegistry(_knownTypes, selected);
    }

    private static bool RelevantStepName(string knownName, string stepName)
    {
        knownName = ApplyMethodResolver.NormalizeTypeName(knownName);
        stepName = ApplyMethodResolver.NormalizeTypeName(stepName);
        return string.Equals(knownName, stepName, StringComparison.Ordinal)
            || (knownName.Length > stepName.Length && knownName.EndsWith(stepName, StringComparison.Ordinal)
                && knownName[knownName.Length - stepName.Length - 1] is '.' or '+');
    }
}
