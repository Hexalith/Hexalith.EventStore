
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Validation;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandRequestValidatorTests {
    private readonly SubmitCommandRequestValidator _validator = new();

    [Fact]
    public void SubmitCommandRequestValidator_MissingTenant_ReturnsValidationError() {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant");
    }

    [Fact]
    public void SubmitCommandRequestValidator_InjectionCharacters_ReturnsValidationError() {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement,
            Extensions: new Dictionary<string, string> { ["key"] = "<script>alert('xss')</script>" });

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Extensions");
    }

    [Fact]
    public void SubmitCommandRequestValidator_ExtensionSizeLimits_ReturnsValidationError() {
        // Arrange - 50 entries * (6+1000) chars * 2 bytes > 64KB
        var extensions = new Dictionary<string, string>();
        for (int i = 0; i < 50; i++) {
            extensions[$"key{i:D3}"] = new string('x', 1000);
        }

        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement,
            Extensions: extensions);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Extensions" && e.ErrorMessage.Contains("64KB"));
    }

    [Fact]
    public void SubmitCommandRequestValidator_FieldLengthLimits_ReturnsValidationError() {
        // Arrange - tenant exceeds canonical AggregateIdentity max length (64 chars)
        var request = new SubmitCommandRequest(
            Tenant: new string('a', 65),
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("64"));
    }

    [Fact]
    public void SubmitCommandRequestValidator_InvalidTenantCharacters_ReturnsValidationError() {
        // Arrange - uppercase not allowed (AggregateIdentity pattern)
        var request = new SubmitCommandRequest(
            Tenant: "INVALID",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("lowercase"));
    }

    [Fact]
    public void SubmitCommandRequestValidator_ValidRequest_Passes() {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{\"amount\":100}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SubmitCommandRequestValidator_JavascriptInjection_ReturnsValidationError() {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement,
            Extensions: new Dictionary<string, string> { ["url"] = "javascript:alert(1)" });

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Extensions");
    }

    [Fact]
    public void SubmitCommandRequestValidator_AmpersandInExtensions_ReturnsValidationError() {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: JsonDocument.Parse("{}").RootElement,
            Extensions: new Dictionary<string, string> { ["key"] = "value&other" });

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void SubmitCommandRequestValidator_DangerousCommandType_ReturnsValidationError() {
        // Arrange - CommandType with injection characters (SEC-4)
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "<script>alert('xss')</script>",
            Payload: JsonDocument.Parse("{}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("dangerous characters"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("onclick=alert(1)")]
    public void SubmitCommandRequestValidator_ScriptPatternInCommandType_ReturnsValidationError(string commandType) {
        // Arrange
        var request = new SubmitCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: commandType,
            Payload: JsonDocument.Parse("{}").RootElement);

        // Act
        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("script injection patterns"));
    }
}
