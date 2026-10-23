---
title: 'Story 3.21 Production Dapr Runtime and Durable Broker Qualification'
type: 'chore'
created: '2026-10-20'
status: 'in-progress'
baseline_commit: 'f6b545cd02632f662136d0b4b8e6952baa7afec3'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Kubernetes at `192.168.1.30` runs Dapr `1.18.1`, below AD-26's floor. Existing Redis evidence and broker templates do not establish a durable production profile.

**Approach:** Inventory the target read-only and retain immutable Dapr `1.18.7`/CLI `1.18.2`, RabbitMQ `4.3.6-management` and Kafka `4.3.1` comparison evidence. EventStore owns local Dapr/component qualification and the guarantee/failure handoff. Hexalith.Platform owns the shared composition, disposable self-hosted Kubernetes rehearsal and later exact deployment/restore bundle; Hexalith.Builds owns catalog, CLI and runner pins. EventStore's own AppHost and broker fixture serve repository tests only. Only authenticated owner AD-26 ratification selects a runtime and broker; technical observations, Platform startup and passing capture tests convey no deployment authority.

## Boundaries & Constraints

**Always:** Target self-hosted Kubernetes. Preserve existing workspace changes and unrelated workloads. Use Dapr for application infrastructure access; credentials resolve through `secretKeyRef` and AD-24 OpenBao, with verified TLS and default-deny grants. Bind runtime, CLI, embedded components, placement, scheduler, broker, loaded component/subscription bytes, sources and binaries. Reject production runtime versions below `1.18.3` and mutable image references. Keep candidate results distinct from owner ratification and production-path proof. Runtime minor changes require broker requalification and Story 4.16 envelope re-proof. Preserve the full 24-vector comparison and its existing pass conditions. Record packet integrity, observed scenario outcomes, candidate qualification, Platform composition/rehearsal and production authority as distinct results. Use an exclusively allocated local Docker daemon for new captures; every pre-existing unrelated container identity/specification remains in the preservation denominator. Platform composition tests consume its runner-owned descriptor and never start an EventStore-owned shared host. All cross-repository evidence binds exact revisions, tool/catalog and artifact identities, loaded configuration and accepted owner handoffs.

**Never:** Modify production cluster resources without separate explicit approval of an exact change bundle. Read production Secrets, enroll credentials, reuse isolated workloads as production, migrate state/consumers, or select the broker automatically. Azure/AWS are outside MVP. Do not change sealed `ci.yml`/`integration.yml`, shared Builds pins, Story 2.13 packets, or existing authority bytes. Do not claim Story 2.13 notification distribution, NFR5, G-APPEND, restore posture or production readiness from broker tests. No Git mutation or dependency refresh. Do not overwrite failed or historical packets, exclude disappearing containers from preservation, treat cancellation or timeout as proof of no effect, convert a rejected guarantee into PASS, or derive broker choice from pass counts. Do not fork Platform composition, edit Builds defaults from EventStore, or substitute the development runtime/catalog for the qualification lock. Preserve Story 2.13, sealed workflows and existing authority records.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Identity | Below-floor runtime, mutable image, wrong loaded component | Qualification guard FAIL | Identify exact field |
| Delivery | Subscriber crashes after receipt, before acknowledgement | Same CloudEvent ID redelivers; persisted end-state reconciles | No success from publish alone |
| Poison | Retry exhausts; quarantine unavailable | No source acknowledgement without durable capture | FAIL on loss or unknown capture |
| Isolation | Forbidden app/topic, absent secret or invalid TLS | Denied beside an allowed control | No direct-provider fallback |
| Authority | Missing AD-26 or publication record | Production eligibility FAIL | Retain technical results separately |

</frozen-after-approval>

## Code Map

