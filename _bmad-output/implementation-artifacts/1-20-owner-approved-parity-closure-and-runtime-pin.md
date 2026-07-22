---
created: 2026-07-15
story_id: "1.20"
story_key: 1-20-owner-approved-parity-closure-and-runtime-pin
status: blocked
baseline_revision: 26842d284f2da91399b7891bf7b5880ce2f6b561
followup_review_recommended: true
supersedes: 1-15-owner-approved-parity-closure-and-runtime-pin.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.20: Owner-Approved Parity Closure And Runtime Pin

Status: blocked

## Reissue Decision

This is the active identity for the unstarted historical Story 1.15. The earlier file
retains its discovery notes. Execution must re-read current status and evidence rather
than treating story creation as owner approval.

## Acceptance Boundary

1. Stories 1.14-1.19 are complete and reviewed, Story 1.2 platform provenance is
   complete before lifecycle/provenance evidence is accepted, and Story 1.16 additionally
   has a dated follow-up-review disposition tied to the candidate runtime. Its historical
   `spec-1-11...` filename does not weaken active identity 1.16.
2. Every parity capability is classified `available` or the packet remains `still blocked`;
   no partial consumer migration is authorized.
3. Evidence records source/test paths, exact commands, persisted read-back, environment,
   limitations, and rollback guidance; mock-only or HTTP-only proof cannot close a row.
4. A named EventStore owner reviews the completed exact-SHA evidence and records approval,
   date, durable source, accepted scope, limitations, and migration decision.
5. Before a runtime SHA is selected, the committed candidate satisfies architecture AD-11,
   including SDK `10.0.302`, ASP.NET `10.0.10`, and an installed
   `Microsoft.NETCore.App` `10.0.10` runtime, or a later replacement documented with
   the named architecture owner, approval date, durable source, rationale and exact
   candidate/toolchain/ASP.NET/runtime scope, and an unexpired `expires_at` value. The executable
   readiness preflight rejects a mismatched exact baseline and any missing, blank,
   malformed, expired, or out-of-scope replacement record before candidate gates. Any
   required pin correction belongs to scoped build/release corrective work, not this
   evidence-only story.
6. The selected candidate is tested from a clean detached checkout. The same 40-hex commit
   is present before and after every production-path, package, and container gate.
7. `tested_runtime_sha` identifies the unchanged runtime commit and equals A's
   `candidate_source_sha`; that real commit is the sole direct parent of evidence commit A,
   whose changed paths are restricted to `_bmad-output/`. Final durable approvals precede A,
   which records the results, hybrid evidence manifest/raw-bundle pins, artifact identities,
   and approval references while keeping
   `documentation_commit_sha: null` and all decision/migration guards blocked. Pointer-only
   commit B, whose direct parent is A, changes only `documentation_commit_sha` to A's 40-hex
   SHA. The field never identifies B, so neither commit self-references. Authorizing commit C
   is B's direct child and may change only the verified packet decision/migration fields plus
   the story status and exact sprint status/date/blocker-comment reconciliation after every A
   identity, prerequisite, and approval is revalidated.
8. Under AD-22, the packet separately pins the exact EventStore source SHA; all 14 NuGet
   package IDs, one exact version, and SHA-256 per package; and the container repository,
   immutable digest whose value equals the raw-manifest SHA-256, exact `linux/amd64` and
   `linux/arm64` platform set, and provenance mapping to the tested runtime SHA. Consumer
   repositories verify both gitlink and checkout against the approved source SHA, or those
   exact package/container identities when that is the approved consumption mode.
9. Story 1.16 follow-up review and the final Story 1.20 packet each receive the required
   named review. External container publication requires a durable release-owner authority
   record created before the registry operation and naming the owner, date, durable source,
   rationale, exact repository/tag/source-SHA scope, and an unexpired `expires_at` value.
   The record is copied and hashed, then revalidated at a fresh action timestamp immediately
   before publication after the candidate HEAD and source cleanliness are rechecked. Ignored
   inputs are limited to generated `bin`/`obj`; the authority record, action time, hashes, and
   actual publish properties are bound into provenance. Missing, blank, malformed, expired,
   or out-of-scope authority is rejected. This
   evidence-integrity preflight neither grants human authority nor replaces registry access
   control. After all evidence exists, the distinct release-owner disposition and named
   EventStore-owner approval must exist durably before evidence commit A.
10. Any unresolved prerequisite, security baseline, review, runtime identity,
    production-path result, package/container pin, publication authority, owner decision,
    evidence commit A, valid pointer-only commit B, or valid authorizing commit C keeps
    `final_decision: still blocked`, `authorize_consumer_migration: false`, `status: blocked`,
    Story 1.20 non-`done`, and Epic 1 `in-progress`.

Produces: `1-20-owner-approved-parity-closure-proof-packet.md`.

## Closure Execution Order

1. Preserve the verified lifecycle-cleanup and AD-11 corrections in the candidate lineage.
2. Complete Story 2.7's pre-authorization correction for stale sample registrations and the
   source-topology query-provenance gate, without changing consumer dependency identities.
3. Select the resulting clean committed runtime SHA.
4. Run and disposition Story 1.16 follow-up review against that SHA.
5. Run all detached exact-SHA persisted production-path gates.
6. Build and hash the exact 14-package inventory.
7. Recheck the candidate HEAD and clean tracked/untracked source, allowing ignored inputs only
   under generated `bin`/`obj`; at a fresh action timestamp revalidate a durable release-owner
   authority record naming the owner, date, source, rationale, exact repository/tag/source-SHA
   scope, and `expires_at` value. Reject missing, blank, malformed, expired, out-of-scope, or
   dirty input before publication. Pin the raw manifest hash to the immutable digest and require
   exactly `linux/amd64` and `linux/arm64` before accepting container inspection evidence.
8. After all results exist, obtain the named EventStore-owner proof approval and the
   release-owner's distinct final disposition in durable external sources.
9. Upload the raw logs to immutable external storage, commit the critical identity/provenance
   manifest under `_bmad-output`, and bind both by URL and SHA-256. Create evidence commit A
   recording those pins, results, artifact identities, and approval references while preserving
   `documentation_commit_sha: null`, `final_decision: still blocked`, and
   `authorize_consumer_migration: false`.
10. Create direct-child pointer-only commit B changing only `documentation_commit_sha` to
    A's 40-hex SHA. Verify A is a single-parent evidence-only child of its equal
    candidate/tested-runtime identity and verify B's one-field diff.
11. Create authorizing commit C as B's direct child. Permit only the exact packet
    decision/migration changes, Story 1.20/Epic 1 status transition, non-regressing tracker date,
    and replacement of the exact fail-closed blocker-comment block with the verified closure
    statement; preserve file modes and revalidate every A prerequisite, evidence, package,
    container, capability-row, and durable approval identity. Until C passes, retain
    `status: blocked` and the current sprint/Epic states.

## Current Sprint-Change Proposal Implementation File Inventory

This inventory describes the current Story 1.16/1.20 proposal implementation. It is
separate from the historical Auto Run `Files changed` list and Dev Agent `File List` below.

| File | Current proposal action |
| --- | --- |
| `1-20-owner-approved-parity-closure-proof-packet.md` | Preserve failed-run evidence and guards; add the observed audit, executable AD-11 gate, hybrid durable-evidence retention, the non-self-referential A/B/C authorization protocol, and the fail-closed publication-authority evidence procedure. |
| `1-20-owner-approved-parity-closure-and-runtime-pin.md` | Record acceptance, authority, commit sequencing, current inventory, and blocked closure order. |
| `deferred-work.md` | Reconcile lifecycle and AD-11 as implementation-complete/evidence-confirmed and record Story 2.7's source-topology provenance blocker without duplication. |
| `sprint-status.yaml` | Preserve the approved Story 1.20 blocker comments and `in-progress` statuses; refresh `last_updated`. |
| `spec-1-11-complete-projection-freshness-lifecycle.md` | Verify only; retain `followup_review_recommended: true` with no disposition edit. |
| `epic-1-context.md` | Restore canonical endpoints and retain durable behavior, release, and compatibility constraints. |
| `spec-1-16-1-20-sprint-change-proposal.md` | Track this implementation and verification; preserve the frozen approval block unchanged. |

Concurrent runtime, test, branch, submodule, Parties, and persisted-data work is excluded
from this proposal inventory and remains owned by its existing changes.

## Review Triage Log

### Review Findings

#### 2026-07-22 — Code review chunk 1: production runtime and unit tests

