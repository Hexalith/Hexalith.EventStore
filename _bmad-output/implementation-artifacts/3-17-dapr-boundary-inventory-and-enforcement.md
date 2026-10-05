---
title: Dapr Boundary Inventory And Enforcement
story_id: '3.17'
story_key: 3-17-dapr-boundary-inventory-and-enforcement
epic: '3'
status: backlog
created: 2026-10-05
updated: 2026-10-05
owner: Platform architecture and packaging maintainer
supporting_requirements: [FR5, FR8, FR32, FR34, NFR12, NFR17]
context:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-10-05.md
  - docs/concepts/dapr-infrastructure-boundary.md
  - docs/architecture/dapr-infrastructure-exceptions.yaml
  - tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs
---

# Story 3.17: Dapr Boundary Inventory And Enforcement

As a maintainer, I want runtime infrastructure dependencies mapped to Dapr capabilities or explicit exceptions, so new work cannot silently introduce provider coupling.

## Scope And Dependencies

This backlog specification owns the final operation/dependency inventory and deterministic guard in the existing Contracts architecture/packaging test lane. It supports PRD §8.4, FR5/FR8/FR32/FR34, NFR12/NFR17 and AD-1/AD-9/AD-11/AD-12. Existing FR/NFR primary owners and readiness failures remain unchanged. It implements no guard, migration, exception approval, package choice, runtime/topology change, or provider provisioning in the current planning slice.

Dependencies: the approved boundary policy and current architecture/packaging evaluation lane, including `DependencyModeEvaluationTests.cs`. Story 3.16 dependency refresh must consult the inventory/policy before accepting an infrastructure change. Stories 2.13 and 8.6 supply later qualification decisions; they do not block initial inventory/guard work or create cycles. Carry their unresolved rows with owners and follow-up triggers. Story 1.11's domain guardrails are inputs with documented limits, not whole-platform enforcement.

## Inventory Contract

For each operation retain a stable row ID, owning runtime/tooling project, exact source and configuration paths, evaluated dependency/call-site provenance, classification, required correctness/security/compatibility/operational guarantees, Dapr API/component candidate, exact runtime/client/component profile, qualification commands/results/artifact identities, disposition, owner/owning story, and review/removal trigger. Record raw provider HTTP and credential/configuration integration even if no provider package appears.

Classifications must distinguish shipped runtime, public client transport, telemetry transport, provisioning/orchestration, explicit-purpose test setup/fault injection/physical diagnostics, approved exact-purpose exceptions, and unresolved cases. A component name or central package-catalog row supplies neither runtime-use evidence nor qualification. A reference under `tests/` or a provider namespace cannot create a global exemption.

## Acceptance Criteria

### AC1 — Evaluated Graph And Compiled Input Coverage

**Given** all production libraries, hosts, samples, package boundaries, and generated-host inputs, **when** inventory runs, **then** it evaluates Debug/source and Release/package intent plus supported feature/configuration branches, recursively follows project and direct/transitive package edges, records resolved assets/TFMs and provenance, and inspects effective `Compile` inputs including linked, conditional, and generated source.

**And** Gateway's linked `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, shared/transitive integration packages, generator templates and runnable emitted-host inputs cannot disappear through source-folder assumptions. Restore/evaluation gaps, stale assets, unavailable optional graph branches, reflection/dynamic configuration, and generated inputs not observed are named unresolved limits, not silently conformant exclusions.

### AC2 — Operations, Configuration, And HTTP Bypasses

**Given** an evaluated runtime integration, **when** its source/configuration is classified, **then** the row maps the actual operation and required guarantees to a Dapr API/component or approved exact-path exception; credentials, connection strings, provider endpoint discovery, raw HTTP requests, provider-schema/SQL use, and client factories are inspected alongside package/type names.

**And** generic bindings carrying application-owned SQL/protocols do not override actor state ownership or establish portability. Actor-owned state remains addressed through its actor boundary; no permission to access Dapr private keys/tables/cache or direct database product evidence for 6.6 is introduced.

### AC3 — Deterministic Guard And Accepted-Exception Validation

**Given** the final inventory and exception schema, **when** the existing Contracts architecture/packaging lane runs, **then** a deterministic guard rejects prohibited runtime provider dependencies/call sites/configuration, missing inventory rows in its declared coverage, and invalid/stale/overbroad exceptions with actionable paths and safe reason codes.

**And** accepted exceptions bind operation, missing guarantee, exact-profile gap evidence and alternatives, isolated adapter, owner, exact allowed paths, architecture-owner decision/evidence, and review/removal trigger. Reject duplicates, unknown fields/dispositions, missing decisions/evidence/owners, unrelated scope, and decisions invalidated by their trigger. The currently empty accepted register remains empty until a separately reviewed decision exists. Unresolved rows cannot be represented as accepted exceptions or full conformance.

### AC4 — Guard Negatives And Non-Leaking Tooling Scope

