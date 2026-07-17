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

### Review Findings

- [ ] [Review][Decision] Record the external host-boundary acceptance evidence — Story 2.5 requires a Tenants maintainer-approved commit, exact accepted Tenants SHA, explicit host scope, and focused results before `done`. Sibling Story 2.4 now accepts only the contract-metadata scope at `80d23613612088a0c3fee23eb149f34ce08e9729`; it does not accept this host boundary, while the current root pins the later Tenants SHA `76474f16ad40f113273e60f662f69493775c5cc4`. Decide whether the current direct admin-authored chain and focused evidence are accepted for Story 2.5 after its remaining host patch, or keep the story open pending a separate maintainer approval record.
- [ ] [Review][Patch] Replace the Tenants-local append-only `DaprAppIdHandler` with the platform-owned, replace-not-append `AddEventStoreDaprServiceInvocation` handler required by AD-18, then update the handler-chain and structural tests [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:74; references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs:8]
