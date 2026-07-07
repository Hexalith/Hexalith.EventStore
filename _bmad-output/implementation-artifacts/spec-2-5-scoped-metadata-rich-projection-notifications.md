---
title: '2.5 Scoped Metadata-Rich Projection Notifications'
type: 'feature'
created: '2026-07-07T00:00:00+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: 'b6cb3594c6a23fb61e8ce80421aff28d3c1d1e9c'
final_revision: 'c2d6faaad042a867b3bbd1e7ef74cdc491c1a487'
context: []
warnings: []
---

<intent-contract>

## Intent

**Problem:** Story 2.5 is partially implemented on the server SignalR broadcaster, but the reusable SignalR client and DAPR projection-notification path still expose only signal-only tenant-wide notifications. Real-time consumers cannot subscribe to scoped detail notifications or carry bounded metadata through the optional pub/sub path without custom code.

**Approach:** Preserve the existing `ProjectionChanged(projectionType, tenantId)` path and group naming exactly, then add additive detail APIs for scoped client subscriptions and projection notification publishing/receiving. Metadata remains bounded, opaque, support-safe, and only a freshness hint; clients still re-query for projection-confirmed state.

## Boundaries & Constraints

**Always:** Keep `ProjectionChanged(projectionType, tenantId)`, `JoinGroup`, `LeaveGroup`, topic `{tenantId}.{projectionType}.projection-changed`, and group `{projectionType}:{tenantId}` backward compatible. Regenerate ETags before any SignalR broadcast. Run tenant authorization before group membership changes. Treat `GroupScope` as a group filter, not a second authorization boundary. Keep metadata values out of Information, Warning, and Error logs.

**Block If:** A required fix needs a breaking change to existing SignalR method names, existing DAPR topic names, or public notification constructor call sites; scope-level authorization semantics are required beyond tenant validation; metadata needs to carry payload/body/content instead of bounded string metadata.