- [x] [Review][Patch] [high] Preserve the public `DaprDomainServiceInvoker` constructor contract when injecting `TimeProvider` [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:21]
- [x] [Review][Patch] [medium] Configure the named domain-service client's infinite timeout during registration instead of mutating each factory-created client [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:73]
- [x] [Review][Patch] [low] Reconcile `DomainServiceException` documentation with invocation failures and document the new internal constructor [src/Hexalith.EventStore.Server/DomainServices/DomainServiceException.cs:3]
- [x] [Review][Patch] [medium] Resolve and exercise both digest-key provider branches through the `AddEventStoreServer` service graph [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:39]
- [x] [Review][Patch] [medium] Verify the four idempotency actor registrations and their stable actor type names [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:204]
- [x] [Review][Patch] [medium] Prove the invoker requests exactly the dedicated no-retry HTTP client [tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:143]
- [x] [Review][Patch] [low] Bind host-startup timeout assertions to `DomainServiceOptions` and the expected validation failure [tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:97]
- [x] [Review][Patch] [low] Move `NoOpProjectionActivationOutbox` to its own source file to preserve the one-type-per-file rule [src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs:51]
- [x] [Review][Patch] [medium] Register the built-in idempotency admission validator additively so a consumer validator cannot replace it [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:102]
- [x] [Review][Patch] [medium] Reject undefined `DigestKeySource` values during startup validation [src/Hexalith.EventStore.Server/Configuration/ValidateIdempotencyAdmissionOptions.cs:31]
- [x] [Review][Patch] [medium] Fail deterministically when direct invoker construction supplies an unsupported invocation timeout [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:62]
- [x] [Review][Patch] [medium] Pin the named HTTP client's infinite timeout with a regression test covering accepted long invocation windows [tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:158]
- [x] [Review][Patch] [medium] Prove invalid idempotency admission configuration fails through `AddEventStoreServer` at host startup [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:103]
- [x] [Review][Patch] [medium] Assert event 3004 and its timeout-classification fields for configured and upstream cancellations [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:226]
- [x] [Review][Defer] [medium] Validate pre-existing `MaxEventsPerResult` and `MaxEventSizeBytes` bounds during startup [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:107] — deferred, pre-existing

Applied and verified all 14 patch findings on 2026-07-23. The pre-existing response-limit
validation gap remains owned by the deferred-work ledger. Story status remains `blocked`
because the acceptance boundary's external exact-SHA and owner-approval gates remain open.

#### 2026-07-22 — Follow-up adversarial review of the hot-reload readiness correction

- [x] [Review][Patch] [high] Bind the retained corrective-run claims to the fresh explicit three-method-plus-class rerun commands, XML/log timestamps, result locations, and SHA-256 digests under `/tmp/story-1-20-code-review-final-evidence-20260722` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:5562]
- [x] [Review][Patch] [medium] Treat valid non-object JSON and non-string `errorCode` values as unrecognized Dapr responses instead of throwing `InvalidOperationException` [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:101]
- [x] [Review][Patch] [low] Catch the documented `OperationCanceledException` family for internally bounded HTTP cancellation so timeout failures retain the readiness helper's diagnostics [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:74]
- [x] [Review][Patch] [medium] Preserve the last unexpected response body or Dapr error code in readiness-timeout diagnostics instead of reporting only HTTP 500 [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:91]
- [x] [Review][Patch] [medium] Give the outer hot-reload operation a budget greater than the newly possible stop-readiness plus Aspire-health plus start-readiness waits [tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs:24]
- [x] [Review][Patch] [medium] Add deterministic coverage for wrong status, malformed/non-object JSON, non-string/wrong `errorCode`, timeout, and parent-cancellation branches in the new readiness state machine [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbeTests.cs:11]
- [x] [Review][Patch] [low] Repair the completed-review traceability links and narrow the false claim of app-id independence to the fixed `sample` AppHost topology actually probed [tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs:239]
- [x] [Review][Patch] [medium] Consolidate the duplicate Tier-3 CI defer into the original deferred-work item and reconcile that item's obsolete shared-fixture, two-test, and unresolved-DCP-stop description [_bmad-output/implementation-artifacts/deferred-work.md:114]
- [x] [Review][Defer] [high] Tier-3 hot-reload readiness still has no PR/push workflow that executes the changed stop/restart path [.github/workflows/integration.yml:87] — deferred, pre-existing; owned by the dedicated Aspire-in-CI follow-up

#### 2026-07-22 — Code review of hot-reload readiness correction

- [x] [Review][Patch] Require the exact HTTP 500 Dapr `ERR_DIRECT_INVOKE` object/string response instead of accepting every non-success response [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:101]
- [x] [Review][Patch] Use a side-effect-free operational-index request with an empty domain filter while explicitly retaining the AppHost's fixed `sample` Dapr target [tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs:248]
- [x] [Review][Patch] Use repository-required `ConfigureAwait(false)` in the non-test readiness helper and its callers [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:55]
- [x] [Review][Patch] Keep timeout diagnostics bound to the most recent response or transport failure [tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs:45]
- [x] [Review][Patch] Refresh the proof-packet frontmatter update timestamp for the 2026-07-22 audit entry [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:6]
- [x] [Review][Defer] Tier-3 hot-reload readiness remains outside PR/push CI [.github/workflows/integration.yml:87] — deferred, pre-existing; the dedicated Aspire-in-CI follow-up and existing HotReload deferred-work entry retain ownership

#### 2026-07-21 — Code review of landed corrective commit `bccc2560`

- [x] [Review][Patch] Production resilience timeouts override the configured domain-service invocation timeout [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:56]
- [x] [Review][Patch] The disposable security topology reuses and leaves behind a store-global writer-protocol marker [tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs:269]
- [x] [Review][Patch] The claimed conflict-status persistence proof still reads only an in-memory fake instead of persisted DAPR state [tests/Hexalith.EventStore.IntegrationTests/EventStore/ConcurrencyConflictIntegrationTests.cs:106]
- [x] [Review][Patch] The tenant-bootstrap test proves event persistence but not the hosted service's terminal success outcome [tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs:71]
- [x] [Review][Patch] The timeout regression test bypasses the production resilience pipeline and relies on a flaky wall-clock ceiling [tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:123]
- [x] [Review][Patch] Eager startup validation and the accepted one-second timeout boundary are not pinned by host-level tests [tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:78]
- [x] [Review][Patch] Unsafe command-POST retry suppression has no deterministic attempt-count verification [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs:90]
- [x] [Review][Patch] Writer-protocol retries for 408, 429, and 5xx activation responses have no deterministic coverage [tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs:316]
- [x] [Review][Patch] Newly added integration-test awaits omit the repository-required `ConfigureAwait(false)` [tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs:46]
- [x] [Review][Patch] New domain-service cancellation telemetry bypasses the required source-generated logging pattern [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:82]

#### 2026-07-19 — Exact-SHA execution corrections

- [x] [Review][Patch] [high] Add the tracked executable provider adapter required by the hybrid
  durable-evidence gate; the reviewed packet referenced
  `tools/evidence-provider-adapters/<id>.sh`, but no adapter existed on `main`. Added the
  authenticated, exact-version Azure immutable-blob adapter and its Contracts test in PR #301.
- [x] [Review][Patch] [high] Read xUnit v3 counters from the single `<assembly>` summary rather
  than the counter-less `<assemblies>` document root. The first isolated exact-SHA run passed
  its first filtered test 8/8 but the obsolete root-counter parser rejected it as zero tests;
  both the runtime gate and committed A/B/C recheck now validate the real schema, including
  `not-run`, child-result totals, and optional aggregate-counter reconciliation. A Contracts
  regression test extracts and executes the packet function against valid and adversarial XML.
- [x] [Review][Patch] [high] Scope the persisted retry-ledger convergence assertion to the
  aggregate's work item instead of requiring the entire 64-way shared shard to be empty. The
  exact-SHA delivery run landed on a shard containing legitimate terminal work from another
  aggregate, so the prior global-empty assertion failed after its own retry had converged. The
  live regression now deterministically co-locates unrelated terminal work and proves that only
  the completed aggregate's item is removed.
- [x] [Review][Patch] [high] Restore the release execution identity invariant after the root
  `Hexalith.Builds` submodule advanced to `ffa1662829b28d1d90554980c87f23bd9d4e25e7` while the
  reusable release workflow remained pinned to its previous commit. The caller and its explicit
  `builds-execution-sha` input now match the root-declared dependency exactly.
- [x] [Review][Patch] [high] Isolate every live-sidecar fixture run behind a unique Dapr app ID
  instead of killing every local sidecar whose command line contains `eventstore`. The exact gate
  now also rejects conflicting topology app IDs before executing, so it cannot disrupt or share
  stale service registrations with another Aspire session.
- [x] [Review][Patch] [high] Bound each query-provenance projection probe to five seconds inside
  its 45-second convergence window. A single resilient request previously inherited a 60-second
  `HttpClient.Timeout` and could overrun the entire poll without retaining the last actor response.
- [x] [Review][Patch] [high] Isolate Admin CLI completion-command output behind injected writers
  and make the existing `ConsoleTests` collection non-parallel with every other collection. The
  exact PR gate exposed concurrent process-wide `Console.Out` mutation; before the fix, repeated
  full-project runs reported inconsistent totals and corrupt durations. After the fix, 20
  consecutive runs passed exactly 342 of 342 tests with stable one-second durations.
- [x] [Review][Patch] [high] Make the Sample Blazor UI compile under the exact AD-11 SDK when
  Fluent components occur inside Razor control-flow blocks. Candidate `15f79b58...` stopped with
  94 Razor/C# diagnostics before any package or container publication. Namespace-qualifying the
  nested Fluent tags and expressing the top-level notification through `Visible` removes the
  ambiguous markup transition; a fresh Release build and all 117 Sample tests pass.

#### 2026-07-19 — Code review: uncommitted Story 1.20 hardening

- [x] [Review][Patch] [high] Implement a trustworthy, version-bound WORM proof contract through a
  verified provider adapter — The owner selected an allowlisted provider adapter that validates
  canonical provider API metadata and retrieves the exact object version; self-declared JSON from
  an arbitrary HTTPS URL is not sufficient.
- [x] [Review][Patch] [high] Require raw evidence to remain under locked retention for at least
  seven years after authorization — The owner selected a seven-year minimum; the current
  `retention_until > checked_at` comparison permits evidence to become mutable immediately after
  authorization.
- [x] [Review][Patch] [high] Enforce current-authority supersession semantics for authorizing C —
  The owner selected a rule under which an older C remains valid only while current `origin/main`
  retains its exact authorization identities; changing them requires a new valid C or an explicit
  revocation record.
