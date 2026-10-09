namespace Hexalith.EventStore.Client.Queries;
/// <summary>Represents a distinct complete actor-owned logical query root, without provider attestation.</summary>
/// <param name = "ModelId">The exact current logical model identity.</param>
/// <param name = "Tenant">The authenticated tenant.</param>
/// <param name = "Domain">The canonical domain.</param>
/// <param name = "HandlerRoute">The declared input handler route.</param>
/// <param name = "KeySpace">The declared logical key-space.</param>
/// <param name = "StoreName">The declared state-store identity.</param>
/// <param name = "BackendDescriptor">The exact declared backend descriptor.</param>
/// <param name = "Generation">The positive actor-owned root generation.</param>
/// <param name = "Rows">The complete ordered logical row inventory and origin witnesses.</param>
internal sealed record DaprLogicalQueryRoot(string ModelId, string Tenant, string Domain, string HandlerRoute, string KeySpace, string StoreName, byte[] BackendDescriptor, long Generation, DaprLogicalQueryRow[] Rows);
