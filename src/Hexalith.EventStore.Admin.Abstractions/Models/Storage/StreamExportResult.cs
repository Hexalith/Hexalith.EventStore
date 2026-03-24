namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Result of a stream export operation.
/// </summary>
/// <param name="Success">Whether the export completed.</param>
/// <param name="TenantId">Source tenant.</param>
/// <param name="Domain">Source domain.</param>
/// <param name="AggregateId">Source aggregate.</param>
/// <param name="EventCount">Number of events exported.</param>
/// <param name="Content">Serialized export content.</param>
/// <param name="FileName">Suggested filename for download.</param>
/// <param name="ErrorMessage">Error details if failed.</param>
public record StreamExportResult(
    bool Success,
    string TenantId,
    string Domain,
    string AggregateId,
    long EventCount,
    string? Content,
    string? FileName,
    string? ErrorMessage);
