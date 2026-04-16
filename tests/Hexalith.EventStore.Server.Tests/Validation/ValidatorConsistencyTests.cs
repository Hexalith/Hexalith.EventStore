
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Validation;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Validation;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Validation;

public class ValidatorConsistencyTests {
    private readonly SubmitCommandRequestValidator _submitCommandValidator = new();
    private readonly SubmitQueryRequestValidator _submitQueryValidator = new();
    private readonly ValidateCommandRequestValidator _validateCommandValidator = new();
    private readonly ValidateQueryRequestValidator _validateQueryValidator = new();

    /// <summary>
    /// Proves that identical Tenant/Domain/AggregateId values produce identical
    /// pass/fail outcomes across all 4 validators — prevents regex drift.
    /// </summary>
    [Theory]
    [InlineData("test-tenant", "test-domain", "agg-001", true)]
    [InlineData("a", "b", "A1", true)]
    [InlineData("tenant-with-hyphens", "domain-99", "Order.123-v2_final", true)]
    [InlineData("UPPERCASE", "test-domain", "agg-001", false)]
    [InlineData("test-tenant", "UPPERCASE", "agg-001", false)]
    [InlineData("test-tenant", "test-domain", "agg@001!", false)]
    [InlineData("test-tenant", "test-domain", "", false)]
    [InlineData("", "test-domain", "agg-001", false)]
    [InlineData("test-tenant", "", "agg-001", false)]
    [InlineData("-invalid", "test-domain", "agg-001", false)]
    [InlineData("test-tenant", "test-domain", "-invalid", false)]
    public void IdentityFieldConsistency_AllValidatorsAgree(
        string tenant, string domain, string aggregateId, bool expectedValid) {
        // SubmitCommandRequest
        var submitCommand = new SubmitCommandRequest(
            MessageId: "msg-1",
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            CommandType: "ValidType",
            Payload: JsonDocument.Parse("{}").RootElement);
        bool submitCommandValid = !_submitCommandValidator.Validate(submitCommand).Errors
            .Any(e => e.PropertyName is "Tenant" or "Domain" or "AggregateId");

        // SubmitQueryRequest
        var submitQuery = new SubmitQueryRequest(
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            QueryType: "ValidType",
            EntityId: null);
        bool submitQueryValid = !_submitQueryValidator.Validate(submitQuery).Errors
            .Any(e => e.PropertyName is "Tenant" or "Domain" or "AggregateId");

        // ValidateCommandRequest (AggregateId provided)
        var validateCommand = new ValidateCommandRequest(
            Tenant: tenant,
            Domain: domain,
            CommandType: "ValidType",
            AggregateId: aggregateId);
        bool validateCommandValid = !_validateCommandValidator.Validate(validateCommand).Errors
            .Any(e => e.PropertyName is "Tenant" or "Domain" or "AggregateId");

        // ValidateQueryRequest (AggregateId provided)
        var validateQuery = new ValidateQueryRequest(
            Tenant: tenant,
            Domain: domain,
            QueryType: "ValidType",
            AggregateId: aggregateId);
        bool validateQueryValid = !_validateQueryValidator.Validate(validateQuery).Errors
            .Any(e => e.PropertyName is "Tenant" or "Domain" or "AggregateId");

        submitCommandValid.ShouldBe(expectedValid, $"SubmitCommandRequest identity fields: T={tenant}, D={domain}, A={aggregateId}");
        submitQueryValid.ShouldBe(expectedValid, $"SubmitQueryRequest identity fields: T={tenant}, D={domain}, A={aggregateId}");
        validateCommandValid.ShouldBe(expectedValid, $"ValidateCommandRequest identity fields: T={tenant}, D={domain}, A={aggregateId}");
        validateQueryValid.ShouldBe(expectedValid, $"ValidateQueryRequest identity fields: T={tenant}, D={domain}, A={aggregateId}");
    }

    [Fact]
    public void IdentityFieldConsistency_AllValidatorsRejectTenantLongerThanCanonicalLimit() => IdentityFieldConsistency_AllValidatorsAgree(new string('a', 65), "test-domain", "agg-001", false);

    [Fact]
    public void IdentityFieldConsistency_AllValidatorsRejectDomainLongerThanCanonicalLimit() => IdentityFieldConsistency_AllValidatorsAgree("test-tenant", new string('b', 65), "agg-001", false);

