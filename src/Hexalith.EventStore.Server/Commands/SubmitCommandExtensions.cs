namespace Hexalith.EventStore.Server.Commands;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// Extension methods for converting <see cref="SubmitCommand"/> to <see cref="CommandEnvelope"/>.
/// </summary>
public static class SubmitCommandExtensions
{
    /// <summary>
    /// Converts a <see cref="SubmitCommand"/> to a <see cref="CommandEnvelope"/> for actor processing.
    /// </summary>
    /// <param name="command">The submit command to convert.</param>
    /// <returns>A command envelope with all fields mapped.</returns>
    public static CommandEnvelope ToCommandEnvelope(this SubmitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandEnvelope(
            TenantId: command.Tenant,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            Payload: command.Payload,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId,
            UserId: command.UserId,
            Extensions: command.Extensions);
    }
}
