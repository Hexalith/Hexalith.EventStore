---
created: 2026-07-15
story_id: "1.10"
story_key: 1-10-tenants-projection-and-event-consumer-adoption
status: review
split_from: 1-6-sample-and-tenants-domain-centric-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.10: Tenants Projection And Event-Consumer Adoption

Status: review

## Review Scope

The parent implementation/spec records Tenants projection and event-consumer adoption.
Review must independently verify tenant isolation, duplicate/out-of-order delivery,
checkpoint advancement, failure recovery, audit behavior, and persisted end state through
production paths in source and package modes.

## Completion Gate

`done` requires the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted
scope, rollback boundary, focused validation results, and independent persisted-path
review. Until those facts exist, local Tenants infrastructure remains intact and this
child remains `review`.

Historical evidence: `spec-1-6-sample-and-tenants-domain-centric-adoption.md` and the
Story 1.6 implementation/review record.

## Review Findings

- [x] [Review][Decision→Patch] Record the maintainer-approved Tenants commit and rollback boundary — **APPLIED:** Administrator approved the latest Tenants commit `8ab537efcb1e035e88f13f7e03508796a88620a8` on 2026-07-16. The SHA equals the Tenants `origin/main` head and the EventStore parent gitlink at `82ed167c`. Accepted scope is the existing Story 1.10 projection/event-consumer adoption with reuse of the historical implementation evidence ending at `24d7d66450cce0be46531f8d70d28d3d6c5a0594`. The remaining review patches stay completion gates; local Tenants infrastructure remains intact. Rollback boundary: `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b`.
- [ ] [Review][Patch] Add package-mode projection/event-consumer production-path validation [_bmad-output/implementation-artifacts/1-10-tenants-projection-and-event-consumer-adoption.md:14]
- [ ] [Review][Patch] Add independent duplicate, out-of-order, checkpoint-advance, and recovery evidence through the production consumer path [_bmad-output/implementation-artifacts/1-10-tenants-projection-and-event-consumer-adoption.md:14]
- [ ] [Review][Patch] Map and execute persisted tenant-isolation, audit, detail/index, freshness, and invalid-identity evidence [_bmad-output/implementation-artifacts/1-10-tenants-projection-and-event-consumer-adoption.md:14]
- [ ] [Review][Patch] Assert the explicit `system|tenants|v1` registration before resolver fallback [tests/Hexalith.EventStore.AppHost.Tests/Configuration/EventStoreDomainServiceConfigurationTests.cs:17]
- [ ] [Review][Patch] Exercise the real Tenants host `/query` and operational-metadata endpoints over HTTP [references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs:129]
- [ ] [Review][Patch] Replace semantically brittle source-substring host assertions with syntax-aware or runtime proof [references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs:101]
- [ ] [Review][Patch] Make the legacy `IDomainProcessor` guardrail syntax-aware for valid C# type forms, comments, and strings [tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:487]
- [ ] [Review][Patch] Cover custom/default/null development health writers on both `/health` and `/ready`, plus the production boundary [src/Hexalith.EventStore.ServiceDefaults/Extensions.cs:174]
- [ ] [Review][Patch] Assert the support-safe health response excludes the injected exception message [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/HealthEndpointsTests.cs:127]
- [ ] [Review][Patch] Update documentation that still points to the deleted `CounterProcessor` [docs/getting-started/quickstart.md:15]
- [ ] [Review][Patch] Use `ConfigureAwait(false)` in the newly added asynchronous tests [tests/Hexalith.EventStore.AppHost.Tests/Configuration/EventStoreDomainServiceConfigurationTests.cs:27]
- [ ] [Review][Patch] Move `EmptyReadModelStore` and `EmptyQueryCursorCodec` into their own files [references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs:716]
