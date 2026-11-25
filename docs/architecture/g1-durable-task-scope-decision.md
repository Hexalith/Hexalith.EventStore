# G-1 Durable Task scope decision

On 2026-11-25 the EventStore Owner approved the G-1 Durable Task design, implementation, and isolated qualification scope requested by the Projects 7.1-P2 owner proposals.

The scope covers generic admission, single-use Confirmation Artifact consumption in the admission transaction, committed task status and owner receipt lookup, worker fencing, and recovery. The original actor, tenant, request and operation remain bound across retries. Review EventStore's existing checkpoint and receipt primitives for reuse.

The delivery checks require clean-checkout persisted tests for equivalent retry, failed and ambiguous admission, lease takeover, lost replies, dependency failure and cancellation, and an exercised rollback or forward recovery on the retained store. Record the exact source, runtime, commands, native results, and owner acceptance after those checks.

The inspected EventStore source was `ae1143bdc831ca6f2cf61726648a3efe946d68a2`. The proposal is `Hexalith.Projects/_bmad-output/implementation-artifacts/7-1-p2-owner-scope-proposals.md` at `fdc8ca69ebe0a656f88d55f9e32c8de71b1262f6`. This decision authorizes work in the isolated qualification scope. Capability acceptance is pending.

## Admission candidate

`DurableTaskAdmission` uses the same conditional state interface as `ProcessCheckpointStore`. One row binds the Tenant, original actor, versioned operation and idempotency key. The row holds the opaque token hash, normalized request hash, independently authorized interactive binding digest and expiry. First admission creates the task and marks the token consumed by that task in one conditional write. Equivalent retries reauthorize the original actor and recover the committed ID. A changed request, foreign scope, stale binding or revoked actor cannot recover or consume that admission. The owner of the authenticated host supplies the current authorization and interactive binding.

The existing `ProcessCheckpointStore` fences a source process around an enumerated event stream. A Durable Task has its own admission and execution lifecycle, described under the execution and lifecycle candidate below. The admission API still has no transport registration, and each effect still needs recorded original-actor authority. The remaining work is listed under Still open.

## Admission verification

From the EventStore checkout with Platform beside it, run the source-mode build and both focused suites.

```bash
run-with-temp bash -c '
set -e
project=tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
dotnet build "$project" --configuration Debug --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=true -m:1
tests="$TMPDIR/artifacts/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll"
dotnet "$tests" -class "Hexalith.EventStore.DomainService.Tests.DurableTasks.DurableTaskAdmissionTests"
python3 ../Hexalith.Platform/eng/durable-task-qualification/probe_state_store.py --native "$tests"
'
```

On 2026-11-25 the build completed with zero warnings and errors. Four focused and three live tests passed with zero skips. The live tests exercised competing task-only admissions and inspected the committed admission and artifact state across two sidecars after failures before and after commit. The fixture injected a lost write response at the storage boundary and used scoped test authority.

## Execution and lifecycle candidate

`DurableTaskExecution` keeps the lease fence, checkpoint, pending step and owner receipts on the admission row. Each owner step uses the stable key `{taskId}/step/{n}` and resolves an ambiguous outcome through the owner lookup before invoking the owner again. An unresolved partial effect enters `NeedsAttention` and fences automatic execution.

The committed lifecycle has the states `Pending`, `Running`, `WaitingForDependency`, `NeedsAttention`, `Succeeded`, `Rejected`, `Failed` and `Cancelled`. Each transition is one conditional write on the admission row, and a persisted state outside the graph fails closed. Rows persist the state by name. An owner failure records `WaitingForDependency` and keeps the step pending. Inconsistent owner lookup evidence, a missing receipt, or a receipt that differs from the committed checkpoint moves the task to `NeedsAttention` before the error is returned. The lease holder records a terminal outcome only when no step is pending, and terminal states are immutable. Cancellation clears the lease and retains committed receipts. It is refused once an irreversible step may have reached its owner, and while another step is in flight unless the task is in `NeedsAttention`. A refused or late cancellation returns a conflict with the committed status.

