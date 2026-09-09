# Architecture Spine Validate Report — 2026-09-09

**Gate verdict: FAIL — CHANGES REQUIRED:** the spine is mechanically clean, but two already-real production divergences and eleven high-severity contract gaps prevent it from serving as the canonical architecture boundary.

## Subject and review boundary

| Item | Value |
| --- | --- |
| Subject | `_bmad-output/planning-artifacts/architecture.md` |
| State | `status: final`; `updated: 2026-08-29`; 25 architecture decisions; 609 lines |
| SHA-256 | `2b96a810f34f3cc40ee2ef5fc0f9b1cfaf01a395c73d1321953a946162fe945b` |
| Intent | Validate — critique only |
| Deterministic lint | **Pass, 0 findings** |
| Semantic gate | **Fail, changes required** |

This report consolidates the independent [rubric walker](review-validate-2026-09-09-rubric-walker.md), [technology-currentness](review-validate-2026-09-09-technology-currentness.md), [adversarial-divergence](review-validate-2026-09-09-adversarial-divergence.md), [security/data-integrity](review-validate-2026-09-09-security-data-integrity.md), and [brownfield-ratification](review-validate-2026-09-09-brownfield-ratification.md) reviews. The canonical spine, memlog, code, configuration, tracker, dependencies, and evidence were **not changed**.

“Raw” below means every severity-labelled finding occurrence across the five reviews. “Consolidated” merges findings that describe the same architecture defect, retaining the highest substantiated severity. The separate upstream PRD inconsistency is excluded from both columns.

| Severity | Raw reviewer occurrences | Consolidated architecture findings |
| --- | ---: | ---: |
| Critical | 6 | **2** |
| High | 21 | **11** |
| Medium | 20 | **12** |
| Low | 9 | **6** |
| **Total** | **56** | **31** |

## Critical findings

### C1 — Production runtime/provider authority is undefined

**Finding.** AD-9 makes topology one change unit and AD-25 approves `oq8-postgresql-v1` as an evidence profile, but no rule selects a production actor state store, broker, deployment target, or proof boundary. The tree presents PostgreSQL and Cosmos DB as production choices while local AppHost uses Redis; the spine itself records a Redis sequence-1 overwrite and forbids inferring equivalent behavior for other providers.

**Evidence.** Spine: `_bmad-output/planning-artifacts/architecture.md:117-121,141-145,451-460,486-513,598-609`. Tree: `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:21-40`; `deploy/dapr/statestore-postgresql.yaml:15-29`; `deploy/dapr/statestore-cosmosdb.yaml:15-36`; `deploy/README.md:1-44,141-180`; `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs:23-55,195-209`; `_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md:13-18,61-63`. Current DAPR documentation still supports the deliberately frozen PostgreSQL v1 component while recommending incompatible v2 for new systems: [PostgreSQL state-store documentation](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/).

**Why critical.** Independently conforming deployment units can claim production readiness with materially different ETag, transaction, ordering, and first-write behavior. Admission fencing is not a storage fence, so an unsupported profile can silently replace durable history.

**Disposition: discuss, then autofix.** Select one named production runtime profile covering provider, broker, target, app IDs, overlays, secrets, resilience, and required proof. The narrow evidence-backed choice is `state.postgresql` v1 under `oq8-postgresql-v1`; classify Redis as Development-only and every other provider as non-authorizing until equivalent persisted-race and AD-25 proof exists. If no choice is made, explicitly prohibit production traffic and name the owner and trigger.

### C2 — Tenant identity has no canonical source or normalization contract

**Finding.** AD-10 requires tenant authorization and the conventions require tenant identity to remain explicit, but neither defines the authoritative input, normalization point, casing, comparison, reserved names, or omission behavior. Current authorization, persistence, Admin, generated REST, and SignalR paths already implement incompatible answers.

**Evidence.** Spine: `_bmad-output/planning-artifacts/architecture.md:129-151,400-415,462-470`. Tree: `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:9-16,23-37,88-94`; `src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs:68-113`; `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs:22-49`; `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs:27-48`; `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs:23-44`; `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:22-26,61-80,203-206`; `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:76-82,104-127`; `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:407-444`; `_bmad-output/planning-artifacts/prd.md:305-307`.

