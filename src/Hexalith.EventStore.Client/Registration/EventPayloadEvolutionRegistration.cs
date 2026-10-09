using System.Reflection;

using Hexalith.EventStore.Client.Events;

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

    internal EventPayloadEvolutionRegistry Build()
    {
        IEventPayloadUpcaster[] discovered = _assemblies.SelectMany(EventPayloadEvolutionRegistry.DiscoverUpcasters).ToArray();
        IEventPayloadUpcaster[] explicitSteps = _upcasterTypes.Select(static type =>
            Activator.CreateInstance(type, nonPublic: true) as IEventPayloadUpcaster
                ?? throw new InvalidOperationException($"Upcaster {type.FullName} has no parameterless constructor.")).ToArray();
        var relevantNames = new HashSet<string>(_knownTypes.SelectMany(static type =>
            new[] { type.FullName ?? type.Name, type.Name }), StringComparer.Ordinal);
        var selected = new List<IEventPayloadUpcaster>(explicitSteps);
        foreach (IEventPayloadUpcaster step in explicitSteps) { relevantNames.Add(step.EventTypeName); }
        bool changed;
        do
        {
            changed = false;
            foreach (IEventPayloadUpcaster step in discovered)
            {
                if (selected.Contains(step) || !relevantNames.Any(name => NamesMatch(name, step.EventTypeName)
                    || (step.TargetEventTypeName is { } target && NamesMatch(name, target))))
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

    private static bool NamesMatch(string left, string right)
        => string.Equals(left, right, StringComparison.Ordinal)
            || (left.Length > right.Length && left.EndsWith(right, StringComparison.Ordinal)
                && left[left.Length - right.Length - 1] is '.' or '+')
            || (right.Length > left.Length && right.EndsWith(left, StringComparison.Ordinal)
                && right[right.Length - left.Length - 1] is '.' or '+');
}
