# Post-Epic 22 ES-9: Contracts DAPR Decoupling

Status: done

Context created: 2026-05-28
Story key: `post-epic-22-es9-contracts-dapr-decoupling`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-9)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Moderate (Developer + Architect note). Public package boundary cleanup with SemVer impact.

## Story

As an EventStore package maintainer,
I want the public `Hexalith.EventStore.Contracts` package to expose query adapter wire contracts without compiling against DAPR actor assemblies,
so that downstream client/contract consumers such as Parties do not inherit DAPR actor, gRPC, or protobuf dependencies unless they intentionally host DAPR actors.

## Background and Verified Residual

ES-9 is a verified post-Epic-22 residual from the EventStore<->Parties review:

- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` currently references `Dapr.Actors` with `PrivateAssets="all"`. This suppresses NuGet transitive flow but still makes the Contracts assembly compile against DAPR actor APIs.
- The residual coupling is `src/Hexalith.EventStore.Contracts/Queries/IProjectionActor.cs`, where `IProjectionActor : Dapr.Actors.IActor`.
- `QueryEnvelope`, `QueryResult`, and `QueryAdapterFailureReason` are implementation-neutral wire/adapter DTOs and should remain in `Hexalith.EventStore.Contracts.Queries`.
- `QueryRouter` already uses the weak DAPR actor proxy path (`IActorProxyFactory.Create(...)` plus `InvokeMethodAsync<QueryEnvelope, QueryResult>(...)`) and only needs the method name `QueryAsync`, not a strongly typed `IProjectionActor` proxy.
- Current docs and tests still describe `IProjectionActor` as the DAPR actor interface and `Contracts` as depending on `Dapr.Actors`; those must be corrected with regression proof.

This story must not change query routing semantics, actor ID derivation, actor type naming, `QueryEnvelope`/`QueryResult` wire shape, DataContract namespace pinning, adapter-edge failure taxonomy, tenant/RBAC behavior, DAPR runtime topology, AppHost resources, payload logging policy, or Parties code.

## Acceptance Criteria

1. **Contracts no longer compiles against DAPR actor assemblies.**
   - Given `Hexalith.EventStore.Contracts` is restored and built
   - When its project file and assembly references are inspected
   - Then `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` contains no `Dapr.Actors`, `Dapr.Client`, `Grpc.*`, `Google.Protobuf`, or `Google.Api.CommonProtos` package reference
   - And the built Contracts assembly references no `Dapr.Actors`, `Dapr.Client`, `Grpc.*`, `Google.Protobuf`, or `Google.Api.CommonProtos` assembly
   - And no source file under `src/Hexalith.EventStore.Contracts/` contains `using Dapr.Actors`, `: IActor`, `Dapr.Actors`, `Dapr.Client`, `Grpc.`, `Google.Protobuf`, or `Google.Api.CommonProtos`
   - And the `Dapr.Actors` reference is removed, not merely hidden behind `PrivateAssets="all"`
   - And no new shared abstraction, adapter, or Contracts-adjacent package is introduced that re-exports or indirectly restores DAPR/gRPC/protobuf coupling into the public Contracts surface
   - And `QueryEnvelope`, `QueryResult`, `QueryAdapterFailureReason`, gateway DTOs, and query metadata contracts remain in `Hexalith.EventStore.Contracts.Queries`
   - And `Hexalith.Commons.UniqueIds` remains the only intentional non-BCL Contracts package dependency unless a separate architecture decision says otherwise.

2. **The public projection query method contract remains implementation-neutral.**
   - Given downstream query-serving code needs to expose the generic adapter method
   - When it references only Contracts
   - Then it can still compile against a public method contract with `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`
   - And that contract must not inherit `Dapr.Actors.IActor` or require any DAPR using
   - And existing `FakeProjectionActor` and downstream-style tests can implement the public contract without importing `Hexalith.EventStore.Server.Actors` or `Dapr.Actors`
   - And if the existing `IProjectionActor` name is retained in Contracts, its XML docs must explicitly say it is an implementation-neutral query adapter method contract, not the DAPR actor base interface
   - And if the name is not retained, an explicit migration path and replacement public type/method-name constant must be documented and tested.

3. **DAPR actor inheritance is owned by runtime/host projects, not Contracts.**
   - Given EventStore's own built-in projection actors and any DAPR-specific typed test guard need an `IActor`-based type
   - When the implementation compiles
   - Then any `: IActor` projection actor interface lives outside Contracts, under `src/Hexalith.EventStore.Server/Actors/`
   - And if a DAPR-specific projection interface is needed, create `src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs`
   - And `CachingProjectionActor` and `EventReplayProjectionActor` still expose `QueryAsync(QueryEnvelope)` for DAPR actor invocation
   - And `DefaultProjectionActorInvoker` continues to create the weak actor proxy via `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)` and invoke method name `nameof(Hexalith.EventStore.Contracts.Queries.IProjectionActor.QueryAsync)` when the Contracts interface name is retained
   - And focused test evidence proves production sends `"QueryAsync"` through `InvokeMethodAsync<QueryEnvelope, QueryResult>(...)` after creating the weak proxy with `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)`
   - And it must not switch back to `CreateActorProxy<IProjectionActor>(...)` or cast a typed proxy to `ActorProxy`
   - And `QueryRouter` cancellation propagation, actor-not-found classification, and adapter-edge failure mapping remain unchanged.

4. **Wire compatibility and public query behavior are preserved.**
   - `QueryEnvelope` and `QueryResult` keep their current `[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]` pin.
   - Data member names, nullability, constructor behavior, payload-byte encoding, `ProjectionType`, `ToString()` payload redaction, and `QueryResult.GetPayload()` behavior remain unchanged.
   - Existing DataContract round-trip tests for non-null and null `EntityId`, `QueryResult` success/failure, and namespace pinning continue to pass.
   - Public API preservation evidence covers `Hexalith.EventStore.Contracts.Queries.QueryEnvelope`, `QueryResult`, `QueryAdapterFailureReason`, and `IProjectionActor.QueryAsync(QueryEnvelope)`.
   - `QueryAdapterFailureReason` constants remain unchanged.
   - Public gateway and client contracts remain untouched except docs generated from XML comments.

5. **Docs and generated API references reflect the new package boundary.**
   - `docs/reference/nuget-packages.md` must no longer say Contracts depends on `Dapr.Actors`, nor list `Dapr.Actors` under Contracts external dependencies.
   - `docs/reference/nuget-packages.md` must explain that DAPR actor hosting projects add DAPR actor packages themselves, while Contracts provides the neutral query envelope/result/method contract.
   - `docs/reference/query-api.md` must stop calling the Contracts type a DAPR actor interface if `IActor` inheritance is removed.
   - `_bmad-output/planning-artifacts/architecture.md` must record the boundary decision: Contracts owns wire DTOs and the DAPR-free query method contract; Server/runtime owns DAPR actor inheritance and weak proxy invocation.
   - Generated API docs under `docs/reference/api/Hexalith.EventStore.Contracts/` must be refreshed or deliberately patched so they no longer show `IProjectionActor` implementing `Dapr.Actors.IActor`.
   - A migration note must state that before ES9, projection query docs described a DAPR actor contract; after ES9, Contracts defines runtime-neutral query contracts and wire DTOs, while DAPR actor packages are referenced only by hosting/adapter projects that execute those contracts through DAPR.
   - The docs must use consistent terminology: "implementation-neutral projection query contract" or "projection query method contract" for Contracts, and "DAPR actor" only for Server/host runtime adapters.
   - Any package graph diagrams or text must remain self-consistent after the change.

6. **Regression tests prove the dependency boundary and runtime path.**
   - Add or update Contracts tests that fail if the Contracts assembly references `Dapr.Actors`, `Dapr.Client`, `Grpc.Core`, `Grpc.Net.Client`, `Google.Protobuf`, or `Google.Api.CommonProtos`.
   - Add a negative source/project-file guard that fails if Contracts contains `using Dapr.Actors`, `: IActor`, or a `Dapr.Actors` package reference.
   - Update `ProjectionAdapterContractTests.IProjectionActor_IsPublicDaprActorContract` to assert the new intended contract, not `IActor` assignability.
   - Keep or add a downstream-style `FakeProjectionActor` proof that the public fake API uses Contracts types only and no Server actor namespace in its public API.
   - Update Server query tests and `DefaultProjectionActorInvokerTests` so the old typed-proxy regression guard still proves the weak path without depending on a Contracts interface that inherits `IActor`.
   - Add a docs/static test, preferably near existing documentation guard tests, that fails if docs reintroduce `Contracts` -> `Dapr.Actors` package guidance or stale phrases such as "Contracts depends on Dapr.Actors", "Contracts reference Dapr.Actors", "IProjectionActor ... DAPR actor interface", or a Contracts package table row listing `Dapr.Actors`.
   - Tests must cover both package-graph shape and source/API behavior; direct project-file grep alone is not enough.

7. **SemVer, release, and Parties handoff are explicit.**
   - The implementation must record whether this is a source-compatible but binary-relevant change or a breaking change for the Contracts package, and name the intended semantic-version consequence.
   - If retaining `IProjectionActor` without `IActor` inheritance, docs must explain that DAPR actor implementers can still implement the neutral interface while inheriting from DAPR `Actor` or another runtime actor base in their host project.
   - If moving/removing `IProjectionActor`, docs must include before/after guidance and any replacement interface/method-name contract.
   - `docs/reference/nuget-packages.md` must tell Parties-style downstream clients they no longer need DAPR packages to consume Contracts/gateway DTOs; Parties actor-host projects may still reference DAPR intentionally.
   - Record the Parties follow-up: relax the Parties `ClientArchitecturalFitnessTests` leaked-set pin after EventStore removes the Contracts DAPR dependency.

8. **Validation evidence is recorded.**
   - Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore`.
   - Run `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore`.
   - Run `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore`.
   - Run `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~QueryRouter|FullyQualifiedName~DefaultProjectionActorInvoker|FullyQualifiedName~CachingProjectionActor" --no-restore`.
   - Run `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj --no-restore`.
   - Run `dotnet build src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj --no-restore`.
   - Run a package graph proof, such as `dotnet list src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj package --include-transitive`, and record that DAPR/gRPC/protobuf packages are absent from Contracts.
   - Inspect restored dependency evidence such as `obj/project.assets.json`, a lock file, or equivalent package metadata to prove forbidden DAPR/gRPC/protobuf packages are absent from the resolved Contracts graph, not only absent from direct project-file references.
   - Run `dotnet pack src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj --no-build` and inspect the package/assembly metadata for forbidden DAPR/gRPC/protobuf dependencies, or record why packaging evidence is not available and substitute assembly metadata inspection.
   - Aspire live runtime validation is not required unless runtime/AppHost code changes beyond Server query actor type wiring, but if attempted and Docker/DAPR blocks it, record the exact blocker.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm current dependency and API shape.** (AC: 1, 2, 3, 4)
  - [x] Re-read `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`, `IProjectionActor.cs`, `QueryEnvelope.cs`, `QueryResult.cs`, and `QueryAdapterFailureReason.cs`.
  - [x] Re-read `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`, `EventReplayProjectionActor.cs`, `IProjectionWriteActor.cs`, `IETagActor.cs`, `IAggregateActor.cs`, `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs`, `IProjectionActorInvoker.cs`, and `QueryRouter.cs`.
  - [x] Re-read `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`, `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeProjectionActorTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs`, and `QueryRouterTests.cs` before editing.
  - [x] Capture the current package graph for Contracts so the before/after proof is visible in the Dev Agent Record.
  - [x] Capture whether generated API docs currently show `IProjectionActor` as implementing `Dapr.Actors.IActor`, so the docs update has a concrete before/after check.

