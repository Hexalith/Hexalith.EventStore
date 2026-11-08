---
title: 'Story 3.21 Dapr 1.19.1 runtime refresh and qualification'
type: 'chore'
created: '2026-11-08'
status: 'in-progress'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
baseline_commit: 'f14d56ea917dd33a552742226d6d7ee65f5e1236'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-08.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Downstream cryptography work needs November's approved runtime refresh qualified.

**Approach:** Qualify runtime/control-plane/CLI 1.19.1 with embedded components 1.19.0 through explicit selection; retain SDK 1.19.0. Parent 3.21 stays in progress.

## Boundaries & Constraints

**Always:** Preserve original lock/defaults and effective sealed subjects. Use Dapr, persisted readback and all 24 broker vectors; keep RabbitMQ 4.3.6-management/Kafka 4.3.1. Before mutation, bind exclusive disposable-worker ownership, a local socket and daemon identity. Preserve every unrelated specification; clean only owned IDs. Separate integrity, rejected guarantees and eligibility. Preserve history. Record tests/CI here, raw output externally; Story 9.1 machinery is superseded.

**Never:** Edit sealed `ci.yml`, `integration.yml`, `docs/ci.md`, original PF-02/Azure subjects, submodules, shared catalogs or dependencies. No production mutation, shared AppHost, Git mutation, publication, broker selection, crypto implementation or weakened guarantees. Builds/Platform remain handoffs; no 4.16 capability follows.

## I/O & Edge-Case Matrix

| Input/state | Required result | Failure handling |
| --- | --- | --- |
| Candidate selected | Matching source/release/binary/chart/OCI identities | Reject mismatch before launch |
| Legacy caller or retained packet | Original identity/default and interpretation | Never repin or relabel old proof |
| Shared/remote/conflicting/changed daemon | No resource mutation | Environment blocker; no rejected-endpoint cleanup |
| Startup failure/cancellation | Owned cleanup; complete baseline comparison | Changed/missing sentinel fails |
| Rejected or unexecuted broker vector | Distinct technical failure/blocker | Capture success never converts it to PASS |

</frozen-after-approval>

## Code Map

- `deploy/dapr/qualification/runtime-lock.json`: indirect sealed dependency; retain historical hashes.
- `tools/dapr_broker_qualification.py`: tuple/path are hardcoded in `validate_profile`/`validate_packet`; preserve observation checks.

## Tasks & Acceptance

