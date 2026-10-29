using Hexalith.EventStore.Contracts.Projections;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class CatalogStateDispatchResult(Func<Task<ProjectionDispatchResponse>> dispatch) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = await dispatch().ConfigureAwait(false);
        await httpContext.Response.WriteAsJsonAsync(response).ConfigureAwait(false);
    }
}
