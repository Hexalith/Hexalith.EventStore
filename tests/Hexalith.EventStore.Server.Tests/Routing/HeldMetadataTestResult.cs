using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises metadata transport during the SDK's genuine deferred result lifetime.</summary>
internal sealed class HeldMetadataTestResult(Func<Task> execute) : IResult
{
    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext) => execute();
}
