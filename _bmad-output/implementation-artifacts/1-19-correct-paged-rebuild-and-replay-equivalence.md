---
created: 2026-07-15
story_id: "1.19"
story_key: 1-19-correct-paged-rebuild-and-replay-equivalence
status: review
supersedes: 1-14-correct-paged-rebuild-and-replay-equivalence.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.19: Correct Paged Rebuild And Replay Equivalence

Status: review

## Reissue Decision

This is the active review identity for historical Story 1.14. Its implementation, task
history, test evidence, and review findings remain in
`1-14-correct-paged-rebuild-and-replay-equivalence.md`; renumbering does not reset or
duplicate development work.

## Acceptance Boundary

- Rebuild handlers use explicit full-replay or incremental semantics; a page is never
  presented as a complete stream.
- Operation-scoped staging preserves the last complete live model until durable promotion.
- Bounded pages neither skip, duplicate, nor reorder events and match canonical replay at
  the same position.
- Cancel, failure, and resume keep live state intact and report only durable progress.
- Every normalized projection target produced by Story 1.17 completes before promotion;
  idempotency/checkpoint behavior follows Stories 1.15-1.18.
- Review evidence must include multi-page, multi-projection, failure/resume, and persisted
  read-back equivalence—not aggregate-only or mock-only proof.

Next action: complete the existing review under this identity and record its disposition;
do not rerun implementation solely because of the migration.
