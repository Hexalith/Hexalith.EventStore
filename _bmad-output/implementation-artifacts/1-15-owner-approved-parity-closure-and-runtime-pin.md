---
created: 2026-07-15
story_id: "1.15"
story_key: 1-15-owner-approved-parity-closure-and-runtime-pin
historical_story_id: "1.15"
superseded_by: 1-20-owner-approved-parity-closure-and-runtime-pin.md
---

# Story 1.15: Owner-Approved Parity Closure And Runtime Pin

> Historical planning artifact. The unstarted closure story is reissued as Story 1.20,
> whose acceptance criteria adopt AD-22's exact source/package/container identity rules.

Status: ready-for-dev

**Requirements covered:** FR36, NFR12, NFR16  
**Governed by:** AD-7, AD-8, AD-12, AD-14, AD-15, AD-19, AD-20  
**Depends on:** Stories 1.9-1.14 complete and reviewed; Story 2.8 complete before Story 1.11 evidence is accepted  
**Produces:** `_bmad-output/implementation-artifacts/1-15-owner-approved-parity-closure-proof-packet.md`

> **Current execution gate (2026-07-15): `still blocked`.** Stories 1.9-1.13 and 2.8 are `done`, but Story 1.14 is `in-progress`, its Task 7 and review are incomplete, and its candidate runtime/test changes are not contained in `HEAD` (`fc1b930a84d9ba5fad34f8d059afe46d3d5b9ea3`). This story is ready for a developer to prepare and drive the closure, but it cannot record `available`, approve the current SHA, or become `done` until the prerequisites, clean-commit evidence, and owner review below are satisfied. Story-creation authorization is not proof-result approval.

## Story

As an EventStore platform owner,  
I want a reviewed parity-closure packet tied to an exact runtime commit,  
so that Parties Story 8.6 resumes only against capabilities that are implemented, verified, and approved.

## Acceptance Criteria

1. **Prerequisites are complete and reviewed.** When closure begins, Stories 1.9-1.14 are complete and reviewed, Story 2.8 is complete before Story 1.11 evidence is accepted, and every public API or compatibility decision is recorded. Stale status text, open review recommendations, and deferred-work contradictions are explicitly reconciled rather than silently ignored.

2. **Every parity item receives a final classification.** The successor packet re-evaluates read-model/checkpoint erasure, coordinated batching, the six-state lifecycle, duplicate/out-of-order production-handler safety, full rebuild equivalence, cursor compatibility, asynchronous persistence, and multiple projections per domain. Every row is either `available` with accepted evidence and limitations or the packet's final decision remains `still blocked`; there is no partial migration authorization.

3. **Evidence proves production behavior.** Each row records implementation source paths, test paths, exact commands and results, inspected persisted-state evidence, environmental/runtime details, residual limitations, and rollback guidance. Mock-only evidence, isolated `AggregateReplayer` proof, HTTP success alone, or aggregate-only replay cannot close a handler-path or persisted-state requirement.

4. **An EventStore owner reviews the proof result.** The packet records the named EventStore reviewer, approval date, durable approval source or PR, accepted scope, accepted residual limitations, and an explicit decision on consumer migration. The reviewer assesses the completed exact-SHA evidence; generating or approving this story is not equivalent to approving the proof result.

5. **One exact runtime commit is approved.** The approved SHA contains the reviewed implementations and evidence for Stories 1.9-1.14 and Story 2.8. The pre-test and post-test working tree is clean, test results correspond to that same commit, and any later documentation-only packet commit is recorded separately without replacing the tested runtime SHA.

6. **Parties verifies both its pin and checkout.** The handoff requires the Parties repository's `references/Hexalith.EventStore` gitlink and checked-out submodule commit to equal the approved runtime SHA. A mismatch leaves Story 8.6 blocked. EventStore approval does not change the Parties submodule, delete Parties rollback code, or authorize an EventStore agent to do either.

7. **Completion fails closed.** If any prerequisite, parity row, production-path proof, exact-SHA gate, or owner decision remains unresolved, the successor packet records `still blocked`, Story 1.15 remains `in-progress`, the precise blocking condition and a scoped corrective item are created, and Epic 1 remains `in-progress`. Story 1.15 and Epic 1 become `done` only after the packet decision is `available` and all preceding criteria are met.

## Tasks / Subtasks

