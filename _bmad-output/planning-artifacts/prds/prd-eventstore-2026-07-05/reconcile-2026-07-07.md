# Reconciliation — 2026-07-07 CI Version-Pin Correction

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md`
- **Verdict:** `not PRD-scoped`

## PRD evidence

- The governing product constraints are centralized package versions and reproducible package-reference releases (`prd.md:143-145`, `prd.md:229`, `prd.md:247-248`).
- The proposal preserves those constraints and changes only a brittle test assertion; it explicitly reports no production, runtime, or packaging-output change (proposal lines 23-29).

## Decisions or requirements not represented

None at product-requirement level. The exact assertion rename and removal of a hard-coded `2.26.0` expectation are implementation corrections.

## Conflicts

None. The proposal intentionally leaves an architecture dependency-table version as a historical snapshot; the PRD contains no conflicting exact Hexalith package version.

## Rationale

This is a test-maintenance correction that keeps an existing central-versioning requirement enforceable without pinning one transient dependency value in the test.
