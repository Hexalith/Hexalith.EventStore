using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hexalith.EventStore.Admin.Server.OpenApi;

/// <summary>
/// Adds common response documentation (401, 403, 503) to admin operations.
/// Scoped to api/v1/admin/ prefix to avoid affecting EventStore endpoints in co-hosted scenarios.
/// </summary>
public sealed class AdminOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // Guard: only transform admin endpoints (safe for co-hosted scenarios)
        string? path = context.Description.RelativePath;
        if (path is null || !path.StartsWith("api/v1/admin/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        operation.Responses ??= [];
        _ = operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized — No valid JWT Bearer token provided",
        });
        _ = operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Forbidden — Insufficient admin role or tenant access denied",
        });
        _ = operation.Responses.TryAdd("503", new OpenApiResponse
        {
            Description = "Service Unavailable — Admin backend service temporarily unavailable (DAPR/infrastructure)",
        });

        return Task.CompletedTask;
    }
}
