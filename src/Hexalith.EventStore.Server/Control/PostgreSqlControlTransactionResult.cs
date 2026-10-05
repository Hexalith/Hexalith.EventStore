namespace Hexalith.EventStore.Server.Control;

/// <summary>Only complete fresh addressed readback grants committed continuation.</summary>
internal enum PostgreSqlControlTransactionResult
{
    Committed,
    EvidenceHold,
}
