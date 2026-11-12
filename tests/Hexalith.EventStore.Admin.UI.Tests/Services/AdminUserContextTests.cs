using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

/// <summary>
/// Test 9.13: AdminUserContext extracts correct AdminRole from claims (AC: 11).
/// Merge-blocking test.
/// </summary>
public class AdminUserContextTests {
    [Theory]
    [InlineData("ReadOnly", "Admin", AdminRole.Admin)]
    [InlineData("Admin", "ReadOnly", AdminRole.Admin)]
    [InlineData("ReadOnly", "Operator", AdminRole.Operator)]
    [InlineData("Operator", "ReadOnly", AdminRole.Operator)]
    [InlineData("invalid", "Operator", AdminRole.Operator)]
    [InlineData("Admin", "invalid", AdminRole.Admin)]
    public async Task ExplicitRoles_UseHighestCanonicalValueRegardlessOfOrder(string first, string second, AdminRole expected) {
        var context = CreateContext(true,
            new Claim(AdminClaimTypes.Role, first),
            new Claim(AdminClaimTypes.Role, second));

        (await context.GetRoleAsync()).ShouldBe(expected);
        (await context.HasMinimumRoleAsync(expected)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Unknown")]
    [InlineData("")]
    public async Task InvalidExplicitRoles_BlockAllFallbackHints(string role) {
        var context = CreateContext(true,
            new Claim(AdminClaimTypes.Role, role),
            new Claim("global_admin", "true"),
            new Claim("eventstore:permission", "command:replay"),
            new Claim("eventstore:tenant", "tenant-a"));

        (await context.HasMinimumRoleAsync(AdminRole.ReadOnly)).ShouldBeFalse();
        (await context.HasMinimumRoleAsync(AdminRole.Admin)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("global_admin", "true", AdminRole.Admin, true)]
    [InlineData("roles", "[\"GlobalAdministrator\"]", AdminRole.Admin, true)]
    [InlineData("eventstore:permission", "command:replay", AdminRole.Operator, true)]
    [InlineData("eventstore:permission", "Command:Replay", AdminRole.ReadOnly, false)]
    [InlineData("eventstore:permission", "command:read", AdminRole.ReadOnly, false)]
    [InlineData("eventstore:tenant", "tenant-a", AdminRole.ReadOnly, true)]
    [InlineData("eventstore:tenant", " ", AdminRole.ReadOnly, false)]
    public async Task AbsentExplicitRoles_MapServerClaims(string claimType, string value, AdminRole expected, bool allowed) {
        var context = CreateContext(true, new Claim(claimType, value));

        (await context.GetRoleAsync()).ShouldBe(expected);
        (await context.HasMinimumRoleAsync(expected)).ShouldBe(allowed);
    }

    [Fact]
    public async Task FallbackRolePrecedence_UsesGlobalThenReplayThenTenant() {
        var context = CreateContext(true,
            new Claim("eventstore:tenant", "tenant-a"),
            new Claim("eventstore:permission", "command:replay"),
            new Claim("global_admin", "true"));
        (await context.GetRoleAsync()).ShouldBe(AdminRole.Admin);

        context = CreateContext(true,
            new Claim("eventstore:tenant", "tenant-a"),
            new Claim("eventstore:permission", "command:replay"));
        (await context.GetRoleAsync()).ShouldBe(AdminRole.Operator);
    }

    [Fact]
    public async Task AnonymousPrincipal_CannotUseExplicitOrMappedRoles() {
        var context = CreateContext(false,
            new Claim(AdminClaimTypes.Role, "Admin"),
            new Claim("global_admin", "true"),
            new Claim("eventstore:permission", "command:replay"),
            new Claim("eventstore:tenant", "tenant-a"));

        (await context.HasMinimumRoleAsync(AdminRole.ReadOnly)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticationChangeInvalidatesAnOlderPendingAdministratorRead(bool minimumRole) {
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new PendingAuthenticationStateProvider(pending.Task);
        var context = new AdminUserContext(provider);
        Task<bool> read = minimumRole ? context.HasMinimumRoleAsync(AdminRole.ReadOnly)
            : ReadAdministratorAsync(context);
        provider.NotifyCurrentState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AdminClaimTypes.Role, "Admin")], "TestAuth"))));
        (await read).ShouldBeFalse();
    }

    [Fact]
    public async Task CanceledPendingRoleReadDoesNotWaitForAnUnresponsiveProvider() {
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new PendingAuthenticationStateProvider(pending.Task);
        var context = new AdminUserContext(provider);
        using var cancellation = new CancellationTokenSource();
        Task<AdminRole> read = context.GetRoleAsync(cancellation.Token);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    private sealed class PendingAuthenticationStateProvider(Task<AuthenticationState> pending) : AuthenticationStateProvider {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => pending;

        public void NotifyCurrentState(Task<AuthenticationState> state) => NotifyAuthenticationStateChanged(state);
    }

    private static async Task<bool> ReadAdministratorAsync(AdminUserContext context)
        => await context.GetRoleAsync() == AdminRole.Admin;

    private static AdminUserContext CreateContext(bool authenticated, params Claim[] claims) {
        AuthenticationStateProvider provider = Substitute.For<AuthenticationStateProvider>();
        _ = provider.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "TestAuth" : null)))));
        return new AdminUserContext(provider);
    }

    [Theory]
    [InlineData("Admin", AdminRole.Admin)]
    [InlineData("Operator", AdminRole.Operator)]
    [InlineData("ReadOnly", AdminRole.ReadOnly)]
    public async Task GetRoleAsync_ExtractsCorrectRole(string claimValue, AdminRole expectedRole) {
        // Arrange
        AuthenticationStateProvider authProvider = CreateAuthProvider(claimValue);
        var context = new AdminUserContext(authProvider);

        // Act
        AdminRole role = await context.GetRoleAsync();

        // Assert
        role.ShouldBe(expectedRole);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("readonly")]
    [InlineData("Unknown")]
    public async Task HasMinimumRoleAsync_RejectsNonCanonicalRoleValues(string claimValue) {
        AuthenticationStateProvider authProvider = CreateAuthProvider(claimValue);
        var context = new AdminUserContext(authProvider);

        bool result = await context.HasMinimumRoleAsync(AdminRole.ReadOnly);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetRoleAsync_ReturnsReadOnly_WhenNoRoleClaim() {
        // Arrange
        AuthenticationStateProvider authProvider = Substitute.For<AuthenticationStateProvider>();
        ClaimsPrincipal user = new(new ClaimsIdentity([], "TestAuth"));
        _ = authProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(user)));
        var context = new AdminUserContext(authProvider);

        // Act
        AdminRole role = await context.GetRoleAsync();

        // Assert
        role.ShouldBe(AdminRole.ReadOnly);
    }

    [Theory]
    [InlineData("Admin", AdminRole.Admin, true)]
    [InlineData("Admin", AdminRole.Operator, true)]
    [InlineData("Admin", AdminRole.ReadOnly, true)]
    [InlineData("Operator", AdminRole.Admin, false)]
    [InlineData("Operator", AdminRole.Operator, true)]
    [InlineData("Operator", AdminRole.ReadOnly, true)]
    [InlineData("ReadOnly", AdminRole.Admin, false)]
    [InlineData("ReadOnly", AdminRole.Operator, false)]
    [InlineData("ReadOnly", AdminRole.ReadOnly, true)]
    public async Task HasMinimumRoleAsync_ReturnsCorrectResult(string claimValue, AdminRole minimumRole, bool expected) {
        // Arrange
        AuthenticationStateProvider authProvider = CreateAuthProvider(claimValue);
        var context = new AdminUserContext(authProvider);

        // Act
        bool result = await context.HasMinimumRoleAsync(minimumRole);

        // Assert
        result.ShouldBe(expected);
    }

    private static AuthenticationStateProvider CreateAuthProvider(string roleValue) {
        AuthenticationStateProvider authProvider = Substitute.For<AuthenticationStateProvider>();
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(AdminClaimTypes.Role, roleValue),
        ], "TestAuth"));
        _ = authProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(user)));
        return authProvider;
    }
}
