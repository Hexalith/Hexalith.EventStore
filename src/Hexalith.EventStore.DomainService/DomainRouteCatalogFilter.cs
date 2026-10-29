using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Runs endpoint work only after the registered platform receiver guard admits it.</summary>
internal sealed class DomainRouteCatalogFilter(IDomainRouteCatalogGuard guard, Microsoft.Extensions.Logging.ILogger<DomainRouteCatalogFilter>? logger = null) : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        RouteCatalogDispatchLease? lease;
        try { lease = await guard.AcquireAsync(context).ConfigureAwait(false); }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception) { RouteCatalogRefusalDiagnostics.Report(logger, "receiver", exception); return Results.StatusCode(503); }
        try
        {
            using var scope = lease is null ? null : RouteCatalogRemoteCall.Enter(lease);
            object? result = await next(context).ConfigureAwait(false);
            // ASP.NET executes IResult after endpoint filters return, including streamed/custom results.
            if (result is IResult response && lease is not null) return new DomainRouteCatalogResult(response, lease);
            if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            return result;
        }
        catch
        {
            if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
