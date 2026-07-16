# Reconciliation — 2026-07-07 Generated API Smoke Preflight Re-Home

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-smoke-preflight-rehome.md`
- **Verdict:** `not PRD-scoped`

## PRD evidence

- **FR34** requires restored meaningful IntegrationTests coverage (`prd.md:192`).
- **NFR16** requires higher-tier tests to assert persisted end-state evidence rather than status-only smoke (`prd.md:236`).
- Section 9.1 includes delivery/test recovery in MVP (`prd.md:281`), and section 0 assigns story slicing and sequencing to `epics.md` (`prd.md:21`).

## Proposal decisions or requirements not represented

- Re-keying the old TEST-1 story as Story 3.8, pulling it forward beside Story 3.1, retaining Epic 3 as backlog, consolidating retrospective action items, endpoint/resource-name corrections, and the AC10 completion gate are story sequencing and test-tool implementation details.
- The exact local script, optional Aspire control-plane flag, resource discovery, and preflight output are verification mechanics, not additional product capability.

## Conflicts and scope rationale

- No conflict. The proposal expressly says FR34/NFR16 already cover the evidence requirement and changes only where/how the verification story is tracked.
- Audit-only gap: the proposal is absent from `source_artifacts` (`prd.md:7-12`) and from `.memlog.md`; the memory's FR/NFR-count line is stale (`.memlog.md:14`).

