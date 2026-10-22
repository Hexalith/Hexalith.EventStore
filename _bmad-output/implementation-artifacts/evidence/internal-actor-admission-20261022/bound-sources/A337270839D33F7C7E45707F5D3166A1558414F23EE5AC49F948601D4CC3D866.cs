using Hexalith.EventStore.Client.Handlers;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Holds the discovered command and Apply methods for one aggregate runtime type.</summary>
/// <param name="HandleMethods">The exact command aliases bound to their Handle methods.</param>
/// <param name="ApplyMethods">The shared state Apply table.</param>
internal sealed record AggregateCommandDispatchMetadata(
    Dictionary<string, AggregateCommandHandleMethod> HandleMethods,
    ApplyMethodTable ApplyMethods);
