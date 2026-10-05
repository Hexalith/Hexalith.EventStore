namespace Hexalith.EventStore.Client.Events;

/// <summary>Names one managed or native dependency by its exact G-row primary-key components.</summary>
internal readonly record struct EventDependencyIdentity(string LogicalLoadIdentity, string Kind);