**Why critical.** `Tenant-A`, `tenant-a`, an omitted tenant defaulted from the first grant, and reserved values can authorize, partition admission, address state, filter Admin reads, and form SignalR groups differently. That creates tenant-isolation and idempotency-authority splits while each unit plausibly follows the spine.

**Disposition: autofix.** Require exactly one explicit tenant at the first application boundary; run request values and `eventstore:tenant` grants through one `Contracts` canonicalizer before authorization or key derivation; carry the validated lowercase `AggregateIdentity` form unchanged; never default from claims/session/config; reserve `system` and wildcard values; and prove equivalent spellings cannot create or reveal different scopes.

## High findings

### H1 — The inbound sidecar-to-application credential is unnamed

AD-10 requires an application-layer credential but does not bind its carrier, issuer, receiver, claims, tenant rule, rotation, readiness, or SDK integration. The gateway trusts `dapr-caller-app-id`, Operations checks an app-channel token, and DomainService maps sensitive endpoints without authentication: `_bmad-output/planning-artifacts/architecture.md:147-151,269-284`; `src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs:9-45`; `src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationOptions.cs:5-17`; `src/Hexalith.EventStore.Operations/Security/DaprAppChannelTokenMiddleware.cs:11-44`; `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:121-174,198-330,404-463`. **Disposition: discuss, then autofix** — choose one platform-owned receiver protocol, install it through the host SDK/ServiceDefaults, fail readiness outside Development when absent, and treat caller app ID plus ACLs as attribution/defense in depth unless explicitly adopted as credential authority.

### H2 — Deny-by-default remains optional

AD-16 says only a host that “introduces” a fallback policy must coordinate probe exemptions; no `FallbackPolicy` exists under `src/`, so a newly mapped endpoint without authorization metadata is anonymous: `_bmad-output/planning-artifacts/architecture.md:243-253,473`; `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:97-103`; `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:198-330`. **Disposition: autofix** — require the platform fallback policy on every HTTP host, allow only the named probe/static-public allowlist, and add an endpoint-metadata conformance scan for every host kind.

### H3 — Admin mutation attribution and delegation are not a durable contract

The spine says only “attributable and support-safe” despite diagrams showing delegated writes. Current paths alternately forward the operator token, manufacture `system:{appId}`, accept caller-supplied actor IDs, or fall back to `anonymous`; audit and mutation state are written separately: `_bmad-output/planning-artifacts/architecture.md:73-82,147-151,539-581,609`; `src/Hexalith.EventStore/Authorization/DualPrincipalIdentity.cs:5-22`; `src/Hexalith.EventStore/Authorization/DualPrincipalClaimsHelper.cs:12-22`; `samples/Hexalith.EventStore.Sample.Api/Program.cs:20-39`; `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs:281-299`; `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:289-395,490-494`. **Disposition: discuss, then autofix** — choose operator pass-through or a precise delegation protocol; derive actor identity only from authenticated claims; bind original actor, workload, delegation, canonical tenant, action, target, requested/final outcome, correlation, timestamp, and surface into an append-only audit record atomically or through a versioned resumable unit with the mutation.

### H4 — AD-24/AD-25 key rotation and fleet catalog ownership can split admission authority

AD-25 defines exact per-record directory and migration phases but not one fleet-wide active digest version, reader set, secret generation, adapter registry, descriptor versions, or retention assignment. AD-24 can revoke after consumer acknowledgement while AD-25 still has live aliases or tombstones: `_bmad-output/planning-artifacts/architecture.md:380-440`; `src/Hexalith.EventStore.Server/Configuration/IdempotencyAdmissionOptions.cs:3-31`; `src/Hexalith.EventStore.Server/Configuration/ValidateIdempotencyAdmissionOptions.cs:19-70`; `src/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterRegistry.cs:7-17,30-52,61-90`; `src/Hexalith.EventStore.DomainService/IIdempotencyIntentAdapter.cs:5-25`. **Disposition: autofix** — make a versioned deployment-overlay catalog the sole authority, key adapters by `(Domain, CommandType)`, compare its digest at readiness, bind the ring as an AD-24 `runtime-required` OpenBao secret, and require both consumer acknowledgement and AD-25 zero-reference retirement before revocation.

### H5 — Correlation identity is a join key without one cross-host contract

