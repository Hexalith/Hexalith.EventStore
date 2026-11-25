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

Continue the deferred-work batch after DW-131, using severity first and ascending ID within each severity. Complete six actionable items, verify each one, and include each ledger closure in its own Conventional Commit. Run the full build and test suite, then push the verified commits to `main`.

- DW-138 exercises the pinned commitlint process for valid messages and rejected subject casing and line lengths.
- DW-139 enforces the documented lowercase first character for a conventional commit description.
- DW-141 checks the default Release version properties against the pinned shared catalog.
- DW-142 corrects the baseline recorded in the Story 4.8 artifact.
- DW-143 adds the missing details and reason codes to the two idempotency examples.
- DW-144 checks catalog examples against actual gateway exception-handler responses.

</frozen-after-approval>

## Implementation Notes

- DW-132 refers to Story 1.20 proof-packet validators retired by `6a0b6441f`.
- DW-133 records a historical mixed commit and calls for preserving published history. Later changes synchronized the shared entry points.
- DW-136 concerns superseded dependency pins. The proposed CI lane belongs to the shared CI/CD owner.
- DW-139 retains the conventional preset options and applies the lowercase policy in the parser used by both the local CLI and the shared pull request title check.
- DW-141 uses the pinned catalog default so the assertion remains valid when the catalog moves. The focused run passed with competing version environment variables set.
- DW-144 retains the existing primary example and adds optional JSON examples for problem types shared by multiple handlers or outcomes. The tests check the registered handler order, HTTP status and response fields. They use a representative response per wire outcome.
- The Phase 4 completion writer reported `STORY_IDENTITY_MISSING` because this batch tracks numbered deferred items. The deferred ledger records each item closure.

## Review Triage Log

- Handler order, medium, fixed. The parity test follows the registered chain and checks which handler returns the response.
- Validation message, medium, fixed. The example now matches a failure produced by the actual request validator.
- Query response variants, medium, fixed. The parity test exercises 400, 403, 500 and 501 responses.
- Admission variants, medium, fixed. The catalog shows the collision, unsafe legacy state, reconciliation unavailable and owner deferral codes alongside the previously covered outcomes.
- Replay 404 example, medium, fixed. The catalog shows both missing command replay and missing projection query examples.
- Retry guidance, low, fixed. A 503 caller uses `Retry-After` when the handler provides it.
- JSON variants, medium, fixed. The HTTP catalog test checks that the 400 and 409 admission examples are present in the serialized response.
- Parser dependency layout, low. A clean install from the tracked npm lockfile resolved the parser, and all ten process and title checks passed. The project and shared validator use npm's default installation layout.
