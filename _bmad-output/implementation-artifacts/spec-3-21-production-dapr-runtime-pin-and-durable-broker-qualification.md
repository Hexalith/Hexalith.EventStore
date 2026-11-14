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

**Approach:** Inventory the target read-only and retain immutable Dapr `1.18.7`/CLI `1.18.2`, RabbitMQ `4.3.6-management` and Kafka `4.3.1` comparison evidence. EventStore owns backend-neutral Dapr requirements, reusable contract tests, application assertions and evaluation of Platform qualification results for the guarantee/failure handoff. Hexalith.Platform owns concrete infrastructure composition and lifecycle, provider/component configuration and execution of local/CI and disposable self-hosted Kubernetes qualification, including the later exact deployment/restore bundle; Hexalith.Builds owns catalog, CLI and runner pins. EventStore's own AppHost and broker fixture serve repository tests only. Only authenticated owner AD-26 ratification selects a runtime and broker; technical observations, Platform startup and passing capture tests convey no deployment authority.

## Boundaries & Constraints

**Always:** Target self-hosted Kubernetes. Preserve existing workspace changes and unrelated workloads. Use Dapr for application infrastructure access; credentials resolve through `secretKeyRef` and AD-24 OpenBao, with verified TLS and default-deny grants. Bind runtime, CLI, embedded components, placement, scheduler, broker, loaded component/subscription bytes, sources and binaries. Reject production runtime versions below `1.18.3` and mutable image references. Keep candidate results distinct from owner ratification and production-path proof. Runtime minor changes require broker requalification and Story 4.16 append/conflict-protection re-proof. Preserve the full 24-vector comparison and its existing pass conditions. Record packet integrity, observed scenario outcomes, candidate qualification, Platform composition/rehearsal and production authority as distinct results. Use an exclusively allocated local Docker daemon for new captures; every pre-existing unrelated container identity/specification remains in the preservation denominator. Platform composition tests consume its runner-owned descriptor and never start an EventStore-owned shared host. All cross-repository evidence binds exact revisions, tool/catalog and artifact identities, loaded configuration and accepted owner handoffs.

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

## Approved Story 4.16 Capability Handoff

Owner “approve and accept” (2026-11-01) accepts the [atomic conflict-protection scope](../planning-artifacts/sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md). Define the module's required Dapr semantics and reusable test vectors, then evaluate Platform-supplied qualification of an exact supported SDK/runtime/component capable of mandatory actor revision comparison, create-once event identity and whole-batch rollback, with no same-identity raw/TTL/delete bypass and preserved authorized erasure. Platform owns concrete environment and provider/fault execution; EventStore owns the logical Dapr application assertions and rejection of an incapable profile. Include the accepted existing lifecycle-capability transport/HMAC-verifier custody extension in the exact capability/trust handoff; it is not already implemented. The inspected lock supplies no demonstrated mandatory primitive, and broker checks do not qualify it. Keep that gap unresolved until observed proof exists; a private runtime build or provider exception needs a separate decision. 4.16 retains the commit contract, actor integration and reused prevention tests. No candidate pin, frozen broker denominator, lifecycle status or production authority changes in this planning application.

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
- [x] `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs` and `ContractTests/DurableBrokerQualificationTests.cs` — exercise each candidate through real Dapr hosts: crash/redelivery, broker restart, durable quarantine and failed diversion, retention/expiry, stable IDs, scopes, TLS/secrets/rotation, cancellation/backpressure and missing bindings. Require persisted application readback and broker diagnostics as separate evidence.
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

- 2026-11-12: Evaluated Platform's retained candidate-01 hosted attempt and exact source/input bindings. The attempt ended before job allocation, with 0/24 observed vectors and no packet, verifier, cleanup or artifact-delivery proof. Recorded proposed inventory/documentation handoffs and current locked-input drift without refreshing profiles or advancing the open parent task. The completed input-enabling slice is no longer the next build target.

- 2026-11-11: Implemented the bounded Platform input seam through a separate explicit profile and schema-3 packet, with the same independently supplied JSON required for verification. Both prior locks, all 24 vectors and all five rejected guarantees remain. Platform's dated dispositions reject the historical reauthentication/alternate-exchange suggestions: RabbitMQ pressure needs a released runtime with the upstream confirm fix, and missing binding separately needs mandatory/return handling. No infrastructure, CI, broker selection or parent completion is claimed.

- 2026-11-11: The owner selected “approve and stop” for the [Platform configuration input slice](spec-3-21-platform-broker-configuration-input.md). Its status is now `ready-for-dev`; implementation has not started. Parent status and qualification results remain unchanged.

- 2026-11-11: The owner answered “yes” to reopening deferred broker remediation under Platform's ownership. Prepared the [Platform configuration input slice](spec-3-21-platform-broker-configuration-input.md) as a draft addressing Platform's documented harness-input blocker. This authorizes reopening/planning, not approval of the new spec or live execution. Closed refresh results, frozen parent intent and parent lifecycle remain unchanged.

