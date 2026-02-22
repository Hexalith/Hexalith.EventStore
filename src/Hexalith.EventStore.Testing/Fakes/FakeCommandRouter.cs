namespace Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// Fake command router for integration testing that returns success without DAPR actor infrastructure.
/// </summary>
public class FakeCommandRouter : ICommandRouter {
    /// <summary>Gets or sets the fake actor to delegate to. If null, returns default success.</summary>
    public FakeAggregateActor? FakeActor { get; set; }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);

        if (FakeActor is not null) {
            // Let conversion exceptions propagate -- they indicate malformed commands
            // that should have been caught by validation (matches production behavior)
            Contracts.Commands.CommandEnvelope envelope = SubmitCommandExtensions.ToCommandEnvelope(command);

            // Actor exceptions should propagate (not caught)
            return FakeActor.ProcessCommandAsync(envelope);
        }

        return Task.FromResult(new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId));
    }
}
