using System.Reflection;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Declares an explicit already-loaded Default-context managed object used by a private composition.</summary>
/// <remarks>Current file identity and exact object identity do not establish immutable executed framework bytes.</remarks>
/// <param name="Dependency">The reviewed managed G declaration, including the exact Default context.</param>
/// <param name="Assembly">The exact runtime object imported by this local composition.</param>
internal sealed record EventManagedAssemblyImport(EventRegistryRow Dependency, Assembly Assembly);
