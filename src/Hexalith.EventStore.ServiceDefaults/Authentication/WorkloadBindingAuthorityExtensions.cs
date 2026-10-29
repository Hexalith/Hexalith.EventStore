using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Explicitly enrolls a technical authority; ordinary workload registration never activates this surface.</summary>
public static class WorkloadBindingAuthorityExtensions
{
    /// <summary>Registers the default-disabled authority and current configuration.</summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The host's protected configuration.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEventStoreWorkloadBindingAuthority(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<WorkloadAuthorityOptions>().Bind(configuration.GetSection(WorkloadAuthorityOptions.SectionName))
            .PostConfigure(options => WorkloadAuthorityBindingConfiguration.Configure(options, configuration))
            .Validate<IServiceProvider>((options, provider) => !options.Enabled
                || provider.GetService<IHostEnvironment>()?.IsDevelopment() == true && options.NonconformingDevelopmentFixture
                    && !services.Any(static descriptor => descriptor.ServiceType == typeof(EventStoreHttpEndpointSource)),
                "The embedded authority requires an explicitly nonconforming Development fixture.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WorkloadBindingAuthority>();
        return services;
    }

    /// <summary>Maps HTTPS bootstrap only in an explicitly nonconforming Development fixture; conforming hosts refuse it.</summary>
    /// <param name="endpoints">The endpoint routes.</param>
    /// <param name="prefix">The path component of the configured issuer.</param>
    /// <returns>The endpoints.</returns>
    public static IEndpointRouteBuilder MapEventStoreWorkloadBindingAuthority(this IEndpointRouteBuilder endpoints,
        string prefix = "/workload-authority")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (!prefix.StartsWith('/') || prefix.EndsWith('/') || prefix.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("An absolute route prefix without trailing slash is required.", nameof(prefix));
        WorkloadAuthorityOptions options = endpoints.ServiceProvider.GetRequiredService<IOptions<WorkloadAuthorityOptions>>().Value;
        IHostEnvironment hostEnvironment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!hostEnvironment.IsDevelopment() || !options.NonconformingDevelopmentFixture
            || endpoints.ServiceProvider.GetService<EventStoreHttpEndpointSource>() is not null)
        {
            throw new InvalidOperationException("Embedded authority routes require an explicitly nonconforming Development fixture.");
        }
        endpoints.ServiceProvider.GetRequiredService<WorkloadBindingAuthority>().BindRoutePrefix(prefix);
        endpoints.MapGet(prefix + "/.well-known/openid-configuration", (WorkloadBindingAuthority authority) =>
            authority.GetMetadata() is { } metadata ? Results.Json(metadata) : Results.NotFound()).WithMetadata(WorkloadAuthorityEndpointMetadata.Discovery).AllowAnonymous();
        endpoints.MapGet(prefix + "/jwks", (WorkloadBindingAuthority authority) =>
            authority.GetPublicKeys() is { } keys ? Results.Json(keys) : Results.NotFound())
            .WithMetadata(WorkloadAuthorityEndpointMetadata.Keys).AllowAnonymous();
        endpoints.MapPost(prefix + "/token", async (HttpContext context, WorkloadBindingAuthority authority, IHostEnvironment environment) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            if (!context.Request.IsHttps || !context.Request.HasFormContentType
                || context.Request.ContentLength is null or > 16384)
                return Results.Json(new { error = "invalid_request" }, statusCode: 400);
            IFormCollection form;
            try { form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false); }
            catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
            { return Results.Json(new { error = "invalid_request" }, statusCode: 400); }
            string[] expected = ["grant_type", "client_id", "client_secret", "scope", "authorization_details"];
            if (form.Count != expected.Length || expected.Any(key => !form.TryGetValue(key, out StringValues value) || value.Count != 1)
                || form["grant_type"] != "client_credentials")
                return Results.Json(new { error = "invalid_request" }, statusCode: 400);
            WorkloadAuthorizationDetails? details = WorkloadAuthorizationDetails.Decode(form["authorization_details"].ToString());
            if (details is null) return Results.Json(new { error = "invalid_authorization_details" }, statusCode: 400);
            string? token = authority.Issue(form["client_id"].ToString(), form["client_secret"].ToString(), form["scope"].ToString(), details);
            return token is null ? Results.Json(new { error = "invalid_grant" }, statusCode: 401)
                : Results.Json(new { access_token = token, token_type = "Bearer", expires_in = Math.Max(0, (int)new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token).ValidTo.Subtract(DateTime.UtcNow).TotalSeconds) });
        }).WithMetadata(WorkloadAuthorityEndpointMetadata.Token).AllowAnonymous().DisableAntiforgery();
        return endpoints;
    }
}