- [x] **ST1 - Select and implement the DAPR-free public query contract shape.** (AC: 1, 2, 7)
  - [x] Preferred path: keep `Hexalith.EventStore.Contracts.Queries.IProjectionActor` as a DAPR-free public method contract containing `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`.
  - [x] Remove `using Dapr.Actors;` and `: IActor` from the Contracts interface.
  - [x] Remove `<PackageReference Include="Dapr.Actors" PrivateAssets="all" />` from `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`.
  - [x] Update XML docs to say the interface is implementation-neutral and can be implemented by test fakes, non-DAPR adapters, or adapter shims without making Contracts depend on DAPR; DAPR actor hosts mirror the same method on a runtime-owned `IActor` interface.
  - [x] If the preferred path does not compile, record the reason in the Dev Agent Record and implement the smallest alternative that keeps Contracts free of DAPR while preserving a public method-name/DTO contract.
  - [x] Treat moving or removing `IProjectionActor` as an exception path that requires explicit architecture and migration rationale, not as an equally preferred implementation.

- [x] **ST2 - Move DAPR-specific actor inheritance to runtime-owned code.** (AC: 3, 4)
  - [x] Add a Server-owned DAPR-specific interface only if compile/test guards need it, with the expected file path `src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs`; final shape is `IDaprProjectionActor : Dapr.Actors.IActor` mirroring `QueryAsync(QueryEnvelope)` because DAPR rejects actor interfaces that inherit a non-actor parent interface.
  - [x] If added, document it as a runtime/typed-proxy guard, not the downstream public package contract; if not added, record why the existing actor base shape is sufficient.
  - [x] Update `CachingProjectionActor` to implement the correct interface(s) while preserving both `QueryAsync(QueryEnvelope)` and the cancellation-aware overload.
  - [x] Update `DefaultProjectionActorInvoker` to use `nameof(Hexalith.EventStore.Contracts.Queries.IProjectionActor.QueryAsync)` or another DAPR-free method-name source; do not hardcode a stray string if a public contract type remains.
  - [x] Update typed-proxy negative assertions to use the Server-owned DAPR interface if one exists; otherwise prove weak proxy creation without a generic DAPR interface assertion.
  - [x] Keep at least one focused Server test that would fail if production returned to `CreateActorProxy<T>` or a typed-proxy-to-`ActorProxy` cast.
  - [x] Keep at least one focused Server test proving production creates the weak proxy with `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)` and invokes method name `"QueryAsync"` through `InvokeMethodAsync<QueryEnvelope, QueryResult>(...)`.

