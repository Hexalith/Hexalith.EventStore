
using System.Text.Json;
using System.Threading.RateLimiting;

using FluentValidation;

using Hexalith.EventStore.CommandApi.Authentication;
using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Configuration;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Filters;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using ExtensionMetadataSanitizer = Hexalith.EventStore.CommandApi.Validation.ExtensionMetadataSanitizer;

namespace Hexalith.EventStore.CommandApi.Extensions;

public static class CommandApiServiceCollectionExtensions
{
    public static IServiceCollection AddCommandApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddProblemDetails();
        _ = services.AddExceptionHandler<ValidationExceptionHandler>();
        _ = services.AddExceptionHandler<AuthorizationServiceUnavailableHandler>();  // 503 — BEFORE 403
        _ = services.AddExceptionHandler<AuthorizationExceptionHandler>();           // 403
        _ = services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
        _ = services.AddExceptionHandler<DomainCommandRejectedExceptionHandler>();
        _ = services.AddExceptionHandler<QueryNotFoundExceptionHandler>();           // 404
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
        _ = services.AddScoped<ITenantValidator>(sp =>
        {
            EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
            if (opts.TenantValidatorActorName is null)
            {
                return sp.GetRequiredService<ClaimsTenantValidator>();
            }

            return sp.GetRequiredService<ActorTenantValidator>();
        });

        _ = services.AddScoped<IRbacValidator>(sp =>
        {
            EventStoreAuthorizationOptions opts = sp.GetRequiredService<IOptions<EventStoreAuthorizationOptions>>().Value;
            if (opts.RbacValidatorActorName is null)
            {
                return sp.GetRequiredService<ClaimsRbacValidator>();
            }

            return sp.GetRequiredService<ActorRbacValidator>();
        });

        _ = services.AddHostedService<CommandApiAuthorizationStartupValidator>();

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

        _ = services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Health endpoints must never be rate limited (H2)
                string path = context.Request.Path.Value ?? string.Empty;
                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/alive", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/ready", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter<string>("__health");
                }

                // Use eventstore:tenant claim set by EventStoreClaimsTransformation (H1)
                string tenantId = context.User?.FindFirst("eventstore:tenant")?.Value ?? "anonymous";

                IOptions<RateLimitingOptions> options = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>();
                RateLimitingOptions rateLimitOptions = options.Value;

                return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ =>
                    new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow,
                        QueueLimit = rateLimitOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });

            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                // H10: Wrap entire OnRejected body in try/catch for resilience
                try
                {
                    IOptions<RateLimitingOptions> options = context.HttpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>();
                    ILogger logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.EventStore.RateLimiting");

                    string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? string.Empty;
                    string tenantId = context.HttpContext.User?.FindFirst("eventstore:tenant")?.Value
                        ?? context.HttpContext.Items["RequestTenantId"]?.ToString()
                        ?? "unknown";
                    string? sourceIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

                    // H11: Use RetryAfter metadata if available, fall back to WindowSeconds
                    int retryAfterSeconds = options.Value.WindowSeconds;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    }

                    logger.LogWarning(
                        "Rate limit exceeded: CorrelationId={CorrelationId}, TenantId={TenantId}, SourceIP={SourceIP}",
                        correlationId,
                        tenantId,
                        sourceIp);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                    context.HttpContext.Response.ContentType = "application/problem+json";

                    var problemDetails = new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too Many Requests",
                        status = 429,
                        detail = $"Rate limit exceeded for tenant '{tenantId}'. Please retry after the specified interval.",
                        instance = context.HttpContext.Request.Path.Value,
                        correlationId,
                        tenantId,
                    };

                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(problemDetails),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Fallback: if OnRejected throws, ASP.NET Core returns bare 500. Write minimal 429 instead.
                    // Use GetService (not GetRequiredService) to avoid a secondary throw if DI is unavailable.
                    ILogger? logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Hexalith.EventStore.RateLimiting");
                    string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? string.Empty;
                    logger?.LogError(ex, "OnRejected callback failed: CorrelationId={CorrelationId}", correlationId);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/problem+json";
                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new { status = 429, title = "Too Many Requests" }),
                        cancellationToken).ConfigureAwait(false);
                }
            };
        });

        // OpenAPI document generation (Story 2.9)
        _ = services.AddOpenApi(options =>
        {
            _ = options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Hexalith EventStore Command API",
                    Version = "v1",
                    Description = "Event Sourcing infrastructure server for multi-tenant command processing with per-tenant rate limiting, JWT authentication, and comprehensive status tracking.",
                };

                // Add JWT Bearer security scheme
                OpenApiComponents components = document.Components ??= new OpenApiComponents();
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
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
            _ = options.AddOperationTransformer((operation, context, ct) =>
            {
                operation.Responses ??= [];
                _ = operation.Responses.TryAdd("429", new OpenApiResponse
                {
                    Description = "Too Many Requests - Rate limit exceeded. See Retry-After header for when to retry.",
                });

                return Task.CompletedTask;
            });
        });

        _ = services.AddMediatR(cfg =>
        {
            _ = cfg.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>();
            _ = cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            _ = cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            _ = cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        _ = services.AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>();

        _ = services.AddControllers(options => options.Filters.Add<ValidateModelFilter>());

        return services;
    }
}
