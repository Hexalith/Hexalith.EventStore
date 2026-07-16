---
schema: hexalith.eventstore.parity-closure-proof-packet/v1
story_id: "1.20"
story_key: 1-20-owner-approved-parity-closure-and-runtime-pin
created: 2026-07-16T05:09:20+02:00
updated: 2026-07-16T10:54:07+02:00
historical_packet: 1-8-projection-query-sdk-owner-proof-packet.md
candidate_source_sha: 85877902f8d60a466ab90cd8b68b53838863db1c
tested_runtime_sha: null
documentation_commit_sha: null
final_decision: still blocked
authorize_consumer_migration: false
---

# Story 1.20 Owner-Approved Parity Closure Proof Packet

## Decision

`still blocked`

This packet authorizes no consumer migration and no removal of consumer-owned
projection, query, rebuild, freshness, erasure, or rollback infrastructure. It is the
successor to the historical Story 1.8 packet; it does not rewrite that packet's decision
or treat its historical runtime SHA as current evidence.

The exact-SHA gate failed before package/container publication and owner review:

1. Story 1.19 is now `done` with an approved review disposition, so the previously
   recorded Story 1.19 prerequisite blocker is resolved.
2. A clean detached checkout of
   `85877902f8d60a466ab90cd8b68b53838863db1c` passed the warning-free Release build and
   broad unit regression lanes, but the full live-sidecar lane failed 2 of 44 tests.
3. The named-projection normal-delivery lifecycle cleanup failure reproduced in an
   isolated class run and again as a single-method run. The candidate therefore is not
   an approvable tested runtime and no parity row is promoted to `available`.
4. The completed Story 1.16 source spec still retains
   `followup_review_recommended: true` without an explicit disposition.
5. No named EventStore owner has reviewed completed passing exact-SHA evidence, and no
   manifest-governed package/container identities have been produced or approved.

### Scoped corrective item

Repair or explicitly disposition the reproducible lifecycle-cleanup defect exercised by
`NamedProjectionDispatchLiveSidecarTests.NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger`.
The scoped corrective item is recorded in `deferred-work.md` with the exact candidate SHA
and three failing commands. Then select a new clean committed runtime, rerun every
production-path and package/container gate at that unchanged SHA, explicitly disposition
Story 1.16's retained follow-up recommendation, obtain named EventStore-owner approval,
and update this packet.

## Prerequisite And Review Ledger

| Prerequisite | Current status | Current evidence | Closure disposition |
| --- | --- | --- | --- |
| Story 1.2 platform provenance | `done` | `1-2-domain-query-routing-and-response-provenance.md` adopts the completed routing and route-aware provenance evidence. | Satisfied for sequencing; must still be rerun at the selected runtime SHA. |
| Story 1.14 erasure | `done` | July 15 story-ID crosswalk maps completed historical Story 1.9 implementation, persisted-path evidence, and review. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.15 coordinated batching | `done` | Crosswalk maps completed historical Story 1.10 implementation and review. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.16 lifecycle | `done` | Crosswalk records completed lifecycle implementation/review and corrected Story 1.2 prerequisite. The retained source spec still says `followup_review_recommended: true`. | Status is complete, but the stale recommendation requires an explicit disposition before approval. |
| Story 1.17 asynchronous multi-projection dispatch | `done` | Crosswalk maps completed historical Story 1.12 and narrows acceptance to AD-19's normalized result. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.18 delivery idempotency | `done` | Crosswalk maps completed historical Story 1.13 production-path evidence. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.19 paged rebuild equivalence | `done` | Active Story 1.19 records approval after 13 in-scope patches, one explicit deferral, a 2,620-test Server pass, the real DAPR/Redis paged-rebuild pass, and a warning-free Release build. | Satisfied for sequencing; the paged-rebuild live test also passed at the current candidate SHA, but the cross-cutting live gate did not. |
| Story 1.20 owner review | pending | No reviewer, approval date, or durable source exists. | **Hard blocker.** |

## Artifact Identity Pin

The identities below are deliberately separate. A value in one identity domain never
substitutes for a value in another.

### Source identity

- candidate checkout SHA: `85877902f8d60a466ab90cd8b68b53838863db1c`
- approved/tested runtime SHA: not selected
- documentation-only commit SHA: not applicable yet
- clean-tree proof before gates: passed in the detached checkout, including all seven
  root-declared submodules and ignored-input rejection
- clean-tree proof after the failed gate: passed for regular/untracked inputs; generated
  Release outputs remained ignored and were not treated as source inputs
- approval status: pending proof-result review

`85877902f8d60a466ab90cd8b68b53838863db1c` is a failed candidate only. It is not an
approved source pin and does not authorize a consumer gitlink or checkout update.

### NuGet package identities

`tools/release-packages.json` currently names exactly 14 package IDs. No closure package
build was produced, so every version and SHA-256 hash is unresolved.

