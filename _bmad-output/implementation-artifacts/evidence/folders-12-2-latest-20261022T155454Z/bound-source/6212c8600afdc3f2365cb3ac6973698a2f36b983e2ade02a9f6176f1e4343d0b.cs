using FluentValidation;

using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulCommandValidator : AbstractValidator<SubmitCommand>
{
    public StatefulCommandValidator(ProviderStateCoordinator coordinator)
    {
        RuleFor(command => command.CommandType)
            .Must(_ => !string.Equals(
                SupportedProviderStates.RequireActive(coordinator),
                "command-validation-failure",
                StringComparison.Ordinal))
            .WithMessage("Synthetic validation failure.");
    }
}
