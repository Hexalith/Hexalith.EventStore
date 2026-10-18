# Epic 9 Context: Phase 4 Gate Decisions Are Machine-Enforced And Approved At A Declared Assurance Level

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make Phase 4 governance executable: corrective changes must prove their narrow authorization, high-risk results must carry verified assurance, and baseline, clause, and MVP coverage decisions must derive from current content-bound evidence. This is MVP governance work independent of the post-MVP payload-protection engine. Completing these tools does not grant readiness, release, deployment, migration, production, or consumer-removal authority; the current readiness verdict remains blocked/reject until every mandatory gate passes against one approved source identity.

## Stories

- Story 9.1: Corrective-Work Authorization Record And Validator
- Story 9.2: Phase 4 High-Risk Gate Matrix And Assurance Control
- Story 9.3: Planning Baseline Manifest And Drift Guard
- Story 9.4: Stable Clause Ledger And All-Clauses Validator
- Story 9.5: Phase 4 MVP Coverage Manifest And Validator

## Requirements & Constraints

- A corrective authorization binds schema/version, failed gate, PRD digest, baseline commit, input-subject digest, deterministic output derivation, authorizing owner and role registry, allowed paths/mutation classes, prohibited paths/actions, expected evidence and exact validator, issuance, expiry, and revocation. `corrective-work-only` is true; every readiness/release/deployment/production/migration/removal flag is false. Missing, expired, revoked, mismatched, overbroad, or out-of-path authority fails before handoff. Postflight covers the resulting diff and evidence and binds the derived output-subject digest; later output changes require new authority.
- Gate classifications cover exactly G-BASELINE, G-OQ8, G-TENANT, G-STATUS-ID, G-APPEND, G-CLAUSE, G-NFR8, G-COMPAT, G-AUTH-HOSTS, G-RUNTIME-PARITY, G-PUBLICATION-AUTH, G-CONSUMER, G-HIGH-RISK, G-NFR18, G-NFR-OWNERSHIP, G-MVP-COVERAGE, and G-READINESS. Each is high-risk or has a reason-coded standard-control classification. Classification cannot omit or waive a gate.
- Baseline approval requires substantive reconciliation of requirements, ownership, statuses, lifecycle values, and corrective authorizations across PRD, architecture, detailed UX, top-level UX, epics, tracker, story records, and evidence. Matching hashes alone cannot establish reconciliation or approval. Readiness rejects active, invalid, or unreconciled corrective authorizations.
- Every stable clause has exactly one primary slice and an evidence identity, pending until passing evidence exists. A parent requirement closes only when all its clauses have passing content-bound evidence; story status cannot substitute.
- MVP coverage is total over FR1–FR36, NFR1–NFR18, all stable clauses, and the `interactive_ui_hosts`, `nfr1_surfaces`, and `high_tier_evidence_cases` inventories. Each binds owner, lifecycle, evidence, validator/result, approval, and blocking gate. Omissions, duplicate ownership, unsupported done, stale/failed/unapproved evidence, incomplete denominators, and MVP N/A fail. FR37/NFR19 do not count toward MVP.
- Every rejection path needs a checked-in negative fixture observed failing alongside a positive control. Validators must report existing failures truthfully; implementation completion must not manufacture approval or refresh digests merely to pass a live gate.

## Technical Decisions

- Authorization records live under `_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations/<gate>/<authorization-id>.json`. The handoff command is `python3 tools/validate-corrective-work-authorization.py <record> --changed-paths-from <baseline-sha>`.
- Assurance derives from authenticated human identities in the owner-role registry: one human requires `single-maintainer-attested`; two or more require `independent`. Aliased roles, tool personas, and CI accounts cannot create human independence. Attestations require at least 24 hours after the subject's last authored change. Downstream records carry the lowest input assurance level.
- Required fixture jobs block merges. Separate live evaluation runs on every push to `main`, publishes retrievable content-bound PASS/FAIL, and does not block merges. Use a new workflow or the Story 9.1 workflow; editing `.github/workflows/ci.yml` or `docs/ci.md` requires a planned Story 4.15 reseal.
- Live results are evidence, never seals. A guarded transition requires its dedicated authenticated CI run on the exact head SHA and workflow digest, bound to that transition and subject/evidence identities. The consuming validator enforces the seal; branch protection is insufficient. Predecessor validation is re-run within the consuming sealed run using the validator retrieved from its pinned commit. Owner records cannot seal themselves.
- Baseline guards compare content and lifecycle semantics as well as SHA-256 values. Architecture repinning propagates newly applicable AD IDs into affected story constraints. AD-26 ratification has a defined subject digest separate from the whole-file traceability digest; open assumption checks read actual decision/clause tags, not quoted mentions.

## Cross-Story Dependencies

Story 9.1 bootstraps the authorization mechanism: a dated owner authorization naming the validator, its tests/fixtures, schema, and new CI workflow must be recorded in its story before development because the absent validator cannot validate its own initial handoff. Stories 9.2–9.5 require Story 9.1 to pass and a separate authorization for their gate. Story 9.5 also depends on 9.4's clause inventory and 9.2's Assurance Control.

Building baseline guards need not wait for approved planning artifacts; minting an approved manifest does require architecture reviewer closure, AD-26 ratification, resolved UX assumptions/final statuses, and renewed epics. Current top-level UX finality does not reconcile upstream source conflicts or stale epics bindings. Story 3.15's bounded technical evidence is not release or readiness authority; Story 9.2 reopens it only if Assurance Control rejects its evidence.
