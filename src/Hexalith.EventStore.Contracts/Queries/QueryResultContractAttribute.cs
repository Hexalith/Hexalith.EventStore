namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>Declares a computed query's typed Gateway result without adding an HTTP route.</summary>
/// <param name="resultType">The public result contract.</param>
/// <param name="domain">The owning domain.</param>
/// <param name="queryType">The existing wire discriminator.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class QueryResultContractAttribute(Type resultType, string domain, string queryType) : Attribute
{
    /// <summary>Gets the result contract.</summary>
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));

    /// <summary>Gets the owning domain.</summary>
    public string Domain { get; } = domain;

    /// <summary>Gets the existing wire discriminator.</summary>
    public string QueryType { get; } = queryType;
}
