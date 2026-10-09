using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Supplies the exact source-generated snapshot metadata registered by Story 8.4 snapshot-routing tests.
/// </summary>
[JsonSerializable(typeof(PartySnapshotState))]
[JsonSerializable(typeof(string))]
internal sealed partial class CompatibilityTestJsonContext : JsonSerializerContext;
