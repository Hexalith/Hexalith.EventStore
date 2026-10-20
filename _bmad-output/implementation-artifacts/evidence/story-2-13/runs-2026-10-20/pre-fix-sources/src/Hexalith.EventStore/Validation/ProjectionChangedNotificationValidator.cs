
using System.Text;
using System.Text.RegularExpressions;

using FluentValidation;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Validation;

public partial class ProjectionChangedNotificationValidator : AbstractValidator<ProjectionChangedNotification> {
    private const int MaxLength = 64;

    private static readonly Regex _kebabCaseRegex = KebabCasePattern();
    private readonly ProjectionChangeNotifierOptions _options;

    public ProjectionChangedNotificationValidator(IOptions<ProjectionChangeNotifierOptions>? options = null) {
        _options = options?.Value ?? new ProjectionChangeNotifierOptions();

        _ = RuleFor(x => x.ProjectionType)
            .NotNull().WithMessage("ProjectionType is required")
            .NotEmpty().WithMessage("ProjectionType cannot be empty")
            .MaximumLength(MaxLength).WithMessage($"ProjectionType cannot exceed {MaxLength} characters")
            .Matches(_kebabCaseRegex).WithMessage("ProjectionType must be kebab-case (lowercase alphanumeric and hyphens)")
            .When(x => !string.IsNullOrEmpty(x.ProjectionType), ApplyConditionTo.CurrentValidator);

        _ = RuleFor(x => x.TenantId)
            .NotNull().WithMessage("TenantId is required")
            .NotEmpty().WithMessage("TenantId cannot be empty")
            .MaximumLength(MaxLength).WithMessage($"TenantId cannot exceed {MaxLength} characters")
            .Matches(_kebabCaseRegex).WithMessage("TenantId must be kebab-case (lowercase alphanumeric and hyphens)")
            .When(x => !string.IsNullOrEmpty(x.TenantId), ApplyConditionTo.CurrentValidator);

        When(x => x.GroupScope is not null, () => {
            _ = RuleFor(x => x.GroupScope)
                .NotEmpty().WithMessage("GroupScope cannot be empty when specified")
                .MaximumLength(MaxLength).WithMessage($"GroupScope cannot exceed {MaxLength} characters")
                .Must(x => x is null || !x.Contains(':', StringComparison.Ordinal))
                .WithMessage("GroupScope must not contain colons");
        });

        _ = RuleFor(x => x.Metadata)
            .Must(HaveValidMetadataEntries).WithMessage("Metadata keys and values must be non-null")
            .Must(HaveMetadataWithinEntryLimit).WithMessage($"Metadata cannot exceed {_options.MaxDetailMetadataEntries} entries")
            .Must(HaveMetadataWithinByteLimit).WithMessage($"Metadata cannot exceed {_options.MaxDetailMetadataBytes} UTF-8 bytes");
    }

    private static bool HaveValidMetadataEntries(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null
            || metadata.All(entry => entry.Key is not null && entry.Value is not null);

    private bool HaveMetadataWithinEntryLimit(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null
            || metadata.Count <= _options.MaxDetailMetadataEntries;

    private bool HaveMetadataWithinByteLimit(IReadOnlyDictionary<string, string>? metadata) {
        if (metadata is null) {
            return true;
        }

        int totalBytes = 0;
        foreach (KeyValuePair<string, string> entry in metadata) {
            if (entry.Key is null || entry.Value is null) {
                return false;
            }

            totalBytes += Encoding.UTF8.GetByteCount(entry.Key) + Encoding.UTF8.GetByteCount(entry.Value);
            if (totalBytes > _options.MaxDetailMetadataBytes) {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex KebabCasePattern();
}
