using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Exact owned snapshot fragment, preserving the existing ContainerDigest/Value property order.</summary>
/// <param name="ContainerDigest">Authenticated complete skeleton digest.</param><param name="Value">Original protected entry value.</param>
internal sealed record SnapshotEntryFragment(string ContainerDigest, JsonNode? Value);
