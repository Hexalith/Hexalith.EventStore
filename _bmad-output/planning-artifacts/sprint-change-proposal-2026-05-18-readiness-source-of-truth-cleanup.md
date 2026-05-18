---
date: 2026-05-18
project: Hexalith.EventStore
source_report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-18.md
scope: Minor
status: Applied
---

# Sprint Change Proposal: Readiness Source-of-Truth Cleanup

## 1. Issue Summary

The 2026-05-18 implementation readiness assessment found the planning set close to ready, with all required planning documents present and 104/104 PRD functional requirements covered by epics. The remaining blocker was source-of-truth drift across PRD, architecture, UX, and epics.

The highest-priority fixes were:

- SEC-1 still referenced an obsolete 11-field event envelope in architecture, UX, and epics while PRD FR11/FR65 define the current 14-field envelope.
- PRD Phase 2 Admin Web UI still named Fluent UI V4 while the active codebase and Epic 21 establish Fluent UI v5.
- UX guidance contradicted itself on whether "aggregate" may appear in consumer-facing ProblemDetails.
- Completed admin Epics 14-21 rely on linked implementation artifacts, but the handoff rule did not yet make those artifacts mandatory for future follow-up work.

## 2. Impact Analysis

Epic impact: No epic resequencing or new epic is required. Epics 1, 14-21, and 22 remain valid, but their source-of-truth language needed tightening.

Story impact: Future work touching completed admin Epics 14-21 must load the linked `Detail` implementation artifacts before implementation or review. Missing linked evidence blocks readiness for that follow-up.

Artifact conflicts resolved:

- PRD: Admin Web UI dependency now uses Blazor Fluent UI v5 and references Epic 21 as the migration baseline.
- Architecture: SEC-1 now owns all 14 envelope metadata fields and names the metadata/payload two-document storage boundary.
- UX: Automatic envelope ownership now says 14 fields. The 409 ProblemDetails example no longer says "aggregate" in `detail`.
- Epics: SEC-1 and UX-DR6/UX-DR26 now match the architecture and UX policy.

Technical impact: Documentation-only. No code or runtime behavior changed.

## 3. Recommended Approach

Chosen path: Direct Adjustment.

Rationale: The assessment did not identify missing FR coverage or a failed implementation approach. The needed change was targeted artifact cleanup, not rollback or MVP redefinition.

Effort: Low.

Risk: Low. The edits align older wording with the already-current PRD, codebase project context, and Epic 21 migration outcome.

Timeline impact: None.

## 4. Detailed Change Proposals

### PRD

Old: Admin Web UI dependency listed "Blazor Fluent UI V4".

New: Admin Web UI dependency lists "Blazor Fluent UI v5" and names Epic 21 as the established migration baseline.

Rationale: Prevents future admin UI work from selecting obsolete component APIs or redoing migration assumptions.

### Architecture

Old: SEC-1 said EventStore owns all 11 envelope metadata fields and listed obsolete/renamed fields including `domain` and `serializationFormat`.

New: SEC-1 says EventStore owns all 14 fields: `messageId`, `aggregateId`, `aggregateType`, `tenantId`, `sequenceNumber`, `globalPosition`, `timestamp`, `correlationId`, `causationId`, `userId`, `eventType`, `domainServiceVersion`, `metadataVersion`, and `extensions`; metadata and payload persist as separate JSON documents.

Rationale: Aligns the security boundary with PRD FR11/FR65 and current envelope compatibility rules.

### UX

Old: UX said all 11 envelope fields are automatic, prohibited "aggregate" everywhere in ProblemDetails, and also used "aggregate" in the 409 conflict example and shared-terminology rule.

New: UX says all 14 envelope fields are automatic. ProblemDetails `title`, `detail`, and reason text must avoid internal implementation terms; stable API field names such as `aggregateId` may appear only in field paths or extension keys. The 409 example uses reader-friendly prose without "aggregate" in `detail`.

Rationale: Keeps consumer errors understandable while preserving stable API contract field names for validation.

### Epics

Old: Epics repeated SEC-1 as 11 fields and treated "aggregate" as globally forbidden in ProblemDetails, while also using aggregate as shared terminology.

New: Epics repeat the 14-field SEC-1 rule, mirror the refined ProblemDetails policy, and require linked `Detail` artifacts for any future follow-up touching completed Epics 14-21.

Rationale: Makes future story work auditable without expanding the compact historical admin epic summaries inline.

## 5. Implementation Handoff

Scope classification: Minor.

Handoff recipient: Developer agent for direct documentation implementation.

Success criteria:

- No remaining references to "11 event envelope metadata fields" or "11 envelope metadata fields" in PRD, architecture, UX, or epics.
- PRD Admin Web UI dependency says Fluent UI v5.
- UX ProblemDetails policy and examples agree on the allowed use of API field names versus internal implementation terminology.
- Epics handoff rules require linked implementation artifacts for completed admin Epics 14-21 follow-up work.

## 6. Checklist Snapshot

- [x] Trigger understood: readiness assessment reported 13 issues, with four highest-priority source-of-truth fixes.
- [x] PRD impact assessed and patched.
- [x] Architecture impact assessed and patched.
- [x] UX impact assessed and patched.
- [x] Epics/story-readiness impact assessed and patched.
- [N/A] Rollback: no code or completed story rollback needed.
- [N/A] MVP review: no MVP scope change required.
- [x] Handoff: direct documentation adjustment applied.
