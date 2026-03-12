
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Contracts.Validation;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Validation;

public class ValidateQueryRequestValidatorTests {
    private readonly ValidateQueryRequestValidator _validator = new();

    [Fact]
    public void ValidateQueryRequestValidator_ValidRequest_Passes() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateQueryRequestValidator_ValidRequestWithAggregateId_Passes() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState",
            AggregateId: "order-123");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateQueryRequestValidator_NullAggregateId_Passes() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState",
            AggregateId: null);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateQueryRequestValidator_EmptyStringAggregateId_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState",
            AggregateId: "");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }

    [Fact]
    public void ValidateQueryRequestValidator_EmptyTenant_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "",
            Domain: "test-domain",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant");
    }

    [Fact]
    public void ValidateQueryRequestValidator_EmptyDomain_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Domain");
    }

    [Fact]
    public void ValidateQueryRequestValidator_EmptyQueryType_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType");
    }

    [Fact]
    public void ValidateQueryRequestValidator_UppercaseTenant_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "INVALID",
            Domain: "test-domain",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("lowercase"));
    }

    [Fact]
    public void ValidateQueryRequestValidator_TenantExceedsMaxLength_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: new string('a', 65),
            Domain: "test-domain",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("64"));
    }

    [Fact]
    public void ValidateQueryRequestValidator_QueryTypeExceedsMaxLength_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: new string('A', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("256"));
    }

    [Fact]
    public void ValidateQueryRequestValidator_DangerousQueryType_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "<script>alert('xss')</script>");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("dangerous characters"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("onclick=alert(1)")]
    public void ValidateQueryRequestValidator_ScriptPatternInQueryType_FailsValidation(string queryType) {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: queryType);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("script injection patterns"));
    }

    [Fact]
    public void ValidateQueryRequestValidator_InvalidAggregateIdCharacters_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState",
            AggregateId: "agg@001!");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }

    [Fact]
    public void ValidateQueryRequestValidator_AggregateIdExceedsMaxLength_FailsValidation() {
        var request = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: "GetCurrentState",
            AggregateId: new string('a', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId" && e.ErrorMessage.Contains("256"));
    }
}