- [ ] **Task 1 - Reconcile prerequisite completion and review authority** (AC: 1, 7)
  - [ ] Re-read `sprint-status.yaml`, Stories 1.9-1.14, Story 2.8, their frozen specs, review sections, `deferred-work.md`, and existing evidence documents at execution time. Do not rely on this story's 2026-07-15 snapshot.
  - [ ] Require Stories 1.9-1.14 and 2.8 to be `done` with a review outcome. In particular, reconcile Story 1.11's `followup_review_recommended: true`, Story 1.14's story-header/sprint mismatch and stale dependency notes, and the later evidence that supersedes Story 1.10's originally blocked live lane.
  - [ ] Record the accepted compatibility decisions: additive erase/batch/lifecycle surfaces; legacy query metadata ABI; versioned `/project/v2` plus legacy `/project`; exact `(Domain, ProjectionType)` routing; the Story 1.13 no-mixed-writer maintenance cutover; and the Story 1.14 rebuild semantics once reviewed.
  - [ ] If a prerequisite is not complete and reviewed, create or update the successor packet with `final_decision: still blocked`, name the blocker/corrective owner, move Story 1.15 to `in-progress`, and stop before approval or Parties authorization.

- [ ] **Task 2 - Freeze a clean tested runtime identity** (AC: 3, 5)
  - [ ] Select the future commit that contains the accepted Story 1.9-1.14 and 2.8 runtime plus tests; do not pin the historical Story 1.8 SHA, a release-only commit, a dirty worktree, or uncommitted Story 1.14 changes.
  - [ ] Before running gates, capture the full commit, `git status --porcelain=v1 --untracked-files=all --ignore-submodules=none`, root submodule status, `dotnet --info`, Dapr CLI/runtime/package versions, Redis version/image digest, and the exact state-store component/capability profile.
  - [ ] After all gates, prove `HEAD` is the same commit and the working tree is still clean. `git diff --quiet` alone is insufficient because it omits untracked files and can hide submodule drift.
  - [ ] If the proof packet is committed later, record `tested_runtime_sha` and `documentation_commit_sha` as distinct fields and state that the latter is documentation-only.

- [ ] **Task 3 - Build the successor parity matrix from historical and current evidence** (AC: 2, 3)
  - [ ] Preserve `1-8-projection-query-sdk-owner-parity-proof.md` as the historical `still blocked` packet. Create the versioned successor at `1-15-owner-approved-parity-closure-proof-packet.md`; do not overwrite the old decision or SHA.
  - [ ] For read-model/checkpoint erasure, cite the conditional eraser, canonical address factory, erase coordinator, checkpoint stores, lifecycle admission, deterministic tests, and `ProjectionEraseLiveSidecarTests`; disclose accepted TOCTOU/admission-race, caller-slot, authenticated stitched-E2E, and global-admin limitations.
  - [ ] For coordinated batching, cite `IReadModelBatchStore`, `ReadModelBatchProtocol`, Dapr/in-memory implementations, deterministic tests, and the later green `ReadModelBatchLiveSidecarTests`; disclose store capability, ETag, corrupt-envelope, abandoned-envelope, terminal-receipt, and cross-profile boundaries.
  - [ ] For lifecycle/provenance, prove all six operational lifecycle values plus `Unknown`, route-bound `ProjectionBacked` authority, persisted freshness/version traversal, and the reviewed Story 2.8 path. Include reviewed Story 1.14 `Rebuilding` wiring; never infer lifecycle or projection version from an ETag.
  - [ ] For duplicate/out-of-order safety, cite the v2 delivery state, MessageId/sequence/fingerprint receipts, reservation fencing, reconciler, writer cutover marker, deterministic tests, `NamedProjectionDispatchLiveSidecarTests`, `ProjectionDeliveryCutoverLiveSidecarTests`, and `docs/operations/projection-delivery-v2-evidence.md`; state that legacy `/project` is outside this guarantee.
  - [ ] For rebuild equivalence, require reviewed Story 1.14 production orchestrator/dispatcher/batch-store evidence for more than two pages, empty/exact/bounded streams, cancellation, failure, persisted resume, safety bounds, lifecycle, promotion, and persisted actor/detail/index/freshness/checkpoint equivalence. The existing dirty candidate and mock-backed deterministic harness are not exact-SHA approval evidence.
  - [ ] For cursor compatibility, cite `IQueryCursorCodec`, `QueryCursorCodec`, `QueryCursorScope`, Data Protection registration, and exact-SHA Client/DomainService tests; distinguish routine retained-key rotation from key-ring loss.
  - [ ] For asynchronous persistence and multiple projections per domain, cite the async handler seam, v2 contracts, exact route catalog/fingerprint, dispatch coordinator, retry worker/outbox, deterministic tests, and named live-sidecar tests; explicitly accept or reject the recorded hand-written-state, work-identity, dead-letter/terminal-cleanup, and null-metadata residuals.
  - [ ] Add one cross-cutting row for public API, serialization, released-package, and legacy-route compatibility. Reconfirm the manifest-owned inventory contains exactly the 14 approved packages; do not edit `tools/release-packages.json` to make a test pass.