- [x] **ST3 - Preserve query DTO wire compatibility.** (AC: 4)
  - [x] Do not move `QueryEnvelope`, `QueryResult`, or `QueryAdapterFailureReason`.
  - [x] Keep the DataContract namespace pin to `Hexalith.EventStore.Server.Actors`.
  - [x] Keep `QueryEnvelope.ToString()` payload redaction and `QueryResult` helper semantics unchanged.
  - [x] Run and preserve existing round-trip tests; add one guard if any code motion risks member names/order.
  - [x] Add or preserve a public API/snapshot-style assertion covering `QueryEnvelope`, `QueryResult`, `QueryAdapterFailureReason`, and `IProjectionActor.QueryAsync(QueryEnvelope)`.

- [x] **ST4 - Add dependency-boundary tests.** (AC: 1, 6)
  - [x] Add a reflection-based Contracts assembly reference test that rejects `Dapr.Actors`, `Dapr.Client`, `Grpc.Core`, `Grpc.Net.Client`, `Google.Protobuf`, and `Google.Api.CommonProtos`.
  - [x] Add or update project-file/package graph guard coverage so `Dapr.Actors` cannot quietly return to Contracts, including the exact command `dotnet list src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj package --include-transitive`.
  - [x] Add a source guard over `src/Hexalith.EventStore.Contracts/` for `using Dapr.Actors`, `: IActor`, `Dapr.Actors`, `Dapr.Client`, `Grpc.`, `Google.Protobuf`, and `Google.Api.CommonProtos`.
  - [x] Update `ProjectionAdapterContractTests.IProjectionActor_IsPublicDaprActorContract` to the new intended assertion: no DAPR assignability, public `QueryAsync(QueryEnvelope)` remains.
  - [x] Preserve Testing fake API proofs and adjust only if the public contract shape changes.

