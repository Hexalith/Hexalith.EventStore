---
created: 2026-07-15
story_id: "2.5"
story_key: 2-5-dedicated-external-tenants-api-host
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.5: Dedicated External Tenants API Host

Status: review

The parent Story 2.4 spec records the dedicated generated host, AppHost/ACL wiring, and
runtime tests. This child reviews the host boundary: inbound auth, generated controllers,
gateway-client-only delegation, no domain/UI dependency, and no direct persistence access.
`done` requires the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted host
scope, and focused compiled-route/topology results. Historical authority remains the parent
spec and implementation review.
