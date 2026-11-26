using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Forwards the structured CloudEvent <c>pubsubname</c> attribute as the <c>Cloudevent.pubsubname</c> header.
/// </summary>
/// <remarks>
/// Dapr's <c>CloudEventsMiddleware</c> forwards CloudEvent attributes as headers but always excludes
/// <c>pubsubname</c>, so the domain-event callback could never verify the delivering component. This middleware runs
/// before it, replaces any caller-supplied value with the one in the body, and leaves the body readable for unwrapping.
/// </remarks>
internal static class CloudEventPubSubNameForwarding
{
    /// <summary>The forwarded header name, matching the casing Dapr uses for the other CloudEvent headers.</summary>
    internal const string HeaderName = "Cloudevent.pubsubname";

    /// <summary>Registers the forwarding middleware.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    internal static IApplicationBuilder UseCloudEventPubSubNameForwarding(this IApplicationBuilder app)
        => app.Use(ForwardAsync);

    private static async Task ForwardAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (HttpMethods.IsPost(request.Method)
            && request.ContentType?.StartsWith("application/cloudevents+json", StringComparison.OrdinalIgnoreCase) == true)
        {
            _ = request.Headers.Remove(HeaderName);
            request.EnableBuffering();
            try
            {
                using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.RequestAborted)
                    .ConfigureAwait(false);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("pubsubname", out JsonElement pubSubName)
                    && pubSubName.ValueKind == JsonValueKind.String)
                {
                    request.Headers[HeaderName] = pubSubName.GetString();
                }
            }
            catch (JsonException)
            {
                // The CloudEvents middleware and the callback reject the malformed body; no header is forwarded.
            }
            finally
            {
                request.Body.Position = 0;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
