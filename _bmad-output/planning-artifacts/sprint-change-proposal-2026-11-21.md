---
date: '2026-11-21'
status: applied-to-sprint-plan
mode: batch
change_scope: moderate
source_revision: 147159db98074b7769645ccc4aaaf0099df03326
phase_4_readiness: reject
---

# Sprint change proposal for Phase 4 readiness recovery

The 21 November sprint-planning run stopped before regeneration. Five planning inputs had drifted, several stories still required retired authorization tooling, and BMAD rejected three values in the existing tracker. The user requested a course correction. The edits below apply the existing owner decisions to the plan and tracking tools. The Product owner’s Phase 4 readiness decision remains **Reject**. [PRD §11.4](prd.md#114-phase-4-mvp-exit-decision-contract) · [Architecture handoff](architecture.md)

## Changes applied

| Area | Previous handoff | Current handoff |
| --- | --- | --- |
| Corrective authorization, Stories 9.3 and 9.4 | 9.3 named an ignored evidence file and a deleted registry. 9.4 required the retired 9.1 record, as did dependencies in Epics 2, 3, 5, 6 and 7. | The gate owner records the corrective scope, subject and approval in the owning spec under AD-12 and PRD §0. The 9.3 CI lane will generate the candidate manifest and retain the run output. Its spec will identify the revision, inputs, invocation, digest, findings and run. The original 9.1 story documents the historical mechanism. |
| Requirement ownership, Epics 1 and 9 | Detailed FR7 story declarations disagreed with the clause ledgers. Story 9.4 looked for FR15 in a ledger containing no FR15 clauses. | Stories 1.17, 1.6, 1.15, 1.18 and 1.19 now declare primary ownership of FR7-C1 through C5 respectively. Story 9.4 checks the declarations against the two ledgers and checks FR15’s six slices against PRD §11.1 and the epic completion rule. |
| API and authentication, Epics 2 and 5 | The PRD permitted a caller to see a rejection after command acceptance. Its tenant rule lacked the cataloged platform exception. Story 5.3’s symmetric-key exception could be read to include Production. | An accepted command keeps HTTP 202 and `Retry-After: 1` even when it lacks a usable status link. Tenant routes require one authorized canonical tenant. Platform routes require the cataloged permission and credentials and reject a request tenant. Production rejects symmetric JWT mode for either flag value. |
| Current evidence, Story 4.5 | The spec, tracker and epic carried the August completion while the acceptance criteria required reopening on source drift or unresolved runtime identity. | All three now record `in-progress` for current evidence. Review loop 4’s observed Redis overwrite remains attributable to its August subject. The persistence and test owners must rerun the live-sidecar lane with the current source, provider and runtime identities. Story 4.16 owns prevention. |
| Story 2.14 and high-risk assurance | Passing 5.4.0 consumer runs and the corrected transition fixture left an ambiguous next step in the epic handoff. | The Story 2.14 owner supplies the NFR2 completion adapter. Story 9.2 integrates G-TENANT business evidence, verifies the exact source and consumer subjects, and checks the authenticated CI seal and human attestation. Both the spec and tracker paths must refuse a mismatched subject or missing proof. |
| Sprint tracking | BMAD rejected the original offset timestamp, Story 4.6’s operator hold and a declined retrospective action. A refresh could reset the hold. | The planning reader accepts the offset timestamp, preserves the hold during status merging and identifies the story awaiting a human. Both planning and retrospective tools recognize `rejected` as a closed action. All three installed BMAD entry points carry the correction. |

The changes use the existing stories and their MVP acceptance criteria. Reverting completed implementation would discard useful evidence. Changing MVP scope would leave the authorization and traceability conflicts unresolved. The UX review confirmed the hidden restore/import controls, gated tenant provisioning and the existing `/types` route in the detailed handoff. Stories 5.10, 7.4 and 7.14 retain their respective delivery work. [Canonical epics](epics.md) · [UX handoff](ux.md) · [Story 4.5 evidence](../implementation-artifacts/spec-4-5-append-durability-race-evidence.md) · [Story 2.14 evidence](../implementation-artifacts/spec-2-14-canonical-tenant-boundary.md)

## Inputs for the Story 9.3 review

The table identifies the bytes reviewed in this working tree after the edits. The full earlier hashes remain in the epic frontmatter. Under OR14, reviewers must close the architecture review, ratify AD-26 or approve a replacement, and validate and approve one reconciled source set before renewing those pins. The final UX documents already define their product decisions. [PRD §11.3](prd.md#113-required-follow-on-readiness-work) · [Architecture approval conditions](architecture.md)

| Input | Previous epic pin, abbreviated | SHA-256 reviewed on 21 November |
| --- | --- | --- |
| PRD | `b99effdb4142` | `3806aff36c5880c3b15400efa3f634905b37c72a52cfb86a851cfe0a8fa04807` |
| Architecture | `7e3dbc7bd335` | `bb2c8d20efe7819fdf2935762960884d654b154f35632d97c7c9c69a5fb8c0d7` |
| UX DESIGN | `76be269737dd` | `d29c9db0f164f0c8f27f2b58bd25475489c2539135697d30b638c0964f0d5727` |
| UX EXPERIENCE | `06de15b34922` | `c584c954a7769bb806c59d89ae87696786cd922ebb5867541694959fa4eed5f2` |
| UX handoff | `2927f97d4fe7` | `5dc2d6782105b4d09d79e68c1b557c93e5e2b7cf0939f04f86ffeb68beec5f7f` |

## Verification and remaining work

- **Tracker and traceability.** The sprint validator returned `valid: true` with zero problems. The 145 story keys include 6.5a–6.5d. Story 4.8 is the historical ledger. Only Story 4.5 changed lifecycle. All 56 FR/NFR definitions matched across the PRD and epics, all 49 stable-clause rows agreed on primary and supporting owners, and each FR7 clause matched its detailed primary story. [Sprint tracker](../implementation-artifacts/sprint-status.yaml)
- **BMAD tooling.** All 39 sprint-planning tests passed in each installation. The retrospective suites passed 93 tests in `.agents` and 91 each in `.agent` and `.claude`. The new cases cover preserving the hold across a refresh, showing pending work, comparing offset timestamps and recording a declined action.
- **Completion checks.** The completion guard refuses uncommitted planning inputs with `SOURCE_MISMATCH`. In a disposable checkout containing the same edited planning bytes at its test revision, all 32 completion tests passed. Both Story 2.14 targets returned exit 1 and `ADAPTER_UNAVAILABLE` for NFR2. The governing-design identity check and its 111 fixtures passed. [Completion guard](../../tools/validate-phase-4-story-completion.py)
- **Assurance.** The local matrix command below returned exit 1. All 12 high-risk rows reported `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`. The five standard-control rows passed classification. [Risk matrix](../implementation-artifacts/phase-4-gate-risk-matrix.json)

This is a moderate backlog correction. Allow roughly half an engineer-day for review and verification of the planning and tracker edits. The main scheduling risk is the time to obtain evidence and decisions from multiple owners.

The Product Owner and developers can schedule the existing 9.3 reconciliation and 9.4 clause checks under AD-12. The Test and Product owners must define which inputs can start the first readiness evaluation and how its sealed run is consumed. The final assessment requires 15 business and coverage gates on the same approved baseline, the G-HIGH-RISK decision and then G-READINESS. Each high-risk owner supplies the evidence and approval for their subject. The local planning edits are complete. A reliable readiness date requires the architecture and Platform decisions, qualifying CI runs, and owner attestations. [Story 9.3 and dependent controls](epics.md#story-93-planning-baseline-manifest-and-drift-guard)

Run from the repository root. The story-completion commands are read-only with these arguments.

```sh
uv run .agents/skills/bmad-sprint-planning/scripts/sprint_plan.py validate --status-file _bmad-output/implementation-artifacts/sprint-status.yaml
python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json
python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md --target spec
python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md --target tracker
```
