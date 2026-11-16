---
title: '9.2 Story 5.3 private authentication integration evidence'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
baseline_commit: '8b6d5bee5217b3f4bbd8fd689ad887aca863961b'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Scanner repair is delivered, but the Story 5.3 adapter still lacks complete authentication integration evidence. Its last genuine run failed fixture startup and exceeded its output bound without completed XML.

**Approach:** Repair private Dapr prerequisites and authentication topology isolation, then execute the unchanged integration lane before attempting VG2 completion-writer proof.

## Boundaries & Constraints

**Always:** Retain all four selectors, 180-second lane budget, 1 MiB output limit, complete adapter denominator, positive discovery and zero failures/skips/not-run. Use real JWT/Keycloak and production writer-protocol activation. Assert persisted tenant isolation. Own disposable resources and await cleanup. Use `run-with-temp`; put disposable copies/feeds/outputs beneath TMPDIR and useful logs outside it. Distinguish fresh and historical evidence.

**Never:** Steal leases, mutate shared Redis, bypass TLS/authentication/health gates, suppress failures, narrow coverage, increase budgets to pass, or fabricate success. Do not change production security policy, dependencies/submodules, protected CI/docs, remote settings/approvals; do not stage/commit/push. Preserve historical labels and parent 9.2 `in-progress`. VG2, other consumers/adapters and live assurance remain separate; grant no NFR/readiness closure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
| --- | --- | --- |
| Private prerequisites | Bound Dapr binaries and fresh scratch | Required directories exist; no caller credentials/configuration inherited |
| Authentication evidence | Real services and all four selected classes | Complete XML; positive discovery per selector; every case passes within fixed bounds |
| Foreign ownership | Existing foreign marker/lease or another topology | Preserve it; use an owned store or refuse before mutation |
| Failure/termination | Startup failure, budget exhaustion or interruption | Truthful bounded FAIL; owned child/resource cleanup; no completion authority |

</frozen-after-approval>

## Code Map

- `tools/phase_4_story_5_3.py`: private prerequisites, diagnostic-only new-source membership, exact-owner cleanup across both validator processes; unchanged `LANES`, `counts` and execution bounds.
- `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogTransportDocker.cs`: existing internal extraction also carries the invocation owner, so bounded parent cleanup covers stopped extraction containers without changing transport custody or public API.
- Integration project root: `tests/Hexalith.EventStore.IntegrationTests/`. Paths below are relative to it.
- `Fixtures/KeycloakAuthFixture.cs`, `Security/AspireTopologyFixture.cs`, `Security/PrivateAuthenticationInfrastructure.cs`: one owned cohort, real credentials, catalog/transport readiness and cleanup.
- `Security/ProjectionDeliveryWriterProtocolTestLease.cs`: authentication fixtures supply private Redis explicitly; production activation, marker restoration and foreign-lease refusal remain enforced. Other fixtures retain constructor compatibility.
- `EventStore/JwtAuthenticationIntegrationTests.cs`: real positive gateway evidence and existing middleware negatives; `ContractTests/KeycloakAuthenticationTests.cs`: persisted isolation plus real signed denial controls.
- `tools/phase_4_story_completion.py`: final inventory rechecks already exist; VG2 needs genuine passing evidence.
- `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidationResult.cs`: two readonly auto-properties preserve the concurrent commit's public getters and `unevaluated` values while fixing CA1822 so verification can build.

## Tasks & Acceptance

**Execution:**

- [x] `tools/phase_4_story_5_3.py`, `tools/tests/test_phase_4_story_5_3.py` — complete token-free Dapr scratch and add prerequisite/isolation controls.
- [x] Integration-root `Fixtures/KeycloakAuthFixture.cs`, `Security/AspireTopologyFixture.cs`, `Security/ProjectionDeliveryWriterProtocolTestLease.cs` — isolate owned state/endpoints and repair reproduced startup/transport failures with safe cleanup.
- [x] Integration-root `Security/ProjectionDeliveryWriterProtocolTestLeaseTests.cs`, `EventStore/JwtAuthenticationIntegrationTests.cs`, `ContractTests/KeycloakAuthenticationTests.cs` — verify ownership/cleanup, real authentication and persisted tenant separation.
- [x] This spec and `_bmad-output/implementation-artifacts/epic-9-context.md` — record commands, counts, source identities, remaining prerequisites and any delivery CI link.

**Acceptance Criteria:**

- Given complete prerequisites, when the unchanged lane runs, then all four selectors produce complete passing XML within fixed bounds.
- Given foreign ownership or initialization failure, when setup/cleanup runs, then foreign state is preserved and owned cleanup succeeds or reports its precise failure.
- Given final source, when the full adapter diagnostic reruns, then every fixed lane and seven scanner probes retain genuine outcomes without an assurance claim.

