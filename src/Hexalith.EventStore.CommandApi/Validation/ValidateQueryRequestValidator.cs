
using System.Text.RegularExpressions;

using FluentValidation;

using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.CommandApi.Validation;

public partial class ValidateQueryRequestValidator : AbstractValidator<ValidateQueryRequest> {
    private const int MaxTenantDomainLength = 64;

    // Consistent with AggregateIdentity: lowercase alphanumeric + hyphens
    private static readonly Regex _tenantDomainRegex = TenantDomainPattern();

    // Consistent with AggregateIdentity: alphanumeric + dots/hyphens/underscores
    private static readonly Regex _aggregateIdRegex = AggregateIdPattern();

    // Dangerous patterns for injection prevention (SEC-4)
    private static readonly Regex _injectionPattern = InjectionPattern();

    public ValidateQueryRequestValidator() {
        _ = RuleFor(x => x.Tenant)
            .NotNull().WithMessage("Tenant is required")
            .NotEmpty().WithMessage("Tenant cannot be empty")
            .MaximumLength(MaxTenantDomainLength).WithMessage($"Tenant cannot exceed {MaxTenantDomainLength} characters")
            .Matches(_tenantDomainRegex).WithMessage("Tenant must contain only lowercase alphanumeric characters and hyphens")
            .When(x => !string.IsNullOrEmpty(x.Tenant), ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.Domain)
            .NotNull().WithMessage("Domain is required")
            .NotEmpty().WithMessage("Domain cannot be empty")
            .MaximumLength(MaxTenantDomainLength).WithMessage($"Domain cannot exceed {MaxTenantDomainLength} characters")
            .Matches(_tenantDomainRegex).WithMessage("Domain must contain only lowercase alphanumeric characters and hyphens")
            .When(x => !string.IsNullOrEmpty(x.Domain), ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.QueryType)
            .NotNull().WithMessage("QueryType is required")
            .NotEmpty().WithMessage("QueryType cannot be empty")
            .MaximumLength(256).WithMessage("QueryType cannot exceed 256 characters")
            .Must(qt => !qt.Contains(':'))
            .WithMessage("QueryType cannot contain colons (reserved as actor ID separator)")
            .When(x => !string.IsNullOrEmpty(x.QueryType), ApplyConditionTo.CurrentValidator)
            .Must(qt => !ContainsDangerousCharacters(qt) && !_injectionPattern.IsMatch(qt))
            .WithMessage("QueryType cannot contain dangerous characters or script injection patterns")
            .When(x => !string.IsNullOrEmpty(x.QueryType), ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.AggregateId)
            .NotEmpty().WithMessage("AggregateId cannot be empty")
            .MaximumLength(256).WithMessage("AggregateId cannot exceed 256 characters")
            .Matches(_aggregateIdRegex).WithMessage("AggregateId must contain only alphanumeric characters, dots, hyphens, and underscores")
            .When(x => x.AggregateId is not null, ApplyConditionTo.AllValidators);
    }

    private static readonly char[] _dangerousChars = ['<', '>', '&', '\'', '"'];

    private static bool ContainsDangerousCharacters(string value) =>
        value.AsSpan().IndexOfAny(_dangerousChars) >= 0;

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex TenantDomainPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex AggregateIdPattern();

    [GeneratedRegex(@"(?i)(javascript\s*:|on\w+\s*=|<\s*script)", RegexOptions.Compiled)]
    private static partial Regex InjectionPattern();

}
