---
title: 'Story 3.21 local broker failure diagnosis and Docker preservation'
type: 'test'
created: '2026-10-20'
status: 'draft-awaiting-owner-approval'
parent_story: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
slice: 'ES-DIAG'
implementation_authorized: false
production_authorized: false
selected_broker: null
baseline_commit: 'f6b545cd02632f662136d0b4b8e6952baa7afec3'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-20.md'
---

# Story 3.21 follow-up: local evidence and preservation

This is a separately bounded follow-up slice, proposed by the
[course correction](../planning-artifacts/sprint-change-proposal-2026-10-20.md).
It does not supersede the parent specification or amend its frozen intent
before named-owner approval of the exact text. Approval of this specification
must be recorded separately from Story 9.1 authorization, AD-26 ratification,
broker selection and production authority. The inspected baseline is dirty;
its commit alone never identifies the uncommitted qualification inputs.

<frozen-after-approval reason="human-owned intent; draft until exact owner approval">

## Intent

**Problem:** The retained local Story 3.21 comparison has four rejected or
unresolved broker checks. Its latest capture also fails preservation because
two unrelated transient containers disappeared. EventStore must provide honest,
reproducible component evidence to the Platform-owned shared-host rehearsal.

**Approach:** Preserve the existing evidence, run new experiments only on an
exclusively allocated disposable local Docker worker, diagnose all four checks
and capture the complete 24-vector comparison. Verify capture integrity
independently of technical qualification. Deliver bounded failure/remedy
handoffs to EventStore Story 3.17, Platform and Builds, without choosing a broker
or claiming the parent story done.

## Boundaries and constraints

**Always:** Read the current parent specification and proposal, compare their
input hashes, and validate the Story 9.1 current-subject corrective authorization
with its exact scope before gate-closing implementation. Preserve user changes,
all historical qualification packets and existing authority records. Use the
retained exact candidate lock: Dapr `1.18.7`, CLI `1.18.2`, embedded
components-contrib `1.18.6`, RabbitMQ `4.3.6-management`, Kafka `4.3.1`, plus
the existing immutable binaries/images/components. Retain all 12 scenarios per
broker and their independent Dapr persisted application readbacks. Label broker
administration as fault injection or diagnostics. Keep secrets ephemeral,
redacted and outside retained artifacts; application access remains Dapr-only.
Keep verifier outputs and generated interpreter caches outside closed packet
trees, including when invoking a retained verifier; use bytecode suppression
or an external cache location for new executions. Never remove a pre-existing
unlisted file from an old packet or ignore it to make integrity pass.

Use one explicit local Unix-socket Docker endpoint for the entire run. Bind its
effective endpoint and daemon identity, including command environment/context,
before the first mutation. A local socket may point to a shared engine; local
transport alone proves no exclusivity. Require an exclusively allocated
disposable worker/VM and run ownership, or fail before creating resources.
Preserve every pre-existing unrelated container/specification in the existing
before/after comparison, even on that isolated worker. Verify cleanup only for
owned IDs and run labels, including cancellation and partial startup.

**Never:** Touch the production Kubernetes context/API, shared Dapr resources,
production Secrets, workloads, PVCs, traffic or consumers. Start or modify
Platform composition, add a second shared AppHost, change Builds pins/tooling,
update dependencies or gitlinks, publish, perform Git mutations or send external
messages. Do not edit sealed `ci.yml`, `integration.yml`, `docs/ci.md`, OQ8
verifiers/authority, Story 2.13 packets or the parent frozen block in this slice.
Do not allow remote Docker endpoints, filter preservation failures, claim a
whole-workspace freeze, relax scenario predicates, use `--allow-failures` as a
qualification pass, select a broker or make a production-readiness claim.

## I/O and failure matrix