## Implementation Notes

The four selectors share one owned gateway/Counter/Keycloak cohort. The fixture uses private Redis, ephemeral endpoints, a run-private HTTPS certificate with normal chain/hostname verification, and the SDK's checksum-pinned Dapr 1.19.1 Development Sentry/placement/scheduler transport. Discovery uses a separate private SQLite directory, outside immutable component inputs. Fixture-private realm imports issue signed UI/service surface claims; unmarked and reserved-tenant clients provide real negative controls. No production realm or authentication policy changed.

Writer activation still calls the authenticated production cutover endpoint and checks the persisted marker. Its original strict pre-cutover health policy is unchanged. Readiness also requires both applications' real health checks, the sample's full native sidecar health, and the mutually authenticated gateway-to-sample `/health` hop. A real Keycloak client-credentials token scoped to `sample` metadata then exercises the protected metadata endpoint. The SDK's committed readback supplies credential-free generation hints only; the receiver authenticates the workload and independently reacquires live catalog authority. Authentication denials fail immediately. No business command is submitted or retried during readiness. Both tenants use the same aggregate ID and distinct command IDs; persisted envelopes assert the exact tenant, aggregate, causation ID and empty CounterIncremented payload. The reserved client receives both a valid tenant grant and the forbidden `system` grant; denied commands assert absence of an event.

The adapter creates an empty private Dapr components directory and includes new source files in diagnostic inventories only, with identical path/mode membership checks at capture and acceptance. It explicitly supplies private transport custody outside checkout ancestry and beneath the wrapper temporary directory. Authenticated membership rules, all selectors, the 21-lane plan, seven scanner probes, and execution bounds remain unchanged. Inner and outer validator processes share a fresh ownership label; the parent can reap owned containers if its bounded child dies before cleanup. Cleanup inspects and removes immutable container IDs, attempts every owned member after individual failures, and accepts disappearance only after an exact-ID absence check. Composite cleanup retains failed handles for retry. Cleanup logs survive an outer failure. No shared repository index is written.

The current transport prerequisite is Linux, the existing checksum-pinned Dapr runtime/CLI, Docker and the existing profile images. Other interactive hosts, full AppHost qualification, VG2 and live assurance remain outside this slice.

Concurrent commit `f85419d501f279e9a813435aa73538185ad26ada` entered the shared tree during verification. It added 29 production-profile files and introduced CA1822 on `ProductionProfileValidationResult.Authorization` and `.Qualification`. The approved baseline above is preserved. After all candidate builds passed, a routine build repair retained both instance getters as readonly auto-properties initialized to the same `unevaluated` value. The public API and both status values are unchanged; all other concurrent work is preserved.

## Spec Change Log

- Independent review identified eight direct internal corrections: extraction labels, continued exact-ID cleanup, immutable C# removal, retryable composite disposal, private temporary custody, diagnostic membership, persisted identities and mixed reserved-grant verification. No public API, production security policy, selector or budget changed; every finding is triaged individually below.

- 2026-11-16: Owner approved the frozen intent and directed continuation. The frozen block is unchanged.
- Implementation discovery: the SDK SQLite startup-ordering helper requires directly launched native executables; this fixture retains the pinned CLI. The incompatible helper was removed. Mutable discovery state was moved outside component inputs after a genuine failed run. Real sidecar-hop health readiness repaired the first-command startup race.
- Verification correction: an early implementation-agent queue continued after a failed build and used a stale assembly. Those results were discarded; the parent driver stops on every failed build. Diagnostic exception instrumentation existed only in a disposable sample copy and is excluded from qualification evidence.
- Owner explicitly approved the minimal CA1822 repair in the concurrent production-profile result type. Later diff audit removed an auxiliary diagnostic collector that could prevent cleanup and made cancellation callback failures reportable without skipping cleanup.
- Anonymous health alone did not eliminate an intermittent first-command failure: `full-current-3` retained a genuine failed integration outcome. Protected metadata readiness now verifies the service JWT path before test bodies; it issues real IdP tokens and never invents an authority lease or approval.

## Review Triage Log

All three independent reviewers completed before triage. Every finding is recorded individually below before grouping. Parent Story 9.2 remains `in-progress`.

