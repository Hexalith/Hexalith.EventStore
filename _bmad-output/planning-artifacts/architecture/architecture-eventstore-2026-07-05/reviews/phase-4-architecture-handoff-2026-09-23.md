---
title: Phase 4 architecture reconciliation and AD-26 owner decision
date: 2026-09-23
status: pending-owner-ratification
architecture_sha256: ba513f4d5e19e292b7b061a4e70235fde88316f8424588387f7952baea68aad8
readiness: blocked
---

# Phase 4 architecture handoff

## Decision required from the Architecture and Platform deployment owners

**AD-26 is not ratified.** The decision subject is `_bmad-output/planning-artifacts/architecture.md` at SHA-256 `ba513f4d5e19e292b7b061a4e70235fde88316f8424588387f7952baea68aad8`, with AD-26 still marked `[ASSUMPTION]` and the spine `status: draft`. The Architecture owner and Platform deployment owner must jointly record one of these outcomes:

| Outcome | Exact owner decision |
| --- | --- |
| Ratify the target | Accept self-managed Kubernetes with independent DAPR sidecars, `statestore` on stable `state.postgresql` v1 with `actorStateStore: true`, OQ8 `oq8-postgresql-v1`, AD-24 OpenBao, a separately selected durable broker, and the canonical single-profile inventory at `deploy/dapr/production-profile.yaml` as the *design target*. Ratification alone grants no release, deployment, production traffic, readiness, or consumer removal. |
| Approve a replacement | Name the replacement deployment target, actor state provider/version and append-fence proof, OQ8 profile, broker, secret boundary, canonical profile artifact and inventory rule, migration/compatibility impact, and the AD-11 publication-authority implications. Amend AD-26 through an approved architecture change before repinning downstream artifacts. |
| Withhold the decision | Keep AD-26 `[ASSUMPTION]`, the spine draft, and production/readiness gates blocked; identify the evidence needed for a later decision. |

The owner record must identify both deciding owners, the selected outcome, this exact architecture digest or a later reviewed digest, decision time, evidence considered, Security/Release/Test review dispositions and any dissent, and the exact profile identity. If the profile file is still absent, say so; do not invent a digest. A later edit to the decision subject requires a new review. No owner decision is recorded by this handoff.