**Execution:**
- [x] `deploy/dapr/qualification/runtime-lock-1.19.1.json`, versioned `public-identities/` — verify official source commits, release/archive/binary checksums, chart version/appVersion and amd64/arm64 OCI child digests; bind current sources.
- [x] `tools/{dapr_broker_qualification,qualify-dapr-brokers,validate-dapr-broker-qualification}.py` — support explicit profiles/approved tuples, exclusive-worker preflight, pinned endpoint/daemon and ID-based cleanup.
- [x] `tools/tests/test_dapr_broker_qualification.py`, new `test_dapr_runtime_qualification.py` — test identity/chart/architecture corruption, legacy packets, endpoint conflicts, missing exclusivity, daemon changes and startup/cancellation/sentinel failures beside positive controls. Retain catalog CI tests.
- [x] `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogRuntimeProfile.cs`, transport/options and profile tests — add matched selection; retain constructors/defaults/constants.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopment{Sidecar,Sentry}.cs`, `Integration/RouteCatalogHostedLiveTests.Transport*.cs`, `Fixtures/DaprTestContainerFixture.cs`, IntegrationTests' `Fixtures/DurableBrokerQualificationFixture.cs`, PublishedCompatibility.Tests' `QualificationRuntime.cs`, and `tools/qualify-internal-actor-admission.py` — bind explicitly selected binaries/control images; retain original invocations.
- [x] `.github/workflows/dapr-broker-qualification.yml`, new `dapr-runtime-qualification.yml` — use exclusive disposable jobs, isolated tools and suppressed bytecode; run the affected classes below with separate outcomes.
- [x] `deploy/dapr/qualification/README.md`, this spec — document selection/outcomes and owners: Builds catalogs/tools, Platform rehearsal, independent sharding/PF-02/Azure subjects and pending 4.16 proof.

**Acceptance Criteria:**
- Given original consumers, when candidate support is installed, then locks/defaults/sealed subjects remain unchanged and compatibility passes.
- Given accepted infrastructure, when qualification runs, then all broker and affected routing/token/ACL, actor/reminder, scheduler/placement, snapshot/restart/restore and compatibility checks execute on observed new identities.
- Given a failed/skipped/blocked selected check, when results are recorded, then refresh qualification remains incomplete; production eligibility stays FAIL and selected broker null.

### Review correction requirements (implementation pass 2)

These requirements refine the non-frozen implementation tasks; the approved intent and acceptance remain unchanged.

- `tools/dapr_runtime_qualification.py`: merge any supplied command environment exactly once, then remove conflicting Docker selectors. The real `Run.container` path must have a synthetic positive test, including `env=None` and supplied broker credentials. Preserve the accepted socket/daemon and exclusive allocation checks.
- `tools/dapr_broker_qualification.py`: restrict the candidate to schema 2; require both observed placement/scheduler identities for executed candidate qualification. Bind worker, socket, endpoint and daemon custody to actual command observations and every Docker operation. Compare candidate execution sources with the independently trusted owning checkout, not only two packet-controlled copies. Add resealed downgrade, omitted identity, unrelated custody and retained-lock-plus-both-source substitution tests. Preserve original schema 1 packet interpretation.
- `tools/qualify-dapr-runtime.py`, `tools/qualification-docker.py` and focused tests: admit the loaded candidate tuple before output/infrastructure creation, rather than trusting a filename. Own the xUnit process group and terminate/reap descendants on success, failure, timeout and interruption. Track child-created Docker resources through the pinned adapter by immutable identities and verified ownership; safely clean every owned child resource or record unresolved ownership/cleanup failure. Never report complete cleanup while child processes/resources remain. Test a real synthetic grandchild and fake child-owned Docker resources without using the shared daemon.
- `tools/qualify-dapr-brokers.py`: attempt independent owned-ID cleanup after a per-container failure while custody remains valid. Stop mutation if custody changes; retain every unresolved identity. Test continuation after one removal failure and the full preservation denominator.
- `tools/qualify-internal-actor-admission.py`, `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorCallbackAdmissionProcessTests.cs` and `.github/workflows/dapr-runtime-qualification.yml`: explicitly execute the selected admission/process-replacement regression, forwarding the selected profile/worker and asserting observed runtime/control-image identities. Give this lane its own disposable worker/lease ownership; do not deadlock a nested claimant against the runtime wrapper's held allocation. Retain legacy invocations. Hash unrelated inspection output before any receipt persistence so unrelated credentials never enter retained commands. Add an executed synthetic credential-retention check for selected baseline inspection.
- `.github/workflows/dapr-runtime-qualification.yml`: make the exact historical EventStore tags/source and Builds commit required by `ConsumerMaterializer` available without nested submodule updates. Retain bounded, sanitized compatibility source/process/restore/cleanup witnesses in the uploaded runtime artifact, excluding private credentials and bulky materialized consumer/package trees. Test the new orchestration and witness-selection helpers.

Local re-derivation aid: the superseded implementation's own code is preserved at `/tmp/story-3-21-preservation-pass2-f2fmsdgz` with `manifest.json`. Its known-good pieces may be reused under the KEEP instructions below; it contains the reviewed defects and is not qualification proof. The live implementation code was restored to the initial HEAD before this pass. Versioned public identities, the original lock, documentation and prior verification history were preserved.

### Review correction requirements (implementation pass 3)

- Preserve every pass-2 correction and passing control. In `ExclusiveDocker.run`, apply the owning runner's credential redaction to both normalized and executed receipt arguments; do not overwrite sanitized argv with raw arguments. Test the RabbitMQ add-user credential path. In xUnit diagnostic sanitization, trust only the exact generated `diagnostic-sha256:` plus 64 lowercase hex digits; arbitrary diagnostic strings with that prefix are untrusted.
- Bind the entire candidate input inventory, including the executed probe/proxy and helper sources, to the trusted owning checkout/profile. Reject a resealed retained lock that substitutes helpers or drops inventory. Bind each complete before/after inventory to its own exact command observations, including empty inventories and each resource kind; deleting baseline entries must fail. For executed placement/scheduler observations, retain command references for image extraction, measured executable hash and native launch, rather than accepting approved placeholders alone. Preserve integrity-valid blocked startup observations and original schema 1 compatibility.
- `tools/run-dapr-runtime-lane.py`, the broker/admission owners and focused controls: direct xUnit lanes need parent-side Docker reconciliation after timeout/cancellation, in addition to native descendant cleanup. Durably record exact resource intents/IDs and ownership before/after each create; retain the complete initial inventory before mutation. Bind the parent to the same accepted worker/socket/daemon before child launch, release its lease for the child owner, then reacquire and revalidate custody for independent cleanup after the child exits or is killed. Clean only independently verified owned identities; unresolved creation or changed custody must produce explicit incomplete cleanup. Compare the full initial inventory even when child shutdown was interrupted. Convert actual SIGTERM into controlled cancellation, retain the final result, and do not depend on three seconds being enough for child Docker cleanup. A passing direct lane requires successful child outcomes, process cleanup, Docker cleanup and preservation. Test actual SIGTERM plus a synthetic child resource owner whose cleanup outlasts the supervisor grace; no shared Docker is needed.
- `tools/dapr_runtime_qualification.py`, `tools/qualification-docker.py`: normalize `docker container create/run` aliases before journaling. Implicit named, anonymous, image-declared and `--mount`/equals-form volume creation must either be tracked with demonstrated creation ownership and safely reconciled, or be rejected before the resource-creating command. Never report complete cleanup with an unjournaled volume. Selected fixtures must still have a supported path. Add fake-engine controls for aliases, implicit volumes and exact identity-preserving cleanup.
- Serialize sanitized compatibility witness bytes exactly once, enforce limits against those exact bytes, and write them unchanged. Record separate original commitments and actual retained hashes/sizes; verify the retained manifest against actual files.
- Add execution tests for the installed child adapter with a fake Docker executable and temporary socket: allocation replacement/content changes, socket replacement, daemon changes, and positive commands. Exercise both runtime and direct-lane result entry points with empty, skipped, failed and passing synthetic xUnit reports; require truthful exit codes and retained outcomes.

## Implementation Notes

Implementation checkboxes record delivered code and input validation, not live
qualification or parent-story completion. Status remains `in-progress`.

- Added the explicit approved 1.19.1/1.19.1/1.19.0 profile, verified official
  latest-stable runtime/CLI and SDK 1.19.0 indexes, source tags/commits, release
  checksums/executables, chart and both architecture-specific OCI children.
  Placement/scheduler/Sentry amd64 executable hashes were independently computed
  from their retained child manifests and verified layer bytes without Docker.
- Original `runtime-lock.json` bytes and all 19 of its input hashes are preserved.
  Versioned historical input copies resolve original checkout consumers; schema 1
  packets retain their original paths/interpretation. Schema 2 separately binds
  current capture sources, preventing current tools from masquerading as old proof.
- New captures require an exclusive worker allocation and hold its file lock;
  pin the local socket and daemon; reject conflicts/identity changes; track exact
  container IDs; and compare all baseline container/network/volume specifications
  even after startup/cancellation failure. Selected .NET child Docker commands use
  the same identity-checking adapter. Rejected infrastructure receives no cleanup
  mutation. Unresolved owned custody fails integrity and retains private material.
- Optional immutable C# profiles preserve existing constructors, deconstruction,
  constants and defaults. Explicit fixture selection binds native binaries and
  control images; PublishedCompatibility's default remains its original 1.18.4.
- Independent manual CI jobs install isolated tools, suppress bytecode, build
  package/Release projects and run built xUnit classes (brokers `-explicit only`).
  Selected live skips/empty execution fail. Broker capture, integrity, guarantees
  and production eligibility remain separate outcomes. CI materializes existing
  root submodules at their gitlinks for package-catalog access; no nested/remote
  updates, catalog edits or dependency changes are introduced.


## Spec Change Log

- 2026-11-08, review loop 2: A second review exposed implicit child volume ownership and direct-lane parent reconciliation gaps in the non-frozen orchestration contract. Clarified durable child resource intent/identity receipts, independent parent cleanup and complete preservation after cancellation, supported/rejected volume creation forms, plus the demonstrated receipt/source/inventory/diagnostic corrections above. Restored only this run's live code before re-derivation. **KEEP:** all prior KEEP constraints, verified public identities and protected bytes; all pass-2 environment/custody/schema/source and selected-admission fixes; escaped-descendant/subreaper handling and child resource journal; exact compatibility history preparation and bounded sanitized witness projection; 76 packet/runtime, 11 admission, 6 catalog and 23 C# controls; four clean Debug/source builds; truthful blocked captures and separate qualification/authority results. The parent independently reran all 116 focused tests and lint successfully; C# 23/23 completed with zero skips in 80.858 seconds. Earlier results remain historical, not proof for unverified pass-3 edits.

- 2026-11-08, review loop 1: Review found missing descendant/resource ownership and selected admission execution/witness handling in the non-frozen implementation tasks. Clarified those contracts and the demonstrated environment, packet-validation and cleanup corrections above, avoiding accepted-worker startup failure, false technical PASS, orphaned children and retained unrelated credentials. Restored only this run's live code before re-derivation; no pre-existing user changes were reverted. **KEEP:** preserve verified official runtime/CLI/control-plane/chart identities, SDK and broker versions, all 19 exact historical inputs, unchanged original lock/defaults/constructors/sealed subjects, explicit selection and non-promoting lanes, complete 24-vector denominator, truthful blockers, the passing deterministic controls and documented owner handoffs. Preserve the frozen intent and earlier verification as history; rerun changed checks before recording new results.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R3-B1 | high | patch | Runtime Redis is created without an explicit pull although local image inspection is now required; selected admission also lacks image preparation. Fresh worker launch is blocked. |
| R3-B2 | high | patch | RouteCatalogDevelopmentRedis supplies no mount for the pinned Redis image data volume, so the selected adapter rejects this existing caller. Provide supported selected-only storage. |
| R3-B3 | medium | patch | Pending network cleanup compares an unresolved name to an ID inventory after inspect failure, permitting false absence. Use exact name reconciliation. |
| R3-B4 | high | patch | The installed adapter forwards non-creation rm without journal ownership checks; the supplied fake-tool reproduction deletes a pre-existing unrelated container. |
| R3-B5 | medium | patch | Container option parsing accepts -l while reserved-label rejection scans only --label, allowing creation with overridden ownership before post-create refusal. |
| R3-B6 | medium | patch | Run loads args.profile but retains ROOT/canonical-profile, so an admitted variant can execute with different recorded lock bytes. Bind actual loaded bytes or reject unsupported variation. |
| R3-B7 | medium | patch | Runtime cleanup starts before Run.finish suppresses SIGTERM; its active handler raises KeyboardInterrupt, which Exception handlers do not catch. Repeated termination can bypass reconciliation/results. |
| R3-B8 | medium | patch | Volume inspection precedes create while --pull=always remains accepted, so mutable replacement can invalidate the inspected volume configuration. Bind the inspected image or reject pulling creation. |
| R3-B9 | high | patch | XML sanitizer hashes text/tail only and leaves name/custom attribute diagnostic values untouched; supplied synthetic credentials remain in attributes. |
| R3-B10 | high | patch | Adapter scans application/container payload arguments for global -c/endpoint selectors, rejecting ordinary /bin/sh -c commands. Restrict transport-option parsing to the global prefix. |
| R3-E1 | high | patch | Same cold-worker missing image preparation as R3-B1. |
| R3-E2 | high | patch | Same installed-adapter unowned mutation path as R3-B4. |
| R3-E3 | medium | patch | Private directory registration is lazy until the first create; a killed child can leave a directory while parent owners=[] reports complete. Register private custody at initialization. |
| R3-V1 | medium | patch | Pre-verified regression gap: C# materialization has no modified-byte, resealed-unapproved-hash or mismatched-version rejection controls; valid live paths cannot detect removal of that guard. |

Third-review root groups: R3-B1/R3-E1 (image preparation), R3-B4/R3-E2 (owned-target mutation). All findings have local corrections using existing custody/source/result contracts; no public API or intent changes are required. Re-engage the pass-3 implementer for minimal patches and focused controls.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R2-B1 | high | patch | run overwrites sanitized argv with raw arguments; the reachable RabbitMQ add_user call supplies the disposable password, leaking it to command receipts. |
| R2-B2 | medium | patch | Trusted comparison covers only three modules; the executed probe/proxy remain packet-controlled after lock resealing. Bind the complete trusted input inventory. |
| R2-B3 | medium | patch | Snapshot-to-receipt binding is subset-only; empty retained snapshots trivially satisfy it despite nonempty observed inventories. Require complete corresponding inventories. |
| R2-B4 | medium | patch | Approved control-plane strings are not tied to extraction/hash/native-launch receipts; source inspection confirms those command references are absent. |
| R2-B5 | high | patch | The direct lane installs no SIGTERM handler; default termination bypasses its try/finally while children use separate sessions. |
| R2-B6 | medium | patch | Namespaced container create/run reach the adapter but bypass the helper creation parser and ownership journal. |
| R2-B7 | medium | bad_spec | The child ownership contract does not account for implicit named/image/alternate-syntax volumes; container-only cleanup can report complete while those volumes remain. Clarify supported or rejected creation forms. |
| R2-B8 | low | patch | Witness cap checks compact serialization while write_json writes indented bytes, so recorded and actual retained sizes diverge. Serialize once. |
| R2-B9 | low | patch | The manifest commits original private bytes and omits a digest of the sanitized retained bytes; add explicit retained commitments. |
| R2-B10 | high | patch | Diagnostic strings beginning with the marker bypass sanitization without validating a digest; supplied synthetic private text survives unchanged. |
| R2-E1 | medium | patch | Same namespaced creation/journal bypass as R2-B6, reproduced by the reviewer with a fake engine. |
| R2-E2 | medium | bad_spec | Same unjournaled implicit volume ownership gap as R2-B7; fake container removal leaves the volume while complete is true. |
| R2-E3 | medium | bad_spec | Direct broker/admission Docker owners are inside the process tree killed after a three-second grace, with no independent parent Docker reconciliation. Refine the non-frozen parent/child lifecycle contract. |
| R2-V1 | medium | patch | Pre-verified regression gap: helper tests bypass the installed adapter allocation/socket/daemon checks. Add subprocess adapter controls. |
| R2-V2 | medium | patch | Pre-verified regression gap: neither result entry point is tested with empty/skipped/failed/passing reports. Add synthetic adjudication controls. |

Loopback roots: R2-B7/R2-E2 (implicit volume custody) and R2-E3 (direct-lane independent cleanup). All other corrections are carried into pass-3 implementation requirements. No frozen-intent ambiguity was found.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | patch | Container startup always forwards env and raw supplies it again; a direct synthetic call reproduced TypeError before launch. |
| B2 | high | patch | ConsumerMaterializer requires tagged root source and SelectedBuilds Git objects; default shallow checkout does not supply them. |
| B3 | high | patch | A candidate fixture downgraded to schema 1 passes with missing custody and incomplete cleanup; reproduced locally. |
| B4 | medium | bad_spec | The xUnit command uses subprocess.run without process-group or child-resource ownership; owner.finish only knows parent-owned entries. Non-frozen orchestration tasks need this lifecycle contract. |
| B5 | medium | patch | The entire owned-container removal loop shares one try; one removal exception skips remaining safe owned IDs. |
| B6 | high | patch | Replacing candidate custody with unrelated endpoint/daemon values still returns technical PASS in the synthetic fixture. |
| B7 | high | patch | The executed candidate positive fixture omits controlPlane and still passes; both observed identities are currently optional. |
| B8 | medium | patch | Substituting both retained capture copies and the retained lock hash passes; reproduced independently. Current-source binding needs the trusted checkout. |
| B9 | medium | patch | Compatibility scratch defaults outside QUALIFICATION_OUTPUT, so the workflow upload omits its detailed witnesses. |
| B10 | high | patch | Admission checks only args.profile.name; an original lock renamed to the candidate name can reach original preparation while advertising candidate selection. |
| B11 | medium | bad_spec | The selected actor-admission branch has no selected C# caller or CI class. Refine non-frozen execution tasks and provide separate worker ownership. |
| E1 | high | patch | Same demonstrated duplicate-env startup failure as B1. |
| E2 | high | patch | Same reproduced schema downgrade as B3. |
| E3 | high | patch | Same demonstrated optional control-plane identities as B7. |
| E4 | medium | bad_spec | Same xUnit descendant lifecycle gap as B4; direct process timeout cannot establish descendant cleanup. |
| E5 | high | patch | Claim pass reproduced the same accepted-infrastructure startup failure as B1; not an additional root cause. |
| V1 | medium | bad_spec | Pre-verified regression gap: nine Python controls and the C# caller use legacy setup; candidate CI excludes the admission process test. Group with B11; the missing ownership/execution contract needs non-frozen task clarification. |
| V2 | high | patch | Selected shared_specs calls run(inspect), which persists raw Config.Env stdout before hashing. The supplied synthetic secret-retention demonstration follows the persisted command path. |

Grouped loopback roots: B4/E4 (descendant custody) and B11/V1 (selected admission execution). Lower patch entries are retained above as re-derivation correction requirements; they were not patched into the superseded code. No frozen-intent ambiguity was found.

## Design Notes

No intent gaps or irreversible actions. Changes span tooling, fixtures, workflows and an optional public profile.

## Verification

- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'`; original catalog tests; changed-workflow `actionlint`; `git diff --check`.
- Build individual .NET projects in Debug/source mode locally, Release/packages in CI. Run built xUnit assemblies using `-class`; brokers require `-explicit only`. Record blockers.
- Reuse broker qualification; Server live route catalog/native actor, actor concurrency/tenant isolation, reminder recovery, snapshot reload; and PublishedCompatibility security/restore classes. Bind observed candidate identities; require zero selected live skips.
- Run candidate producer and independent validator separately with `--allow-failures`, normally, and `--require-production-authority`.