- [x] **ST5 - Align docs, architecture, and generated API docs.** (AC: 5, 7)
  - [x] Update `docs/reference/nuget-packages.md` dependency graph description and Contracts dependency table.
  - [x] Update `docs/reference/nuget-packages.md#serving-projection-queries` so downstream actor hosts know to add their DAPR actor package intentionally, while pure gateway/contract consumers do not.
  - [x] Update `docs/reference/query-api.md` Projection Query Actor Contract wording so it describes an implementation-neutral Contracts method contract plus DAPR host shape.
  - [x] Update `_bmad-output/planning-artifacts/architecture.md` around the Projection Query Adapter Contract and package dependency table so it no longer says public Contracts owns a DAPR actor interface.
  - [x] Refresh or patch generated API docs for `Hexalith.EventStore.Contracts.Queries.IProjectionActor`.
  - [x] Add a migration note using this framing: before ES9, docs described projection query contracts as DAPR actor contracts; after ES9, `Hexalith.EventStore.Contracts` defines runtime-neutral query contracts and wire DTOs, while DAPR actor packages are referenced only by hosting/adapter projects that execute those contracts through DAPR.
  - [x] Add a docs/static regression test if there is an existing docs guard location that can assert no stale "Contracts depends on Dapr.Actors", "Contracts reference Dapr.Actors", "IProjectionActor ... DAPR actor interface", or Contracts dependency-table `Dapr.Actors` wording remains.

- [x] **ST6 - Validate and record evidence.** (AC: 8)
  - [x] Run the targeted Contracts, Testing, and Server test commands listed in AC8.
  - [x] Run the Client test command listed in AC8 so downstream gateway/contract consumers are covered.
  - [x] Run the Contracts and Server builds listed in AC8.
  - [x] Run package graph proof for Contracts and record absent DAPR/gRPC/protobuf dependencies.
  - [x] Inspect restored dependency metadata such as `src/Hexalith.EventStore.Contracts/obj/project.assets.json`, a lock file, or equivalent package metadata, and record absent DAPR/gRPC/protobuf dependencies.
  - [x] Run `dotnet pack` plus package/assembly metadata inspection, or deliberately skip it with a recorded substitute and reason.
  - [x] Update the Dev Agent Record with SemVer classification, validation results, known pre-existing failures, Docker/Aspire limitations, exact commands run, restored dependency evidence, and Parties handoff.

## Dev Notes

### Preferred Implementation Shape

Prefer this shape unless a compile break proves it wrong:

- `Hexalith.EventStore.Contracts.Queries.IProjectionActor` remains public but no longer inherits `Dapr.Actors.IActor`.
- Contracts keeps only the method contract: `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`.
- `QueryEnvelope`, `QueryResult`, and `QueryAdapterFailureReason` stay in Contracts unchanged.
- Server/runtime code owns any DAPR-specific actor inheritance. If needed, add `src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs` with `Hexalith.EventStore.Server.Actors.IDaprProjectionActor : IProjectionActor, IActor`.
- `DefaultProjectionActorInvoker` keeps weak actor invocation and gets the method name from the DAPR-free public contract: `nameof(Hexalith.EventStore.Contracts.Queries.IProjectionActor.QueryAsync)`.

This is intentionally different from simply moving the current `IProjectionActor : IActor` into Server and deleting the public interface. A pure public method contract keeps downstream source usage and Testing fakes straightforward while removing the DAPR dependency from Contracts. If implementation chooses deletion instead, document the break and migration path explicitly.

### Architecture Decision Note

Decision: keep `Hexalith.EventStore.Contracts.Queries.IProjectionActor` as a DAPR-free public method contract, rather than deleting it or moving the full projection interface into Server.

Rejected options:

- Delete `IProjectionActor`: maximizes decoupling but creates broader downstream churn and loses a stable method-name anchor for weak invocation.
- Move `IProjectionActor : IActor` into Server only: keeps DAPR semantics local but removes a useful public adapter contract from Contracts.
- Keep `PrivateAssets="all"`: hides transitive NuGet flow but leaves compile-time coupling and public API inheritance in place.

Rationale: the stable public contract is `QueryAsync(QueryEnvelope) -> QueryResult`; DAPR actor inheritance is runtime hosting infrastructure. The existing weak proxy path already invokes by actor type and method name, so Contracts can define the method shape without referencing DAPR.

### Compatibility and SemVer Note

