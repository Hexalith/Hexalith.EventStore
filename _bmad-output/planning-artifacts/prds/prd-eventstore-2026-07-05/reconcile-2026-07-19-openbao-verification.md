# Reconciliation Verification: OpenBao-Backed DAPR Secret Store (2026-07-19)

Verifier: automated PRD reconciliation check, 2026-07-19.
Verdict: **faithfully-applied** (no PRD-scoped gaps; two non-blocking residual observations).

## Input

- Proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19-openbao-secret-store.md`
  (status: final, approved 2026-07-19; targets FR34/NFR4/NFR17, Epic 7 / Story 7.6, AD-24).
- PRD: `_bmad-output/planning-artifacts/prd.md` (updated: 2026-07-19).
- Applying commit: `fcff0464` "docs(architecture): propose OpenBao secret store"
  (touches `prd.md` +11/-x, `epics.md`, `architecture.md`, the proposal itself, and
  spec-kernel companions).

PRD-scoped approved changes per the proposal's own artifact table (section 2.2) and
section 4.2: (1) make FR34 OpenBao/DAPR-specific, (2) replace NFR17, (3) leave NFR4
wording unchanged, (4) add Story 7.6 to the NFR4 and NFR17 traceability rows,
(5) keep FR34 assigned to Epic 7. Everything else in the proposal (Story 7.6 rewrite,
AD-24 amendments, implementation boundary, verification lanes, documentation) is
owned by `epics.md` / `architecture.md` / downstream artifacts.

## What The Loop Applied (prd.md hunks in fcff0464)

1. Frontmatter: `updated: 2026-07-18` -> `2026-07-19`; appended
   `sprint-change-proposal-2026-07-19-openbao-secret-store.md` to `source_artifacts`.
2. FR34 (section 6.7 table): replaced "add secret-store-backed configuration" with the
   OpenBao/DAPR Secrets API/Kubernetes-bootstrap-restriction wording.
3. NFR17 (section 7 table): replaced the provider-neutral sentence with the canonical
   `openbao` component / `secretKeyRef` + `auth.secretStore: openbao` / DAPR Secrets
   API / default-deny / bootstrap-exception wording, retaining the app-health,
   readiness, resiliency, immutable-tag, and crypto-shred clauses.
4. Traceability 11.2: `NFR4 | 5.3, 7.3` -> `5.3, 7.3, 7.6`; `NFR17 | 5.6, 7.3` ->
   `5.6, 7.3, 7.6`.

No other PRD content was modified.

## Coverage Check Per Approved Item

| # | Approved PRD change (proposal 4.2) | Status | Where in prd.md |
| --- | --- | --- | --- |
| 1 | FR34 phrase replacement: OpenBao-backed DAPR secret-store configuration; application retrieval through the DAPR Secrets API; Kubernetes Secrets restricted to documented bootstrap credentials only when no approved mounted/projected mechanism is available; remainder of FR34 unchanged | APPLIED | Line 225 (section 6.7 FR table). Surrounding FR34 clauses (delivery semantics, poison handling, dedup bounds, admin claims/audit/hiding, readiness/app-health, IntegrationTests CI) are byte-identical to the pre-change text. |
| 2 | NFR17 full replacement (canonical DAPR `openbao` component; dependent components use `secretKeyRef` with `auth.secretStore: openbao`; application code uses DAPR Secrets API; per-application default-deny; bootstrap credentials are platform inputs with the narrow Kubernetes-Secret exception; retain app-health/readiness/resiliency/immutable-tags/crypto-shred) | APPLIED, verbatim match to proposal NEW text | Line 270 (section 7 NFR table). |
| 3 | NFR4 wording remains unchanged | CONFIRMED unchanged | Line 257: "Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets." No diff hunk touches it. |
| 4 | Add Story 7.6 to NFR4 traceability | APPLIED | Line 406: `NFR4 | 5.3, 7.3, 7.6`. |
| 5 | Add Story 7.6 to NFR17 traceability | APPLIED | Line 416: `NFR17 | 5.6, 7.3, 7.6`. |
| 6 | Keep FR34 assigned to Epic 7 | CONFIRMED | Line 394: `FR34 | Epic 7 - Delivery, admin, deploy, and IntegrationTests recovery` (untouched). |
| 7 | Proposal recorded in frontmatter `source_artifacts` | CONFIRMED | Frontmatter line 43; `updated: 2026-07-19` line 5. |

Downstream artifacts (context only, not PRD-verified here): `epics.md` line 2738 now
carries "### Story 7.6: OpenBao-Backed DAPR Secret Store" and `architecture.md`
contains 17 OpenBao references, so the same commit carried the epic/architecture
sides of the proposal.

## Fidelity Issues

None material.

- Trivial grammatical adaptation, judged faithful: the proposal's replacement phrase
  reads "...DAPR Secrets API, **and** restrict Kubernetes Secrets...". The applied
  FR34 drops that "and" because the replacement sits mid-list in the longer FR34
  sentence (the serial "and" correctly remains before "restore meaningful
  IntegrationTests CI coverage"). Meaning is unchanged.
- No contradictions introduced:
  - NFR17 (OpenBao-backed `secretKeyRef`) is consistent with NFR4 (no committed
    operational secrets) — logical references replace, not add, committed credentials.
  - Section 9 ownership boundary ("Provider/operator root-key custody, production
    credentials, KMS/HSM/secret-store service operation, and environment policy remain
    operational responsibilities") matches the proposal's platform-overlay ownership
    (endpoint, TLS trust, token projection, policies, values) and its explicit
    out-of-scope list (production OpenBao HA/lifecycle, KEK/HSM custody).
  - FR26 ("strip committed admin secrets") and NFR1/NFR3 fail-closed posture remain
    aligned with the new fail-closed secret wording.
  - No stale "secret-store-backed configuration" or provider-neutral "must support
    secret stores" phrasing remains anywhere in prd.md.

## Remaining Gaps

PRD-scoped gaps against the approved proposal: **none**. All five approved PRD edits
are present and faithful.

Residual observations (non-blocking; not required by the proposal, recorded for a
future editorial pass):

1. Section 6.7 done-evidence (line 228) was not extended: it still cites only admin
   `501`/audit/persisted-state/backlog evidence and says nothing about the new
   OpenBao-specific FR34 clauses (e.g., a real DAPR-to-OpenBao read, default-deny
   scope evidence). The proposal's prd.md row scoped only FR34/NFR17/traceability, and
   the verification evidence contract lives in Story 7.6 AC7 and section 5.4 success
   criteria (epics/architecture-owned), so this is an optional tightening, not a
   misapplication.
2. Explicit containment of the auto-provisioned default `kubernetes` DAPR secret store
   (`defaultAccess: deny` on that store as well as `openbao`) exists only as an AD-24
   amendment in architecture.md. NFR17's "per-application access must be default-deny"
   plus the bootstrap-only Kubernetes-Secret restriction carry the requirement-level
   intent; the store-by-store mechanics are architecture-scoped per the proposal's own
   allocation.

Explicitly checked and NOT gaps:

- Crypto-shred boundaries: retained verbatim in the applied NFR17 tail.
- Dev/local posture (pinned dev OpenBao container, non-production dev mode, dev-only
  application fallback): proposal places these in AD-24 amendments and Story 7.6
  AC1/AC2 — downstream mechanics, deliberately not PRD text.
- Migration constraints (credential-bearing `{env:...}` placeholders -> `secretKeyRef`
  in committed production components): requirement-level intent is covered by NFR17
  sentence 2 plus NFR4; the exact placeholder prohibition is Story 7.6 AC3
  (epics-owned).
