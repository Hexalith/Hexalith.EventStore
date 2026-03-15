
using System.Text;
using System.Text.RegularExpressions;

using FluentValidation;

using Hexalith.EventStore.CommandApi.Models;

namespace Hexalith.EventStore.CommandApi.Validation;

/// <summary>
/// Validates <see cref="SubmitCommandRequest"/> payloads for structural integrity and
/// security constraints before command processing.
/// </summary>
public partial class SubmitCommandRequestValidator : AbstractValidator<SubmitCommandRequest> {
    private const int MaxTenantDomainLength = 64;
    private const int MaxExtensionEntries = 50;
    private const int MaxExtensionKeyLength = 100;
    private const int MaxExtensionValueLength = 1000;
    private const int MaxTotalExtensionBytes = 65_536; // 64KB

    // Consistent with AggregateIdentity: lowercase alphanumeric + hyphens
    private static readonly Regex _tenantDomainRegex = TenantDomainPattern();

    // Consistent with AggregateIdentity: alphanumeric + dots/hyphens/underscores
    private static readonly Regex _aggregateIdRegex = AggregateIdPattern();

    // Dangerous patterns for injection prevention (SEC-4)
    private static readonly Regex _injectionPattern = InjectionPattern();

    public SubmitCommandRequestValidator() {
        _ = RuleFor(x => x.MessageId)
            .NotNull().WithMessage("MessageId is required")
            .NotEmpty().WithMessage("MessageId cannot be empty");

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

        _ = RuleFor(x => x.CommandType)
            .NotNull().WithMessage("CommandType is required")
            .NotEmpty().WithMessage("CommandType cannot be empty")
            .MaximumLength(256).WithMessage("CommandType cannot exceed 256 characters")
            .Must(ct => !ContainsDangerousCharacters(ct) && !_injectionPattern.IsMatch(ct))
            .WithMessage("CommandType cannot contain dangerous characters or script injection patterns")
            .When(x => !string.IsNullOrEmpty(x.CommandType), ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.Payload)
            .Must(p => p.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            .WithMessage("Payload is required");

        _ = RuleFor(x => x.Extensions)
            .Must(ext => ext == null || ext.Count <= MaxExtensionEntries)
            .WithMessage($"Extensions dictionary cannot exceed {MaxExtensionEntries} entries")
            .Must(ext => ext == null || ext.All(kvp => kvp.Key.Length <= MaxExtensionKeyLength && kvp.Value.Length <= MaxExtensionValueLength))
            .WithMessage($"Extension keys must be ≤{MaxExtensionKeyLength} chars and values ≤{MaxExtensionValueLength} chars")
            .Must(ext => ext == null || ext.Sum(static kvp => Encoding.UTF8.GetByteCount(kvp.Key) + Encoding.UTF8.GetByteCount(kvp.Value)) <= MaxTotalExtensionBytes)
            .WithMessage($"Total extension size cannot exceed {MaxTotalExtensionBytes / 1024}KB")
            .Must(ext => ext == null || !ext.Any(kvp => ContainsDangerousCharacters(kvp.Key) || ContainsDangerousCharacters(kvp.Value)))
            .WithMessage("Extensions cannot contain dangerous characters (<, >, &, ', \") for injection prevention")
            .Must(ext => ext == null || !ext.Any(kvp => _injectionPattern.IsMatch(kvp.Value) || _injectionPattern.IsMatch(kvp.Key)))
            .WithMessage("Extensions cannot contain script or HTML injection patterns");
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