Removing `Dapr.Actors.IActor` inheritance from a public interface is a deliberate public API change. It preserves DTO wire compatibility and is source-compatible for consumers that only call or implement `QueryAsync(QueryEnvelope)`, but it is source/binary breaking for consumers that rely on `IProjectionActor` being assignable to `IActor` or on the Contracts package carrying DAPR assemblies. The implementation must classify the release consequence explicitly and update package/migration docs accordingly.

Unless this package is governed by pre-1.0 compatibility rules, treat the inheritance removal as a major-version change for `Hexalith.EventStore.Contracts`. If pre-1.0 rules apply, release notes must still call out the source/binary break plainly and provide before/after guidance.

### Documentation Terminology

Use this vocabulary consistently:

- Contracts: "implementation-neutral projection query contract" or "projection query method contract".
- Server/host runtime: "DAPR actor", "DAPR actor adapter", or "DAPR hosting project".
- Avoid calling `Hexalith.EventStore.Contracts.Queries.IProjectionActor` a "DAPR actor interface" after the inheritance is removed.

Suggested migration wording:

> Before ES9, documentation described projection query contracts as DAPR actor contracts. After ES9, `Hexalith.EventStore.Contracts` defines runtime-neutral query contracts and wire DTOs. DAPR actor packages are referenced only by hosting/adapter projects that execute those contracts through DAPR.

### Current Files and Behaviors

`src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`

- Current state: references `Dapr.Actors` with `PrivateAssets="all"` and `Hexalith.Commons.UniqueIds`.
- Story change: remove `Dapr.Actors`; leave `Hexalith.Commons.UniqueIds`.
- Preserve: centralized package management; no `Version=` attributes in project files.

`src/Hexalith.EventStore.Contracts/Queries/IProjectionActor.cs`

- Current state: public DAPR actor interface, `IProjectionActor : IActor`, method `Task<QueryResult> QueryAsync(QueryEnvelope envelope)`.
- Story change: remove DAPR inheritance or relocate it out of Contracts.
- Preserve: method name and signature unless the implementation records a stronger migration reason.

`src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`

- Current state: DataContract DTO with pinned old Server.Actors namespace, required tenant/domain/aggregate/query/payload/correlation/user fields, optional `EntityId`, `AggregateIdentity` helper, payload-redacting `ToString()`.
- Story change: none expected except XML docs if needed.
- Preserve: DataContract namespace, DataMembers, nullability, validation, payload redaction.

`src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`

- Current state: DataContract DTO with success, optional payload bytes/error/projection type, payload helpers, failure guard.
- Story change: none expected except XML docs if needed.
- Preserve: DataContract namespace, helper semantics, adapter-edge failure behavior.

`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`

- Current state: extends DAPR `Actor`, implements Contracts `IProjectionActor`, serves `QueryAsync(QueryEnvelope)`, includes cancellation-aware internal overload, ETag cache, projection type discovery, no payload logging.
- Story change: update implemented interface(s) after Contracts no longer inherits `IActor`.
- Preserve: cache behavior, cancellation behavior, projection type validation, logging policy, `ExecuteQueryAsync` overload behavior.

`src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs`

- Current state: uses `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)` and `ActorProxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)`.
- Story change: keep weak path; update `nameof(...)` import if `IProjectionActor` changes namespace/meaning.
- Preserve: no `CreateActorProxy<T>` production path, request cancellation token, argument guards.

`src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs`

- Current state: does not exist.
- Story change: create only if a Server-owned typed DAPR projection actor interface is needed for compile/test/runtime shape.
- Preserve: this must remain outside Contracts and must not become the downstream public package contract.

`tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`

- Current state: imports `Dapr.Actors` and asserts `typeof(IActor).IsAssignableFrom(typeof(IProjectionActor))`.
- Story change: remove DAPR using and assert the opposite boundary plus method availability.
- Preserve: DataContract round-trip, namespace pin, failure reason constants.

`src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs`

- Current state: implements public `IProjectionActor`, records `QueryEnvelope`, returns `QueryResult`, has cancellation-aware helper, no Server actor import.
- Story change: should continue compiling without DAPR through Contracts.
- Preserve: public fake API and tests proving no Server actor namespace leaks.

### Must Preserve

- Do not remove or rename `QueryEnvelope`, `QueryResult`, `QueryAdapterFailureReason`, `SubmitQueryRequest`, or `SubmitQueryResponse`.
- Do not change actor ID routing, `ProjectionActorType`, `ProjectionType`, payload checksum routing, or `QueryRouter.ProjectionActorTypeName`.
- Do not alter Story R22A1 weak proxy behavior. Typed DAPR proxy regression tests must still fail if production starts using `CreateActorProxy<T>` again.
- Do not log query payload bytes, projection payload bytes, protected data, DAPR addresses, or stack traces into client-facing responses.
- Do not add a new DAPR package reference to Contracts under another name or through a new shared project.
- Do not introduce a new shared abstraction, adapter, or helper package that makes Contracts consumers inherit DAPR/gRPC/protobuf dependencies indirectly.
- Do not leave stale docs saying `Contracts` depends on `Dapr.Actors` or that the Contracts `IProjectionActor` is a DAPR actor interface.
- Do not update Parties in this repo; record the handoff only.
- Do not initialize nested submodules.

### Party-Mode Review Hardening

Party-mode review on 2026-05-28 reached consensus that ES9 is directionally correct and ready after story hardening:

