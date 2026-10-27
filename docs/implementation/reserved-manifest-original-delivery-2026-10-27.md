# Read-only reserved manifest original — source delivery, 2026-10-27

The private protection-owner port now exposes `ReadReservedManifestOriginalAsync(TenantId, BatchId, ReservationReceiptId)`. It returns `DeletionReservedManifestOriginal(Original, Outcome)` only for an existing retained `ConsumptionReserved` or `Consumed` batch with that exact reservation receipt. `Original` is the complete retained `Current` request: capability, detached JWS, committed issue and dispatch facts, and original target order. A prior legitimate re-attestation is preserved. A consumed result carries its actual retained terminal outcome and revision; no earlier reservation outcome is reconstructed.

The actor authorizes `ReadReservedDeletionManifestOriginal` before and after a successful read. Both snapshots require the exact independent current-state anchor and the same retained head. Missing state, authority, permission, selection or currentness returns no original. Pending transitions are unavailable, including independently verified transitions: this read never invokes the shared reconciliation reader. Actor state I/O, request/output capture, digest comparisons and authority calls share the existing thirty-second entry budget. The dedicated Dapr adapter preserves the exact tenant, actor address and selection, caller cancellation, and its whole-call deadline. Existing implementations of `IDeletionProtectionOwner` default to null when they omit this port.

The read invokes no recovery, provider, transition admission/recording, save, repair, enrollment, reservation or activation. Existing `LookupAsync` recovery semantics are unchanged. No public endpoint, Workflow/general actor transport or production registration was added. A manifest publisher is not part of this delivery.

## Source and verification

Owning checkout baseline: `fd327dc553169128021ee447d2a2f247fe1c33e4`. The [verification receipt](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/verification.json) retains final binary hashes, results and command/log paths. The [source hash manifest](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/source-sha256.json) identifies every changed C# source. Before/after NuGet package-lock inventories are retained alongside it: none existed in the inventoried dependency graph and none were created. No package lock was edited.

The owning test project was built individually with Debug assets and root-declared source dependencies. Both the initial and final normal builds exited 0 with zero warnings and errors. No audit, warning or version gate was suppressed. Exact final command:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug --artifacts-path /tmp/hexalith-reserved-original-20261027 -m:1 -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/home/administrator/projects/hexalith/agents/references/Hexalith.Commons -p:HexalithPolymorphicSerializationsRoot=/home/administrator/projects/hexalith/agents/references/Hexalith.PolymorphicSerializations -p:HexalithTenantsBasePath=/home/administrator/projects/hexalith/agents/references/Hexalith.Tenants/src
```

Each class below ran against that final built xUnit assembly using this invocation with its full class name:

```bash
dotnet /tmp/hexalith-reserved-original-20261027/bin/Hexalith.EventStore.Server.Tests/debug/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Security.DeletionReservedManifestOriginalTests
```

| Class | Passed | Exit |
| --- | ---: | ---: |
| `DeletionReservedManifestOriginalTests` | 29 | 0 |
| `DeletionConsumptionActorTests` | 58 | 0 |
| `PublishedTrustOwnerStateAuthorityTests` | 19 | 0 |
| `PublishedTrustDeletionConsumptionAuthorityTests` | 42 | 0 |
| `PublishedTrustReplacementAuthorityTests` | 98 | 0 |

All 246 tests passed with zero errors, failures, skips or unrun cases. `git diff --check` also passed. The initial 27-case focused run is retained separately; the final 29-case run covers the final source.

The focused cases verify serialized actor reconstruction and byte-identical durable ledger contents for reserved and consumed originals; prior re-attestation; wrong receipt, registration receipt, tenant and batch; unconsumed, blocked, covered and missing state; absent/revoked permission; divergent authority, changed head and pending transitions; owned immutable output vectors; whole-entry deadlines, stalled state/authority reads, private adapter addressing and cancellation; and fail-closed default implementations. Read-only state-manager call assertions exclude every mutation method, and independent authority assertions exclude transition recovery/recording and dispatch/activation validation. One test configures a `NotStarted` provider result and proves ordinary lookup subsequently calls `ConsumeAsync`, while the new accessor calls neither physical port and preserves the reserved ledger.

## Remaining delivery boundary

This is source/component evidence using synthetic authority, actor storage and provider ports. There is no installed acceptance or Levels 4/5 qualification result. The prior timed-out Platform Aspire baseline and absence of a running AppHost are not superseded. Independent installation, exact targets, private operation permission, trust bindings, complete owner returns and every existing qualification gate remain required. No dependency status, accepted target/date/contract, Branch B identity decision, 365 fixed-day retention, host `192.168.1.30`, timing bound `300/30/600/86400/604800`, original attempt or outcome is changed.

No Live/Full/cohort/consuming execution, installation, deployment, native physical work, commit, push, sibling change or submodule initialization was performed. The parent build workflow owns final review and the next manifest-index publication dependency.
