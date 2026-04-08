
using System.Security.Claims;

using Hexalith.EventStore.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

/// <summary>
/// Unit tests for <see cref="ClaimsRbacValidator"/>.
/// These tests prove the extracted logic matches the inline code in AuthorizationBehavior (lines 46-84).
/// </summary>
public class ClaimsRbacValidatorTests {
    private readonly ClaimsRbacValidator _validator = new();

    private static ClaimsPrincipal CreatePrincipal(
        string[]? domains = null,
        string[]? permissions = null) {
        var claims = new List<Claim>();
        if (domains is not null) {
            foreach (string d in domains) {
                claims.Add(new Claim("eventstore:domain", d));
            }
        }

        if (permissions is not null) {
            foreach (string p in permissions) {
                claims.Add(new Claim("eventstore:permission", p));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    // --- Domain authorization tests ---

    [Fact]
    public async Task ValidateAsync_MatchingDomain_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(domains: ["test-domain"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_NoDomainClaims_ReturnsAllowed() {
        // Arrange — no domain claims means all domains authorized
        ClaimsPrincipal principal = CreatePrincipal();

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WrongDomain_ReturnsDenied() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(domains: ["other-domain"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason!.ShouldContain("domain");
    }

    [Fact]
    public async Task ValidateAsync_DomainComparison_IsCaseInsensitive() {
        // Arrange — domain comparison is OrdinalIgnoreCase
        ClaimsPrincipal principal = CreatePrincipal(domains: ["TEST-DOMAIN"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    // --- Permission authorization tests ---

    [Fact]
    public async Task ValidateAsync_MatchingPermission_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["CreateOrder"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_NoPermissionClaims_ReturnsAllowed() {
        // Arrange — no permission claims means all commands authorized
        ClaimsPrincipal principal = CreatePrincipal();

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WrongPermission_ReturnsDenied() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["OtherCommand"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason!.ShouldContain("command type");
    }

    [Fact]
    public async Task ValidateAsync_WrongPermissionForQuery_ReturnsDeniedWithQueryTypeReason() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["OtherQuery"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for query type 'GetOrder'.");
    }

    [Fact]
    public async Task ValidateAsync_WildcardPermission_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["commands:*"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "AnyCommandType", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_SubmitPermission_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["command:submit"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PermissionComparison_IsCaseInsensitive() {
        // Arrange — permission comparison is OrdinalIgnoreCase
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["CREATEORDER"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    // --- messageCategory contract tests ---

    [Theory]
    [InlineData("command")]
    [InlineData("query")]
    public async Task ValidateAsync_ExactMessageType_AllowsForBothCategories(string messageCategory) {
        // Arrange — exact messageType match works for both categories
        ClaimsPrincipal principal = CreatePrincipal(
            domains: ["test-domain"],
            permissions: ["CreateOrder"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", messageCategory, CancellationToken.None);

        // Assert — both categories accept exact messageType match
        result.IsAuthorized.ShouldBeTrue();
    }

    [Theory]
    [InlineData("command")]
    [InlineData("query")]
    public async Task ValidateAsync_DomainDenial_IdenticalForBothCategories(string messageCategory) {
        // Arrange — domain denial is independent of category
        ClaimsPrincipal principal = CreatePrincipal(
            domains: ["other-domain"],
            permissions: ["CreateOrder"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", messageCategory, CancellationToken.None);

        // Assert — both categories produce the same domain denial
        result.IsAuthorized.ShouldBeFalse();
        result.Reason!.ShouldContain("domain");
    }

    // --- Query-specific permission tests ---

    [Fact]
    public async Task ValidateAsync_QueryWildcardPermission_AllowsQueryAccess() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["queries:*"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_QueryReadPermission_AllowsQueryAccess() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["query:read"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_LegacyCommandQueryPermission_AllowsQueryAccess() {
        // Arrange — local Keycloak realm currently emits command:query for query access.
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["command:query"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_CommandWildcard_DoesNotAllowQueryAccess() {
        // Arrange — commands:* should NOT grant query access
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["commands:*"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for query type 'GetOrder'.");
    }

    [Fact]
    public async Task ValidateAsync_CommandSubmit_DoesNotAllowQueryAccess() {
        // Arrange — command:submit should NOT grant query access
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["command:submit"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "GetOrder", "query", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for query type 'GetOrder'.");
    }

    [Fact]
    public async Task ValidateAsync_QueryWildcard_DoesNotAllowCommandAccess() {
        // Arrange — queries:* should NOT grant command access
        ClaimsPrincipal principal = CreatePrincipal(permissions: ["queries:*"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for command type 'CreateOrder'.");
    }

    [Fact]
    public async Task ValidateAsync_NullUser_ThrowsArgumentNullException() =>
        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => _validator.ValidateAsync(null!, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None));

    // --- Combined domain + permission tests ---

    [Fact]
    public async Task ValidateAsync_MatchingDomainAndPermission_ReturnsAllowed() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(
            domains: ["test-domain"],
            permissions: ["CreateOrder"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_MatchingDomainButWrongPermission_ReturnsDenied() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(
            domains: ["test-domain"],
            permissions: ["OtherCommand"]);

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason!.ShouldContain("command type");
    }

    // --- Global admin bypass tests ---

    [Fact]
    public async Task ValidateAsync_GlobalAdmin_BypassesDomainCheck() {
        // Arrange — global admin with wrong domain claims still allowed
        var claims = new List<Claim> {
            new("global_admin", "true"),
            new("eventstore:domain", "other-domain"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_GlobalAdmin_BypassesPermissionCheck() {
        // Arrange — global admin with no matching permissions still allowed
        var claims = new List<Claim> {
            new("global_admin", "true"),
            new("eventstore:permission", "other-permission"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_GlobalAdminFalse_DoesNotBypass() {
        // Arrange — global_admin: false should not bypass
        var claims = new List<Claim> {
            new("global_admin", "false"),
            new("eventstore:domain", "other-domain"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        // Act
        RbacValidationResult result = await _validator.ValidateAsync(
            principal, "test-tenant", "test-domain", "CreateOrder", "command", CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
    }
}
