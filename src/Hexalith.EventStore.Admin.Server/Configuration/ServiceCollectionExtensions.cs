using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.OpenApi;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Hexalith.EventStore.Admin.Server.Configuration;

/// <summary>
/// Extension methods for registering Admin.Server services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the Admin API layer: authorization policies, claims transformation, tenant filter, and admin services.
    /// The host (Story 14-4) MUST additionally call <c>AddAuthentication().AddJwtBearer()</c> and
    /// <c>AddControllers().AddApplicationPart(typeof(AdminStreamsController).Assembly)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdminApi(
        this IServiceCollection services,
        IConfiguration configuration) {
        // 1. Authorization policies (NFR46)
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminAuthorizationPolicies.ReadOnly, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole))
            .AddPolicy(AdminAuthorizationPolicies.Operator, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Operator), nameof(Abstractions.Models.Common.AdminRole.Admin)))
            .AddPolicy(AdminAuthorizationPolicies.Admin, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin)));

        // 2. Admin claims transformation (maps existing claims to admin roles)
        services.AddTransient<IClaimsTransformation, AdminClaimsTransformation>();

        // 3. Tenant authorization filter
        services.AddScoped<AdminTenantAuthorizationFilter>();

        // 4. Register admin services (from Story 14-2)
        services.AddAdminServer(configuration);

        // 5. OpenAPI document generation (Story 14-5)
        services.AddAdminOpenApi();

        return services;
    }

    /// <summary>
    /// Registers OpenAPI document generation for the Admin API.
    /// The host must call <c>MapOpenApi()</c> and <c>UseSwaggerUI()</c> to enable endpoints.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdminOpenApi(
        this IServiceCollection services) {
        services.AddOpenApi(options =>
        {
            // Document metadata and JWT security scheme
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Hexalith EventStore Admin API",
                    Version = "v1",
                    Description = "Administration API for Hexalith EventStore — stream browsing, projection management, type catalog, health monitoring, storage operations, dead-letter management, and tenant administration. Requires JWT Bearer authentication with role-based access control (ReadOnly, Operator, Admin). Error reference documentation is available at /api/v1/admin/problems/{error-type} on this server.",
                };

                // Add JWT Bearer security scheme (same pattern as CommandApi)
                OpenApiComponents components = document.Components ??= new OpenApiComponents();
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Bearer token with admin role claims. Roles: ReadOnly (stream browsing, health), Operator (projection controls, snapshots), Admin (tenant management). Obtain from your identity provider.",
                };

                var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    { schemeReference, new List<string>() },
                });

                return Task.CompletedTask;
            });

            // Common response codes on all admin operations
            options.AddOperationTransformer<AdminOperationTransformer>();

            // Role descriptions per endpoint
            options.AddOperationTransformer<AdminRoleDescriptionTransformer>();
        });

        return services;
    }

    /// <summary>
    /// Adds all Admin.Server DAPR-backed service implementations to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdminServer(
        this IServiceCollection services,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<AdminServerOptions>(
            configuration.GetSection(AdminServerOptions.SectionName));
        services.TryAddSingleton<IValidateOptions<AdminServerOptions>, AdminServerOptionsValidator>();

        // Host-agnostic default auth context.
        // ASP.NET Core hosts can override this with an IHttpContextAccessor-based implementation.
        services.TryAddScoped<IAdminAuthContext, NullAdminAuthContext>();

        // Register all 10 service implementations as scoped
        services.TryAddScoped<IStreamQueryService, DaprStreamQueryService>();
        services.TryAddScoped<IProjectionQueryService, DaprProjectionQueryService>();
        services.TryAddScoped<IProjectionCommandService, DaprProjectionCommandService>();
        services.TryAddScoped<ITypeCatalogService, DaprTypeCatalogService>();
        services.TryAddScoped<IHealthQueryService, DaprHealthQueryService>();
        services.TryAddScoped<IStorageQueryService, DaprStorageQueryService>();
        services.TryAddScoped<IStorageCommandService, DaprStorageCommandService>();
        services.TryAddScoped<IDeadLetterQueryService, DaprDeadLetterQueryService>();
        services.TryAddScoped<IDeadLetterCommandService, DaprDeadLetterCommandService>();
        services.TryAddScoped<ITenantQueryService, DaprTenantQueryService>();

        return services;
    }
}
