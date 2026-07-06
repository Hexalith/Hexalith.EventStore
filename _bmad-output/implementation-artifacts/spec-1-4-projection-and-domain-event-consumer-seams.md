---
title: '1.4 Projection And Domain Event Consumer Seams'
type: 'feature'
created: '2026-07-06'
status: 'done'
baseline_revision: '62220cb913ef0abccb07039e18df885507385a23'
final_revision: '3c158ba5f14998ddfd7cfa59284ae21ec565166f'
review_loop_iteration: 1
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-1-3-generic-read-models-and-query-cursors.md'
warnings: ['oversized']
---

<intent-contract>

## Intent

**Problem:** Projection and domain-event consumer seams exist, but the platform contract is still thin: projection handlers with duplicate domains first-match, domain-event deduplication is process-local instead of marker-backed, and consumer registration/endpoint behavior is not pinned by focused tests. This leaves domain modules room to reintroduce projection endpoints, DAPR subscription plumbing, and ad hoc duplicate handling.

**Approach:** Harden the existing seams additively: validate projection handler routes deterministically, add a marker-store seam for domain-event consumer deduplication by EventStore message ID, fix payload identity reflection, validate support-safe event envelopes, and prove registration plus DAPR endpoint mapping without changing domain logic.

## Boundaries & Constraints

**Always:** Keep `IDomainProjectionHandler` and `IEventStoreDomainEventHandler<TEvent>` as the author seams; keep Client ASP.NET-free and DomainService responsible for endpoint mapping; preserve the canonical `/project` yield behavior for app-owned POST `/project`; deduplicate consumed events by EventStore `MessageId`; treat pub/sub delivery as at-least-once and unordered; keep all public changes additive and warnings-as-errors clean.

**Block If:** Durable marker behavior requires a non-additive contract break, a distributed lease/TTL policy that cannot be safely chosen from existing state-store semantics, DAPR component/AppHost changes, submodule edits, Tenants migration, or changes to the EventStore publisher wire format.

