using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Internal contract used by DI registration to initialize discovered projections
/// with optional infrastructure services after construction.
/// </summary>
internal interface IEventStoreProjection {
    IProjectionChangeNotifier? Notifier { get; set; }

    ILogger? Logger { get; set; }

    string? TenantId { get; set; }
}