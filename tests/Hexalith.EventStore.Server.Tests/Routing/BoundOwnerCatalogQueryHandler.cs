using System.Text.Json;

using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Records the isolated terminal after the real controller proof and MediatR authorization/eligibility pipeline.</summary>
internal sealed class BoundOwnerCatalogQueryHandler(Func<bool> leased) : IRequestHandler<SubmitQuery, SubmitQueryResult>
{
    internal int Calls { get; private set; }

    public Task<SubmitQueryResult> Handle(SubmitQuery request, CancellationToken cancellationToken)
    {
        Assert.True(leased());
        Calls++;
        return Task.FromResult(new SubmitQueryResult(request.CorrelationId, JsonSerializer.SerializeToElement(new { allowed = true }), null, null));
    }
}