- [x] [Review][Patch] [high] Make authorizing commit C semantically verifiable before it lands on
  `origin/main`, while retaining a post-merge official-main reachability gate
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2551]
- [x] [Review][Patch] [medium] Reject unapproved extra files in evidence commit A and extra logs in
  the raw bundle by comparing both complete inventories with their exact allowlists
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2043]
- [x] [Review][Patch] [high] Bind every mandatory evidence name to an independently fixed assembly,
  selector kind, and selector instead of trusting `test-evidence-identities.tsv` to define its own
  expected tuples
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2973]
- [x] [Review][Patch] [high] Prove full-assembly runs are complete and contain only the expected
  assembly by retaining a complete method list and comparing it with every executed XML case
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2992]
- [x] [Review][Patch] [high] Reconcile xUnit root counters and child test results, requiring zero
  failed, error, not-run, or unexpected skipped cases. The only exceptions are the exact committed
  126-case deferred red-phase inventories across six full-assembly lanes for DW1, DW2, DW4, DW5,
  DW6, and DW9; any identity, reason, digest, assembly, count, or evidence-name drift fails closed
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:738]
- [x] [Review][Patch] [high] Revalidate prerequisite review/provenance evidence from the committed
  story artifacts instead of accepting only seven mutable `done` tracker strings
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3882]
- [x] [Review][Patch] [high] Require every cross-cutting release-gate result cell to equal `PASS`
  exactly rather than accepting arbitrary `PASS...` prefixes
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3774]
- [x] [Review][Patch] [high] Re-evaluate AD-11 package pins under the actual Debug/source and
  Release/package project properties so conditional pins cannot escape the synthetic empty-global
  evaluator
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3065]
- [x] [Review][Patch] [high] Replace the marker-only proposal verifier and its early `exit 0` with
  reachable behavioral mutation fixtures for ancestry, xUnit identity/skip handling, WORM
  enforcement, and both platform smokes
  [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:773]
- [x] [Review][Patch] [high] Populate Story 1.16 `findings_and_resolutions` from an explicit reviewed
  field instead of duplicating `scope.summary` under a stronger label
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3389]
- [x] [Review][Patch] [medium] Make the live Story 1.16 disposition update atomic and retry-safe so
  concurrent edits cannot be overwritten and a later gate failure cannot strand an ambiguous
  partial mutation
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2185]
- [x] [Review][Patch] [high] Preserve or explicitly reject every section after `## Final Decision`
  instead of letting commit C silently delete an arbitrary packet tail
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3976]
- [x] [Review][Patch] [medium] Treat failed container cleanup as a gate failure and install an EXIT
  trap so successful platform smokes cannot leave proof containers running
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1723]

Applied and verified on 2026-07-19: all 16 findings (13 high, 3 medium) are patched. The
packet now uses a committed version-bound provider adapter and seven-year post-authorization
WORM retention, exact evidence/test/prerequisite contracts, two-phase current-authority
verification, atomic Story 1.16 reconciliation, tail-preserving C reconstruction, and
cleanup-gated two-platform smokes. All 23 packet Bash blocks parse; all 11 proposal verification
blocks pass, including reachable negative mutation fixtures; and `git diff --check` passes.
Story 1.20 remains fail-closed because no actual exact-SHA evidence, release identities, durable
owner approvals, or authorizing A/B/C chain exists yet.

#### 2026-07-19 — Code review chunk 1: proof packet

- [x] [Review][Patch] [high] Require the runtime and A/B/C authorization chain to be reachable from freshly fetched `origin/main` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:237]
- [x] [Review][Patch] [high] Initially required `skipped == 0` for every filtered and full xUnit
  result credited toward closure; the 2026-07-19 execution later narrowed this to zero unexpected
  skips so every separately frozen deferred red-phase inventory remains enforceable without
  authorizing DW1, DW2, DW4, DW5, DW6, or DW9 implementation
  [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:713]
- [x] [Review][Patch] [high] Restrict evidence commit A to the packet, the exact evidence directory, and a deterministic Story 1.16 disposition transform [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2478]
- [x] [Review][Patch] [high] Require provider-neutral WORM evidence for the raw bundle: URL, object version, retention deadline, immutable-policy proof, and SHA-256 [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1955]
- [x] [Review][Patch] [high] Start and health-check the digest-pinned image on both approved platforms, allowing emulation when native hardware is unavailable [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3713]

- [x] [Review][Patch] [high] Correct the nonexistent Story 2.7 SHA and reconcile the contradictory readiness results [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:51]
- [x] [Review][Patch] [high] Generate `raw-evidence-files.txt` without including the manifest itself so the mandated bundle can pass both verifiers [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1925]
- [x] [Review][Patch] [high] Execute the complete mandatory `Hexalith.EventStore.IntegrationTests` assembly, not only one provenance class [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1392]
- [x] [Review][Patch] [high] Bind every result filename and method list to its expected assembly, class, method, and executed XML cases [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2675]
- [x] [Review][Patch] [high] Revalidate Stories 1.2 and 1.14–1.19 completion and review prerequisites before commit C can authorize migration [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2313]
- [x] [Review][Patch] [high] Re-derive AD-11 SDK, ASP.NET, and runtime pins from the tested candidate instead of trusting self-reported evidence JSON [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2713]
- [x] [Review][Patch] [high] Capture and validate publication-authority expiry after final identity and cleanliness checks at the actual action time [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1705]
- [x] [Review][Patch] [high] Make authorizing commit C reconcile every blocking decision narrative, not only five exact markers [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3393]
- [x] [Review][Patch] [high] Require every cross-cutting release gate row to be exactly complete and passing before authorization [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3214]
- [x] [Review][Patch] [high] Propagate `git status` command failures instead of treating empty failed output as a clean repository [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:591]
- [x] [Review][Patch] [high] Verify each OCI child manifest config's actual OS and architecture rather than trusting index descriptor labels [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3108]
- [x] [Review][Patch] [high] Resolve the restored consumer assets path from evaluated MSBuild properties instead of hard-coding `obj/project.assets.json` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:3640]
- [x] [Review][Patch] [medium] Discover configured test projects from evaluated test-project semantics rather than a direct case-exact `xunit.v3` reference [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1356]

Applied and verified on 2026-07-19 (chunk 1): applied all 18 review patches (17 high,
1 medium). The complete proposal verification suite passes, including packet Bash parsing,
publication ordering, deterministic Story 1.16 disposition, configured-test discovery contract,
raw-evidence identity and WORM guards, strict A/B/C mutation boundaries, and both platform-smoke
requirements; `git diff --check` also passes. Story 1.20 remains fail-closed because the actual
exact-SHA evidence, release identities, durable approvals, and authorizing A/B/C chain do not yet
exist.

#### 2026-07-17 — Code review chunk 1: proof packet

- [x] [Review][Patch] [high] Retrieve GitHub approval records through the API and require each approving login to match the committed allowlist for its EventStore, release, architecture, or Story 1.16 reviewer role [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1668]
- [x] [Review][Patch] [high] Require the committed critical-evidence manifest to contain exactly the mandatory inventory and reject symlinks or non-basename paths [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1945]
- [x] [Review][Patch] [high] Retrieve and hash the raw evidence bundle, then validate a deterministic gate/log/XML inventory and successful results before A/B/C authorization [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1951]
- [x] [Review][Patch] [high] Parse and revalidate committed AD-11 and source-state evidence against the tested runtime, required versions, replacement authority, clean states, and submodule identities [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1945]
- [x] [Review][Patch] [high] Bind final approvals to the exact evidence-manifest and packet content, require post-gate approval timestamps, and disposition every named matrix limitation [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1668]
- [x] [Review][Patch] [high] Validate exactly one available classification and complete evidence fields under each of the nine named capability sections instead of counting unscoped strings [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2198]
- [x] [Review][Patch] [high] Require every Story 1.20 deferred-work section to carry exactly one explicit allowed closed status instead of checking only for absence of `open-blocking` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2006]
- [x] [Review][Patch] [high] Prevent evidence commit A from weakening the reviewed gate harness or A/B/C verifier by pinning immutable executable-block identities [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1925]
- [x] [Review][Patch] [high] Reconcile and verify the packet's body-level Decision, Owner Review, and Final Decision when commit C authorizes migration [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2249]
- [x] [Review][Patch] [high] Re-query the canonical registry during A/B/C verification and bind the approved repository, immutable digest, child manifests, platform set, and tested-runtime provenance [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2059]
- [x] [Review][Patch] [high] Load source, NuGet, and container consumer pins from a verified authorizing commit C instead of accepting caller-supplied self-consistent identities [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:2339]
- [x] [Review][Patch] [high] Add the configured `samples/Hexalith.EventStore.Sample.Tests` project and compare the mandatory test list with discovered solution test projects [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1215]
- [x] [Review][Patch] [high] Require Story 1.16 follow-up evidence to use a real calendar date and a resolvable durable source tied to the reviewed runtime [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1968]
- [x] [Review][Patch] [medium] Run the lifecycle provenance E2E in supported Debug/project-reference mode rather than Release with source references [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:891]
- [x] [Review][Patch] [medium] Reconcile the packet's stale Story 2.7 `review`/working-tree claims with the canonical committed status and exact-SHA rerun boundary [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:126]

