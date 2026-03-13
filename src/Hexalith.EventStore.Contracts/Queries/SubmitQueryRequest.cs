
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

public record SubmitQueryRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    JsonElement? Payload = null,
    string? EntityId = null);