Baseline: `python3 -B -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py`: 25/25 passed at the baseline SHA. Docker Desktop is shared; no accepted worker. No resources created or CI run. Candidate unqualified.

### Implementation validation — 2026-11-08

Working HEAD: `b437879df2339692d177774d1f076936da6ebd62`; original spec baseline remains unchanged. No commit,
branch, push, publication, cluster/shared-AppHost start or Docker resource mutation
was performed. CI has not been dispatched, so there is no CI run link yet.

| Command / check | Result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` | 57/57 PASS, including original 25 packet controls, candidate identities/packets and exclusive-worker/startup/cancellation/sentinel failures |
| `python3 -B -m unittest discover -s tools/tests -p test_route_catalog_ci_tools.py` | 6/6 PASS; sealed catalog CI controls retained |
| `python3 -B -m unittest discover -s tools/tests -p test_qualify_internal_actor_admission.py` | 9/9 PASS, including actual owned-process signal/owner-loss cleanup |
| `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` | PASS, 0 warnings/errors |
| Same Debug/source build for `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj` | PASS, 0 warnings/errors |
| Same Debug/source build for `tests/Hexalith.EventStore.PublishedCompatibility.Tests/Hexalith.EventStore.PublishedCompatibility.Tests.csproj` | PASS, 0 warnings/errors |
| Same Debug/source build for `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` (final incremental run adds `--no-restore`) | PASS, 0 warnings/errors |
| `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*DevelopmentRouteCatalogRuntimeProfileTests' -class '*DevelopmentRouteCatalogAspireTests'` | 23/23 PASS, 0 skipped/not-run |
| `actionlint .github/workflows/dapr-broker-qualification.yml .github/workflows/dapr-runtime-qualification.yml` | PASS |
| `git diff --check` | PASS |
| SHA-256/byte comparison with HEAD of original lock, `ci.yml`, `integration.yml`, `docs/ci.md`; all 19 historical input hashes | PASS, original lock remains `326a24228313b4f76428f3091bfa449ba00c6a37fc4bdca55fb78409ab433238` |