- [x] [Review][Patch] [high] Restore Story 1.16 to fail-closed review state until a named disposition is tied to the committed candidate runtime [_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md:9]
- [x] [Review][Patch] [high] Make the AD-11 mutation fixture actually vary repository and installed SDK versions [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:318]
- [x] [Review][Patch] [high] Reject mixed ASP.NET patch bands across every effective 10.x central package pin [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:240]
- [x] [Review][Patch] [high] Compare package output with the literal approved 14-package inventory instead of trusting candidate-owned validators [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1047]
- [x] [Review][Patch] [medium] Generate the NuGet SHA-256 manifest with portable relative package filenames [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1051]
- [x] [Review][Patch] [high] Require the checksum manifest to cover exactly all 14 approved package filenames [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1433]
- [x] [Review][Patch] [high] Restore and rebuild the container project from fresh exact-candidate outputs immediately before publication [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1151]
- [x] [Review][Patch] [high] Recheck source and submodule cleanliness after the publication-capable build [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1163]
- [x] [Review][Patch] [high] Bind provenance to the digest produced by this publication rather than resolving a mutable tag afterward [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1166]
- [x] [Review][Patch] [high] Verify evidence commit A keeps the story and Epic 1 status guards blocked, not only packet front matter [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1318]
- [x] [Review][Patch] [medium] Add a negative A/B fixture where candidate and tested-runtime SHAs differ [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:549]
- [x] [Review][Patch] [medium] Hash consumer-fetched raw manifest bytes and compare them with the approved image digest [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1496]
- [x] [Review][Patch] [medium] Replace the obsolete current-work attribution check with a reproducible historical revision check [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:223]
- [x] [Review][Patch] [high] Persist a hybrid exact-SHA evidence bundle: commit the manifest and critical identity/provenance files under `_bmad-output`, and bind externally retained raw logs by immutable URL and SHA-256 [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:221]
- [x] [Review][Patch] [high] Add an authorizing commit C as B's direct child, with strict path and field whitelists plus executable checks for every evidence pin, durable approval, available parity row, and permitted status transition [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md:98]
- [x] [Review][Patch] [high] Evaluate effective ASP.NET pins without depending on XML attribute order or line layout [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:245]
- [x] [Review][Patch] [high] Run the source-topology provenance lane in the supported Debug/project-reference mode and retain separate Release/package-mode evidence [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1049]
- [x] [Review][Patch] [high] Capture every evidence artifact promised by the gate table, including build, status, submodule, environment, package-validator, and consumer logs [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1413]
- [x] [Review][Patch] [high] Bind NuGet consumer restore results to the literal approved package IDs and exact approved package bytes [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1662]
- [x] [Review][Patch] [high] Require the container consumer's approved platform file to contain exactly `linux/amd64` and `linux/arm64` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1707]
- [x] [Review][Patch] [high] Require evidence commit A to change the packet and record completed results and durable approval references [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1499]
- [x] [Review][Patch] [medium] Make the cited-path audit cover every repository path category that it claims to verify [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1801]
- [x] [Review][Patch] [high] Revalidate replacement AD-11 authority and environment identity before each major build, package, and publication gate [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:353]
- [x] [Review][Patch] [medium] Recheck ignored inputs after non-publication gates instead of checking only tracked and untracked files [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:513]
- [x] [Review][Patch] [medium] Quarantine or remove a published proof tag when post-publication validation fails [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1300]
- [x] [Review][Patch] [high] Verify container-consumer provenance maps the approved digest to the approved tested runtime SHA [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1709]
- [x] [Review][Patch] [high] Run every configured CI test project in the mandatory closure gate [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1034]
- [x] [Review][Patch] [medium] Reject file-mode changes in pointer-only commit B [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1532]
- [x] [Review][Patch] [high] Reject xUnit evidence runs whose matched tests are all skipped [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:447]
- [x] [Review][Patch] [medium] Synchronize `review_loop_iteration` with the recorded sixth review loop [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:7]
- [x] [Review][Patch] [medium] Update the checked sprint-date contract and verifier to the reconciled 2026-07-17 tracker date [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:55]
- [x] [Review][Patch] [high] Reconcile the mandatory AD-11 ledger assertion with its implementation-complete/evidence-confirmed status and closure evidence [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:200]
- [x] [Review][Patch] [high] Reject duplicate platform manifests instead of accepting a unique-sorted two-platform projection [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1422]
- [x] [Review][Patch] [high] Revalidate publication authority and candidate/source cleanliness at the final action timestamp immediately before `dotnet publish` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1499]
- [x] [Review][Patch] [medium] Scope the publication-order structural verifier to the executable publication block so unreachable duplicate markers cannot satisfy it [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:699]
- [x] [Review][Patch] [high] Commit and validate the final EventStore-owner approval and release-owner disposition records rather than accepting syntactic URL/hash placeholders [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1849]
- [x] [Review][Patch] [high] Validate A's package hash manifest against the literal 14-package release inventory before C may authorize migration [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1856]
- [x] [Review][Patch] [high] Revalidate the committed container provenance's publication-authority, checked-at, and SDK-capture identities during A/B/C verification [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1870]
- [x] [Review][Patch] [high] Require A to record an approved exact-runtime Story 1.16 follow-up disposition before authorizing C can pass [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1738]
- [x] [Review][Patch] [high] Reject A/C while any Story 1.20 deferred-work prerequisite remains `open-blocking` [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1738]
- [x] [Review][Patch] [high] Make C reconcile the Story 1.20 blocker comments and require a non-regressing tracker date instead of changing only status rows [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1962]
- [x] [Review][Patch] [medium] Restore the operational-metadata routing contract removed from the Epic 1 requirements context [_bmad-output/implementation-artifacts/epic-1-context.md:31]
- [x] [Review][Patch] [medium] Clarify that handler-computed provenance remains `HandlerComputed` while its lifecycle evidence resolves to `Unknown` [_bmad-output/implementation-artifacts/epic-1-context.md:49]
- [x] [Review][Patch] [medium] Replace the contradictory “Edit freely” generated-context instruction with an explicit planning-source synchronization rule [_bmad-output/implementation-artifacts/epic-1-context.md:3]
- [x] [Review][Patch] [medium] Expand negative A/B/C histories to cover candidate/runtime mismatch, A runtime drift, semantic B drift, merge/non-direct C, stale approval, open prerequisites, and tracker regression [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:748]

Applied and verified on 2026-07-17 (chunk 2): applied all 16 review patches and passed the
complete proposal verification suite. The suite now proves final action-time publication
readiness, committed approval records, literal release-package identities, complete container
authority provenance, completed Story 1.16 review, closed Story 1.20 prerequisites, coherent
sprint closure, monotonic tracker chronology, and targeted rejection histories for every guard;
all packet/proposal Bash blocks parse and `git diff --check` passes.

Applied and verified on 2026-07-17: accepted the hybrid evidence-retention and strict
authorizing-commit-C decisions, applied all 16 chunk-1 review patches, and passed the complete
proposal verification suite. The suite includes Bash syntax checks, evaluated-MSBuild AD-11
mutations, skipped-only xUnit rejection, literal configured-suite inventory comparison,
package/container identity fixtures, and positive/negative A/B/C histories; `git diff --check`
also passes.

Applied and verified on 2026-07-16: the complete proposal verification script passes
fail-fast, including every added positive and negative fixture; `git diff --check` also passes.

### 2026-07-16 — Review pass

- intent_gap: 0
- bad_spec: 0
- patch: 17: (high 9, medium 7, low 1)
- defer: 1: (high 1, medium 0, low 0)
- reject: 4: (high 0, medium 2, low 2)
- addressed_findings:
  - `[high]` `[patch]` Preserved the story's fail-closed lifecycle by keeping a `still blocked` packet non-`done` and making the story status consistently `blocked`.
  - `[high]` `[patch]` Replaced prose-only parity commands with literal repository-root commands for every capability lane.
  - `[high]` `[patch]` Added a detached clean-checkout gate that rejects ignored source and configuration inputs.
  - `[high]` `[patch]` Required fresh Release rebuilds so pre-existing assemblies cannot be credited as exact-SHA evidence.
  - `[high]` `[patch]` Added method-list and positive XML-total guards so zero-match xUnit filters cannot pass a gate.
  - `[low]` `[patch]` Added a no-index whitespace check that covers the untracked proof packet.
  - `[medium]` `[patch]` Replaced the vague file-existence assertion with a reproducible all-cited-path check.
  - `[high]` `[patch]` Strengthened release inventory verification from count/uniqueness to the exact 14-package ID set.
  - `[high]` `[patch]` Added exact NuGet and immutable container consumer verification procedures.
  - `[high]` `[patch]` Added clean consumer and EventStore-submodule checks to the gitlink/checkout handoff.
  - `[medium]` `[patch]` Expanded the erasure evidence map through the conditional store and lifecycle admission surfaces.
  - `[medium]` `[patch]` Expanded the batching evidence map through concrete Dapr and in-memory implementations.
  - `[medium]` `[patch]` Expanded lifecycle/provenance evidence through freshness, gateway, controller, cache, generator, and E2E carriers.
  - `[medium]` `[patch]` Expanded delivery-safety evidence through reconciler, retry, outbox, dispatch, and persistence owners.
  - `[medium]` `[patch]` Expanded rebuild evidence through checkpoint, lifecycle, promotion, boundary, cancellation, failure, and resume surfaces.
  - `[medium]` `[patch]` Added the persisted data-protection/key-ring evidence path to cursor compatibility.
  - `[high]` `[patch]` Made the cross-cutting build, test, package, container, compatibility, and owner-disposition gate auditable.

## Auto Run Result

Status: blocked
Blocking condition: prerequisite and owner-approval gate remains unresolved

