# Validation Orientation Extract — 2026-09-10

## Product, Stakes, And Shape

Hexalith.EventStore Phase 4 is a high-stakes brownfield developer-platform and operations-hardening program. The product surface spans .NET libraries, DAPR/Aspire-hosted services, generated external REST hosts, Admin/Sample/Tenants UI integrations, release workflows, and retained production evidence. The PRD's central decision is conjunctive: domain authors gain reusable platform seams only together with fail-closed security, tenant isolation, event correctness, bounded recovery, honest operational surfaces, and reproducible release proof. Because the document governs readiness, release, deployment, and consumer migration, it should be validated as a chain-top launch artifact rather than as an internal feature brief.

## Authority And Baseline

- `_bmad-output/planning-artifacts/prd.md` owns FR/NFR intent, MVP scope, success criteria, and traceability. It is `document_status: final` but explicitly `implementation_readiness_status: blocked` / `implementation_readiness_result: reject`.
- The bound assessment is the 2026-09-09 `Poor` / `Reject` report at repository baseline `1b6f08d41de040615d3b08675d98e46cfa5bab0c`. Current committed `HEAD` is `293c69c42d35dee26682d42f05c943c0b65786f4`, fourteen commits later. The old result remains the last authority but cannot establish the current tree's readiness.
- `architecture.md` owns technical decisions and production gates; its current frontmatter is `status: draft`. `epics.md` owns slicing, sequencing, and story acceptance. `sprint-status.yaml` and story/spec artifacts are lifecycle evidence, not substitutes for requirement or architecture approval.
- The PRD lists 61 provenance artifacts. Since the bound baseline, only two listed inputs changed: `epics.md` and the bound validation report itself. The newer architecture-condensation proposal and subsequent implementation/evidence changes are not PRD source-list entries; list membership would not by itself grant approval in any case.
- OQ8's normative design remains owned by external Hexalith.Folders at the PRD-bound repository/path/commit/digest. EventStore still does not retain bytes that reproduce that identity, so OR11 remains open regardless of local Story 4.15 packet status.

## Material Current-Source Conflicts

### 1. The epic input hashes were refreshed without the approval sequence OR14 requires

`epics.md` now records the exact current PRD digest `b99effdb...` and architecture digest `7e3dbc7b...`. Commit `0994c378` changed those frontmatter values without reconciling epic content, while the architecture it accepts is still `draft` and contains unratified AD-26 `[ASSUMPTION]`. This is the hash-only refresh that PRD OR14 explicitly forbids. The same epic header is already stale again for UX inputs: recorded/current DESIGN digests are `76be2697...` / `3f4f0181...`, and recorded/current EXPERIENCE digests are `06de15b3...` / `11f75403...`. Matching PRD/architecture hashes therefore do not prove one reviewed and renewed planning baseline.

### 2. Architecture was materially reconciled, then deliberately returned to draft

Post-baseline architecture work added or tightened AD-26 through AD-33 and repaired security, routing, OQ8, release, and production-profile rules. Its memlog first says the update finalized, then records that the shipped condensation had not preserved load-bearing AD-1 through AD-25 clauses, that the accompanying reviews still said `CHANGES REQUIRED`/`FAIL`, and that frontmatter was returned to `status: draft`; AD-26 was also relabeled `[ASSUMPTION]` because owner acceptance was never granted. The current spine is useful fail-closed input, but it does not close PRD OR14 or authorize the epic plan's renewed-approval claim.

### 3. Story 5.3 changed after validation and immediately recreated lifecycle drift

At committed `HEAD`, `spec-5-3-production-authentication-guards-and-secret-stripping.md` is `status: done`, review loop 8, while `sprint-status.yaml` remains `in-progress`; `epics.md` and the PRD's retired OR2 narrative still say the spec is in progress at review loop 3 with six items open and forbid NFR3/NFR4 delivery claims. The live worktree contains an in-flight tracker edit to `done`, but `epics.md` and the PRD remain unreconciled. Story 5.3 therefore cannot yet be used as coherent NFR3/NFR4 closure evidence, and it demonstrates that the missing OR8/OR13 lifecycle and correctness guards are still consequential.

### 4. Story 4.15's local completion packet remains non-authorizing and live validation is red

Current sources disagree deliberately: the Story 4.15 spec says `done`; the tracker says `review`; the v3 selector/handoff says `eventStorePlatformComplete: true` but denies release, deployment, runtime-pin, consumer-migration, external-repository, and Folders-final-closure authority. Running `python3 tools/validate-oq8-platform-evidence.py` against the current tree exits 1 with `Reviewed public document body drift: docs/guides/configuration-reference.md`. This preserves the PRD's fail-closed SM11/OR15 posture: NFR7 class (e) is not closed for readiness, and the external normative-byte problem in OR11 is independently unresolved.

### 5. The append-loss blocker did not materially move

No post-baseline committed file touching the aggregate/event append or state-store fencing path was found. Architecture's production-gate table still says provider-portable append fencing/write-once protection is absent, and the known `same-key-overwrite-raw-durable-write-lost` outcome remains the governing evidence. PRD OR4 and NFR7 class (c) therefore remain blocking; Story 4.15 and authentication progress do not affect this condition.

## Orientation Verdict

The 2026-09-09 PRD correction improved the document's truthfulness, and post-baseline work advanced architecture and authentication evidence. It did not produce one approved current planning baseline or close the principal safety gates. Reviewer passes should treat OR4, OR8, OR11, OR13, OR14, and OR15 as demonstrably live, re-evaluate Story 5.3 against current committed evidence, and avoid interpreting matching epic digests or local `done` frontmatter as readiness authority.