The first LiveSidecar build used `--no-restore` and failed with CS1704 because
existing assets mixed Tenants source and package assemblies. Re-running the exact
Debug/source command above with restore passed; no build gate was weakened.
Initial implementation-only CA1416/CA1822 diagnostics were fixed before the passing
builds. An importlib-based actor-admission test exposed a helper import-path issue;
it was fixed and its nine controls pass.

Candidate producer command (exit 1):

```bash
QUALIFICATION_DAPRD=/tmp/eventstore-dapr1191-tools/runtime/daprd PATH=/tmp/eventstore-dapr1191-tools/cli:$PATH python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-candidate-blocked-20261108-verified
```

All 24 vectors recorded `infrastructure-blocker`: **exclusive disposable-worker
allocation is required before mutation**. Only Docker context/daemon-ID reads ran;
no container/image/volume/network mutation or rejected-endpoint cleanup ran.
Private temporary material was removed, owned resources remaining were empty.

Independent commands against `/tmp/story-3-21-candidate-blocked-20261108-verified`:

- `python3 -B tools/validate-dapr-broker-qualification.py /tmp/story-3-21-candidate-blocked-20261108-verified --allow-failures`: exit 0, integrity PASS; technical qualification FAIL, all 24 blocked vectors retained.
- Same validator without flags: exit 1, technical qualification FAIL.
- Same validator with `--require-production-authority`: exit 1, production eligibility FAIL; authenticated AD-26 Architecture/Platform records, Story 3.19 profile/publication binding, Kubernetes rehearsal/restore and Story 4.16 proof remain absent.

