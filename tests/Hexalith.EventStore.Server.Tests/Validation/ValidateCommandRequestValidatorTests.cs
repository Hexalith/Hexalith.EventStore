
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Contracts.Validation;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Validation;

public class ValidateCommandRequestValidatorTests {
    private readonly ValidateCommandRequestValidator _validator = new();

    [Fact]
    public void ValidateCommandRequestValidator_ValidRequest_Passes() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommandRequestValidator_ValidRequestWithAggregateId_Passes() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder",
            AggregateId: "order-123");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommandRequestValidator_NullAggregateId_Passes() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder",
            AggregateId: null);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommandRequestValidator_EmptyStringAggregateId_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder",
            AggregateId: "");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }

    [Fact]
    public void ValidateCommandRequestValidator_EmptyTenant_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "",
            Domain: "test-domain",
            CommandType: "CreateOrder");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant");
    }

    [Fact]
    public void ValidateCommandRequestValidator_EmptyDomain_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "",
            CommandType: "CreateOrder");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Domain");
    }

    [Fact]
    public void ValidateCommandRequestValidator_EmptyCommandType_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType");
    }

    [Fact]
    public void ValidateCommandRequestValidator_UppercaseTenant_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "INVALID",
            Domain: "test-domain",
            CommandType: "CreateOrder");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("lowercase"));
    }

    [Fact]
    public void ValidateCommandRequestValidator_TenantExceedsMaxLength_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: new string('a', 65),
            Domain: "test-domain",
            CommandType: "CreateOrder");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("64"));
    }

    [Fact]
    public void ValidateCommandRequestValidator_CommandTypeExceedsMaxLength_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: new string('A', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("256"));
    }

    [Fact]
    public void ValidateCommandRequestValidator_DangerousCommandType_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "<script>alert('xss')</script>");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("dangerous characters"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("onclick=alert(1)")]
    public void ValidateCommandRequestValidator_ScriptPatternInCommandType_FailsValidation(string commandType) {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: commandType);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("script injection patterns"));
    }

    [Fact]
    public void ValidateCommandRequestValidator_InvalidAggregateIdCharacters_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder",
            AggregateId: "agg@001!");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }

    [Fact]
    public void ValidateCommandRequestValidator_AggregateIdExceedsMaxLength_FailsValidation() {
        var request = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: "CreateOrder",
            AggregateId: new string('a', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId" && e.ErrorMessage.Contains("256"));
    }
}
