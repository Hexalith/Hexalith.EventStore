using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>Retains the backend input actually selected by the SDK topology, independently of shadow consumer component templates.</summary>
internal sealed record EventStoreRedisBackendAnnotation(string? ComponentPath, string DefaultRedisHost) : IResourceAnnotation;