- Winston: architecture boundary is right; name the compatibility break plainly, keep DataContract pins, and make the DAPR-free finish line mechanical.
- Amelia: add the exact Server-owned DAPR interface path, preserve `nameof(IProjectionActor.QueryAsync)` weak invocation, add negative source guards, and include Client tests.
- Murat: require package/assembly proof, public API/source proof, focused Server weak-path tests, docs/API reference evidence, and precise validation logs.
- Paige: use runtime-neutral terminology, add a migration note, update generated API docs, and guard against stale "Contracts depends on Dapr.Actors" wording.

### Previous Story Intelligence

- Story 22.2 established the public projection adapter DTOs in Contracts and pinned DataContract namespace compatibility after review found wire-namespace drift. ES9 must keep that wire decision.
- R22A1 fixed a runtime NRE by replacing typed projection actor proxy creation with weak `ActorProxy` invocation. ES9 must preserve weak proxy creation even if the public interface shape changes.
- ES6 added typed DAPR actor-not-found detection around the existing `IProjectionActorInvoker` seam. ES9 must not reopen actor-not-found behavior.
- ES7 and ES8 both used docs/static tests to prevent architecture drift. ES9 needs the same style of executable package/docs guard because package graphs drift quietly.
- ES8 should not be reopened; domain-service resolver key formats and fallback order are out of scope.

### Git Intelligence

Recent commits show this cluster prefers narrow, evidence-backed corrections:

- `ffe6cc0d fix(server): harden domain resolver docs` - ES8 completed docs/tests/code alignment around a narrow contract.
- `3f5758b1 fix(apphost): align statestore yaml source` - ES7 made one source-of-truth decision and guarded it with tests/docs.
- `3f301470 fix(server): harden actor not-found detection` - ES6 hardened query-router failure classification without changing public query contracts.
- `d0b70239` and `a3d80d98` only updated subproject commits; ignore unrelated dirty submodule state unless it blocks builds.

### Latest Technical Information

- Local package source of truth is `Directory.Packages.props`: DAPR packages are `1.17.9`, .NET target is `net10.0`, xUnit v3 is `3.2.2`, Shouldly is `4.3.0`, and NSubstitute is `5.3.0`.
- NuGet lists Dapr.Actors/Dapr.Actors.AspNetCore latest stable as `1.17.9` published 2026-04-18. Dapr.Actors.AspNetCore brings Dapr.Actors plus gRPC/protobuf dependencies, which is exactly the dependency tail ES9 must keep out of Contracts.
- Official DAPR actor docs describe `IActorProxyFactory` as the ASP.NET Core/DI actor client factory and `CreateActorProxy<T>` as the strongly typed client path. EventStore's QueryRouter must keep its weak `IActorProxyFactory.Create(...)` path because R22A1 proved typed dispatch proxies are the wrong primitive for weak `InvokeMethodAsync` plus request cancellation.
- Older official DAPR actor how-to docs state strongly typed actor interfaces inherit `Dapr.Actors.IActor`; ES9 deliberately moves that requirement out of Contracts and into DAPR-hosting/runtime code.

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Treat warnings as build-breaking.
- Use centralized package management; do not add package versions directly to project files.
- Keep dependencies flowing inward: `Contracts` has no Hexalith dependencies except the approved Commons unique ID package; Server/runtime/host projects own DAPR.
- Validate with targeted test projects individually.
- Prefer official Aspire, Microsoft, DAPR, and NuGet docs for version-sensitive infrastructure.

### Aspire Baseline

Before creating this story, the repository Aspire baseline was attempted on 2026-05-28:

