---
title: Restore catalog-compatible native hosts and real integration fixtures
status: in-review
created: 2026-10-31
baseline_commit: c02afa0fce310ccdb696232f18829df96cba088e
---

## Intent

The operator requests current SDK/packages, passing CI and an actual release. Public3.129.0 introduced mandatory executable catalog admission. Its native sample, gateway actor target inventory and six existing sidecar integration fixtures are incompatible with that requirement. Restore the native sample and real SDK test fixtures while preserving admission, authentication, signed-surface eligibility, complete inventories, transport, leases, authority/trust policy and persistence assertions. Publish an additive bug-fix SDK release only after exact-source automated gates succeed.

Reuse existing public SDK bootstrap and readonly authority seams. Native Sample Contracts must advertise the actual five Counter UI operations; Sample includes Greeting plus its internal projection operation, with unchanged target-only intent policy and compiled adapter source. Domain observers retain full owner declarations but claim only installed domain targets. Only canonical gateway-owned projection-actor queries delegate to the gateway, which verifies the actual actor/invoker before claiming its configured app identity. Expose the existing unchanged projection notification endpoint binding through a public shim so external package wrappers can preserve subscription behavior.

Sample API always registers common catalog services. Activation remains opt-in private Development composition; unenrolled hosts keep liveness but refuse readiness and generated dispatch. Preserve all existing authentication/status/payload tests using the real generated filter, immutable committed snapshots and real leases. Exercise signed schema3 UI eligibility and reject missing/agent surfaces before dispatch. Observe actual suffixed worker identity and five compiled operation/declaration digests without inventing an activation claim.

Add an explicit opt-in ServiceDefaults hosting-startup observer for genuine client-only hosts in private Development. Missing role is inert; invalid role, non-Development, incomplete bootstrap, actual installed receiver endpoints or incoming ACL grants refuse. Empty declared/target/admission/operation inventories are valid only for that demonstrated client-only role; retain actual run/scope identity and real authority lifecycle. Ordinary native/source/publish graphs remain unchanged. The five native hosted test peers may use published SDK packages only under the explicit qualified-test property; all actual restored EventStore library assets must remain one exact package cohort.

Repair the six original sidecar failures with strict immutable test-owned snapshots bound to actual run/host/app/tenant/revision. Lifecycle mutations refuse; preserve real admission/router/receiver/lease and existing default OQ8 assertions. Default OQ8 uses a private SQLite discovery namespace; explicitly configured overrides retain their coherent all-or-none boundary. Preserve all cleanup, cancellation, replay, failover and real PostgreSQL checks. No nested submodule initialization, production policy fabrication, fake generated callback, weakened assertions, broader eligibility or manual gate waiver.

## Tasks & Acceptance

- Preserve all14 published package IDs, owner default source references and production bootstrap/trust policy.
- Keep the six previously failing live fixture cases real and unskipped; retain all47 callback checks, OQ8 twelve-writer/failover/rotation/governance checks and private resources cleanup.
- Verify actual actor target inventory, malformed declarations and wrong actor/invoker/identity rejection, complete sample schema/adapter coverage and Counter result shape.
- Verify common Sample API readiness/refusal, existing HTTP authentication/status/payload contracts, actual signed schema3 surface policy and private worker observations.
- Verify real auxiliary Kestrel admission negatives and both explicit published-test and ordinary native project-reference profiles.
- Run owner Release build plus focused and catalog regression tests, exact-main CI and integration gates, then publish SDK3.129.1 and verify official archives/source. The parent must subsequently run all hosted topology tests against that actual public package cohort.

## File Scope

Allowed files for this story:

