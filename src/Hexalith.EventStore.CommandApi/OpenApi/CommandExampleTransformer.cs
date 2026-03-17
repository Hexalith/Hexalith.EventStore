using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hexalith.EventStore.CommandApi.OpenApi;

/// <summary>
/// Adds pre-populated example payloads to the POST /commands operation.
/// Uses named examples so Swagger UI shows a dropdown for multiple domains.
/// </summary>
public sealed class CommandExampleTransformer : IOpenApiOperationTransformer {
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // Guard: only apply to POST /api/v1/commands
        if (operation.RequestBody is null) {
            return Task.CompletedTask;
        }

        string? relativePath = context.Description.RelativePath;
        if (!string.Equals(relativePath, "api/v1/commands", StringComparison.OrdinalIgnoreCase)) {
            return Task.CompletedTask;
        }

        if (context.Description.HttpMethod is null
            || !string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) {
            return Task.CompletedTask;
        }

        if (operation.RequestBody?.Content is not null
            && operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? mediaType)) {
            mediaType.Examples ??= new Dictionary<string, IOpenApiExample>();
            mediaType.Examples["IncrementCounter"] = new OpenApiExample {
                Summary = "Increment Counter (Counter domain)",
                Description = "A valid Counter domain IncrementCounter command. Generate a unique ULID for messageId on each submission. Reusing the same messageId triggers idempotency detection and returns a silent success without processing a new command. Replace 'tenant-a' with your actual tenant identifier. If JWT authentication is disabled for local development (EventStore:Auth:Enabled = false), you can test without obtaining a token first.",
                Value = JsonNode.Parse("""
                    {
                      "messageId": "01JAXYZ1234567890ABCDEFGH",
                      "tenant": "tenant-a",
                      "domain": "counter",
                      "aggregateId": "01JAXYZ1234567890ABCDEFJK",
                      "commandType": "IncrementCounter",
                      "payload": {}
                    }
                    """),
            };
        }

        return Task.CompletedTask;
    }
}
