# Sprint Change Proposal - 2026-05-21 Admin Deferred Quick Wins

**Project:** Hexalith.EventStore
**Trigger:** Post-DW14 backend gap triage — 3 of 5 Issue 15 operations are implementable without new architecture; 2 connexe write operations on `/snapshots` share the same gap.
**Source evidence:**

- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs:55-93`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:64-115`
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw14-admin-deferred-operations-ux-policy.md` (UI-side baseline, status=done)
- `_bmad-output/planning-artifacts/prd.md` (FR76 storage/compaction/snapshot/backup management)
- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md` (Issue 15 evidence)

**Workflow:** bmad-correct-course, Batch mode
**Status:** Approved by Jerome on 2026-05-21 via Party Mode review follow-up

## 1. Issue Summary

DW14 made the Admin UI honest about five backend-deferred operations on `/snapshots`, `/compaction`, `/backups` (manual snapshot, compaction, backup creation, backup validation, stream export) using the Truth-before-submit pattern. The backend itself still returns `AdminOperationResult(Success=false, ErrorCode="Deferred")` for all five and for two connexe write operations (`SetSnapshotPolicyAsync`, `DeleteSnapshotPolicyAsync`) plus restore and import.

A backend-side triage classifies these write operations into two groups:

**Group A — Implementable without new architecture (3 operations)**

1. **Manual snapshot creation** (`CreateSnapshotAsync`) — mechanism already lives in Story 7-1 `EventStoreAggregate<TState>` snapshot path; only an explicit trigger endpoint plus a minimal job tracking model is missing.
2. **Snapshot policy CRUD** (`SetSnapshotPolicyAsync`, `DeleteSnapshotPolicyAsync`) — read side already lists fixtures from `admin:storage-snapshot-policies:{tenant}`; only the persist + invalidate path is missing.
3. **Stream export** (`ExportStreamAsync`) — Story 22.6 already shipped the stream read APIs and paginated reader. Only a bounded contract (`MaxEvents`, format selector), streaming HTTP output, and audit are missing.

**Group B — Requires new architecture (4 operations)**

4. **Backup creation + validation** — manifest format, transport, streaming engine, crypto-shredding awareness (Story 22.7c). The `AdmitRestoredBackupAsync` security gate already exists and waits on this.
5. **Restore** — depends on backup manifest; touches idempotency, safe namespace, tenant isolation, audit.
6. **Import** — payload validation, idempotency, target namespace, audit rules.
7. **Compaction** — touches the write-once event-keys invariant (CLAUDE.md). Needs a non-destructive compaction model (archival, snapshot-and-replace, or tombstone) chosen by Winston.

This proposal **directly adjusts** the sprint for Group A and **records explicit accepted debt** for Group B. Group B does not block any current backlog item.

## 2. Impact Analysis

### Epic impact

The completed Admin Storage Cluster (`admin-storage-snapshot-compaction-backup-operations` — done) and DW14 (`post-epic-deferred-dw14-admin-deferred-operations-ux-policy` — done) remain valid. The correct routing is **three new post-epic deferred follow-up rows**, not reopening completed epics.

No existing in-progress story is invalidated. DW15 (`post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene` — ready-for-dev) is untouched.

### PRD impact

No PRD scope reduction is required. The three Group A stories are direct backfill of an existing functional requirement:

- **FR76** "The admin tool can manage storage — show growth trends, hot streams, and trigger compaction, snapshot creation, and backup operations" — currently partially satisfied (read paths complete, write paths deferred). Group A completes the snapshot creation slice and the stream export slice. Compaction and backup remain deferred via accepted debt.

The PRD already accepts that the v2 admin baseline includes snapshot policies (per the Maria storyline lines 379-385) so policy CRUD is in scope, not new scope.

### Architecture impact

**No new architecture work for Group A.**

- Story 16 (`post-epic-deferred-dw16`) reuses the existing `EventStoreAggregate<TState>` snapshot mechanism (Story 7-1) and the existing Admin API command routing (Stories 14-1/14-2/14-3).
- Story 17 (`post-epic-deferred-dw17`) reuses the existing `admin:storage-snapshot-policies:{tenant}` state-store key shape (already lists work) and existing snapshot policy read path.
- Story 18 (`post-epic-deferred-dw18`) reuses the Story 22.6 stream read APIs and the Story 22.7d redaction machinery for crypto-shredded payloads.