Admission reserves a task-identifier index row before the admission commit. Status and cancellation by task ID reauthorize the original scope and return no task for an unknown identifier or a denied caller. A worker acquires its lease from a scheduled task ID. A reservation whose admission row names another task resolves to no task.

Still open:

- The authenticated host and routes, including the admission lookup that binds the original actor for delegated steps.
- Separate authorization for status, cancellation and admission, including confirmation for cancellation.
- The scheduler and durable discovery.
- A terminal path for a pending step the owner definitively rejects. `IDurableTaskEffect` cannot report one, so `CompleteAsync` with `Rejected` or `Failed` is unreachable for that step.
- The retry budget and its `NeedsAttention` transition. Until then, a `WaitingForDependency` task with a pending step cannot be cancelled.
- Reconciliation from `NeedsAttention`.
- Retention of admission and index rows.
- Cancellation race tests.
- The selected rollback, including the compatibility of rows written by the previous candidate and of new rows read by it.

## Lifecycle verification

From the EventStore checkout with Platform beside it, build the test project, run the focused classes and run both native phases. The evidence directory keeps the native xUnit receipts after the temporary directory is removed.

```bash
run-with-temp bash -c '
set -e
evidence="$HOME/.local/state/hexalith-eventstore/g1-lifecycle"
mkdir -p "$evidence"
project=tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
dotnet build "$project" --configuration Debug --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=true -m:1
tests="$TMPDIR/artifacts/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll"
dotnet "$tests" -class "*DurableTaskAdmissionTests" -class "*DurableTaskExecutionTests" -class "*DurableTaskLifecycleTests" -failSkips -result-xml "$evidence/focused.xml"
python3 ../Hexalith.Platform/eng/durable-task-qualification/probe_state_store.py --native-execution "$tests" --evidence-directory "$evidence"
'
```

On 2026-11-25 the build completed with zero warnings and errors. The focused run passed 39 tests: 4 in `DurableTaskAdmissionTests`, 4 in `DurableTaskExecutionTests` and 31 in `DurableTaskLifecycleTests`. The probe passed 4 tests in `DurableTaskAdmissionLiveTests` and 1 test in each of the two `DurableTaskExecutionLiveTests` phases, with zero skips across two sidecars and a Redis restart. The fixture uses scoped test authority and a test owner.

## Original-actor binding (2026-11-25)

The EventStore Owner selected the committed admission lookup for delegated owner steps. A delegated step presents its task ID and stable step key. The owner reads the committed admission row through an authenticated EventStore lookup that binds the Tenant, original actor, operation and request hash. It checks that the step and target match, then re-evaluates the actor's current authority at the operation. No task-bound bearer credential is introduced. Platform and Folders recorded the same selection.

## One-task crash recovery — 2026-11-25

`DurableTaskRecovery` claims a known, committed task and prepares one registered owner step on the admission row. It reuses the admission store's conditional writes and the lifecycle execution record. The prepared checkpoint retains the task, Tenant, original actor, operation, idempotency key, normalized intent and deterministic step key. Takeover increments the fence under CAS and preserves the checkpoint. A worker consults the same owner authority before each submission. Only authoritative absence permits submission under the same key. Unavailable lookup or an ambiguous submission leaves the checkpoint prepared. A matching terminal receipt is stored under the current fence, and subsequent claims leave the completed row intact. The receipts below were captured against the pre-lifecycle row shape retained in `eventstore-recovery.patch`.

The trusted host supplies the exact admitted task, registered step and stable owner adapter. The adapter must check the original actor's current authority on lookup and submission, and the owner must commit the effect and receipt atomically. Lease expiry gates new work on the worker's UTC clock. The takeover CAS establishes the durable fence against older writes. An already dispatched request can reach its destination after lease expiry, so the owner must deduplicate by the committed step key. The fixture qualifies one synthetic owner on single-primary Redis.

### Source and runtime

