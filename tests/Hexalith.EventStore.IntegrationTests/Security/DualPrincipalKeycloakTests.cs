using System.Security.Claims;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Verifies the claims used by dual-principal queries after real OIDC bearer validation.</summary>
[Trait("Category", "E2E")]
public sealed class DualPrincipalKeycloakTests
{
    [Fact]
    public async Task KeycloakClaimsReachTheQueryIdentityThroughTheProductionBearerHandler()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var issuer = new PrivateKeycloakIssuer();
        await issuer.StartAsync(cancellationToken);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(Options.Create(new EventStoreAuthenticationOptions
        {
            Authority = issuer.Authority,
            Issuer = issuer.Authority,
            Audience = PrivateKeycloakIssuer.DirectClient,
            AllowedAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireHttpsMetadata = false,
        }));
        _ = services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        _ = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        await using ServiceProvider provider = services.BuildServiceProvider();

        // Both cases are issuer-signed. Only the private test client adds delegation claims.
        ClaimsPrincipal direct = await AuthenticateAsync(provider,
            await issuer.IssueAsync(PrivateKeycloakIssuer.DirectClient, cancellationToken));
        DualPrincipalIdentity directIdentity = DualPrincipalClaimsHelper.Extract(direct, direct.FindFirstValue("sub").ShouldNotBeNull());
        directIdentity.OriginalActorId.ShouldBe(issuer.Subject);
        directIdentity.AuthenticatedWorkloadId.ShouldBe(PrivateKeycloakIssuer.DirectClient);
        directIdentity.IsDelegated.ShouldBeFalse();
        directIdentity.DelegationId.ShouldBeNull();

        ClaimsPrincipal delegated = await AuthenticateAsync(provider,
            await issuer.IssueAsync(PrivateKeycloakIssuer.DelegatedClient, cancellationToken));
        delegated.FindFirstValue("client_id").ShouldBe("fixture-caller");
        delegated.FindFirstValue("azp").ShouldBe(PrivateKeycloakIssuer.DelegatedClient);
        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(delegated, delegated.FindFirstValue("sub").ShouldNotBeNull());
        identity.OriginalActorId.ShouldBe(issuer.Subject);
        identity.AuthenticatedWorkloadId.ShouldBe(PrivateKeycloakIssuer.DelegatedClient);
        identity.IsDelegated.ShouldBeTrue();
        identity.DelegationId.ShouldBe("fixture-delegate");
        identity.Scopes.ShouldNotBeNull().ShouldContain("profile");
        identity.Audience.ShouldNotBeNull().ShouldContain(PrivateKeycloakIssuer.DirectClient);
    }

    private static async Task<ClaimsPrincipal> AuthenticateAsync(ServiceProvider provider, string token)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = "Bearer " + token;
        AuthenticateResult result = await context.AuthenticateAsync();
        result.Succeeded.ShouldBeTrue(result.Failure?.GetType().Name);
        return result.Principal.ShouldNotBeNull();
    }
}