| Exact package ID | Approved version | SHA-256 |
| --- | --- | --- |
| `Hexalith.EventStore.Contracts` | not selected | not produced |
| `Hexalith.EventStore.Client` | not selected | not produced |
| `Hexalith.EventStore.Server` | not selected | not produced |
| `Hexalith.EventStore.SignalR` | not selected | not produced |
| `Hexalith.EventStore.Testing` | not selected | not produced |
| `Hexalith.EventStore.Testing.Integration` | not selected | not produced |
| `Hexalith.EventStore.Aspire` | not selected | not produced |
| `Hexalith.EventStore.ServiceDefaults` | not selected | not produced |
| `Hexalith.EventStore.DomainService` | not selected | not produced |
| `Hexalith.EventStore.RestApi.Generators` | not selected | not produced |
| `Hexalith.EventStore.Gateway` | not selected | not produced |
| `Hexalith.EventStore.Admin.Abstractions` | not selected | not produced |
| `Hexalith.EventStore.Admin.Cli` | not selected | not produced |
| `Hexalith.EventStore.Admin.Server` | not selected | not produced |

### Container identity

- configured candidate repository: `registry.hexalith.com/eventstore`
- approved immutable digest: not selected
- approved platform set: not selected
- release provenance mapping to approved source SHA: absent

The configured repository name is not a deployed identity. A tag, repository, source SHA,
or package version cannot substitute for an immutable image digest and its platform set.

## Candidate Environment Inventory

Captured from the failed exact-SHA gate environment; these values do not identify an
approved runtime:

- OS: Ubuntu 26.04, Linux `x64`
- .NET SDK: `10.0.301` (`96856fd726`)
- .NET host/runtime: `10.0.9` (`901ca94124`)
- Dapr CLI: `1.18.0`
- Dapr runtime: `1.18.1`
- Docker Engine: `29.4.3`
- root submodules: all seven root-declared `references/` submodules were initialized at
  their committed gitlinks in the disposable checkout; no nested submodule was initialized
- Redis/DAPR production-path lane: started by the live-sidecar fixture; persisted erasure,
  batch, cutover, and paged-rebuild tests passed, while named-delivery lifecycle cleanup failed

## Mandatory Exact-SHA Gate Harness

The following repository-root procedure is mandatory before any capability command below.
It creates a detached disposable checkout containing only committed inputs, initializes
only root-declared `references/` submodules, rejects untracked or ignored inputs before
restore, uses an isolated NuGet/Dotnet cache, and defines runners that cannot credit stale
Release assemblies or zero-match filters.

```bash
set -euo pipefail
: "${CANDIDATE_SHA:?export the selected 40-hex EventStore commit SHA}"
[[ "$CANDIDATE_SHA" =~ ^[0-9a-f]{40}$ ]]

SOURCE_REPOSITORY="$(git rev-parse --show-toplevel)"
GATE_ROOT="$(mktemp -d)"
CHECKOUT="$GATE_ROOT/eventstore"
EVIDENCE_ROOT="$GATE_ROOT/evidence"
mkdir -p "$EVIDENCE_ROOT"
git -C "$SOURCE_REPOSITORY" worktree add --detach "$CHECKOUT" "$CANDIDATE_SHA"
cd "$CHECKOUT"

test "$(git rev-parse --verify --end-of-options 'HEAD^{commit}')" = "$CANDIDATE_SHA"
mapfile -t ROOT_SUBMODULES < <(
  git config -f .gitmodules --get-regexp '^submodule\..*\.path$' |
    awk '$2 ~ /^references\// { print $2 }'
)
test "${#ROOT_SUBMODULES[@]}" -gt 0
git submodule update --init --checkout -- "${ROOT_SUBMODULES[@]}"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"

for repository in . "${ROOT_SUBMODULES[@]}"; do
  test -z "$(git -C "$repository" status --porcelain=v1 --untracked-files=all)"
  test -z "$(git -C "$repository" status --porcelain=v1 --ignored=matching --untracked-files=all)"
done

export DOTNET_CLI_HOME="$GATE_ROOT/dotnet-home"
export NUGET_PACKAGES="$GATE_ROOT/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$GATE_ROOT/nuget-http-cache"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH"

fresh_release() {
  local project="$1"
  local project_directory="${project%/*}"
  rm -rf "$project_directory/bin/Release" "$project_directory/obj/Release"
  dotnet restore "$project" \
    --configfile nuget.config \
    --packages "$NUGET_PACKAGES" \
    -p:UseHexalithProjectReferences=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0
  dotnet build "$project" \
    --configuration Release \
    --no-restore \
    -m:1 \
    -p:UseHexalithProjectReferences=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0
}

run_xunit_class() {
  local assembly="$1"
  local class_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  test -f "$assembly"
  dotnet "$assembly" -noColor -list methods -class "$class_name" | tee "$methods"
  grep -Fq "$class_name." "$methods"
  dotnet "$assembly" -noColor -class "$class_name" -xml "$results"
  grep -Eq 'total="[1-9][0-9]*"' "$results"
}

run_xunit_method() {
  local assembly="$1"
  local method_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  test -f "$assembly"
  dotnet "$assembly" -noColor -list methods -method "$method_name" | tee "$methods"
  grep -Fxq "$method_name" "$methods"
  dotnet "$assembly" -noColor -method "$method_name" -xml "$results"
  grep -Eq 'total="[1-9][0-9]*"' "$results"
}

run_xunit_all() {
  local assembly="$1"
  local evidence_name="$2"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  test -f "$assembly"
  dotnet "$assembly" -noColor -xml "$results"
  grep -Eq 'total="[1-9][0-9]*"' "$results"
}
```