- [ ] **Task 4 - Re-run closure gates at the selected SHA** (AC: 3, 5)
  - [ ] Run the Release solution build only; run tests per project, never solution-level `dotnet test`. At minimum rerun Contracts, Client, DomainService, QueryRouting, Server, RestApi.Generators, Sample, Testing, Integration focused provenance, and Server.LiveSidecar suites needed by the matrix.
  - [ ] Run focused persisted/live lanes for erasure, batches, named dispatch, delivery cutover, Story 1.14 paged rebuild equivalence, and query provenance. Record exact commands, pass/fail/skip counts, runtime versions, component YAML/store capabilities, and the persisted keys/values or semantic end state inspected.
  - [ ] Pack the manifest inventory, validate exactly 14 `.nupkg` files, and build the temporary package-only consumer from those packages. Record the synthetic package version and confirm no project reference substituted for a package.
  - [ ] Treat an unavailable or failed production-path lane as a closure blocker unless the EventStore owner explicitly records a requirement-valid alternative. Deterministic evidence may diagnose a failure but cannot silently replace an acceptance-mandated persisted path.
  - [ ] Record each limitation against the row it affects. Do not copy historical test counts or environment claims as though they were rerun at the approved SHA.

- [ ] **Task 5 - Author the auditable closure packet** (AC: 2-5)
  - [ ] Include packet schema/version, creation/update dates, historical-packet link, `tested_runtime_sha`, optional documentation-only SHA, clean-tree proof before/after, tool/runtime/store inventory, and the prerequisite/review ledger.
  - [ ] Give each parity row: requirement/architecture links, final classification, source paths, test paths, exact commands/results, persisted-state observation, compatibility decision, residual limitations, rollback action, and owner disposition.
  - [ ] Make the final decision machine-obvious: exactly `available` or `still blocked`. `available with follow-up`, `mostly available`, and equivalent partial-authority labels are invalid.
  - [ ] Preserve the rollback boundary: Parties retains its local projection/query actors, rebuild services, freshness adapters, erasure paths, and rollback implementation until the packet is `available` and its EventStore gitlink/checkout matches the approved SHA.

- [ ] **Task 6 - Obtain proof-result owner review** (AC: 4, 7)
  - [ ] Present the completed exact-SHA packet to a named authorized EventStore owner. Record reviewer identity, approval date, durable source/PR, accepted capability scope, every accepted residual limitation, and explicit `authorize_parties_migration: true|false`.
  - [ ] Approval must cover the evidence and exact runtime commit, not merely wording, story creation, or an earlier implementation review.
  - [ ] If any row or limitation is rejected, keep `still blocked`, create a narrowly scoped corrective item in the owning story/deferred ledger, and do not implement an ad hoc runtime fix inside this evidence-only story.

- [ ] **Task 7 - Produce the Parties handoff without changing Parties** (AC: 6)
  - [ ] In the packet, provide the approved SHA and commands for a Parties maintainer to compare both the superproject gitlink and the checked-out `references/Hexalith.EventStore` commit. Both values must equal the approved SHA.
  - [ ] State that a dirty EventStore submodule, detached checkout at another SHA, or gitlink/checkout mismatch leaves Story 8.6 blocked.
  - [ ] Do not modify `references/Hexalith.Tenants`, any other root submodule, the Parties repository, or Parties rollback code as part of this story.

- [ ] **Task 8 - Close status only after the decision is available** (AC: 7)
  - [ ] If `final_decision: available`, update Story 1.15 to `done`, update its sprint entry to `done`, and move Epic 1 to `done` only after confirming no other Epic 1 story is non-done.
  - [ ] If `final_decision: still blocked`, keep Story 1.15 `in-progress`, Epic 1 `in-progress`, and record the exact corrective item. Never mark the story `done` merely because a blocked packet was written.