| Input / scenario | Required bounded observation | Disposition |
| --- | --- | --- |
| Wrong/remote/changed daemon, unavailable exclusive worker | Reject before mutation; no cleanup command on an unaccepted endpoint | Environment blocked, no capture pass |
| Unrelated sentinel unchanged | Complete baseline and command-bound specification hashes match after owned cleanup | Preservation PASS |
| Unrelated container removed or specification changed | Independent verifier rejects, including when producer claims preservation passed | Preservation FAIL; packet retained |
| Extra cache/log/output in a closed packet | Exact artifact-inventory check rejects it | Integrity FAIL; preserve old packet, prevent pollution in a fresh run |
| RabbitMQ rotation | Publish/overlap/acknowledge/controlled restart/revoke receipts; old-session and cold-old-generation denial, new-generation one-effect success | Timeout `599` remains unknown; no false denial |
| Kafka rotation | Actual authenticated principal and ACL/session diagnostics; same old/new controls | Old-generation `204` remains rejected/unresolved |
| RabbitMQ pressure | Executed queue overflow/negative-confirm fault, every HTTP/ID receipt and drain/restart readback | Any acknowledged ID unreconciled within the declared bound fails; bound never implies universal loss proof |
| Caller cancellation | Bounded caller termination, subsequent same-ID Dapr reconciliation | Neither timeout nor cancellation establishes no effect or permits a fresh effect |
| RabbitMQ absent binding | Independently demonstrated absent binding beside successful bound control | Unbound `204` fails existing guarantee; no queue-acceptance inference from confirms |
| Runtime cannot supply guarantee | Pinned-source/native reproduction plus owner/upstream remedy proposal | Candidate remains FAIL; no provider fallback or automatic version move |
| All observations captured, candidate still rejected | Complete integrity-valid packet and honest nonzero technical/authority results | ES-DIAG may finish; parent 3.21 stays in progress |

## Acceptance criteria

1. **AC1 — Preservation:** A new full capture on the accepted exclusive local
   worker passes the unchanged unrelated-container preservation denominator and
   owned-resource cleanup. Endpoint/daemon changes, remote targets, partial
   startup, cancellation and sentinel deletion/specification mutations have
   meaningful positive/negative controls; rejected endpoint preflight performs
   no mutation. A lock alone on the shared development daemon is insufficient.
2. **AC2 — Rotation:** Both brokers retain old-session and cold-old-generation
   controls after revocation, correlated with Dapr/component and broker identity
   diagnostics, alongside the fresh-generation success/readback. Startup-only
   credentials use controlled reinitialization; acknowledge all intended
   consumers before revoke. Denial PASS requires explicit attributable rejection
   and persisted readback. A timeout or unrelated server error is not denial.
3. **AC3 — RabbitMQ publication:** Both backpressure/negative-confirm and
   missing-binding checks retain an allowed control, native fault/binding
   diagnostics, exact publication receipts and bounded per-ID Dapr readback.
   Distinguish pending, observed loss within the bound and unknown outcomes.
   Preserve current guarantee predicates. A runtime defect is a truthful rejected
   result with a reproduction and proposed remedy, not a reason to weaken them.
4. **AC4 — Complete independent evidence:** All 24 required observations execute
   and bind retained source/component/subscription, runtime/CLI/control-plane,
   OCI, command, loaded-metadata and state identities. Missing, contradictory,
   blocked or unexecuted vectors fail the capture milestone. An integrity-valid
   rejected vector may satisfy diagnostic capture but never candidate
   qualification. Capture, technical qualification and production-authority
   outputs and exits remain distinguishable.
   Unlisted bytecode/log files remain integrity failures; suppress generated
   cache/output in new packets instead of weakening the exact inventory.
5. **AC5 — Handoff and lifecycle:** New dated evidence links each original failed
   vector to its new observation, root-cause finding or explicit uncertainty,
   owner and next action. Proposed Story 3.17 rows bind profile, operation,
   guarantee, exact versions and evidence/disposition; they are not accepted
   inventory. Platform receives composition/rehearsal requirements; Builds
   receives any catalog/CLI/runner or runtime-remedy proposal. Parent 3.21 remains
   in progress, selected broker stays null and production eligibility stays
   FAIL. Only this diagnostic slice may be completed after its own review and
   gates; no story-done label supplies high-risk authority.

</frozen-after-approval>

## Code map and allowed implementation scope

- `tools/qualify-dapr-brokers.py`: preflight, exact owned lifecycle, rotation and
  failure probes, sanitized command/native evidence.
- `tools/dapr_broker_probe.py`, `tools/dapr_qualification_tcp_proxy.py`: only
  required evidence/reconciliation corrections; no application provider client.