- `_bmad-output/implementation-artifacts/development-auxiliary-catalog-verification.json`
- `_bmad-output/implementation-artifacts/live-sidecar-catalog-fixture-verification.json`
- `_bmad-output/implementation-artifacts/sample-api-catalog-gate-verification.json`
- `_bmad-output/implementation-artifacts/spec-bind-live-sidecar-catalog-fixtures.md`
- `_bmad-output/implementation-artifacts/spec-development-auxiliary-catalog-clients.md`
- `_bmad-output/implementation-artifacts/spec-enroll-sample-catalog-and-actor-targets.md`
- `_bmad-output/implementation-artifacts/spec-restore-catalog-host-compatibility.md`
- `samples/Hexalith.EventStore.Sample.Api/Hexalith.EventStore.Sample.Api.csproj`
- `samples/Hexalith.EventStore.Sample.Api/Program.cs`
- `samples/Hexalith.EventStore.Sample.Api/Services/SampleApiCatalog.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj`
- `samples/Hexalith.EventStore.Sample.Contracts/Counter/CounterCatalogDeclarations.cs`
- `samples/Hexalith.EventStore.Sample.Contracts/Counter/Queries/CounterStatusPayload.cs`
- `samples/Hexalith.EventStore.Sample.Contracts/Hexalith.EventStore.Sample.Contracts.csproj`
- `samples/Hexalith.EventStore.Sample/Counter/SampleCatalog.cs`
- `samples/Hexalith.EventStore.Sample/Counter/SampleCatalogAdapters.cs`
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj`
- `samples/Hexalith.EventStore.Sample/Program.cs`
- `src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj`
- `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj`
- `src/Hexalith.EventStore.DomainService/DevelopmentDomainRouteCatalog.cs`
- `src/Hexalith.EventStore.Server/Routing/RunScopedRouteCatalogGateway.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentAuxiliaryClientCatalog.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentAuxiliaryClientCatalogHostingStartup.cs`
- `src/Hexalith.EventStore/Extensions/ProjectionNotificationEndpointExtensions.cs`
- `src/Hexalith.EventStore/Program.cs`
- `tests/Hexalith.EventStore.Sample.Tests/HttpHostSecurityTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiCatalogReadinessTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiHostMiddlewareTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApiCatalogTestServices.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApiCatalogTestStartupFilter.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApiCommittedCatalog.cs`
- `tests/Hexalith.EventStore.Sample.Tests/SampleApiGeneratedFilterFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionLiveSidecarTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionOq8PostgresqlTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Events/P1RRemediationPersistenceTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogBinding.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogRegistration.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogScope.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogStartupFilter.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionRouteCatalog.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionRouteCatalogTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveIncrementCounterIdempotencyIntentAdapter.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8BoundaryCounterStartupFilter.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8BoundaryCounterStartupFilterTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8HostingStartup.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopmentSidecar.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RouteCatalogHostedLiveTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/ProjectionSubscriptionMetadataTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DevelopmentDomainRouteCatalogTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/RunScopedProjectionCatalogTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/SampleCompiledCatalogTests.cs`
- `tests/Hexalith.EventStore.ServiceDefaults.Tests/DevelopmentAuxiliaryClientCatalogTests.cs`
- `tools/qualify-internal-actor-admission.py`
- `tools/tests/test_hosted_peer_sdk_references.py`
- `tools/tests/test_qualify_internal_actor_admission.py`

## Verification

Before combined review: six real integration failures now pass; strict fixture unit31 and Python9 pass. Catalog-focused23 plus449 catalog regression tests pass. Native sample212 pass before final common-gate correction; corrected gate31 pass with original auth/status/payload assertions preserved. Auxiliary14 real-Kestrel/negative and20 reference-profile cases pass. Detailed source-hash-bound fixture/client/API evidence is committed beside their component specs; temporary detailed logs are retained in `/tmp`. These component results do not substitute for final combined build, exact-source CI, actual root full topology or official package publication.

## Review Triage Log

Three context-free reviewers examined the complete SDK/root/Platform code packet (300216bytes) in parallel. All14 findings were individually verified and recorded in the parent adoption spec; B10 is rejected because existing real persisted Counter E2E already traverses the generated actor binding. All remaining findings concern root test composition, capability selection, ACL narrowing, prepared input binding or wrapper verification. No surviving defect concerns this SDK owner's source; the root bounded corrections proceed separately while this exact SDK source runs its mandatory hosted gates.

Combined owner verification: full Release solution build0warnings/errors (1m55), Sample218/218, ServiceDefaults95/95, catalog449/449 and strict fixture15/15 pass. Full Server6586 cases:6554passed,25existing skips,7blocked by unrelated pre-existing Tenants DCP on127.0.0.1:5000; all7 pass with `ASPNETCORE_URLS=http://127.0.0.1:0` (6.852s), preserving that unrelated process. Full Domain1294 cases:1292passed,1existing content-live skip,1subject-path precondition failure because nested Tenants remains uninitialized. Existing supported `HEXALITH_TENANTS_DOMAIN_ROOT` points to the actual initialized parent root-declared Tenants source; all37authoring guardrails pass (1.186s). No nested dependency was initialized and no source guard was weakened.

Logs and exact XML: `/tmp/es-combined-owner-release-build.log`, `/tmp/es-combined-{sample,defaults,catalog,fixtures,server-full,domain}-tests.{log,xml}`, `/tmp/es-combined-server-provenance-retry.{log,xml}`, `/tmp/es-combined-domain-guardrail-retry.{log,xml}`. These local results are not a claim of hosted CI or publication. Require exact new main push CI+commitlint and the separate integration workflow, then release/public archive verification before root qualification.