## Dev Notes

### Scope and implementation boundary

Story 1.15 is an evidence, review, and pinning story. Its normal production-code delta is **zero**. Expected changes are the successor proof packet, this story's execution record/status, narrow sprint-status bookkeeping, and—only after approval—a successor link from a relevant evidence index or a separate addendum. If analysis finds a runtime defect, record a corrective item in the owning story/ledger and keep this story blocked; do not mix an unreviewed fix into the closure commit.

The historical Story 1.8 packet is immutable evidence of the earlier blocked decision. Its SHA `f31777...` and the current release-only `fc1b930a...` are not closure candidates. Current uncommitted Story 1.14 changes belong to the active implementation and must be preserved.

### Current prerequisite and evidence state

| Area | 2026-07-15 state | Closure instruction |
| --- | --- | --- |
| Stories 1.9-1.13 | `done`; implementation/review evidence exists | Reconcile residuals and rerun material gates at the final SHA. |
| Story 2.8 | `done` after review | Accept before relying on Story 1.11 provenance/lifecycle evidence. |
| Story 1.14 | `in-progress`; Tasks 1-6 recorded green, Task 7/review/final commit absent | Hard blocker. Finish, independently review, commit, and rerun exact-SHA persisted evidence. |
| Story 1.15 packet | Does not yet exist | Create the named successor; initial decision is `still blocked`. |
| Epic 1 | `in-progress` | Cannot become `done` until the successor decision is `available`. |

Do not trust stale artifact text without reconciling it. Known examples include: Story 1.11's follow-up-review recommendation; Story 1.10's checked live task versus its originally blocked log (superseded only by later live runs); Story 1.12's obsolete live-class name; Story 1.13 evidence spanning a baseline, uncommitted changes, and a later HEAD; Story 1.14's stale header/dependency prose; and deferred-work rows already resolved by later stories. Preserve history, add explicit supersession links, and do not rewrite old evidence into a false single-SHA narrative.

### Capability reuse and anti-reinvention map

- **Erasure:** extend/cite `IReadModelConditionalEraser`, `DaprReadModelStore`, `ProjectionReadModelAddressFactory`, `ProjectionEraseCoordinator`, `ProjectionCheckpointTracker`, `ProjectionRebuildCheckpointStore`, and persisted lifecycle admission. Do not create another eraser or key convention.
- **Batching:** cite `IReadModelBatchStore`, `ReadModelBatchProtocol`, existing Dapr/in-memory stores, stable batch identity/fingerprint, and resumable marker semantics. Do not claim cross-store atomicity or invent a second transaction profile.
- **Lifecycle/provenance:** cite `ProjectionLifecycleState`, `ProjectionLifecyclePolicy`, `QueryResponseProvenance`, `ReadModelFreshnessExtensions`, the routers/controller/client/generator, and persisted lifecycle storage. Only `ProjectionBacked` is authoritative; `LocalOnly` is never projection-confirmed.
- **Named delivery:** cite `IAsyncDomainProjectionHandler`, exact route catalog/fingerprint, `NamedProjectionDispatchCoordinator`, retry/outbox, and the v2 delivery idempotency coordinator/store/reconciler. Preserve stable ordering, sibling independence, v1 compatibility, and the no-mixed-writer cutover.
- **Rebuild:** cite the reviewed Story 1.14 complete-prefix, staging/promotion, lifecycle, safety-bound, equivalence-oracle, and production-path artifacts. Paging is only a bounded-read implementation detail; it cannot change replay semantics.
- **Cursor:** cite the existing codec/scope/Data Protection path. Do not expose or interpret opaque cursor contents.

### Exact-SHA and Parties verification commands

```bash
# EventStore: run before and after the recorded gates.
tested_sha="$(git rev-parse --verify --end-of-options 'HEAD^{commit}')"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"

# ...run and record the gates...

test "$tested_sha" = "$(git rev-parse --verify --end-of-options 'HEAD^{commit}')"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"

# Parties repository: both outputs must equal the approved EventStore SHA.
git ls-tree --object-only HEAD references/Hexalith.EventStore
git -C references/Hexalith.EventStore rev-parse --verify --end-of-options 'HEAD^{commit}'
```

`rev-parse --verify ...^{commit}` proves the name resolves to a commit. Porcelain v1 is stable for scripting; explicit untracked/submodule options prevent local configuration from hiding drift. The Parties gitlink and checkout checks are intentionally separate.