- `../../tools/qualify-dapr-brokers.py`, `dapr_broker_probe.py`, and `dapr_qualification_tcp_proxy.py`: owned disposable hosts, Dapr-backed application effects, crash diagnostics and stable TLS transport across Docker Desktop restarts.
- `../../tools/dapr_broker_qualification.py` and `validate-dapr-broker-qualification.py`: retained-byte, loaded-identity, command/state cross-checks and non-authority guards.
- `../../deploy/dapr/qualification/`: immutable candidate lock, scoped components, exact subscription catalog and reproduction guide.
- `evidence/story-3-21/qualification-2026-10-20/` below this spec's directory: retained inspection, immutable candidate identities and rollout plan; not executed broker proof.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/SignalRDaprDistribution{Fixture,Node}.cs`: existing two-host patterns are Redis-specific and hardcode local runtime/control-plane endpoints; preserve their behavior and add a separate qualification fixture.
- `deploy/dapr/pubsub-{kafka,rabbitmq}.yaml`: non-authorizing templates with unsuitable environment interpolation; author separate candidate files.
- `architecture.md` AD-5/8/24/26/31/34/36 and Story 3.21 in `planning-artifacts/epics.md`: controlling gates. Stories 3.17/3.19/5.7 own inventory/profile/component handoffs.

## Tasks & Acceptance

**Execution:**

**2026-10-23 evidence policy:** do not write under `evidence/`; record commands, pass/fail counts, commit SHA and CI run link in this story (architecture AD-12).

- [x] `tools/qualify-dapr-brokers.py` — orchestrate only owned disposable infrastructure, capture exact inputs/commands/times/exit codes and cleanup; reject production contexts/endpoints and missing requested scenarios.
- [x] `deploy/dapr/qualification/{runtime-lock.json,rabbitmq.yaml,kafka.yaml,subscriptions.yaml}` — bind retained immutable candidates, OpenBao references, explicit scopes, verified TLS, durable queues/topics and pre-enrolled exact topic catalogs. These are candidate inputs, never `production-profile.yaml`.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs` and `ContractTests/DurableBrokerQualificationTests.cs` — exercise each candidate through real Dapr hosts: crash/redelivery, broker restart, durable quarantine and failed diversion, retention/expiry, stable IDs, scopes, TLS/secrets/rotation, cancellation/backpressure and missing bindings. Require persisted application readback and broker diagnostics as separate evidence.
- [x] `tools/validate-dapr-broker-qualification.py` and `tools/tests/test_dapr_broker_qualification.py` — validate identities, retained hashes, complete scenario outcomes and non-authority; mutation-prove below-floor/mutable pins, missing/false evidence and missing owner binding.
- [x] `.github/workflows/dapr-broker-qualification.yml` — add an independent, manually invoked non-promoting qualification lane, with immutable tool/image inputs and retained sanitized receipts.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-3-21/` — Retain the existing packets unchanged and create fresh diagnostic runs for all 24 broker vectors. Close the capture/preservation task only when the independent verifier accepts the complete executed capture and unrelated-resource preservation; explicitly retain any rejected guarantee. Deliver proposed Story 3.17 rows, Story 5.9 drift and content-bound inputs to Platform's existing hosted qualification work. Platform owns the later exact production deployment/restore bundle. Accept Platform rehearsal receipts into Story 3.21 only when they bind the same qualified immutable runtime/component/broker inputs and the actual composed workload identity. After valid AD-26 records explicitly select a qualified candidate, retain accepted selected-component and runtime/profile-input handoff receipts for Stories 5.7/3.19.

**Acceptance Criteria:**

- Given both self-hosted candidates, when the isolated lane completes, then every required guarantee has an observed bounded result or explicit FAIL; cloud behavior is outside scope.
- Given a retained packet, when independently verified, then source/component/binary/OCI identities and scenario evidence reproduce without touching production or changing sealed workflows.
- Given read-only inspection and a proposed rollout, when compared with retained baselines, then unrelated workspace bytes and workload specifications remain unchanged; production changes still require exact-bundle approval.
- Given missing owner ratification, when eligibility is checked, then it reports truthful FAIL and no selected broker, promotion or story-done result.