| ID | Verdict | Evidence and disposition |
| --- | --- | --- |
| B1 | medium | SDK extraction writes its intent before `docker create`, but labels only `hexalith.eventstore.transport`; both timeout reapers select the authentication label. Forced termination can retain a stopped container. Patch the internal extraction label propagation; retain exact-owner checks. |
| B2 | medium | Python cleanup stops on the first failed inspect/remove. Docker auto-removal can make a listed immutable ID disappear while other owned IDs remain. Patch to attempt every ID and aggregate real failures. |
| B3 | medium | C# disposal verifies a name, then removes that name. A replacement between these calls can be foreign. Patch to inspect identity and owner together, then remove only that immutable identity. |
| B4 | medium | Initialization failure calls `SafeShutdownAsync`, and xUnit later calls disposal again. The infrastructure currently marks itself disposed and discards `_transport` even when the SDK retains live processes for retry. Patch to retain failed handles and mark completion only after successful release. |
| B5 | medium | Custody is currently selected beside the repository. A normal wrapper invocation in the existing checkout writes outside its TMPDIR, while a nested checkout's parent is rejected by the SDK. Patch custody to private temporary space outside checkout ancestry. |
| B6 | medium | `inventory` admits untracked source, but final diagnostic revalidation compares tracked membership only. A newly added source after binding is omitted without refusal. Patch membership recomputation and regression coverage. |
| B7 | medium | Both persisted events are checked only by event type. `EventEnvelope` also contains tenant, aggregate and causation identity; copied envelopes under both tenant keys can pass the current assertion. Patch to assert the actual tenant and originating command identities. The domain's `CounterIncremented` payload is intentionally empty, so invented distinct payloads are unnecessary. |
| B8 | false | The cited 403 paths return from `CommandsController.SubmitCoreAsync` before command dispatch, so they cannot queue a later append. Immediate absence checks observe this synchronous refusal; no reachable delayed-persistence path producing the described false pass was identified. |
| B9 | low | A mutable Redis tag limits exact infrastructure reproducibility, but does not change the tested signed-token or persisted-isolation assertions. Binding a new container-provenance contract adds machinery beyond a direct correction; reject this low-impact improvement in this local diagnostic slice. Dependency changes remain prohibited. |
| B10 | false | Awaited StackExchange.Redis connection establishment and bounded connection retries precede all lease-test commands; the main fixture also starts transport and healthy applications before using Redis. `docker run` returning before readiness does not bypass those consumer checks. A Redis that never starts causes a real failed test. |
| B11 | medium | Concurrent commit `f85419d501f279e9a813435aa73538185ad26ada` documents `statestore` for every actor app, contradicting AD-5's distinct component names for other apps in a namespace. This is pre-existing concurrent production-profile work; defer. The frozen intent excludes protected production docs changes. |
| B12 | medium | The concurrent production-profile document describes `ProductionProfileValidator.Validate` and canonical codec behavior, but this commit provides records only and no such implementation. Defer to that independently owned production-profile delivery; do not add it to authentication work. |
| E1 | medium | The SDK extraction path has managed `finally` cleanup but no authentication-owner label; a killed worker cannot execute it. Patch; same demonstrated leak as B1. |
| E2 | medium | A listed container can disappear before inspection/removal because Docker owns auto-removal; the first failure currently prevents remaining cleanup attempts. Patch; same defect as B2. |
| E3 | medium | The C# ownership check and removal both target a reusable name rather than the inspected immutable ID. Patch; same defect as B3. |
| E4 | medium | The SDK rejects custody beneath any enclosing Git checkout. Choosing only the inner repository's parent therefore fails a reachable nested layout. Patch; same custody selection defect as B5. |
| V1 | medium | Pre-verified broken-verification gap: the reserved client grants only `system`, while the request targets `tenant-a`; ordinary missing membership independently returns 403. Patch the real token to contain both grants and assert both before the existing refusal/persistence checks. |
| V2 | medium | The extraction containers have durable receipts and a transport label but are outside the new authentication cleanup inventory. Patch; same demonstrated leak as B1/E1. |
| V3 | medium | The reviewer reproduced a late untracked `late.cs` addition passing `assert_stable(..., diagnostic=True)`. Authenticated membership remains unaffected. Patch; same defect as B6. |

Grouped patch entries: extraction ownership (B1/E1/V2), cleanup continuation (B2/E2), immutable removal (B3/E3), retryable infrastructure disposal (B4), temporary custody (B5/E4), diagnostic membership (B6/V3), persisted identity assertions (B7), and mixed reserved-grant control (V1). Each is a direct internal correction for the demonstrated state, adds no public API, and preserves the approved intent. Deferred entries B11 and B12 belong to concurrent production-profile work. No frozen-intent renegotiation or spec loopback is required.