### Validation commands

Run against the clean selected commit and record the actual results rather than the illustrative project list alone:

```bash
dotnet --info
dapr --version
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0

dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj -c Release --no-restore
dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj -c Release --no-restore

# Persisted Dapr/Redis parity lane; record runtime/store details and focused-class results.
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Release --no-restore

# Manifest/package compatibility. The wrapper normalizes the synthetic version for consumer validation.
package_dir="$(mktemp -d)"
python3 scripts/pack-release-packages.py "$package_dir" 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py "$package_dir"
python3 scripts/validate-consumer-package-references.py "$package_dir"

git diff --check
```

For Story 2.8's Aspire-hosted provenance proof, reuse its documented IntegrationTests prerequisites and exact focused command rather than fabricating metadata carriers. For Story 1.14, require the committed focused class containing the more-than-two-page persisted Redis equivalence case. A green full live suite is useful but does not replace row-level persisted observations in the packet.

### Architecture, compatibility, and rollback guardrails

- AD-7/AD-19: read-model writes, erasure, cursor scope, and `(Domain, ProjectionType)` routes remain explicit. Domain-only ambiguous routing is not closure evidence.
- AD-8/AD-12/NFR16: delivery is at-least-once and may be unordered; durable persisted state after the real handler path is the acceptance evidence. Response codes and mock call counts are diagnostic only.
- AD-15: lifecycle/freshness is route-bound. ETags are opaque cache validators, never projection version or lifecycle evidence.
- AD-20: rebuild output, versions, and checkpoints equal canonical replay for the same prefix; live state survives incomplete work and promotion follows all required projection durability.
- NFR12: preserve released shapes, legacy `/project`, additive adapters, and the 14-package manifest. No package or actor-SDK migration belongs in closure.
- The release inventory is exactly `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, `Hexalith.EventStore.Server`, `Hexalith.EventStore.SignalR`, `Hexalith.EventStore.Testing`, `Hexalith.EventStore.Testing.Integration`, `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults`, `Hexalith.EventStore.DomainService`, `Hexalith.EventStore.RestApi.Generators`, `Hexalith.EventStore.Gateway`, `Hexalith.EventStore.Admin.Abstractions`, `Hexalith.EventStore.Admin.Cli`, and `Hexalith.EventStore.Admin.Server` as governed by `tools/release-packages.json`.
- Delivery v2 rollback requires a maintenance boundary. Before the global marker, stop writers, verify a backup, and restore/redeploy the whole old fleet. After the marker, rolling downgrade is forbidden: stop all writers, preserve regressed rows, restore a complete backup to an isolated/replacement store, and move the fleet together. Never lower/delete the marker during scoped erasure.
- For an ambiguous batch, retry/reconcile the same batch identity and profile. Do not call cancellation rollback or switch profiles. Rebuild cancellation/failure must preserve the previous complete live state.

### Stack and current technical notes

- The repository pins .NET SDK `10.0.302` with `rollForward: latestPatch` and targets `net10.0`. Capture `dotnet --info`: `latestPatch` selects the latest installed patch in the 10.0.3xx feature band, so the executed SDK/runtime cannot be inferred from `global.json` alone.
- Used Dapr .NET packages are pinned at `1.18.4`; the integration workflow pins runtime `1.18.0`. Preserve and record the tested combination instead of upgrading during closure. `Dapr.Actors.Next` changes hosting, dispatch, and serialization; actor migration is out of scope.
- Dapr state transactions are atomic only within one transaction request to a store advertising transaction support. ETags opt into optimistic concurrency; omitted ETags are last-write-wins. Do not claim atomicity across separate Dapr calls or stores.
- Actor turn serialization is per actor ID, not a global delivery-order guarantee. Service invocation can retry, so persisted duplicate/out-of-order and partial-failure evidence remains mandatory.
- Dapr 1.18 includes a service-invocation path-traversal ACL fix. For Kubernetes/control-plane rollback from 1.18, do not roll below 1.17.7 because older Sentry versions cannot read the newer Ed25519 workload-identity material.

### UX impact

No EventStore UI implementation belongs in Story 1.15. The packet must nevertheless prove the consumer-facing lifecycle mapping: `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`, with `Unknown` as the fail-safe fallback. Only `ProjectionBacked` evidence may authorize projection lifecycle claims; `HandlerComputed`/`Unknown` render as unknown, and `LocalOnly` never becomes projection-confirmed success. Parties UI or local-infrastructure removal remains a consumer-side change after explicit migration authorization and SHA verification.

### Previous-story and Git intelligence

- Story 1.8 established the blocked matrix and rollback boundary; Story 1.15 must produce a successor, not reinterpret 1.8 as success.
- Stories 1.9-1.13 supply the erasure, batch, lifecycle, async named-dispatch, and idempotency evidence. Their review findings and accepted residuals are inputs to the owner decision, not details to omit after tests turn green.
- Story 1.14 is the nearest prior story. Its active candidate adds complete-prefix bounded paging, named rebuild plans, coordinated promotion, persisted `Rebuilding`, safety limits, and a production-path equivalence harness. Until the change is committed and independently reviewed, it is intelligence—not approved evidence.
- Recent history is mostly release and submodule bookkeeping. The latest substantive parity commit is `1a01e0ea` (Story 1.13); `fc1b930a` only releases 3.64.2 and does not contain Story 1.14. Do not infer parity from a release number or recent HEAD.

### Project Structure Notes

- NEW: `_bmad-output/implementation-artifacts/1-15-owner-approved-parity-closure-proof-packet.md`.
- UPDATE: this story's execution record/status and `_bmad-output/implementation-artifacts/sprint-status.yaml` with narrow, state-valid transitions.
- OPTIONAL AFTER APPROVAL: a non-destructive successor link from a relevant evidence index or a separate addendum; keep the historical Story 1.8 packet unchanged.
- No normal changes under `src/`, `tests/`, `references/`, `tools/release-packages.json`, `Directory.Packages.props`, AppHost, Dapr YAML, or consumer repositories.
- Follow repository rules: `.slnx`, central package versions, one C# type per file, XML docs on public/internal members, xUnit v3 + Shouldly, per-project test execution, `ConfigureAwait(false)` in production code, ULID-safe identifiers, and no recursive submodule initialization.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1.15-Owner-Approved-Parity-Closure-And-Runtime-Pin`] — canonical story and acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/prd.md#Functional-Requirements`] — FR36; NFR12 and NFR16; consumer-deletion proof boundary.
- [Source: `_bmad-output/planning-artifacts/architecture.md`] — AD-7, AD-8, AD-12, AD-14, AD-15, AD-19, AD-20 and pinned stack.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md#4.10`] — closure rewrite and successor-packet requirements.
- [Source: `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`; `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`] — historical blocked decision, capability matrix, and rollback boundary.
- [Source: `_bmad-output/implementation-artifacts/1-9-read-model-and-projection-checkpoint-erasure.md`; `1-10-coordinated-read-model-batch-writes.md`; `spec-1-11-complete-projection-freshness-lifecycle.md`; `1-12-asynchronous-multi-projection-dispatch.md`; `1-13-projection-handler-delivery-idempotency.md`; `1-14-correct-paged-rebuild-and-replay-equivalence.md`] — prerequisite implementation, evidence, residuals, and review history.
- [Source: `_bmad-output/implementation-artifacts/2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md`] — reviewed route-provenance prerequisite.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`; `docs/operations/projection-delivery-v2-evidence.md`] — corrective ledger and bounded persisted delivery evidence.
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`] — six-state lifecycle presentation, provenance authority, and fail-safe fallback behavior.
- [Source: `_bmad-output/project-context.md`] — repository build, testing, identity, and code rules.
- [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0); [`global.json` roll-forward](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) — SDK/runtime capture rules.
- [Dapr .NET SDK 1.18.4](https://github.com/dapr/dotnet-sdk/releases/tag/v1.18.4); [Dapr support policy](https://docs.dapr.io/operations/support/support-release-policy); [State API](https://docs.dapr.io/reference/api/state_api/); [Actors overview](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/) — pinned-version and persisted-evidence boundaries.
- [`git rev-parse`](https://git-scm.com/docs/git-rev-parse); [`git status --porcelain`](https://git-scm.com/docs/git-status); [`git ls-tree`](https://git-scm.com/docs/git-ls-tree); [Git submodules](https://git-scm.com/docs/gitsubmodules) — exact commit, clean-tree, and Parties gitlink verification.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

- `_bmad-output/implementation-artifacts/1-15-owner-approved-parity-closure-and-runtime-pin.md`
