using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Registers and applies the common fail-closed HTTP host boundary.</summary>
public static class EventStoreHttpHostSecurityExtensions
{
    /// <summary>The policy for explicitly classified human endpoints.</summary>
    public const string HumanPolicy = "EventStoreHuman";

    /// <summary>Registers named scheme-bound policies, a denying fallback, and startup inventory validation.</summary>
    /// <param name="services">The host services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEventStoreHttpHostSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(EventStoreHttpEndpointSource)))
        {
            return services;
        }

        services.AddSingleton<EventStoreHttpEndpointSource>();
        services.AddAuthentication().AddEventStoreSidecarChannelScheme();
        services.AddEventStoreWorkloadPolicies(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, []);
        services.AddAuthorization();
        services.PostConfigure<AuthorizationOptions>(static options =>
        {
            options.AddPolicy(HumanPolicy, CreateHumanPolicy());
            options.DefaultPolicy = CreateHumanPolicy();
            options.FallbackPolicy = new AuthorizationPolicyBuilder(EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new EventStoreUnclassifiedEndpointRequirement())
                .Build();
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventStoreHttpHostSecurityStartupValidator>());
        return services;
    }

    /// <summary>Creates a bearer-only human policy that rejects workload and mixed credentials.</summary>
    /// <returns>The human policy.</returns>
    public static AuthorizationPolicy CreateHumanPolicy()
        => new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(static context => IsHuman(context.User)
                && (context.Resource is not HttpContext http
                    || !http.Request.Headers.ContainsKey(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName)
                        && http.Request.Headers.Authorization.Count <= 1))
            .Build();

    /// <summary>Tests the validated principal's credential kind without deriving authority from request headers.</summary>
    /// <param name="principal">The validated principal.</param>
    /// <returns>Whether it is an authenticated human principal.</returns>
    public static bool IsHuman(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.Identity?.IsAuthenticated == true
            && !principal.HasClaim(static claim => claim.Type is EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType
                or EventStoreWorkloadAuthenticationDefaults.ChannelClaimType
                or EventStoreWorkloadAuthenticationDefaults.OperationClaimType);
    }

    /// <summary>Captures the complete endpoint source and denies invalid late endpoints before downstream work.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseEventStoreHttpHostSecurity(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        EventStoreHttpEndpointSource source = app.Services.GetRequiredService<EventStoreHttpEndpointSource>();
        if (source.Endpoints is not null)
        {
            return app;
        }

        source.Endpoints = app;
        app.Use(async (context, next) =>
        {
            // Unmatched paths disclose no endpoint and run no handler. Middleware surfaces install their
            // own named-policy guard before this boundary (for example Swagger UI).
            if (context.GetEndpoint() is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
                && !EventStoreHttpEndpointInventory.IsProbe(context.Request.Path.Value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (context.GetEndpoint() is { } endpoint
                && (await EventStoreHttpEndpointInventory.ValidateAsync([endpoint], context.RequestServices).ConfigureAwait(false)).Count > 0)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    /// <summary>Applies the channel-only policy and removes framework anonymous metadata.</summary>
    /// <typeparam name="TBuilder">The convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The convention builder.</returns>
    public static TBuilder RequireEventStoreChannel<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        static void RemoveAnonymous(Microsoft.AspNetCore.Builder.EndpointBuilder endpoint)
        {
            for (int index = endpoint.Metadata.Count - 1; index >= 0; index--)
            {
                if (endpoint.Metadata[index] is IAllowAnonymous)
                {
                    endpoint.Metadata.RemoveAt(index);
                }
            }
        }
        try
        {
            builder.Finally(RemoveAnonymous);
        }
        catch (NotImplementedException)
        {
            // Dapr composite convention builders implement Add but not the optional Finally member.
            builder.Add(RemoveAnonymous);
        }
        return builder.RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);
    }

    /// <summary>Protects middleware-based human surfaces such as Swagger UI, including its static assets.</summary>
    /// <param name="app">The pipeline.</param>
    /// <param name="path">The exact path segment owned by the middleware.</param>
    /// <returns>The pipeline.</returns>
    public static IApplicationBuilder UseEventStoreHumanSurface(this IApplicationBuilder app, PathString path)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(path))
            {
                var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);
                var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
                if (!result.Succeeded || !(await authorization.AuthorizeAsync(result.Principal!, context, HumanPolicy).ConfigureAwait(false)).Succeeded)
                {
                    context.Response.StatusCode = result.Succeeded ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
                    return;
                }
                context.User = result.Principal!;
            }
            await next(context).ConfigureAwait(false);
        });
    }
}
