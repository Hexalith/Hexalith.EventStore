namespace Hexalith.EventStore.Server.Events;

/// <summary>Recognizes fixed logical-read failures without forwarding diagnostic text or identifiers.</summary>
internal static class LogicalEventReadRejection
{
    /// <summary>Gets the additive support-safe code for an addressed logical-read refusal.</summary>
    internal const string ReasonCode = "logical-event-read-rejected";

    /// <summary>Recognizes a missing prefix or an allow-listed reader refusal.</summary>
    internal static bool IsRejection(Exception exception)
        => exception is MissingEventException
            || (exception is InvalidOperationException && IsRejection(exception.Message));

    /// <summary>Recognizes only fixed failure-code prefixes produced by the shared reader.</summary>
    internal static bool IsRejection(string? message)
    {
        if (message is null)
        {
            return false;
        }

        foreach (string code in new[]
        {
            "UnknownEventContract", "LogicalDigestMismatch", "CapabilityMismatch",
            "ReplayRestartRequired", "AddressMismatch", "SourceHeadChanged",
            "RollbackReaderCapabilityHold", "UpcasterContractViolation", "LegacyArrayLimit",
            "ScratchLimit", "ReadableLimit", "RawEnvelopeLimit", "MetadataLimit",
        })
        {
            if (message.StartsWith(code + ":", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return string.Equals(message, "The addressed logical prefix is incomplete.", StringComparison.Ordinal);
    }
}
