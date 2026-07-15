---
created: 2026-07-15
story_id: "2.7"
story_key: 2-7-tenants-compatibility-and-package-mode-validation
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.7: Tenants Compatibility And Package-Mode Validation

Status: review

The parent Story 2.4 spec records Debug/source and Release/package validation. This child
reviews exact EventStore/Tenants SHAs in source mode and exact package versions/hashes in
package mode, with no mixed Gateway-source/DomainService-package graph. `done` requires the
Tenants maintainer-approved repository boundary, exact SHA, compatibility disposition, and
focused restore/build/test evidence. Incomplete identity evidence remains fail-closed.
