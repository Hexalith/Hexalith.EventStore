
using System.Text.Json;
using System.Threading.RateLimiting;

using FluentValidation;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Filters;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Validation;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using ExtensionMetadataSanitizer = Hexalith.EventStore.Validation.ExtensionMetadataSanitizer;

namespace Hexalith.EventStore.Extensions;

public static class EventStoreServiceCollectionExtensions {
    public static IServiceCollection AddEventStore(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddProblemDetails();
        _ = services.AddExceptionHandler<ValidationExceptionHandler>();
        _ = services.AddExceptionHandler<AuthorizationServiceUnavailableHandler>();  // 503 — BEFORE 403
        _ = services.AddExceptionHandler<AuthorizationExceptionHandler>();           // 403
        _ = services.AddExceptionHandler<BackpressureExceptionHandler>();
        _ = services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
        _ = services.AddExceptionHandler<DomainCommandRejectedExceptionHandler>();
        _ = services.AddExceptionHandler<QueryNotFoundExceptionHandler>();           // 404
        _ = services.AddExceptionHandler<QueryExecutionFailedExceptionHandler>();    // 403 / 501 query failures
        _ = services.AddExceptionHandler<BackpressureExceptionHandler>();             // 429 (per-aggregate backpressure)
        _ = services.AddExceptionHandler<DaprSidecarUnavailableHandler>();          // 503 (sidecar down)
        _ = services.AddExceptionHandler<GlobalExceptionHandler>();

        _ = services.AddHttpContextAccessor();

        // JWT Bearer Authentication (replaces auth stubs from Story 2.1)
        _ = services.AddOptions<EventStoreAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<EventStoreAuthenticationOptions>, ValidateEventStoreAuthenticationOptions>();
        _ = services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        _ = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        _ = services.AddAuthorization();

        // Authorization options (Story 17-1) — claims-based default, actor-based when configured
        _ = services.AddOptions<EventStoreAuthorizationOptions>()
            .BindConfiguration("EventStore:Authorization")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<EventStoreAuthorizationOptions>, ValidateEventStoreAuthorizationOptions>();

        // Register concrete claims-based implementations (always available)
        _ = services.AddScoped<ClaimsTenantValidator>();
        _ = services.AddScoped<ClaimsRbacValidator>();

        // Register concrete actor-based implementations (always available for DI)
        _ = services.AddScoped<ActorTenantValidator>();
        _ = services.AddScoped<ActorRbacValidator>();

        // Factory delegate selects implementation at resolve-time based on configuration
        _ = services.AddScoped<ITenantValidator>(sp => {
            EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
            if (opts.TenantValidatorActorName is null) {
                return sp.GetRequiredService<ClaimsTenantValidator>();
            }

            return sp.GetRequiredService<ActorTenantValidator>();
        });

        _ = services.AddScoped<IRbacValidator>(sp => {
            EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
            if (opts.RbacValidatorActorName is null) {
                return sp.GetRequiredService<ClaimsRbacValidator>();
            }

            return sp.GetRequiredService<ActorRbacValidator>();
        });

        _ = services.AddHostedService<EventStoreAuthorizationStartupValidator>();

        // Claims transformation
        _ = services.AddTransient<IClaimsTransformation, EventStoreClaimsTransformation>();

        // Command status tracking (Story 2.6)
        _ = services.AddOptions<CommandStatusOptions>()
            .BindConfiguration("EventStore:CommandStatus");
        _ = services.AddSingleton<ICommandStatusStore, DaprCommandStatusStore>();

        // Command archive for replay (Story 2.7)
        _ = services.AddSingleton<ICommandArchiveStore, DaprCommandArchiveStore>();

        // Extension metadata sanitization (Story 5.4, SEC-4)
        _ = services.AddOptions<ExtensionMetadataOptions>()
            .BindConfiguration("EventStore:ExtensionMetadata")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<ExtensionMetadataOptions>, ValidateExtensionMetadataOptions>();
        _ = services.AddSingleton<ExtensionMetadataSanitizer>();

        // Rate limiting (Story 2.9)
        _ = services.AddOptions<RateLimitingOptions>()
            .BindConfiguration("EventStore:RateLimiting")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<RateLimitingOptions>, ValidateRateLimitingOptions>();

        // DAPR config store sync for per-tenant rate limit overrides (optional, graceful fallback)
        _ = services.AddHostedService<DaprRateLimitConfigSync>();

        _ = services.AddRateLimiter(rateLimiterOptions => {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-tenant limiter (existing, unchanged logic)
            var tenantLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
                // Health endpoints must never be rate limited (H2)
                string path = context.Request.Path.Value ?? string.Empty;
                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/alive", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/ready", StringComparison.OrdinalIgnoreCase)) {
                    return RateLimitPartition.GetNoLimiter<string>("__health");
                }

                // Use eventstore:tenant claim set by EventStoreClaimsTransformation (H1)
                string tenantId = context.User?.FindFirst("eventstore:tenant")?.Value ?? "anonymous";

                IOptionsMonitor<RateLimitingOptions> optionsMonitor = context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>();
                RateLimitingOptions rateLimitOptions = optionsMonitor.CurrentValue;

                // Per-tenant resolution: TenantPermitLimits[tenantId] > PermitLimit (default).
                // Note: PartitionedRateLimiter caches SlidingWindowRateLimiter instances per partition key.
                // Updated options only affect partitions created after the change. Existing active partitions
                // keep their old limits until they expire from idle timeout (~60 seconds). This eventual-
                // consistency is acceptable by design.
                int permitLimit = rateLimitOptions.TenantPermitLimits.TryGetValue(tenantId, out int tenantLimit)
                    ? tenantLimit
                    : rateLimitOptions.PermitLimit;

                return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ =>
                    new SlidingWindowRateLimiterOptions {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow,
                        QueueLimit = rateLimitOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });

            // Per-consumer limiter keyed by JWT "sub" claim (Story 7.3)
            var consumerLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
                // Health endpoints must never be rate limited (same exemption as per-tenant)
                string path = context.Request.Path.Value ?? string.Empty;
                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/alive", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/ready", StringComparison.OrdinalIgnoreCase)) {
                    return RateLimitPartition.GetNoLimiter<string>("__health");
                }

                // Consumer identity from JWT "sub" claim. All anonymous traffic shares one consumer bucket.
                string? rawConsumerId = context.User?.FindFirst("sub")?.Value;
                string consumerId = string.IsNullOrWhiteSpace(rawConsumerId) ? "anonymous" : rawConsumerId;

                IOptionsMonitor<RateLimitingOptions> optionsMonitor = context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>();
                RateLimitingOptions rateLimitOptions = optionsMonitor.CurrentValue;

                // Per-consumer resolution: ConsumerPermitLimits[consumerId] > ConsumerPermitLimit (default)
                int permitLimit = rateLimitOptions.ConsumerPermitLimits.TryGetValue(consumerId, out int consumerLimit)
                    ? consumerLimit
                    : rateLimitOptions.ConsumerPermitLimit;

                return RateLimitPartition.GetSlidingWindowLimiter(consumerId, _ =>
                    new SlidingWindowRateLimiterOptions {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.ConsumerWindowSeconds),
                        SegmentsPerWindow = rateLimitOptions.ConsumerSegmentsPerWindow,
                        // QueueLimit is intentionally shared — if per-consumer queuing is ever needed,
                        // it would require a separate ConsumerQueueLimit property
                        QueueLimit = rateLimitOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });

            // CreateChained short-circuits on first rejection — Retry-After reflects the rejecting
            // limiter's window (tenant=60s, consumer=1s). This is correct: clients should retry based
            // on the limit they actually hit.
            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.CreateChained(tenantLimiter, consumerLimiter);

            rateLimiterOptions.OnRejected = async (context, cancellationToken) => {
                // H10: Wrap entire OnRejected body in try/catch for resilience
                try {
                    IOptionsMonitor<RateLimitingOptions> optionsMonitor = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>();
                    ILogger logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.EventStore.RateLimiting");

                    string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? string.Empty;
                    string tenantId = context.HttpContext.User?.FindFirst("eventstore:tenant")?.Value
                        ?? context.HttpContext.Items["RequestTenantId"]?.ToString()
                        ?? "unknown";
                    string? rawConsumerId = context.HttpContext.User?.FindFirst("sub")?.Value;
                    string consumerId = string.IsNullOrWhiteSpace(rawConsumerId) ? "anonymous" : rawConsumerId;
                    string? sourceIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

                    // H11: Use RetryAfter metadata if available, fall back to WindowSeconds
                    int retryAfterSeconds = optionsMonitor.CurrentValue.WindowSeconds;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)) {
                        retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    }

                    logger.LogWarning(
                        "Rate limit exceeded: CorrelationId={CorrelationId}, TenantId={TenantId}, ConsumerId={ConsumerId}, SourceIP={SourceIP}",
                        correlationId,
                        tenantId,
                        consumerId,
                        sourceIp);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                    context.HttpContext.Response.ContentType = "application/problem+json";

                    var problemDetails = new ProblemDetails {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Type = ErrorHandling.ProblemTypeUris.RateLimitExceeded,
                        Detail = "Rate limit exceeded. Please retry after the specified interval.",
                        Instance = context.HttpContext.Request.Path.Value,
                        Extensions =
                        {
                            ["correlationId"] = correlationId,
                            ["tenantId"] = tenantId,
                            ["consumerId"] = consumerId,
                        },
                    };

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        problemDetails,
                        (JsonSerializerOptions?)null,
                        "application/problem+json",
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    // Fallback: if OnRejected throws, ASP.NET Core returns bare 500. Write minimal 429 instead.
                    // Use GetService (not GetRequiredService) to avoid a secondary throw if DI is unavailable.
                    ILogger? logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Hexalith.EventStore.RateLimiting");
                    string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? string.Empty;
                    logger?.LogError(ex, "OnRejected callback failed: CorrelationId={CorrelationId}", correlationId);

                    if (context.HttpContext.Response.HasStarted) {
                        return;
                    }

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/problem+json";

                    // Preserve Retry-After in fallback: try to read from options, default to 60s
                    int fallbackRetryAfter = 60;
                    try {
                        RateLimitingOptions? opts = context.HttpContext.RequestServices
                            .GetService<IOptionsMonitor<RateLimitingOptions>>()?.CurrentValue;
                        if (opts is not null) {
                            fallbackRetryAfter = opts.WindowSeconds;
                        }
                    }
                    catch (Exception) {
                        // Best-effort: keep default if DI fails
                    }

                    context.HttpContext.Response.Headers.RetryAfter = fallbackRetryAfter.ToString();

                    var fallbackProblemDetails = new ProblemDetails {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Type = ErrorHandling.ProblemTypeUris.RateLimitExceeded,
                        Detail = "Rate limit exceeded. Please retry after the specified interval.",
                        Instance = context.HttpContext.Request.Path.Value,
                    };
                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(fallbackProblemDetails),
                        CancellationToken.None).ConfigureAwait(false);
                }
            };
        });

        // OpenAPI document generation (Story 2.9)
        _ = services.AddOpenApi(options => {
            _ = options.AddDocumentTransformer((document, context, ct) => {
                document.Info = new OpenApiInfo {
                    Title = "Hexalith EventStore Command API",
                    Version = "v1",
                    Description = "Event Sourcing infrastructure server for multi-tenant command processing with per-tenant rate limiting, JWT authentication, and comprehensive status tracking. Error reference documentation is available at `/problems/{error-type}` on this server. In production, error type URIs resolve at `https://hexalith.io/problems/{error-type}`.",
                };

                // Add JWT Bearer security scheme
                OpenApiComponents components = document.Components ??= new OpenApiComponents();
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Bearer token. Obtain from your identity provider and include as: Authorization: Bearer {token}",
                };

                var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    { schemeReference, new List<string>() },
                });

                return Task.CompletedTask;
            });

            // Add 429 response documentation to all operations (H14)
            _ = options.AddOperationTransformer((operation, context, ct) => {
                operation.Responses ??= [];
                _ = operation.Responses.TryAdd("429", new OpenApiResponse {
                    Description = "Too Many Requests - Rate limit exceeded. See Retry-After header for when to retry.",
                });

                return Task.CompletedTask;
            });

            // Add pre-populated example payloads (Story 3.6, UX-DR13)
            _ = options.AddOperationTransformer<CommandExampleTransformer>();
        });

        _ = services.AddMediatR(cfg => {
            _ = cfg.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>();
            _ = cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            _ = cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            _ = cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        _ = services.AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>();

        _ = services.AddControllers(options => options.Filters.Add<ValidateModelFilter>());

        _ = services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context => {
            string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
            string? tenantId = context.HttpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj)
                && tenantObj is string tenant
                && !string.IsNullOrWhiteSpace(tenant)
                    ? tenant
                    : null;

            List<(string Key, string Message)> failures = context.ModelState
                .Where(kvp => kvp.Value is { Errors.Count: > 0 })
                .SelectMany(kvp => kvp.Value!.Errors.Select(err => {
                    string message = string.IsNullOrWhiteSpace(err.ErrorMessage)
                        ? "Invalid value."
                        : err.ErrorMessage;
                    return (NormalizeModelStateKey(kvp.Key), message);
                }))
                .ToList();

            var errors = failures
                .GroupBy(f => f.Key)
                .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(f => f.Message)));

            int errorCount = failures.Count;
            ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(
                $"The command has {errorCount} validation error(s). See 'errors' for specifics.",
                errors,
                correlationId,
                tenantId);
            problemDetails.Instance = context.HttpContext.Request.Path;

            var result = new BadRequestObjectResult(problemDetails);
            result.ContentTypes.Add("application/problem+json");
            return result;
        });

        return services;
    }

    private static string NormalizeModelStateKey(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            return "request";
        }

        string normalized = key.Trim();
        if (normalized.StartsWith("$.", StringComparison.Ordinal)) {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith("$", StringComparison.Ordinal)) {
            normalized = normalized[1..];
        }

        if (normalized.StartsWith(".", StringComparison.Ordinal)) {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized)) {
            return "request";
        }

        return string.Join(
            ".",
            normalized
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
