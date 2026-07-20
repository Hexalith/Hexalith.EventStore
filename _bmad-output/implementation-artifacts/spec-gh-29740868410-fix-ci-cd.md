---
title: 'Fix build-and-test CI: result-payload withholding tests and missing error catalog entry'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
route: 'one-shot'
---

## Fix build-and-test CI: result-payload withholding tests and missing error catalog entry

## Intent

**Problem:** CI run [29740868410](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29740868410/job/88347066900) (`ci / build-and-test`) failed on `main` with 4 red tests. PR #319 (`6945714b`) moved the result-payload-withholding decision in `SubmitCommandHandler` from a post-hoc status-store read to the router-authoritative `CommandProcessingResult.ResultPayloadWithheld` flag, but left 3 unit tests mocking the old behavior, and separately added `ProblemTypeUris.IdempotencyAdmissionFailure` without a matching `ErrorReferenceModel` catalog entry.

**Approach:** Update the 3 stale tests to mock `ResultPayloadWithheld` directly (renaming them to reflect what actually drives the drop), add one new test proving the flag is authoritative even when the tracked status read fails, and add an accurate `idempotency-admission-failure` catalog entry. An adversarial review pass caught that the first version of the catalog entry falsely implied a single status/certain-non-execution when the type URI actually covers 6 codes across 503 and 409 — corrected before commit. Two pre-existing gaps the review also surfaced (no producer-side test for `AggregateActor`'s `ResultPayloadWithheld` formulas; a stale log-message string) were out of scope for this fix and deferred.

## Suggested Review Order

**Result-payload withholding contract (tests)**

- Entry point: shows the router now owns the drop decision; the mocked status only feeds log properties.
  [`SubmitCommandHandlerResultPayloadTests.cs:46`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs#L46)

- New test closing the regression-detection gap: a failed status read must not gate the payload when the router did not withhold it.
  [`SubmitCommandHandlerResultPayloadTests.cs:138`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs#L138)

- `CreateRouter` helper threading the new `resultPayloadWithheld` parameter into the mocked `CommandProcessingResult`.
  [`SubmitCommandHandlerResultPayloadTests.cs:186`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs#L186)

**Error catalog completeness**

- New catalog entry for `idempotency-admission-failure`, worded to admit the 6 codes / two status codes (503, 409) it actually maps to.
  [`ErrorReferenceEndpoints.cs:89`](../../src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs#L89)

**Deferred (not in this change)**

- Two review findings recorded for later: missing producer-side `ResultPayloadWithheld` coverage in `AggregateActor`, and a stale `ResultPayloadDropped` log message.
  [`deferred-work.md`](../../_bmad-output/implementation-artifacts/deferred-work.md)