Raw output and the packet stay under `/tmp/story-3-21-*`; no run output is committed.
The live broker guarantees, selected routing/security/actor/reminder/scheduler/
placement/snapshot/restart/restore/compatibility classes and CI executions remain
**unexecuted or blocked**, not passed by these deterministic checks. Builds shared
catalog/tool adoption and Platform composed Kubernetes rehearsal/restore remain
owner handoffs. Independent sharding/PF-02/Azure subjects and Story 4.16 re-proof
remain pending. Parent 3.21 stays in progress; candidate qualification remains
incomplete; production eligibility is FAIL and selected broker is null.

Selected runtime-wrapper admission was also exercised without allocation (exit 1):

```bash
QUALIFICATION_DAPRD=/tmp/eventstore-dapr1191-tools/runtime/daprd PATH=/tmp/eventstore-dapr1191-tools/cli:$PATH python3 -B tools/qualify-dapr-runtime.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-runtime-blocked-20261108 --assembly tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll --class '*RouteCatalogHostedLiveTests' --class '*ActorConcurrencyConflictTests' --class '*ActorTenantIsolationTests' --class '*ReminderRecoveryLiveSidecarTests' --class '*FoldedSnapshotReloadLiveSidecarTests'
```

`regression-results.json` records the same missing-exclusive-worker blocker, zero
executed classes, technical qualification FAIL and production eligibility FAIL.
Cleanup completed; only read-only Docker admission commands ran. Regression
receipts are distinct from the complete broker packet and never masquerade as it.


### Implementation pass 2 validation — 2026-11-08

Re-derived the preserved implementation and corrected the review-loop defects.
The execution checkboxes above record delivered code/input validation only.
The frozen intent, original baseline, previous verification and `in-progress`
status are preserved. Working HEAD remains
`b437879df2339692d177774d1f076936da6ebd62`; no Git mutation, publication,
production/shared-AppHost start, submodule/catalog/dependency update or shared
Docker resource mutation was performed. CI has not been dispatched.

- Docker command environments merge once, including explicit `env=None` and
  broker credentials, before conflicting selectors are removed. Accepted worker
  allocation contents/inode, socket identity and daemon are rechecked. Candidate
  packets require schema 2, both placement/scheduler observations for executed
  results, command-bound custody and execution-source comparison with the
  independently trusted owning checkout. Original schema 1 interpretation stays
  intact.
- Cleanup attempts every independently owned container after a removal failure;
  custody loss stops further mutation and retains each unresolved ID. The xUnit
  wrapper owns a process group, uses Linux subreaper and process start identities
  to track escaped sessions, and terminates/reaps descendants on every exit path.
  The pinned child Docker adapter journals resource creation and verifies immutable
  container/network IDs and volume creation/owner commitments before cleanup.
  Unresolved child processes/resources fail cleanup and retain private custody.
- The admission process-replacement C# test forwards selected profile/worker,
  asserts observed runtime/control-image identities and checks replacement-sidecar
  observations. Its CI lane owns a separate disposable worker and allocation,
  avoiding nested lock claims. Unrelated baseline inspection is hashed before any
  receipt persistence, covering containers, networks and volumes.
- Compatibility orchestration supplies the three historical EventStore tags,
  selected source and exact Builds commit without nested updates. Private scratch
  remains outside the uploaded artifact; bounded source/process/restore/cleanup
  witnesses are projected into `compatibility-witnesses/`, with diagnostic strings
  committed by hash and private/materialized consumer/package trees excluded.

| Command / check | New result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` | 76/76 PASS, including legacy packet controls; resealed downgrade/source/custody/identity corruption; real container environment forwarding; independent cleanup; inherited/escaped real grandchildren on success/failure/timeout/interruption; fake child Docker resources; orchestration/witness projection |
| `python3 -B -m unittest discover -s tools/tests -p test_qualify_internal_actor_admission.py` | 11/11 PASS, including executed selected baseline credential-retention and renamed-original-profile rejection controls |
| `python3 -B -m unittest discover -s tools/tests -p test_route_catalog_ci_tools.py` | 6/6 PASS |
| `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` | PASS, zero warnings/errors |
| Same Debug/source build for AppHost.Tests, PublishedCompatibility.Tests and IntegrationTests individually | PASS, zero warnings/errors in each |
| `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*DevelopmentRouteCatalogRuntimeProfileTests' -class '*DevelopmentRouteCatalogAspireTests'` | 23/23 PASS, zero skipped/not-run |
| `actionlint .github/workflows/dapr-broker-qualification.yml .github/workflows/dapr-runtime-qualification.yml` | PASS |
| `git diff --check`; original lock/sealed-input byte comparisons; all 19 historical input hashes | PASS; original lock SHA-256 remains `326a24228313b4f76428f3091bfa449ba00c6a37fc4bdca55fb78409ab433238` |

Final pass-2 blocker captures are under
`/tmp/story-3-21-pass2-final-blocked-9idbugo2/` (raw output remains external).
Using `QUALIFICATION_DAPRD=/tmp/eventstore-dapr1191-tools/runtime/daprd` and
`PATH=/tmp/eventstore-dapr1191-tools/cli:$PATH`, with no worker allocation:

```bash
python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-pass2-final-blocked-9idbugo2/broker
python3 -B tools/validate-dapr-broker-qualification.py /tmp/story-3-21-pass2-final-blocked-9idbugo2/broker --allow-failures
python3 -B tools/validate-dapr-broker-qualification.py /tmp/story-3-21-pass2-final-blocked-9idbugo2/broker
python3 -B tools/validate-dapr-broker-qualification.py /tmp/story-3-21-pass2-final-blocked-9idbugo2/broker --require-production-authority
python3 -B tools/qualify-dapr-runtime.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-pass2-final-blocked-9idbugo2/runtime --assembly tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll --class '*RouteCatalogHostedLiveTests'
python3 -B tools/qualify-internal-actor-admission.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --evidence /tmp/story-3-21-pass2-final-blocked-9idbugo2/admission
```

