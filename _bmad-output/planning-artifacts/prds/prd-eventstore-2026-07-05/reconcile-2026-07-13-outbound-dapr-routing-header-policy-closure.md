# Reconciliation — 2026-07-13 Outbound DAPR Routing-Header Policy Closure

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13-outbound-dapr-routing-header-policy-closure.md`
- **Verdict:** `not PRD-scoped`

## PRD evidence

- The relevant product trust-boundary intent is already covered by fail-closed security and app-layer credential requirements (`FR28`, `NFR1`; `prd.md:171`, `prd.md:221`) and tenant-safe topology (`FR32`; `prd.md:172`).
- The proposal states that the exact header replacement policy is already implemented under architecture invariant AD-18 and Story 2.7, with no PRD or runtime change (proposal lines 24-35, 74-89).

## Decisions or requirements not represented

None at product-requirement level. Exact `dapr-app-id`/`dapr-api-token` remove-then-set behavior and handler ordering are architecture/transport mechanics already delivered; the proposal only closes stale sprint bookkeeping.

## Conflicts

None with the PRD. The Tenants-local handler remains a separately authorized submodule follow-up, not an EventStore PRD scope change.

## Rationale

This proposal reconciles epic/action status with shipped evidence. Adding the status transition or exact transport algorithm to the PRD would duplicate sprint and architecture ownership.