The xUnit v3 in-process runner returns exit code zero when a filter matches zero tests.
The `-list methods` plus `grep` checks reject that case before execution, and each XML
result must report a positive `total`. `fresh_release` deletes Release `bin`/`obj`,
restores through the committed `nuget.config` into a new cache, and builds before an
assembly is invoked. After all gates, the runtime-source proof is:

```bash
test "$(git rev-parse --verify --end-of-options 'HEAD^{commit}')" = "$CANDIDATE_SHA"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
```

Ignored `bin`/`obj` evidence output is expected only after the pre-gate ignored-input
check; it is never used as a source or configuration input.

## Parity Capability Matrix

All rows remain `still blocked`. Current source and test paths identify the evidence that
must be rerun; they are not accepted as exact-SHA closure evidence in this packet.

### Read-model and projection-checkpoint erasure

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddressFactory.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
  - `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionEraseCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionLifecycleGatewayTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionEraseLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelStoreTests \
  erasure-dapr-store

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionEraseCoordinatorTests \
  erasure-coordinator
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Actors.ProjectionLifecycleActorTests \
  erasure-lifecycle-admission
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionLifecycleGatewayTests \
  erasure-lifecycle-transport

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionEraseLiveSidecarTests \
  erasure-live
```

- closure result and persisted read-back: four exact-SHA live erasure tests passed, but
  the result is not promoted because the cross-cutting live gate failed and owner review
  has not occurred
- limitations requiring owner disposition: admission/write-fence races, caller-owned slot
  completeness, authenticated stitched E2E, and global-administrator boundary
- rollback: retain the consumer erasure and checkpoint-reset implementation

### Coordinated batching

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelBatchStateAccessor.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelBatchTests \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelStoreTests \
  Hexalith.EventStore.Client.Tests.Projections.InMemoryReadModelStoreTests \
  Hexalith.EventStore.Client.Tests.Projections.ReadModelBatchStoreTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "batch-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests \
  batch-live
```

- closure result and persisted read-back: six exact-SHA live batch tests passed, including
  detail/index/receipt state, abort restoration, checkpoint immutability, cancellation,
  and same-identity convergence; the result remains non-authorizing because the
  cross-cutting live gate failed
- limitations requiring owner disposition: store transaction capability, ETag behavior,
  corrupt/abandoned envelopes, terminal receipt retention, and cross-profile boundaries
- rollback: retain the consumer's coordinated detail/index write path and retry protocol

### Six-state lifecycle and route-bound provenance

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs`
  - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
  - `src/Hexalith.EventStore/Controllers/QueriesController.cs`
  - `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
  - `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
  - `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`
- test paths:
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
  - `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs`
  - `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs`
  - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj
for class_name in \
  Hexalith.EventStore.Contracts.Tests.Queries.ProjectionAdapterContractTests \
  Hexalith.EventStore.Contracts.Tests.Queries.SubmitQueryResponseTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Projections.ReadModelFreshnessTests \
  Hexalith.EventStore.Client.Tests.Gateway.EventStoreGatewayClientTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll \
  Hexalith.EventStore.QueryRouting.Tests.HandlerAwareQueryRouterTests \
  lifecycle-query-routing

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Queries.QueryRouterTests \
  Hexalith.EventStore.Server.Tests.Controllers.QueriesControllerTests \
  Hexalith.EventStore.Server.Tests.Actors.CachingProjectionActorTests \
  Hexalith.EventStore.Server.Tests.Integration.QueryResponseProvenancePersistenceTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj
for class_name in \
  Hexalith.EventStore.RestApi.Generators.Tests.RestApiControllerGenerationTests \
  Hexalith.EventStore.RestApi.Generators.Tests.RestApiGeneratedControllerErrorSemanticsTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll \
  Hexalith.EventStore.Sample.Tests.SampleApi.SampleApiGeneratedControllerRuntimeTests \
  lifecycle-sample-runtime

fresh_release tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  lifecycle-provenance-e2e
```

- closure result and persisted read-back: contract, client, query-routing, server,
  generator, and Sample regression projects passed at the candidate SHA; the required
  query-provenance E2E lane was not reached after the live regression failure
- compatibility decision awaiting approval: preserve `Unknown`, all six operational values,
  the legacy metadata ABI, and the rule that ETag never supplies lifecycle or version
- rollback: retain consumer freshness adapters and fail closed on non-projection evidence

### Duplicate and out-of-order production-handler safety

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryIdempotencyCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryReconciler.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryCutover.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionActivationOutbox.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryStateStore.cs`
  - `docs/operations/projection-delivery-v2-evidence.md`
