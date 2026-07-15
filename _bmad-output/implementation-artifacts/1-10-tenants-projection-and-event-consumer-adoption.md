---
created: 2026-07-15
story_id: "1.10"
story_key: 1-10-tenants-projection-and-event-consumer-adoption
status: done
split_from: 1-6-sample-and-tenants-domain-centric-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.10: Tenants Projection And Event-Consumer Adoption

Status: done

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

- [x] [Review][Decision→Patch] Record the maintainer-approved Tenants commit and rollback boundary — **APPLIED:** Administrator approved the latest Tenants pointer on 2026-07-16. The final reviewed and pinned commit is `c59a13f6dc7699c7ea48b1d4b573c1c0dbf2dbcd`; it equals the Tenants `origin/main` head and the EventStore gitlink at current parent `105d7a95800a5e0d5b713710a02a118b5d8321ed`. The initially reviewed baseline was `8ab537efcb1e035e88f13f7e03508796a88620a8`; that is the rollback point for the review patches, while the original functional rollback boundary remains `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b`. Accepted scope is the existing Story 1.10 projection/event-consumer adoption with reuse of the historical implementation evidence ending at `24d7d66450cce0be46531f8d70d28d3d6c5a0594`.
- [x] [Review][Patch] Add package-mode projection/event-consumer production-path validation — **APPLIED:** Release/package-mode consumer, real-host, support-safe health, and 68-test projection/query lanes pass with the default published dependencies (`Hexalith.EventStore 3.67.1`, `Hexalith.Memories 2.6.17`).
- [x] [Review][Patch] Add independent duplicate, out-of-order, checkpoint-advance, and recovery evidence through the production consumer path — **APPLIED:** `TenantProjectionEventConsumerProductionPathTests` resolves the production `EventStoreDomainEventProcessor` from `AddHexalithTenants`, proves message-marker deduplication, stale-sequence protection, tenant isolation, completion-marker advancement after persistence, marker release after a failed save, and successful retry.
- [x] [Review][Patch] Map and execute persisted tenant-isolation, audit, detail/index, freshness, and invalid-identity evidence — **APPLIED:** `DomainServiceEndpointsTests` inspects persisted detail/index/audit state after real `/project` HTTP dispatch; package and source runs of projection conformance, tenant/global-administrator handlers, freshness, and dispatcher invalid-identity tests pass (68 tests in each mode).
- [x] [Review][Patch] Assert the explicit `system|tenants|v1` registration before resolver fallback — **APPLIED:** the root configuration test binds `DomainServiceOptions`, asserts the exact key and all registration fields, then tests resolver behavior.
- [x] [Review][Patch] Exercise the real Tenants host `/query` and operational-metadata endpoints over HTTP — **APPLIED:** `DomainServiceEndpointsTests` boots the real entry point and verifies `/project`, `/query`, and `/admin/operational-index-metadata` responses plus persisted end state.
- [x] [Review][Patch] Replace semantically brittle source-substring host assertions with syntax-aware or runtime proof — **APPLIED:** the raw `Program.cs` substring/order test was removed in favor of the real-host HTTP test.
- [x] [Review][Patch] Make the legacy `IDomainProcessor` guardrail syntax-aware for valid C# type forms, comments, and strings — **APPLIED:** Roslyn parsing now inspects type base lists; focused class/record/struct/primary-constructor/qualified/escaped-identifier and comment/string/generic-negative cases pass.
- [x] [Review][Patch] Cover custom/default/null development health writers on both `/health` and `/ready`, plus the production boundary — **APPLIED:** four TestServer scenarios cover both routes and environment boundaries.
- [x] [Review][Patch] Assert the support-safe health response excludes the injected exception message — **APPLIED:** the real Tenants host test now excludes `Synthetic readiness failure` explicitly.
- [x] [Review][Patch] Update documentation that still points to the deleted `CounterProcessor` — **APPLIED:** active quickstart, lifecycle, versioning, and brownfield source-tree documentation now describe `CounterAggregate<TState>` and typed `Apply` compatibility.
- [x] [Review][Patch] Use `ConfigureAwait(false)` in the newly added asynchronous tests — **APPLIED WITH ENFORCED TEST CONVENTION:** asynchronous production callbacks/helpers use `ConfigureAwait(false)`; xUnit test-method awaits use `ConfigureAwait(true)` because this repository treats xUnit analyzer `xUnit1030` (which forbids `false` in test methods) as a build error.
- [x] [Review][Patch] Move `EmptyReadModelStore` and `EmptyQueryCursorCodec` into their own files — **APPLIED:** each helper now has a dedicated source file.

## Validation Evidence

- Aspire baseline: the root AppHost built with zero warnings/errors, the dashboard reached `Running`, and resource startup began. The detached CLI child later exited with its parent orphan detector, so no AppHost process or file lock remained during validation.
- Root focused lanes: configuration `3/3`, syntax-aware guardrail `10/10`, health endpoint/writer `7/7`.
- Tenants Release/package lanes: production consumer `2/2`, real-host plus support-safe health `2/2`, persisted projection/query evidence `68/68`, using the default published dependencies (`Hexalith.EventStore 3.67.1`, `Hexalith.Memories 2.6.17`). A no-cache, force-evaluated restore succeeded without version overrides.
- Tenants source lanes (`HexalithEventStoreFromSource=true`): production consumer `2/2`, real-host plus support-safe health `2/2`, persisted projection/query evidence `68/68`; the Server lane deliberately used the present Memories source checkout to exercise the source-reference path.
