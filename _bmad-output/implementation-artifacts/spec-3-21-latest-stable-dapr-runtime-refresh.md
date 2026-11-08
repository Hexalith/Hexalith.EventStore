---
title: 'Story 3.21 Dapr 1.19.1 runtime refresh and qualification'
type: 'chore'
created: '2026-11-08'
status: 'ready-for-dev'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
baseline_commit: 'f14d56ea917dd33a552742226d6d7ee65f5e1236'
review_loop_iteration: 0
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
- [ ] `deploy/dapr/qualification/runtime-lock-1.19.1.json`, versioned `public-identities/` — verify official source commits, release/archive/binary checksums, chart version/appVersion and amd64/arm64 OCI child digests; bind current sources.
- [ ] `tools/{dapr_broker_qualification,qualify-dapr-brokers,validate-dapr-broker-qualification}.py` — support explicit profiles/approved tuples, exclusive-worker preflight, pinned endpoint/daemon and ID-based cleanup.
- [ ] `tools/tests/test_dapr_broker_qualification.py`, new `test_dapr_runtime_qualification.py` — test identity/chart/architecture corruption, legacy packets, endpoint conflicts, missing exclusivity, daemon changes and startup/cancellation/sentinel failures beside positive controls. Retain catalog CI tests.
- [ ] `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogRuntimeProfile.cs`, transport/options and profile tests — add matched selection; retain constructors/defaults/constants.
- [ ] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopment{Sidecar,Sentry}.cs`, `Integration/RouteCatalogHostedLiveTests.Transport*.cs`, `Fixtures/DaprTestContainerFixture.cs`, IntegrationTests' `Fixtures/DurableBrokerQualificationFixture.cs`, PublishedCompatibility.Tests' `QualificationRuntime.cs`, and `tools/qualify-internal-actor-admission.py` — bind explicitly selected binaries/control images; retain original invocations.
- [ ] `.github/workflows/dapr-broker-qualification.yml`, new `dapr-runtime-qualification.yml` — use exclusive disposable jobs, isolated tools and suppressed bytecode; run the affected classes below with separate outcomes.
- [ ] `deploy/dapr/qualification/README.md`, this spec — document selection/outcomes and owners: Builds catalogs/tools, Platform rehearsal, independent sharding/PF-02/Azure subjects and pending 4.16 proof.

**Acceptance Criteria:**
- Given original consumers, when candidate support is installed, then locks/defaults/sealed subjects remain unchanged and compatibility passes.
- Given accepted infrastructure, when qualification runs, then all broker and affected routing/token/ACL, actor/reminder, scheduler/placement, snapshot/restart/restore and compatibility checks execute on observed new identities.
- Given a failed/skipped/blocked selected check, when results are recorded, then refresh qualification remains incomplete; production eligibility stays FAIL and selected broker null.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

No intent gaps or irreversible actions. Changes span tooling, fixtures, workflows and an optional public profile.

## Verification

- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'`; original catalog tests; changed-workflow `actionlint`; `git diff --check`.
- Build individual .NET projects in Debug/source mode locally, Release/packages in CI. Run built xUnit assemblies using `-class`; brokers require `-explicit only`. Record blockers.
- Reuse broker qualification; Server live route catalog/native actor, actor concurrency/tenant isolation, reminder recovery, snapshot reload; and PublishedCompatibility security/restore classes. Bind observed candidate identities; require zero selected live skips.
- Run candidate producer and independent validator separately with `--allow-failures`, normally, and `--require-production-authority`.

Baseline: `python3 -B -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py`: 25/25 passed at the baseline SHA. Docker Desktop is shared; no accepted worker. No resources created or CI run. Candidate unqualified.
