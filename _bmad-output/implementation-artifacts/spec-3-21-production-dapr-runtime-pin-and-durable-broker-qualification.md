---
title: 'Story 3.21 Production Dapr Runtime and Durable Broker Qualification'
type: 'chore'
created: '2026-10-20'
status: 'ready-for-dev'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Kubernetes at `192.168.1.30` runs Dapr `1.18.1`, below AD-26's floor. Existing Redis evidence and broker templates do not establish a durable production profile.

**Approach:** Inventory the cluster read-only, qualify immutable Dapr `1.18.7`/CLI `1.18.2` candidates with RabbitMQ `4.3.6` and Kafka `4.3.1` in disposable infrastructure, and deliver reproducible evidence with a gated rollout/rollback plan. Only an owner AD-26 record selects a runtime/broker; passing experiments convey no deployment authority.

## Boundaries & Constraints

**Always:** Target self-hosted Kubernetes. Preserve existing workspace changes and unrelated workloads. Use Dapr for application infrastructure access; credentials resolve through `secretKeyRef` and AD-24 OpenBao, with verified TLS and default-deny grants. Bind runtime, CLI, embedded components, placement, scheduler, broker, loaded component/subscription bytes, sources and binaries. Reject production runtime versions below `1.18.3` and mutable image references. Keep candidate results distinct from owner ratification and production-path proof. Runtime minor changes require broker requalification and Story 4.16 envelope re-proof.

**Never:** Modify production cluster resources without separate explicit approval of an exact change bundle. Read production Secrets, enroll credentials, reuse isolated workloads as production, migrate state/consumers, or select the broker automatically. Azure/AWS are outside MVP. Do not change sealed `ci.yml`/`integration.yml`, shared Builds pins, Story 2.13 packets, or existing authority bytes. Do not claim Story 2.13 notification distribution, NFR5, G-APPEND, restore posture or production readiness from broker tests. No Git mutation or dependency refresh.

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

- `evidence/story-3-21/qualification-2026-10-20/` below this spec's directory: retained inspection, immutable candidate identities and rollout plan; not executed broker proof.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/SignalRDaprDistribution{Fixture,Node}.cs`: existing two-host patterns are Redis-specific and hardcode local runtime/control-plane endpoints; preserve their behavior and add a separate qualification fixture.
- `deploy/dapr/pubsub-{kafka,rabbitmq}.yaml`: non-authorizing templates with unsuitable environment interpolation; author separate candidate files.
- `architecture.md` AD-5/8/24/26/31/34/36 and Story 3.21 in `planning-artifacts/epics.md`: controlling gates. Stories 3.17/3.19/5.7 own inventory/profile/component handoffs.

## Tasks & Acceptance

**Execution:**

- [ ] `tools/qualify-dapr-brokers.py` — orchestrate only owned disposable infrastructure, capture exact inputs/commands/times/exit codes and cleanup; reject production contexts/endpoints and missing requested scenarios.
- [ ] `deploy/dapr/qualification/{runtime-lock.json,rabbitmq.yaml,kafka.yaml,subscriptions.yaml}` — bind retained immutable candidates, OpenBao references, explicit scopes, verified TLS, durable queues/topics and pre-enrolled exact topic catalogs. These are candidate inputs, never `production-profile.yaml`.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs` and `ContractTests/DurableBrokerQualificationTests.cs` — exercise each candidate through real Dapr hosts: crash/redelivery, broker restart, durable quarantine and failed diversion, retention/expiry, stable IDs, scopes, TLS/secrets/rotation, cancellation/backpressure and missing bindings. Require persisted application readback and broker diagnostics as separate evidence.
- [ ] `tools/validate-dapr-broker-qualification.py` and `tools/tests/test_dapr_broker_qualification.py` — validate identities, retained hashes, complete scenario outcomes and non-authority; mutation-prove below-floor/mutable pins, missing/false evidence and missing owner binding.
- [ ] `.github/workflows/dapr-broker-qualification.yml` — add an independent, manually invoked non-promoting qualification lane, with immutable tool/image inputs and retained sanitized receipts.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-3-21/` — retain outcomes and Story 3.17 inventory-row handoff, guide drift for Story 5.9, and exact proposed production diff/restore bundle after qualification. Bind to Stories 5.7/3.19 only after valid owner records; keep Story 3.21 unfinished until those gates pass.

**Acceptance Criteria:**

- Given both self-hosted candidates, when the isolated lane completes, then every required guarantee has an observed bounded result or explicit FAIL; cloud behavior is outside scope.
- Given a retained packet, when independently verified, then source/component/binary/OCI identities and scenario evidence reproduce without touching production or changing sealed workflows.
- Given read-only inspection and a proposed rollout, when compared with retained baselines, then unrelated workspace bytes and workload specifications remain unchanged; production changes still require exact-bundle approval.
- Given missing owner ratification, when eligibility is checked, then it reports truthful FAIL and no selected broker, promotion or story-done result.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

RabbitMQ backlog durability and Kafka replay are separate claims. Production retention, capacity and restore objectives need owner records. Static source concerns require live fault tests.

## Verification

- `python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py` — positive packet and mutation cases pass.
- `python3 tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock.json --brokers rabbitmq,kafka --output /tmp/story-3-21-qualification` — owned local execution only, complete receipts and cleanup.
- `python3 tools/validate-dapr-broker-qualification.py /tmp/story-3-21-qualification --require-production-authority` — expected FAIL until real owner records exist.
- `git diff --check` and retained workspace/workload comparison — no introduced whitespace faults or unrelated changes.