**Given** representative isolated fixtures, **when** each prohibited integration is introduced, **then** the guard fails for a direct database driver, broker/cloud-provider integration, transitive provider integration, linked source compiled into a shipped project, generated-host provider code, direct-provider HTTP with no SDK reference, and application provider credentials where Dapr supplies the operation.

**Given** valid Dapr usage and narrowly scoped orchestration/test tooling or a separately approved exact-path exception fixture, **when** the same guard runs, **then** it passes only that intended scope. A file moved under `tests/` but compiled/packed into runtime, a provider-namespace allowlist, linked/generated escape, or expanded exception path must fail. Use synthetic exception records for validator unit fixtures; do not add them to the accepted production register.

### AC5 — Credential And Profile Boundary

**Given** supported profiles and runtime options, **when** configuration/credential flow is evaluated, **then** application provider connection strings, endpoints, secrets, token acquisition, and direct client construction are prohibited where a qualified Dapr operation supplies the capability; deployment/component credentials and scopes remain separately governed.

**And** deliberate fault-injection or provisioning identity is purpose/path-bounded and cannot enter application settings, generated hosts, runtime package assets, or shared client factories. Never fall back to provider HTTP/SDK credentials after a Dapr failure. Public HTTP/browser SignalR and diagnostic OTLP remain explicitly classified transports under their authorization/redaction contracts rather than blanket network exemptions.

### AC6 — Published Inventory, Unresolved Work, And Analysis Limits

**Given** the guard and final inventory are published, **when** conformance is assessed, **then** the guide/register, evaluated project/input matrix, operation/evidence rows, deterministic negatives, exact commands/results/tool/source identities, and explicit analysis limits agree.

**And** every unresolved row has an owner/story/trigger, including retained direct Redis under 2.13 and planned key operations under 8.6/draft amendment. Partial analysis can prevent new bypasses while known unresolved work remains, but cannot label the entire runtime conformant. Explain limitations for dynamic/reflection/native dependencies, external package internals, configuration-generated endpoints, runtime egress, and unobserved graph branches; static scans cannot prove complete network enforcement.

## Implementation Tasks

- [ ] Use `Hexalith.EventStore.slnx`, `tools/release-packages.json`, production/sample `.csproj` files, and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs` to enumerate/evaluate the closed project/package/generated-host and mode/configuration universe; retain exact graph/input identities and gaps in new `_bmad-output/implementation-artifacts/evidence/story-3-17/evaluated-inputs.json`.
- [ ] Inspect effective/linked/generated inputs from `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj`, generator inputs under `src/Hexalith.EventStore.RestApi.Generators/`, host/sample configuration, and each evaluated transitive graph; record provider HTTP/client/credential call-site paths in `docs/concepts/dapr-infrastructure-boundary.md` and new `docs/architecture/dapr-infrastructure-inventory.yaml`.
- [ ] Refine `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-inventory.yaml`, and `docs/architecture/dapr-infrastructure-exceptions.yaml` to the final operation/evidence/owner/disposition schema, preserving unresolved 2.13/8.6 rows and accepting no exception without its separate exact-content decision.
- [ ] After graph inspection, specify and implement the narrow deterministic guard in new `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DaprInfrastructureBoundaryTests.cs`, reusing `DependencyModeEvaluationTests.cs`/the existing lane; record any required separate helper type/file choice in `_bmad-output/implementation-artifacts/evidence/story-3-17/inventory-and-guard-verification.md` before adding it.
- [ ] In `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DaprInfrastructureBoundaryTests.cs`, enforce the final inventory/exception fields and runtime/tooling/compiled-input scope, reject invalid/stale/overbroad decisions, and emit safe exact-path diagnostics; publish the accepted schema in `docs/architecture/dapr-infrastructure-exceptions.yaml`.
- [ ] Add isolated negative/positive fixtures under `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/` with exact filenames enumerated in the verification packet; use `DaprInfrastructureBoundaryTests.cs` to prove database/broker/transitive/linked/generated/raw-HTTP/credential failures and Dapr/isolated-tooling/synthetic-exception positives, including test-file-to-runtime leakage. Synthetic exceptions never enter the accepted production register.
- [ ] Retain exact commands/results, unresolved 2.13/8.6 migrations, declared coverage denominator, analysis limits and reviewer disposition in `_bmad-output/implementation-artifacts/evidence/story-3-17/inventory-and-guard-verification.md`; synchronize the guide/register, this story and `sprint-status.yaml` only after the applicable completion gates pass, without claiming whole-runtime conformance while unresolved rows remain.

## Verification Handoff

Build `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` individually and invoke its built executable; use xUnit v3 `-class`/`-method` filters for focused guard/evaluation work, then run the complete Contracts executable for the changed lane. Record exact evaluated MSBuild commands, property matrix, assets/compiled-input identities, fixture outcomes, and deterministic ordering. No test is implemented or claimed passed by this planning artifact.

The guard must fail reproducibly on the declared negatives and identify its coverage denominator. If evaluation/restore or a required branch is environment-blocked, record the exact command/result and retain narrower evidence separately without suppressing that branch. Live/profile correctness and security/compatibility remain the owning stories' gates; a passing packaging guard cannot replace them.
