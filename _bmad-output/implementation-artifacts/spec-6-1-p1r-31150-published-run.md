---
title: '6.1-P1R published EventStore 3.115.0 qualification run'
type: 'feature'
created: '2026-10-07'
status: 'ready-for-dev'
approved: '2026-10-07'
approval_decision: 'approve-and-stop'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '98da5a04e6df33ba026cbaae46d1777acdca7a21'
tracking_scope: 'qualification follow-up; no Projects or sprint-status changes'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="human-owned qualification scope; execution selections accepted on 2026-10-07">

## Intent

**Problem:** The completed harness has no real published-package or operational qualification evidence. Source remediation and packet validity cannot establish P1R compatibility.

**Approach:** Execute the canonical seventeen scenarios/seven families and selected additions against immutable packages; import measured receipts into a fresh validated packet. Use the accepted execution selections below; keep acceptance decisions and conformance pending.

## Boundaries & Constraints

**Always:** Separate execution from compatibility and owner acceptance. Use fresh isolated Release/package consumers, `CI=true`, isolated `NUGET_PACKAGES`, exact assets/lock graphs, actual signatures and physically loaded DLL hashes. Keep canonical version directions and required operations explicit. Bind the actual executor checkout separately from package repository commits and candidate Builds provenance. Retain every failed, unavailable, skipped, incompatible or unmeasured portion as nonpassing. Use one writer, real backup/fresh-database restoration, persisted inventories and repeated owned cleanup. Preserve Projects AD-17 mutation freeze/forward recovery and `p1r_usable=false`.

**Never:** Invent decisions/counts, rewrite sealed evidence, republish/rebuild existing versions, modify Projects/Builds/pins/acceptance/readiness/sprint tracking, initialize nested submodules, commit/push/publish, or substitute self-roundtrips for missing cross-version execution. Runtime application state access remains Dapr/actor-owned; provider backup/provisioning and bounded fixture diagnostics have separate purposes.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Retained database | Fresh restore of floor 5/head 12/snapshot 9 reconstructs 12; append 13, stop writer, restart and replay reconstruct 13; preserve prior hashes, snapshot and second tenant. |
| Invalid evidence or incapable historical operation | Measure refusal/effect and unchanged domain inventories; unsupported/incompatible results cannot pass qualification. |

## Settled Execution Selections

The user accepted all six recommendations on 2026-10-07. These authorize execution inputs and Test-owner instrumentation, not the four owners' eventual qualification acceptance.

1. Candidate: nuget.org 3.115.0, tag `v3.115.0`, commit `283b07a52c9c70e1c940164a7011ee8c3ad98b2d`; Client/Contracts/DomainService/Server/ServiceDefaults with actual archive/content hashes and repository commits in the run's `planning-observations.json`.
2. Builds: untagged `4.29.1-22-gba4ca78`, commit `ba4ca78c3868a4757cb92d912a54c8a237871b54`. Keep workspace Builds 4.30.0 unchanged and separate.
3. Rollback: `null`; retain approved Projects AD-17 mutation freeze/forward recovery.
4. Profile: invocation-owned Redis at the observed registry digest indexed in the run README, Dapr runtime 1.18.4; preserve shared containers. PostgreSQL/Dapr 1.18.2 receives no inherited evidence.
5. Test selections: both `reminder-recovery` and `logical-event-evolution`; accepted mechanism `p1r-executed-checks-v1` records every executed Boolean check by stable ID, including failures, and derives counters from those records.
6. Comparisons: narrowly bind actual published 3.70.1 and 3.110.0 inputs/evidence for canonical directions. These are comparison packages only; rollback remains null and their actual incompatibilities remain nonpassing.

</frozen-after-approval>

## Code Map

- `tools/p1r_published_qualification.py` — strict input, package and lane binding; currently only candidate/rollback archives can bind lanes.
- `tools/p1r_qualification.py` — source closure; currently refuses an absent nested Builds checkout despite available workspace-root Builds.
- `evidence/6-1-p1r-3110/verification/{run_verification.py,host,domain,probe}` under implementation artifacts — read-only patterns; historical coordinates, PostgreSQL topology and literal counters cannot be reused unchanged.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/{Events,Integration,Fixtures}` — persistence, Reminder, evolution and ownership patterns; source tests and logical fixture restore do not qualify published recovery.

## Tasks & Acceptance

**Execution:**

- [ ] `tools/p1r_qualification.py` and `tools/tests/test_p1r_qualification.py` — support strictly verified workspace-root-declared Builds observation without nested initialization; retain actual source/Builds identity and refuse substitutions.
- [ ] `tools/p1r_published_qualification.py`, `tools/p1r-published-qualification.py`, `tools/tests/test_p1r_published_qualification.py` — implement selected comparison policy; independently verify comparison archives/graphs/binaries without making them rollback or granting acceptance.
- [ ] `tools/p1r_published_executor.py`, `tools/p1r-published-executor.py`, `tools/p1r-published-consumers/` — create isolated Host/Domain/Probe consumers and orchestrator; execute canonical case inventory and selected additions with measured receipts and persisted state checks.
- [ ] `tools/tests/test_p1r_published_executor.py` — test false counts, missing direction, substitutions, interruption and continued cleanup; tooling fixtures confer no operational pass.
- [ ] `evidence/6-1-p1r-31150-published-run/` under implementation artifacts — write owner inputs only after selections; retain package and operational receipts; prepare/validate new packet with `--inputs`, evidence directories and receipts; index outstanding owner decisions in README.

**Acceptance Criteria:**

- Given selected inputs, when consumers execute, then every required lane has its real disposition; omissions remain nonpassing.
- Given restore/append/restart, when inventories are queried, then they meet `tools/p1r_qualification_runtime.py`'s exact contract.
- Given startup/failure/timeout/cancellation drills, when repeated cleanup completes, then owned resources are absent and shared resources remain unchanged.
- Given incomplete owner decisions, when independent validation succeeds, then technical qualification is recomputed true or false and decisions/usability remain false.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Focused Python suites; actual Release consumer and distinct Debug/source comparison runs; restore/cleanup drills; fresh published-harness prepare/validate; `git diff --check`.
- Retained preflight: five archives restored/signature-verified. Aspire startup exits 2 for missing nested Tenants; preparation/validate exit 2 for absent nested Builds. Initialize neither.
