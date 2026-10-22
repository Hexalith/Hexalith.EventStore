namespace Hexalith.McpCli.Abstractions;

/// <summary>
/// Declares an agent-facing gateway query on a Contracts class or record.
/// </summary>
/// <param name="description">What the query reads for a reader unfamiliar with the source.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HexalithQueryAttribute(string description) : Attribute
{
    /// <summary>Gets the required query description.</summary>
    public string Description { get; } = description;

    /// <summary>Gets or sets a validating JSON payload example.</summary>
    public string? Example { get; set; }

    /// <summary>Gets or sets the ASCII lowercase kebab-case operation name part, overriding the name derived from the type name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the gateway domain when no contract interface supplies it.</summary>
    public string? Domain { get; set; }

    /// <summary>Gets or sets the gateway wire type when no contract interface supplies it.</summary>
    public string? WireType { get; set; }

    /// <summary>Gets or sets the exact CLR property containing the aggregate identifier.</summary>
    public string? AggregateIdProperty { get; set; }

    /// <summary>Gets or sets a constant aggregate identifier for a list query; mutually exclusive with <see cref="AggregateIdProperty"/>.</summary>
    public string? AggregateId { get; set; }

    /// <summary>Gets or sets the gateway projection type when no contract interface supplies it.</summary>
    public string? ProjectionType { get; set; }

    /// <summary>Gets or sets the named projection actor type, when used.</summary>
    public string? ProjectionActorType { get; set; }

    /// <summary>Gets or sets the exact CLR property filled from the envelope tenant.</summary>
    public string? TenantProperty { get; set; }
}