Summary: Produced and review-hardened a fail-closed parity proof packet. Story 1.19's review
is complete and is no longer a prerequisite blocker. The packet authorizes no consumer
migration because Story 1.16's follow-up review remains undispositioned, the only tested
clean candidate failed the live-sidecar gate, no replacement runtime has passed every gate,
package/container identities are not pinned, and no named EventStore owner has approved the
completed evidence.

Files changed:

- `1-20-owner-approved-parity-closure-and-runtime-pin.md` — recorded the workflow baseline, review triage, follow-up recommendation, and blocked outcome.
- `1-20-owner-approved-parity-closure-proof-packet.md` — added the prerequisite ledger, parity matrix, exact-SHA gate harness, identity pins, consumer guards, verification evidence, and fail-closed decision.
- `sprint-status.yaml` — kept Story 1.20 and Epic 1 in progress while closure remains blocked.
- `deferred-work.md` — recorded the generic dev-auto finalization guard gap exposed by this fail-closed story.

Review findings breakdown: 17 patches applied (high 9, medium 7, low 1); 1 high
pre-existing workflow issue deferred; 4 review findings rejected as non-actionable or
inapplicable to an intentionally blocked packet.

Follow-up review recommendation: `true`; patched counts are high 9, medium 7, low 1.
The weighted medium/low score is `22` (`3 x 7 + 1 x 1`), and high-severity patches also
independently require follow-up review.

Verification performed:

- Exact release manifest inventory: PASS; all and only the 14 approved package IDs are present.
- Cited-path audit: PASS; every extracted repository source, test, evidence, configuration,
  submodule-owned configuration, release-tool, solution, and restore-config path exists.
- xUnit v3 zero/skip probe: PASS; zero-match returned zero with `total="0"`, an all-skipped
  result was rejected, and a positive result plus method listing was accepted.
- Tracked and no-index whitespace checks: PASS; the tracked diff and complete untracked packet are clean.
- Packet structural checks: PASS; all nine closure classifications remain `still blocked`, and migration authorization remains false.
- Exact-SHA production-path gate: FAILED for candidate `85877902...`; the full
  live-sidecar lane and both isolated reproductions retained the lifecycle-cleanup defect.
- Package-build, package-consumer, and container-publication gates: NOT RUN after the
  reproducible live failure; Story 1.19's completed review is no longer the blocker.

Residual risks: Story 1.19's review is complete and is no longer a blocker. Story 1.16's
retained follow-up recommendation still needs explicit reconciliation; the failed candidate
does not supply exact source/package/container identities or complete persisted production
evidence; named owner approval remains absent. No consumer migration is authorized.

### 2026-07-17 current-HEAD reconciliation

- Current clean detached commit `772cdfefa8163704de0f57042af5b0507c1ac771`
  passes the AD-11 executable preflight, the exact former lifecycle failure, its complete
  class, and the 44-test live-sidecar lane. Those two implementation blockers are closed.
- The proof harness incorrectly built the Tenants-dependent E2E in package mode; it now
  selects source mode explicitly for that topology.
- The corrected E2E fails reproducibly with HTTP 404 / `query_projection_missing`. Stale
  base sample registrations (`orders`, `inventory`) do not match the current sample host
  (`counter`, `greeting`), causing the atomic operational-index load to suppress
  `admin:query-types:tenants`.
- Story 2.7 owns only the pre-authorization EventStore registration/harness correction.
  Story 2.12 is the registered backlog owner for later authorized Tenants identity adoption;
  no duplicate Tenants-local story was created.

## Dev Agent Record

### Debug Log

- 2026-07-16: Re-read current sprint/story evidence. Story 1.19 is now `done` with an
  approved review disposition; Story 1.16 still retains an undispositioned follow-up flag.
- Selected clean candidate `85877902f8d60a466ab90cd8b68b53838863db1c` and created a
  detached checkout with only the seven root-declared submodules initialized.
- Release solution build passed with 0 warnings/errors. Eighteen unit/focused projects,
  Testing.Integration, AppHost, and Admin UI E2E passed.
- Full live-sidecar validation failed 2/44. The named-dispatch class reproduced 1/6, and
  its normal-delivery method reproduced 1/1, meeting the workflow's three-consecutive-failure
  HALT condition.
- 2026-07-19: Exact PR validation run `29700903245` passed build/package validation and the
  Contracts, Client, Testing, SignalR, and Admin.Abstractions suites, then exposed an intermittent
  `StringWriter` corruption in `ConfigCompletionCommandTests`. Writer injection plus an explicit
  non-parallel `ConsoleTests` boundary made 20 consecutive complete Admin CLI runs pass with the
  same 342/342 count and duration.
- Candidate `15f79b58b106c0bd1903f75d3f60042181be18f2` passed the early exact-SHA gates but
  stopped at the Sample test-project build with 94 Razor/C# diagnostics. The fail-fast harness did
  not reach source-topology, package, container, or external-evidence operations.
- A fresh detached reproduction confirmed that the tracked Razor source, not generated output,
  needed an unambiguous markup transition for Fluent components inside control-flow blocks. The
  corrected Sample UI Release build passed with zero warnings/errors and the complete Sample test
  project passed 117/117.
- Candidate `e4f5ad06a16301237e3cd355f61e7ff2be28aedb` then passed every focused
  capability lane, the source-topology E2E, and the warning-free Release solution build. The
  complete cross-cutting inventory stopped at Admin MCP with 320 passed, eight skipped, zero
  failed of 328. All eight are the frozen DW2 red-phase scaffolds that this story is forbidden to
  enable before their later live transcript exists.
- Initially reconciled the two contracts with a committed, exact eight-test skip allowlist. That
  first repair checked fully qualified names, reasons, assembly, evidence name, aggregate counters, and every
  child result in the initial run, raw bundle, and A/B/C revalidation paths; all other skips still
  fail closed.
- Candidate `689f71bf696246ab271956a3a1c218d6e51386fb` proved that first repair was too
  narrow: it passed every focused lane, the clean Release solution build, and the Admin MCP
  inventory, then failed closed at Admin Server with 717 passed, 18 frozen DW2 scaffolds skipped,
  and zero failed of 735. No publication occurred.
- Audited the remaining configured assemblies and replaced the single-lane exception with six
  exact name/reason inventories: Admin MCP 8, Admin Server 18, Admin UI E2E 2, deferred-work
  governance 19, operational evidence 54, and Server 25. The 126-case manifest binds each lane,
  assembly, count, deferred-work scope, and canonical SHA-256; every other skip still fails closed.
- The separately scoped prerequisite corrective-work stream, owned by `jpiquot` and executed on
  the candidate-correction branch, is not Story 1.20 implementation or owner-approval evidence.
  Its scope is limited to defects that prevent the exact-SHA gates from executing faithfully;
  final named approval remains outstanding and is not inferred from this corrective work.
- Candidate base `095b85b4fb4bacae3bd16450dbda4044c53079ad` exposed four independent
  cross-cutting gate defects after the original 75-failure run was reduced: test command routing
  retained the mandatory DAPR projection-activation outbox, the writer-protocol health check was
  not removed by no-sidecar test overrides, six Aspire collections shared app IDs/state while
  running concurrently, and the disposable security topology never performed the explicit v2
  writer cutover required by its fail-closed health contract.
- Replaced the mandatory outbox with an in-memory no-op at the same test boundary as the fake
  command router, removed the writer-protocol health check only in the explicit no-DAPR helper,
  serialized all shared Aspire topologies, and made the security fixture obtain an admin token and
  record a disposable writer-protocol cutover before requiring `/health` to be ready.
- The reduced full run then exposed two pre-existing contract defects: the documented
  `InvocationTimeoutSeconds` value was never applied to domain-service HTTP calls, and the Aspire
  contract client retried unsafe command POSTs. The invoker now enforces linked cancellation with
  startup validation, while the fixture disables retry for unsafe HTTP methods. The conflict
  simulator also propagates the production `messageId` needed for rejected-status persistence.
- Initialized only the root-declared `references/Hexalith.Tenants` dependency and rebuilt with
  `UseHexalithProjectReferences=true`; no nested submodule was initialized. The diagnostic
  source-topology assembly passed 235 and skipped one bootstrap log-buffer observation in
  1,534.222 seconds, so the zero-unexpected-skip contract rejected it as non-authorizing. The
  complete Server suite passed 2,756 with the exact 25 frozen ATDD skips of 2,781, and Testing
  passed 152 of 152. The bootstrap proof must be corrected and the full source-topology gate rerun.

#### 2026-07-20 — Adversarial review of candidate gate corrections

- [x] [Review][Patch] Retry disposable writer-protocol activation within the bounded topology startup budget [tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs:240]
- [x] [Review][Patch] Activate only when the detailed health report identifies the writer-protocol marker as the blocking check [tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs:243]
- [x] [Review][Patch] Translate every non-caller HTTP cancellation into the domain-service failure contract [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:75]
- [x] [Review][Patch] Reject invocation timeout values outside the supported bounded range during startup validation [src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:85]
- [x] [Review][Patch] Preserve structured tenant/domain context on domain-service timeout failures [src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:87]
- [x] [Review][Patch] Make no-sidecar health-check removal independent of service-registration order [src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs:37]
- [x] [Review][Patch] Preserve cancellation semantics in the no-op projection activation outbox [src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs:53]
- [x] [Review][Patch] Verify caller cancellation remains unwrapped [tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:117]
- [x] [Review][Patch] Verify fail-fast validation for zero, negative, and excessive invocation timeouts [tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:18]
- [x] [Review][Patch] Cover the no-op outbox completion and deferral operations [tests/Hexalith.EventStore.Testing.Tests/Fakes/TestServiceOverridesTests.cs:18]
- [x] [Review][Patch] Pin the configured invocation timeout duration in the regression test [tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:117]
- [x] [Review][Patch] Assert conflict-status persistence under the submitted message identity [tests/Hexalith.EventStore.IntegrationTests/EventStore/ConcurrencyConflictIntegrationTests.cs:104]
- [x] [Review][Patch] Separate prerequisite corrective-work attribution from this evidence-only story [1-20-owner-approved-parity-closure-and-runtime-pin.md:489]
- [x] [Review][Patch] Derive the disposable cutover marker from the exact runtime source commit and verify persisted read-back [tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs:22]
- [x] [Review][Patch] Replace the unexpected Tenant bootstrap skip with deterministic persisted-event verification [tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs:57]