- Command: `$env:EnableKeycloak='false'; aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json`
- Result: AppHost build succeeded with 0 warnings and 0 errors.
- Aspire exited with code 2 because Docker is not running or not installed. Log path: `C:\Users\quent\.aspire\logs\cli_20260528T080155964_detach-child_e4db1ae3c5f048399e9cf3752048e006.log`.
- Aspire MCP tools were searched for in this Codex session and were not available.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-9`] - verified ES9 residual and Parties handoff.
- [Source: `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`] - public query adapter DTO ownership, DataContract compatibility, Testing fake decoupling, and query docs baseline.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix.md`] - weak ActorProxy invocation requirement and regression-sensitive test pattern.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es6-projection-actor-not-found-typed-check.md`] - actor-not-found classifier guardrails around the current query invoker seam.
- [Source: `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`] - current Contracts package references.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/IProjectionActor.cs`] - current DAPR-coupled interface.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`] - public query envelope DTO and DataContract pin.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs`] - public query result DTO and DataContract pin.
- [Source: `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`] - built-in query actor base and cache behavior.
- [Source: `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs`] - weak DAPR proxy invocation path.
- [Source: `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`] - Contracts query adapter tests needing dependency-boundary update.
- [Source: `src/Hexalith.EventStore.Testing/Fakes/FakeProjectionActor.cs`] - public fake that should remain DAPR-free.
- [Source: `docs/reference/query-api.md`] - public query adapter docs needing wording update.
- [Source: `docs/reference/nuget-packages.md`] - package dependency graph needing correction.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-Query-Adapter-Contract`] - architecture boundary needing ES9 note.
- [External: DAPR actor .NET client docs](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-client/) - strong vs weak actor client context.
- [External: NuGet Dapr.Actors.AspNetCore 1.17.9](https://www.nuget.org/packages/Dapr.Actors.AspNetCore/1.17.9) - DAPR/gRPC/protobuf dependency tail.
- [External: NuGet DAPR package profile](https://www.nuget.org/profiles/dapr.io) - DAPR package current version family.

### Review Findings

- [x] [Review][Patch] Broaden dependency guards and refresh tracked Contracts dependency evidence [tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs:11] -- The guard list checks exact names such as `Grpc.Core` and `Grpc.Net.Client`, so `Grpc.Core.Api` and `Grpc.Net.Common` can re-enter while AC1's `Grpc.*` ban still appears green. The tracked `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj.lscache` also still records `Dapr.Actors`, `Dapr.Client`, `Google.Protobuf`, `Grpc.Core.Api`, `Grpc.Net.Client`, and `Grpc.Net.Common`, contradicting the dependency-evidence story.
- [x] [Review][Patch] Correct DAPR-host guidance and story notes to the final mirror-only actor contract [docs/reference/query-api.md:41] -- The docs say projection adapters should implement the neutral `IProjectionActor`, while the sample and implementation use a DAPR-owned `IActor` interface that mirrors `QueryAsync`. This contradicts the dev-recorded DAPR limitation and the still-active AC7 wording; docs and story notes should distinguish non-DAPR adapters that implement `IProjectionActor` from DAPR actor hosts that mirror the method on an `IActor` interface.
- [x] [Review][Patch] Refresh generated Testing API docs for `FakeProjectionActor` [docs/reference/api/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.Fakes.FakeProjectionActor.md:11] -- The generated docs still show `FakeProjectionActor : Hexalith.EventStore.Server.Actors.IProjectionActor, Dapr.Actors.IActor` and Server-owned `QueryEnvelope`/`QueryResult`, while the source now uses `Hexalith.EventStore.Contracts.Queries`; this leaves public docs contradicting the ES9 package boundary and fake API proof.
- [x] [Review][Patch] Add reflection coverage that the DAPR mirror interface stays aligned with the public contract [src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs:16] -- Because `IDaprProjectionActor` cannot inherit the neutral `IProjectionActor`, future signature drift is easy: weak invocation still sends `QueryAsync`, but runtime actors could expose a mismatched parameter or return type. Add a Server test comparing method name, return type, and parameter types against `IProjectionActor.QueryAsync`.
- [x] [Review][Patch] Prove the weak invoker sends `QueryAsync` through `InvokeMethodAsync<QueryEnvelope, QueryResult>` [tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs:40] -- Current tests prove `IActorProxyFactory.Create(...)` and absence of typed proxy creation, but they stop at the substitute NRE and do not prove the method name/generic weak invocation required by AC3.
- [x] [Review][Patch] Split unrelated workspace changes out of the ES9 review set [src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css:29] -- The diff includes Admin UI reconnect CSS and root submodule pointer updates for `Hexalith.AI.Tools`, `Hexalith.Commons`, and `Hexalith.Tenants`, none of which are part of the Contracts/DAPR decoupling file list or story scope.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-28 ST0 baseline: `aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json` built AppHost with 0 warnings/errors, then exited code 2 because Docker is not running or installed. Log: `C:\Users\quent\.aspire\logs\cli_20260528T082405341_detach-child_2ba5cc0f73a74137b27b7fe740d93aa8.log`. Aspire MCP tools were searched for and not available in this Codex session.
- 2026-05-28 ST0 before graph: `dotnet list src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj package --include-transitive` showed direct `Dapr.Actors 1.17.9` and transitive `Dapr.Client`, `Google.Api.CommonProtos`, `Google.Protobuf`, `Grpc.Core.Api`, `Grpc.Net.Client`, and `Grpc.Net.Common`.
- 2026-05-28 red phase: focused `ProjectionAdapterContractTests` failed on DAPR assignability, Contracts project/package/assets/source guards, assembly reference guard, and stale docs wording.
- 2026-05-28 implementation nuance: preferred `IDaprProjectionActor : IProjectionActor, IActor` shape was attempted, but focused Server tests proved DAPR runtime rejects actor types with any implemented interface hierarchy containing a non-actor parent. Final Server-owned `IDaprProjectionActor` derives from `IActor` and mirrors `QueryAsync(QueryEnvelope)`; Contracts remains the method-name/source contract via `nameof(IProjectionActor.QueryAsync)`.
- 2026-05-28 package proof: first `dotnet pack --no-build` found a stale Release package assembly still referencing `Dapr.Actors`; `dotnet build src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj -c Release --no-restore` followed by `dotnet pack ... -c Release --no-build` produced clean nuspec and package assembly metadata.

### Completion Notes List

- Removed `Dapr.Actors` from `Hexalith.EventStore.Contracts` and changed `Hexalith.EventStore.Contracts.Queries.IProjectionActor` into an implementation-neutral `Task<QueryResult> QueryAsync(QueryEnvelope envelope)` method contract.
- Added Server-owned `IDaprProjectionActor` for runtime DAPR actor typed-proxy guard surfaces, with `CachingProjectionActor` implementing it while preserving existing `QueryAsync(QueryEnvelope)` and cancellation-aware overload behavior.
- Preserved weak actor invocation through `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)` and `ActorProxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)`.
- Added Contracts regression guards for forbidden assembly references, direct package references, resolved assets graph entries, source coupling tokens, public API shape, docs stale wording, and Contracts dependency-table drift.
- Updated Server weak-path tests to guard against `CreateActorProxy<IDaprProjectionActor>(...)` while keeping production on the weak proxy path.
- Updated package docs, query API docs, architecture notes, and generated Contracts API docs to use implementation-neutral projection query terminology and record the SemVer consequence.
- SemVer classification: source-compatible for consumers that only call or implement `QueryAsync(QueryEnvelope)`, but source/binary breaking for consumers that rely on `IProjectionActor` being assignable to `Dapr.Actors.IActor` or on Contracts carrying DAPR assemblies; treat as a major-version change unless pre-1.0 rules apply.
- Parties handoff recorded: relax the Parties `ClientArchitecturalFitnessTests` leaked-set pin after consuming an EventStore build with this Contracts DAPR dependency removal.
- Validation evidence: Contracts package graph now resolves only `Hexalith.Commons.UniqueIds` directly and `ByteAether.Ulid` transitively; restored assets, built Contracts assembly, nuspec, and packaged assembly contain no forbidden DAPR/gRPC/protobuf dependencies.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es9-contracts-dapr-decoupling.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Queries.IProjectionActor.md`
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Queries.md`
- `docs/reference/api/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.Fakes.FakeProjectionActor.md`
- `docs/reference/nuget-packages.md`
- `docs/reference/query-api.md`
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj.lscache`
- `src/Hexalith.EventStore.Contracts/Queries/IProjectionActor.cs`
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
- `src/Hexalith.EventStore.Server/Actors/IDaprProjectionActor.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`

