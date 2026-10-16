using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public sealed partial class DaprTestContainerFixture
{
    private static async Task<ProjectionDispatchResponse> ExecuteLiveRebuildAsync(IServiceProvider services,
        ProjectionDispatchRequest request, ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity,
        DomainProjectionRebuildBatchAction action, HttpContext context, CancellationToken cancellationToken)
    {
        request = DomainProjectionReplayContext.Bind(request, context.Request.Headers);
        ProjectionDispatchResponse response;
        if (context.Request.Headers.TryGetValue(ProjectionReplayRecoveryBinding.HeaderName, out var header))
            response = await DomainProjectionReplayRecovery.ExecuteAsync(services, request, options, identity, action,
                header.ToString(), cancellationToken).ConfigureAwait(false);
        else response = action switch
        {
            DomainProjectionRebuildBatchAction.Stage => await DomainProjectionDispatcher.StageRebuildAsync(services, request, options, identity, cancellationToken).ConfigureAwait(false),
            DomainProjectionRebuildBatchAction.Commit => await DomainProjectionDispatcher.CommitRebuildAsync(services, request, options, identity, cancellationToken).ConfigureAwait(false),
            DomainProjectionRebuildBatchAction.Abort => await DomainProjectionDispatcher.AbortRebuildAsync(services, request, options, identity, cancellationToken).ConfigureAwait(false),
            _ => await DomainProjectionDispatcher.VerifyRebuildAsync(services, request, options, identity, cancellationToken).ConfigureAwait(false),
        };
        if (request.Request.Domain == LiveReplayCounterProjectionHandler.SourceDomain
            && response.Outcomes.All(static outcome => outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)
            && services.GetRequiredService<LiveReplayCounterFaultControl>().Consume(action)) context.Abort();
        return response;
    }
}
