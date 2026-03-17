
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Authentication;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class EventStoreClaimsTransformationTests {
    private const string TenantClaimType = "eventstore:tenant";
    private const string DomainClaimType = "eventstore:domain";
    private const string PermissionClaimType = "eventstore:permission";

    private readonly EventStoreClaimsTransformation _sut;

    public EventStoreClaimsTransformationTests() {
        ILogger<EventStoreClaimsTransformation> logger = Substitute.For<ILogger<EventStoreClaimsTransformation>>();
        _sut = new EventStoreClaimsTransformation(logger);
    }

    [Fact]
    public async Task TransformAsync_JwtWithTenantsArray_AddsEventStoreTenantClaims() {
        // Arrange
        string tenantsJson = JsonSerializer.Serialize(new[] { "tenant-a", "tenant-b" });
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tenants", tenantsJson),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(TenantClaimType).Select(c => c.Value)
            .ShouldBe(["tenant-a", "tenant-b"]);
    }

    [Fact]
    public async Task TransformAsync_JwtWithSingleTenantId_AddsEventStoreTenantClaim() {
        // Arrange
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-x"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(TenantClaimType).Select(c => c.Value)
            .ShouldBe(["tenant-x"]);
    }

    [Fact]
    public async Task TransformAsync_SubClaimWithoutNameIdentifier_AddsNameIdentifierClaim() {
        // Arrange
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("user-1");
    }

    [Fact]
    public async Task TransformAsync_ExistingNameIdentifier_DoesNotDuplicateClaim() {
        // Arrange
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.NameIdentifier).Count().ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_JwtWithDomainsAndPermissions_AddsNormalizedClaims() {
        // Arrange
        string domainsJson = JsonSerializer.Serialize(new[] { "orders", "inventory" });
        string permissionsJson = JsonSerializer.Serialize(new[] { "commands:*" });
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("domains", domainsJson),
            new Claim("permissions", permissionsJson),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(DomainClaimType).Select(c => c.Value)
            .ShouldBe(["orders", "inventory"]);
        result.FindAll(PermissionClaimType).Select(c => c.Value)
            .ShouldBe(["commands:*"]);
    }

    [Fact]
    public async Task TransformAsync_NoCustomClaims_AddsNoEventStoreClaims() {
        // Arrange
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(TenantClaimType).ShouldBeEmpty();
        result.FindAll(DomainClaimType).ShouldBeEmpty();
        result.FindAll(PermissionClaimType).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_AlreadyTransformed_DoesNotDuplicate() {
        // Arrange - pre-add eventstore:tenant claims to simulate already-transformed principal
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tenants", JsonSerializer.Serialize(new[] { "tenant-a" })),
        ], "Bearer");
        var existingIdentity = new ClaimsIdentity([
            new Claim(TenantClaimType, "tenant-a"),
        ]);
        var principal = new ClaimsPrincipal(identity);
        principal.AddIdentity(existingIdentity);

        // Act - run transformation again (should be idempotent)
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert - should still have exactly one tenant claim, not duplicated
        result.FindAll(TenantClaimType).Count().ShouldBe(1);
        result.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("user-1");
    }

    [Fact]
    public async Task TransformAsync_AlreadyTransformedDomainsOnly_DoesNotDuplicate() {
        // Arrange - JWT with domains but NO tenants, pre-add eventstore:domain to simulate prior transformation
        string domainsJson = JsonSerializer.Serialize(new[] { "orders" });
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("domains", domainsJson),
        ], "Bearer");
        var existingIdentity = new ClaimsIdentity([
            new Claim(DomainClaimType, "orders"),
        ]);
        var principal = new ClaimsPrincipal(identity);
        principal.AddIdentity(existingIdentity);

        // Act - run transformation again (should be idempotent even without tenant claims)
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert - should still have exactly one domain claim, not duplicated
        result.FindAll(DomainClaimType).Count().ShouldBe(1);
        result.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("user-1");
    }

    [Fact]
    public async Task TransformAsync_NullPrincipal_ThrowsArgumentNullException() =>
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.TransformAsync(null!));

    // --- Story 5.1 Gap-Closure Tests (5.3.6) ---

    [Fact]
    public async Task TransformAsync_TidFallbackClaim_AddsEventStoreTenantClaim() {
        // Arrange (5.3.6 — singular "tid" claim fallback when "tenants" is absent)
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tid", "tenant-from-tid"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(TenantClaimType).Select(c => c.Value)
            .ShouldBe(["tenant-from-tid"]);
    }

    [Fact]
    public async Task TransformAsync_TenantIdAndTidBothPresent_TenantIdTakesPrecedence() {
        // Arrange — tenant_id and tid both present; tenant_id takes precedence via ?? operator
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("tid", "tenant-b"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert — only tenant_id extracted (tid is fallback only)
        result.FindAll(TenantClaimType).Select(c => c.Value)
            .ShouldBe(["tenant-a"]);
    }

    [Fact]
    public async Task TransformAsync_SpaceDelimitedDomains_ParsesCorrectly() {
        // Arrange — space-delimited string format (not JSON array)
        var identity = new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("domains", "orders inventory shipping"),
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await _sut.TransformAsync(principal);

        // Assert
        result.FindAll(DomainClaimType).Select(c => c.Value)
            .ShouldBe(["orders", "inventory", "shipping"]);
    }
}
