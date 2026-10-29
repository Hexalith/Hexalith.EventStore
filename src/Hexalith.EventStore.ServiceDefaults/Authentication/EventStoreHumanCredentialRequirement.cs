using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Retains and enforces the human credential-kind boundary when policies are composed.</summary>
internal sealed class EventStoreHumanCredentialRequirement : AuthorizationHandler<EventStoreHumanCredentialRequirement>, IAuthorizationRequirement
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EventStoreHumanCredentialRequirement requirement)
    {
        if (EventStoreHttpHostSecurityExtensions.IsHuman(context.User)
            && (context.Resource is not HttpContext http
                || !http.Request.Headers.ContainsKey(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName)
                    && http.Request.Headers.Authorization.Count <= 1))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
