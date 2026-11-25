using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Optionally maps the transport response for a dispatched domain-service query.</summary>
/// <remarks>The SDK resolves a single policy, so the last registration wins.</remarks>
public interface IDomainQueryResponsePolicy
{
    /// <summary>Creates the response after receiver admission and query dispatch.</summary>
    /// <param name="query">The wire envelope as received, before administrator normalization. Its claim fields, such as
    /// <see cref="QueryEnvelope.IsGlobalAdmin"/>, are unverified wire values and must not drive authorization.</param>
    /// <param name="result">The result returned by the dispatcher.</param>
    /// <returns>The response to write to the caller.</returns>
    IResult CreateResponse(QueryEnvelope query, QueryResult result);
}
