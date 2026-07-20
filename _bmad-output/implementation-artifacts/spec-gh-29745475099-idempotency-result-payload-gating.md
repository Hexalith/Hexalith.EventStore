---
title: 'Fix idempotency-admission regressions breaking CI (result-payload gating + error catalog)'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
route: 'one-shot'
---

# Fix idempotency-admission regressions breaking CI (result-payload gating + error catalog)

## Intent

**Problem:** GitHub Actions run [29745475099](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29745475099) failed `Unit tests (Tier 1, VSTest)` with 4 failures, all regressions from today's `19465ef8` commit ("feat(tests): add comprehensive unit tests for idempotency and aggregate actor functionalities"): `SubmitCommandHandler.Handle` started gating the returned `ResultPayload` on the actor-local `ResultPayloadWithheld` flag instead of the durable status-store `Completed` check, silently leaking result payloads for commands whose async status hadn't yet confirmed completion (or had failed/errored); and the same commit added `ProblemTypeUris.IdempotencyAdmissionFailure` without a matching entry in `ErrorReferenceEndpoints.ErrorModels`, breaking the catalog-sync guardrail test.

**Approach:** Reverted the gating condition in `SubmitCommandHandler.Handle` back to `finalStatus?.Status == CommandStatus.Completed` (the pre-regression, tested behavior; `ResultPayloadWithheld` remains correctly used only by the separate idempotency-replay path) and added the missing `idempotency-admission-failure` error-reference-model entry, with its description/resolution steps corrected during adversarial review to accurately reflect that the shared Type URI spans both HTTP 503 and HTTP 409 outcomes.

## Suggested Review Order

**Result-payload gating regression (security-relevant)**

- Entry point: the swapped-back gating condition, now with an inline comment pinning the invariant so it isn't swapped a third time.
  [`SubmitCommandHandler.cs:538`](../../src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs#L538)

- Regression tests that caught this and now pass again — they encode the exact contract the gate must satisfy.
  [`SubmitCommandHandlerResultPayloadTests.cs:46`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs#L46)

**Error catalog completeness**

- New entry closing the `ProblemTypeUris.IdempotencyAdmissionFailure` / `ErrorReferenceEndpoints.ErrorModels` sync gap, with description and resolution steps covering both the 503 and 409 outcomes sharing this Type URI.
  [`ErrorReferenceEndpoints.cs:89`](../../src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs#L89)

- Guardrail test that failed CI and now passes, plus the reverse-direction sync check.
  [`ErrorReferenceEndpointTests.cs:76`](../../tests/Hexalith.EventStore.Server.Tests/OpenApi/ErrorReferenceEndpointTests.cs#L76)