AD-17 relies on `CorrelationId` while the gateway accepts a bounded broad token and mints a sortable ID, but Admin accepts only `Guid` and mints another GUID using the parser the spine forbids: `_bmad-output/planning-artifacts/architecture.md:255-265,462-468`; `src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs:7-45`; `src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs:6-27`; `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs:10-31,45-65`. **Disposition: autofix** — put the `X-Correlation-ID` grammar/validator/generator in `Contracts`, mint only at the first application boundary, echo and propagate unchanged, distinguish it from `traceparent`, and decide whether status identity is `MessageId` or `CorrelationId`.

### H6 — Erasure has competing authorities and no ordered completion model

AD-7 owns read-model/checkpoint erasure, AD-23 owns payload-key invalidation through `IErasureStateProvider`, and AD-20 requires rebuild completion, but no rule orders these operations or tells projection delivery how to advance past intentionally unreadable history: `_bmad-output/planning-artifacts/architecture.md:129-133,311-315,370-378,609`; `_bmad-output/implementation-artifacts/spec-1-9-read-model-and-projection-checkpoint-erasure.md:100-110`; `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:938-950,1370-1385,2153-2159`. **Disposition: discuss across EventStore and Parties, then update** — name one orchestration/state authority, order projection cleanup and key invalidation, define a typed `Erased` delivery/rebuild outcome that can safely advance without recreating data, and distinguish read-model, crypto, and physical/backup erasure completion.

### H7 — `ProjectionVersion` has provenance but no interoperable semantics

AD-15 permits a persisted version string and AD-20 requires equality after replay, but neither defines whether it is opaque, monotonic, a sequence, checkpoint, content hash, schema version, or rebuild lineage. The shared interface explicitly permits incompatible examples and production owns no constructor: `_bmad-output/planning-artifacts/architecture.md:227-241,311-315`; `src/Hexalith.EventStore.Client/Projections/IReadModelFreshness.cs:22-34`; `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs:62-85`; `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:14-24,602-615`; `src/Hexalith.EventStore.Client/Projections/ReadModelBatchKeys.cs:3-14`. **Disposition: discuss, then autofix** — either declare it opaque and forbid ordering/progress inference while adding a separate typed checkpoint, or define one bounded platform value, producer, comparer, scope, change rule, catalog/schema binding, and rebuild/live-lineage behavior.

### H8 — Poison/dead-letter semantics and the Operations service have no architecture owner

The spine binds at-least-once projection behavior and FR34-FR35 but omits the existing stateful `eventstore-operations` workload from the paradigm, seed, topology, capability map, AppHost, and release identity. Current defaults are consumer-specific and capture can lose a dead letter after retry exhaustion: `_bmad-output/planning-artifacts/architecture.md:56-89,135-145,515-596`; `src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj:1-8`; `src/Hexalith.EventStore.Operations/Program.cs:13-46`; `src/Hexalith.EventStore.Operations/Configuration/EventStoreOperationsOptions.cs:6-27,43-59`; `src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs:39-49,60-119,227-235`; `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs:26`; `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterCommandService.cs:93-101`; `.github/workflows/release.yml:125-136`. **Disposition: discuss, then update** — adopt Operations with capture/ack/retry/replay/checkpoint outcomes, durable state, tenant identity, authorization/audit, configured targets, app ID, topology, and release status; or mark it explicitly non-production under Story 7.1 with fail-closed interim rules.

### H9 — AD-19 mandates a normalized result contract that does not exist

AD-19 requires exact `ProjectionDispatchResult`, `ProjectionDispatchResultEntry`, and `ProjectionCheckpointAdvanceState` types and rejects alternatives, while the implementation exposes `ProjectionDispatchResponse`, `ProjectionDispatchOutcome`, coordinator-internal checkpoint control, and `Task<bool>` ownership: `_bmad-output/planning-artifacts/architecture.md:286-309,477`; `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchResponse.cs:1-10`; `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchOutcome.cs:6-17`; `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs:11-64`; `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:152-191,522-600,1021-1055`. Exact-symbol search under `src/` and `tests/` found no mandated type. **Disposition: discuss, then update** — mark the exact shape prospective with an implementation owner and proof gate, or re-ratify AD-19 around the shipped seam while preserving its normalized outcome/checkpoint matrix.

