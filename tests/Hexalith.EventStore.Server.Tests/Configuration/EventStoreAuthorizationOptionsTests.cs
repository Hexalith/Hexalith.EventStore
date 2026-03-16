
using Hexalith.EventStore.CommandApi.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class EventStoreAuthorizationOptionsTests {
    [Fact]
    public void DefaultValues_AreNull() {
        // Arrange & Act
        var options = new EventStoreAuthorizationOptions();

        // Assert
        options.TenantValidatorActorName.ShouldBeNull();
        options.RbacValidatorActorName.ShouldBeNull();
    }

    [Fact]
    public void Validation_BothNull_Succeeds() {
        // Arrange
        var options = new EventStoreAuthorizationOptions();
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_TenantValidatorActorName_NonNull_ValidString_Succeeds() {
        // Arrange
        var options = new EventStoreAuthorizationOptions {
            TenantValidatorActorName = "TenantValidatorActor",
        };
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_RbacValidatorActorName_NonNull_ValidString_Succeeds() {
        // Arrange
        var options = new EventStoreAuthorizationOptions {
            RbacValidatorActorName = "RbacValidatorActor",
        };
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_BothNonNull_ValidStrings_Succeeds() {
        // Arrange
        var options = new EventStoreAuthorizationOptions {
            TenantValidatorActorName = "TenantValidatorActor",
            RbacValidatorActorName = "RbacValidatorActor",
        };
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void Validation_TenantValidatorActorName_EmptyOrWhitespace_Fails(string value) {
        // Arrange
        var options = new EventStoreAuthorizationOptions {
            TenantValidatorActorName = value,
        };
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TenantValidatorActorName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void Validation_RbacValidatorActorName_EmptyOrWhitespace_Fails(string value) {
        // Arrange
        var options = new EventStoreAuthorizationOptions {
            RbacValidatorActorName = value,
        };
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RbacValidatorActorName");
    }

    [Fact]
    public void Validation_NullOptions_ThrowsArgumentNullException() {
        // Arrange
        var validator = new ValidateEventStoreAuthorizationOptions();

        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => validator.Validate(null, null!));
    }

}
