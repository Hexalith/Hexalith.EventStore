using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class CatalogV3CommandHandler(CatalogGeneratedEffectCounter counter, Func<bool> leaseHeld)
    : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        leaseHeld().ShouldBeTrue();
        counter.Record();
        return Task.FromResult(new SubmitCommandResult(request.CorrelationId));
    }
}
