---
title: 'Story 6.5a: Event Contract, Writer, and Migration Evidence Spec'
type: 'feature'
created: '2026-09-27'
status: 'backlog'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify stable event identity, bounded writer admission, actor-owned evidence, and retained-history migration as a focused input to the single Story 6.5 AD-13 artifact. This is specification work, not runtime implementation or approval of Story 6.6.

## Boundaries & Constraints

Preserve immutable stored event bytes, message and sequence identity, current constructors and public package compatibility, actor save/readback authority, and the distinct new-writer and retained-offline V1 evidence branches. Epic 8's production protection engine is not a prerequisite. The existing normative draft remains unapproved until Story 6.5's exact-content human gate passes.

## Tasks & Acceptance

- [ ] Inventory the contract, wire, serializer, append, actor, result, migration, and diagnostic paths that this slice owns.
- [ ] Propose exact canonical metadata and registry descriptors, fingerprint and chain compatibility rules, numeric write/read ceilings, preallocation checks, and V1/V2 writer negotiation.
- [ ] Propose provider-portable actor intent/commit evidence, sidecar/origin and retained-history admission, no-op witness and capsule retirement, and truthful command/publication outcome revisions.
- [ ] Give `BH37-1`, `BH37-2`, `BH37-6`, `BH37-7`, and `BH37-8` explicit accepted or rejected dispositions with cross-links to the proposed normative sections and verification vectors.

**Acceptance Criteria:**

- Given new and retained V1 events and V2 writes, when the proposed contracts are checked, then the exact metadata, source-kind, bounded admission, same-save proof, no-op, failure, cancellation, and retry outcomes are deterministic without rewriting history or trusting caller assertions.
- Given a whole wire result or an ambiguous actor save, when size and evidence are checked, then allocation is bounded before materialization and committed truth is established by authenticated provider readback before any retry or public outcome.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and findings are ready for reconciliation with Stories 6.5b and 6.5c; this child story alone authorizes no runtime work.

## Verification

Review the candidate against the current source inventory and the exact `BH37-*` triage rows. Check positive, corrupt, oversized, stale, ambiguous-save, no-op, cancellation, and mixed-version vectors; keep the AD-13 receipt `UNAPPROVED`.
