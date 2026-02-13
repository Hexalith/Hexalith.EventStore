namespace Microsoft.Extensions.DependencyInjection;

using System.Text.Json;
using System.Threading.RateLimiting;

using FluentValidation;

using Hexalith.EventStore.CommandApi.Authentication;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

public static class CommandApiServiceCollectionExtensions
{
    public static IServiceCollection AddCommandApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<AuthorizationExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHttpContextAccessor();

        // JWT Bearer Authentication (replaces auth stubs from Story 2.1)
        services.AddOptions<EventStoreAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer")
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<EventStoreAuthenticationOptions>, ValidateEventStoreAuthenticationOptions>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorization();

        // Claims transformation
        services.AddTransient<IClaimsTransformation, EventStoreClaimsTransformation>();

        // Command status tracking (Story 2.6)
        services.AddOptions<CommandStatusOptions>()
            .BindConfiguration("EventStore:CommandStatus");
        services.AddSingleton<ICommandStatusStore, DaprCommandStatusStore>();

        // Command archive for replay (Story 2.7)
        services.AddSingleton<ICommandArchiveStore, DaprCommandArchiveStore>();

        // Rate limiting (Story 2.9)
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration("EventStore:RateLimiting")
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RateLimitingOptions>, ValidateRateLimitingOptions>();

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Health endpoints must never be rate limited (H2)
                string path = context.Request.Path.Value ?? string.Empty;
                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/alive", StringComparison.OrdinalIgnoreCase))
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
                    ILogger logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.EventStore.RateLimiting");
                    string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? string.Empty;
                    logger.LogError(ex, "OnRejected callback failed: CorrelationId={CorrelationId}", correlationId);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/problem+json";
                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new { status = 429, title = "Too Many Requests" }),
                        cancellationToken).ConfigureAwait(false);
                }
            };
        });

        // OpenAPI document generation (Story 2.9)
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
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
            options.AddOperationTransformer((operation, context, ct) =>
            {
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("429", new OpenApiResponse
                {
                    Description = "Too Many Requests - Rate limit exceeded. See Retry-After header for when to retry.",
                });

                return Task.CompletedTask;
            });
        });

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ValidateModelFilter>();
        });

        return services;
    }
}
