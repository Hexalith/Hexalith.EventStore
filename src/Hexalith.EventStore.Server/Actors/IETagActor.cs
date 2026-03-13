
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor interface for projection ETag management.
/// Each actor instance tracks the current ETag for a projection+tenant pair.
/// Actor ID format: "{ProjectionType}:{TenantId}" (colon separator per codebase convention).
/// </summary>
public interface IETagActor : IActor {
    /// <summary>
    /// Gets the current ETag value, or null if never set (cold start).
    /// Returns in constant time (SEC-4).
    /// </summary>
    Task<string?> GetCurrentETagAsync();

    /// <summary>
    /// Generates a new base64url-encoded GUID ETag, persists it to DAPR state store,
    /// and returns the new value. The in-memory cache is only updated after successful persistence (FM-1).
    /// </summary>
    Task<string> RegenerateAsync();
}
