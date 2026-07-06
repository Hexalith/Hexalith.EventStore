
using System.Text.RegularExpressions;

using FluentValidation;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Validation;

public partial class SubmitQueryRequestValidator : AbstractValidator<SubmitQueryRequest> {
    private const int MaxTenantDomainLength = 64;

    // Consistent with AggregateIdentity: lowercase alphanumeric + hyphens
    private static readonly Regex _tenantDomainRegex = TenantDomainPattern();

    // Consistent with AggregateIdentity: alphanumeric + dots/hyphens/underscores
    private static readonly Regex _aggregateIdRegex = AggregateIdPattern();

    // Dangerous patterns for injection prevention (SEC-4)
    private static readonly Regex _injectionPattern = InjectionPattern();

    public SubmitQueryRequestValidator() {
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

        _ = RuleFor(x => x.AggregateId)
            .NotNull().WithMessage("AggregateId is required")
            .NotEmpty().WithMessage("AggregateId cannot be empty")
            .MaximumLength(256).WithMessage("AggregateId cannot exceed 256 characters")
            .Matches(_aggregateIdRegex).WithMessage("AggregateId must contain only alphanumeric characters, dots, hyphens, and underscores")
            .When(x => !string.IsNullOrEmpty(x.AggregateId), ApplyConditionTo.CurrentValidator);

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

        _ = RuleFor(x => x.ProjectionType)
            .Cascade(CascadeMode.Stop)
            .Must(pt => pt is null || !string.IsNullOrWhiteSpace(pt))
            .WithMessage("ProjectionType cannot be empty or whitespace when provided")
            .MaximumLength(MaxTenantDomainLength).WithMessage($"ProjectionType cannot exceed {MaxTenantDomainLength} characters")
            .Matches(_tenantDomainRegex).WithMessage("ProjectionType must contain only lowercase alphanumeric characters and hyphens")
            .When(x => x.ProjectionType is not null, ApplyConditionTo.CurrentValidator)
            .Must(pt => pt == null || !pt.Contains(':'))
            .WithMessage("ProjectionType cannot contain colons (reserved as actor ID separator)")
            .When(x => x.ProjectionType is not null, ApplyConditionTo.CurrentValidator)
            .Must(pt => pt == null || (!ContainsDangerousCharacters(pt) && !_injectionPattern.IsMatch(pt)))
            .WithMessage("ProjectionType cannot contain dangerous characters or script injection patterns")
            .When(x => x.ProjectionType is not null, ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.EntityId)
            .Cascade(CascadeMode.Stop)
            .Must(eid => eid is null || !string.IsNullOrWhiteSpace(eid))
            .WithMessage("EntityId cannot be empty or whitespace when provided")
            .Must(eid => eid == null || !eid.Contains(':'))
            .WithMessage("EntityId cannot contain colons (reserved as actor ID separator)")
            .MaximumLength(256).WithMessage("EntityId cannot exceed 256 characters")
            .Matches(_aggregateIdRegex).WithMessage("EntityId must contain only alphanumeric characters, dots, hyphens, and underscores")
            .When(x => x.EntityId is not null, ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.ProjectionActorType)
            .Cascade(CascadeMode.Stop)
            .Must(pat => pat is null || !string.IsNullOrWhiteSpace(pat))
            .WithMessage("ProjectionActorType cannot be empty or whitespace when provided")
            .Must(pat => pat == null || !pat.Contains(':'))
            .WithMessage("ProjectionActorType cannot contain colons (reserved as actor ID separator)")
            .MaximumLength(MaxTenantDomainLength).WithMessage($"ProjectionActorType cannot exceed {MaxTenantDomainLength} characters")
            .Must(pat => pat == null || (!ContainsDangerousCharacters(pat) && !_injectionPattern.IsMatch(pat)))
            .WithMessage("ProjectionActorType cannot contain dangerous characters or script injection patterns")
            .When(x => x.ProjectionActorType is not null, ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.Payload)
            .Must(p => p == null || p.Value.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            .WithMessage("Payload must be a valid JSON element when provided");

        _ = RuleFor(x => x.Paging)
            .Must(p => p is null || string.IsNullOrWhiteSpace(p.Cursor) || p.Offset is null)
            .WithMessage("Cursor and offset paging cannot be used together.")
            .WithErrorCode(QueryProblemReasonCodes.InvalidPage);

        _ = RuleFor(x => x.Paging!.PageSize)
            .GreaterThan(0)
            .WithMessage("PageSize must be greater than zero.")
            .WithErrorCode(QueryProblemReasonCodes.InvalidPage)
            .LessThanOrEqualTo(QueryPolicyLimits.MaxPageSize)
            .WithMessage($"PageSize cannot exceed {QueryPolicyLimits.MaxPageSize}.")
            .WithErrorCode(QueryProblemReasonCodes.InvalidPage)
            .When(x => x.Paging?.PageSize is not null);

        _ = RuleFor(x => x.Paging!.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Offset must be greater than or equal to zero.")
            .WithErrorCode(QueryProblemReasonCodes.InvalidPage)
            .When(x => x.Paging?.Offset is not null);

        _ = RuleFor(x => x.Search)
            .Must(string.IsNullOrWhiteSpace)
            .WithMessage("Search policy fields are reserved; use the query payload for domain-specific search parameters.")
            .WithErrorCode(QueryProblemReasonCodes.UnsupportedSearch)
            .When(x => x.Search is not null);

        _ = RuleFor(x => x.Filters)
            .Must(filters => filters is null || filters.Count == 0)
            .WithMessage("Filter policy fields are reserved and are not supported by this endpoint yet.")
            .WithErrorCode(QueryProblemReasonCodes.UnsupportedFilter);

        _ = RuleFor(x => x.OrderBy)
            .Must(orderBy => orderBy is null || orderBy.Count == 0)
            .WithMessage("Order policy fields are reserved and are not supported by this endpoint yet.")
            .WithErrorCode(QueryProblemReasonCodes.UnsupportedOrder);

        _ = RuleFor(x => x.Freshness!.MaxStaleness)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .WithMessage("MaxStaleness must be greater than or equal to zero.")
            .WithErrorCode(QueryProblemReasonCodes.MalformedRequest)
            .When(x => x.Freshness?.MaxStaleness is not null);

        _ = RuleFor(x => x.AdditionalProperties)
            .Must(properties => properties is null || properties.Count == 0)
            .WithMessage("Unknown query policy fields are not supported.")
            .WithErrorCode(QueryProblemReasonCodes.MalformedRequest);
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
