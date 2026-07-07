
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Validation;

using Microsoft.Extensions.Options;

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

    [Fact]
    public void ValidNotificationWithScopeAndMetadata_Passes() {
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order-123",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            });

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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("order:123")]
    public void InvalidGroupScope_Fails(string groupScope) {
        var notification = new ProjectionChangedNotification("order-list", "acme", GroupScope: groupScope);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "GroupScope");
    }

    [Fact]
    public void GroupScopeExceeds64Chars_Fails() {
        string longScope = new('a', 65);
        var notification = new ProjectionChangedNotification("order-list", "acme", GroupScope: longScope);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "GroupScope");
    }

    [Fact]
    public void MetadataExceedsEntryLimit_Fails() {
        Dictionary<string, string> metadata = Enumerable
            .Range(0, 17)
            .ToDictionary(i => $"k{i}", i => "v", StringComparer.Ordinal);
        var notification = new ProjectionChangedNotification("order-list", "acme", Metadata: metadata);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Metadata");
    }

    [Fact]
    public void MetadataEntryLimit_UsesConfiguredProjectionChangeNotifierOptions() {
        var validator = new ProjectionChangedNotificationValidator(Options.Create(new ProjectionChangeNotifierOptions {
            MaxDetailMetadataEntries = 1,
            MaxDetailMetadataBytes = 2_048,
        }));
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["a"] = "1",
            ["b"] = "2",
        };
        var notification = new ProjectionChangedNotification("order-list", "acme", Metadata: metadata);

        FluentValidation.Results.ValidationResult result = validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Metadata");
    }

    [Fact]
    public void MetadataExceedsByteLimit_Fails() {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["too-large"] = new('x', 2_048),
        };
        var notification = new ProjectionChangedNotification("order-list", "acme", Metadata: metadata);

        FluentValidation.Results.ValidationResult result = _validator.Validate(notification);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Metadata");
    }
}
