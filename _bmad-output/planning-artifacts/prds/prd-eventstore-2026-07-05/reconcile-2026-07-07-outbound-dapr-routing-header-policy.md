# Reconciliation — 2026-07-07 Outbound DAPR Routing-Header Policy

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md`
- **Verdict:** `not PRD-scoped`

## PRD evidence

- **FR1** requires reusable platform boilerplate to live in EventStore libraries rather than domain/host copies (`prd.md:105`).
- **FR13-FR14** establish the generated external-host and gateway/client boundaries (`prd.md:126-127`).
- **FR28** and **NFR1** establish the fail-closed trust boundary (`prd.md:171`, `221`), while **NFR4** prohibits operational-secret exposure (`prd.md:224`).

## Proposal decisions or requirements not represented

- The precise `dapr-app-id`/`dapr-api-token` remove-then-set algorithm, innermost `DelegatingHandler` ordering, `Hexalith.EventStore.Client` placement, deletion of host-local handlers, structural guardrail tests, Story 2.7, AD-18, and the coordinated Tenants submodule follow-up are intentionally absent.

## Conflicts and scope rationale

- No PRD conflict. The product-level outcomes—platform-owned reusable plumbing, protected trust boundaries, and no secret leakage—already exist. The change selects and enforces a concrete client-to-sidecar transport mechanism, so its normative home is architecture plus a development story, as the proposal itself specifies.
- Audit-only gap: the proposal is absent from `source_artifacts` (`prd.md:7-12`) and `.memlog.md`; `.memlog.md:14` is stale about current FR/NFR counts.

