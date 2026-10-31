using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Native persistence terminal behind the real authorization/eligibility pipeline; never live owner acceptance.</summary>
internal sealed class TechnicalHttpCoreCommandHandler(RecordingDaprClient state) : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    internal int Effects { get; private set; }
    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        Effects++;
        await RouteCatalogRemoteExecution.InvokeAsync(() => ((Dapr.Client.DaprClient)state).SaveStateAsync("statestore", "business-effect", new { value = 7 }, cancellationToken: cancellationToken), cancellationToken);
        return new SubmitCommandResult(request.CorrelationId);
    }
}