**Evidence and limits for that decision.** `deploy/dapr/statestore-postgresql.yaml` provides a PostgreSQL v1 target template; local AppHost still uses Redis, and the 2026-09-09 validation report records the provider/append-race danger. [DAPR's PostgreSQL component reference](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/) confirms v1 remains available and v2 has no compatible in-place migration. `deploy/dapr/production-profile.yaml`, the production broker selection, exact production DAPR runtime pin, publication-authority validator/record, and provider-level NFR7 append fence are absent or unproven. The selected target is therefore reviewable as a design choice only. Production proof remains governed by AD-26 and the PRD mandatory gates.

## Exact architecture edits

| Location | Change and boundary |
| --- | --- |
| Frontmatter | Dated the spine 2026-09-23, added the approved change proposal as a source, and updated the official DAPR release source to v1.18.4. `status: draft` is unchanged. |
| AD-10 | Expanded the versioned ServiceDefaults JWT contract/fingerprint inventory to EventStore, Admin Server Host, Admin UI, Sample API, Sample Blazor UI, both Tenants hosts, runnable generated REST hosts, and future externally reachable or JWT-binding application hosts. Missing host, local validator, absent inbound auth, and fingerprint mismatch fail conformance/readiness; AD-28 remains the distinct app-channel scheme. This is design ownership, not proof of adoption. |
| AD-11 | Added FR36 binding and the exact five ordered publication states: `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted`. Candidate and Story 3.15 evidence cannot authorize later states. Separate authenticated release/deployment records bind schema, subject/source, Story 3.14 packet, package/OCI, Story 3.15 validator/result/receipts, predecessor, issuer authentication evidence, role, validity, and profile/deployment identity; a changed subject restarts at `built`. A validated OCI index is necessary artifact identity, not production permission. |
| AD-22 | Required the same subject's source/package, runtime, release, and profile-specific promotion outcomes, complete consumer universe, passing consumer-manifest validator, and separate Consumer-owner receipt before removal. |
| AD-26 | Added FR36 binding and a single canonical production-profile inventory slot. Only a validator-computed digest of retained `deploy/dapr/production-profile.yaml` bytes can authorize that profile. The file is absent, so there is currently no authorizing profile. Candidate and evidence-validation states confer no release/promotion authority. The AD remains `[ASSUMPTION]` pending the owner choice above. |
| Stack and production gates | Refreshed the dated repository-value seed from current `global.json` and the root-declared Builds gitlink without changing dependencies. Added explicit JWT all-host, publication-authority, and AD-26 ratification blockers. |
| Memlog | Appended the proposal, constraints, decisions, repository-version observation, owner question, and review corrections. Earlier entries and review files remain intact. |

## Outstanding finding disposition

The [2026-09-09 validation report](validate-report-2026-09-09.md) recorded two critical and eleven high architecture clusters. Its subsequent [rubric](review-update-2026-09-09-rubric-closure.md), [technology](review-update-2026-09-09-technology-closure.md), and [adversarial](review-update-2026-09-09-adversarial-closure.md) closure reviews resolved their design wording, while explicitly leaving implementation proof open. This update rechecked their current AD mapping: C1 is safely gated but AD-26 still needs the owner decision; C2 is bound by AD-27; H1-H11 have current AD-5/7/8/15/16/19/24/25/28/29/31/32/33 or dated Stack dispositions. The 2026-09-09 reconciliation findings R1-R8 are reflected in the current spine; R9 remains open by design because AD-26 is `[ASSUMPTION]`. Historical reports retain their original verdicts and snapshot line numbers; their closure is not runtime evidence.

The independent 2026-09-23 [rubric](review-update-2026-09-23-rubric-walker.md), [technology/reality](review-update-2026-09-23-technology-reality.md), and [adversarial](review-update-2026-09-23-adversarial-divergence.md) reviews pass the saved architecture wording. They found and closed gaps during this edit: the second Tenants JWT host, Admin UI and Sample UI inventory, runnable generated-host proof, stale Stack values, future external-host coverage, exact publication-record fields, and complete consumer-manifest validation. Their PASS verdicts do not ratify AD-26 or change any readiness gate.

## Remaining blockers and ordered handoff

1. **Architecture decision:** The Architecture and Platform deployment owners must explicitly ratify AD-26 or approve its replacement against the exact subject above, with Security/Release/Test review and dissent recorded. Until then, keep the spine draft and AD-26 `[ASSUMPTION]`.
2. **JWT implementation/proof:** Tenants API still configures JWT locally; Admin UI has a local bearer registration; Sample Blazor UI has no inbound scheme; a runnable generated-host JWT fixture and all-host fingerprint/negative conformance gate are absent. PRD G-AUTH-HOSTS and NFR3 remain failed. Any Tenants source change needs its own repository authority; this architecture edit changes no submodule code.
3. **Publication authority:** The PRD still names historical `aafe9040…` as the current Story 3.15 subject and authority path, while the approved proposal records a later `7d64f87e…` technical result. The Release/PRD owners must approve a current-subject authority record and validator; receipts cannot be moved across subjects. The production profile file, authority record, and `tools/validate-publication-authority.py` do not exist. G-PUBLICATION-AUTH and G-CONSUMER remain failed.
4. **Production proof:** The broker, exact runtime pin, profile digest, two-host/shared-backend evidence, append fence, OpenBao deployment, restore posture, Operations wiring, and other AD-26 production gates remain unproven. No production promotion, traffic, migration, or readiness claim follows from this architecture update.
5. **Planning/readiness:** Complete the proposal's UX, PRD, epics/ownership, lifecycle, and one-manifest approval steps before any epics digest renewal. G-BASELINE and G-READINESS remain failed/blocked. Do not regenerate `sprint-status.yaml` or reuse the historical 2026-08-01 `READY` report.

## Validation and preservation

- `uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` returned `ok: true`, `total_findings: 0`.
- Three independent reviewer lenses above returned PASS for the architecture wording. The architecture SHA-256 recorded at the top binds the reviewed subject.
- `git diff --check` returned no whitespace errors. No runtime build/test or readiness validator is claimed by this documentation update.
- The two pre-existing dirty files, `_bmad-output/implementation-artifacts/deferred-work.md` and `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`, were left untouched. Earlier reports, sealed packets, and evidence trees were not edited. The approved proposal remains an untracked user input; it was read and cited, not rewritten.
