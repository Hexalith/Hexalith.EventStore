namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Request to export a single stream.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Format">Export format: "JSON" or "CloudEvents".</param>
public record StreamExportRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    string Format = "JSON");
