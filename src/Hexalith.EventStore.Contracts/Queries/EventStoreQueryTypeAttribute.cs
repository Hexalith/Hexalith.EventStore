
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Overrides the convention-derived query type name for a query class.
/// When applied, NamingConventionEngine.GetQueryTypeName returns this
/// attribute's value instead of deriving from the type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventStoreQueryTypeAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreQueryTypeAttribute"/> class.
    /// </summary>
    /// <param name="queryType">The explicit query type name. Must be non-empty, non-whitespace, and must not contain colons.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="queryType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queryType"/> is empty, whitespace, or contains colons.</exception>
    public EventStoreQueryTypeAttribute(string queryType) {
        ArgumentNullException.ThrowIfNull(queryType);
        if (string.IsNullOrWhiteSpace(queryType)) {
            throw new ArgumentException(
                "Query type name cannot be empty or whitespace.", nameof(queryType));
        }

        // Colons reserved as actor ID separator — validated at attribute, resolver, and helper layers.
        if (queryType.Contains(':')) {
            throw new ArgumentException(
                "Query type name cannot contain colons (reserved as actor ID separator).",
                nameof(queryType));
        }

        QueryType = queryType;
    }

    /// <summary>Gets the explicit query type name.</summary>
    public string QueryType { get; }
}
