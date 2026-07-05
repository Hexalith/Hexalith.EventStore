namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Overrides the generated HTTP verb and route template for a command or query message.
/// Applicable to both <see cref="Hexalith.EventStore.Contracts.Commands.ICommandContract"/> and
/// <see cref="Hexalith.EventStore.Contracts.Queries.IQueryContract"/> types.
/// </summary>
/// <param name="verb">The HTTP verb for the generated endpoint.</param>
/// <param name="template">The route template appended to the domain route prefix (e.g. "{tenantId}/users" or "~/api/users/{userId}/tenants"). May be empty to route at the prefix root.</param>
/// <remarks>
/// When this attribute is absent the generator applies the convention fallback: commands map to
/// <c>POST {prefix}</c>; queries map to <c>GET {prefix}</c> (or <c>POST {prefix}</c> when the query
/// carries a body payload), where <c>{prefix}</c> is the assembly-level
/// <see cref="RestApiAttribute.RoutePrefix"/>. The route template is structural; its shape is
/// validated by the source generator, not here — only a null template is rejected.
/// <see cref="ApiScope"/> is optional for source contracts in the API host compilation. It is required
/// for referenced contracts that should be emitted by a host with a non-empty
/// <see cref="RestApiAttribute.Tag"/> so the generator can fail closed instead of publishing unrelated
/// referenced contracts under the host route prefix.
/// Example templates: <c>"{tenantId}"</c>, <c>"{tenantId}/users"</c>,
/// <c>"~/api/users/{userId}/tenants"</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RestRouteAttribute(RestVerb verb, string template) : Attribute
{
    private string? _apiScope;

    /// <summary>Gets the HTTP verb for the generated endpoint.</summary>
    public RestVerb Verb { get; } = verb;

    /// <summary>Gets the route template appended to the domain route prefix.</summary>
    public string Template { get; } = ValidateTemplate(template);

    /// <summary>
    /// Gets or sets the API scope used to filter referenced-contract discovery.
    /// </summary>
    /// <remarks>
    /// When a contract is discovered from a referenced assembly, this value must match the consuming
    /// host's <see cref="RestApiAttribute.Tag"/>. Leave it unset for contracts compiled directly into
    /// the API host, where the host has already explicitly opted into generation.
    /// </remarks>
    public string? ApiScope
    {
        get => _apiScope;
        set => _apiScope = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ValidateTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template;
    }
}
