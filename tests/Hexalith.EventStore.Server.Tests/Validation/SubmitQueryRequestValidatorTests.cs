
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Contracts.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Validation;

public class SubmitQueryRequestValidatorTests {
    private readonly SubmitQueryRequestValidator _validator = new();

    [Fact]
    public void SubmitQueryRequestValidator_ValidRequest_Passes() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            Payload: JsonDocument.Parse("{\"page\":1}").RootElement);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SubmitQueryRequestValidator_NullPayload_Passes() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            Payload: null);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SubmitQueryRequestValidator_UndefinedPayload_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            Payload: default(JsonElement));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Payload");
    }

    [Fact]
    public void SubmitQueryRequestValidator_EmptyTenant_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant");
    }

    [Fact]
    public void SubmitQueryRequestValidator_EmptyDomain_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Domain");
    }

    [Fact]
    public void SubmitQueryRequestValidator_EmptyAggregateId_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }

    [Fact]
    public void SubmitQueryRequestValidator_EmptyQueryType_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType");
    }

    [Fact]
    public void SubmitQueryRequestValidator_UppercaseTenant_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "INVALID",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("lowercase"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_TenantExceedsMaxLength_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: new string('a', 65),
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Tenant" && e.ErrorMessage.Contains("64"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_AggregateIdExceedsMaxLength_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: new string('a', 257),
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId" && e.ErrorMessage.Contains("256"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_QueryTypeExceedsMaxLength_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: new string('A', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("256"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_DangerousQueryType_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "<script>alert('xss')</script>");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("dangerous characters"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("onclick=alert(1)")]
    public void SubmitQueryRequestValidator_ScriptPatternInQueryType_FailsValidation(string queryType) {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: queryType);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("script injection patterns"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_EntityIdWithColon_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: "entity:bad");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityId" && e.ErrorMessage.Contains("colon"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_EntityIdExceedsMaxLength_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: new string('a', 257));

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityId" && e.ErrorMessage.Contains("256"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SubmitQueryRequestValidator_EmptyOrWhitespaceEntityId_FailsValidation(string entityId) {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: entityId);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityId" && e.ErrorMessage.Contains("empty or whitespace"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_EntityIdWithInvalidChars_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: "entity@bad!");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityId");
    }

    [Fact]
    public void SubmitQueryRequestValidator_NullEntityId_Passes() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: null);

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SubmitQueryRequestValidator_ValidEntityId_Passes() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "GetCurrentState",
            EntityId: "order-123");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void SubmitQueryRequestValidator_QueryTypeWithColon_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: "Get:Order");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("colon"));
    }

    [Fact]
    public void SubmitQueryRequestValidator_InvalidAggregateIdCharacters_FailsValidation() {
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg@001!",
            QueryType: "GetCurrentState");

        FluentValidation.Results.ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AggregateId");
    }
}