| Input | Revision or digest |
| --- | --- |
| Approved G-1 admission baseline | EventStore `01e412351ca4b99e95cab68750297b3580726421` |
| EventStore source used for this slice | Latest release `v5.6.0`, `295b5a6ba584167f21cfef40787477b8814e32da`, plus the retained working-tree patch below. The admission baseline is an ancestor. |
| Builds | Latest release `v6.8.3`, `62876129eaa035e78bd0324419231f07d43a07b8` |
| EventStore source dependencies | Commons `59c22162e6b54a39864dbc5f0ba76056045a411f`, Tenants `067ac3fec62b0a607bb512bd75cb1ca0fd780ae7` |
| Platform fixture | `d2ee05007ebae4332df5b3e17e747d450faa83f1`, `eng/durable-task-qualification/probe_state_store.py` SHA-256 `1cbfa945f30fa4c0ef6c8b813503e8476821f097e21b44b98a3f7f1f58479e69` |
| Projects evidence | Projects `e5d4a21bf8547c4d1fce1aab97fc489db3c0c09a`, `_bmad-output/implementation-artifacts/evidence/epic7/entry-blockers-2026-11-25.md` SHA-256 `dea2b52f7c3a2487ecd824685b84a695c6268e1aa58e002834fc357d005fa161` |
| Dapr 1.19.2 | `daprio/daprd@sha256:ed471547b67db50c831d498b8ee861822fb037d9d594c38ac299546b425f575a` |
| Redis 7.4.12 Alpine | `redis@sha256:7f7d30b7fdd09467bac58198a1f166dd69789d348d4fe859cccb20e0cf287fbe` |
| Build and fixture tools | .NET SDK `11.0.100`; runner images `mcr.microsoft.com/dotnet/sdk@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea` and `python@sha256:4766d8b510c428e595d74b9cc5bbb2fae8e26316fffb4adc89908d79aacd58a2` |

Two sidecars used the same `g1-tasks` app ID and `g1-task-state` key prefix on a private network. Redis ran AOF with synchronous fsync. The test supplied a known task to each worker and used a deterministic UTC clock, a 30-second lease and an explicit takeover tick. The initial EventStore release AppHost also started in a disposable checkout using `aspire run --detach --non-interactive --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`. `aspire describe` captured the startup resource states and `aspire stop` stopped the instance.

### Reproduce the source run

Run from the standalone EventStore checkout with the Projects-pinned Platform checkout available locally. The retained patch covers the compiled source and tests. Each clone uses the recorded revision and has a detached HEAD. The replay writes fresh results to a separate local directory.

```bash
run-with-temp bash -eu -o pipefail -c '
receipt_dir="$HOME/.local/state/hexalith-eventstore/g1-recovery-2026-11-25/verified"
output_dir="$receipt_dir/../replay"
mkdir -p "$output_dir"
for module in EventStore Builds Commons Tenants; do
  if [[ "$module" == EventStore ]]; then
    source_repo="$1"
    revision=295b5a6ba584167f21cfef40787477b8814e32da
    target="$TMPDIR/EventStore"
  else
    source_repo="$1/references/Hexalith.$module"
    revision=$(git -C "$1" rev-parse "295b5a6ba584167f21cfef40787477b8814e32da:references/Hexalith.$module")
    target="$TMPDIR/EventStore/references/Hexalith.$module"
  fi
  git -c advice.detachedHead=false clone --quiet --shared --revision "$revision" "$source_repo" "$target"
done
git -c advice.detachedHead=false clone --quiet --shared --revision d2ee05007ebae4332df5b3e17e747d450faa83f1 "$2" "$TMPDIR/Platform"
cd "$TMPDIR/EventStore"
git status --short --branch
git reflog -3
git apply --check "$receipt_dir/eventstore-recovery.patch"
git apply "$receipt_dir/eventstore-recovery.patch"
git diff --check
project=tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
dotnet build "$project" --configuration Debug --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=true -m:1 2>&1 | tee "$output_dir/eventstore-build.log"
tests="$TMPDIR/artifacts/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll"
python3 "$TMPDIR/Platform/eng/durable-task-qualification/probe_state_store.py" --native "$tests" > "$output_dir/platform-native.log" 2>&1
dotnet "$tests" -class "*DurableTaskRecoveryTests" -class "*DurableTaskAdmissionTests" \
  -class "*CheckpointedProcessRunnerTests" -class "*DomainServiceTrustBoundaryTests" \
  -class "*CatalogMetadataBoundaryTests" -class "*DomainRouteCatalogAuxiliaryTests" \
  -failSkips -result-xml "$output_dir/eventstore-tests.xml" > "$output_dir/eventstore-tests.log" 2>&1
' verify "$PWD" "$PWD/../projects/references/Hexalith.Platform"
```

