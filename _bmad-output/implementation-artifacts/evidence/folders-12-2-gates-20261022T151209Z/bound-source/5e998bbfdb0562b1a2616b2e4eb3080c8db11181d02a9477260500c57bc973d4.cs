using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hexalith.EventStore.Admin.Server.OpenApi;

/// <summary>
/// Inspects endpoint authorization metadata and prepends role requirement text
/// to operation descriptions in the OpenAPI document.
/// Scoped to api/v1/admin/ prefix to avoid affecting EventStore endpoints in co-hosted scenarios.
/// </summary>
public sealed class AdminRoleDescriptionTransformer : IOpenApiOperationTransformer {
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // Guard: only transform admin endpoints (safe for co-hosted scenarios)
        string? path = context.Description.RelativePath;
        if (path is null || !path.StartsWith("api/v1/admin/", StringComparison.OrdinalIgnoreCase)) {
            return Task.CompletedTask;
        }

        // Extract [Authorize(Policy = ...)] from endpoint metadata.
        // Use LastOrDefault — method-level [Authorize(Policy)] overrides class-level.
        // ASP.NET Core lists class attributes before method attributes in EndpointMetadata.
        string? policy = context.Description.ActionDescriptor
            .EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .LastOrDefault(p => p is not null);

        string roleText = policy switch {
            AdminAuthorizationPolicies.ReadOnly => "**Required role:** ReadOnly (or higher)\n\n",
            AdminAuthorizationPolicies.Operator => "**Required role:** Operator (or Admin)\n\n",
            AdminAuthorizationPolicies.Admin => "**Required role:** Admin only\n\n",
            _ => "**Required role:** Any authenticated admin user\n\n",
        };

        operation.Description = roleText + (operation.Description ?? string.Empty);
        return Task.CompletedTask;
    }
}
