# Story 5.12 implementation investigation

Investigated 2026-10-25 at EventStore HEAD `45ec9c43102b638478e6fb9374b06b1abd21af0a`. Planning only: no runtime changes or test execution. Existing unrelated FrontComposer/Platform submodule changes are outside this work. User approved completing this prerequisite before Story 2.14, keeping its scope to schema, codec, validator and Development/test catalog.

## Current authority

- `_bmad-output/planning-artifacts/epics.md:4801` owns the construction story. 5.13 owns catalog activation and multi-host rollback; 5.14 owns broader ingress inventory/enforcement.
- Architecture AD-17, AD-25, AD-27 and AD-33 define MessageId declarations, idempotency facet, platform scope and catalog identity. Their adopted rules supersede older 5.12 assumption wording.
- `architecture.md:569` explicitly requires owner choices on operation-vocabulary ownership, human-bearer claim type and ingress inclusion before 5.12 freezes. Its no-Contracts-dependency rationale is stale: `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` already references Contracts.
- `architecture/architecture-eventstore-2026-07-05/.memlog.md:188,202` records the deferred owner choices. The companion `reviews/review-update-2026-10-07b-rubric-walker.md:165` identifies the unresolved human grant mapping.
- Story 5.5 is done in its spec and tracker. Preserve shared JWT/app-channel/workload contracts, exact audience/operation token acquisition, minimal workload principals and resource-bound provenance.
- The October 23 evidence policy supersedes deleted authorization records/tools; retain story/spec Verification and CI rather than recreate the deleted machinery. No new production authority follows from construction.

## Routing and declaration seams

| Source | Reuse or missing input |
| --- | --- |
| `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`, `CommandContractMetadata.cs` | Domain and command type only. No explicit MessageId grammar, contract version, credential kind, operation or target. Extend declarations additively; do not infer undeclared grammar. |
| `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`, `QueryContractMetadata.cs` | Domain, query type and projection type only. |
| `src/Hexalith.EventStore.Client/Commands/CommandContractResolver.cs`, `Queries/QueryContractResolver.cs` | Existing metadata resolution; derive new metadata from the same declaration source. |
| `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` | Exact/wildcard static registrations, optional Dapr configuration and a convention fallback resolve tenant/domain/version to app and process method. A second handwritten catalog must not replace this evidence. |
| `src/Hexalith.EventStore/Queries/DaprDomainQueryInvoker.cs` | Invokes method `query`, ignoring the registration's command method. Parity must exercise this path rather than copy command mapping. |
| `src/Hexalith.EventStore/appsettings.Development.json`, `appsettings.json` | counter/greeting map to sample; tenants/global-administrators map to tenants; system registrations still exist. Construction preserves effective mappings; 2.14 changes request scope. |
| `src/Hexalith.EventStore.Client/Projections/ProjectionDispatchRoute.cs`, `ProjectionRouteCatalogFingerprint.cs` | Domain/projection routes and existing capability-bound fingerprint. Do not change its digest protocol to reuse it as the new root digest. |
| `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` | Authenticated operational metadata discovery supplies actual projection inventory. |
| `src/Hexalith.EventStore.DomainService/DomainProjectionCatalogRegistry.cs`, `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalogEntry.cs` | Existing runtime projection registries are not the missing AD-33 envelope. |

The builder needs explicit declarations plus a value-free Development composition binding. Host app IDs are deployment bindings, not defaults guessed from the domain name. The resulting catalog must be compared against effective command/query/projection resolution for the declared profile. Any admitted fallback must be explicit and bounded.

## Idempotency input gap

`src/Hexalith.EventStore.DomainService/IIdempotencyIntentAdapter.cs` exposes CommandType, AdapterId, OperationId, DescriptorVersion and RetentionTier. `src/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterRegistry.cs` indexes only CommandType, without Domain; policy version comes from `CreateIntent`, not a static declaration. No adapter registration was found under current `src/` or `samples/` beyond registration infrastructure.

`src/Hexalith.EventStore.Server/Configuration/IdempotencyAdmissionOptions.cs` contains generation and secret-reference settings plus actual Development key material. Never serialize that object wholesale. `src/Hexalith.EventStore.Contracts/Commands/IdempotencyReplayRetentionTier.cs` supplies Mutation/Commit tiers, but their existence cannot assign a policy to a command.

