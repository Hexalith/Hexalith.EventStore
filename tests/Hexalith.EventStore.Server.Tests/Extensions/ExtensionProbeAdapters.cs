using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Extensions;

/// <summary>Creates the one declared probe adapter that emitted extension assemblies return from their adapter source.</summary>
public static class ExtensionProbeAdapters
{
    /// <summary>Creates a fresh declared adapter for <see cref="ExtensionProbeCommand"/>.</summary>
    /// <returns>The complete probe adapter inventory.</returns>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create()
    {
        RouteContractDeclaration route = new("extension-probe-record", "command", "extension-probe", nameof(ExtensionProbeCommand), null,
            "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(ExtensionProbeCommand).FullName!, "clr-short-name-v1", [nameof(ExtensionProbeCommand)], "domain-service", null, []));
        CommandIntentDeclaration intent = new(route, "extension-probe:adapter", "extension-probe:record", "extension-probe:policy-v1",
            new("extension-probe:descriptor", 1, [new("target", "string"), new("payload-sha256", "string")]),
            IdempotencyReplayRetentionTier.Mutation, false);
        return [new DeclaredIdempotencyIntentAdapter<ExtensionProbeCommand>(intent,
            payload => new Dictionary<string, object?> { ["payload-sha256"] = CanonicalIntentPayload.Sha256(payload) })];
    }
}