**Never:** Do not make SignalR notifications proof of command success. Do not parse or expose ETags, cursors, JWTs, raw payloads, or event bodies as notification metadata. Do not route UI hosts through generated REST controllers. Do not initialize nested submodules or change submodule files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| legacy signal | Existing client subscribes to `counter:acme` and server broadcasts signal-only | Existing callback fires; existing build/tests pass unchanged | No error expected |
| scoped detail | Client subscribes to projection `counter`, tenant `acme`, scope `counter-1`; producer sends detail with metadata | Detail callback receives `ProjectionChangedDetail` for `counter:acme:counter-1`; tenant-wide callbacks do not receive scoped detail | No error expected |
| wrong scope | Client subscribes to `counter:acme:counter-2`; detail arrives for `counter:acme:counter-1` | Callback is not invoked and no receipt log is emitted | No error expected |
| invalid group part | Projection type, tenant id, or scope is null/blank or contains `:` | Client or hub rejects before joining a group; denied joins do not consume quota | Throw `ArgumentException` client-side or `HubException` server-side |
| oversized metadata | Detail metadata exceeds configured entry or byte caps | Metadata is clipped deterministically before broadcast; count-only clipping evidence is logged | Values are not logged above Debug |
| DAPR detail | Pub/sub notification contains group scope and metadata | Controller regenerates ETag, then calls detail broadcaster; actor failure returns non-200 and does not broadcast | Broadcast failure is fail-open after ETag regeneration |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/ProjectionChangedDetail.cs` -- server-side producer/broadcaster detail payload exposed through the client package.
- `src/Hexalith.EventStore.SignalR/ProjectionChangedDetail.cs` -- lightweight reusable SignalR client detail payload, kept local so the SignalR package does not depend on the DAPR client package.
- `src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs` -- add an additive detail notification overload.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs` -- extend the DAPR notification DTO with optional `GroupScope` and metadata defaults while preserving legacy constructor call compatibility.
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` -- add detail handler registration, scoped subscribe/unsubscribe APIs, scoped rejoin support, and metadata-safe receipt logging.
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` -- publish or directly process detail notifications while preserving legacy signal-only behavior.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` -- receive optional detail metadata from DAPR, regenerate ETag first, then broadcast signal-only or detail as appropriate.
- `src/Hexalith.EventStore/Validation/ProjectionChangedNotificationValidator.cs` -- validate optional scope and metadata bounds/safe separator rules for incoming pub/sub notifications.
- `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` -- extend development-only runtime proof broadcast request/result for optional scoped detail without changing legacy proof calls.
- `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` -- client unit coverage for scoped detail subscriptions, callbacks, mismatch filtering, logging, and reconnect.
- `tests/Hexalith.EventStore.Server.Tests/Projections/` -- notifier unit coverage for direct and pub/sub detail paths.
- `tests/Hexalith.EventStore.Server.Tests/Validation/ProjectionChangedNotificationValidatorTests.cs` -- validation coverage for scope and metadata limits.
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- controller integration coverage for cross-process detail notifications.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` -- optional live proof for scoped detail fan-out when Aspire/Redis is available.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs` -- add optional `GroupScope` and `Metadata` primary-constructor parameters with defaults after `EntityId` -- lets DAPR carry detail data without breaking existing signal-only call sites.
- [x] `src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs` and `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` -- add and implement a detail overload using `ProjectionChangedDetail`; direct mode regenerates then broadcasts detail, pub/sub mode publishes the extended DTO -- gives producers one platform seam for scoped detail.
- [x] `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` -- detect detail notifications by nonblank scope or present metadata, regenerate the ETag first, then call the detail broadcaster; retain signal-only broadcast for legacy notifications -- preserves cache invalidation and compatibility.
- [x] `src/Hexalith.EventStore/Validation/ProjectionChangedNotificationValidator.cs` -- validate optional `GroupScope` against blank/colon/length rules and metadata against the same entry/byte defaults used by SignalR options -- rejects oversized pub/sub payloads before broadcast clipping is the only defense.
- [x] `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` -- add `ProjectionChangedDetail` handler plus `SubscribeDetailAsync`/`UnsubscribeDetailAsync` APIs that use `JoinGroupScoped`/`LeaveGroupScoped`, dispatch `ProjectionChangedDetail` callbacks, ignore mismatched scopes, and rejoin scoped groups after reconnect -- makes the reusable client consume the new path.
- [x] `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` -- add optional scope/metadata fields to the proof request and choose signal-only or detail broadcast based on their presence -- enables live backplane proof without adding production endpoints.
- [x] `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` -- add focused tests for scoped detail subscribe/unsubscribe, null scope fallback, wrong-scope no-op, reconnect rejoin, invalid scope rejection, and metadata values absent from non-Debug logs -- covers client edge cases in the matrix.
- [x] `tests/Hexalith.EventStore.Server.Tests/Projections/`, `tests/Hexalith.EventStore.Server.Tests/Validation/ProjectionChangedNotificationValidatorTests.cs`, and `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- add direct/pubsub/controller validation tests proving detail metadata flows after ETag regeneration, actor failures do not broadcast, and oversized metadata is rejected or bounded as documented -- covers DAPR/detail edge cases.
- [x] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` -- extend the live Redis backplane proof for scoped detail delivery when Tier 3 prerequisites are available, or document the exact blocker in the auto-run result -- satisfies the optional DAPR/runtime proof requirement.

**Acceptance Criteria:**
- Given an existing signal-only projection notification consumer, when the story changes are built and existing SignalR tests run, then `ProjectionChanged(projectionType, tenantId)`, `JoinGroup`, `LeaveGroup`, and group `{projectionType}:{tenantId}` remain backward compatible.
- Given a producer calls the detail notifier or broadcaster with projection type, tenant id, optional group scope, and metadata, when transport is direct or pub/sub, then ETag regeneration happens first and the detail payload is sent only to the matching scoped group.
- Given metadata exceeds configured entry or byte limits, when notification validation or broadcast runs, then the framework rejects or clips according to the documented bounds and never logs metadata values above Debug.
- Given clients join or leave scoped projection groups, when authorization fails or group names contain reserved separators, then tenant authorization still runs before membership changes and invalid joins do not consume group quota.
- Given SignalR and optional DAPR notification tests run, when scoped detail, signal-only, auth rejection, fail-open broadcast, and Redis backplane paths are exercised, then all focused tests pass or exact environmental blockers are recorded.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 7: (high 2, medium 5, low 0)
- defer: 0
- reject: 1: (high 0, medium 1, low 0)
- addressed_findings:
  - `[high]` `[patch]` Preserved public `ProjectionChangedNotification` constructor and legacy three-field deconstruction compatibility; added explicit legacy constructors, a legacy `Deconstruct`, JSON constructor selection, and DTO compatibility/serialization tests.
  - `[high]` `[patch]` Preserved external `IProjectionChangeNotifier` implementations by making the new detail overload a default interface method that delegates to the legacy signal-only method; added a compatibility test.
  - `[medium]` `[patch]` Fixed tenant-wide empty detail notifications being downgraded to signal-only by treating present metadata, including an empty map, as detail intent; added controller coverage.
  - `[medium]` `[patch]` Bounded DAPR detail metadata before pub/sub publish using configured notifier limits and deterministic clipping; added producer-side clipping and invalid-scope tests.
  - `[medium]` `[patch]` Replaced hard-coded validation defaults with configured `SignalROptions` metadata limits; added option-driven validator coverage.
  - `[medium]` `[patch]` Removed the reusable SignalR package dependency on `Hexalith.EventStore.Client` by adding a local SignalR detail DTO and keeping DAPR dependencies out of the lightweight client package.
  - `[medium]` `[patch]` Rolled back client detail subscriptions when `JoinGroupScoped` fails so rejected joins do not leave stale callbacks; added focused client coverage.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 1, medium 3, low 1)
- defer: 0
- reject: 0
- addressed_findings:
  - `[high]` `[patch]` Replaced the legacy `IProjectionChangeNotifier` default detail overload downgrade with an explicit `NotSupportedException`, preserving implementer compatibility while preventing silent loss of scope and metadata.
  - `[medium]` `[patch]` Added the 64-character group-scope guard to both the reusable SignalR client and the hub so clients cannot join scoped groups that producer/validator paths cannot target.
  - `[medium]` `[patch]` Copied incoming SignalR detail metadata into a read-only dictionary before callbacks so consumer mutation cannot affect other callbacks or future empty-detail notifications.
  - `[medium]` `[patch]` Fixed the development runtime proof endpoint so explicitly present empty metadata still triggers tenant-wide detail broadcast instead of being downgraded to signal-only.
  - `[low]` `[patch]` Returned `400 BadRequest` for runtime proof metadata with null keys or values instead of allowing malformed proof payloads to reach broadcaster execution.

### 2026-07-07 — Second follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 1, medium 4, low 4)
- defer: 0
- reject: 6: (high 0, medium 2, low 4)
- addressed_findings:
  - `[high]` `[patch]` Removed producer/receiver metadata cap drift by validating incoming DAPR projection notifications with `ProjectionChangeNotifierOptions`, matching the producer's pub/sub bounding options and allowing SignalR to clip lower at broadcast time.
  - `[medium]` `[patch]` Rejected undefined `ProjectionChangeTransport` values during options validation so invalid configuration cannot silently fall into the direct transport branch.
  - `[medium]` `[patch]` Eliminated the scoped-detail reconnect race by deriving the SignalR client rejoin method from immutable `GroupScope` instead of a mutable `RequiresScopedHubMethod` flag.
  - `[medium]` `[patch]` Made shared empty metadata instances immutable in notifier/controller/broadcaster/proof paths so downstream code cannot cast and mutate state reused by later notifications.
  - `[medium]` `[patch]` Hardened the development runtime proof endpoint to reject overlong scopes and bound metadata before reporting the proof result.
  - `[low]` `[patch]` Added scoped leave validation for colon and overlong scopes so raw hub clients cannot request oversized scoped group removals.
  - `[low]` `[patch]` Extended controller integration coverage to prove invalid detail scopes and oversized detail metadata are rejected before actor regeneration or SignalR broadcast.
  - `[low]` `[patch]` Added producer-side byte-cap clipping coverage for pub/sub detail metadata.
  - `[low]` `[patch]` Extended the Redis runtime proof to assert overlong-scope rejection and clipped metadata counts.

### 2026-07-07 — Third follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 1: (high 0, medium 1, low 0)
- defer: 1: (high 0, medium 0, low 1)
- reject: 0
- addressed_findings:
  - `[medium]` `[patch]` Extended the Redis runtime proof to start the reusable `EventStoreSignalRClient`, subscribe to a scoped detail group over the real hub, and assert cross-instance scoped detail delivery with metadata; added the required test project reference and new proof evidence.

## Design Notes

Server-side `ProjectionChangedDetail`, scoped hub groups, and broadcaster metadata clipping already exist. This story should finish the seams around that existing capability rather than replacing it. Keep the extended DAPR DTO additive by appending optional parameters:

```csharp
public record ProjectionChangedNotification(
    string ProjectionType,
    string TenantId,
    string? EntityId = null,
    string? GroupScope = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
```

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: projection notification DTO compatibility and serialization tests pass.
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` -- expected: reusable client signal-only and detail subscription tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ProjectionChangeNotifier|FullyQualifiedName~ProjectionChangedNotification|FullyQualifiedName~ETagActorIntegrationTests"` -- expected: focused server notification tests pass; if known Server.Tests CA2007 baseline blocks unrelated build, run narrower affected test classes and document.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter FullyQualifiedName~SignalRRedisBackplaneRuntimeProofTests` -- expected: live Redis backplane scoped detail proof passes when Docker/Aspire prerequisites are available, otherwise exact blocker is documented.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: Release build passes with warnings as errors.
- `git diff --check` -- expected: no whitespace errors.

**Results (2026-07-07):**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed: 595 tests.
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` -- passed: 42 tests.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- passed: 520 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ProjectionChangeNotifier|FullyQualifiedName~ProjectionChangedNotification|FullyQualifiedName~ETagActorIntegrationTests"` -- passed: 113 tests.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter FullyQualifiedName~SignalRRedisBackplaneRuntimeProofTests` -- passed: 1 test; evidence written to `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-150530Z.md`.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed.

**Follow-up review results (2026-07-07):**
- `dotnet test tests/Hexalith.EventStore.Client.Tests/ --filter "FullyQualifiedName~AddEventStoreTests"` -- passed: 21 tests.
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/ --filter "FullyQualifiedName~EventStoreSignalRClientTests"` -- passed: 44 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ProjectionChangedHubTests"` -- passed: 23 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ProjectionChangeNotifier|FullyQualifiedName~ProjectionChangedNotification|FullyQualifiedName~ETagActorIntegrationTests"` -- passed: 114 tests.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter FullyQualifiedName~SignalRRedisBackplaneRuntimeProofTests` -- passed: 1 test; evidence written to `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-152519Z.md`.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed.

**Second follow-up review results (2026-07-07):**
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/ --filter "FullyQualifiedName~EventStoreSignalRClientTests"` -- passed: 44 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ProjectionChangeNotifierOptionsTests|FullyQualifiedName~ProjectionChangedNotificationValidatorTests|FullyQualifiedName~DaprProjectionChangeNotifierTests|FullyQualifiedName~ProjectionChangedHubTests|FullyQualifiedName~ETagActorIntegrationTests"` -- passed: 86 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ProjectionChangeNotifier|FullyQualifiedName~ProjectionChangedNotification|FullyQualifiedName~ETagActorIntegrationTests"` -- passed: 120 tests.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter FullyQualifiedName~SignalRRedisBackplaneRuntimeProofTests` -- passed: 1 test; evidence written to `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-154949Z.md`.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed.

**Third follow-up review results (2026-07-07):**
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter FullyQualifiedName~SignalRRedisBackplaneRuntimeProofTests --no-restore` -- passed: 1 test; evidence written to `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-160644Z.md`.
- `git diff --check` -- passed.

## Auto Run Result

Status: done.

Summary: Implemented scoped, metadata-rich projection notifications across reusable SignalR clients, DAPR direct/pubsub notification paths, validation, runtime proof endpoints, and compatibility safeguards. Follow-up reviews hardened notifier API behavior, scoped group validation, callback metadata isolation, runtime proof handling, and finally proved the packaged reusable SignalR client against the real hub/backplane path.

Files changed:
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` -- clarified automatic projection notification behavior.
- `src/Hexalith.EventStore.Client/Projections/IProjectionChangeNotifier.cs` -- added detail notification overload for producer APIs with an explicit unsupported default for legacy implementations.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs` -- extended DAPR notification DTO with scope/metadata while preserving constructors, deconstruction, and JSON binding.
- `src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs` -- added metadata bounds for detail notifications.
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` -- implemented direct/pubsub detail notification handling with deterministic metadata bounding.
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` -- added scoped detail subscription, unsubscribe, reconnect, mismatch filtering, rollback behavior, scope length validation, and read-only callback metadata snapshots.
- `src/Hexalith.EventStore.SignalR/ProjectionChangedDetail.cs` -- added SignalR-local client detail payload.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` -- routed detail notifications through ETag regeneration before SignalR broadcast.
- `src/Hexalith.EventStore/SignalRHub/SignalROptions.cs` -- exposed configured metadata bounds for SignalR validation/broadcasting.
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` -- added hub-side scoped group length validation.
- `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` -- extended development-only proof broadcast request/result for detail notifications and hardened empty/null metadata handling.
- `src/Hexalith.EventStore/Validation/ProjectionChangedNotificationValidator.cs` -- validated scope and metadata using configured bounds.
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreProjectionTests.cs` -- covered producer detail notification behavior.
- `tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs` -- covered default interface compatibility and explicit unsupported detail fallback.
- `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionChangedNotificationTests.cs` -- covered detail DTO compatibility and JSON deserialization.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` -- extended Redis backplane proof for scoped detail delivery, tenant-wide empty metadata detail delivery, and malformed metadata rejection.
- `tests/Hexalith.EventStore.Server.Tests/Configuration/ProjectionChangeNotifierOptionsTests.cs` -- covered metadata bound defaults and validation.
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- covered controller detail flow, fail-before-broadcast behavior, and test service replacement.
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionChangeNotifierSignalRTests.cs` -- covered direct SignalR detail broadcast behavior.
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionChangeNotifierTests.cs` -- covered direct/pubsub detail notifier behavior and metadata clipping.
- `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs` -- covered hub-side scoped group length rejection.
- `tests/Hexalith.EventStore.Server.Tests/Validation/ProjectionChangedNotificationValidatorTests.cs` -- covered scope and metadata validation.
- `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` -- covered reusable client detail subscription behavior, scope length rejection, and read-only metadata snapshots.
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-150530Z.md` -- recorded final runtime proof evidence.
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-152519Z.md` -- recorded follow-up runtime proof evidence.

Second follow-up review patch additions:
- `src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs` -- rejected undefined transport enum values during options validation.
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` -- made empty detail metadata immutable.
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` -- removed mutable scoped-rejoin state and derives rejoin behavior from immutable group scope.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` -- made empty detail metadata immutable.
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` -- rejected invalid scoped leave values before group removal.
- `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs` -- made empty detail metadata immutable.
- `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` -- rejected overlong proof scopes and bounded proof metadata before reporting/broadcasting.
- `src/Hexalith.EventStore/Validation/ProjectionChangedNotificationValidator.cs` -- aligned DAPR notification validation metadata caps with projection notifier producer options.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` -- covered proof scope rejection and clipped metadata counts.
- `tests/Hexalith.EventStore.Server.Tests/Configuration/ProjectionChangeNotifierOptionsTests.cs` -- covered undefined transport validation.
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- covered actual endpoint rejection before actor/broadcast for invalid scoped detail payloads.
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionChangeNotifierTests.cs` -- covered producer-side byte-cap metadata clipping.
- `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs` -- covered scoped leave validation.
- `tests/Hexalith.EventStore.Server.Tests/Validation/ProjectionChangedNotificationValidatorTests.cs` -- covered projection notifier option-driven metadata validation.
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-154949Z.md` -- recorded second follow-up runtime proof evidence.

Third follow-up review patch additions:
- `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` -- referenced the reusable SignalR client package from the Tier 3 runtime proof project.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` -- used `EventStoreSignalRClient.StartAsync` and `SubscribeDetailAsync` against the real instance-B hub, then proved cross-instance scoped detail delivery from instance A.
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-07-07-160644Z.md` -- recorded third follow-up runtime proof evidence.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- appended one new deferred-work entry for pre-existing raw hub leave projection/tenant validation.

Review findings breakdown: initial review addressed 7 patch findings with 0 deferred and 1 rejected; follow-up review addressed 5 patch findings with 0 deferred and 0 rejected; second follow-up review addressed 9 patch findings with 0 deferred and 6 rejected; third follow-up review addressed 1 patch finding, deferred 1 pre-existing low-severity finding, and rejected 0 findings.

Follow-up review recommendation: false.

Verification performed: all commands listed in the 2026-07-07 verification results, follow-up review results, second follow-up review results, and third follow-up review results passed.

Residual risks: Metadata remains a freshness hint only; consumers still need to re-query projection state. The raw hub leave projection/tenant validation gap is deferred as pre-existing work in `_bmad-output/implementation-artifacts/deferred-work.md`. The optional Tier 3 Redis proof depends on external Redis/Docker availability, but it passed in this run.