Applied and verified on 2026-07-20: all 15 candidate-gate review patches are resolved. The
complete Server assembly passed 2,762 with the exact 25 frozen ATDD skips of 2,787; Testing
passed 152 of 152; the corrected Tenant bootstrap and conflict persistence proofs each passed
without skips; and the disposable security topology activated and read back its schema-1,
writer-protocol-v2 marker for exact source commit `97665d4606725478f60f55cfc285dbdb3913fa0f`.
This review closure validates only the separately scoped prerequisite corrections and does not
override Story 1.20's external evidence and named-approval gates.

#### 2026-07-21 — Docker/Dapr exact-gate unblock

- Confirmed Docker 29.6.1, Dapr CLI 1.18.0/runtime 1.18.1, the four self-hosted Dapr
  infrastructure containers, and the complete source Aspire topology were healthy before the
  detached gate attempt. The Aspire application topology was then stopped cleanly so no reserved
  app ID could contaminate the proof.
- Clean official-main candidate `4cb7738d6cfad8a9a99638644ac5de77f902245e` passed AD-11,
  all Story 1.11-1.19 capability lanes, the warning-free Release solution build, broad project
  suites, and the browser E2E lane. Its 258-test Debug/source integration assembly failed only
  `TenantBootstrapHealthTests` after the Redis chaos collection: 257 passed, one failed, zero
  skipped. The same bootstrap test passed 1/1 in isolation.
- A deterministic same-fixture sequence reproduced the failure at 3/4 passed. Redis container
  logs established that the default Docker stop deadline killed Redis while it was saving the
  final snapshot for a roughly 2 GB, 1,017,331-key data set. Restart then loaded an older RDB and
  lost the just-persisted bootstrap event.
- Corrected the chaos harness to allow a 90-second graceful Redis stop and to wait for
  `redis-cli PING`/`PONG`, not merely an open TCP port. The deterministic sequence then passed
  4/4 in 97.347 seconds.
- Added an optional `R10A2_EVIDENCE_DIRECTORY` boundary to the SignalR runtime proof and set it
  to the detached evidence root in the mandatory harness. The proof passed 1/1 in 187.403 seconds
  and left no generated file in the candidate checkout.
- The complete patched Debug/source integration assembly passed 258/258 with zero skips in
  1,450.691 seconds. The proof-packet integrity suite passed 3/3, and every fenced Bash block
  passed `bash -n`. This is corrective working-tree evidence, not an exact committed runtime
  authorization: a fresh committed candidate must rerun the protocol from zero.

#### 2026-07-22 — Exact-gate hot-reload readiness correction

- Clean official-main candidate `c6b72caa4ed90ea55a29644f0e40a0e5c44cf791` passed AD-11,
  every focused capability lane, the warning-free Release solution build, and the configured
  cross-cutting project suites through the Debug/source integration assembly.
- The complete 258-test integration assembly stopped the fail-fast run at 257 passed, one failed,
  zero skipped. `HotReloadTests.ProcessCommand_AfterDomainServiceRestart_CompletesSuccessfully`
  received HTTP 500 instead of 202 on the first command submitted after the sample restart; no
  package, container, external-evidence, approval, or publication operation followed.
- An isolated clean-candidate rerun passed, confirming an intermittent readiness race. The
  passing trace showed the start command completing at `23:06:45.435`, the command POST beginning
  at `23:06:45.440`, and the replacement sample not listening until `23:06:46.375`.
- A direct sample `/health` probe was insufficient: a repeated run returned 200 while the sample
  Dapr sidecar simultaneously classified its app channel unhealthy, and the following command
  still failed 500. Dapr's own `/v1.0/healthz` also remained 204 across the stopped-app window.
- The corrected restart helper now observes a side-effect-free
  `POST /admin/operational-index-metadata` through EventStore's Dapr sidecar. It requires the
  invocation to become unavailable after stop and successful after start before issuing any
  non-idempotent command; command POSTs are never retried by the correction.
- The integration project source-mode Debug build passed with zero warnings/errors. The formerly
  failing method passed three consecutive review-patched fresh-topology runs (40.554, 45.506, and
  55.702 seconds), and the complete `HotReloadTests` class passed 3/3 in 117.017 seconds. The pure
  readiness parser/polling suite also passed 11/11. Exact commands, timestamps, result/log paths,
  input hashes, and artifact SHA-256 values are recorded in the proof packet's
  `2026-07-22 review-patched corrective verification` section. These are corrective working-tree
  results; a new committed candidate must restart the exact protocol from zero.

#### 2026-07-22 — Exact-SHA closure gate run against committed candidate `440ff4cb`

- The committed hot-reload readiness correction landed clean on official main as
  `440ff4cb36a9ea1446024f3906c132b0398e881f`, so the closure protocol was restarted from zero
  against it (Closure Execution Order steps 3-6) from a fresh detached checkout with only the seven
  root-declared submodules initialized non-recursively. `git rev-parse HEAD` equalled the candidate
  before and after every gate; no nested submodule was initialized. AD-11 verified exact: SDK
  `10.0.302`, effective ASP.NET `10.0.10`, installed `Microsoft.NETCore.App` /
  `Microsoft.AspNetCore.App` `10.0.10`; no replacement-authority record was required.
- Story 1.16 follow-up review re-verified technically at this SHA: both the lifecycle fix
  (`7b73a2f5...`) and Story 2.7's source-topology correction (`fd8ab24d...`) are ancestors of the
  candidate; `NamedProjectionDispatchLiveSidecarTests` 6/6 (including the formerly-failing
  `NormalDelivery_...` method), full `Server.LiveSidecar.Tests` 49/49, `Server.Tests`
  2874 total / 2849 passed / 25 DW1 skips / 0 failed, `Contracts.Tests` 756/756. No regression. The
  named `story_1_16_reviewer` durable GitHub-sourced disposition remains an unresolved
  human-authority gate; it is not inferred from this technical re-verification.
- Release solution build passed 0 warnings / 0 errors. Twenty-two of the twenty-three mandatory
  test lanes passed with zero failures and exactly the 126-case allowlisted skips (DW1 25, DW2
  8+18, DW4/DW9 54, DW5 2, DW6 19) and zero unexpected skips anywhere; the known `Server.Tests`
  CA2007 build failure did not reproduce at this candidate.
- The 14-package release inventory built exactly (no extra, none missing), passed the package and
  consumer-restore validators, and was SHA-256 hashed (recorded under the scratchpad proof
  evidence root).
- The one lane that did not return a clean, trustworthy result was the full Debug/source
  `Hexalith.EventStore.IntegrationTests` assembly: 269 total / 241 passed / 28 failed / 0 skipped in
  3031.9s (~2x the historical baseline). Every one of the 28 failures is a timeout/cancellation
  signature (`TaskCanceledException` / Polly `TimeoutRejectedException` on the 60s `HttpClient`),
  spread uniformly across unrelated feature areas, plus one `address already in use` port collision.
  A concurrent, unrelated `Hexalith.Works` Aspire session
  (`/home/administrator/projects/hexalith/works`, running its own Story 4.7 integration lane) was
  observed registering the same reserved `eventstore` / `eventstore-admin` Dapr app-ids against the
  shared placement/scheduler/Redis for the entire run window, and was still cycling those app-ids
  when this gate concluded. Verdict: environmental interference, not a candidate regression and not a
  verified pass. This lane must be re-run in an isolated environment before the detached exact-SHA
  production-path gate can be treated as satisfied.
- Container build/publication (Closure Execution Order step 7) was not entered. No git commit, no
  registry interaction, no GitHub approval-API call, and no external/WORM upload occurred; this run
  produced no owner approval or authorization evidence, and no repository file was modified (the run
  was a read-only verification against the committed HEAD).
- Isolated re-run of the integration lane was then attempted after the interfering session was
  reported finished (`dapr list` empty at launch, 13:58). It ran clean for ~9 minutes, but the
  concurrent `Hexalith.Works` Aspire AppHost auto-restarted at ~14:07 and re-registered the reserved
  `eventstore` app-id against the shared placement (two `eventstore` app-ids captured simultaneously
  at 14:11:53). The run was aborted at ~14:12 on detected contention rather than crediting a second
  polluted result. Because that auto-dev session (a concurrent `bmad-loop`/Works loop) auto-cycles
  its Aspire topology every few minutes on the shared `dapr_placement`/`dapr_scheduler`/`dapr_redis`,
  a reliable ~50-minute quiescent window is not available while it runs. The integration lane
  therefore remains environmentally blocked — not a `440ff4cb` defect — and must be re-run either
  with that loop genuinely paused for the full duration or against isolated Dapr infrastructure
  (dedicated placement/scheduler/Redis on distinct ports). This aligns with the existing closure
  requirement to pause the concurrent auto-dev loop before the A/B/C authorization chain.