### H10 — The hard-coded .NET/ASP.NET security baseline is superseded

AD-11 and Stack call SDK `10.0.400` and runtime/package `10.0.11` current, while Microsoft released security-bearing .NET `10.0.12` with SDK `10.0.401` on 2026-09-08. The repository still seeds `10.0.400` and centrally pins the `10.0.11` package family: `_bmad-output/planning-artifacts/architecture.md:153-157,486-506`; `global.json:1-4`; `references/Hexalith.Builds/Props/Directory.Packages.props:172-193,202-221,308`. Primary sources: [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [.NET 10.0.12 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md), [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), and [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). **Disposition: discuss, then update as one validated unit** — move the SDK and coupled .NET/ASP.NET/Microsoft.Extensions family only after restore/build/test/consumer proof; if held, label `10.0.11` a dated catalog pin with owner and revisit trigger rather than the current security baseline.

### H11 — A public unfenced actor mutation route contradicts AD-5/AD-25

AD-5 says `AggregateActor` accepts only a current-fence context and AD-25 requires the current non-zero fence at every mutation/side-effect boundary. The public request makes the idempotency key optional; null bypasses admission, routes through the unfenced actor method, and `EnsureExecutionFenceAsync` treats null as success: `_bmad-output/planning-artifacts/architecture.md:117-121,400-415`; `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs:16-26`; `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:55-77,250-281`; `src/Hexalith.EventStore.Server/Commands/CommandRouter.cs:33-54,62-99`; `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs:11-25`; `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:346-371,4167-4179`. **Disposition: implementation blocker plus architecture clarification** — make admission/fence mandatory for every protected mutation and remove/fail the unfenced production path, or explicitly scope AD-5/AD-25 to a named operation set and justify each allowed unfenced mutation. The absolute adopted rule and current path cannot both be authoritative.

## Medium and low tail

### Medium

| ID | Consolidated finding and evidence | Disposition |
| --- | --- | --- |
| M1 | Route ownership is not unique across app IDs; fallback routing and host-overridable projection paths can admit two owners for one domain/projection key (`architecture.md:99-103,286-309`; `DaprDomainServiceInvoker.cs:78-84,106-113`; `EventStoreDomainServiceExtensions.cs:198-205,227-238`). | **Autofix:** versioned route catalog; exactly one owner per operation/projection key; duplicate ownership fails readiness. |
| M2 | Stack and target/current views drift: FrontComposer is rendered as `4.1.1` while the live catalog and NuGet are `4.4.0`, source is five commits past the tag, and pending FrontComposer/OpenBao work is drawn as present (`architecture.md:214,482,506,539-570`; catalog `:9,53-57`; Admin.UI project `:18-32`; `epics.md:5553`; `sprint-status.yaml:220,229`). | **Autofix, then discuss parity policy:** remove volatile literal or refresh it; label delivered and target topology separately. |
| M3 | “DAPR CI/deployment seed `1.18.0`” conflates CLI `1.18.0`, CI sidecar `1.18.2`, and deployment example `1.18.0`; stable runtime `1.18.3` is available ([DAPR releases](https://github.com/dapr/dapr/releases); `.github/workflows/integration.yml:24-25,64-68,148`; `deploy/README.md:243,261,279,298`). | **Autofix wording; defer upgrade:** state each fact separately and move runtime only after evidence refresh. |
| M4 | AD-3 forbids direct Admin state-store calls while the topology and `DaprProjectionQueryService` require support-safe reads (`architecture.md:105-109,539-577`; service `:61-80`). | **Autofix:** name and constrain the tenant-authorized read-only Admin exception, or remove that path. |
| M5 | Structural Seed omits existing architectural parts including AppHost, SignalR, Testing, Testing.Integration, Admin.Server.Host, and deployment/sample topology roots (`architecture.md:515-537`; `docs/brownfield/architecture.md:58-80`). Operations is handled separately at H8. | **Autofix:** add and classify current libraries, hosts, tests, and deployment assets. |
| M6 | Operational dimensions and telemetry hygiene are neither decided nor safely deferred: retention/restore, environment promotion, scale/multi-region, exporters/cardinality, and platform-wide payload/credential/PII redaction (`architecture.md:486-513,598-609`; `prd.md:309`). | **Defer with safe posture, owner, and trigger; autofix telemetry classification:** prohibit sensitive payloads/tokens/keys in logs, traces, metrics, problems, and support output. |
| M7 | AD-23/AD-24 still describe payload protection as draft/not authorized although the exact-digest specification records `approved-authorized` and `AR-20260801-01` (`architecture.md:370-398`; spec `:1-10,61-75,2253-2272`). | **Autofix:** record satisfied prerequisite while retaining every successor implementation/evidence gate. |
| M8 | Story status, dated exceptions, receipts, package-count schedules, and migration procedure obscure stable invariants (`architecture.md:60-71,153-165,323-368,484`). | **Discuss:** move volatile lifecycle/evidence procedure to memlog/spec/story artifacts; retain stable ownership and proof rules. |
| M9 | The conventions table duplicates AD-7/8/11/16/18/19/20/21/23/24/25 and has already drifted (`architecture.md:462-484`). | **Autofix:** retain only independent conventions and replace duplicate prose with `See AD-x`. |
| M10 | Production state-store templates use environment placeholders and do not name OpenBao despite AD-24 requiring `secretKeyRef`/`openbao` (`architecture.md:380-390`; PostgreSQL template `:19-29`; Cosmos template `:20-36`). | **Defer as implementation drift, but gate deployment:** no such profile is conforming until templates and validation implement AD-24. |
| M11 | The spine names OQ8 by version/digest but omits the available governing repository/path/commit identity (`architecture.md:58-65`; `prd.md:94,532`). | **Autofix:** add repository `github.com/Hexalith/Hexalith.Folders`, path `docs/exit-criteria/oq8-idempotency-design.md`, commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2`, and existing digest without claiming local retrieval. |
| M12 | The append-only memlog stops at the earlier SDK/version baseline and lacks the accepted 2026-08-20/29 inputs (`.memlog.md:97,105`; `architecture.md:10,35-36,492`). | **Autofix only in an authorized Update:** append recovered constraints and current version evidence; never rewrite history. |

### Low

| ID | Consolidated finding and evidence | Disposition |
| --- | --- | --- |
| L1 | CodeCoverage `18.10.0` is catalog-consistent but `18.11.0` is current ([NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.codecoverage/index.json); `architecture.md:504`). | **Defer** to the next tested catalog refresh. |
| L2 | CommunityToolkit DAPR remains compatible, with later `13.5.1-beta.*` builds on another prerelease channel ([NuGet index](https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json); `architecture.md:496`). | **Defer/ignore in spine** pending the normal prerelease compatibility gate. |
| L3 | AD-17 cites obsolete RFC 7231 and implies absolute `Location` is an RFC rule; RFC 9110 permits a URI-reference ([RFC 7231 status](https://www.rfc-editor.org/info/rfc7231/), [RFC 9110 §10.2.2](https://www.rfc-editor.org/rfc/rfc9110.html#section-10.2.2); `architecture.md:261`). | **Autofix:** cite RFC 9110 and label absolute form as Hexalith's stricter contract. |
| L4 | The final Deferred row still contains the editorial token `ADD,` (`architecture.md:606`). | **Autofix.** |
| L5 | Frontmatter lists the spine itself as a companion (`architecture.md:49-51`). | **Autofix** unless a documented consumer requires it. |
| L6 | Capability map overstates direct FR17/FR18 binding relative to AD-11/AD-12 (`architecture.md:153-171,584-591`). | **Autofix:** bind directly or narrow the map. |

## Upstream PRD inconsistency — not an architecture defect

### U1 — Retired ownership-resolution work remains contradictory inside the PRD

The approved 2026-09-08 proposal resolves duplicate primary claims and `prd.md:541` retires OR12, but `prd.md:400` still says SM2 awaits OR12, `prd.md:439-453` still labels eight FRs as multiply owned, and `prd.md:467` repeats the refuted FR1/Epic-4 assertion. Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-08.md:61+`.

**Disposition: refer to the PRD owner.** Correct the PRD in its own governed update. Do not count U1 as an architecture finding or modify the PRD during an architecture Validate run.

## What passed

- Deterministic structure passed with zero findings: 25 stable AD identifiers, required `Binds`/`Prevents`/`Rule` fields, no lint-detected placeholders, and pinned Stack rows.
- AD-1 through AD-9 still provide a coherent DAPR-backed hexagonal/event-sourcing core: actor-owned mutation, stable event identity, at-least-once projection semantics, and topology co-change rules.
- AD-11/AD-22 release identity remains unusually exact: immutable OCI index/children/config graph, canonical bytes, one-use authority, role receipts, and consumer-owner acceptance.
- AD-24's base secret-store contract remains current and closed: OpenBao uses DAPR's `secretstores.hashicorp.vault` v1 component, value-free overlay catalog, default-deny scopes, bounded cache, and coordinated rotation. Sources: [DAPR OpenBao catalog](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/), [Vault/OpenBao-compatible component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/), [OpenBao releases](https://github.com/openbao/openbao/releases).
- AD-19's status/checkpoint matrix is internally exact even though H9 shows the named carrier types are not ratified by code.
- AD-14/AD-15 provenance, AD-17 gateway-owned absolute status location, and AD-18 outbound remove-and-replace ownership of `dapr-app-id`/`dapr-api-token` remain sound.
- AD-25 tombstone fields, expiry response, directory promotion order, redirect, mixed-version fail-closed behavior, and legacy migration phases remain exact within one catalog.
- The JWT contract is materially fail-closed, SignalR group admission checks tenant authorization, and the fenced actor path validates protected context strongly when that path is used.
- Most named technologies remain published and fit the selected bands. Aspire `13.5.3`, DAPR .NET SDK `1.18.5`, MediatR `14.2.0`, FluentValidation `12.1.1`, Roslyn `5.9.0`, Fluent UI Blazor V5 RC, OpenTelemetry `1.18.0`, xUnit v3 `4.0.0`, Shouldly `4.3.0`, NSubstitute `6.2.0`, and NBomber `6.6.0` remain verified against their official release/NuGet authorities. Full links and exact indexes are retained in the [technology-currentness review](review-validate-2026-09-09-technology-currentness.md).
- Current DAPR actor/pub-sub semantics continue to support the architecture's conservative guarantees: [actors](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/) and [pub/sub](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/). Azure Container Apps' managed DAPR limitation that excludes OpenBao remains documented: [Azure Container Apps DAPR overview](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview).

## Delta since 2026-09-08

The subject is unchanged: same `updated: 2026-08-29`, same 25 ADs, and same SHA-256. All movement below is better evidence, consolidation, or severity calibration—not a spine regression.

| Area | 2026-09-08 | 2026-09-09 assessment |
| --- | --- | --- |
| Gate | Fail | **Fail, unchanged** |
| Critical | 2 consolidated | **2 consolidated, same defects**: production runtime/provider and tenant identity |
| High | 10 consolidated | **11 consolidated**: the public unfenced actor route is now separately high because exact request → handler → router → actor bypass evidence proves an adopted/current contradiction |
| Counts | Prior report used a different raw/consolidation convention (`2/16/20/15` raw; `2/10/8/9` consolidated) | This report counts all five lenses literally (`6/21/20/9` raw) and merges to `2/11/12/6`; compare clusters, not raw totals |
| .NET currentness | `10.0.12` appeared on release day with propagation ambiguity | Official metadata, release notes, download page, and SignalR NuGet now consistently establish the security update and SDK `10.0.401` |
| AD-25 key source | Production configuration source was a concern | Current validator now gates inline configuration keys to Development/Test; this portion is **closed**, leaving fleet catalog/OpenBao retirement coordination open |
| Brownfield | Operations and phantom AD-19 types were identified | Both are reconfirmed with direct Admin→Operations and coordinator/interface evidence; current-vs-target labeling and OQ8 locator/memlog continuity are clearer |
| Upstream | Ownership cleanup was treated as external continuity | PRD internal contradiction is now explicit as **U1**, outside architecture severity counts |

## Gate decision and next action

Do not use `status: final` as evidence of production readiness. Resolve C1 and C2 before any production-readiness claim; close or safely defer H1-H11 with named owners, interim prohibitions, and evidence gates; then run an authorized **Update** that preserves AD IDs and append-only memlog history, followed by deterministic lint and all five reviewer lenses again.

This Validate run is critique-only. `_bmad-output/planning-artifacts/architecture.md` remains unchanged.
