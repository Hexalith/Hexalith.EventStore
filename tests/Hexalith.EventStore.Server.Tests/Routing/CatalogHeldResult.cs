using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Represents a response that keeps doing observable work after the endpoint delegate has returned.</summary>
internal sealed class CatalogHeldResult(bool canceled) : IResult
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        await httpContext.Response.Body.WriteAsync("started"u8.ToArray());
        Started.SetResult();
        await Release.Task;
        if (canceled) httpContext.RequestAborted.ThrowIfCancellationRequested();
        await httpContext.Response.Body.WriteAsync("completed"u8.ToArray());
    }
}