All three commands exited 0. The build reported zero warnings and errors. The selected EventStore lane passed 175 cases, including eight admission and recovery cases. The pinned Platform runner passed four native cases with zero failures or skips. Its preliminary provider probe returned first-write statuses `[409, 204]`, rejected the stale ETag and recovered fence 2 after restarting Redis and the two sidecars. The native task crash test ran after that provider restart.

### Persisted recovery receipt

The crash test started worker A as a child .NET process attached to replica A. Worker A persisted the prepared checkpoint at fence 1. The synthetic owner committed one effect and its receipt in the same first-write CAS and withheld the reply. The controller observed the owner row through replica B, killed worker A and waited for exit `137`. Replica B refused takeover before expiry. At expiry, worker B claimed fence 2 and read the same prepared checkpoint. A replay of worker A's lease via replica A failed while A's local fixture clock still considered the old lease live. The task row's ETag and bytes remained identical across that attempt.

Worker B issued exactly one owner call, a strong receipt lookup, and stored the result without a second submission. Both sidecars read the same final task and the same owner row. The owner row retained exactly one history entry with the original binding. Its bytes and ETag matched the row observed before takeover. A new claim after the second lease expired left the terminal task row unchanged. The focused tests separately exercised late lookup and submission responses after takeover, unavailable lookup, changed step input and a foreign receipt.

| Binding | Persisted value |
| --- | --- |
| Task | `01M899X5KMPR6KZA85DFN63VN4` |
| Tenant and original actor | `g1-tenant`, `original-actor` |
| Operation and admission idempotency key | `g1.recover@1`, `one-admitted-task` |
| Normalized intent SHA-256 | `2a4aa9f70c0992b77d21de01e7eb329387dbb39daf7355c5f31b76d9f9a15f5c` |
| Task-step key | `durable-task-step-v1-acff44db0a94ce07bdb1f437d49e84a43a95a56453ed6fcb09d5620e07c63289` |
| Owner receipt SHA-256 | `3c7582dcbd08bde6cd3eeb77d88b32861fe5c37fea23e0bcfe192409a2bd9cfb` |

| Strong-read snapshot | ETag | Raw row SHA-256 |
| --- | --- | --- |
| Admitted task, legacy row shape | 1 | `41c087d4c51fd0fc92f3853840fe855a10477b748fa53a2853eb5ef432c80626` |
| Task after A's crash, prepared at fence 1 | 3 | `6f95939cff27b8df08b5c8fdc55fbc032c8c0cac0525204eff066df543187ff5` |
| Task after B's takeover and after A's stale attempt | 4 | `812b923797ecc980984a1e7479febed3133324fc0956da9b5230e4d6805053b5` |
| Completed task, owner receipt retained | 5 | `43c253a4ec4a1dcfee44648a4a35455125b822dffccdf407efdd12b59faf8d69` |
| Owner effect history and receipt | 1 | `819d914ef7c82426662525018e55ef219dc4c9358d2b9751a01a5e77d9904042` |

Native output, the decoded-row checks and the xUnit result were verified locally. The JSON receipt preserves base64 of the exact persisted bytes. The following files are retained in `~/.local/state/hexalith-eventstore/g1-recovery-2026-11-25/verified/`.

| Artifact | SHA-256 |
| --- | --- |
| `eventstore-recovery.patch` | `d56568fcffb44c1cb139e79167d368f347544dcb7c62aa4ac27dfe73addc2060` |
| `eventstore-tests.xml` | `a52b6a39ee6d47c64fb56b4ba5124193d16972edbed19357f5d93b9199066567` |
| `platform-native.log` | `7cb82c99a61a9029411adfa0f8edc3d2418a0a559c3e1f12ee6eb6ac28b6643b` |
| `provider-receipt.json` | `a7aa51652fe6b22c592554ad59cf36c3c5e1224763fc002b641ef898f6483163` |
| `recovery-receipt.json` | `c91ecfe532d10f483289312e38a84bf3b14372d1fe940ce55bcd95a68b0b5350` |