- 2026-11-09: Checked the DurableBrokerQualificationFixture/Tests task against hosted run [42532776864](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42532776864) on `0a6725d51`: C# capture PASS (1/1), all 24 vectors observed and preservation PASS. Separate guarantee results remain RabbitMQ rotation FAIL, pressure FAIL and missing binding FAIL; Kafka rotation FAIL and pressure FAIL. Parent status remains `in-progress`.

- 2026-11-09: Marked ES-DIAG superseded by the closed 1.19.1 refresh slice. Pointed the follow-up and verification sections at the hosted 1.19.1 results and the deferred broker remediation. Frozen intent, tasks, acceptance criteria and `in-progress` status are unchanged.

- 2026-11-01: Applied the owner-approved Story 4.16 capability dependency and append/conflict-protection re-proof handoff; candidate pins, full broker matrix, historical observations and in-progress lifecycle remain unchanged.

- 2026-10-20: implemented the disposable local stage and tightened evidence checks against native receipts. Clarified that the C# capture test accepts observed rejected guarantees while the qualification validator and non-promoting workflow still fail them. Frozen intent and acceptance expectations are unchanged.
- 2026-10-20: user explicitly approved the [shared-host course correction](../planning-artifacts/sprint-change-proposal-2026-10-20.md); frozen intent and staged acceptance were renegotiated under [the exact-subject planning receipt](evidence/story-3-21-platform-host-reconciliation-2026-10-20/approval/approval.json). ES-DIAG intent is approved; its Story 9.1 implementation preflight remains required. No AD-26 ratification, broker selection or production authority is inferred.

- 2026-11-13: Reconciled Platform candidate-02's retained, independently verified 20/24 result and subsequent remediation intake. The November 12 pre-allocation failure and source-drift observations below are historical. Recorded the current four failed vectors, all five open historical guarantees and missing engineering/owner inputs without changing frozen intent, task completion, predicates, locks or either parent status.

- 2026-11-13: Continued offline assessment from published Platform `44756d1db8a38ac305cbb5247ae9cd52985b1d76` and EventStore `5f4db84502c7c54b316dd9429dccd12ae881e759`. Prepared the proposed remediation evidence contract: pressure-fault feasibility plus producer/verifier admission dependency, minimum principal/applied-ACL/publisher/readback join, supported RabbitMQ delivery and packet-custody decision. This accepts no implementation, execution, pins or guarantee closure. The reproducible audit now compares immutable evidence links with the published Git baseline, allowing the current intake to evolve without rewriting the published subject.

## Review Triage Log

## Approved follow-up and current integrity limitation