- A complete, integrity-valid failed experiment may complete the separate ES-DIAG diagnostic slice. It never marks candidate qualification or Story 3.21 done. Missing observations, preservation failure or contradictory receipts fail even that capture milestone. Both rotation checks and RabbitMQ backpressure/missing bindings meet the closure controls in the approved course correction or remain explicit FAIL/unresolved with reproducible owner handoffs; their original results remain retained.
- Platform's rehearsal binds server/node versions, chart/rendered manifests, admission and sidecar injection, mTLS/trust domains, DNS, native TLS, OpenBao generations and grants, topic catalog, durable storage restart, scheduler/placement and isolated restore. Applicable Dapr delivery/security/fault vectors run against the actual shared host with target deviations retained; local SQLite readback is not PostgreSQL actor proof.
- Story 3.21 stays in progress until qualified selected inputs, valid owner AD-26 records and accepted 5.7/3.19 input bindings exist. It does not wait for Story 3.19 to issue the downstream records that depend on it. Story 3.19 issuance and production execution remain separately blocked.

## Implementation Notes

The local Docker stage is implemented; it does not establish self-hosted Kubernetes or production-path proof. The C# entry point requires an independently verified, observed result for every vector. A rejected guarantee remains FAIL even if the evidence-capture test passes. Readiness failures and unrelated-container drift fail the full lane.

Twenty-five verifier tests pass. They include contradictory Dapr state readback, wrong loaded components/source identities, false security outcomes, rejected remote Docker endpoints with no cleanup mutation, and escaping input paths rejected before copying. Debug package-mode integration build passes with zero warnings/errors. The source-mode CS1704 blocker is retained separately without changing dependency pins.

Actual broker failures and preservation blockers are retained rather than repaired by changing the pinned runtime or weakening gates. Production selection, Kubernetes rehearsal, protected prior-release inputs, restore/envelope proof, owner records and the exact production bundle remain outstanding. Story status stays `in-progress`.

## Spec Change Log

- 2026-10-20: implemented the disposable local stage and tightened evidence checks against native receipts. Clarified that the C# capture test accepts observed rejected guarantees while the qualification validator and non-promoting workflow still fail them. Frozen intent and acceptance expectations are unchanged.
- 2026-10-20: user explicitly approved the [shared-host course correction](../planning-artifacts/sprint-change-proposal-2026-10-20.md); frozen intent and staged acceptance were renegotiated under [the exact-subject planning receipt](evidence/story-3-21-platform-host-reconciliation-2026-10-20/approval/approval.json). ES-DIAG intent is approved; its Story 9.1 implementation preflight remains required. No AD-26 ratification, broker selection or production authority is inferred.

## Review Triage Log

## Approved follow-up and current integrity limitation

The next bounded build specification is [ES-DIAG](spec-3-21-local-broker-failure-diagnosis.md). It addresses exclusive local Docker execution, both credential rotations, RabbitMQ backpressure and absent bindings without weakening guarantees. Parent status remains `in-progress`. Platform/Builds work remains owner-routed.

The course-correction read-only packet recheck exits 1 with `FAIL: artifacts: unlisted or missing retained files`, before the preservation comparison: `packet/inputs/tools/__pycache__/dapr_broker_qualification.cpython-314.pyc` was already present at task start and remains untouched. Preserve this distinct failure and the reported Docker failure; prevent caches/output inside new closed packets without filtering inventory or resealing old bytes.

## Design Notes

RabbitMQ backlog durability and Kafka replay are separate claims. Production retention, capacity and restore objectives need owner records. Static source concerns require live fault tests.

## Verification

Current results and exact receipts: [retained qualification report](evidence/story-3-21/verification-2026-10-20/report.md). Verifier tests: 25 passed. Debug package-mode build, workflow lint and whitespace checks pass. The latest explicit C# live test executes all 24 vectors but fails preservation because unrelated containers disappear during the run. Four vectors remain rejected/unresolved: both credential rotations, RabbitMQ pressure and RabbitMQ missing bindings. The live-test and final evidence tasks stay unchecked; no formal review or completion is claimed.

- `python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py` — positive packet and mutation cases pass.
- `python3 tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock.json --brokers rabbitmq,kafka --output /tmp/story-3-21-qualification` — owned local execution only, complete receipts and cleanup.
- `python3 tools/validate-dapr-broker-qualification.py /tmp/story-3-21-qualification --require-production-authority` — expected FAIL until real owner records exist.
- `git diff --check` and retained workspace/workload comparison — no introduced whitespace faults or unrelated changes.
