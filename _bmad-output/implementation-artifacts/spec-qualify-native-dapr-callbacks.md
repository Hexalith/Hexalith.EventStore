---
title: Qualify native Dapr discovery and signed projection notifications
status: verified-awaiting-parent-integration
baseline_commit: 5938ab989
---

## Result and boundary

The real native Program maps authenticated GET /dapr/subscribe and POST /projections/changed. The private technical fence previously rejected both as unenrolled, because they are neither human MVC operations nor actor callbacks. A closed SDK-owned callback observer now binds the actual installed Dapr mapper or actual notification MVC request-delegate/action, exact channel policy/verb, loaded owner/framework digests and configured component/topic/mode. The optional DaprCallback init property is distinct from Http/NativeActor; existing4/7-argument CLR constructors, Deconstruct and legacy null-omitting JSON remain unchanged. Its lifecycle binding requires schema3, exact known paths/operations/verbs/digests and rejects mixed/unknown metadata. Generic outbound service invocation cannot borrow native callback authority.

Discovery remains an authenticated immutable startup handshake. Explicit declarative mode preserves the actual absent endpoint's authenticated404 with no execution. Notifications require an actual committed Development schema3 inventory/code lease, and retain the original real signed publisher allowlist, operation, issuer/audience/lifetime and tenant/projection/topic binding checks. Pubsub supplies no fabricated HTTP Root/Epoch stamps. The controller's actual actor RPC uses the existing remote uncertainty boundary so caught transport failures cannot release uncertain fences. Production authentication/trust, message schemas/eligibility and subscription policy remain unchanged. Activation still requires the existing real authority/coordinator path; no fake authority/callback or plugin was added.

## Three distinct native proof facts

1. Real mapped discovery: actual Dapr1.19 mapper/action metadata is qualified for default pubsub, nondefault in-memory-events and explicit declarative mode. Programmatic mode returns the unchanged *.*.projection-changed topic with the actual configured component; declarative mode has no programmatic topic metadata/mapper and preserves404. Actual foreign delegates, anonymous/mutated policy, component/topic/raw/bulk/metadata shape and changed config identities refuse.
2. Real exact-topic delivery: each test explicitly installs an additional fixture-owned declarative subscription for tenant-a.counter.projection-changed. The real SDK notifier signs provenance and emits actual HTTP/2 PublishEvent RPCs; pinned daprd/in-memory broker deliver real cloud events through the genuine controller, catalog lease and ETagActor. The ETag is persisted in owned Redis and reloads after deactivation. Precommit503, missing/invalid channel401 and signed provenance/body-binding403 controls remain. Existing actor/query/reminder/persistence/refusal checks are retained. A bounded counter awaits the second real callback emitted by the legacy projection update before the unchanged final empty-lease assertion.
3. Existing broker limitation: pinned components1.18.6 in-memory only matches exact or suffix-star prefix topics; Redis subscribes exact stream names. Neither supplies delivery for the existing embedded-star *.*.projection-changed pattern. This patch preserves SDK programmatic discovery/subscription policy. The declared exact fixture subscription is delivery proof, never a claim that programmatic wildcard delivery works.

Initial broker attempts timed out and are retained in /tmp/es-callback-live1..6 logs. The real publication counter and exact captured issuer checks ruled out a silent fail-closed issuer return before the broker source established the limitation. Temporary private-field and sidecar-log diagnostic probes are removed from the final files. The first clean rerun exposed a second-notification completion race; the final fixture observes actual completion without changing any budget, assertion or production code.

## Focused verification

All builds are isolated in /tmp/eventstore-development-pubsub-callbacks on preserved5938, Release/external package mode with explicit Version3.130.0 and the root-declared Builds props. Three affected projects build with warnings as errors and zero warnings/errors. Tests run directly as individual assemblies with exact affected-class selectors.

| Check | Passed | Failed/skipped/not-run |
| --- | ---: | ---: |
| Server callback metadata/activation/native/topic/security/notifier/catalog regressions | 184 | 0/0/0 |
| ServiceDefaults finite/opt-in/auxiliary/cancellation/GeneratedRest regressions | 95 | 0/0/0 |
| Real pinned daprd+owned broker/Redis native modes | 3 | 0/0/0 |
| Actual tracked-source scanner including every new path through alternate index | 1 | 0/0/0 |

Total283passed. Final native3completed11.160s. The unchanged consumer PE compiled against published Contracts3.129.0 runs with the source Contracts3.130.0 DLL: old four-argument constructor/Deconstruct and exact legacy JSON pass. Pinned commitlint21.2.4 accepts the exact candidate in the compact report. No commit/push is performed here. Original producers and normal index stay untouched; no nested dependency is initialized/updated and no files are deleted. All owned native resources and run directories are cleaned up.

Exact commands, actual class execution, XML/log/catalog/source/runtime hashes, ABI identity, cleanup and limits are in native-dapr-callback-verification.json beside this spec (SHA256 3d4250683d384299e96cb62b1b9999c8b72b7fca0e58bab5039d22aa1d5383f5). Parent owns integration, full hosted gates, official package verification and complete Root topology qualification.

## Exact file scope

- `_bmad-output/implementation-artifacts/native-dapr-callback-verification.json`
- `_bmad-output/implementation-artifacts/spec-qualify-native-dapr-callbacks.md`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogAuxiliaryOperation.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogDaprCallbackTransport.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogReceiverEndpoint.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogTopologyValidator.cs`
- `src/Hexalith.EventStore.Gateway/Routing/DevelopmentDaprCallbackReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentTechnicalHttpTransport.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/FiniteRouteCatalogReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/IDevelopmentDaprCallbackReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogActivationBinding.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogInvocationHandler.cs`
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs`
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/NativeCallbackRpcObserver.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NativeActorCatalogLiveTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DaprCallbackActivationBindingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DevelopmentDaprCallbackReceiverTests.cs`
