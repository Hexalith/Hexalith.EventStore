---
title: 'Story 3.21 Platform broker configuration input'
type: 'feature'
created: '2026-11-11'
status: 'ready-for-dev'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md'
  - '{project-root}/references/Hexalith.Platform/docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform cannot test broker configuration remedies because EventStore's qualification lock binds the harness and component bytes. Platform's November 10 dispositions identify this input restriction as the first dependency; five guarantees remain rejected.

**Approach:** Extend the existing harness with an explicitly selected, bounded Platform configuration input and independent verification. Platform supplies configuration and owns execution; EventStore retains its assertions. This enables remediation experiments without promising fixes or copying the harness.

## Boundaries & Constraints

**Always:** Retain both historical locks, defaults and packet interpretations. Keep runtime/CLI 1.19.1, SDK/components 1.19.0 and existing immutable broker identities. Preserve all 24 vectors, predicates, publication counts, observation bounds, oldest replay, TLS, grants, scopes, durability, confirms, pressure controls, worker custody and full preservation/cleanup checks. Bind exact configuration bytes, declared Platform revision/path, current sources and sanitized effective inputs. Provenance is content verification, not authenticated owner acceptance.

**Never:** Accept arbitrary YAML, commands, paths to executable content, credentials, endpoints or unlisted settings. Change production, dependencies, shared catalogs, sealed workflows, existing authority or historical packets. Introduce a copied harness or descriptor schema. Run infrastructure or CI in this slice, select a broker, grant exceptions or close parent 3.21. Platform execution still requires its accepted scope and runner support.

## I/O & Edge-Case Matrix

| Input / state | Required behavior | Failure handling |
| --- | --- | --- |
| Explicit valid configuration | New profile/schema; all 24 vectors retained | Separate capture, technical and authority results |
| Unknown/duplicate key, invalid type/range, escaping file or forbidden setting | Reject before output creation or resource mutation | Identify field without private values |
| Missing/substituted configuration, sources or effective inputs | Independent verification rejects | Resealing cannot repair mismatch |
| Historical caller/packet or schema downgrade | Preserve historical behavior; reject relabelled new capture | No silent profile selection |
| Rejected guarantee, timeout or missing effect | Retain FAIL/uncertainty | No inferred absence, exception or production PASS |

</frozen-after-approval>

## Code Map

- `tools/qualify-dapr-brokers.py`: reuse `Run.components`, `kafka_config`, lifecycle and scenario execution.
- `tools/dapr_broker_qualification.py`: `validate_profile`, `validate_packet` and unchanged scenario evaluators.
- `tools/dapr_runtime_qualification.py`: `profile_path`, `source_input`; existing trusted historical-input archive pattern.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs`: existing explicit C# entry point.

## Tasks & Acceptance

**Execution:**
- [ ] `deploy/dapr/qualification/public-identities/1.19.1-original-inputs/` — preserve affected pinned source bytes from `c94e6fffaf2d689c94bb288f151a65e10a2fea82` before editing; retain their trusted identity for historical verification.
- [ ] `tools/dapr_platform_broker_configuration.py` — add strict schema-1 parsing and configuration provenance/content checks. Permit only RabbitMQ `reconnectWaitSeconds` in `{1,2,3}`, outbound timeout milliseconds in `{250,500,1000,2000,3000}`, Kafka `consumeRetryInterval` milliseconds in `{100,200,500,1000}` and broker `group.initial.rebalance.delay.ms` in `{0,100,500,1000}`. Missing tuning fields retain defaults. These bounds define experiments, not proven remedies.
- [ ] `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` — add an explicit profile identity with unchanged runtime/broker identities and a complete current source inventory; preserve both prior lock files.
- [ ] `tools/{qualify-dapr-brokers,dapr_broker_qualification,dapr_runtime_qualification}.py` — add `--platform-config` and explicit profile dispatch; retain schema-3 configuration bytes, provenance, rendered-input commitments and execution receipts. Verify effective inputs against requested settings plus only established lab substitutions/scenario mutations. Preserve historical schema-1/2 verification and reject downgrade or mixed identities.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs` — forward optional configuration only with the new profile; retain original invocation and matrix behavior.
- [ ] `tools/tests/test_dapr_platform_broker_configuration.py`, existing qualification tests — exercise actual producer configuration application beside positive, rejection, substitution, downgrade and historical controls, without Docker. Retain custody/preservation regressions and prove schema-3 FAIL adjudication for rejected guarantees.
- [ ] `deploy/dapr/qualification/README.md`, parent spec and `deferred-work.md` — document exact input syntax, provenance limits and Platform execution handoff. Preserve all five rejected guarantees, including RabbitMQ's separate upstream requirements; correct stale mitigation suggestions by reference to Platform's dated dispositions.

**Acceptance Criteria:**
- Given a Platform configuration and the new explicit profile, when admitted and rendered, then only permitted settings differ and every scenario and security invariant remains enforced.
- Given a new capture, when independently verified, then configuration/source/effective-input substitution fails even after artifact hashes are recomputed.
- Given existing callers and retained packets, when the harness or verifier runs, then their original profile interpretation and trusted source checks remain effective.
- Given this enabling slice completes, when parent eligibility is evaluated, then 3.21 stays in progress, no broker is selected and live qualification remains unproven.

## Implementation Notes

- 2026-11-11: The owner selected “approve and stop” for this spec. The intent is approved and frozen; implementation has not started. Resume this file directly with `bmad-build`.

## Spec Change Log

## Review Triage Log

## Design Notes

The owner's November 11 “yes” reopens remediation planning. This first slice removes the input blocker identified by Platform. It does not reopen the closed refresh or superseded ES-DIAG. Platform's unpublished descriptor-v2 design and current Redis-only composition supply no durable-broker execution capability.

## Verification

- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'`
- `python3 -B -m unittest discover -s tools/tests -p test_dapr_platform_broker_configuration.py`
- Build the changed IntegrationTests project in Debug/source mode; run its narrow non-live controls. Retain exact dependency blockers and focused fallback evidence. Do not run the explicit live broker class here.
- `git diff --check`; confirm historical locks, frozen parent intent, sealed files and gitlinks are unchanged. Record commands, counts and source revision here; run output stays outside Git.