- `tools/dapr_broker_qualification.py`, `tools/validate-dapr-broker-qualification.py`,
  `tools/tests/test_dapr_broker_qualification.py`: independent checks and meaningful
  adverse fixtures; preserve fail-closed existing predicates.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs`
  and `ContractTests/DurableBrokerQualificationTests.cs`: non-exported repository
  capture fixture only, without becoming Platform orchestration.
- `deploy/dapr/qualification/`: candidate-only diagnostics/documentation; any
  changed input byte receives a new retained lock/source snapshot. Keep the
  runtime/broker candidate identities unchanged. Never refresh old packet locks.
- `.github/workflows/dapr-broker-qualification.yml`: only if needed to provide
  exclusive disposable execution; it remains manual and non-promoting, with
  sanitized evidence and truthful failed results. No sealed workflow edits.
- New evidence under a fresh Story 3.21 run directory; no replacement of
  `qualification-2026-10-20`, `implementation-2026-10-20` or
  `verification-2026-10-20`, including rejected and historical subruns.

The authorization record must enumerate the actual allowed paths and mutation
classes. This list is a proposed scope, not an authority record. Broader runner
capabilities belong to Builds; Platform descriptor integration and Kubernetes
or Helm work are later slices in their owning repository.

## Tasks

- [ ] Verify approved intent, actual input identities and valid Story 9.1
  authorization. Stop with the exact missing/invalid record or validator result
  if gate-closing work is unauthorized; never author owner approval yourself.
- [ ] Establish endpoint-bound exclusive local execution and preserve the full
  unrelated-resource gate; add only meaningful lifecycle/preservation controls.
- [ ] Diagnose/reproduce both broker rotation checks and both RabbitMQ
  publication checks, retaining explicit failure/unknown dispositions as needed.
- [ ] Run the existing verifier/mutation suite and the explicit real C# capture
  against all 24 scenarios on the exclusive worker. Capture original outputs
  before any remedy; use a new directory for every changed source/lock.
- [ ] Retain exact source, command, time, exit, state and preservation receipts;
  deliver proposed inventory/remedy/Platform handoffs and the parent-status
  statement. Review this slice under its own acceptance, not as parent closure.

## Verification plan

Use the narrow existing Python verifier suite first:

```bash
python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py
```

Build the integration project under the repository's dependency-mode policy and
run its built xUnit assembly with
`-class "*DurableBrokerQualificationTests" -explicit only`, using the newly
accepted exclusive worker. Bind the exact command and configuration; do not
present the retained package-mode Debug build as source-mode success. Existing
source-mode CS1704 remains a separate blocker unless resolved in independently
authorized dependency work. Follow the repository fallback validation ladder.

For each new packet, retain separate exact invocations/results of:

```bash
python3 tools/validate-dapr-broker-qualification.py <fresh-packet> --allow-failures
python3 tools/validate-dapr-broker-qualification.py <fresh-packet>
python3 tools/validate-dapr-broker-qualification.py <fresh-packet> --require-production-authority
```

The first validates evidence integrity while reporting rejected scenarios; it
must fail preservation/contradictions. The second must fail any rejected
guarantee. The third must report production FAIL. Never mark all three as a
single green gate. Retain workflow lint if that workflow changes and narrow
whitespace/preservation checks. No production or new Kubernetes execution is
needed to complete this diagnostic slice.

## Parent and cross-repository handoff

This slice unblocks diagnosis and preparation only. Builds catalog/tool
alignment, EventStore 5.7/7.6 contracts, Platform-owned disposable Kubernetes
rehearsal, owner AD-26 selection, Story 4.16/7.22 proof and Story 3.19 issue
gates remain ordered in the proposal. The future Platform composition uses
owner declarations and runner descriptors, not this local fixture's lifecycle.
An unavailable guarantee returns to the owning runtime/component or
Architecture; no selection or deployment follows automatically.

## Approval and execution record

Proposal approval: pending. Exact frozen intent approval: pending.
Story 9.1 authorization: not supplied or validated by this planning task.
Implementation: not started. Parent status: in progress.

The prior report's 25 verifier tests, build/lint passes and failed full capture
are historical observations, not verification of this draft or new runtime
execution. The planning validation record is linked from the proposal.
The course-correction recheck currently fails earlier on a pre-existing
unlisted `inputs/tools/__pycache__/dapr_broker_qualification.cpython-314.pyc`
in the retained latest packet. Preserve it and this distinct failure; the
planning task neither removed it nor regenerated that packet's inventory.