**Current (2026-11-13):** Platform candidate-02 observed all 24 vectors, with retained independent verification recording 20 PASS / 4 FAIL, integrity verified and cleanup credited. Technical qualification and production eligibility remain FAIL, selected broker null, and both parent stories in-progress. The [current result reconciliation](#platform-result-reconciliation--2026-11-13) below records exact execution identities and the unresolved Kafka pressure, Kafka rotation and supported RabbitMQ delivery intakes. The [offline successor assessment](../../docs/implementation/story-3-21-broker-remediation-evidence-contract.md) records the owner-approved offline evidence criteria and remaining concrete inputs; no remedy or execution is accepted. The reconciliation supersedes the startup failure and source-drift observations as current blockers, while preserving those dated assessments. Reading the evidence does not accept a diagnostic plan, inventory/profile handoff, AD-26 decision or further execution.

**Historical (2026-11-12):** EventStore assessed Platform's [candidate-01 hosted report](../../references/Hexalith.Platform/docs/implementation/story-4-17-broker-candidate-01-hosted.md) and retained receipts. Its single attempt ended as `startup_failure` before job allocation: zero jobs, zero artifacts and 0/24 observed vectors. This supplies an infrastructure-blocker assessment, with packet integrity, technical qualification, cleanup and artifact delivery still unproven. The [Platform configuration input slice](spec-3-21-platform-broker-configuration-input.md) is done. Platform owns startup investigation and any separately accepted further execution; the consumed single-attempt scope does not authorize a retry. The exact-subject assessment and proposed handoffs below preserve all five historical rejected guarantees, the full 24-vector denominator, null broker selection, FAIL production eligibility and `in-progress` parent status.

**Enabling slice (2026-11-11):** The owner reopened deferred broker remediation under Platform's ownership. The approved [Platform configuration input slice](spec-3-21-platform-broker-configuration-input.md) implemented an explicitly selected schema-3 harness input and independent verification without running infrastructure or CI. Platform's [2026-11-10 dispositions](../../references/Hexalith.Platform/docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md) identified the harness's pinned configuration as the first dependency. Configuration provenance binds exact bytes and declared Platform revision/path without authenticating owner acceptance. The enabling implementation supplied no runner capability or execution acceptance.

**Prior closure (2026-11-09):** No EventStore build slice was open. The [Dapr 1.19.1 refresh slice](spec-3-21-latest-stable-dapr-runtime-refresh.md) superseded [ES-DIAG](spec-3-21-local-broker-failure-diagnosis.md) before ES-DIAG was implemented, and the owner closed the refresh slice on 2026-11-09. Hosted runs [42532773702](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42532773702) and [42532776864](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42532776864) on `0a6725d51bb17a9eae28a24a53f3636bab9d48d7` executed all 24 vectors. Capture, integrity, cleanup and unrelated-resource preservation passed. Kafka passed 10/12 and RabbitMQ 9/12. Five guarantees remain rejected: RabbitMQ rotation, pressure and missing binding, and Kafka rotation and pressure. No broker can be selected until each one is remediated or accepted as an owner exception. That remediation was an optional `deferred-work.md` item awaiting the owner go-ahead recorded above. Parent status remains `in-progress`. It waits on Platform's Kubernetes rehearsal, the Builds pin adoption, AD-26 ratification and the Story 4.16 handoff.

**Historical (2026-10-20, 1.18.7 packet):** ES-DIAG was the next bounded slice, covering exclusive local Docker execution, both credential rotations, RabbitMQ backpressure and absent bindings. The course-correction read-only packet recheck exits 1 with `FAIL: artifacts: unlisted or missing retained files`, before the preservation comparison: `packet/inputs/tools/__pycache__/dapr_broker_qualification.cpython-314.pyc` was already present at task start and remains untouched. Preserve this distinct failure and the reported Docker failure; prevent caches/output inside new closed packets without filtering inventory or resealing old bytes.

## Design Notes

RabbitMQ backlog durability and Kafka replay are separate claims. Production retention, capacity and restore objectives need owner records. Static source concerns require live fault tests.

## Platform result assessment — 2026-11-12

Historical assessment of the first, unallocated attempt. Its statements about the then-current startup and lock-drift blockers are superseded by the November 13 reconciliation; its original identities, observations and verification results remain unchanged.

Assessed the retained Platform checkout `add09b79e61b6e08469bc37f9ec12e82899531db` from EventStore source `9aba65a5f4c8735e666d96b397a06ca8d8d9a55d`. The [hosted report](../../references/Hexalith.Platform/docs/implementation/story-4-17-broker-candidate-01-hosted.md), [execution status](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/execution-status.json), [vector assessment](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/vector-assessment.json) and [independent-verifier assessment](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/independent-verifier-assessment.json) agree. This assessment reads existing receipts; it does not authenticate an owner handoff or establish execution from a source pin alone.

| Bound input | Exact identity |
| --- | --- |
| Platform run / attempt | [43097161643 / 1](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643), actual run head `94001fad925fdf7fa03a90b507b8f8e19074f9e3` |
| EventStore source selected by that Platform tree | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Explicit qualification profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json`, SHA-256 `1c1da59176f78b9b60a87ba85b6076b8d215ff912840ecb52bdb7432497c587f`, 91 inputs |
| Original independent Platform configuration | `docs/implementation/story-4-17-broker-candidate-01.json`, SHA-256 `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| Configuration's declared provenance | Platform `6d422033b959c249c149a4cb3785704e9eebe03c`; unauthenticated declaration, distinct from actual run head |
| Platform workflow at actual run head | `.github/workflows/broker-candidate-qualification.yml`, SHA-256 `011a84ed42b60ad30bc0c7c6cfbf80481d9883f9fb00c184756b44f9bfec12b2` |
| Retained metadata manifest | SHA-256 `84af5f8c872acf1a5073af93b89d0f3566a161c93139c464fbb5af7c1af33176`; all 79 listed files match their size/hash commitments and complete inventory |

The actual Platform Git tree contains the stated original configuration bytes and EventStore gitlink. The immutable profile retains Dapr runtime/CLI 1.19.1, SDK/components 1.19.0, both digest-pinned brokers and all 24 vectors. These are input identities. No allocated runner observed binaries, tool/catalog adoption, worker custody, loaded components/subscriptions, composed workload, commands, persisted effects or provider diagnostics.

| Result dimension | Assessment |
| --- | --- |
| Retained metadata integrity | PASS for the complete metadata inventory and cross-receipt consistency; no broker packet exists |
| Hosted execution | `startup_failure` before job allocation; zero jobs and zero artifacts |
| Scenario observations | 0/24; each expected broker/scenario pair is present exactly once as `NOT_EXECUTED` with no observed result |
| Packet integrity / technical / authority verifier execution | All absent; actual argv and exits are null, so no verifier PASS or observed technical result is assigned |
| Cleanup / unrelated-resource preservation / artifact delivery | Unproven; no cleanup receipt, execution baseline or downloadable packet |
| Candidate qualification / production authority | Unqualified / FAIL; selected broker null |

GitHub's retained unexpected-error annotation supplies support request `4ed78a69-de10-11f1-8d56-5705d8f2f5ff`. Its cause is unconfirmed. A generic CLI hint about a possible workflow-file issue does not diagnose the failure. Platform's accepted attempt is consumed; startup investigation and a separate decision on further execution precede any new capture. This assessment closes no capture/preservation, Kubernetes rehearsal or owner-acceptance requirement.

### Proposed downstream handoffs

These rows are proposals for the owning stories, with the exact input subject above. No inventory/profile acceptance, owner exception or cross-repository delivery receipt is inferred.

| Owner / proposed row | Required guarantee and current disposition | Missing acceptance input |
| --- | --- | --- |
| Story 3.17 — `pubsub.rabbitmq/v1`, local candidate | Full 12-scenario Dapr-path matrix. Historical runtime-1.19.1 results remain 9 PASS / 3 FAIL: rotation, pressure and missing binding. Candidate-01 has 0/12 observations. Provider administration remains diagnostic/fault injection. | A complete independently verified executed packet with persisted Dapr readback, exact loaded inputs, worker custody and preservation; Platform acceptance of its exact execution subject |
| Story 3.17 — `pubsub.kafka/v1`, local candidate | Full 12-scenario Dapr-path matrix. Historical runtime-1.19.1 results remain 10 PASS / 2 FAIL: rotation and pressure. Candidate-01 has 0/12 observations. Timeouts and bounded non-observation do not establish absence. | The same packet/identity/preservation controls, with attributable authorization and exact publication reconciliation; no direct-provider application fallback |
| Story 5.9 — qualification reproduction guidance | The enabling slice is done; the current dependency is Platform's pre-job startup failure. Distinguish declared configuration provenance, actual source pins, absent execution and historical observed failures. Current-checkout qualification commands also reject the source drift recorded below. | Documentation aligned with the actual execution subject, non-authorizing examples and unchanged immutable lock identities |
| Platform Story 4.17 — candidate-01 | Retain the exact original JSON, explicit profile and EventStore source pin for startup investigation and any separately accepted further attempt. All four chosen tuning values remain experiments. | Accepted scope, supported exclusively allocated runner and a complete new packet; later Kubernetes rehearsal must bind the same qualified inputs and actual composed workload |
| Builds / Stories 4.16, 5.7 and 3.19 | Shared catalog/tool/runner adoption, mandatory actor conflict-protection and lifecycle-capability/HMAC custody proof, qualified selected-component and runtime/profile handoffs remain separate. | Builds adoption; observed supported-Dapr capability proof; valid per-role AD-26 ratification and accepted exact-subject 5.7/3.19 input bindings |

RabbitMQ pressure still requires a released runtime containing the upstream negative-confirm fix identified in Platform's dated dispositions; missing binding separately requires mandatory/return handling. No current upstream-release claim or new remedy is established here. Kafka principal/session, ACL timing and exact publication-to-offset causality remain unresolved. The five historical FAILs are neither replaced by `NOT_EXECUTED` nor converted into passes.

### Assessment verification and current source blocker

- Read-only SHA-256, size and complete-inventory checks passed for all 79 current Platform metadata files; all 24 expected vector identities, null verifier argv/exits, source/configuration/gitlink bindings and FAIL/null authority fields agree. The original archived manifest was also checked using its documented archive-first/parent fallback without changing historical bytes.
- `python3 -B -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py` — 25 passed, zero failures, in the current EventStore checkout.
- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'` — exit 1 in the current checkout. An audit rerun with `-v` retained `/tmp/story-3-21-current-validation-ywt2bozc/qualification-tests.log`: 156 test methods in 14.850 seconds, 96 passed and 60 unsuccessful; unittest reports 76 failure entries and 22 error entries including subtests. Of those unsuccessful methods, 46 have failures and 17 have errors, with three in both groups. The first rejection is `inputs..github/workflows/dapr-runtime-qualification.yml: source/component bytes drifted`; dependent controls cannot reach their intended assertions. A full hash comparison identifies three drifted inputs in the historical 1.19.1 lock: `.github/workflows/dapr-runtime-qualification.yml`, `tools/qualify-internal-actor-admission.py` and `tools/run-dapr-runtime-lane.py`. The explicit Platform lock also rejects `tools/tests/test_qualify_internal_actor_admission.py`. All 19 original-1.18.7 input commitments still resolve without drift. No profile or trusted historical bytes were refreshed to make these checks pass.
- Extracted EventStore `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` with read-only `git archive` into `/tmp/story-3-21-pinned-validation-gdcf11pk/source`. Its Platform profile bytes exactly match the hash above. In that isolated exact source, `python3 -B -m unittest discover -v -s tools/tests -p 'test_dapr*qualification.py'` — 156 passed, zero failures/skips, in 43.570 seconds. Output is `/tmp/story-3-21-pinned-validation-gdcf11pk/qualification-tests.log`. This is offline pinned-source evidence, not success of the current checkout or a live broker capture.
- `git diff --check` — passed. Direct Git-object comparisons preserve frozen parent intent, the complete historical next-build prompt, both historical locks, the Platform lock, sealed `ci.yml`/`integration.yml` and `docs/ci.md`. All six new relative links/anchors resolve; only the two assessment/planning documents changed, with no gitlink mutation.

Metadata audit command (run from EventStore's root): `python3 -B /tmp/story-3-21-platform-assessment-f5w6cc57/verify_metadata.py` — exit 0. It verifies all 79 current and 52 original archived commitments, exact source/configuration/profile/workflow/gitlink identities, 24 `NOT_EXECUTED` vectors, absent packet/verifier/cleanup and FAIL/null authority. The stdlib-only read-only script and its `verification.log` remain in that system-temporary directory; no packet verifier was invoked against missing evidence.

No new CI run was dispatched. The linked Platform run is the retained failed attempt, not a new verification run. The uncompleted final task, frozen intent and `in-progress` status remain unchanged. Fresh execution remains blocked on Platform's startup investigation and accepted further scope/worker; this session has no owner-issued exclusive worker allocation. Kubernetes/restore, Builds adoption, Story 4.16 capability proof, AD-26 ratification and accepted selected input bindings remain outstanding. Historical packets, locks, sealed workflows and authority records remain untouched.

## Platform result reconciliation — 2026-11-13

Read the retained Platform [authoritative attempt assessment](https://github.com/Hexalith/Hexalith.Platform/blob/a71fee52cf03417e439e0b1141ccc64ae5cf3ad9/_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1/attempt-assessment.json), [vector assessment](https://github.com/Hexalith/Hexalith.Platform/blob/a71fee52cf03417e439e0b1141ccc64ae5cf3ad9/_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1/vector-assessment.json), [hosted report](https://github.com/Hexalith/Hexalith.Platform/blob/a71fee52cf03417e439e0b1141ccc64ae5cf3ad9/docs/implementation/story-4-17-broker-candidate-02-hosted-attempt-1.md) and [next-candidate intake](https://github.com/Hexalith/Hexalith.Platform/blob/a71fee52cf03417e439e0b1141ccc64ae5cf3ad9/docs/implementation/story-4-17-next-candidate-intake.md). These immutable links require no nested Platform submodule. This reconciliation checks the retained assessments and current source admission; it does not re-download or independently reverify the original broker packet. The hosted and local verifier results below are those recorded by Platform.

| Bound input | Exact identity |
| --- | --- |
| Executed run / attempt | [43386723091 / 1](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43386723091); immutable tag `qualification/broker-candidate-02/attempt-1-4d66ff55` |
| Executed Platform / EventStore revisions | `4d66ff5584e4f62239551bb02311649334171a84` / `495dd24943031ec75e2822c6168ada3c8b9fdb82` |
| Original independent configuration | `docs/implementation/story-4-17-broker-candidate-02.json`, SHA-256 `b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b` |
| Explicit profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json`; runtime/CLI 1.19.1, SDK/components 1.19.0; full both-broker/24-vector denominator |
| Artifact identity and custody limitation | ID `14018214306`, ZIP SHA-256 `a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a`; bytes were not committed; recorded expiry `2026-12-13T07:40:43Z`. The links retain assessments, not the entire packet or independent off-site custody. |
| Documentation review baselines | Platform `a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9`; EventStore `fd07d0ab33858818d836e43bf8c112c5d4827534`. Neither replaces the executed identities above. |

| Result dimension | Retained assessment |
| --- | --- |
| Capture and integrity | All 24 observed; producer exit 1; independent integrity exit 0 both hosted and locally, with identical verifier stdout |
| Technical qualification | FAIL, independent technical exit 1; 20 verified PASS / 4 verified FAIL |
| Current failed vectors | `kafka/cancellation-backpressure`, `rabbitmq/secret-rotation`, `rabbitmq/cancellation-backpressure`, `rabbitmq/missing-bindings` |
| Cleanup | Complete receipt and credited cleanup, following verified packet integrity |
| Production authority | Expected independent authority rejection, exit 1; production eligibility FAIL and selected broker null |
| Historical guarantees | All five remain REJECTED/open; candidate-02's verified Kafka rotation PASS is one observation awaiting historical-failure adjudication |

Kafka pressure now has acknowledgedLoss 0, but none of its two HTTP 204 and six HTTP 500 responses supplies the accepted attributable rejection. RabbitMQ durable-quarantine and retention-expiry pass with the candidate-02 defaults; its rotation (stale publish 599), pressure (three acknowledged IDs missing at the bound, no attributable rejection) and missing binding (unbound publish 204) still fail. Candidate-01 attempt 2 was diagnostic-only because its pinned verifier rejected the packet; it receives no qualification credit from candidate-02's later verified packet.

The next-candidate intake leaves three engineering dependencies:

1. **Kafka pressure — EventStore 3.21:** demonstrate a supported producer-pressure fault satisfying the unchanged `pressure_rejection` predicate through Dapr. The present `producer_byte_rate=1024` fault throttles rather than demonstrating the required Produce rejection; another configuration-only attempt has no demonstrated remedy. Timeouts remain uncertain and FAIL. Bring any proposed guarantee/predicate change to the owner separately.
2. **Kafka rotation — EventStore 3.21:** adjudicate the historical failure versus the one verified PASS. Existing receipt/source comparisons do not bind the stale request to its actual authenticated broker principal or prove applied authorizer state. Define the missing publisher/generation, principal, ACL-state, stale-publication and persisted-readback observations, retaining the positive new-generation control and missing cold-old-generation proof. The analysis supplies no accepted diagnostic plan, instrumentation or new run.
3. **RabbitMQ — EventStore/upstream, then Builds and Platform:** obtain supported publish-path fixes for NACK propagation through retry/cancellation, revoked-credential/reconnect errors, and mandatory publishing with correlated returns. A lower-level NACK backport alone demonstrates no end-to-end remedy. At this read-only check the latest [runtime 1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1) and [components 1.19.0](https://github.com/dapr/components-contrib/releases/tag/v1.19.0) predate [backport #4748](https://github.com/dapr/components-contrib/pull/4748). Builds owns accepted runtime/tool alignment; Platform owns exact supported configuration and execution. No private build or exception is accepted.

The previous approved attempt is consumed. A successor needs its own accepted question/evidence plan, exact source/lock/configuration bindings, supported worker/tools and separate execution decision. Platform Kubernetes/shared-host and restore rehearsal, staging 4.14–4.16, Builds adoption, authenticated per-role AD-26 ratification, Platform Story 4.9's profile inventory and accepted 5.7/3.19 input bindings remain outstanding. Frozen parent intent, tasks, acceptance criteria, all historical assessments and both in-progress parent statuses are preserved.

### Current proposed downstream handoffs

These rows replace the November 12 table only as current proposals; no inventory/profile acceptance or owner-delivery receipt is supplied.

| Owner / proposed row | Latest exact-subject observation | Missing acceptance input |
| --- | --- | --- |
| Story 3.17 — `pubsub.kafka/v1`, local/CI candidate | Candidate-02: 11/12 verified PASS; pressure FAIL with acknowledgedLoss 0 but no attributable rejection. Rotation PASS once; historical rotation and pressure guarantees remain REJECTED/open. | Feasible accepted pressure fault and rotation adjudication/evidence plan; qualified selected inputs and authenticated exact-subject inventory acceptance remain absent. |
| Story 3.17 — `pubsub.rabbitmq/v1`, local/CI candidate | Candidate-02: 9/12 verified PASS; rotation, pressure and missing binding FAIL. | Supported end-to-end publish-path fixes and full requalification; no remedy, exception or selected-component handoff is accepted. |
| Story 5.9 — qualification reproduction guidance | Record the consumed candidate-02 attempt, separate 20/24 technical FAIL versus verified integrity/credited cleanup, original configuration and execution identities, and current 94-input admission PASS. Startup/lock-drift statements from November 12 are historical. | Guidance using these immutable assessment/intake links, no claims of new packet verification, production authority, accepted diagnostic scope or a runnable successor. |

Platform Story 4.17's deferred-work ledger records the separate custody decision needed before the artifact's recorded expiry. The current assessment links are insufficient custody of the full packet required for later adjudication.

Current offline check: `tools/qualify-dapr-brokers.py`, `tools/validate-dapr-broker-qualification.py`, `tools/dapr_platform_broker_configuration.py` and `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` are byte-identical to executed EventStore `495dd24943031ec75e2822c6168ada3c8b9fdb82`, and `validate_profile` accepts all 94 current locked inputs. This resolves the November 12 locked-input drift as a current admission blocker; it proves no new broker behavior or runtime capability.

Offline verification from the Platform workspace: `PYTHONDONTWRITEBYTECODE=1 PYTHONOPTIMIZE=2 python3 -B /tmp/platform417-owner-reconciliation-elxsnl8b/verify_reconciliation.py` — exit 0. This temporary stdlib-only audit checked all 94 locked inputs, nine protected source/profile/workflow hashes, four exact candidate-source comparisons, unchanged frozen intent/tasks/acceptance criteria, the 24-vector inventory and 20/4/five-historical counts, original configuration/run/verifier/cleanup/authority identities, both parent statuses/main branches and four immutable evidence links against their Git objects. The remote GitHub commit API also confirmed the linked Platform revision exists. This corrects documentation only; no new CI or broker run occurred and no behavioral test result is claimed.

Reproduce the offline checks below from the Platform workspace containing its root-declared EventStore checkout. No temporary script, saved snapshot, nested submodule, service or network access is required. The source identities must already exist in the local Git object stores. Failure to locate an identity stops verification; do not substitute current sources for it.

```bash
PYTHONOPTIMIZE=2 python3 -B - <<'PY'
from collections import Counter
from pathlib import Path
import hashlib, json, re, subprocess, sys
root = Path.cwd()
owner = root / "references/Hexalith.EventStore"
def check(ok, message):
    if not ok:
        raise SystemExit(message)
def blob(repo, revision, path):
    return subprocess.run(["git", "-c", "core.commitGraph=false", "show", revision + ":" + path],
                          cwd=repo, check=True, capture_output=True).stdout
sys.path.insert(0, str(owner / "tools"))
from dapr_broker_qualification import BROKERS, SCENARIOS, validate_profile
profile_path = "deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json"
profile = json.loads((owner / profile_path).read_text())
validate_profile(profile, owner)
check(len(profile["inputs"]) == 94, "locked-input denominator differs")
baseline = "fd07d0ab33858818d836e43bf8c112c5d4827534"
candidate = "495dd24943031ec75e2822c6168ada3c8b9fdb82"
protected = ["tools/qualify-dapr-brokers.py", "tools/validate-dapr-broker-qualification.py",
             "tools/dapr_broker_qualification.py", "tools/dapr_platform_broker_configuration.py",
             "deploy/dapr/qualification/runtime-lock.json", "deploy/dapr/qualification/runtime-lock-1.19.1.json",
             profile_path, ".github/workflows/ci.yml", ".github/workflows/integration.yml"]
for path in protected:
    check((owner / path).read_bytes() == blob(owner, baseline, path), "protected bytes differ: " + path)
for path in [protected[0], protected[1], protected[3], profile_path]:
    check((owner / path).read_bytes() == blob(owner, candidate, path), "candidate source differs: " + path)
parent = "_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md"
before = blob(owner, baseline, parent).decode()
after = (owner / parent).read_text()
for start, end in [("<frozen-after-approval", "</frozen-after-approval>"),
                   ("## Tasks & Acceptance", "## Implementation Notes")]:
    check(before[before.index(start):before.index(end)] == after[after.index(start):after.index(end)],
          "frozen intent/tasks/acceptance criteria differ")
base = root / "_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1"
a = json.loads((base / "attempt-assessment.json").read_text())
v = json.loads((base / "vector-assessment.json").read_text())
check(len(v["vectors"]) == 24 and {x["vector"] for x in v["vectors"]} ==
      {b + "/" + s for b in BROKERS for s in SCENARIOS}, "vector inventory differs")
check(Counter(x["verifierResult"] for x in v["vectors"]) == {"PASS": 20, "FAIL": 4} and
      all(x["verified"] is True for x in v["vectors"]) and
      sum(x["historicallyRejected"] for x in v["vectors"]) == 5, "vector outcomes differ")
failed = {"kafka/cancellation-backpressure", "rabbitmq/cancellation-backpressure",
          "rabbitmq/missing-bindings", "rabbitmq/secret-rotation"}
check({x["vector"] for x in v["vectors"] if x["verifierResult"] == "FAIL"} ==
      set(a["vectors"]["verifiedFailed"]) == failed and
      a["vectors"]["observed"] == v["counts"]["observed"] == 24 and
      a["vectors"]["verifiedPass"] == v["counts"]["verifiedPass"] == 20 and
      a["vectors"]["verifiedFail"] == v["counts"]["verifiedFail"] == 4, "assessment counts/failures differ")
check(a["runId"] == 43386723091 and a["runAttempt"] == 1 and
      a["identitiesObservedOnWorker"]["platform-revision"] == "4d66ff5584e4f62239551bb02311649334171a84" and
      a["identitiesObservedOnWorker"]["eventstore-revision"] == candidate, "run/source identities differ")
config_hash = hashlib.sha256((root / "docs/implementation/story-4-17-broker-candidate-02.json").read_bytes()).hexdigest()
check(config_hash == a["identitiesObservedOnWorker"]["configuration-sha256"] ==
      "b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b", "configuration differs")
check(a["verification"]["hosted"] == a["verification"]["local"] ==
      {"integrity": 0, "technical": 1, "authority": 1} and a["cleanup"]["credited"] is True and
      a["cleanup"]["receiptComplete"] is True and a["verification"]["integrityVerified"] is True and
      a["verification"]["hostedAndLocalStdoutIdentical"] is True and
      a["verification"]["technicalQualification"] == "FAIL" and
      a["unchanged"]["selectedBroker"] is None and a["unchanged"]["productionEligibility"] == "FAIL",
      "verification/cleanup/authority differs")
for repo, key in [(root, "4-17-select-and-qualify-the-durable-production-broker"),
                  (owner, "3-21-production-dapr-runtime-pin-and-durable-broker-qualification")]:
    check(re.search(r"^  " + re.escape(key) + r": in-progress$",
                    (repo / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text(), re.M),
          "parent status differs")
    branch = subprocess.run(["git", "-c", "core.commitGraph=false", "branch", "--show-current"],
                            cwd=repo, check=True, capture_output=True, text=True).stdout.strip()
    check(branch == "main", "branch differs")
links = re.findall(r"https://github.com/Hexalith/Hexalith.Platform/blob/([0-9a-f]{40})/([^\s)]+)", after)
check(len(links) == 4, "immutable file-link count differs")
for revision, path in links:
    check(blob(root, revision, path) == blob(root, "44756d1db8a38ac305cbb5247ae9cd52985b1d76", path),
          "linked published evidence differs: " + path)
print("PASS: 94 inputs; protected/candidate bytes; frozen requirements; 24 vectors, 20/4/five historical; identities; authority; statuses; immutable evidence links")
PY
```

Final reconciliation checks: the exact documented Bash block was extracted and executed with `bash -eu`, including inherited Python optimization; exit 0 with the PASS output above. Parsed sprint data is unchanged in both repositories; changed-line whitespace passes. Blind review produced five findings: denominator wording was clarified outside frozen intent, the audit was made reproducible, the profile-inventory prerequisite and current downstream proposals were added, and the pre-existing packet-custody decision was deferred to Platform before expiry. No new behavior or qualification was tested. Local commit-message preflight used the repository's pinned commitlint 21.2.4: `./node_modules/.bin/commitlint --edit /tmp/platform417-owner-reconciliation-elxsnl8b/eventstore-commit.txt --verbose` — exit 0, zero problems/warnings. No remote publication is authorized.

## Offline successor proposal — 2026-11-13

The proposed [owning evidence contract](../../docs/implementation/story-3-21-broker-remediation-evidence-contract.md)
extends the published reconciliation and intake. It records demonstrated source behavior,
unresolved dependencies and conditional acceptance criteria separately:

- Kafka's present bandwidth/request quotas do not supply the required Produce rejection.
  No supported alternative was identified. The verifier additionally requires the exact
  byte-rate-quota command, so any feasible replacement requires EventStore producer/evidence
  admission changes while preserving rejection semantics and all 24 vectors.
- Rotation adjudication needs the actual publisher/generation, broker-observed effective
  principal, installed **and applied** ACL state, stale request and persisted readback joined
  per capture. Stock audit logging is only a potential observation input; adequate request
  correlation remains to be established. Missing historical observations cannot be recreated
  by another passing run. Cold-generation denial remains a proposed diagnostic question.
- Current official Dapr/component releases still predate the NACK backport. Exact upstream
  branch inspection leaves retry/cancellation, reconnect error and mandatory/return delivery
  requirements open. No end-to-end supported RabbitMQ remedy is demonstrated.
- Platform must decide durable packet custody, retention, operator and independent verifier,
  and complete separately authorized preservation before candidate-02's recorded
  `2026-12-13T07:40:43Z` expiry. The two relied-on historical Kafka failure packets expire earlier,
  at `2026-12-08T23:05:01Z` and `2026-12-08T23:08:16Z`; their custody decisions cannot wait until
  December 13. Assessment links and temporary downloads are insufficient custody.

The owner's subsequent **“approve”** accepts the bounded offline feasibility/evidence scope,
minimum rotation criteria, RabbitMQ delivery requirements and per-archive preservation criteria;
the [owning contract](../../docs/implementation/story-3-21-broker-remediation-evidence-contract.md#owner-approval--2026-11-13)
records that decision and the subsequent bounded packet recheck. This owner approval supplies
no supported pressure mechanism, exact instrumentation admission, upstream delivery or Builds
adoption. Missing historical principal/applied-state observations leave causality unadjudicable
on the available evidence, with Kafka rotation still REJECTED/open. Platform still needs actual
custody terms/operator/verifier and a later exact execution subject; no transfer or run is admitted.
The separate EventStore sibling checkout's concurrent work is untouched; both owning checkouts
remain on main. All five historical guarantees remain **REJECTED/open**, technical qualification
**FAIL (20/24)**, broker selection **null**, production eligibility **FAIL**, and both parent
statuses **in-progress**. Frozen intent/tasks/acceptance, original packets and all pins are preserved.

## Applied recommendations — 2026-11-14

The owner's **“apply recommended”** direction produced a
[bounded Kafka work package](../../docs/implementation/story-3-21-kafka-bounded-investigation.md)
and [two local RabbitMQ upstream drafts](../../docs/implementation/story-3-21-rabbitmq-upstream-work-packages.md).
The Kafka source review supplies no supported rejecting Produce fault and stops replacement
implementation. Supported dynamic request/authorizer logger controls narrow the rotation candidate,
but process/socket, stale-ID/request and audit/request joins remain unresolved. No instrumentation
or causal remedy is claimed. RabbitMQ package A covers pressure/rotation publish-error propagation;
package B follows the unresolved mandatory/returns half of closed upstream issue #4697. Neither
draft was posted, and no supported complete delivery was identified in the fresh source check.

Platform also staged the four exact original archives locally, with independently checked
digests, 2,215 entry inventories and 2,059 internal artifact hashes. Original evidence labels
remain. Staged names share original cache inodes after disk-space recovery and are not independent
storage. The inspected handoffs identify no accepted off-site archive/allocation/access reference;
this is a scoped finding. Off-site custody is still open before the recorded December 8/13
deadlines, with a retention hold while guarantees remain open and later policy review.

The source producer/verifier, locks, runtime/tool pins and all 24 predicates/vectors are unchanged.
Both parent statuses stay in-progress, five historical guarantees REJECTED/open, technical
qualification FAIL (20/24), broker selection null and production eligibility FAIL. No infrastructure,
qualification, external issue, pin adoption or broker-selection action occurred.

## Verification

The latest retained independently verified broker-guarantee result is Platform candidate-02, reconciled above on 2026-11-13: 20/24 PASS, technical qualification FAIL, five historical guarantees open. The closed [refresh slice](spec-3-21-latest-stable-dapr-runtime-refresh.md) records the earlier 19/24 result. The November 12 assessment preserves its unexecuted attempt and then-current source blocker as history. The rest of this section is historical 2026-10-20 1.18.7 evidence. Its linked report was removed by the 2026-10-23 evidence-storage simplification.

Historical results and exact receipts: [retained qualification report](evidence/story-3-21/verification-2026-10-20/report.md). Verifier tests: 25 passed. Debug package-mode build, workflow lint and whitespace checks pass. The latest explicit C# live test executes all 24 vectors but fails preservation because unrelated containers disappear during the run. Four vectors remain rejected/unresolved: both credential rotations, RabbitMQ pressure and RabbitMQ missing bindings. The live-test and final evidence tasks stay unchecked; no formal review or completion is claimed.

- `python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py` — positive packet and mutation cases pass.
- `python3 tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock.json --brokers rabbitmq,kafka --output /tmp/story-3-21-qualification` — owned local execution only, complete receipts and cleanup.
- `python3 tools/validate-dapr-broker-qualification.py /tmp/story-3-21-qualification --require-production-authority` — expected FAIL until real owner records exist.
- `git diff --check` and retained workspace/workload comparison — no introduced whitespace faults or unrelated changes.