All eight grouped patches are implemented and verified. Fresh Debug/source checks passed 27/27 controls and all four authentication selectors 29/29. The actual SDK was forcibly interrupted after its first stopped extraction container was created; parent cleanup removed both it and Redis, a fresh query found zero owned containers, the workspace was removed and no matching job process remained. The complete post-review adapter passed all 21 lanes; shared Phase 4 regressions passed 264/264. The five additional stopped-extraction controls also passed. No introduced medium/high finding remains open; B11 and B12 are recorded in the deferred-work ledger for the concurrent production-profile owner.

## Design Notes

At planning, the invocation authorized scoped preparation under the execution clarification. Both investigation agents recommended integration repair before VG2. Historical logs showed missing Dapr components and foreign-lease refusal. Scanner repair was already delivered. The implementation and fresh evidence below supersede that planning-time state.

## Verification

- Establish the required Aspire baseline before edits using its lifecycle skill.
- Build the integration project in disposable Debug/source mode; invoke its assembly with xUnit single-dash selectors for affected classes.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_5_3.py'`; run shared Phase 4 regressions after adapter changes.
- `run-with-temp python3 -B tools/validate-story-5-3-nfr.py --logs <new-ignored-log-directory>` — retain fixed Release/package-mode evidence; missing prerequisites refuse.
- `run-with-temp bash scripts/check-committed-files.sh`; `git diff --check`. Use the mandatory completion validator for any slice `done`; retain parent status.

Recorded local results, with no delivery CI link or assurance claim:

- Baseline lifecycle prerequisite: disposable `aspire start --isolated`, `aspire describe`, and `aspire stop` exited 0. Resources were Starting/Waiting; this was not a healthy baseline qualification.
- Approved-baseline diagnostic `approved-baseline-6`: Debug/source build succeeded with 0 warnings/errors; ownership/TLS/cutover controls 20/20; all four authentication selectors 29/29 in 65.137 seconds, 183,992 output bytes. Selector discovery was JWT 12, contract 9, E2E security 6, smoke 2; no skips/errors/not-run. This copy omitted only the concurrent commit's 29 additions. Its source manifest SHA-256 is `baebcfcc2cbea8cf9d76224167e04ad091dca6ccd4ed2a363b642333b0675ebb`. It is not a current-tree adapter PASS.
- Current-tree full adapter `full-current-1`: truthful FAIL. Nine builds failed with CA1822; eight test lanes were explicitly unavailable after failed builds. Load-token 6/6, script preflight 56/56 and all seven scanner capability probes passed. The complete fixed plan was retained. Inventory SHA-256: `5393fc4620061c3728de6970e9c54ae6e20b58ae1447d75b3a8168232e102053`.
- Proposed-build-fix diagnostic `proposed-build-fix-2`: all 21 lanes reported; 20 passed. Integration passed 29/29 in 70.724 seconds, 189,482 bytes. The remaining scanner failure reported four new literal-only token-call assignments, subsequently changed to the existing explicit username-variable pattern without changing scanner rules. This result includes an unapplied production-profile proposal and is not current-tree evidence.
- Adapter controls: 26/26 in 16.622 seconds; the strengthened outer-budget control also passed separately. Final broader Phase 4 controls passed 261/261 in 818.048 seconds after the outer cleanup change.
- Real timeout-cleanup probe: an intentionally blocked child exceeded five seconds after starting a genuine owned Redis container. The expected `STORY_EVIDENCE_LIMIT` occurred; exact-owner cleanup removed the container and a fresh Docker query found zero owned containers. No foreign resource was mutated.
- `full-current-2` passed all 21 lanes, but the subsequent failure-path-hardened `full-current-3` reported integration 28/29. That intermittent failure remains a genuine FAIL rather than being replaced with the earlier PASS. Response instrumentation was confined to disposable copies and is not qualification evidence.
- Corrected protected-readiness check `final-authenticated-readiness-2`: current Debug/source build succeeded with 0 warnings/errors, controls 20/20 and authentication 29/29 in 76.954 seconds. The preceding wrong-header probe was correctly refused with 401 and is retained as a failed diagnostic.
- `final-release-stability-1`: fresh Release/package build and 20 controls passed, followed by four uninstrumented cohorts of all four selectors: 29/29 each in 68.063, 72.595, 64.527 and 69.338 seconds. All 116 cases passed with no failures/skips/errors/not-run; every cohort retained the 180-second and 1 MiB bounds and completed owned cleanup.
- Pre-review `run-with-temp python3 -B tools/validate-story-5-3-nfr.py --logs _bmad-output/implementation-artifacts/evidence/private-auth-parent/full-current-4`: PASS, all 21 lanes, no gaps or diagnostics, seven of seven scanner probes. Genuine .NET cases were 689/689 and script preflight 56/56; no failures/skips/errors/not-run. Integration was 29/29 in 84.671 seconds, 168,182 output bytes. Solution Release build: 0 warnings/errors. Inventory SHA-256: `2be3d53bba7eb3e16d3bab25888d6e6d46129e0b14f0f101a87cfa40a819f025`; unchanged plan SHA-256: `b3fb8169b66b70b24248a8312b5a5b67c7ff773409437f115b0d758b68bdeea7`. This is a dirty-source local diagnostic (`root_tree: null`), not authenticated completion consumption.
- `run-with-temp bash scripts/check-committed-files.sh`: PASS, 10,619 tracked files. `git diff --check`: PASS.
- Useful logs and exact argv/source inventories are ignored scratch under `evidence/private-auth-parent/`. They are diagnostics, never a reusable approval, seal or completion authority. Startup, output-bound and incomplete-custody failures remain recorded as FAIL.


