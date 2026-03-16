
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

    private enum ChallengeKind {
        MissingToken,
        InvalidToken,
        ExpiredToken,
    }

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
                ChallengeKind challengeKind = GetChallengeKind(context.Error, context.AuthenticateFailure);
                string challengeReason = GetChallengeReason(context.Error, challengeKind);

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
                string detail = GetDetailMessage(challengeKind);
                string typeUri = challengeKind == ChallengeKind.ExpiredToken
                    ? ProblemTypeUris.TokenExpired
                    : ProblemTypeUris.AuthenticationRequired;

                // Set WWW-Authenticate header per RFC 6750 (UX-DR4)
                context.Response.Headers.WWWAuthenticate = GetWwwAuthenticateHeader(challengeKind);

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

    private static ChallengeKind GetChallengeKind(string? error, Exception? failure) {
        if (failure is SecurityTokenExpiredException) {
            return ChallengeKind.ExpiredToken;
        }

        if (failure is not null) {
            return ChallengeKind.InvalidToken;
        }

        // Only the RFC 6750 invalid_token challenge maps to the invalid-JWT client contract.
        // Other challenge error codes must not be rewritten into the invalid-token response.
        if (string.Equals(error, "invalid_token", StringComparison.OrdinalIgnoreCase)) {
            return ChallengeKind.InvalidToken;
        }

        return ChallengeKind.MissingToken;
    }

    private static string GetChallengeReason(string? error, ChallengeKind challengeKind) {
        return challengeKind switch {
            ChallengeKind.ExpiredToken => "TokenExpired",
            ChallengeKind.InvalidToken => error ?? "InvalidToken",
            _ => string.IsNullOrWhiteSpace(error) ? "MissingToken" : error,
        };
    }

    private static string GetDetailMessage(ChallengeKind challengeKind) {
        return challengeKind switch {
            ChallengeKind.ExpiredToken => "The provided authentication token has expired.",
            ChallengeKind.InvalidToken => "The provided authentication token is invalid.",
            _ => "Authentication is required to access this resource.",
        };
    }

    private static string GetWwwAuthenticateHeader(ChallengeKind challengeKind) {
        return challengeKind switch {
            ChallengeKind.ExpiredToken => "Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token has expired\"",
            ChallengeKind.InvalidToken => "Bearer realm=\"hexalith-eventstore\", error=\"invalid_token\", error_description=\"The token is invalid\"",
            _ => "Bearer realm=\"hexalith-eventstore\"",
        };
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
