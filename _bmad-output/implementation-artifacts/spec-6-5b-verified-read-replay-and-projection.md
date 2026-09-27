---
title: 'Story 6.5b: Verified Read, Replay, and Projection Spec'
type: 'feature'
created: '2026-09-27'
status: 'backlog'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify one authenticated, bounded event-evolution read path for replay, projection, query, reconstruction, backup, and inspection as an input to the single Story 6.5 AD-13 artifact. This story changes no runtime behavior.

## Boundaries & Constraints

Read-time adaptation never rewrites stored bytes, changes message/sequence identity, skips poison history, advances an unearned checkpoint, or publishes partial authoritative state. Protected evidence remains protected. Legacy adapters stay explicitly bounded and source compatible. No independent approval of Story 6.6 is granted.

## Tasks & Acceptance

- [ ] Inventory every raw source, proof, unprotect, upcast, Apply, projection, query, Admin, backup, and snapshot-recovery path in this slice.
- [ ] Propose exact authenticated source and effective-view contracts, alias and hop validation, complete-prefix paging, route scope, named-root and checkpoint proof, timeline semantics, typed failure, and cancellation behavior.
- [ ] Propose compositional memory and byte budgets, successor buffer ownership across async saves, and replay/index retention rules with a safe hold before capacity is exhausted.
- [ ] Give `BH37-3`, `BH37-4`, `BH37-5`, and `BH37-10` explicit accepted or rejected dispositions with cross-links to proposed normative sections and verification vectors.

**Acceptance Criteria:**

- Given one mixed-version prefix, when any supported consumer reads it, then the same registered chain and authenticated source evidence yield the same current domain meaning or the same typed last-good-state failure; adaptation happens once.
- Given paging, cancellation, oversized inputs, stale proofs, or an async successor write, when the design is checked, then no partial success, caller-mutable committed bytes, unbounded allocation, or unearned checkpoint is possible.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and findings are ready for reconciliation with Stories 6.5a and 6.5c; this child story alone authorizes no runtime work.

## Verification

Review the candidate against the current source inventory and the exact `BH37-*` triage rows. Check cross-consumer equivalence, tamper/race, page/timeline, 64 MiB legacy input plus prior state, cancellation, and rollback vectors; keep the AD-13 receipt `UNAPPROVED`.
