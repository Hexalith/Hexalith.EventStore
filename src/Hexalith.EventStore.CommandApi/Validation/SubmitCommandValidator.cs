namespace Hexalith.EventStore.CommandApi.Validation;

using FluentValidation;

using Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// MediatR-level validator for <see cref="SubmitCommand"/>.
/// Provides defense-in-depth validation after the HTTP request DTO has been mapped.
/// </summary>
public class SubmitCommandValidator : AbstractValidator<SubmitCommand> {
    public SubmitCommandValidator() {
        RuleFor(x => x.Tenant).NotEmpty().WithMessage("Tenant is required");
        RuleFor(x => x.Domain).NotEmpty().WithMessage("Domain is required");
        RuleFor(x => x.AggregateId).NotEmpty().WithMessage("AggregateId is required");
        RuleFor(x => x.CommandType).NotEmpty().WithMessage("CommandType is required");
        RuleFor(x => x.Payload).NotNull().WithMessage("Payload is required").NotEmpty().WithMessage("Payload cannot be empty");
        RuleFor(x => x.CorrelationId).NotEmpty().WithMessage("CorrelationId is required");
    }
}
