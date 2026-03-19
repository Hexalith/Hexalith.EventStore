using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Sample;

/// <summary>
/// Routes domain service requests to the keyed processor matching the command domain.
/// </summary>
public static class DomainServiceRequestRouter {
    /// <summary>
    /// Processes a domain service request using the keyed processor registered for the request domain.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The domain service request to process.</param>
    /// <returns>A wire-safe representation of the domain result.</returns>
    public static async Task<DomainServiceWireResult> ProcessAsync(IServiceProvider serviceProvider, DomainServiceRequest request) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        IDomainProcessor processor = serviceProvider.GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain);
        DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);

        return DomainServiceWireResult.FromDomainResult(result);
    }
}
