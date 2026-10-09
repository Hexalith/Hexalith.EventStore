namespace Hexalith.EventStore.Client.Queries;
/// <summary>Names one exact authorized input without permitting physical-key or type selection.</summary>
/// <param name = "StoreName">The declared state-store identity.</param>
/// <param name = "LogicalKey">The exact pre-admitted logical key.</param>
/// <param name = "MappingId">The exact registered immutable input-value mapping.</param>
internal sealed record LogicalQueryReadPlanEntry(string StoreName, string LogicalKey, string MappingId);
