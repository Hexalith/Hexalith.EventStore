---
created: 2026-07-15
story_id: "1.9"
story_key: 1-9-tenants-query-and-read-model-adoption
status: review
split_from: 1-6-sample-and-tenants-domain-centric-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.9: Tenants Query And Read-Model Adoption

Status: review

## Review Scope

The parent implementation/spec records Tenants adoption of `IDomainQueryHandler`,
`IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`, including scoped tests.
Review must verify tenant isolation, RBAC/audit behavior, cursor scope, paging, conflicts,
persisted lifecycle state, and Story 1.2 provenance without ETag-derived aliases.

## Completion Gate

`done` requires the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted
scope, source/package mode, focused validation results, and persisted production-path
evidence. Until those external-authority facts are recorded, this child remains `review`
and does not authorize a submodule change.

Historical evidence: `spec-1-6-sample-and-tenants-domain-centric-adoption.md` and the
Story 1.6 implementation/review record.
