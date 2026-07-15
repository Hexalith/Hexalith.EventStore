---
created: 2026-07-15
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.6: Tenants UI Client-Library Alignment And UX Evidence

Status: review

The parent Story 2.4 spec records UI-boundary guardrails. This child reviews typed-client
consumption, absence of generated/hand-written per-message controllers, and canonical UX
states. Only `ProjectionBacked` evidence may claim lifecycle state; handler-computed,
missing, or invalid provenance renders `Unknown`. `done` requires Sally's focused UX
review, Tenants maintainer approval, the exact Tenants SHA, and structural/UI test evidence.
