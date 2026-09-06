using System.Text.Json;

using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Normalizes Admin API authorization failures into bounded, identifier-free Problem Details.
/// </summary>
public sealed class AdminAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler {
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult) {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Succeeded || !IsAdminEndpoint(context)) {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
            return;
        }

        IHeaderDictionary capturedHeaders = await CaptureSchemeResponseAsync(
            next,
            context,
            policy,
            authorizeResult).ConfigureAwait(false);

        StringValues authenticateHeaders = capturedHeaders.WWWAuthenticate;
        StringValues correlationHeaders = capturedHeaders["X-Correlation-ID"];
        context.Response.Clear();
        context.Response.StatusCode = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        if (authenticateHeaders.Count > 0) {
            context.Response.Headers[HeaderNames.WWWAuthenticate] = authenticateHeaders;
        }

        if (correlationHeaders.Count > 0) {
            context.Response.Headers["X-Correlation-ID"] = correlationHeaders;
        }

        string correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
        var problem = new ProblemDetails {
            Status = context.Response.StatusCode,
            Title = authorizeResult.Challenged ? "Unauthorized" : "Forbidden",
            Detail = authorizeResult.Challenged
                ? "Authentication is required to access this resource."
                : "The request is not authorized for this resource.",
            Extensions = { ["correlationId"] = correlationId },
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            JsonOptions,
            context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<IHeaderDictionary> CaptureSchemeResponseAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult) {
        Stream originalBody = context.Response.Body;
        using var capturedBody = new MemoryStream();
        context.Response.Body = capturedBody;
        try {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
            return new HeaderDictionary(context.Response.Headers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value));
        }
        finally {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsAdminEndpoint(HttpContext context) {
        ControllerActionDescriptor? descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return descriptor?.ControllerTypeInfo.Assembly == typeof(AdminStreamsController).Assembly;
    }
}