- test paths:
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryIdempotencyCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryReconcilerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCutoverTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionActivationOutboxTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionDeliveryCutoverLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryIdempotencyCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryReconcilerTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryCutoverTests \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionDeliveryRetrySchedulerTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionActivationOutboxTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "delivery-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionDeliveryCutoverLiveSidecarTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
    "$class_name" \
    "delivery-${class_name##*.}"
done
```

- closure result and persisted read-back: FAILED at the candidate SHA. The full live lane
  failed two named-dispatch tests; the isolated six-method class retained the normal-delivery
  lifecycle record (5 passed, 1 failed), and the single-method run reproduced it (0 passed,
  1 failed). The candidate cannot close duplicate/order production-handler safety.
- compatibility decision awaiting approval: v2 protects exact named routes; legacy
  `/project` remains outside the v2 no-mixed-writer guarantee
- rollback: preserve the complete pre-cutover fleet and backup boundary; never roll one
  writer back across the global v2 marker

### Full paged-rebuild equivalence

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs`
  - `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
- test paths:
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionPathTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  rebuild-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionRebuildProductionPathTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionUpdateOrchestratorTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionRebuildCheckpointStoreTests \
  Hexalith.EventStore.Server.Tests.Actors.ProjectionLifecycleActorTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "rebuild-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_method \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests.PagedRebuild_MoreThanTwoPages_PersistsEquivalentRedisActorDetailIndexAndCheckpoints \
  rebuild-live-persisted-equivalence
```

- closure result and persisted read-back: the real DAPR/Redis multi-page rebuild test
  passed at the candidate SHA and Story 1.19 review is closed, but the enclosing
  named-dispatch live gate failed and therefore cannot be approved as a complete lane
- limitations requiring owner disposition: complete-prefix memory/byte bounds, coordinated
  promotion boundary, cancellation/resume semantics, and every deferred Story 1.19 review item
- rollback: retain the last complete live model and consumer rebuild path; never promote
  page-only or incomplete staged output

### Cursor compatibility

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs`
  - `src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/EventStoreDataProtectionServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/DaprXmlRepository.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorCodecTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorScopeTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDataProtectionTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Queries.QueryCursorCodecTests \
  Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "cursor-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.EventStoreDataProtectionTests \
  cursor-persisted-key-ring
```

- closure result and persisted read-back: Client and DomainService regression projects
  passed at the candidate SHA; the result remains non-authorizing pending the failed live
  gate, release identities, and owner review
- limitation: routine retained-key rotation is distinct from key-ring loss/replacement
- rollback: preserve opaque cursor handling and the existing consumer compatibility path

### Asynchronous projection persistence

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.DomainService/ReadModelBatchProjectionResultMapper.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionActivationOutbox.cs`
- test paths:
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionActivationOutboxTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  async-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionDeliveryRetrySchedulerTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionActivationOutboxTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "async-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  async-live
```

- closure result and persisted read-back: FAILED as a complete production lane because
  named-delivery lifecycle cleanup did not converge in the full, class, or method reruns
- limitations requiring owner disposition: hand-written state writes, stable work identity,
  dead-letter/terminal cleanup, and null metadata behavior
- rollback: retain the consumer's asynchronous persistence handoff

### Multiple projections per domain

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.Client/Projections/ProjectionRouteCatalogFingerprint.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalog.cs`
- test paths:
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Indexes/AdminOperationalIndexHostedServiceTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionRouteCatalogTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  multiprojection-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
  Hexalith.EventStore.Client.Tests.Indexes.AdminOperationalIndexHostedServiceTests \
  multiprojection-catalog-publication

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionRouteCatalogTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "multiprojection-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  multiprojection-live
```

- closure result and persisted read-back: FAILED as a complete production lane. Four of
  six named-dispatch methods passed in the full live run, but normal delivery retained a
  lifecycle row and the concurrent scenario reported an unreleased lifecycle lease
- compatibility decision awaiting approval: keep versioned `/project/v2`, legacy
  `/project`, exact `(Domain, ProjectionType)` routing, and bounded AD-19 results
- rollback: retain consumer-owned detail/index handlers and route composition

## Cross-Cutting Compatibility And Release Gate

- classification: `still blocked`
- production source paths:
  - `Directory.Build.targets`
  - `tools/release-packages.json`
  - `tools/pack-release-packages.py`
  - `scripts/pack-release-packages.py`
  - `scripts/validate-nuget-packages.py`
  - `scripts/validate-consumer-package-references.py`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore/Hexalith.EventStore.csproj`
- focused compatibility and packaging test paths:
  - `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionHandlerCompatibilityTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
- required exact build and test commands, run only inside the mandatory detached harness:

```bash
find . -type d \( -path '*/bin/Release' -o -path '*/obj/Release' \) \
  -prune -exec rm -rf {} +