- After the concurrent loop was stopped, the shared Dapr state was cleaned (owner-approved): the
  bloated shared Redis (1,023,104 keys / 1.94 GB accumulated across runs) was `FLUSHALL`-ed and
  `dapr_placement` / `dapr_scheduler` restarted. A pristine-environment full-assembly re-run reduced
  the failure set from 28 to a single-rooted cascade and identified the true root cause:
  `QueryResponseProvenanceE2ETests.LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence`
  fails at `AspireContractTestFixture.RestartEventStoreWithClearedHandlerQueryTypesStateAsync`
  (`AspireContractTestFixture.cs:137`) with `Failed to stop resource 'eventstore-<suffix>'`. daprd
  starts each EventStore sidecar with an infinite actor graceful-shutdown timeout; under
  full-assembly load the fixed-name actors on the shared placement/scheduler do not drain promptly,
  so daprd waits indefinitely, the app never terminates, Aspire DCP's `StopCommand` returns
  `Success=false`, and the half-stopped resource wedges the entire `AspireContractTestFixture`
  collection — every subsequent test then fails with a pure 60s `HttpClient` / Polly timeout. The
  ~27 downstream failures are all cascade from that one wedge.
- Root-cause classification: an Aspire-DCP / daprd resource-stop flake under full-assembly
  conditions on shared Tier-3 infrastructure — NOT a `440ff4cb` defect. Evidence: the root test and
  its cascaded neighbours pass 5/5 (method ×3, full class ×2) and 1/1 in isolation from the clean
  worktree; `git show 440ff4cb` changes only `HotReloadTests.cs`, the new
  `DaprInvocationReadinessProbe(.Tests).cs`, four `_bmad-output/` docs, and the Tenants submodule
  pointer — it does not touch `AspireContractTestFixture.cs` (unchanged since `f6db558c`) or
  `QueryResponseProvenanceE2ETests.cs` (unchanged since `15f79b58`); the clean 258/258 baseline
  `4cb7738d` (2026-07-21) is an ancestor and the +11 tests are exactly the added hot-reload readiness
  tests. The identical "Failed to stop resource" was also run-1's odd-one-out failure, before any
  placement/scheduler restart, and it reproduces at both pristine (384-key) and bloated (1.94 GB)
  Redis — ruling out the restart, Redis bloat, and foreign contention as the trigger. This is the
  documented Tier-3 constraint (shared Dapr placement + fixed-name actors → hangs; DCP-stop
  cascades; suite not gated in CI). The full 269-test integration assembly therefore cannot be shown
  green as a single run in this shared environment, but the failure is not attributable to the
  candidate. A durable fix is test-infrastructure work outside this evidence-only story (a finite
  daprd graceful-shutdown timeout for the test topology, or per-collection unique app-ids as the
  live-sidecar lane already uses).

### Completion Notes

- Story remains fail-closed and non-authorizing. Runtime and test corrections in the candidate
  lineage are separately scoped prerequisite corrective work; they are not credited as
  implementation of this evidence-only story and do not grant owner approval.
- Updated the proof packet with the current exact-SHA results and recorded a scoped
  corrective item in `deferred-work.md`.
- Package, container, provenance E2E, and owner-approval gates were not run after the
  reproducible live regression failure.
- The PR-gate console race is repaired without treating a rerun as acceptance evidence. The
  replacement candidate still requires a completely green PR gate and a fresh unchanged-SHA
  closure run before any publication or approval step.
- The candidate `15f79b58...` run remains rejected and produced no package/container publication.
  The Razor correction must merge through a new exact-head PR before the closure protocol restarts
  from a fresh detached checkout.
- The candidate `e4f5ad06...` run also remains rejected and produced no package/container
  publication. Its Admin MCP stop exposed a proof-contract defect, not a DW2 implementation
  authorization. A new merged exact SHA must rerun the full protocol from zero after the strict
  deferred-skip contract is reviewed.
- The local correction branch is not authorization evidence. Its affected unit suites passed, but
  its full source-topology diagnostic was rejected because of an unexpected skip. Story 1.20
  remains fail-closed until the corrected zero-unexpected-skip gate passes, the corrections merge,
  a fresh official-main SHA reruns every packet gate from zero, immutable raw evidence and artifact
  identities exist, both named approvals are durable, and A/B/C validate.
- Resolved all 15 adversarial review patches. The full Server and Testing assemblies pass, the
  Tenant bootstrap and conflict persistence focused proofs pass without skips, and the security
  fixture persists and reads back the exact runtime cutover marker. Story status remains `blocked`
  because code-review closure is not named owner approval or immutable authorization evidence.
- Resolved all 10 findings from the 2026-07-21 review of `bccc2560`: domain invocation now owns
  its validated timeout independently of host-wide HTTP resilience; disposable cutover state is
  snapshotted and restored; retry classifiers and unsafe-method behavior have deterministic tests;
  Tenant bootstrap requires a buffered terminal-success log; and conflict rejection has a compiled
  production DAPR persistence proof. Focused Server tests passed 45/45, deterministic retry-policy
  tests passed 12/12, the in-process conflict test passed 1/1, and the production DAPR
  live-sidecar persistence proof passed 1/1. xUnit test methods use `ConfigureAwait(true)` as
  required by xUnit1030, while fixture and non-test helper awaits retain `ConfigureAwait(false)`.
  This review closure does not satisfy the external Story 1.20 gates.
- Completed the user-requested Docker/Dapr unblock. The Redis chaos/tenant-bootstrap regression
  now passes both its deterministic 4-test sequence and the complete 258-test source-topology
  assembly, and SignalR evidence can be routed outside the candidate checkout. The corrections
  remain uncommitted, so no clean exact runtime SHA, publication, approval, or migration authority
  is claimed.
- Candidate `c6b72caa...` remains rejected after its 257/258 integration result. The hot-reload
  correction passes three consecutive focused cycles and the complete class without unsafe POST
  retry, but remains non-authorizing until it is committed and every exact-SHA gate restarts from
  zero. Story 1.16's named disposition and Story 1.20's durable owner approval remain human gates.
- Exact-SHA gate run against committed candidate `440ff4cb...` (2026-07-22) passed the Release
  build, the Story 1.16 technical re-verification, all twenty-two other mandatory test lanes with
  zero unexpected skips, and the 14-package build+hash+consumer-validation gate. It did NOT obtain a
  clean result on the full Debug/source integration lane (28/269 timeout-signature failures) because
  a concurrent, unrelated `Hexalith.Works` Aspire session shared and contended for the same
  fixed-name Dapr app-ids and placement/scheduler/Redis infrastructure. Story 1.20 therefore remains
  fail-closed: the integration lane must be re-run in isolation, and the human-only gates (named
  Story 1.16 disposition, named EventStore-owner proof approval, release-owner authority record,
  immutable/WORM raw-evidence upload, and the A/B/C authorization chain) remain outstanding.
- Root-cause investigation closed the integration-lane question: the residual full-assembly failure
  is an Aspire-DCP / daprd resource-stop flake (infinite actor graceful-shutdown timeout colliding
  with fixed-name actors on shared Tier-3 placement/scheduler), rooted in the restart-based
  `QueryResponseProvenanceE2ETests.LiveHandlerRoute` test and cascading to the rest of its
  collection. It is not a `440ff4cb` defect (the affected path is candidate-unchanged; the root test
  and cascaded neighbours pass 5/5 and 1/1 in isolation). The full 269-test assembly cannot be
  shown green as one run in this shared environment; a definitive full-assembly pass needs the
  ledgered Tier-3 test-infra fix (finite daprd shutdown timeout or per-collection unique app-ids) or
  dedicated isolated Dapr infrastructure. This does not change Story 1.20's fail-closed status; it
  characterizes the one remaining technical gate as blocked by a known, non-candidate Tier-3
  constraint rather than by any defect in `440ff4cb`.

## File List

This development record spans the evidence-only story documents and the separately scoped
prerequisite corrective-work stream above. Listing a runtime/test path here records branch
traceability; it does not reclassify that path as a Story 1.20 implementation deliverable.

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`
- `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json`
- `_bmad-output/implementation-artifacts/1-20-deferred-xunit-skip-allowlist.json`
- `_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `docs/guides/configuration-reference.md`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigCompletionCommand.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigCompletionCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/ConsoleTestCollection.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Configuration/ValidateIdempotencyAdmissionOptions.cs`
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceException.cs`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs`
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`
- `src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs`
- `src/Hexalith.EventStore.Testing/Fakes/NoOpProjectionActivationOutbox.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ChaosResilienceTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbeTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/EventStore/ConcurrencyConflictIntegrationTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestCollection.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractHttpResilience.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractHttpResilienceTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireProjectionFaultTestCollection.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestCollection.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/KeycloakAuthFixture.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyCollection.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Security/ProjectionDeliveryWriterProtocolCutoverPolicy.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Security/ProjectionDeliveryWriterProtocolCutoverPolicyTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Commands/ConcurrencyConflictStatusPersistenceLiveSidecarTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/TestServiceOverridesTests.cs`

## Change Log

