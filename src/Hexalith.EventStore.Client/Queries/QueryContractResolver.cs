
using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Queries;

/// <summary>
/// Resolves and validates query contract metadata from <see cref="IQueryContract"/> implementations.
/// Results are cached per type using a thread-safe <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public static class QueryContractResolver {
    private static readonly ConcurrentDictionary<Type, QueryContractMetadata> _cache = new();

    /// <summary>
    /// Resolves and validates the query contract metadata for the specified query type.
    /// Reads static abstract members, validates all fields against kebab-case rules,
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TQuery">The query type implementing <see cref="IQueryContract"/>.</typeparam>
    /// <returns>The validated <see cref="QueryContractMetadata"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a static member returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when a static member value is invalid.</exception>
    public static QueryContractMetadata Resolve<TQuery>()
        where TQuery : IQueryContract {
        return _cache.GetOrAdd(typeof(TQuery), static _ => {
            string queryType = TQuery.QueryType;
            string domain = TQuery.Domain;
            string projectionType = TQuery.ProjectionType;

            NamingConventionEngine.ValidateKebabCase(queryType, "QueryType");
            NamingConventionEngine.ValidateKebabCase(domain, "Domain");
            NamingConventionEngine.ValidateKebabCase(projectionType, "ProjectionType");

            // Colons reserved as actor ID separator — validated at attribute, resolver, and helper layers.
            if (queryType.Contains(':')) {
                throw new ArgumentException(
                    $"QueryType '{queryType}' cannot contain colons (reserved as actor ID separator).");
            }

            return new QueryContractMetadata(queryType, domain, projectionType);
        });
    }

    /// <summary>
    /// Gets the ETag actor ID for a query contract using ProjectionType (not Domain).
    /// Format: {ProjectionType}:{TenantId}.
    /// </summary>
    /// <typeparam name="TQuery">The query type implementing <see cref="IQueryContract"/>.</typeparam>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The ETag actor ID string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tenantId"/> is empty or whitespace.</exception>
    public static string GetETagActorId<TQuery>(string tenantId)
        where TQuery : IQueryContract {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (string.IsNullOrWhiteSpace(tenantId)) {
            throw new ArgumentException("TenantId cannot be empty or whitespace.", nameof(tenantId));
        }

        if (tenantId.Contains(':')) {
            throw new ArgumentException(
                "TenantId cannot contain colons (reserved as actor ID separator).",
                nameof(tenantId));
        }

        return $"{Resolve<TQuery>().ProjectionType}:{tenantId}";
    }

    /// <summary>
    /// Clears the resolver cache. Intended for test isolation.
    /// </summary>
    internal static void ClearCache() => _cache.Clear();
}
