using System.Security.Claims;

using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class AuthorizedViewRoleChangeTests : AdminUITestContext {
    [Fact]
    public void AuthorizedView_RecomputesWhenAuthenticationStateChanges() {
        var authProvider = new MutableAuthStateProvider(AdminRole.Admin);
        _ = Services.RemoveAll<AuthenticationStateProvider>();
        _ = Services.RemoveAll<AdminUserContext>();
        _ = Services.AddSingleton<AuthenticationStateProvider>(authProvider);
        _ = Services.AddScoped<AdminUserContext>();

        IRenderedComponent<AuthorizedView> cut = Render<AuthorizedView>(parameters => parameters
            .Add(p => p.MinimumRole, AdminRole.Operator)
            .AddChildContent("Operator content"));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Operator content"), TimeSpan.FromSeconds(5));

        authProvider.SetRole(AdminRole.ReadOnly);

        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("Operator content"), TimeSpan.FromSeconds(5));

        authProvider.SetRole(AdminRole.Operator);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Operator content"), TimeSpan.FromSeconds(5));
    }

    private sealed class MutableAuthStateProvider(AdminRole initialRole) : AuthenticationStateProvider {
        private AdminRole _role = initialRole;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(CreatePrincipal(_role)));

        public void SetRole(AdminRole role) {
            _role = role;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private static ClaimsPrincipal CreatePrincipal(AdminRole role)
            => new(new ClaimsIdentity([new Claim(AdminClaimTypes.Role, role.ToString())], "TestAuth"));
    }
}
