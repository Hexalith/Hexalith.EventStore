# Story 3.21 retained local qualification

Story remains **in-progress**. These are local Docker/native-sidecar observations,
not Kubernetes or production-path proof. Neither broker is selected and production
eligibility is **FAIL**. The exact frozen implementation intent remains unchanged.

The latest full C# capture gate is **FAIL** because two unrelated transient
containers disappeared during the run. No surviving container specification
changed, and no owned resource remains. There are **25 passing verifier tests**;
build, workflow lint and whitespace checks pass. Four broker vectors are rejected
or unresolved. The PASS cells below describe bounded scenario observations only.

The runner, immutable candidate components, independent verifier, mutation tests,
explicit C# entry point and manual non-promoting workflow are implemented.
Application effects are written/read through Dapr state.sqlite/v1; file logs are
only receipt/crash diagnostics. Broker administration is separate fault/diagnostic
evidence. The packet binds runtime/CLI/control-plane binaries, OCI indices,
source/component/subscription bytes, actual metadata, commands and state readback.

## Observed matrix

| Scenario | RabbitMQ 4.3.6 | Kafka 4.3.1 |
| --- | --- | --- |
| crash-redelivery | PASS | PASS |
| broker-restart | PASS | PASS |
| durable-quarantine | PASS | PASS |
| failed-diversion | PASS | PASS |
| retention-expiry | PASS | PASS |
| stable-id-deduplication | PASS | PASS |
| app-topic-scopes | PASS | PASS |
| tls-negative | PASS | PASS |
| missing-secret | PASS | PASS |
| secret-rotation | FAIL | FAIL |
| cancellation-backpressure | FAIL | PASS |
| missing-bindings | FAIL | PASS |

A failed row remains rejected or unresolved. A passing capture test only verifies
that every vector executed and retained independently checked evidence. It never
turns a rejected guarantee into a qualification or deployment pass. Kafka retention
uses native earliest-offset movement after closed-segment expiry and a fresh Dapr
consumer group replaying an acknowledged control, with one persisted effect.

RabbitMQ's pressure probe retains every HTTP status and reconciles each acknowledged
ID against Dapr state; the missing-binding probe retains a positive bound control
beside the unbound publish. A stale-credential timeout is unknown outcome and cannot
stand in for explicit denial. See each observation, HTTP receipt, state readback
and broker diagnostic under [packet](packet/packet.json).

The latest RabbitMQ run returned HTTP 204 for four IDs absent from the bounded
persisted-state reconciliation and for an unbound publish. Its old-credential
probe timed out (599). Kafka returned 204 to its stale-credential probe, so that
denial guarantee is also unresolved. Kafka's quota-pressure probe produced bounded
server failures with no acknowledged-ID loss. These observations convey no
production recommendation or durability claim beyond the recorded bounds.

## Verification and preservation

The [checks](verification-summary.json) retain exact commands, UTC windows,
exit codes and output. The C# run is in [live-test.log](live-test.log); its separate
execution and verifier logs are beside the packet. Debug package-mode build passed
with zero warnings/errors. Source-mode Debug build separately hit existing CS1704
for mixed Hexalith.Commons.UniqueIds references; its exact command and diagnostics
remain in the earlier [checkpoint](../implementation-2026-10-20/checkpoint.md) and
[source-mode log](source-mode-build-blocker.log). No dependencies were refreshed.

Owned resources/private material were removed. Unrelated container specification
hashes must match for packet integrity to pass. Two quota/preservation runs were
correctly rejected when unrelated containers disappeared during execution; their
original bytes remain retained. The latest full gate remains FAIL. The
[earlier passing capture run](earlier-valid-local-run/packet.json) is historical
evidence from its own source snapshot and does not qualify the current lane.
Concurrent P1R workspace changes were preserved; no global workspace freeze is
claimed. Protected CI, integration, Builds and Story 2.13 inputs are checked separately.

## Outstanding owner-bound work

[Story 3.17 handoff](story-3-17-handoff.json) is proposed inventory, not an accepted
register. [Guide drift](story-5-9-drift-handoff.json) remains for its owners.
[Production bundle status](production-bundle-status.json) enumerates the remaining
gates and protected inputs. The retained [rollout/rollback plan](../qualification-2026-10-20/rollout-rollback.md)
remains a proposal. No exact production diff/restore bundle can be sealed while
qualification, Kubernetes, owner selection, restore and protected release inputs
are missing. No production resource, Secret, traffic or consumer was changed.

The Build workflow remains in implementation: its [task-verification instruction](../../../../../_bmad/render/bmad-build/eventstore-5ec6a32020fe/70d85eb25b5f587c9387/step-03-implement.md)
says, "If any task is not done or any acceptance criterion is not satisfied, finish
the missing work before proceeding". The invoked [bmad-build skill](../../../../../.agents/skills/bmad-build/SKILL.md)
therefore has not advanced to formal review or story completion. The final evidence
handoff task remains unchecked; no approval or owner identity is inferred.