**Group B explicitly requires architecture work** and is *not* part of this proposal. Recording it as accepted debt (with `owner: architect` and `next-review-date: 2026-08-31`) routes it for future planning without false urgency.

### UX impact

**Two outcomes after Group A lands:**

1. DW14 deferred badges on `/snapshots > Create Snapshot` and `/backups > Export Stream` must be **removed** when DW16 and DW18 land respectively, and the final action labels reverted from `Submit Deferred Request` to their truthful working labels (`Create Snapshot`, `Export`). DW14 already documented this dependency in its Out-of-Scope section.
2. Existing `/snapshots` `Add Policy` / `Delete Policy` flows show an error toast today because the backend returns `Success=false`. DW17 will flip those to success without UI change required — the existing toast logic already handles both outcomes correctly.

**Group B operations (compaction, backup, restore, import) keep the DW14 deferred UX** until their architecture-backed stories land.

### Other artifacts

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — add three `backlog` rows (DW16, DW17, DW18) and one comment block routing this proposal.
- `_bmad-output/implementation-artifacts/deferred-work.md` — add four `[ACCEPTED-DEBT]` entries (Backup+Validation, Restore, Import, Compaction) with `owner: architect`, `next-review-date: 2026-08-31`, citing this proposal.

## 3. Checklist Results

| Checklist item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | DW14 review-to-done transition + backend code triage on `DaprStorageCommandService` / `DaprBackupCommandService`. |
| 1.2 Core problem | [x] Done | Scope/UX gap split — 3 operations implementable without architecture, 4 require architecture. Category: scope clarification + technical investigation. |
| 1.3 Evidence | [x] Done | Direct code references (file:line), DW14 baseline status, PRD FR76, Issue 15 manual evidence. |
| 2.1 Current epic impact | [x] Done | Completed epics remain valid; new post-epic deferred rows route the work. |
| 2.2 Epic-level changes | [x] Done | Three new `backlog` rows in sprint-status.yaml. |
| 2.3 Remaining epics | [x] Done | No conflict with DW15 (ready-for-dev) or any in-progress work. |
| 2.4 New/obsolete epics | [x] Done | No new epic needed. |
| 2.5 Priority changes | [x] Done | DW16 / DW17 / DW18 are sized small (~3-5 days each) and can interleave with DW15 or stand alone. |
| 3.1 PRD conflicts | [x] Done | Direct backfill of FR76; no scope change. |
| 3.2 Architecture conflicts | [x] Done | None for Group A. Group B (compaction, backup, restore, import) explicitly deferred with `owner: architect`. |
| 3.3 UX conflicts | [!] Action-needed | DW16 and DW18 must remove DW14 deferred badges + revert final action labels on their respective pages. Documented as story dependencies. |
| 3.4 Other artifacts | [x] Done | sprint-status.yaml + deferred-work.md updates planned. |
| 4.1 Direct adjustment | [x] Viable | Best path for Group A. Effort: ~3-5 days each. Risk: Low. |
| 4.2 Rollback | [N/A] | Nothing to roll back; DW14 is the agreed UX baseline. |
| 4.3 MVP review | [N/A] | No MVP scope change. |
| 4.4 Selected approach | [x] Done | Direct adjustment for Group A + accepted debt for Group B. |
| 5.1 Issue summary | [x] Done | Section 1 above. |
| 5.2 Epic + artifact impact | [x] Done | Section 2 above. |
| 5.3 Recommended path | [x] Done | Section 4 below. |
| 5.4 MVP impact | [x] Done | No MVP scope change. |
| 5.5 Handoff plan | [x] Done | Section 6 below. |

## 4. Recommended Approach

**Direct Adjustment for Group A + Accepted Debt for Group B.**

### Group A — Create three backend follow-up stories

**4.1 DW16 — Manual snapshot creation backend**

- Story key: `post-epic-deferred-dw16-manual-snapshot-creation-backend`
- Initial status in sprint-status.yaml: `backlog`
- Scope:
  - Implement `DaprStorageCommandService.CreateSnapshotAsync` to invoke the existing EventStore.Server snapshot path on the targeted `(tenant, domain, aggregateId)` at current sequence.
  - Add minimal job tracking via state store key `admin:storage-snapshot-jobs:{tenant}` mirroring the compaction job index shape, with statuses `queued/running/done/failed`.
  - Idempotence: skip when a snapshot for the same `(tenant, domain, aggregateId, currentSequence)` already exists; return the existing job id.
  - Unit + Tier 2 integration tests (Tier 2 covers the state store key + actor invocation path).
  - Remove DW14 deferred badge + revert "Submit Deferred Request" → "Create Snapshot" on `/snapshots > Create Snapshot` once green.