The producer exits 1 and records all 24 vectors as infrastructure blockers:
`exclusive disposable-worker allocation is required before mutation`. Independent
validator exits are respectively 0, 1 and 1: integrity PASS, technical qualification
FAIL and production eligibility FAIL. Runtime and selected admission exit 1 with
zero executed selected checks and successful private/owned cleanup. Captured
Docker commands are exclusively context/daemon reads; no rejected endpoint receives
cleanup mutation. These deterministic controls and blocked captures establish no
live runtime/broker guarantee.

The selected broker, routing/security, admission/process replacement, actors,
reminders, scheduler/placement, snapshots and compatibility/restore lanes still
need execution on an accepted disposable worker, with zero selected live skips.
The new CI jobs have not run. Builds catalog/tool adoption, Platform composed
Kubernetes rehearsal/restore, independent sharding/PF-02/Azure subjects and Story
4.16 runtime-minor proof remain pending. Candidate qualification is incomplete;
production eligibility stays FAIL, selected broker stays null, and parent 3.21
stays in progress.

### Parent verification and matrix audit — implementation pass 2

The parent reran 76/76 packet/runtime tests, 11/11 admission tests, 6/6 catalog tests, actionlint for both manual workflows and `git diff --check`; all passed. The real descendant lifecycle and three child-resource controls were also independently exercised before the full pass. Matrix coverage is present in executed identity corruption/positive controls (candidate identities), original packet/profile controls (legacy behavior), worker custody controls (endpoint/exclusivity/daemon changes), startup/cancellation/process/resource preservation controls (owned cleanup), and rejected/blocked packet plus real preflight refusals (truthful incomplete qualification). The selected live checks remain blocked by unavailable exclusive infrastructure, as recorded above.

### Implementation pass 3 validation — 2026-11-08

Preserved the complete pass-2 implementation and corrected all pass-3 requirements.
Execution checkboxes record delivered code/input validation only. The frozen intent,
original baseline and earlier verification remain unchanged. Working HEAD remains
`b437879df2339692d177774d1f076936da6ebd62`. No Git mutation, publication,
production/shared-AppHost start, dependency/catalog/submodule update or shared
Docker resource mutation was performed. CI has not been dispatched; there is no
new CI run link. The candidate lock binds 73 current inputs and has SHA-256
`dc4a35f482e9ff9f75c87156f5aca22f262e545292fe2878866b6f72575f9466`.

- Both normalized and executed Docker arguments retain the owning runner's
  credential redaction, including RabbitMQ `add_user`. Candidate input and tool
  inventories are compared completely with the trusted owning checkout/profile.
  Independent before/after inventory chains cover every resource kind and empty
  lists; deleting entries or reusing earlier observations fails. Executed control
  identities bind image creation/extraction, measured executable and native launch.
  Original schema 1 packets still validate with POSIX `fcntl` unavailable.
- Direct xUnit lanes bind the initial worker/socket/daemon and full baseline,
  release the lease for the child, then reclaim/revalidate the same custody before
  independent cleanup. Atomic, fsynced child journals retain absence checks,
  creation intents, ownership tokens, IDs and private-directory identities. SIGTERM
  enters controlled cancellation. A real synthetic child whose cleanup sleeps
  15 seconds is killed after the native grace; the parent still reconciles its
  exact Docker identity and private directory and preserves every baseline resource.
  Changed custody or unresolved ownership explicitly fails cleanup.
- The installed adapter normalizes namespaced create/run, admits explicit binds,
  tmpfs and independently measured journal-owned volumes, and rejects implicit or
  uncovered image volumes before creation. Internal owners explicitly use tmpfs
  for unused image scratch paths. Unsupported global/bulk commands cannot bypass
  the journal. Actual subprocess controls cover allocation replacement/content,
  socket replacement, daemon changes and positive identity-preserving cleanup.
- Compatibility witnesses are serialized once; limits, retained hashes and sizes
  describe those exact written bytes and the manifest is verified against files.
  Only exact generated diagnostic digests are trusted. Both result entry points
  reject empty/skipped/failed reports, accept passing synthetic reports only after
  successful cleanup, and replace interrupted XML containing synthetic private text
  with digest-only failed-report witnesses.

