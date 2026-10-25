namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Exact request authenticated by a workload assertion over method, scope, epoch and immutable serialized body.</summary>
/// <param name="Method">The closed canonical operation.</param><param name="Scope">The exact namespace.</param>
/// <param name="Epoch">The enrolled authority epoch.</param><param name="Body">The bounded owned request bytes.</param><param name="Assertion">The short-lived resource-bound workload assertion.</param>
public sealed record SourcePublicationAuthorityCall(string Method, SourcePublicationScope Scope, string Epoch, byte[] Body, string Assertion);