    /// <summary>
    /// Proves that identical strings used as CommandType and QueryType produce
    /// identical pass/fail outcomes — prevents injection-rule drift.
    /// </summary>
    [Theory]
    [InlineData("CreateOrder", true)]
    [InlineData("GetCurrentState", true)]
    [InlineData("My.Namespace.CommandType", true)]
    [InlineData("<script>alert('xss')</script>", false)]
    [InlineData("type&other", false)]
    [InlineData("type'injected", false)]
    [InlineData("type\"quoted", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("onclick=alert(1)", false)]
    public void TypeFieldConsistency_AllValidatorsAgree(string typeValue, bool expectedValid) {
        // SubmitCommandRequest (CommandType)
        var submitCommand = new SubmitCommandRequest(
            MessageId: "msg-2",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: typeValue,
            Payload: JsonDocument.Parse("{}").RootElement);
        bool submitCommandValid = !_submitCommandValidator.Validate(submitCommand).Errors
            .Any(e => e.PropertyName == "CommandType");

        // SubmitQueryRequest (QueryType)
        var submitQuery = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: typeValue,
            EntityId: null);
        bool submitQueryValid = !_submitQueryValidator.Validate(submitQuery).Errors
            .Any(e => e.PropertyName == "QueryType");

        // ValidateCommandRequest (CommandType)
        var validateCommand = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: typeValue);
        bool validateCommandValid = !_validateCommandValidator.Validate(validateCommand).Errors
            .Any(e => e.PropertyName == "CommandType");

        // ValidateQueryRequest (QueryType)
        var validateQuery = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: typeValue);
        bool validateQueryValid = !_validateQueryValidator.Validate(validateQuery).Errors
            .Any(e => e.PropertyName == "QueryType");

        submitCommandValid.ShouldBe(expectedValid, $"SubmitCommandRequest CommandType='{typeValue}'");
        submitQueryValid.ShouldBe(expectedValid, $"SubmitQueryRequest QueryType='{typeValue}'");
        validateCommandValid.ShouldBe(expectedValid, $"ValidateCommandRequest CommandType='{typeValue}'");
        validateQueryValid.ShouldBe(expectedValid, $"ValidateQueryRequest QueryType='{typeValue}'");
    }

    /// <summary>
    /// Max length consistency: identical over-length strings produce identical
    /// failures across all validators.
    /// </summary>
    [Fact]
    public void MaxLengthConsistency_TypeField_AllValidatorsReject() {
        string longType = new('A', 257);

        var submitCommand = new SubmitCommandRequest(
            MessageId: "msg-3",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: longType,
            Payload: JsonDocument.Parse("{}").RootElement);

        var submitQuery = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            QueryType: longType,
            EntityId: null);

        var validateCommand = new ValidateCommandRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            CommandType: longType);

        var validateQuery = new ValidateQueryRequest(
            Tenant: "test-tenant",
            Domain: "test-domain",
            QueryType: longType);

        _submitCommandValidator.Validate(submitCommand).Errors
            .ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("256"));
        _submitQueryValidator.Validate(submitQuery).Errors
            .ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("256"));
        _validateCommandValidator.Validate(validateCommand).Errors
            .ShouldContain(e => e.PropertyName == "CommandType" && e.ErrorMessage.Contains("256"));
        _validateQueryValidator.Validate(validateQuery).Errors
            .ShouldContain(e => e.PropertyName == "QueryType" && e.ErrorMessage.Contains("256"));
    }

    [Fact]
    public void SubmitCommandRequestValidator_ExtensionsUtf8ByteBudget_UsesActualUtf8Length() {
        var extensions = Enumerable.Range(0, 22)
            .ToDictionary(
                i => $"k{i}",
                _ => new string('€', 1000));

        var submitCommand = new SubmitCommandRequest(
            MessageId: "msg-utf8-budget",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "ValidType",
            Payload: JsonDocument.Parse("{}").RootElement,
            Extensions: extensions);

        _submitCommandValidator.Validate(submitCommand).Errors
            .ShouldContain(e => e.PropertyName == "Extensions" && e.ErrorMessage.Contains("64KB"));
    }
}
