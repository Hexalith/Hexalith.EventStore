using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hexalith.EventStore.OpenApi;

/// <summary>
/// Restores command endpoint documentation in the runtime OpenAPI document.
/// </summary>
public sealed class CommandDocumentationTransformer : IOpenApiOperationTransformer {
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (string.Equals(context.Description.RelativePath, "api/v1/commands", StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) {
            operation.Summary = "Submits a command for asynchronous processing.";
            operation.Description =
                "The command is validated and routed to the appropriate domain aggregate for processing. " +
                "On success, returns 202 Accepted with a Location header pointing to the status polling endpoint. " +
                "The consumer should poll the status endpoint using the returned MessageId until a terminal status is reached.";
            SetResponseDescription(
                operation,
                "202",
                "Command accepted for processing. Check status at the Location header URL.");
        }
        else if (string.Equals(context.Description.RelativePath, "api/v1/commands/status/{messageId}", StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Description.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)) {
            operation.Summary = "Gets the current processing status of a command by message ID, with bounded correlation compatibility.";
            operation.Description =
                "Tenant-scoped: only returns status for the authenticated user's authorized tenants (SEC-3).\n\n" +
                "Command Lifecycle States:\n\n" +
                "In-flight states continue polling with the Retry-After interval from the 202 response. " +
                "Terminal states mean the command has reached its final outcome. In-flight states indicate " +
                "the consumer should continue polling with the Retry-After interval.";
            SetResponseDescription(
                operation,
                "404",
                "No command status found for the given message or correlation identifier.");
        }

        return Task.CompletedTask;
    }

    private static void SetResponseDescription(OpenApiOperation operation, string statusCode, string description) {
        if (operation.Responses is not null
            && operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? response)
            && response is OpenApiResponse openApiResponse) {
            openApiResponse.Description = description;
        }
    }
}