| Command / check | Pass-3 result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` | 93/93 PASS; independent parent rerun 93/93 PASS in 14.264 seconds |
| `python3 -B -m unittest discover -s tools/tests -p test_qualify_internal_actor_admission.py` | 11/11 PASS; independent parent rerun PASS |
| `python3 -B -m unittest discover -s tools/tests -p test_route_catalog_ci_tools.py` | 6/6 PASS; independent parent rerun PASS |
| `actionlint .github/workflows/dapr-broker-qualification.yml .github/workflows/dapr-runtime-qualification.yml` | PASS; independent parent rerun PASS |
| `git diff --check` | PASS; independent parent rerun PASS |
| Parent frozen-block/protected-subject/historical-input/submodule audit | PASS; original lock, `ci.yml`, `integration.yml`, `docs/ci.md`, all 19 historical hashes and existing gitlinks preserved |
| Parent comparison of the 13 restored C# files with pass-2 preservation | Exact byte matches; four clean Debug/source builds and 23/23 zero-skip AppHost tests above remain retained evidence, not newly rerun C# checks |

The parent executed this correctly quoted missing-allocation capture (exit 1):

```bash
env -u QUALIFICATION_EXCLUSIVE_WORKER QUALIFICATION_DAPRD=/tmp/eventstore-dapr1191-tools/runtime/daprd PATH="/tmp/eventstore-dapr1191-tools/cli:$PATH" python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-parent-pass3-blocked-20261108
```

All 24 vectors record `exclusive disposable-worker allocation is required before
mutation`. Independent validator commands against that directory return 0 with
`--allow-failures` (integrity PASS, technical qualification FAIL), 1 without flags,
and 1 with `--require-production-authority`. Captured Docker commands are only
context inspection and pinned daemon-ID reads. Cleanup is complete, no owned
resources remain and private material is removed. The parent's initial unquoted
PATH attempt exited 127 in `env` before producer startup; the quoted command above
is the actual qualification capture.

Selected runtime and admission commands were also run with the same isolated
tool environment and no worker allocation:

```bash
python3 -B tools/qualify-dapr-runtime.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-pass3-blocked-qszm2sjm/runtime --assembly tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll --class '*RouteCatalogHostedLiveTests'
python3 -B tools/qualify-internal-actor-admission.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --evidence /tmp/story-3-21-pass3-blocked-qszm2sjm/admission
```

Both exit 1 for the same missing-allocation blocker, execute zero selected checks
and retain successful private/owned cleanup. Runtime results retain technical and
production FAIL with selected broker null; admission retains qualification false
and cleanup true. These captures establish no live guarantee.

Raw output stays under the two `/tmp/story-3-21-*` paths above. All selected live
broker, routing/security, admission/process replacement, actor/reminder,
scheduler/placement, snapshot/restart/restore and compatibility checks still need
an accepted disposable worker and zero selected live skips. CI remains unexecuted.
Builds catalog/tool adoption, Platform composed Kubernetes rehearsal/restore,
independent sharding/PF-02/Azure subjects and Story 4.16 runtime-minor proof remain
owner handoffs. Candidate qualification remains incomplete; production eligibility
stays FAIL, selected broker stays null and parent Story 3.21 stays in progress.

### R3 follow-up corrections and final verification — 2026-11-08

The following implementation corrections supersede the earlier pass-3 code/hash
subject while preserving its verification as history. The frozen intent, original
baseline and parent story remain unchanged and `in-progress`.

| Patch | Resolution | Focused evidence |
| --- | --- | --- |
| R3-1: cold selected image cache | Runtime Redis and every selected admission image are explicitly pulled by immutable reference before inspection/creation; legacy admission keeps its original image invocation. The executed cold-cache control also exposed and fixed missing admission custody-command receipts. | Actual fake-engine cold-cache admission preparation; cold-cache runtime entry-point controls |
| R3-2: selected Redis fixture storage | Selected route-catalog Redis adds explicit `/data` tmpfs, retaining its ephemeral behavior and original unselected arguments. | LiveSidecar Debug/source build; supported tmpfs adapter controls |
| R3-3: unresolved network name | Pending networks without an ID reconcile against exact network names; failed inventories remain unresolved. | Pending-network inspection-failure control |
| R3-4: unrelated mutation targets | Installed-adapter mutations require an exact measured journal identity and verified ownership; unsupported mutations are rejected. | Installed-adapter owned mutation and unrelated rm/stop/exec/copy controls |
| R3-5: short ownership labels | Reserved ownership labels are checked through normalized `--label`/`-l` options, including equals forms, before creation. | Short-option ownership override controls |
| R3-6: executed/retained profile mismatch | The runner retains the exact loaded profile bytes at the canonical retained path, including admitted topic/OpenBao variants; original packet interpretation is preserved. | Candidate and legacy variant byte-retention controls |
| R3-7: repeated cleanup cancellation | The entire runtime cleanup block suppresses repeated SIGTERM/SIGINT before witness retention or reconciliation begins. | Real repeated SIGTERM during cleanup retains final failed qualification and successful cleanup |
| R3-8: invalidating image pull | Mutable-image `--pull=always` is rejected before inspection/creation can be invalidated. | Mutable always-pull control |
| R3-9: XML attributes | Closed result values and bounded numeric measurements remain readable; identity attributes become commitments and custom diagnostic attributes are discarded. | Synthetic-secret parameter/custom-attribute control with valid-result adjudication |
| R3-10: command payload flags | Docker global selectors are parsed only before the command; shell payload `-c` is accepted. | Installed create/exec shell-payload controls |
| R3-11: pre-create private custody | Broker/admission owners register private-directory identity during initialization; interrupted children without registration remain explicitly unresolved. | Actual SIGTERM before the first Docker create reconciles the registered private directory |
| R3-12: selected binary rejection | The materialization path has a focused internal source/version seam and rejects modified bytes, a self-resealed unapproved hash and a mismatched receipt version before copying the rejected executable. | New `QualificationSelectedRuntimeTests`: 3/3 PASS, zero skipped/not-run |
| R3-13: candidate source binding | Only candidate input hashes were refreshed, adding the changed Redis fixture and new materialization-test source; original/protected subjects remain intact. | All 75 candidate inputs and original-input preservation audits PASS |

Implementation-side focused Python selection passed **34/34** controls. The two
affected projects built successfully with zero warnings/errors using:

```bash
dotnet build tests/Hexalith.EventStore.PublishedCompatibility.Tests/Hexalith.EventStore.PublishedCompatibility.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --no-restore
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --no-restore
dotnet tests/Hexalith.EventStore.PublishedCompatibility.Tests/bin/Debug/net10.0/Hexalith.EventStore.PublishedCompatibility.Tests.dll -class '*QualificationSelectedRuntimeTests'
```

The last command passed 3/3 tests with zero skips. Broader verification was left to
the parent, which independently reported these final results:

| Command / check | Final result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` | 103/103 PASS in 20.393 seconds |
| `python3 -B -m unittest discover -s tools/tests -p test_qualify_internal_actor_admission.py` | 11/11 PASS |
| `python3 -B -m unittest discover -s tools/tests -p test_route_catalog_ci_tools.py` | 6/6 PASS |
| Built `QualificationSelectedRuntimeTests` command above | 3/3 PASS, zero skips |
| `actionlint .github/workflows/dapr-broker-qualification.yml .github/workflows/dapr-runtime-qualification.yml`; `git diff --check` | PASS |
| Frozen intent, original lock/`ci.yml`/`integration.yml`/`docs/ci.md`, all 19 historical hashes and existing gitlinks | PASS, preserved |
| All 75 candidate inputs, including the changed fixture and new C# test source | PASS; candidate lock SHA-256 `997af6c192dc74fdcb1d7ed95fc79fb87844e8587558711f241799e0fc7429ea` |

The parent ran a fresh producer with the isolated verified tools, no exclusive
allocation and output `/tmp/story-3-21-parent-final-blocked-20261108`. It exited 1;
all 24 vectors retain the missing-exclusive-allocation infrastructure blocker.
Independent validator exits are 0 with `--allow-failures` (integrity PASS,
technical qualification FAIL), 1 without flags and 1 with
`--require-production-authority`. Only Docker context/info reads were executed;
cleanup completed, no owned resources remain and private material was removed.

