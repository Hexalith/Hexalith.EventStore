namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// HTTP verb used by a generated REST endpoint.
/// </summary>
public enum RestVerb {
    /// <summary>HTTP GET.</summary>
    Get,

    /// <summary>HTTP POST.</summary>
    Post,

    /// <summary>HTTP PUT.</summary>
    Put,

    /// <summary>HTTP PATCH.</summary>
    Patch,

    /// <summary>HTTP DELETE.</summary>
    Delete,
}

/// <summary>
/// Overrides the generated HTTP verb and route template for a command or query message.
/// Applicable to both <see cref="Hexalith.EventStore.Contracts.Commands.ICommandContract"/> and
/// <see cref="Hexalith.EventStore.Contracts.Queries.IQueryContract"/> classes.
/// </summary>
/// <remarks>
/// When this attribute is absent the generator applies the convention fallback: commands map to
/// <c>POST {prefix}</c>; queries map to <c>GET {prefix}</c> (or <c>POST {prefix}</c> when the query
/// carries a body payload), where <c>{prefix}</c> is the assembly-level
/// <see cref="RestApiAttribute.RoutePrefix"/>. The route template is structural; its shape is
/// validated by the source generator, not here — only a null template is rejected.
/// Example templates: <c>"{tenantId}"</c>, <c>"{tenantId}/users"</c>,
/// <c>"~/api/users/{userId}/tenants"</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RestRouteAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="RestRouteAttribute"/> class.
    /// </summary>
    /// <param name="verb">The HTTP verb for the generated endpoint.</param>
    /// <param name="template">The route template appended to the domain route prefix (e.g. "{tenantId}/users" or "~/api/users/{userId}/tenants"). May be empty to route at the prefix root.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> is null.</exception>
    public RestRouteAttribute(RestVerb verb, string template) {
        ArgumentNullException.ThrowIfNull(template);
        Verb = verb;
        Template = template;
    }

    /// <summary>Gets the HTTP verb for the generated endpoint.</summary>
    public RestVerb Verb { get; }

    /// <summary>Gets the route template appended to the domain route prefix.</summary>
    public string Template { get; }
}
