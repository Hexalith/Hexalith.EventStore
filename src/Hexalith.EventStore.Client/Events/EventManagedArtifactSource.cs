namespace Hexalith.EventStore.Client.Events;

/// <summary>Supplies one reviewed G declaration and the file to capture privately before managed loading.</summary>
/// <param name="Dependency">The exact managed G row retained by the artifact owner.</param>
/// <param name="ResolvedFile">The source file whose bytes must match that row during private admission.</param>
internal sealed record EventManagedArtifactSource(EventRegistryRow Dependency, string ResolvedFile);