No exclusive worker has been verified; the owner does not know whether one is
available. Selected live qualification and CI remain incomplete, with no new CI
run link or live guarantee. The unchanged Builds/Platform, Kubernetes/restore,
independent sharding/PF-02/Azure and Story 4.16 handoffs remain pending. Production
eligibility remains FAIL, selected broker remains null and Story 3.21 remains
`in-progress`. No source/workflow/hash change followed the final verification.

### Parent matrix audit — implementation pass 3 before R3 patches (historical)

All matrix rows have executed passing controls: official identity/architecture/chart corruptions and approved positive controls; original schema/profile/import compatibility; endpoint/allocation/daemon/socket refusal and installed-adapter subprocess controls; startup, actual SIGTERM, inherited/escaped descendant, independent Docker/private cleanup and complete inventory preservation controls; and empty/skipped/failed/partial-report plus all-24-vector blocker controls. The parent reran 93/93 packet/runtime, 11/11 admission, 6/6 catalog tests and both-workflow lint/whitespace checks successfully. All 13 C# change files exactly match the pass-2 sources already built in four Debug/source projects and tested at 23/23 with zero skips. These deterministic controls and truthful refused captures do not establish live candidate qualification.

### Final parent disposition after R3 patches

The final parent execution passed 103 packet/runtime, 11 admission, 6 catalog and 3 C# materialization tests; the unchanged AppHost/profile code retains the earlier independent 23/23 result (146 focused tests across the final verified sources). PublishedCompatibility and LiveSidecar Debug/source builds were refreshed successfully after the C# patches; the earlier AppHost and IntegrationTests builds remain unchanged evidence. Both workflow lint and final whitespace/preservation/input-hash checks pass. Every third-review finding has a recorded local resolution; no findings were deferred. The final code diff is retained locally at `/tmp/story-3-21-build-final.diff`.

The older parent audit immediately above predates the R3 patches; its 93-test and 13-file equality statements are historical and are superseded by the final R3 verification table and this disposition. No live matrix was executed on accepted infrastructure. The spec and parent sprint row stay `in-progress`; the generic workflow done/review transition and commit are not applied because the approved story still requires live qualification and forbids Git mutations. No commit, push, publication, production action or CI dispatch was performed.

### Current-HEAD verification and allocation inspection — 2026-11-08

At HEAD `07f5ef132deb17d45d7209357f8ac89ef39bc557`, the existing implementation
already contains the specified R3 corrections. No implementation, workflow or lock
change was needed. This append preserves the frozen intent and earlier evidence.
Exact commands, exit codes, fresh logs and blocked packets are retained externally
under `/tmp/story-3-21-current-head-verification-ncjqjuwq/` (`verification.json`
and `commands.json`). CI was not dispatched; no new CI run link exists.

| Command / check | Current result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` | 103/103 PASS in 20.576 seconds |
| `python3 -B -m unittest discover -s tools/tests -p test_qualify_internal_actor_admission.py` | 11/11 PASS |
| `python3 -B -m unittest discover -s tools/tests -p test_route_catalog_ci_tools.py` | 6/6 PASS |
| `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --no-restore` | PASS, zero warnings/errors |
| `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*DevelopmentRouteCatalogRuntimeProfileTests' -class '*DevelopmentRouteCatalogAspireTests'` | 23/23 PASS, zero skipped/not-run, 80.898 seconds |
| `dotnet tests/Hexalith.EventStore.PublishedCompatibility.Tests/bin/Debug/net10.0/Hexalith.EventStore.PublishedCompatibility.Tests.dll -class '*QualificationSelectedRuntimeTests'` | 3/3 PASS, zero skipped/not-run |
| `actionlint .github/workflows/dapr-broker-qualification.yml .github/workflows/dapr-runtime-qualification.yml`; `git diff --check` | PASS |
| Original/candidate input audit; protected-subject comparison with frozen `f14d56ea917dd33a552742226d6d7ee65f5e1236` | All 19 original and 75 candidate hashes match; original lock, `ci.yml`, `integration.yml` and `docs/ci.md` unchanged |

Read-only inspection found `QUALIFICATION_EXCLUSIVE_WORKER` unset,
`/tmp/qualification-worker.json` absent, no configured qualification/Docker selector
variables and no GitHub-hosted runner environment. Docker context `default`
resolves to canonical local socket `unix:///run/docker.sock`, daemon
`afcd365d-1578-410b-af0b-98550207ef1c`. These observations do not establish exclusive
worker ownership; no allocation document was created and no shared resource was
mutated. The isolated runtime and CLI executable hashes match the candidate lock.

With `QUALIFICATION_EXCLUSIVE_WORKER` unset,
`QUALIFICATION_DAPRD=/tmp/eventstore-dapr1191-tools/runtime/daprd` and
`PATH="/tmp/eventstore-dapr1191-tools/cli:$PATH"`, fresh commands were:

```bash
python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-current-head-verification-ncjqjuwq/broker
python3 -B tools/qualify-dapr-runtime.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --output /tmp/story-3-21-current-head-verification-ncjqjuwq/runtime --assembly tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll --class '*RouteCatalogHostedLiveTests'
python3 -B tools/qualify-internal-actor-admission.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --evidence /tmp/story-3-21-current-head-verification-ncjqjuwq/admission
```

All three exit 1 for `exclusive disposable-worker allocation is required before
mutation`. The producer retains all 24 blocked vectors; runtime/admission execute
zero selected checks. Independent broker validation exits 0 with
`--allow-failures` (integrity PASS, technical qualification FAIL), 1 without flags
and 1 with `--require-production-authority`. Broker/runtime Docker receipts contain
only context inspection and pinned daemon-ID reads. All three cleanup results pass;
no owned resources remain and private material is removed.

Accepted exclusive-worker execution and CI remain the acceptance gap. Existing
Builds/Platform, Kubernetes rehearsal/restore, independent sharding/PF-02/Azure and
Story 4.16 handoffs remain pending. Candidate qualification stays incomplete,
production eligibility stays FAIL, selected broker stays null and Story 3.21 stays
`in-progress`. Only this verification record changed; no Git mutation, publication,
production/shared-AppHost start, dependency/catalog/submodule update or shared
Docker resource mutation was performed.
