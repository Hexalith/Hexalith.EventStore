# Read-only reserved manifest original — source delivery, 2026-10-27

Root verification after review passed **257/257 cases**, including 40 focused cases and all four prior regression classes. [Parent verification](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/parent-final-verification.json) records exact commands, hashes and limits. Current Contracts/Server source builds pass; the full test-project source graph is blocked by concurrent Tenants routing references, so the focused compilation reused retained dependency assets without analyzer suppression. Initial normal-build/246-case evidence below describes the earlier snapshot. This remains source-only delivery, with no installed qualification or manifest publisher.

The private protection-owner port now exposes `ReadReservedManifestOriginalAsync(TenantId, BatchId, ReservationReceiptId)`. It returns `DeletionReservedManifestOriginal(Original, Outcome)` only for an existing retained `ConsumptionReserved` or `Consumed` batch with that exact reservation receipt. `Original` is the complete retained `Current` request: capability, detached JWS, committed issue and dispatch facts, and original target order. A prior legitimate re-attestation is preserved. A consumed result carries its actual retained terminal outcome and revision; no earlier reservation outcome is reconstructed.

The actor authorizes `ReadReservedDeletionManifestOriginal` before and after a successful read. Both snapshots require the exact independent current-state anchor and the same retained head. Missing state, authority, permission, selection or currentness returns no original. Pending transitions are unavailable, including independently verified transitions: this read never invokes the shared reconciliation reader. Actor state I/O, request/output capture, digest comparisons and authority calls share the existing thirty-second entry budget. The dedicated Dapr adapter preserves the exact tenant, actor address and selection, caller cancellation, and its whole-call deadline. Its sixteen shared admission slots remain occupied until abandoned RPCs actually complete, and saturation throws before proxy creation or invocation. Existing implementations of `IDeletionProtectionOwner` default to null when they omit this port, including with a cancelled token.

Null refusals are distinct from exceptions: invalid scope/selection, malformed retained state or owned capture, and state/authority/transport faults may throw without returning a usable original or performing an effect. Actor entry timeouts return null; adapter caller cancellation and its deadline propagate `OperationCanceledException`. Adapter admission saturation propagates `AuthoritativeStreamReadAdmissionException`. These existing failure distinctions are documented without converting faults into absence claims.

The read invokes no recovery, provider, transition admission/recording, save, repair, enrollment, reservation or activation. Existing `LookupAsync` recovery semantics are unchanged. No public endpoint, Workflow/general actor transport or production registration was added. A manifest publisher is not part of this delivery.

## Source and verification before review corrections

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

All 246 tests passed with zero errors, failures, skips or unrun cases before review corrections. `git diff --check` also passed. The initial 27-case focused run is retained separately; the 29-case run covers the source before the corrections below, not the corrected source.

The focused cases verify serialized actor reconstruction and byte-identical durable ledger contents for reserved and consumed originals; prior re-attestation; wrong receipt, registration receipt, tenant and batch; unconsumed, blocked, covered and missing state; absent/revoked permission; divergent authority, changed head and pending transitions; owned immutable output vectors; whole-entry deadlines, stalled state/authority reads, private adapter addressing and cancellation; and fail-closed default implementations. Read-only state-manager call assertions exclude every mutation method, and independent authority assertions exclude transition recovery/recording and dispatch/activation validation. One test configures a `NotStarted` provider result and proves ordinary lookup subsequently calls `ConsumeAsync`, while the new accessor calls neither physical port and preserves the reserved ledger.

## Review corrections and focused verification

The corrected private adapter uses `ReadWithAdmissionAsync`. Its focused nonparallel test reuses the existing `NativeCaptureAdmission` collection: sixteen noncooperative calls time out while retaining every slot; saturation does not create a proxy or invoke the actor; after actual completion, sixteen new concurrent calls can be admitted. The focused class now also verifies a valid reservation receipt paired with another genuinely reserved batch through both actor and adapter, reserved/consumed reads with no physical provider, state/authority faults, malformed retained state, and the default port with caller cancellation.

The owning EventStore actor registration and Platform qualification composition have no JSON serializer override and use Dapr's default proxy factory. Tests confirm Dapr 1.18.10 proxy/runtime defaults select DataContract serialization. The actual Dapr provider-created `DataContractSerializer` initially rejected the new DTO and then its owned read-only carriers. The additive correction supplies `DataContract`/`DataMember` metadata on exactly `DeletionReservedManifestOriginal`, `DeletionBatchConsumptionRequest`, `DeletionBatchCapabilityV1`, `DeletionConsumptionOutcome`, `ProtectionTarget`, and `DeletionTargetReceipt`; two `KnownType` attributes on the new DTO register its owned read-only vectors. No public member or transport/configuration default changes. Reserved and consumed results now round-trip through that serializer with complete request/JWS, target order, outcome and independent vectors. This is serializer component evidence, without live actor transport.

The [JSON comparison](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/review/json-equivalence.json) proves byte-identical complete reserved and consumed JSON against preserved binaries from the original verified build, before these annotations. The baseline binary hashes match the original verification receipt. The fixture JSON and the reflection harness source/commands are retained for inspection; this comparison covers these fixtures, not general source invariance.

The current normal Server.Tests source build is blocked by ten `CS0234`/`CS0246` errors: newer root-declared Tenants `TenantCatalogDeclarations.cs` refers to routing types absent from this EventStore Contracts checkout. The normal failure remains recorded. Current `Hexalith.EventStore.Server.csproj` builds with Debug/source references and zero warnings/errors. For focused diagnostics only, Server.Tests was compiled with `--no-restore -p:BuildProjectReferences=false` against retained dependency assets after rebuilding the current affected Server/Contracts sources. Only `DeletionReservedManifestOriginalTests` ran after these corrections: **40 passed, zero errors/failures/skips/unrun**, including both transport serializer cases. The parent owns broader final verification. No source, audit or CI gate was relaxed.

The [compact command summaries](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/review/command-summaries.json) preserve exit/count results, exact command links, raw-log byte counts and SHA-256 hashes, including the earlier runs and corrected diagnostic run. Raw logs remain locally preserved under the existing ignore policy; neither ignore rules nor CI policy was changed. The parent archived the original full evidence/diff in Agents at `tests/owner-prerequisite-2026-10-27/reserved-original-before-review-patches.tar.gz`.

The [provenance snapshot](../../_bmad-output/implementation-artifacts/tests/reserved-manifest-original-2026-10-27/review/post-verification-provenance.json) records dependent source repositories, HEAD/dirty-state metadata, relevant source and dependency-asset hashes, SDK/runtime identities and final binaries in a bounded window **after verification**. Those observations are not pre-build pins, an atomic cross-repository snapshot, or proof that sources stayed invariant during earlier commands. No CI execution or installed acceptance is inferred.

## Remaining delivery boundary

This is source/component evidence using synthetic authority, actor storage and provider ports. There is no installed acceptance or Levels 4/5 qualification result. The prior timed-out Platform Aspire baseline and absence of a running AppHost are not superseded. Independent installation, exact targets, private operation permission, trust bindings, complete owner returns and every existing qualification gate remain required. No dependency status, accepted target/date/contract, Branch B identity decision, 365 fixed-day retention, host `192.168.1.30`, timing bound `300/30/600/86400/604800`, original attempt or outcome is changed.

No Live/Full/cohort/consuming execution, installation, deployment, native physical work, commit, push, sibling change or submodule initialization was performed. The parent build workflow owns final review and the next manifest-index publication dependency.
