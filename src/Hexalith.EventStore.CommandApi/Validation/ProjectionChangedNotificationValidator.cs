
using System.Text.RegularExpressions;

using FluentValidation;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.CommandApi.Validation;

public partial class ProjectionChangedNotificationValidator : AbstractValidator<ProjectionChangedNotification> {
    private const int MaxLength = 64;

    private static readonly Regex _kebabCaseRegex = KebabCasePattern();

    public ProjectionChangedNotificationValidator() {
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
    }

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex KebabCasePattern();
}
