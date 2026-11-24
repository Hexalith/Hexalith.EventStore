using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Optionally maps the transport response for a dispatched domain-service query.</summary>
public interface IDomainQueryResponsePolicy
{
    /// <summary>Creates the response after receiver admission and query dispatch.</summary>
    /// <param name="query">The original query envelope received by the host.</param>
    /// <param name="result">The result returned by the dispatcher.</param>
    /// <returns>The response to write to the caller.</returns>
    IResult CreateResponse(QueryEnvelope query, QueryResult result);
}
