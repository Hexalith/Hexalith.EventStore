
using System.Security.Claims;

using Hexalith.EventStore.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

/// <summary>
/// Unit tests for <see cref="ClaimsTenantValidator"/>.
/// These tests prove the extracted logic matches the inline code in CommandsController (lines 48-61).
/// </summary>
public class ClaimsTenantValidatorTests {
    private readonly ClaimsTenantValidator _validator = new();

    private static ClaimsPrincipal CreatePrincipal(params string[] tenants) {
        var claims = new List<Claim>();
        foreach (string t in tenants) {
            claims.Add(new Claim("eventstore:tenant", t));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task ValidateAsync_NoTenantClaims_ReturnsDenied() {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("No tenant authorization claims found. Access denied.");
    }

    [Fact]
    public async Task ValidateAsync_MatchingTenant_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal("test-tenant");

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
        result.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_WrongTenant_ReturnsDenied() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal("other-tenant");

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for tenant 'test-tenant'.");
    }

    [Fact]
    public async Task ValidateAsync_MultipleTenants_OneMatching_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal("tenant-a", "test-tenant", "tenant-c");

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_CaseSensitive_DifferentCase_ReturnsDenied() {
        // Arrange — tenant comparison is Ordinal (case-SENSITIVE)
        ClaimsPrincipal principal = CreatePrincipal("TEST-TENANT");

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhitespaceOnlyClaims_TreatedAsNoClaims() {
        // Arrange — whitespace-only claims are filtered out
        var claims = new List<Claim> {
            new("eventstore:tenant", " "),
            new("eventstore:tenant", ""),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("No tenant authorization claims found. Access denied.");
    }

    [Fact]
    public async Task ValidateAsync_WhitespacePaddedTenantClaim_Denied() {
        // Arrange
        var claims = new List<Claim> {
            new("eventstore:tenant", " test-tenant "),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for tenant 'test-tenant'.");
    }

    [Fact]
    public async Task ValidateAsync_NullUser_ThrowsArgumentNullException() =>
        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => _validator.ValidateAsync(null!, "test-tenant", CancellationToken.None));

    // --- Global admin bypass tests ---

    [Fact]
    public async Task ValidateAsync_GlobalAdmin_AllowsAnyTenant() {
        // Arrange — global admin with no tenant claims can access any tenant
        var claims = new List<Claim> { new("global_admin", "true") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "system", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_GlobalAdminWithIsGlobalAdmin_AllowsAnyTenant() {
        // Arrange — is_global_admin claim variant
        var claims = new List<Claim> { new("is_global_admin", "true") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "any-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_GlobalAdminFalse_DoesNotBypass() {
        // Arrange — global_admin claim set to false should not bypass
        var claims = new List<Claim> { new("global_admin", "false") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_GlobalAdminNonBoolean_DoesNotBypass() {
        // Arrange — non-boolean value should not bypass
        var claims = new List<Claim> { new("global_admin", "yes") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        TenantValidationResult result = await _validator.ValidateAsync(principal, "test-tenant", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
    }
}
