---
title: 'Next six EventStore deferred items'
type: 'bugfix'
created: '2026-11-25'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Complete six actionable deferred items in severity order, using ascending item IDs within each severity. Carry forward still-valid skip decisions from the previous batch.

- DW-548: Verify that invalid Sample API authentication configuration rejects startup even when the test host reports disposal during startup.
- DW-90: Exercise distinct message and correlation identities, expired command state, and submit-handler identity guards against committed test state.
- DW-91: Exercise message-primary admin trace lookup, ambiguous correlation, and missing advisory index entries.
- DW-108: Let named handlers return compatibility state with an already-completed result.
- DW-109: Isolate retry work by admitted service binding while retaining usable existing retry records.
- DW-114: Retain projection activation until each required delivery path has durable evidence.

Record each closure with its own validated Conventional Commit. Run the full build and test suite and push to main after validation. Record skips and their reasons.

</frozen-after-approval>

## Implementation Notes

- Severity takes precedence over item number. Items without a severity follow the explicit severities in ascending item order.
- DW-90 now tests both expired record layouts against the checker’s read-only expiry contract. The retained distinct-identity drain test verifies the persisted checkpoint, publication and advisory status.
- DW-109 keeps matching legacy retry rows in place and uses the same shard and aggregate lease for a new binding at the same head. Retry checks use the original app, version and catalog fingerprint. The tests cover persisted rows created by earlier versions and mixed legacy-form and binding-aware scheduling through the updated scheduler.
- DW-114 requires a named handoff for committed plans that declare it. The internal compatibility seam can still fall back to the legacy path when it has no committed named declaration. Activation cleanup requires ETag and checkpoint success for direct legacy writes. Mixed guarded and legacy work pins the activation revision before checking named completion.

## Skipped Items

Still-applicable prior skips are documented in [the previous batch](spec-next-seven-deferred-items.md#skipped-items). The following were evaluated after that batch.

| Item | Reason |
| --- | --- |
| DW-96 | Story 1.9 chose caller-authoritative erase manifests. The public factory is released, and the controller handles the denial outcome. Removing either API requires a compatibility decision. |
| DW-97 | The resumable protocol requires ETags for compare-and-set. No ETag-less store has been qualified. |
| DW-98 | The envelope decoder already validates both base64 payloads and rejects malformed envelopes before readers or reconciliation can use them. |
| DW-99 | Story 1.13 requires resuming the stable batch and preserving a foreign pending batch. Elapsed time alone cannot authorize deletion. |
| DW-111 | Permanent failure, catalog rebinding and terminal cleanup require a retry policy that accounts for transient 4xx responses and retained ownership evidence. |

## Review Triage Log

- Medium, patched. Retain work on a failed ETag request or an unreadable checkpoint, including an empty event stream.
- Medium, patched. Validate the named head identity in both released and direct delivery before allowing activation cleanup.
- Medium, patched. Sample the activation revision before the guarded named proof. A later activation fences legacy intent binding and cleanup.
- Medium, patched. Fence ordinary, streamed and source-guard retry by the original catalog binding. A stale retry defers the original work.
- Medium, patched. Legacy-form and binding-aware scheduling locate the same exact binding in both scheduling orders. Different bindings retain separate work.
- Low, patched. Exercise actor and ETag recovery after a state-bearing handler reports AlreadyCompleted.
- False for the active production entry point. Every production orchestrator has catalog authority and requires a named handoff when its committed plan declares one. The internal compatibility path supports older isolated clients without a committed named plan.
- Superseded. Expiry no longer stages actor mutation. Tests assert that both primary and legacy terminal records remain committed and cannot execute again.
- Intentional behavior. A correlation lookup can fall back to a message-primary lookup by the supplied identifier. The controller returns Unknown when neither path finds status and no stream context was supplied.

The Phase 4 completion writer reported `STORY_IDENTITY_MISSING` for the batch record. The numbered item closures are in the deferred-work ledger.
