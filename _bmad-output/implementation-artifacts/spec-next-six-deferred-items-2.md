---
title: 'Next six EventStore deferred items'
type: 'bugfix'
created: '2026-11-25'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete six actionable deferred items in ledger priority order. Verify and commit each item independently, then run the full build and test suite before pushing to main.

- DW-115: Preserve the bounded query response during erasure with unknown lifecycle and no freshness or validator evidence.
- DW-116: Refuse a malformed named projection head and verify that the durable activation retries without another command.
- DW-117: Keep explicit async projection handler registrations and discover remaining implementations.
- DW-118: Separate the projection lifecycle actor contracts into one declaration per file.
- DW-120: Reject blank erase identities and admit fresh erasure only from the known idle phase.
- DW-125: Restore Server.Tests to the documented baseline.

</frozen-after-approval>

## Implementation Notes

Priority follows the previous batch: explicit severity first, then ascending IDs. The [previous six-item batch](spec-next-six-deferred-items.md#skipped-items) records earlier skips and links to the earlier seven-item batch.

DW-121 cites the retired Story 1.20 parity packet. The broader automation concern belongs to the maintained BMAD Build Auto workflow and remains open for a current acceptance contract.

DW-131 replaced DW-117 as the sixth implementation. The guardrail now cites the shared Hexalith LLM baseline in its documentation and failure message.

## Skipped Items

| Item | Reason |
| --- | --- |
| DW-117 | Interface-typed factory registrations conceal the concrete handler type until resolution. A scan-side change can add the same handler twice and fail startup. Preserving factory behavior requires a handler resolution design that can distinguish explicit registrations from discovered ones. Open generic handlers also need to be excluded by that design. |
| DW-121 | The Story 1.20 proof packet was retired. The general automation issue needs a current acceptance contract. |
| DW-126 | Duplicate of DW-115. Its ledger entry points to the same fix. |
| DW-127 | The build failure belongs to a historical commit. Current dependencies already supply the package version. |
| DW-128 | The validator was retired with its proof packet. |
| DW-130 | The shared instruction baseline is owned by Hexalith.AI.Tools. |

## Review Triage Log

- DW-115, patched. Suppress validators when either observation for the selected actor scope shows erasure, including a transition back to idle during the actor call. The controller test exercises a matching conditional request.
- DW-115, scoped. The recorded decision concerns the persisted projection-actor lifecycle overlay. The query reference now states the actor scope.
- DW-116, patched. The ordinary named coordinator refuses the malformed handoff. The activation test verifies persisted deferral and successful retry after the original head is restored.
- DW-117, deferred. The type and instance registration approach would regress factory registrations. Open generic definitions also need filtering.
- DW-126, closed as a duplicate. DW-115 is the implementation owner.

The Phase 4 completion writer returned `STORY_IDENTITY_MISSING` for this numbered deferred-work batch. The numbered ledger records each completion.