**Never:** Do not migrate Tenants or edit `references/Hexalith.Tenants`; do not implement generated REST/UI behavior; do not rewrite projection actors or the projection rebuild orchestrator; do not expose raw payload bytes, decoded payloads, protected metadata, stack traces, or cursor/ETag internals in consumer errors or logs; do not run solution-level `dotnet test`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Projection dispatch | One `IDomainProjectionHandler` matches `ProjectionRequest.Domain` | `/project` and `DomainProjectionDispatcher` route to that handler and preserve the handler response | No error expected |
| Duplicate projection domains | Two projection handlers advertise the same domain ignoring case | SDK endpoint setup and dispatcher validation fail deterministically with the duplicate domain and handler names | Startup/test failure, not first-match routing |
| Consumer first delivery | Known event type, valid JSON payload, unused marker key | Processor acquires a message marker, builds context, dispatches typed handlers once, and marks the message completed | Handler exception releases the marker and propagates so DAPR can redeliver |
| Consumer duplicate delivery | Marker store reports the message completed | Processor skips handlers and endpoint returns success so DAPR acknowledges the duplicate | No payload deserialization required |
| Invalid consumer envelope | Missing/blank identity fields, unsupported `SerializationFormat`, unknown event type, malformed payload, or payload aggregate mismatch | Unknown/mismatch/invalid payload outcomes stay support-safe and do not dispatch handlers; malformed payloads remain acknowledged to avoid poison loops | Logs include safe metadata only; raw payload and decoded values are not emitted |
| Subscription endpoint | `MapEventStoreDomainEvents()` is used with configured pub/sub, topic, and route | Endpoint is mapped with DAPR topic metadata and maps processed/duplicate/skipped/invalid outcomes intentionally | Unexpected processor result maps to 500 |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs` -- projection handler selection and duplicate-domain validation.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- SDK endpoint mapping, `/project` route yield, and projection validation hook.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` -- DAPR pub/sub subscription endpoint and processing-result HTTP mapping.
- `src/Hexalith.EventStore.Client/Registration/EventStoreDomainEventsServiceCollectionExtensions.cs` -- consumer options, event-type registry, marker-store registration, and handler idempotency.
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs` -- event envelope validation, marker-backed deduplication, payload identity validation, and handler dispatch.
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventEnvelope.cs`, `EventStoreDomainEventContext.cs`, and `EventStoreDomainEventsOptions.cs` -- additive consumer wire/context/options surface.
- `src/Hexalith.EventStore.Client/Subscriptions/IEventStoreDomainEventMarkerStore.cs`, `DaprEventStoreDomainEventMarkerStore.cs`, and `InMemoryEventStoreDomainEventMarkerStore.cs` -- new marker-store seam and implementations.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` and `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainEventsEndpointExtensionsTests.cs` -- projection and subscription endpoint coverage.
- `tests/Hexalith.EventStore.Client.Tests/Subscriptions/EventStoreDomainEventProcessorTests.cs` and `tests/Hexalith.EventStore.Client.Tests/Registration/EventStoreDomainEventsServiceCollectionExtensionsTests.cs` -- consumer marker, validation, registry, and registration coverage.
- `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs` -- projection DTO compatibility coverage for `MessageId` and `UserId` evidence.
- `docs/reference/stream-replay-api.md` and `docs/reference/nuget-packages.md` -- update projection idempotency and domain-event consumer seam guidance after tests prove behavior.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs` and `EventStoreDomainServiceExtensions.cs` -- add projection-handler route materialization/validation so duplicate domains fail before first-match dispatch, while no-handler requests still return `null`/404.
- [x] `src/Hexalith.EventStore.Client/Subscriptions/IEventStoreDomainEventMarkerStore.cs`, `DaprEventStoreDomainEventMarkerStore.cs`, and `InMemoryEventStoreDomainEventMarkerStore.cs` -- add a marker-store seam keyed by EventStore `MessageId`, with DAPR completed-marker first-write concurrency and deterministic in-memory tests.
- [x] `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs` -- replace private process-local deduplication with the marker store, release the marker on handler exceptions, fix payload identity property caching so it keys by event type plus property name, and keep support-safe logging.
- [x] `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventEnvelope.cs`, `EventStoreDomainEventContext.cs`, and `EventStoreDomainEventsOptions.cs` -- add only additive context/options fields needed for marker state-store names and publisher metadata; validate blank identity fields and unsupported serialization formats before dispatch.
- [x] `src/Hexalith.EventStore.Client/Registration/EventStoreDomainEventsServiceCollectionExtensions.cs` -- register the marker store, preserve handler registration idempotency, and harden event-type registry building for loadable event payload classes.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` -- prove and, if needed, adjust the subscription endpoint result mapping so duplicates/skips/invalid payloads are acknowledged and transient processor failures remain retryable.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/`, `tests/Hexalith.EventStore.Client.Tests/`, and `tests/Hexalith.EventStore.Contracts.Tests/` -- add focused coverage for every matrix scenario, including DAPR `WithTopic` metadata, marker concurrency, handler exception retry, payload identity cache isolation, and projection DTO `MessageId`/`UserId` round trips.
- [x] `docs/reference/stream-replay-api.md` and `docs/reference/nuget-packages.md` -- document the hardened `/project` and domain-event consumer seams, including message-marker deduplication expectations and the fact that domains write handlers, not DAPR endpoint plumbing.

**Acceptance Criteria:**
- Given discovered projection handlers, when SDK endpoint mapping runs, then duplicate handler domains are rejected deterministically and a single handler domain still dispatches through `/project`.
- Given a consumed EventStore event is delivered more than once, when the marker store already contains its message ID, then handlers are not invoked again and the endpoint acknowledges the duplicate.
- Given event handler execution fails after a marker is acquired, when DAPR redelivers the same message, then the marker can be acquired again and the handler can run.
- Given payload identity validation is configured, when two processors use different identity property names for the same event type, then each processor validates against its own property and cannot reuse stale reflection cache entries.
- Given the domain-event endpoint is mapped, when endpoint metadata is inspected in tests, then the configured route, pub/sub component, and topic are present and result-to-HTTP mapping is intentional.
- Given a domain author inspects the docs and Sample shape, when they implement projections or event consumers, then they are guided to write domain handlers and platform registration only, not hand-written projection actors, subscription routers, or DAPR endpoint plumbing.

## Spec Change Log

- 2026-07-06: Review remediation narrowed the DAPR marker store to completed markers only, made durable marker registration explicit opt-in, added topic/route marker-key scope, hardened ULID message-id validation, and preserved app-owned POST `/project` yield behavior before projection-handler validation.

