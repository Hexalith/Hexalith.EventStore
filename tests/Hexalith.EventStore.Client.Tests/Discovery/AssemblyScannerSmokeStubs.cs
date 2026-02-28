
using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Client.Tests.Discovery;

/// <summary>Public aggregate stub for smoke tests via <c>GetExportedTypes()</c>.</summary>
public sealed class SmokeTestAggregate : EventStoreAggregate<SmokeTestState> { }

/// <summary>State type for <see cref="SmokeTestAggregate"/>.</summary>
public sealed class SmokeTestState { }

/// <summary>Public projection stub for smoke tests via <c>GetExportedTypes()</c>.</summary>
public sealed class SmokeTestProjection : EventStoreProjection<SmokeTestReadModel> { }

/// <summary>Read model type for <see cref="SmokeTestProjection"/>.</summary>
public sealed class SmokeTestReadModel { }
