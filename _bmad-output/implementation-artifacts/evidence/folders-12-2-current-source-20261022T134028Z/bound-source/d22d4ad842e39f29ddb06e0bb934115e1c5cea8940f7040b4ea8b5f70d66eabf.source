using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Validates the private replay header before any preparation or persisted manifest access.</summary>
internal static class DomainProjectionReplayContext
{
    internal static ProjectionDispatchRequest Bind(ProjectionDispatchRequest request, IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(ProjectionReplayTransportContext.HeaderName, out var header)) return request;
        try
        {
            if (header.Count != 1 || string.IsNullOrEmpty(header[0])) throw new JsonException();
            return ProjectionReplayTransportContext.Bind(request, header[0]);
        }
        catch (JsonException) { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability); }
    }
}
