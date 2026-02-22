
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class TenantValidatorTests {
    private readonly TenantValidator _validator = new(NullLogger<TenantValidator>.Instance);

    [Fact]
    public void Validate_MatchingTenant_DoesNotThrow() =>
        // Act & Assert -- should not throw
        _validator.Validate("tenant-a", "tenant-a:orders:order-42");

    [Fact]
    public void Validate_MismatchingTenant_ThrowsTenantMismatchException() =>
        // Act & Assert
        Should.Throw<TenantMismatchException>(
            () => _validator.Validate("tenant-b", "tenant-a:orders:order-42"));

    [Fact]
    public void Validate_MismatchException_ContainsCorrectTenants() {
        // Act
        TenantMismatchException ex = Should.Throw<TenantMismatchException>(
            () => _validator.Validate("tenant-b", "tenant-a:orders:order-42"));

        // Assert
        ex.CommandTenant.ShouldBe("tenant-b");
        ex.ActorTenant.ShouldBe("tenant-a");
    }

    [Fact]
    public void Validate_CaseSensitive_MismatchOnCase() =>
        // Act & Assert -- case-sensitive per AggregateIdentity validation rules
        Should.Throw<TenantMismatchException>(
            () => _validator.Validate("Tenant-A", "tenant-a:orders:order-42"));

    [Fact]
    public void Validate_NullCommandTenant_ThrowsArgumentException() => Should.Throw<ArgumentException>(
            () => _validator.Validate(null!, "tenant-a:orders:order-42"));

    [Fact]
    public void Validate_NullActorId_ThrowsArgumentException() => Should.Throw<ArgumentException>(
            () => _validator.Validate("tenant-a", null!));

    [Fact]
    public void Validate_EmptyCommandTenant_ThrowsArgumentException() => Should.Throw<ArgumentException>(
            () => _validator.Validate("", "tenant-a:orders:order-42"));

    [Fact]
    public void Validate_MalformedActorId_NoColons_ThrowsInvalidOperationException() =>
        // F-RT1, F-FM2: Actor ID must have exactly 3 colon-separated segments
        Should.Throw<InvalidOperationException>(
            () => _validator.Validate("tenant-a", "no-colons"));

    [Fact]
    public void Validate_MalformedActorId_OneColon_ThrowsInvalidOperationException() =>
        // Only 2 parts instead of 3
        Should.Throw<InvalidOperationException>(
            () => _validator.Validate("tenant-a", "tenant:domain"));

    [Fact]
    public void Validate_MalformedActorId_ExtraColons_ThrowsInvalidOperationException() =>
        // 4 parts instead of 3
        Should.Throw<InvalidOperationException>(
            () => _validator.Validate("tenant-a", "a:b:c:d"));
}