dotnet restore Hexalith.EventStore.slnx \
  --configfile nuget.config \
  --packages "$NUGET_PACKAGES" \
  -p:NuGetAudit=false \
  -p:MinVerVersionOverride=1.0.0
dotnet build Hexalith.EventStore.slnx \
  --configuration Release \
  --no-restore \
  -m:1 \
  -warnaserror \
  -p:NuGetAudit=false \
  -p:MinVerVersionOverride=1.0.0

while IFS='|' read -r project assembly evidence_name; do
  test -n "$project"
  fresh_release "$project"
  run_xunit_all "$assembly" "$evidence_name"
done <<'TEST_PROJECTS'
tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj|tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll|crosscut-contracts
tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj|tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll|crosscut-client
tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj|tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll|crosscut-domain-service
tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj|tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll|crosscut-query-routing
tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj|tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll|crosscut-server
tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj|tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll|crosscut-rest-generator
tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj|tests/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll|crosscut-sample
tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj|tests/Hexalith.EventStore.Testing.Tests/bin/Release/net10.0/Hexalith.EventStore.Testing.Tests.dll|crosscut-testing
TEST_PROJECTS

fresh_release tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  crosscut-focused-integration

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_all \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  crosscut-live-sidecar

run_xunit_class \
  tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll \
  Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests \
  crosscut-release-manifest
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionHandlerCompatibilityTests \
  crosscut-domain-compatibility
```

- required exact package commands:

```bash
PACKAGE_OUTPUT="$GATE_ROOT/packages"
PACKAGE_VERSION="999.1.20-proof.$(git rev-parse --short=12 "$CANDIDATE_SHA")"
rm -rf "$PACKAGE_OUTPUT"
find src -type d \( -path '*/bin/Release' -o -path '*/obj/Release' \) \
  -prune -exec rm -rf {} +
