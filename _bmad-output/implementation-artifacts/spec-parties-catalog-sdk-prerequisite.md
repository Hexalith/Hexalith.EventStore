---
title: Qualify the finite Parties catalog SDK prerequisite
status: locally-verified-awaiting-protected-ci
baseline_commit: 6b9004d942e280724ca5bf554f3862ab23af68a0
story_key: parties-catalog-sdk-prerequisite
---

# Finite release prerequisite

Preserve the complete qualified native catalog/Admin/callback/Direct-notifier work
through 6b9004d94 and add only the selected Parties compatibility dependencies.
PartiesServiceCollectionExtensions.AddHexalithParties calls AddHexalithTenants;
Tenants.Client then registers AddEventStoreDomainEvents. The real SDK subscription
must therefore be observed and fenced even though Parties does not call that
registration directly. ResolvePartyIdentity and the other owner identity readers
are compiled internal schema3 queries and still require their configured exact
identity-admission proof. Signed owner-authority queries retain their configured
caller/query allowlist, request proof, service surface and actual action binding.

The prerequisite is intended to merge normally before PR377. All PR377 Works
DomainAuxiliary/Operations/HA/restart/resume changes, PR378 stream helper, their
private workspaces and bounded review fixes remain preserved on their branches.
Their complete four-logical-role and physical-HA source/private-package evidence
is pending and is not claimed by this prerequisite. No Works HTTP trie, auxiliary
extractor, executable-replica digest, Operations witness or stream helper is in
this file scope. No new production trust, eligibility or authority policy is added.

## File Scope

- `_bmad-output/implementation-artifacts/spec-parties-catalog-sdk-prerequisite.md`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogAuxiliaryOperation.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogReceiverEndpoint.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogSidecarTransport.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogTopologyValidator.cs`
- `src/Hexalith.EventStore.DomainService/DomainRouteCatalogSidecarMetadata.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`
- `src/Hexalith.EventStore.DomainService/RouteCatalogDomainObservation.cs`
- `src/Hexalith.EventStore.Server/Commands/DaprCommandCorrelationIndex.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/FiniteRouteCatalogReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogActivationBinding.cs`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore/Pipeline/RouteCatalogEligibilityBehavior.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainEventsEndpointExtensionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.ProtectedCatalogIdentity.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/BoundOwnerCatalogAdmissionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/BoundOwnerCatalogQueryHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/CatalogRemoteAdapterCancellationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DomainSidecarCatalogLeaseTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DomainSidecarLeaseTestHost.cs`

## Behavior and constraints

The SDK-created subscription witness binds the actual delegate, concrete POST,
channel policy, configured component/topic and actual Dapr topic metadata, including
programmatic/declarative mode. Schema3 activation compares the actual finite
receiver witness. Actual native delivery then acquires the local committed lease,
compares its exact subscription binding and enters the existing remote-call scope.
No synthetic workload Root/Epoch stamps are required for native pub/sub. Missing
commit, pending activation or changed subscription metadata refuses before business
processing. Ambiguous dispatched work retains its durable fence; known completion
releases normally. Existing channel/envelope/handler checks remain effective.

The query compatibility slice uses the existing exact identity-admission authority
and a private in-flight owner marker bound to the actual context/request/principal,
endpoint, payload hash and captured root/epoch. Copied markers, changed payload or
scope, unsupported caller and unconfigured or empty proof refuse. Four already
wrapped actual Dapr boundaries now pass the caller cancellation token to the
existing remote fence, preventing pre-cancelled requests from enrolling or calling.

## Verification

All work and producer outputs are in the isolated prerequisite checkout; original
SDK/Root producers and nested submodules are untouched. Only ordinary selected
Builds a22 source is used for compilation. The release-workflow tests explicitly
read a separate exact-a22 standalone Builds clone through their existing supported
source override; no nested dependency is initialized.

- Before query/pre-cancel source fixes: 38 cases, 13 failures, 25 passes.
  After fixes: all38 pass, zero skips.
- Before Sidecar committed-lease guard: seven real HTTP cases, four failures.
  Actual mapper, processor, channel auth and real CAS authority pass afterward.
  The final twelve cases also cover topic/component/code/method/operation drift,
  exact pending rollback, pending actual remote work and uncertain completion.
- Same actual delegate with seven altered topic metadata forms: all seven fail
  before the witness correction; all17 subscription mapping/metadata cases and
  six existing HTTP/channel security cases pass afterward.
- Topic/component replacement metadata explicitly retains the actual SDK
  EnableRawPayload=false value. Removing only the component/topic comparisons
  now produces exactly two failures among17 cases; restoring them gives17/17.
  The unrelated flag cannot mask a missing topic-specific validation anymore.
- Normal isolated whole-solution Release build: zero warnings/errors.
- Exact final routing/query/notifier suite: 643 pass, zero skips.
- Exact real native actor/callback suite: three pass, zero skips.
- Source header ownership: four pass, zero skips.

Logs and XML are /tmp/es-parties-prereq-{server,domain,client,native}.*;
red logs are /tmp/es-parties-prereq-query-cancel-red.log and
/tmp/es-parties-prereq-topic-red.log. The Sidecar original red is
/tmp/es377-sidecar-red.log. ABI and release packaging proof are finalized in the
local /tmp/es-parties-prereq-verification.json receipt before commit.

An initial whole-solution --artifacts-path build collided on the two existing
Sample.Tests project basenames; the normal per-project bin/obj build passes.
An overbroad packaging namespace probe encountered intentionally absent Git
metadata in ordinary dependency snapshots; only the required exact workflow
inventory suite is rerun with its supported explicit exact-source override.
Neither environment issue was concealed by changing product code or validators.

The complete exact-head protected PR CI and Integration workflows, normal merge,
fresh-main CI/commitlint, protected Release and fourteen official source-matching
NuGet archives remain mandatory before publication is claimed. Actual public SDK
Root runtime qualification follows publication and retains all existing assertions.
