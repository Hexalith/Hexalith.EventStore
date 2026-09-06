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