python3 scripts/pack-release-packages.py "$PACKAGE_OUTPUT" "$PACKAGE_VERSION"
python3 scripts/validate-nuget-packages.py "$PACKAGE_OUTPUT"
test "$(find "$PACKAGE_OUTPUT" -maxdepth 1 -type f -name '*.nupkg' | wc -l)" -eq 14
python3 scripts/validate-consumer-package-references.py "$PACKAGE_OUTPUT"
sha256sum "$PACKAGE_OUTPUT"/*.nupkg | sort -k2 > "$EVIDENCE_ROOT/nuget-sha256.txt"
test "$(wc -l < "$EVIDENCE_ROOT/nuget-sha256.txt")" -eq 14
```

- required exact container publication, immutable inspection, and provenance commands:

```bash
IMAGE_REPOSITORY='registry.hexalith.com/eventstore'
IMAGE_TAG="proof-${CANDIDATE_SHA}"
dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
  --configuration Release \
  --no-restore \
  -p:PublishProfile=DefaultContainer \
  -p:ContainerRegistry=registry.hexalith.com \
  -p:ContainerImageTag="$IMAGE_TAG" \
  "-p:ContainerRuntimeIdentifiers=linux-x64;linux-arm64"
docker buildx imagetools inspect "$IMAGE_REPOSITORY:$IMAGE_TAG" \
  | tee "$EVIDENCE_ROOT/container-inspect.txt"
IMAGE_DIGEST="$({ awk '$1 == "Digest:" { print $2; exit }' \
  "$EVIDENCE_ROOT/container-inspect.txt"; } || true)"
[[ "$IMAGE_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
docker buildx imagetools inspect "$IMAGE_REPOSITORY@$IMAGE_DIGEST" --raw \
  > "$EVIDENCE_ROOT/container-manifest.json"
jq -e '.manifests | type == "array" and length > 0' \
  "$EVIDENCE_ROOT/container-manifest.json"
jq -r '.manifests[].platform | "\(.os)/\(.architecture)\(if .variant then "/" + .variant else "" end)"' \
  "$EVIDENCE_ROOT/container-manifest.json" | sort -u \
  > "$EVIDENCE_ROOT/container-platforms.txt"
test -s "$EVIDENCE_ROOT/container-platforms.txt"
jq -n \
  --arg source_sha "$CANDIDATE_SHA" \
  --arg repository "$IMAGE_REPOSITORY" \
  --arg digest "$IMAGE_DIGEST" \
  --rawfile platforms "$EVIDENCE_ROOT/container-platforms.txt" \
  '{source_sha: $source_sha, repository: $repository, digest: $digest,
    platforms: ($platforms | split("\n") | map(select(length > 0)))}' \
  > "$EVIDENCE_ROOT/container-provenance.json"
```

| Gate | Required result | Evidence target | Required disposition owner | Current result |
| --- | --- | --- | --- | --- |
| Exact committed source | Same 40-hex SHA before and after gates; clean regular and ignored inputs before restore | pre/post status, submodule SHAs, environment inventory | EventStore owner | PASS for failed candidate `85877902f8d60a466ab90cd8b68b53838863db1c` |
| Release build and tests | warning-free solution build; positive xUnit totals for every listed project/filter | build log plus XML and method-list files under `$EVIDENCE_ROOT` | EventStore owner | **FAIL**; Release build and broad unit lanes passed, full live-sidecar was 42 passed / 2 failed |
| NuGet inventory | exact 14-ID set, one approved version, 14 SHA-256 values, package-only consumer success | package listing, validator logs, `nuget-sha256.txt`, consumer assets/tool-install log | EventStore release owner | NOT RUN |
| Container runtime | immutable registry digest, non-empty explicit platform set, digest-to-tested-SHA provenance | `container-inspect.txt`, manifest, platform set, `container-provenance.json` | EventStore release owner | NOT RUN |
| Limitations and migration | every matrix-row limitation accepted or rejected by a named reviewer in a durable source | signed review record or PR URL and date | EventStore owner | NOT RUN |

- compatibility boundary: additive public APIs, the legacy query metadata ABI, legacy
  `/project`, versioned `/project/v2`, and the manifest-owned 14-package inventory remain
  unchanged by this evidence-only packet
- owner disposition: pending; the candidate failed before package/container gates and no
  named owner has accepted the row limitations
- rollback: do not change package pins, image deployment, consumer gitlinks, or consumer
  infrastructure from this packet

## Owner Review

- reviewer: not assigned
- approval date: not recorded
- durable approval source or PR: not recorded
- accepted scope: none
- accepted limitations: none
- migration decision: `authorize_consumer_migration: false`

Story creation, implementation review of an individual prerequisite, and authorship of
this blocked packet are not proof-result owner approval.

## Consumer Handoff Guard

All placeholders below intentionally fail closed. They may be replaced only from a future
`available` packet carrying named owner approval and the matching evidence files.

### Source consumer procedure

```bash
set -euo pipefail
APPROVED_EVENTSTORE_SHA='NOT_SELECTED'
[[ "$APPROVED_EVENTSTORE_SHA" =~ ^[0-9a-f]{40}$ ]]
EVENTSTORE_SUBMODULE='references/Hexalith.EventStore'

GITLINK_SHA="$(git ls-tree --object-only HEAD "$EVENTSTORE_SUBMODULE")"
CHECKOUT_SHA="$(git -C "$EVENTSTORE_SUBMODULE" \
  rev-parse --verify --end-of-options 'HEAD^{commit}')"
test "$GITLINK_SHA" = "$APPROVED_EVENTSTORE_SHA"
test "$CHECKOUT_SHA" = "$APPROVED_EVENTSTORE_SHA"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
test -z "$(git -C "$EVENTSTORE_SUBMODULE" \
  status --porcelain=v1 --untracked-files=all)"
test -z "$(git -C "$EVENTSTORE_SUBMODULE" \
  status --porcelain=v1 --ignored=matching --untracked-files=all)"
```

A dirty consumer, dirty or ignored EventStore source/configuration input, or any approved
SHA/gitlink/checkout mismatch keeps source migration blocked.

### NuGet consumer procedure

The future approval record must supply `APPROVED_PACKAGE_VERSION`, the directory containing
the exact 14 `.nupkg` files, and a `sha256sum --check` file whose filenames are relative to
that directory. The approved ID inventory is literal, not merely a count:

```bash
set -euo pipefail
APPROVED_PACKAGE_VERSION='NOT_SELECTED'
APPROVED_PACKAGE_DIRECTORY='NOT_SELECTED'
APPROVED_PACKAGE_HASHES='NOT_SELECTED'
CONSUMER_PROJECT='NOT_SELECTED'
test "$APPROVED_PACKAGE_VERSION" != 'NOT_SELECTED'
test -d "$APPROVED_PACKAGE_DIRECTORY"
test -f "$APPROVED_PACKAGE_HASHES"
test -f "$CONSUMER_PROJECT"

EXPECTED_IDS="$(mktemp)"
cat > "$EXPECTED_IDS" <<'PACKAGE_IDS'
Hexalith.EventStore.Admin.Abstractions
Hexalith.EventStore.Admin.Cli
Hexalith.EventStore.Admin.Server
Hexalith.EventStore.Aspire
Hexalith.EventStore.Client
Hexalith.EventStore.Contracts
Hexalith.EventStore.DomainService
Hexalith.EventStore.Gateway
Hexalith.EventStore.RestApi.Generators
Hexalith.EventStore.Server
Hexalith.EventStore.ServiceDefaults
Hexalith.EventStore.SignalR
Hexalith.EventStore.Testing
Hexalith.EventStore.Testing.Integration
PACKAGE_IDS
sort -u -o "$EXPECTED_IDS" "$EXPECTED_IDS"
test "$(wc -l < "$EXPECTED_IDS")" -eq 14

ACTUAL_IDS="$(mktemp)"
find "$APPROVED_PACKAGE_DIRECTORY" -maxdepth 1 -type f -name '*.nupkg' -printf '%f\n' \
  | sed -n "s/\.${APPROVED_PACKAGE_VERSION//./\\.}\.nupkg$//p" \
  | sort -u > "$ACTUAL_IDS"
test "$(find "$APPROVED_PACKAGE_DIRECTORY" -maxdepth 1 -type f -name '*.nupkg' | wc -l)" -eq 14
diff -u "$EXPECTED_IDS" "$ACTUAL_IDS"
(cd "$APPROVED_PACKAGE_DIRECTORY" && sha256sum --check "$APPROVED_PACKAGE_HASHES")

CONSUMER_GATE="$(mktemp -d)"
cat > "$CONSUMER_GATE/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="approved-eventstore" value="$APPROVED_PACKAGE_DIRECTORY" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="approved-eventstore">
      <package pattern="Hexalith.EventStore*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF
dotnet restore "$CONSUMER_PROJECT" \
  --configfile "$CONSUMER_GATE/nuget.config" \
  --packages "$CONSUMER_GATE/packages" \
  --force-evaluate \
  -p:UseHexalithProjectReferences=false
ASSETS_FILE="$(dirname "$CONSUMER_PROJECT")/obj/project.assets.json"
jq -e --arg version "$APPROVED_PACKAGE_VERSION" '
  [.libraries | to_entries[] |
    select(.key | startswith("Hexalith.EventStore") or startswith("hexalith.eventstore"))] as $eventstore |
  ($eventstore | length > 0) and
  all($eventstore[];
    .value.type == "package" and
    ((.key | ascii_downcase) | endswith(("/" + $version) | ascii_downcase)))
' "$ASSETS_FILE"
dotnet build "$CONSUMER_PROJECT" \
  --configuration Release \
  --no-restore \
  -p:UseHexalithProjectReferences=false
```

The filename/version check proves the full approved inventory is present; the asset-file
check proves every EventStore dependency actually selected by the consumer resolves as a
package at that exact version rather than as a project reference.

### Container consumer procedure

```bash
set -euo pipefail
APPROVED_IMAGE_REPOSITORY='NOT_SELECTED'
APPROVED_IMAGE_DIGEST='NOT_SELECTED'
APPROVED_PLATFORM_SET_FILE='NOT_SELECTED'
test "$APPROVED_IMAGE_REPOSITORY" != 'NOT_SELECTED'
[[ "$APPROVED_IMAGE_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
test -s "$APPROVED_PLATFORM_SET_FILE"

IMMUTABLE_IMAGE="$APPROVED_IMAGE_REPOSITORY@$APPROVED_IMAGE_DIGEST"
INSPECT_OUTPUT="$(mktemp)"
RAW_MANIFEST="$(mktemp)"
ACTUAL_PLATFORMS="$(mktemp)"
docker buildx imagetools inspect "$IMMUTABLE_IMAGE" | tee "$INSPECT_OUTPUT"
test "$(awk '$1 == "Digest:" { print $2; exit }' "$INSPECT_OUTPUT")" \
  = "$APPROVED_IMAGE_DIGEST"
docker buildx imagetools inspect "$IMMUTABLE_IMAGE" --raw > "$RAW_MANIFEST"
jq -e '.manifests | type == "array" and length > 0' "$RAW_MANIFEST"
jq -r '.manifests[].platform | "\(.os)/\(.architecture)\(if .variant then "/" + .variant else "" end)"' \
  "$RAW_MANIFEST" | sort -u > "$ACTUAL_PLATFORMS"
test -s "$ACTUAL_PLATFORMS"
diff -u <(sort -u "$APPROVED_PLATFORM_SET_FILE") "$ACTUAL_PLATFORMS"
```

The deployment reference must be the verified `repository@sha256:...` value, never a tag.
Until all three procedures have real approved pins and pass in the consumer repository,
this packet authorizes no consumer repository, package pin, deployment, or rollback change.

## Verification Log

### Discovery-only identity and environment

| Command | Result |
| --- | --- |
| `git rev-parse --verify --end-of-options 'HEAD^{commit}'` | PASS; candidate discovery SHA `26842d284f2da91399b7891bf7b5880ce2f6b561`. |
| `git status --porcelain=v1 --untracked-files=all --ignore-submodules=none` | CLOSURE BLOCKED; the Story 1.20 specification was already modified, so `HEAD` was not selected as tested runtime. |
| `git submodule status` | PASS for inventory capture; two root-declared submodules were uninitialized and no nested submodules were initialized. |
| `dotnet --info` | PASS; SDK `10.0.301`, host/runtime `10.0.9`, Ubuntu 26.04 `linux-x64`. |
| `dapr --version` | PASS; CLI `1.18.0`, runtime `1.18.1`. |

### Exact release-package inventory check

The following check passed and compares the manifest with the literal approved inventory,
not merely with a count:

```bash
set -euo pipefail
EXPECTED_IDS="$(mktemp)"
cat > "$EXPECTED_IDS" <<'PACKAGE_IDS'
Hexalith.EventStore.Admin.Abstractions
Hexalith.EventStore.Admin.Cli
Hexalith.EventStore.Admin.Server
Hexalith.EventStore.Aspire
Hexalith.EventStore.Client
Hexalith.EventStore.Contracts
Hexalith.EventStore.DomainService
Hexalith.EventStore.Gateway
Hexalith.EventStore.RestApi.Generators
Hexalith.EventStore.Server
Hexalith.EventStore.ServiceDefaults
Hexalith.EventStore.SignalR
Hexalith.EventStore.Testing
Hexalith.EventStore.Testing.Integration
PACKAGE_IDS
ACTUAL_IDS="$(mktemp)"
jq -r '.packages[].id' tools/release-packages.json | sort -u > "$ACTUAL_IDS"
test "$(jq '.packages | length' tools/release-packages.json)" -eq 14
test "$(wc -l < "$ACTUAL_IDS")" -eq 14
diff -u "$EXPECTED_IDS" "$ACTUAL_IDS"
```

Result: PASS; all and only the 14 listed package IDs are present.

### Reproducible cited-path check

This command extracted every cited repository source, test, documentation, release-tool,
solution, and restore-config path, deduplicated them, and failed on any absent path:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
CITED_PATHS="$(mktemp)"
perl -nle 'while (m{(?<![A-Za-z0-9_.-])((?:src|tests|docs|scripts|tools)/[A-Za-z0-9_./-]+\.(?:csproj|cs|md|py|json)|Directory\.Build\.targets|Hexalith\.EventStore\.slnx|nuget\.config)}g) { print $1 }' \
  "$PACKET" | sort -u > "$CITED_PATHS"
MISSING_PATHS="$(mktemp)"
while IFS= read -r path; do
  test -e "$path" || printf '%s\n' "$path"
done < "$CITED_PATHS" > "$MISSING_PATHS"
test ! -s "$MISSING_PATHS"
test "$(wc -l < "$CITED_PATHS")" -eq 105
```

Result: PASS; all 105 unique cited repository paths exist.

### xUnit v3 filter and zero-test guard check

The installed xUnit v3 in-process runner was probed directly. A nonexistent class returned
exit code zero and XML `total="0"`, proving that exit status alone is insufficient:

```bash
set -euo pipefail
ASSEMBLY='tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll'
ZERO_XML="$(mktemp --suffix=.xml)"
set +e
dotnet "$ASSEMBLY" -noColor \
  -class Hexalith.EventStore.Client.Tests.DoesNotExist \
  -xml "$ZERO_XML"
ZERO_STATUS=$?
set -e
test "$ZERO_STATUS" -eq 0
grep -Eq 'total="0"' "$ZERO_XML"

METHODS="$(mktemp)"
dotnet "$ASSEMBLY" -noColor -list methods \
  -class Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests > "$METHODS"
grep -Fq 'Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests.' "$METHODS"
```

Result: PASS; zero-match returned `0`/`total="0"`, while the positive list filter found six
method rows. This was a runner-behavior probe against an existing assembly, not parity
closure evidence; the mandatory harness still requires a fresh Release build and positive
XML total for every credited run.

### Packet whitespace checks

Because this packet is untracked, both tracked and no-index checks are required:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
git diff --check
set +e
NO_INDEX_OUTPUT="$(git diff --no-index --check /dev/null "$PACKET" 2>&1)"
NO_INDEX_STATUS=$?
set -e
test "$NO_INDEX_STATUS" -eq 1
test -z "$NO_INDEX_OUTPUT"
```

Result: PASS; the tracked diff and the complete untracked packet contain no whitespace
errors. Exit `1` is the expected `--no-index` content-difference status.

### 2026-07-16 exact-SHA completion attempt

Candidate: `85877902f8d60a466ab90cd8b68b53838863db1c` in a clean detached checkout with isolated
NuGet/Dotnet caches and all seven root-declared submodules initialized at their gitlinks.

- Release solution build: PASS, 0 warnings and 0 errors.
- Broad regression: PASS across 18 unit/focused projects. Key totals were Contracts 702,
  Client 673, DomainService 143, QueryRouting 6, REST generator 124, Sample 117,
  Testing 150, and Server 2,620 passed / 25 skipped.
- Additional regression: PASS; Testing.Integration 44/44, AppHost 48/48, and Admin UI E2E
  39 passed / 2 skipped.
- Full live-sidecar: **FAIL**, 42 passed / 2 failed. The persisted batching, erasure,
  cutover, and paged-rebuild scenarios passed.
- Isolated `NamedProjectionDispatchLiveSidecarTests`: **FAIL**, 5 passed / 1 failed.
- Single `NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger`:
  **FAIL**, 0 passed / 1 failed. The persisted lifecycle hash remained present instead of
  returning to the idle/absent baseline.
- Post-attempt source identity and regular/untracked cleanliness: PASS at the same SHA.

The query-provenance E2E, package build/consumer, container publish/inspect, and owner
disposition gates were not run after the reproducible live regression failure. They cannot
turn a failing runtime into an approved pin.

## Final Decision

`still blocked`

Story 1.20 and Epic 1 remain `in-progress`. The clean candidate failed a production-path
gate, every parity row remains non-authorizing, source/package/container identities are
unapproved or unresolved, Story 1.16 follow-up disposition is open, and owner approval is
absent.
