namespace Hexalith.EventStore.CommandApi.Validation;

using FluentValidation;

using Hexalith.EventStore.CommandApi.Models;

public class SubmitCommandRequestValidator : AbstractValidator<SubmitCommandRequest>
{
    public SubmitCommandRequestValidator()
    {
        RuleFor(x => x.Tenant)
            .NotNull().WithMessage("Tenant is required")
            .NotEmpty().WithMessage("Tenant cannot be empty");

        RuleFor(x => x.Domain)
            .NotNull().WithMessage("Domain is required")
            .NotEmpty().WithMessage("Domain cannot be empty");

        RuleFor(x => x.AggregateId)
            .NotNull().WithMessage("AggregateId is required")
            .NotEmpty().WithMessage("AggregateId cannot be empty");

        RuleFor(x => x.CommandType)
            .NotNull().WithMessage("CommandType is required")
            .NotEmpty().WithMessage("CommandType cannot be empty");

        RuleFor(x => x.Payload)
            .Must(p => p.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            .WithMessage("Payload is required");

        RuleFor(x => x.Extensions)
            .Must(ext => ext == null || ext.Count <= 50)
            .WithMessage("Extensions dictionary cannot exceed 50 entries")
            .Must(ext => ext == null || ext.All(kvp => kvp.Key.Length <= 100 && kvp.Value.Length <= 1000))
            .WithMessage("Extension keys must be ≤100 chars and values ≤1000 chars")
            .Must(ext => ext == null || ext.All(kvp => !kvp.Key.Contains('<') && !kvp.Value.Contains('<')))
            .WithMessage("Extensions cannot contain < character (injection prevention)");
    }
}