## Review Triage Log

### 2026-07-06 — Review pass 1

- patched (10): default domain-event marker store changed from DAPR-backed to in-memory so consumers do not implicitly require state-store access; DAPR marker acquisition no longer writes durable `InProgress` records; DAPR marker keys are scoped by topic, subscription route, and message id; handler-failure marker release uses a non-request cancellation token and support-safe logging; completion-marker write failure after successful handlers is acknowledged without releasing the marker; event-type and domain handler discovery now fail fast on assembly load failures instead of silently dropping types; projection-handler validation now runs only when the SDK maps POST `/project`; `messageId` is validated as a ULID before marker-key construction; marker acquisition enum handling is an explicit switch with unsupported values kept retryable; in-memory marker acquisition no longer indexes after a failed add while a concurrent release can remove the key.
- deferred (1): multiple independently side-effecting handlers share one message-level marker; a later handler failure can replay earlier successful handlers. Docs now require idempotent handlers and recommend a composite handler for side effects that must succeed or fail together.
- rejected (0): no reviewer findings were rejected.

## Design Notes

The marker seam should be small and reusable rather than a full projection framework. A completed marker means "this message ID was already handled or terminally skipped"; a handler exception must not leave a completed marker. The default store is in-memory; the DAPR store is explicit opt-in and persists completed markers only, avoiding a durable in-progress lease/TTL policy in this story. If a future durable processing lease becomes necessary, block instead of inventing one without explicit state-store semantics.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: projection contract tests pass.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- expected: subscription, marker-store, and registration tests pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: projection dispatcher and domain-events endpoint tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: succeeds with 0 warnings and 0 errors.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: `done`

Summary:
- Added deterministic projection-handler route validation so duplicate projection domains fail during SDK endpoint setup and direct dispatch, while no-handler projection requests still return `null` for 404 mapping.
- Added marker-backed domain-event consumer idempotency with in-memory default markers, explicit DAPR completed-marker opt-in, scoped marker keys, deterministic in-memory marker tests, handler-exception release, terminal invalid-message completion, safe envelope validation, and payload identity reflection cache isolation by event type plus property name.
- Registered the marker store through the domain-event consumer DI seam, preserved handler registration idempotency, mapped DAPR topic metadata through `MapEventStoreDomainEvents()`, and documented domain-author guidance for projection and event-consumer seams.
- Review remediation patched marker-release behavior so a completion-write failure after successful handler execution does not delete the marker and force an immediate duplicate side-effect retry; added regression tests for DAPR opt-in, ULID message-id validation, unsupported acquisition values, scoped marker keys, and app-owned POST `/project` route yield.

Files changed:
- `docs/reference/nuget-packages.md`
- `docs/reference/stream-replay-api.md`
- `src/Hexalith.EventStore.Client/Registration/EventStoreDomainEventsServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/DaprEventStoreDomainEventMarkerStore.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventContext.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventEnvelope.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventMarkerAcquisitionResult.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventMarkerRecord.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventMarkerState.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessingResult.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventsOptions.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/IEventStoreDomainEventMarkerStore.cs`
- `src/Hexalith.EventStore.Client/Subscriptions/InMemoryEventStoreDomainEventMarkerStore.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerRouteValidator.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`
- `tests/Hexalith.EventStore.Client.Tests/Registration/EventStoreDomainEventsServiceCollectionExtensionsTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Subscriptions/EventStoreDomainEventMarkerStoreTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Subscriptions/EventStoreDomainEventProcessorTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainEventsEndpointExtensionsTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`
- `_bmad-output/implementation-artifacts/spec-1-4-projection-and-domain-event-consumer-seams.md`

Verification:
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed, 566 tests.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- passed, 518 tests.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed, 58 tests.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed, no whitespace errors.

Residual Risks:
- Message-level markers do not make multiple independently side-effecting handlers atomic; a later handler failure can replay earlier successful handlers. Deferred work records the need for a per-handler marker or transactional composite-handler contract if the platform needs stronger guarantees.
- If an explicit DAPR completed-marker write fails after handlers already ran, the endpoint acknowledges the current delivery to avoid an immediate duplicate side-effect retry; a later duplicate delivery can still re-run unless handler logic is idempotent.