### Projects package check

In disposable Projects `e5d4a21bf8547c4d1fce1aab97fc489db3c0c09a` checkouts with Builds `v6.8.3` available under `references/Hexalith.Builds`, both commands ran under `run-with-temp`. The released catalog defaults to EventStore 5.5.2, so the command selected published 5.6.0 explicitly. The restored `Hexalith.EventStore.DomainService.5.6.0.nupkg` had SHA-256 `a8753ab14aed9340fabd1bf060ba4e5609ad324e08c0e5296320bc5d9ebfdcab`.

```bash
CI=true dotnet build src/Hexalith.Projects.Server/Hexalith.Projects.Server.csproj --configuration Release \
  --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=false -p:HexalithEventStoreVersion=5.6.0 -m:1
CI=true dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Release \
  --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=false -p:HexalithEventStoreVersion=5.6.0 -m:1
```

The Server built with exit 0, zero warnings and zero errors. Its restored assets selected 5.6.0 for all seven EventStore packages, including the SDK query response policy. The Server tests build exited 1 with zero warnings and two errors in `ProjectsServerModuleTests.cs`. It reported CS0103 for `ProjectReadAuthorityFixture` at line 264 and CS0246 for `ProjectAuthoritySnapshot` at line 266. The package build logs are retained one directory above the recovery receipts. SHA-256 is `fd7f682516a594ec777579fc1625ed411d874f72a0aa3f5abd98e5235dd661ff` for `projects-package-server.log` and `1f488e55709730241566b654890cb5631d05b4e938bf49734b14fec81043e78a` for `projects-package-build.log`.

### Evidence still due and acceptance

| Accountable owner | Remaining evidence or decision |
| --- | --- |
| EventStore Owner | Version and qualify the authenticated admission and execution contract, original-actor and interactive binding, task discovery, authorized status and receipt lookup by task ID, retained intent/idempotency policy and authoritative cancellation and terminal transitions. Exercise persisted denial, dependency outage, ambiguous task-state writes and competing owner calls. Bind the registered step to its authoritative owner and verify receipt rebinding and conflicting retries end to end. Accept the exact capability candidate and its persisted lifecycle and recovery results. |
| Platform Owner with EventStore Owner | Select the exact package or source and approve the production provider, clock and skew, scheduling, lease and recovery bounds. Stop new admissions and fence candidate writers, then rehearse rollback or approved forward recovery on the retained task and owner store. Recheck the same admission, checkpoint, receipt and single owner effect before reopening work. Record dated acceptance of the runtime and the exercised retained-store procedure. |
| Folders Owner (Jerome), with EventStore and Platform | Qualify the create-only contract, expected version and collision behavior, task-bound current authority, sole-manager ACL, equivalent/conflicting retries and authoritative provisioning receipt/status after lost response. Accept retention with `NeedsAttention` or an explicitly authorized, idempotent compensator. Exercise rollback or fenced forward recovery with Folder history and grants retained. |
| Solution Architect and Test Architect | Review the same immutable EventStore, Platform and Folders coordinates, persisted matrix and rollback receipts. The three owners and both architects must each accept the same 7.1-P2 qualification digest before Projects can record the capability pin. |
| Projects Owner with EventStore, Builds, CiCd, Platform and Test Architect | Complete DW-63's catalog release selection and governed package CI, positive authenticated ordinary AppHost delivery, historical replay and writer-equivalence proof, and owner acceptance of the v4 comparison and independent retained-read rollback. |

7.1-P2 and DW-63 remain open. The hand-reconciled Projects tracker retains Story 7.1 as blocked and Epic 7 as backlog. The Projects production-authority index passed with `[6, 7, 8]` and all 19 active Section G manifest digests matched. The Product Owner and Authorized Solution Architect Section G decisions remain Pending.
