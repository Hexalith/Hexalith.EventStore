
using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// Extension methods for converting <see cref="SubmitCommand"/> to <see cref="CommandEnvelope"/>.
/// </summary>
public static class SubmitCommandExtensions {
    private const string TraceParentExtensionKey = "traceparent";
    private const string TraceStateExtensionKey = "tracestate";

    /// <summary>
    /// Converts a <see cref="SubmitCommand"/> to a <see cref="CommandEnvelope"/> for actor processing.
    /// </summary>
    /// <param name="command">The submit command to convert.</param>
    /// <returns>A command envelope with all fields mapped.</returns>
    public static CommandEnvelope ToCommandEnvelope(this SubmitCommand command) {
        ArgumentNullException.ThrowIfNull(command);

        Dictionary<string, string> extensions = command.Extensions is null
            ? [with(StringComparer.OrdinalIgnoreCase)]
            : new Dictionary<string, string>(command.Extensions, StringComparer.OrdinalIgnoreCase);

        if (Activity.Current is Activity current) {
            if (!string.IsNullOrWhiteSpace(current.Id)) {
                extensions[TraceParentExtensionKey] = current.Id;
            }

            if (!string.IsNullOrWhiteSpace(current.TraceStateString)) {
                extensions[TraceStateExtensionKey] = current.TraceStateString;
            }
        }

        return new CommandEnvelope(
            TenantId: command.Tenant,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            Payload: command.Payload,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId,
            UserId: command.UserId,
            Extensions: extensions.Count > 0 ? extensions : null);
    }
}