- Out of scope: snapshot scheduling (already covered by Epic 7-1 auto-snapshot policies), cross-tenant fan-out.
- Effort: 3-5 days. Risk: Low. No new architecture.

**4.2 DW17 — Snapshot policy CRUD backend**

- Story key: `post-epic-deferred-dw17-snapshot-policy-crud-backend`
- Initial status in sprint-status.yaml: `backlog`
- Scope:
  - Implement `DaprStorageCommandService.SetSnapshotPolicyAsync` (create or update) and `DeleteSnapshotPolicyAsync` to persist into the existing state store key `admin:storage-snapshot-policies:{tenant}` (read path already operational).
  - Wire the policy into the Epic 7-1 auto-snapshot engine (verify the policy is actually consulted at command processing time).
  - Validate `intervalEvents` boundary (min 1, sensible max like 100000).
  - Unit + Tier 2 integration tests (write a policy → read back via `GetSnapshotPoliciesAsync` → confirm round-trip and TTL/etag semantics).
  - No UI change required — existing `OnCreatePolicyConfirm` / `OnDeletePolicyConfirm` handle success and error paths correctly.
- Out of scope: per-aggregate (vs per-aggregate-type) policies, scheduled policies.
- Effort: 3-5 days. Risk: Low. No new architecture.

**4.3 DW18 — Stream export backend**

- Story key: `post-epic-deferred-dw18-stream-export-backend`
- Initial status in sprint-status.yaml: `backlog`
- Scope:
  - Implement `DaprBackupCommandService.ExportStreamAsync` to read events for `(tenant, domain, aggregateId)` via the Story 22.6 stream read APIs, paginated.
  - Fix `MaxEvents = 50000` (the value already documented in the DW14 UI copy) as a configurable constant. Document the limit in `docs/reference/`.
  - Support format `JSON` (default) and `CloudEvents` via the existing `_exportFormat` UI selector.
  - Apply Story 22.7d redaction to crypto-shredded payloads — fail the export with explicit `RedactionRequired` error code (not a silent leak).
  - Stream the response (`IAsyncEnumerable<byte[]>` or chunked HTTP) so a 50k-event export does not hold the full payload in memory.
  - Audit log entry per export: who, what, when, count.
  - Unit + Tier 2 integration tests (Tier 2 covers the stream read APIs + redaction + size limit).
  - Remove DW14 deferred badge + revert "Submit Deferred Request" → "Export" on `/backups > Export Stream` once green. Re-enable the `blazorDownloadFile` call in `OnExportConfirm`.
- Out of scope: cross-stream export, time-bounded export.
- Effort: 3-5 days. Risk: Low-Medium (streaming output is the new piece). No new architecture.

### Group B — Record explicit accepted debt

Append to `_bmad-output/implementation-artifacts/deferred-work.md`:

```
## Deferred from: Sprint Change Proposal 2026-05-21 — Admin Deferred Quick Wins (Group B)

- **[ACCEPTED-DEBT] DW14-B1 — Backup creation + validation backend** — owner: architect; next-review-date: 2026-08-31; grouping: post-epic-bucket; rationale: Requires manifest format, streaming engine, transport, and crypto-shredding awareness. `AdmitRestoredBackupAsync` security gate already exists (Story 22.7c) and waits on this. Estimated as 1 architecture story + 2-3 dev stories. evidence: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:64-79`, `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md`.
- **[ACCEPTED-DEBT] DW14-B2 — Restore backend** — owner: architect; next-review-date: 2026-08-31; grouping: post-epic-bucket; rationale: Depends on DW14-B1 backup manifest. Touches idempotency, safe restore namespace, tenant isolation, audit. DW14 UI preserved two-step + acknowledgement + point-in-time + dry-run guards meanwhile. evidence: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:82-89`.
- **[ACCEPTED-DEBT] DW14-B3 — Stream import backend** — owner: architect; next-review-date: 2026-08-31; grouping: post-epic-bucket; rationale: Needs payload validation, idempotency, target namespace, audit. Lighter than restore but still needs Winston touch on the namespace rule. DW14 UI preserved file-size guard + JSON preview + schema validation meanwhile. evidence: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:109-115`.
- **[ACCEPTED-DEBT] DW14-B4 — Compaction backend** — owner: architect; next-review-date: 2026-08-31; grouping: post-epic-bucket; rationale: Touches the write-once event-keys invariant. Requires a non-destructive compaction model (archival, snapshot-and-replace, or tombstone) chosen by Winston, plus product arbitrage on cost/benefit. Likely a 2-3 week design + 2-4 weeks of dev. evidence: `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs:56-62`, CLAUDE.md "Event store keys are write-once".
```

### Why this approach

- **Cheap clarity now**: 3 new sprint rows + 4 accepted-debt entries close the post-DW14 ambiguity in under 10 minutes of paperwork.
- **No false urgency**: Group B keeps the DW14 honest UX, so operators are not blocked or misled. Architecture work happens on its own clock.
- **Continuity with prior CC pattern**: this mirrors the 2026-05-04 deferred-work triage proposal (`sprint-change-proposal-2026-05-04-deferred-work-triage.md`) which spawned DW1-DW6 the same way.
- **FR76 partial → full**: Group A moves the snapshot + export slice of FR76 from partial to full. Compaction + backup slice of FR76 stays partial, explicitly tagged as v2-late or v3.

## 5. Detailed Change Proposals

### 5.1 sprint-status.yaml — three new rows after DW15

Append after the `post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene: ready-for-dev` line:

```yaml
  # Post-DW14 Admin backend deferred Group A quick wins
  # Added by sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md.
  # Three sized-down backend follow-ups that complete the implementable slice of
  # FR76 without new architecture. Group B (backup, validation, restore, import,
  # compaction) is tracked as ACCEPTED-DEBT in deferred-work.md with
  # owner=architect, next-review-date=2026-08-31.
  post-epic-deferred-dw16-manual-snapshot-creation-backend: backlog
  post-epic-deferred-dw17-snapshot-policy-crud-backend: backlog
  post-epic-deferred-dw18-stream-export-backend: backlog
```

And update the `last_updated` header line with a note pointing to this proposal.

### 5.2 deferred-work.md — Group B accepted debt block

Append a new section at the appropriate chronological position (most recent first). Content as shown in Section 4 Group B above.

### 5.3 Story files — not created in this proposal

Per the BMAD convention, story files are created by `bmad-create-story` when work begins, not by Correct Course. This proposal sets the backlog rows and the per-story scope hints; the SM/Dev workflow will produce the full story files (Dev Notes, Tasks, ACs) at story-start time.

## 6. Implementation Handoff

**Change scope: Moderate** — three new backlog rows + four accepted-debt entries + cross-document consistency updates.

| Recipient | Responsibility |
|---|---|
| **PO / SM** | Approve this proposal. Decide the relative ordering of DW15/DW16/DW17/DW18. |
| **Dev (Claude / human)** | When this proposal is approved, apply sprint-status.yaml + deferred-work.md updates. **Do not create story files yet** — those come from `bmad-create-story` when each story is picked up. |
| **Architect (Winston)** | No action needed for Group A. Group B `[ACCEPTED-DEBT]` entries will surface at the `next-review-date: 2026-08-31` cycle for backup/restore/import/compaction design kickoff. |
| **Product (John)** | No action needed for Group A (FR76 backfill, already in scope). Group B review will benefit from John's priority arbitrage at the 2026-08-31 cycle. |

### Success criteria for this Correct Course

1. `sprint-status.yaml` shows DW16, DW17, DW18 as `backlog` after this proposal lands.
2. `deferred-work.md` contains the four `[ACCEPTED-DEBT]` entries with `owner: architect` and `next-review-date: 2026-08-31`.
3. The next `bmad-create-story` run on any of DW16/DW17/DW18 picks up its row and produces a properly contextualized story file.
4. No reopening of completed epics or completed stories (DW14 stays `done`).

### Follow-up triggers

- DW14 UI cleanup will land **inside** each Group A story when it completes (badge removal + label revert). This proposal explicitly assigns that cleanup as a sub-task of each Group A story, not as a separate UI story.
- A future Correct Course (or a sprint-change-proposal) should fire at the `2026-08-31` review date for Group B if any item is still `[ACCEPTED-DEBT]` at that point.