Post-review final verification (supersedes the pre-review source snapshot):

- `run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/private-auth-parent/verify-review.py post-review-debug-1`: Debug/source build PASS, zero warnings/errors; ownership/TLS/cutover controls 27/27 in 3.209 seconds; complete authentication selectors 29/29 in 93.696 seconds, 160,938 output bytes. The actual SDK interruption reached real extraction-container creation and the expected 12-second bound. Exact-owner cleanup removed the stopped extraction container and Redis; a fresh query found none remaining. The wrapper removed its workspace, and a separate process check found no matching job processes.
- Focused implementation regression driver: both integration and Server.LiveSidecar Debug/source builds passed with zero warnings/errors; ownership/custody 7/7, extraction ownership 5/5 and both changed real-Keycloak cases 2/2. A timed-test analyzer failure was corrected by linking cancellation to the test token before any runtime cases ran; no failed-build assembly was used.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4*.py'`: PASS, 264/264 in 1014.074 seconds, including all 29 adapter controls. The added controls reject late untracked additions/mode drift without writing the shared index, continue after inspected/removed container disappearance, and report genuine errors after attempting remaining owned IDs.
- `run-with-temp python3 -B tools/validate-story-5-3-nfr.py --logs _bmad-output/implementation-artifacts/evidence/private-auth-parent/full-post-review-1`: PASS, all 21 lanes, no gaps or diagnostics, 689/689 .NET cases, 56/56 script cases and 7/7 scanner probes. Authentication 29/29 in 91.215 seconds, 175,408 output bytes, with exact selector discovery 12/9/6/2 and zero failures/skips/errors/not-run. Solution Release build: zero warnings/errors in 101.42 seconds. Inventory SHA-256 `203eb56142de351f2a4392d13376e753af68eb8b31e8697b15ce2ca827bcf44c`; unchanged plan SHA-256 `b3fb8169b66b70b24248a8312b5a5b67c7ff773409437f115b0d758b68bdeea7`; source count 19,280. This binds the tested dirty local source (`root_tree: null`) before these result annotations; it supplies no authenticated completion consumption or assurance authority.
- `run-with-temp bash scripts/check-committed-files.sh`: PASS, 10,619 tracked files. All disposable verification jobs exited; their owned resources and workspaces were cleaned. `git diff --check`: PASS.

Matrix audit:

| Frozen row | Executed coverage |
| --- | --- |
| Private prerequisites | `test_private_dapr_home_has_empty_components_without_ambient_copy`; bounded-process token/environment controls; complete adapter PASS |
| Authentication evidence | Four fixed selectors, positive discovery 12/9/6/2; post-review 29/29 in both Debug and Release with the fixed 180-second/1 MiB bounds; persisted tenant/aggregate/causation assertions and the signed mixed reserved-grant refusal |
| Foreign ownership | Real private-Redis foreign-marker/lease and restoration controls; exact-container-owner refusal control; TLS own/foreign certificate controls; all 27 post-review C# controls passed |
| Failure/termination | Real expected five-second timeout and owned-container removal; outer-budget ownership/retained-log control before/after startup; detached descendant/output-bound controls; actual SDK interruption during stopped-container extraction and zero remaining owned resources/processes; post-review broader Python 264/264 |

Completion writer: `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-9-2-story-5-3-private-authentication-integration.md --target spec --write` returned PASS with disposition `ordinary-slice`, derived story `9.2` and empty NFR/high-risk NFR coverage. Only this spec was marked done. The parent tracker remains unchanged at `in-progress`; VG2 and authenticated Story 5.3 consumption remain separate. No commit or push was made, as the approved frozen scope prohibits both.