| Date | Phase | Test-method delta | Verification | File-list reconciliation |
| --- | --- | ---: | --- | --- |
| 2026-07-23 | Code-review chunk 1 patch application: production runtime and unit tests | `+8` test methods / `+10` cases | Release builds: Server.Tests and Testing.Tests 0 warnings/errors. Focused xUnit v3: registration 20/20, invoker 38/38, testing overrides 2/2. Complete assemblies: Server 2,859 passed / 25 existing skips / 0 failed; Testing 152/152. | Applied all 14 patch findings, retained the one pre-existing deferred item, added the extracted no-op outbox and idempotency validator paths, and preserved `blocked` / sprint `in-progress`. |
| 2026-07-22 | Integration-lane isolation + root-cause after Redis/placement clean (candidate `440ff4cb...`) | `+0` (read-only investigation; no source change) | After stopping the concurrent loop, `FLUSHALL`-ed the 1.94 GB shared Redis and restarted `dapr_placement`/`dapr_scheduler`; a pristine full-assembly re-run reduced 28 failures to a single-rooted cascade at `AspireContractTestFixture.RestartEventStoreWithClearedHandlerQueryTypesStateAsync` ("Failed to stop resource"). Root cause = Aspire-DCP/daprd resource-stop flake (infinite actor graceful-shutdown timeout + fixed-name actors on shared placement/scheduler), NOT a candidate defect: root test + cascaded neighbours PASS 5/5 and 1/1 in ISOLATION; `440ff4cb` leaves `AspireContractTestFixture.cs`/`QueryResponseProvenanceE2ETests.cs` unchanged; failure also present in run-1 before any restart and at pristine Redis. | No repository file changed; evidence under the scratchpad proof root. Integration full-assembly gate blocked by the ledgered Tier-3 DCP-stop-cascade constraint (needs finite daprd shutdown timeout / per-collection app-id isolation or dedicated Dapr infra), not by `440ff4cb`. Story stays `blocked`; human-authority gates remain open. |
| 2026-07-22 | Exact-SHA closure gate run (Closure Execution Order steps 3-6) against committed candidate `440ff4cb36a9ea1446024f3906c132b0398e881f` | `+0` (read-only verification; no source change) | PASS: Release solution build 0W/0E; 22/23 mandatory lanes green with exactly 126 allowlisted skips / 0 unexpected; Story 1.16 technical re-verification (LiveSidecar 49/49, Server 2849/2874, Contracts 756/756); 14-package build+SHA-256+consumer-validation. INCONCLUSIVE at the time: full Debug/source `IntegrationTests` 241/269 (28 timeout-signature failures) from a concurrent, unrelated `Hexalith.Works` Aspire session contending for the shared `eventstore`/`eventstore-admin` Dapr app-ids and placement/scheduler/Redis (root cause later isolated — see row above). Container step 7 not entered. | No repository file changed (verification-only against committed HEAD); evidence retained under the scratchpad proof root. Story remains `blocked`; all human-authority gates remain open. |
| 2026-07-22 | Exact-gate hot-reload readiness correction after rejected candidate `c6b72caa4ed90ea55a29644f0e40a0e5c44cf791` | `+4` test methods / `+11` deterministic cases | RED: exact Debug/source integration assembly 257/258; repeated direct-health attempt still failed 1/1 after `/health` returned 200. GREEN after review patches: source-mode Debug build 0 warnings/errors; readiness parser/polling 11/11; formerly failing method 3/3 across fresh topologies; complete `HotReloadTests` 3/3 in 117.017s. | Added the hot-reload contract-test path plus the readiness helper and deterministic test paths. The correction proves EventStore-to-sample Dapr invocation unavailable after stop and ready after start without retrying command POSTs. Exact commands/artifact hashes are bound in the proof packet. Story remains `blocked`; a new clean candidate must restart every exact gate. |
| 2026-07-21 | Docker/Dapr exact-gate unblock on official-main base `4cb7738d6cfad8a9a99638644ac5de77f902245e` | `+0` | RED: deterministic Redis-chaos/bootstrap sequence 3/4; GREEN: 4/4 in 97.347s. SignalR external-evidence proof 1/1 in 187.403s. Complete patched Debug/source integration assembly 258/258, zero skipped, in 1,450.691s. Integration project source-mode Debug build: 0 warnings/errors. Proof-packet integrity 3/3; all fenced Bash passed `bash -n`. | Added the Redis chaos harness, SignalR runtime-proof evidence boundary, and proof-packet harness paths. Story remains `blocked`; the working-tree corrections require a new committed exact candidate and a full restart of the protocol. |
| 2026-07-21 | Ledger adoption baseline for the user-approved direct Story 1.20 corrective pass at `014bd00a9ef6d3eac2e4900feedbedc093fab188` | `+0` (planned `+6..+9`) | Release builds passed with 0 warnings/errors. Runner discovery: Server `2560` methods (`DaprDomainServiceInvokerTests=28`, `EventStoreServerServiceCollectionExtensionsTests=7`); Integration `234` methods (`TenantBootstrapHealthTests=1`, `ConcurrencyConflictIntegrationTests=11`). Commands: `dotnet exec tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -list methods -noLogo`; `dotnet exec tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll -list methods -noLogo`. | Current adoption delta `0/0` paths against `014bd00a...`; the existing File List remains the legacy pre-adoption traceability inventory. Story remains `blocked` / sprint `in-progress`; this phase cannot authorize migration. |

### Legacy narrative

- 2026-07-16: Recorded the failed exact-SHA completion attempt and kept Story 1.20
  fail-closed pending a scoped lifecycle-cleanup fix, remaining review disposition, release
  identity gates, and named owner approval.
- 2026-07-17: Reconciled current-HEAD evidence: lifecycle and AD-11 pass; corrected the
  source-topology proof harness; routed the reproducible stale-registration/query-provenance
  blocker to existing Story 2.7; retained all release and migration guards.
- 2026-07-17: Applied all 15 findings from proof-packet review chunk 1: GitHub role-bound
  approvals, exact durable/raw evidence inventories, semantic A/B/C verification, live registry
  re-query, verified-C consumer pins, complete test discovery, and reconciled Story 2.7 status.
- 2026-07-19: Applied all 18 findings from the new proof-packet review chunk: official-main
  reachability, zero-skipped and identity-bound complete test evidence, WORM retention,
  deterministic evidence-commit A, re-derived AD-11 and release prerequisites, actual OCI child
  configuration checks, and digest-pinned health smoke on both approved platforms.
- 2026-07-19: Applied all 16 adversarial follow-up findings: provider-adapter WORM proof and
  seven-year authorization retention, current-authority supersession, fixed xUnit identities and
  completeness, exact prerequisite/evidence inventories, dual-mode AD-11 evaluation, retry-safe
  Story 1.16 disposition, tail-safe authorization reconstruction, and executable mutation tests.
- 2026-07-19: Repaired two execution-blocking proof defects found by the actual closure run: added
  the missing authenticated immutable-blob adapter and aligned both xUnit validators with the
  real xUnit v3 `<assemblies>/<assembly>` result schema.
- 2026-07-19: Corrected the live retry-ledger convergence proof to preserve unrelated terminal
  work in the same shard while requiring the completed aggregate's retry item to be removed.
- 2026-07-19: Realigned the reusable release-workflow execution SHA with the root-declared
  `Hexalith.Builds` dependency after the concurrent submodule bump.
- 2026-07-19: Isolated the live-sidecar Dapr app identity and added a conflicting-topology
  preflight after the closure run killed an unrelated Aspire session's EventStore sidecars and
  subsequently observed stale `sample` / `tenants` registrations.
- 2026-07-19: Made the provenance convergence probe enforce a per-request timeout shorter than
  its outer polling deadline while retaining the last response or timeout diagnostic.
- 2026-07-19: Removed process-wide console mutation from the completion-command tests and made
  the remaining `ConsoleTests` collection non-parallel with all other collections after the PR
  gate exposed concurrent `StringWriter` corruption.
- 2026-07-19: First preserved the frozen DW2 red phase while repairing Story 1.20's contradictory
  zero-skip gate for the exact eight committed Admin MCP scaffold names and reasons.
- 2026-07-19: Extended that repair after candidate `689f71bf...` exposed the next known deferred
  lane. The validator now permits only the exact 126 committed red-phase cases across the six
  bound DW1/DW2/DW4/DW5/DW6/DW9 lanes and rejects all manifest, identity, reason, count, digest,
  assembly, evidence-name, or unexpected-skip drift.
- 2026-07-20: Repaired cross-cutting no-sidecar overrides, shared Aspire topology isolation,
  disposable writer-protocol cutover, bounded domain invocation, unsafe POST retry, and conflict
  status identity propagation as separately scoped prerequisite corrective work. The diagnostic
  source-topology run passed 235/236 but its Tenant bootstrap observation skip is not allowlisted,
  so the gate rejected the run and authorization remains blocked pending a zero-unexpected-skip
  rerun, merge, and fresh exact-SHA closure run.
- 2026-07-20: Resolved all 15 adversarial review findings with bounded cutover retry and exact
  persisted marker readback, structured domain timeout enforcement, order-independent no-sidecar
  overrides, complete fake-outbox cancellation coverage, exact conflict identity assertions, and
  deterministic persisted Tenant bootstrap verification. Review closure does not change the
  fail-closed Story 1.20 status.
- 2026-07-19: Repaired the Sample Blazor UI's .NET 10 Razor control-flow markup transitions after
  the exact-SHA proof correctly stopped its Sample test-project build before publication.
- 2026-07-21: Applied all 10 patches from the review of landed corrective commit `bccc2560`,
  including production timeout isolation, disposable Redis-state restoration, persisted DAPR
  conflict proof, terminal bootstrap verification, and deterministic retry behavior. Retained the
  fail-closed story and sprint states because external evidence, exact-runtime, and named-approval
  gates remain outstanding.
