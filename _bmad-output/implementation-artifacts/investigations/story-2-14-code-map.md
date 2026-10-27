# Story 2.14 implementation investigation

Investigated 2026-10-25 at EventStore HEAD `3ef316a14d74dac429adf13f105e1858082a4020`. This is planning context, not runtime proof or migration approval. No source or submodule edits were made. Paths below are repository-relative; `T` means `references/Hexalith.Tenants`.

## Governing constraints and prerequisites

- `_bmad-output/planning-artifacts/epics.md:2052` defines Story 2.14, its 2.12/5.12 dependencies, owner-approved migration, and compiled/runtime proof obligations.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` records 2.12 done, 2.14 backlog, 5.12 backlog. No `deploy/dapr/eventstore-routing-catalog.json` or Contracts catalog schema/codec was found. Existing unrelated catalogs are not AD-33 substitutes.
- `_bmad-output/planning-artifacts/architecture.md:353` defines AD-27: explicit tenant or platform scope; grants never select scope; lowercase then validate 1–64 characters against `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`; reject public `system`; platform namespace is disjoint from tenant grammar and derives from Contracts/catalog declarations.
- Architecture AD-33 adopts credential kinds and operations. Older Story 5.12 prose still calls these assumptions; current architecture governs. Its open-issues row still leaves vocabulary ownership, the human operation claim type, and cataloging ingress endpoints for the owner to decide before 5.12 freezes.
- Architecture AD-12 and `spec-evidence-storage-simplification.md` supersede deleted authorization records/validators with story/spec Verification plus CI. Do not require or recreate the deleted Story 9.1 tool merely because older story text references it. Remaining assurance/production restrictions still apply.
- Story 2.13 is done under the accepted October 25 Redis exception. Preserve its transport/metadata behavior; its historical qualification failures do not become 2.14 completion evidence.

## EventStore source map

| File | Relevant behavior/action |
| --- | --- |
| `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` | Constructor normalizes tenant and validates grammar; durable actor/key shapes and the special `system` topic must remain readable. Add boundary canonicalization without prohibiting historical stored identities globally. |
| `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` | Mirrors unprefixed `{domain}.events` topic for `system`; include in migration/compatibility tests. |
| `src/Hexalith.EventStore.Contracts/Rest/RestTenantSource.cs`, `RestApiAttribute.cs` in the same directory | Public Claims/Route/System metadata; Claims is the default. Both claim-only selection and System are affected public behavior. |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` | `ResolveTenant` returns raw Route, filters/collapses Claims, or synthesizes System. Emit validation before gateway dispatch and forward canonical values. |
| `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs` | Administrator bypass precedes missing checks; claims compare Ordinal and blank grants are discarded. Replace with shared explicit-scope validation. |
| `src/Hexalith.EventStore/Authorization/TenantValidationResult.cs` | Currently carries allowed/reason only. A comparison-only fix cannot propagate canonical scope; resolve that data flow explicitly. |
| `src/Hexalith.EventStore/Authorization/ActorTenantValidator.cs`, `Extensions/ServiceCollectionExtensions.cs` | Optional actor validator receives raw tenant. Local validation must precede actor invocation; otherwise invalid scope already caused downstream work. |
| `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs` | Command/query authorization stage must retain its position before logging and validation. |
| `src/Hexalith.EventStore/Controllers/QueriesController.cs`, `CommandValidationController.cs`, `QueryValidationController.cs`, `StreamsController.cs`, `AdminStorageCommandController.cs` | Validator callers; propagate canonical scope before dispatch/state. Streams also rejects uppercase independently and compares workload-bound scope; keep resource-binding semantics consistent. |
| `src/Hexalith.EventStore/Controllers/CommandStatusController.cs`, `ReplayController.cs` | Direct grant enumeration discovers tenant storage outside ITenantValidator. Include explicit-scope correction without changing Story 2.15's separate MessageId grammar/Location policy. |
| `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` | Independent canonical tenant/projection grammar check mirrors Streams; inventory tenant behavior while preserving projection rules. |
| `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Validates then builds group names from raw request text. Normalize join/leave consistently before group operations; preserve operation authorization and reconnect rules. |
| `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs` | Administrator bypass and first-grant fallback. Remove scope synthesis. |
| `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs`, `Controllers/AdminStreamsController.cs`, `Services/DaprStreamQueryService.cs` | Filter repeats bypass/fallback; stream controller also resolves scope independently; query service compares OrdinalIgnoreCase. Keep existing Admin operation permissions while sharing scope rules. |
| `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` | Links gateway source from the host project; edit owning files once, verify both build graphs. |

Canonicalization must distinguish explicit request sources from a principal's legitimate multiple distinct tenant grants. Reject malformed/duplicate normalized values without choosing a scope from the grant set. Do not trim malformed request input into validity or infer wildcards from administrator identity.

## Tenants migration inventory

| File under T | Relevant behavior/action |
| --- | --- |
| `src/Hexalith.Tenants.Contracts/Identity/TenantIdentity.cs` | `DefaultTenantId = "system"`; `ForTenant` and `ForGlobalAdministrators` select durable storage. Changing the constant would silently select different streams. |
| `src/Hexalith.Tenants.Api/RestApiAssemblyInfo.cs` | Whole API uses RestTenantSource.System. |
| `src/Hexalith.Tenants.Contracts/Module.cs` | McpCli decoration uses FixedTenant=DefaultTenantId. REST/UI-only migration leaves another system producer. Check shared McpCli capability before changing decoration. |
| `src/Hexalith.Tenants.UI/Services/Gateways/TenantCommandGateway.cs` | Eleven command submissions and command-status validation rely on fixed system scope. |
| `src/Hexalith.Tenants/Authorization/TenantsSystemTenantValidator.cs`, `Program.cs` | Registered validator requires exact system request and grant. |
| `src/Hexalith.Tenants/Validation/TenantSubmitCommandValidator.cs` | Global-admin commands require system/domain/aggregate tuple. Preserve domain/aggregate ownership while replacing request scope. |
| `src/Hexalith.Tenants/Bootstrap/TenantBootstrapCredentialProvider.cs`, `TenantBootstrapHostedService.cs` | Mint system grant and submit system request; migrate with host paths. |
| `src/Hexalith.Tenants/Queries/Handlers/TenantOwnerAuthorityQueryHandler.cs`, `TenantReadAuthorityQueryHandler.cs`, `src/Hexalith.Tenants/Authorization/TenantOwnerSourceGateway.cs` | Bind authoritative reads to system-backed identities. |
| `src/Hexalith.Tenants.Server/Projections/TenantAuditStreams.cs` | Distinguishes tenant and global administrator owners; retain ownership-aware readers and immutable legacy data. |
| `docs/audit-stream-rollout.md`, `docs/event-contract-reference.md`, `docs/maintenance-backlog.md` | Explicitly preserve a managed tenant literally named system, conflicting with AD-27. No approved AD-27 migration was found in searched Tenants docs/planning. |

The existing audit identities are distinct:

- `system:tenants:system` maps to `audit-tenants:system`.
- `system:global-administrators:global-administrators` maps to `audit-global-administrators:singleton`.
- `audit:system` is immutable legacy evidence consumed by ownership-aware compatibility readers.

Owner direction accepted on 2026-10-25: complete 5.12 first, then coordinate a SemVer-major explicit-scope transition. Preserve existing durable identities and writable platform administration. The `system` storage partition holds all managed-tenant aggregates, so making it read-only would break normal administration. Inventory whether a managed tenant literally named `system` exists and is active before choosing its retirement policy. Unused/retired history remains readable through authorized platform access; active use requires owner reconciliation with AD-27, not automatic freezing. No deployed inventory has been obtained, and repository tests do not establish deployed usage. No blanket key rename or ambiguous merged audit stream is authorized.

## Validation plan

Run each affected project independently. Local builds use `dotnet build <project.csproj> -c Debug -p:UseHexalithProjectReferences=true`; restore when reference mode changes. Focused xUnit v3 runs invoke the built executable with single-dash `-class` filters, then run the relevant full suites. CI uses Release/package mode. Start and inspect the Aspire baseline before code changes, following the repository lifecycle guidance.

| Test project / files | Required coverage |
| --- | --- |
| `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` | Canonicalizer matrix, normalized duplicate/invalid grants, reserved system, durable identity/topic compatibility. |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj` | Reuse `RestApiGeneratorTestHarness.cs` and `RestApiGeneratedControllerErrorSemanticsTests.cs`; compile and execute generated controllers, assert canonical forwarded values and zero gateway calls on denial. Existing positive fixtures need authenticated principals. Update source assertions in `RestApiControllerGenerationTests.cs`. |
| `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` | Claims/actor/admin filter tests; command/query/stream/admin-storage/status/replay paths; hub join/leave. Replace old case-sensitive denial and administrator-system allow expectations. Assert no validator actor call for locally invalid scope. |
| `tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj` | AdminTenantAuthorizationFilterTests, DaprStreamQueryServiceTests, and controller scope resolution; preserve operation authorization. |
| `tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` | Legacy topic behavior and any scope transport adaptations. |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj` | Runtime scope isolation and Dapr-path persisted end-state/legacy readback; mocks alone cannot close migration evidence. |
| `T/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj` | TenantsApiGeneratedControllerTests, CommandApiRuntimeIntegrationTests, AuditStreamPersistenceTests; runtime identity and distinct historical owner readback. |
| `T/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj` | TenantCommandGatewayTests across command and status flows. |
| `T/tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj` | McpCliDecorationTests and new explicit scope metadata. |

`.github/workflows/ci.yml` already runs deterministic Contracts, generator, Server, Admin.Server and Client suites. `.github/workflows/integration.yml` runs Server.LiveSidecar.Tests, but only the assembly-parallelization class from IntegrationTests; new runtime proof must actually be bound to an executing lane. EventStore's tenants-source-mode job runs AppHost tests, not all Tenants runtime tests. Bind those in Tenants' owning CI or add an explicit executing integration step. Record exact commands/results and CI links in the story; no committed evidence packets.
