
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.CommandApi.Authentication;
/// <summary>
/// Configures JwtBearerOptions using EventStoreAuthenticationOptions from configuration.
/// Supports two modes: OIDC discovery (production) and symmetric key (development/testing).
/// </summary>
public class ConfigureJwtBearerOptions(
    IOptions<EventStoreAuthenticationOptions> authOptions,
    ILoggerFactory loggerFactory) : IConfigureNamedOptions<JwtBearerOptions> {
    private const string RequestAuditMetadataKey = "AuthFailureRequestAuditMetadata";
    private readonly ILogger _logger = loggerFactory.CreateLogger<ConfigureJwtBearerOptions>();

    public void Configure(string? name, JwtBearerOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme) {
            return;
        }

        EventStoreAuthenticationOptions authConfig = authOptions.Value;

        // Preserve original JWT claim names (avoid Microsoft namespace mapping)
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = authConfig.Issuer,
            ValidAudience = authConfig.Audience,
        };

        if (!string.IsNullOrEmpty(authConfig.Authority)) {
            // Production mode: OIDC discovery
            options.Authority = authConfig.Authority;
            options.RequireHttpsMetadata = authConfig.RequireHttpsMetadata;
        }
        else if (!string.IsNullOrEmpty(authConfig.SigningKey)) {
            // Development/testing mode: symmetric key
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfig.SigningKey));
        }

        // Configure events for failure logging and ProblemDetails responses
        options.Events = new JwtBearerEvents {
            OnAuthenticationFailed = async context => {
                string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
                string sourceIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                string requestPath = context.Request.Path;
                RequestAuditMetadata metadata = await TryExtractRequestAuditMetadataAsync(context.HttpContext).ConfigureAwait(false);

                // Determine failure reason from exception type
                string failureReason = context.Exception switch {
                    SecurityTokenExpiredException => "TokenExpired",
                    SecurityTokenInvalidSignatureException => "InvalidSignature",
                    SecurityTokenInvalidIssuerException => "InvalidIssuer",
                    SecurityTokenInvalidAudienceException => "InvalidAudience",
                    _ => context.Exception?.GetType().Name ?? "Unknown",
                };

                // Log at Warning level with SecurityEvent field (AC #7, NFR11)
                // NEVER log the JWT token itself
                _logger.LogWarning(
                    "Authentication failed: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, SourceIp={SourceIp}, Path={RequestPath}, Tenant={TenantId}, CommandType={CommandType}, Reason={Reason}, FailureLayer={FailureLayer}",
                    "AuthenticationFailed",
                    correlationId,
                    sourceIp,
                    requestPath,
                    metadata.TenantId,
                    metadata.CommandType,
                    failureReason,
                    "JwtValidation");

                await Task.CompletedTask.ConfigureAwait(false);
            },

            OnChallenge = async context => {
                // Suppress default challenge behavior - we'll write our own ProblemDetails response
                context.HandleResponse();

                string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
                string sourceIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                string requestPath = context.Request.Path;
                RequestAuditMetadata metadata = await TryExtractRequestAuditMetadataAsync(context.HttpContext).ConfigureAwait(false);

                // Determine challenge reason
                string challengeReason = string.IsNullOrEmpty(context.Error)
                    ? "MissingToken"
                    : context.Error;

                // Log failed auth at Warning level with SecurityEvent field (AC #7, NFR11)
                // NEVER log the JWT token itself — server-side logging retains all diagnostic details
                _logger.LogWarning(
                    "Authentication challenge: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, SourceIp={SourceIp}, Path={RequestPath}, Tenant={TenantId}, CommandType={CommandType}, Reason={Reason}, FailureLayer={FailureLayer}",
                    "AuthenticationFailed",
                    correlationId,
                    sourceIp,
                    requestPath,
                    metadata.TenantId,
                    metadata.CommandType,
                    challengeReason,
                    "JwtChallenge");

                // Determine human-readable detail message and type URI based on failure type
                string detail = GetDetailMessage(context.AuthenticateFailure);
                bool isExpired = context.AuthenticateFailure is SecurityTokenExpiredException;
                string typeUri = isExpired ? ProblemTypeUris.TokenExpired : ProblemTypeUris.AuthenticationRequired;

                // Set WWW-Authenticate header per RFC 6750 (UX-DR4)
                context.Response.Headers.WWWAuthenticate = GetWwwAuthenticateHeader(context.AuthenticateFailure);

                // UX-DR2: No correlationId or tenantId on 401 (pre-pipeline rejection)
                var problemDetails = new ProblemDetails {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Type = typeUri,
                    Detail = detail,
                    Instance = context.Request.Path,
                };

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    problemDetails,
                    options: null,
                    contentType: "application/problem+json").ConfigureAwait(false);
            },
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    private static string GetDetailMessage(Exception? failure) {
        if (failure is SecurityTokenExpiredException) {
            return "The provided authentication token has expired.";
        }

        if (failure is not null) {
            return "The provided authentication token is invalid.";
        }

        return "Authentication is required to access this resource.";
    }

    private static string GetWwwAuthenticateHeader(Exception? failure) {
        if (failure is SecurityTokenExpiredException) {
            return "Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token has expired\"";
        }

        if (failure is not null) {
            return "Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token is invalid\"";
        }

        return "Bearer realm=\"hexalith-eventstore\"";
    }

    private static async Task TryAddTenantExtensionAsync(HttpContext httpContext, ProblemDetails problemDetails) {
        // Try HttpContext.Items first (set by controller for valid requests)
        if (httpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj)
            && tenantObj is string tenantId
            && !string.IsNullOrEmpty(tenantId)) {
            problemDetails.Extensions["tenantId"] = tenantId;
            return;
        }

        // During auth failure the controller hasn't run, so try extracting tenant from the request body.
        // Best-effort: do not fail if the body is unreadable or missing the tenant field.
        try {
            if (httpContext.Request.Body.CanSeek) {
                httpContext.Request.Body.Position = 0;
            }
            else if (httpContext.Request.Body.CanRead) {
                httpContext.Request.EnableBuffering();
                httpContext.Request.Body.Position = 0;
            }
            else {
                return;
            }

            using JsonDocument doc = await JsonDocument.ParseAsync(httpContext.Request.Body).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("tenant", out JsonElement tenantElement)
                && tenantElement.ValueKind == JsonValueKind.String) {
                string? tenant = tenantElement.GetString();
                if (!string.IsNullOrEmpty(tenant)) {
                    problemDetails.Extensions["tenantId"] = tenant;
                }
            }

            // Reset position for any downstream consumers
            if (httpContext.Request.Body.CanSeek) {
                httpContext.Request.Body.Position = 0;
            }
        }
        catch (Exception) {
            // Best-effort: silently ignore failures
        }
    }

    private static async Task<RequestAuditMetadata> TryExtractRequestAuditMetadataAsync(HttpContext httpContext) {
        if (httpContext.Items.TryGetValue(RequestAuditMetadataKey, out object? cached)
            && cached is RequestAuditMetadata cachedMetadata) {
            return cachedMetadata;
        }

        var metadata = new RequestAuditMetadata("unknown", "unknown");

        if (!httpContext.Request.Body.CanRead) {
            httpContext.Items[RequestAuditMetadataKey] = metadata;
            return metadata;
        }

        try {
            if (!httpContext.Request.Body.CanSeek) {
                httpContext.Request.EnableBuffering();
            }

            httpContext.Request.Body.Position = 0;
            using JsonDocument doc = await JsonDocument.ParseAsync(httpContext.Request.Body).ConfigureAwait(false);

            string tenant = "unknown";
            if (doc.RootElement.TryGetProperty("tenant", out JsonElement tenantElement)
                && tenantElement.ValueKind == JsonValueKind.String) {
                tenant = tenantElement.GetString() ?? "unknown";
            }

            string commandType = "unknown";
            if (doc.RootElement.TryGetProperty("commandType", out JsonElement commandTypeElement)
                && commandTypeElement.ValueKind == JsonValueKind.String) {
                commandType = commandTypeElement.GetString() ?? "unknown";
            }

            metadata = new RequestAuditMetadata(tenant, commandType);

            if (httpContext.Request.Body.CanSeek) {
                httpContext.Request.Body.Position = 0;
            }
        }
        catch (Exception) {
            // Best-effort: do not fail auth flow on metadata extraction errors.
            metadata = new RequestAuditMetadata("unknown", "unknown");
        }

        httpContext.Items[RequestAuditMetadataKey] = metadata;
        return metadata;
    }

    private sealed record RequestAuditMetadata(string TenantId, string CommandType);
}
