using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Operations.Configuration;
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

    /// <summary>Authorizes one explicit canonical tenant without selecting scope from a caller's grants.</summary>
    internal static bool CanAccessTenant(ClaimsPrincipal principal, string? tenant)
        => IsCanonicalTenant(tenant)
            && (ResolveRole(principal) == "Admin"
                || principal.HasClaim("eventstore:tenant", tenant!));

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
        string? explicitRole = principal.FindFirst("eventstore:admin-role")?.Value;
        if (explicitRole is "Admin" or "Operator" or "ReadOnly")
        {
            return explicitRole;
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
