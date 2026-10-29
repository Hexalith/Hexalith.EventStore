using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Security;

/// <summary>Independently validates relayed human credentials before any operator effect.</summary>
internal static class OperationsHttpSecurityExtensions
{
    /// <summary>The policy prefix for the distinct human relay operations.</summary>
    internal const string PolicyPrefix = "EventStoreOperations:";

    /// <summary>Registers the shared human JWT contract and the channel-bound relay policies.</summary>
    internal static IServiceCollection AddOperationsHttpSecurity(this IServiceCollection services)
    {
        services.AddOptions<JwtBearerAuthenticationOptions>()
            .BindConfiguration(EventStoreWorkloadAuthenticationDefaults.JwtContractSection).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtBearerAuthenticationOptions>, ValidateJwtBearerAuthenticationOptions>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerAuthenticationOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddEventStoreHttpHostSecurity();
        services.AddAuthorization(options =>
        {
            foreach (string operation in new[] { "count", "list", "retry", "skip", "archive" })
            {
                string requiredRole = operation == "count" ? "Admin" : operation == "list" ? "ReadOnly" : "Operator";
                options.AddPolicy(PolicyPrefix + operation, new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .Combine(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy())
                    .RequireAssertion(context => context.Resource is HttpContext http
                        && IsAdmitted(http, requiredRole))
                    .Build());
            }
        });
        return services;
    }

    /// <summary>Rejects missing, duplicate, or conflicting action-body tenant scope before model binding and effects.</summary>
    internal static WebApplication UseOperationsHumanRelayScope(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var policies = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (HttpMethods.IsPost(context.Request.Method)
                && policies?.Any(static item => item.Policy?.StartsWith(PolicyPrefix, StringComparison.Ordinal) == true) == true)
            {
                if (context.Request.Query.ContainsKey("tenantId"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                // Allow every bounded identifier even at the JSON encoder's six-byte escaping expansion.
                int maxBytes = checked(1024 + context.RequestServices.GetRequiredService<IOptions<EventStoreOperationsOptions>>()
                    .Value.MaxActionItems * ((DeadLetterSafeIdentity.MaxValueLength * 6) + 3));
                if (context.Request.ContentLength > maxBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
                (byte[] body, bool tooLarge) = await Endpoints.DeadLetterOperationsEndpointExtensions
                    .ReadBoundedBodyAsync(context.Request, maxBytes).ConfigureAwait(false);
                if (tooLarge)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                using (document)
                {
                    JsonProperty[] scopes = document.RootElement.ValueKind == JsonValueKind.Object
                        ? [.. document.RootElement.EnumerateObject().Where(static property => string.Equals(property.Name, "tenantId", StringComparison.OrdinalIgnoreCase))]
                        : [];
                    if (scopes.Length != 1 || scopes[0].Value.ValueKind != JsonValueKind.String
                        || !CanAccessTenant(context.User, scopes[0].Value.GetString()))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }
                using var retainedBody = new MemoryStream(body, writable: false);
                Stream originalBody = context.Request.Body;
                context.Request.Body = retainedBody;
                try
                {
                    await next(context).ConfigureAwait(false);
                }
                finally
                {
                    context.Request.Body = originalBody;
                }
                return;
            }
            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    /// <summary>Authorizes one explicit canonical tenant without selecting scope from a caller's grants.</summary>
    internal static bool CanAccessTenant(ClaimsPrincipal principal, string? tenant)
        => IsCanonicalTenant(tenant)
            && (IsAdministrator(principal)
                || tenant != DeadLetterSafeIdentity.UnidentifiedTenantId && principal.HasClaim("eventstore:tenant", tenant!));

    /// <summary>Checks the independently validated principal's effective Admin role.</summary>
    internal static bool IsAdministrator(ClaimsPrincipal principal) => ResolveRole(principal) == "Admin";

    /// <summary>Validates canonical tenant syntax, including the reserved system scope.</summary>
    internal static bool IsCanonicalTenant(string? tenant)
        => tenant is { Length: > 0 and <= 64 } && tenant != "system"
            && tenant[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
            && tenant[^1] is >= 'a' and <= 'z' or >= '0' and <= '9'
            && tenant.All(static value => value is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static bool IsAdmitted(HttpContext context, string requiredRole)
    {
        if (!DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(context.Request,
            context.RequestServices.GetRequiredService<IConfiguration>(),
            context.RequestServices.GetRequiredService<IHostEnvironment>())))
        {
            return false;
        }

        // Sidecar caller attribution can reject a mismatch. It never creates identity or grants.
        var callers = context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName];
        string expected = context.RequestServices.GetRequiredService<IOptions<EventStoreOperationsOptions>>().Value.AdminCallerAppId;
        if (callers.Count > 1 || callers.Count == 1 && !string.Equals(callers[0], expected, StringComparison.Ordinal))
        {
            return false;
        }

        Claim[] subjects = [.. context.User.FindAll("sub")];
        if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value))
        {
            return false;
        }

        string? role = ResolveRole(context.User);
        return role == "Admin" || role == requiredRole || role == "Operator" && requiredRole == "ReadOnly";
    }

    // Preserve Admin.Server's mapping of signed human claims at this independent receiver boundary.
    private static string? ResolveRole(ClaimsPrincipal principal)
    {
        string[] explicitRoles = [.. principal.FindAll("eventstore:admin-role").Select(static claim => claim.Value)];
        if (explicitRoles.Length > 0)
        {
            return explicitRoles.Contains("Admin", StringComparer.Ordinal) ? "Admin"
                : explicitRoles.Contains("Operator", StringComparer.Ordinal) ? "Operator"
                : explicitRoles.Contains("ReadOnly", StringComparer.Ordinal) ? "ReadOnly" : null;
        }
        if (principal.Claims.Any(static claim => claim.Type is "global_admin" or "is_global_admin"
            && bool.TryParse(claim.Value, out bool admin) && admin
            || claim.Type is "role" or ClaimTypes.Role && IsAdminRole(claim.Value)
            || claim.Type == "roles" && ContainsAdminRole(claim.Value)))
        {
            return "Admin";
        }
        return principal.HasClaim("eventstore:permission", "command:replay") ? "Operator"
            : principal.HasClaim(static claim => claim.Type == "eventstore:tenant" && !string.IsNullOrWhiteSpace(claim.Value)) ? "ReadOnly" : null;
    }

    private static bool ContainsAdminRole(string value)
    {
        if (value.StartsWith('['))
        {
            try { return JsonSerializer.Deserialize<string[]>(value)?.Any(IsAdminRole) == true; }
            catch (JsonException) { return false; }
        }
        return value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Any(IsAdminRole);
    }

    private static bool IsAdminRole(string value) => string.Equals(value, "GlobalAdministrator", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "global-administrator", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "global-admin", StringComparison.OrdinalIgnoreCase);
}