Before the complete Development catalog can be generated, the owning command/profile declarations must supply the trusted adapter, operation, canonical descriptor schema/digest, retention tier, active/reader generations, logical secret map and consumer identity required by AD-25. This is an outstanding input. Synthetic fixtures can prove codec/validator behavior but cannot establish an actual complete Development catalog or OQ8 authority. Do not silently add domain mutation semantics or substitute retention defaults; expose missing data by route/field.

## Proposed owner decisions

These are proposals, not approvals.

1. **Contracts vocabulary, ServiceDefaults authentication.** Put identifiers in Contracts; preserve public `ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs` constants as forwarding constants with unchanged values. Keep issuer logic, header/claim parsing, policies and authentication in ServiceDefaults. No new abstractions package or reversed dependency.
2. **Existing human permission claim, exact platform match.** Reuse `eventstore:permission`, which is distinct from workload `eventstore:operation`. New permission values still need provisioning when 2.14 adopts enforcement. `src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs` currently allows missing permissions, wildcard/category matches, case-insensitive comparison and administrator bypass; it cannot authorize a platform operation unchanged. A future platform check must demand an authenticated human and exact declared permission. Authentication claims remain owned by `ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs`; existing transformation/pipeline sources are `src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs` and `Pipeline/AuthorizationConstants.cs`.
3. **Separate admission metadata, no general ingress table.** On platform-scoped declarations, require namespace plus one `RequiredHumanPermission`. Derive both into the same catalog root; generated endpoint metadata references that declaration. The downstream route still declares `CredentialKind=workload` and its actual receiving operation, such as `domain-service:process` or `domain-service:query`. Human admission and workload dispatch are different hops. Never change a domain-service row to delegated-user because its original caller was human. Leave broad ingress inventory in 5.14; do not maintain another endpoint-to-permission authority.

`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceRoutes.cs` and `EventStoreDomainServicePolicies.cs` already associate receiving endpoints with operations/policies and should consume the same identifiers after approval.

## Codec and validation

Use BCL `Utf8JsonWriter`, strict JSON parsing and SHA-256 in Contracts. A versioned closed schema owns ordering, integer/string encodings, optional-field presence and digest preimages. Detect duplicate and unknown properties before typed decoding; require byte-for-byte equality with canonical re-encoding. Avoid self-referential digest inputs. Return safe route/field diagnostics rather than echoing payloads.

`src/Hexalith.EventStore.Client/Projections/ReadModelBatchCanonicalJson.cs` provides implementation patterns but is internal generic JSON code in an outward layer. `Client/Events/EventRegistryFingerprintCodec.cs`, projection fingerprinting and `Contracts/Security/DeletionBatchCapabilityIdentity.cs` own different existing protocols; leave their bytes and identities unchanged.

New models belong under `Contracts/Routing`, one type per file. No new serialization dependency, catalog server, dynamic discovery service, activation protocol or production signer is needed for construction. A repository developer script may generate/check a local file using the same codec; it is not a new operator/domain CLI surface.

## Validation targets

New Contracts codec/validator tests must include valid controls and observed negative fixtures for unsupported schema, duplicate/unknown fields, noncanonical bytes, ambiguous keys, missing declaration metadata, invalid joins/digests, disallowed overrides and missing adapter generations.

Reuse these tests for real derivation/parity:

- `tests/Hexalith.EventStore.Client.Tests/Commands/CommandContractResolverTests.cs`.
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs`.
- `tests/Hexalith.EventStore.QueryRouting.Tests/DaprDomainQueryInvokerTests.cs`.
- `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionRouteCatalogTests.cs`.
- `tests/Hexalith.EventStore.Server.Tests/Commands/TrustedIdempotencyIntentTests.cs`.

Build each affected project with `dotnet build <project.csproj> -c Debug -p:UseHexalithProjectReferences=true`, then invoke built xUnit executables with single-dash `-class` filters and run the full affected suites. Restore after reference-mode changes. Start and inspect the Aspire baseline before code changes. `.github/workflows/ci.yml` already executes Contracts, Client, ServiceDefaults, Server and QueryRouting suites in Release/package mode. Catalog drift checks must execute in an existing lane, not merely produce source-text assertions. Tests have not run during this planning pass.
