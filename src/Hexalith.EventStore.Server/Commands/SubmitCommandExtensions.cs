using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Extension methods for converting <see cref="SubmitCommand"/> to <see cref="CommandEnvelope"/>.
/// </summary>
public static class SubmitCommandExtensions {
    private const string _globalAdminExtensionKey = "actor:globalAdmin";
    private const string _traceParentExtensionKey = "traceparent";
    private const string _traceStateExtensionKey = "tracestate";

    /// <summary>
    /// Converts a <see cref="SubmitCommand"/> to a <see cref="CommandEnvelope"/> for actor processing.
    /// </summary>
    /// <param name="command">The submit command to convert.</param>
    /// <returns>A command envelope with all fields mapped.</returns>
    public static CommandEnvelope ToCommandEnvelope(this SubmitCommand command) {
        ArgumentNullException.ThrowIfNull(command);

#pragma warning disable IDE0028 // Simplify collection initialization
        Dictionary<string, string> extensions = command.Extensions is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(command.Extensions, StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization

        _ = extensions.Remove(_globalAdminExtensionKey);

        if (command.IsGlobalAdmin) {
            extensions[_globalAdminExtensionKey] = "true";
        }

        if (Activity.Current is Activity current) {
            if (!string.IsNullOrWhiteSpace(current.Id)) {
                extensions[_traceParentExtensionKey] = current.Id;
            }

            if (!string.IsNullOrWhiteSpace(current.TraceStateString)) {
                extensions[_traceStateExtensionKey] = current.TraceStateString;
            }
        }

        return new CommandEnvelope(
            MessageId: command.MessageId,
            TenantId: command.Tenant,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            Payload: command.Payload,
            CorrelationId: command.CorrelationId,
            CausationId: command.MessageId,
            UserId: command.UserId,
            Extensions: extensions.Count > 0 ? extensions : null);
    }
}
