
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

public record SubmitQueryResponse(
    string CorrelationId,
    JsonElement Payload);