## Verification Status

Implementation complete; review patches applied and validated.

- PASS: review patch validation `dotnet test tests\Hexalith.EventStore.Contracts.Tests\Hexalith.EventStore.Contracts.Tests.csproj --no-restore` (520 passed).
- PASS: review patch validation `dotnet test tests\Hexalith.EventStore.Testing.Tests\Hexalith.EventStore.Testing.Tests.csproj --no-restore` (144 passed).
- PASS: review patch validation `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~QueryRouter|FullyQualifiedName~DefaultProjectionActorInvoker|FullyQualifiedName~CachingProjectionActor" --no-restore` (87 passed).
- PASS: review patch validation `dotnet list src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj package --include-transitive` shows only direct `Hexalith.Commons.UniqueIds 2.16.0` and transitive `ByteAether.Ulid 1.3.7`.

- PASS: `dotnet test tests\Hexalith.EventStore.Contracts.Tests\Hexalith.EventStore.Contracts.Tests.csproj --no-restore` (520 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Client.Tests\Hexalith.EventStore.Client.Tests.csproj --no-restore` (399 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Testing.Tests\Hexalith.EventStore.Testing.Tests.csproj --no-restore` (144 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~QueryRouter|FullyQualifiedName~DefaultProjectionActorInvoker|FullyQualifiedName~CachingProjectionActor" --no-restore` (85 passed).
- PASS: `dotnet build src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj --no-restore` (0 warnings, 0 errors).
- PASS: `dotnet build src\Hexalith.EventStore.Server\Hexalith.EventStore.Server.csproj --no-restore` (0 warnings, 0 errors).
- PASS: `dotnet list src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj package --include-transitive` shows only direct `Hexalith.Commons.UniqueIds 2.16.0` and transitive `ByteAether.Ulid 1.3.7`.
- PASS: `src\Hexalith.EventStore.Contracts\obj\project.assets.json` resolved target/library entries contain no `Dapr.Actors`, `Dapr.Client`, `Grpc.Core`, `Grpc.Net.Client`, `Google.Protobuf`, or `Google.Api.CommonProtos`.
- PASS: `dotnet pack src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj -c Release --no-build` after Release build; nuspec and packaged assembly references contain no forbidden DAPR/gRPC/protobuf dependencies.
- BLOCKED runtime only: Aspire live run cannot start resources because Docker is not running or installed; no AppHost/runtime code changed.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-28 | 1.0 | Implemented ES9 Contracts DAPR decoupling, Server-owned DAPR projection interface, dependency/docs guards, generated API doc updates, package proof, and validation evidence. | Codex |
| 2026-05-28 | 0.3 | Applied advanced elicitation hardening for ADR framing, indirect dependency non-goal, restored dependency proof, SemVer consequence, and weak invocation evidence. | Codex |
| 2026-05-28 | 0.2 | Applied party-mode review hardening for explicit Server DAPR interface location, compatibility/SemVer notes, negative dependency and docs guards, package/assembly evidence, generated API docs, and migration wording. | Codex |
| 2026-05-28 | 0.1 | Created ready-for-dev ES9 story for removing DAPR actor dependencies from Contracts while preserving public query wire DTOs, weak actor invocation, docs, package graph evidence, and Parties handoff. | Codex |

## Story Completion Status

Implementation complete; review patches applied and validated. Status: done.
