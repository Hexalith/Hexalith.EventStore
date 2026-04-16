
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Validation;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Validation;

public class ProjectionChangedNotificationValidatorTests {
    private readonly ProjectionChangedNotificationValidator _validator = new();

    [Fact]
    public void ValidNotification_Passes() {
        var notification = new ProjectionChangedNotification("order-list", "acme");

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidNotificationWithEntityId_Passes() {
        var notification = new ProjectionChangedNotification("order-list", "acme", "order-123");

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyProjectionType_Fails(string? projectionType) {
        var notification = new ProjectionChangedNotification(projectionType!, "acme");

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ProjectionType");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyTenantId_Fails(string? tenantId) {
        var notification = new ProjectionChangedNotification("order-list", tenantId!);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TenantId");
    }

    [Theory]
    [InlineData("OrderList")]    // PascalCase not kebab
    [InlineData("-order-list")]  // Leading hyphen
    [InlineData("order-list-")] // Trailing hyphen
    [InlineData("ORDER-LIST")]  // Uppercase
    public void InvalidProjectionTypeFormat_Fails(string projectionType) {
        var notification = new ProjectionChangedNotification(projectionType, "acme");

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ProjectionType");
    }

    [Theory]
    [InlineData("ACME")]       // Uppercase
    [InlineData("-acme")]      // Leading hyphen
    [InlineData("acme-")]      // Trailing hyphen
    [InlineData("Acme Corp")]  // Space + uppercase
    public void InvalidTenantIdFormat_Fails(string tenantId) {
        var notification = new ProjectionChangedNotification("order-list", tenantId);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TenantId");
    }

    [Fact]
    public void ProjectionTypeExceeds64Chars_Fails() {
        string longName = new('a', 65);
        var notification = new ProjectionChangedNotification(longName, "acme");

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TenantIdExceeds64Chars_Fails() {
        string longTenant = new('a', 65);
        var notification = new ProjectionChangedNotification("order-list", longTenant);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
    }
}
