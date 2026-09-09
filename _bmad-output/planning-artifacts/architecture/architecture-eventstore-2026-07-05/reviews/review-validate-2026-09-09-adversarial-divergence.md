# Reviewer Gate - 2026-09-09 VALIDATE Adversarial Divergence

**Subject:** `_bmad-output/planning-artifacts/architecture.md` (`status: final`, `updated: 2026-08-29`, 25 ADs, 609 lines).

**Lens:** Construct two units one level below the spine that each obey every applicable AD literally but choose incompatible shared-data shapes, owners, mutation paths, identities, runtime contracts, or deployment assumptions. A surviving pair is an architecture hole, even when one current implementation happens to choose a sensible answer.

**Scope:** Critique only. The spine was not changed. The deterministic pre-pass was reported as passing with zero findings.

## Verdict

**FAIL - CHANGES REQUIRED (2 critical, 7 high, 2 medium).** The deterministic structure is clean, but the semantic gate does not pass. The two critical pairs are not speculative: incompatible tenant representations and multiple production state-store profiles already exist in the tree. Seven high pairs leave internal authentication, delegated identity, AD-25 catalog authority, correlation, erasure, projection versioning, and poison-message handling open to mutually incompatible implementations.

This is a recurrence against an unchanged spine, not a new regression introduced since yesterday. The architecture memlog already records the 2026-09-08 validate result as 2 critical plus 10 consolidated high clusters and says the spine remained `final` pending an Update (`_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md:105`). The current source still has `updated: 2026-08-29` and does not bind the 2026-09-08 authentication ratification in `sources:` (`_bmad-output/planning-artifacts/architecture.md:10-36`).

## Severity and disposition

- **Critical:** independently buildable units can corrupt or split a production authority, cross tenant boundaries, or ship an unproved persistence guarantee now.
- **High:** independently buildable units can deny valid traffic, lose operational work, make an asserted guarantee untestable, or create conflicting security/audit truth.
- **Medium:** the pair is latent or fails closed, but still forces a downstream story to invent a cross-unit rule.
- **Autofix:** the missing rule is narrow enough to state in the spine without a product decision.
- **Discuss:** an owner must choose between materially different policies before wording can be safely fixed.
- **Defer:** acceptable only with an explicit interim rule and named owner; omission is not a disposition.

## Findings

### C1 - Tenant identity has no single source or canonical form, and the tree already implements incompatible identities (critical)

**Spine evidence.** AD-10 requires tenant authorization (`_bmad-output/planning-artifacts/architecture.md:147-151`), AD-8 scopes sequence guards by tenant (`:135-139`), AD-25 partitions admission by managed tenant (`:400-407`), and the State keys convention requires tenant identity to remain explicit across actor IDs, state keys, topics, query scopes, SignalR groups, and admin filters (`:464-468`). None specifies whether the authoritative value comes from the request, route, claim, or a fixed system tenant; none specifies case, trimming, alphabet, or the one component that normalizes it.

**Repository evidence.** `AggregateIdentity` lowercases tenant and domain and validates the normalized values against a lowercase-only grammar (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:9-16,23-37`). Gateway claim authorization preserves the raw claim and compares it ordinally (`src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs:33-45`). Admin authorization can infer an omitted tenant from the first raw tenant claim and also compares ordinally (`src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs:27-48`). Admin projection fallback invents the sentinel `all` and compares tenant values case-insensitively (`src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:22-26,61-80,203-206`). SignalR builds its group name from the raw authorized input (`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:76-82,104-127`). Generated REST route and claims paths preserve raw case and use ordinal comparison/deduplication (`src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:407-444`).

**Conforming Unit A.** The state/projection unit treats `AggregateIdentity` as canonical. It accepts `Acme`, derives `acme`, writes actors/read models/topics/admission directories under `acme`, and satisfies the explicit-identity, tenant-scoping, and canonical-validation language in AD-7/AD-8/AD-10/AD-25.

**Conforming Unit B.** The authorization/Admin/SignalR unit treats the authenticated claim as canonical and case-sensitive because tenant IDs are system-assigned. It authorizes and groups `Acme` exactly, satisfying AD-10 and the same explicit-identity convention. Nothing in an AD requires Unit B to call `AggregateIdentity` or Unit A to preserve claim case.

**Incompatibility.** One principal is authorized for `Acme`; its aggregate/admission/read-model state is stored under `acme`; Admin may match either spelling; and its SignalR client joins `Projection:Acme` while the producer broadcasts `Projection:acme`. Tenant isolation and one logical tenant-scoped operation hold within each unit but not across them. Case variants can also create distinct AD-25 partitions before an eventual lowercase aggregate lookup, splitting idempotency authority.

**Disposition: autofix.** Add one Tenant identity convention and refine AD-10: every non-system request declares exactly one tenant; a single normalizer in `Contracts` produces the `AggregateIdentity` lowercase form; authorization compares normalized request and normalized `eventstore:tenant` claims; all downstream hops carry that value without re-deriving or defaulting it; actor IDs, state keys, topics, groups, admission partitions, Admin filters, and audit records use it; `system` and any wildcard are reserved platform values and cannot be tenant claims.

### C2 - The production actor state-store provider is undecided even though AD-25 proof is provider-specific (critical)

**Spine evidence.** AD-9 makes AppHost, DAPR configuration, scopes, ACLs, resiliency, and topology tests one change unit (`_bmad-output/planning-artifacts/architecture.md:141-145`). AD-25 approves only the `oq8-postgresql-v1` evidence profile: DAPR 1.18.x, `state.postgresql`, two hosts, one PostgreSQL backend (`:451-459`). The Deferred table separately records that Redis allowed a durable sequence-1 value to be silently overwritten and expressly says no behavior is inferred for another provider (`:600-606`). Neither Stack, topology, nor a provider AD says which provider is allowed in production.

**Repository evidence.** Deployment documentation labels both PostgreSQL and Cosmos DB production and says they can be swapped with no application redeployment (`deploy/README.md:1-15,23-33`). The profiles are `state.postgresql` (`deploy/dapr/statestore-postgresql.yaml:15-29`) and `state.azure.cosmosdb` (`deploy/dapr/statestore-cosmosdb.yaml:15-36`); local AppHost uses `state.redis` (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:21-40`). Tests enumerate both production files and prove only `actorStateStore: true` plus the shared component name (`tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs:23-55,195-209`). The current Cosmos secret is also independently nonconforming with AD-24 because `masterKey` is an inline environment reference rather than `secretKeyRef` (`deploy/dapr/statestore-cosmosdb.yaml:23-28`); that defect can be repaired without resolving provider semantics.

**Conforming Unit A.** A platform slice deploys PostgreSQL, runs the exact two-host `oq8-postgresql-v1` packet, and treats its transaction/consistency results as AD-12/AD-25 evidence.

**Conforming Unit B.** A deployment slice chooses Cosmos DB, updates AppHost/YAML/scopes/topology tests atomically per AD-9, fixes the secret to AD-24 `secretKeyRef`, and supplies ordinary persisted-path tests per AD-12. It does not claim that Cosmos is PostgreSQL; no AD forbids the profile or requires an equivalent provider-specific AD-25 race packet.

**Incompatibility.** Both slices can call themselves production-conforming while only Unit A proves the transactional/strong-consistency behavior on which admission directories, actor turns, fences, and compaction depend. A shared component name and `actorStateStore: true` do not imply equivalent ETag/transaction/first-write behavior; the spine itself warns against that inference for Redis. The two deployments therefore implement different durability semantics below the same architecture.

**Disposition: discuss, then autofix.** Choose the production provider. The narrow safe rule is: `state.postgresql` under `oq8-postgresql-v1` is the only conforming production actor store; Redis is Development-only; every other provider is non-production until it passes an equivalent named AD-12/AD-25 packet on that provider. Topology validation must assert component type and evidence-profile identity, not just component name and actor flag. If provider choice is intentionally open, add it as an explicit Open Question and prohibit production claims meanwhile.

### H1 - “Application-layer credential” has no protocol or owner; two secure internal hosts cannot interoperate (high)

**Spine evidence.** AD-10 correctly says public, internal, domain-service, projection-notification, and admin-computation endpoints require application-layer credentials and tenant authorization; DAPR ACLs and network location are insufficient (`_bmad-output/planning-artifacts/architecture.md:147-151`). AD-18 assigns only outbound sidecar control-plane headers (`dapr-app-id`, `dapr-api-token`) to one handler (`:269-284`). No AD chooses the sidecar-to-application credential, claim set, validation component, forwarding rule, or rotation/readiness behavior.

**Repository evidence.** The gateway currently authenticates an allow-listed `dapr-caller-app-id` header and mints `system:{appId}` with `global_admin` (`src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs:9-15,20-45`), even though its own options call the header trusted because of DAPR mTLS/local sidecar placement (`src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationOptions.cs:5-17`). The composite gateway scheme chooses that header-based identity before JWT (`src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:65-101`). Operations instead validates a DAPR app-channel token before endpoints execute (`src/Hexalith.EventStore.Operations/Program.cs:13-18,33-44`). The DomainService SDK maps `/process`, `/query`, `/project`, and rebuild endpoints without installing authentication/authorization middleware (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:121-172,198-225`). The EventStore invoker sends a plain `DomainServiceRequest` over DAPR (`src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:78-100`), whose shared shape contains only command and current state (`src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs:3-13`).

**Conforming Unit A.** A domain-host SDK enforces the DAPR app-channel token (`APP_API_TOKEN`) on every non-probe route, validates tenant from a protected request context, and fails readiness when the token is absent outside Development. This is an application-layer credential above ACLs and obeys AD-10.

**Conforming Unit B.** The EventStore invoker presents a short-lived workload JWT with tenant/audience/scope claims, and a receiving host validates it. That is also an application-layer credential above ACLs and obeys AD-10.

**Incompatibility.** Unit A reads a shared-secret header; Unit B emits a bearer token. Each independently rejects the other. A third literal implementation can reproduce the current caller-app-id principal and still argue that JWT remains available for external paths. AD-10 names a property, not an interoperable wire contract.

**Disposition: discuss, then autofix.** Select one sidecar-to-app protocol and put its registration/middleware in `ServiceDefaults` or the domain-host SDK. Bind exact carrier, credential issuer/secret, required claims, tenant-binding rule, exempt probe paths, readiness failure, rotation owner, and the invoking handler. State explicitly that `dapr-caller-app-id` and ACLs are attribution/defense in depth, never authentication by themselves. Run AD-16 denial evidence on every host kind, not only the gateway.

### H2 - Admin mutation attribution does not choose operator pass-through or workload delegation (high)

**Spine evidence.** The design and runtime diagrams call Admin-to-gateway writes “delegated” (`_bmad-output/planning-artifacts/architecture.md:73-82,539-581`), while AD-10 only requires mutations to be “attributable and support-safe” (`:151`). No AD defines the authoritative actor, authenticated workload, delegation evidence, audit record, or token-lifetime behavior.

**Repository evidence.** The external Sample API forwards the operator bearer unchanged (`samples/Hexalith.EventStore.Sample.Api/Program.cs:20-39`). The gateway already has a dual-principal shape separating original actor (`sub`), workload (`azp`/`client_id`/`aud`), and delegation (`act`) (`src/Hexalith.EventStore/Authorization/DualPrincipalIdentity.cs:5-22`), but its helper says no OBO/service-account flow exists today (`src/Hexalith.EventStore/Authorization/DualPrincipalClaimsHelper.cs:12-22`). The internal DAPR authentication alternative instead creates a `system:{appId}` global administrator (`src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs:34-45`).

**Conforming Unit A.** Admin.Server forwards the operator's current bearer unchanged. Gateway authorization, command `UserId`, and Admin audit all use `sub`. It is attributable, tenant-authorized, and fail-closed under AD-10.

**Conforming Unit B.** Admin.Server performs durable/long-running work with its workload credential and a signed RFC 8693-style delegation record carrying the original actor. Gateway authorization uses workload plus delegation, while audit records both. It is also attributable, tenant-authorized, and fail-closed under AD-10.

**Incompatibility.** Unit A rejects service-principal writes after the operator token expires; Unit B's audit actor differs from an implementation that equates the presented principal with `UserId`. If a header-based internal principal is accepted, the current implementation can collapse every operator into `system:eventstore-admin` with `global_admin`. All choices can satisfy the adjective “attributable,” but they produce different authorization and audit truth.

**Disposition: discuss, then autofix.** For Phase 4, choose operator-token pass-through or a precise delegation protocol. Bind the audit tuple (original actor, authenticated workload, delegation id, tenant, action, target, outcome, correlation, timestamp), prohibit actor identity from body fields/placeholders, and specify which identity is written to command envelopes. If service delegation is deferred, explicitly forbid standing Admin credentials for gateway mutations until a separate AD approves it.

### H3 - AD-25's digest-key, adapter, and retention catalog is host-local, so a fleet can create two admission authorities (high)

**Spine evidence.** AD-25 says a registered trusted adapter supplies a versioned descriptor and fixed retention class (`_bmad-output/planning-artifacts/architecture.md:400-405`), partitions actors by digest-key version (`:406-415`), and requires a directory on every compatible host (`:428-440`). It rejects software that does not implement directory routing, but it never requires every host to use the same active key, reader set, adapter registry, descriptor versions, or retention catalog. AD-24's rotate-after-consumer-acknowledgement rule (`:380-392`) does not say digest-key retirement remains blocked by AD-25's live references.

**Repository evidence.** Active and reader digest versions are per-host options (`src/Hexalith.EventStore.Server/Configuration/IdempotencyAdmissionOptions.cs:3-31`); validation proves only internal consistency of that host's set (`src/Hexalith.EventStore.Server/Configuration/ValidateIdempotencyAdmissionOptions.cs:19-70`). The adapter registry is keyed by bare `CommandType`, not `(Domain, CommandType)`, and rejects duplicate bare names (`src/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterRegistry.cs:7-17,30-52,61-90`). Each adapter independently supplies `AdapterId`, `OperationId`, `DescriptorVersion`, and retention tier (`src/Hexalith.EventStore.DomainService/IIdempotencyIntentAdapter.cs:5-25`); the shared retention enum fixes durations but not assignment (`src/Hexalith.EventStore.Contracts/Commands/IdempotencyReplayRetentionTier.cs:5-15`).

**Conforming Unit A.** Host A is active `v2`, reads `[v1]`, and registers domain `orders` command `Create` as descriptor 2 / `Mutation`. It implements every directory phase and retains old aliases, satisfying AD-25.

**Conforming Unit B.** Host B is active `v1`, has no readers, and registers domain `tenants` command `Create` as descriptor 1 / `Commit`. It also implements every directory phase and passes the literal mixed-version rule because it supports directory routing.

**Incompatibility.** The same raw key routed to A then B can create distinct `v2` and `v1` canonical actors because B never derives the `v2` alias. Independently developed domains collide on bare `Create` at startup or force one side to silently namespace descriptor bytes, changing replay equivalence. AD-24 can also revoke a key after all runtime consumers acknowledge a generation while AD-25 still has records/tombstones/aliases/holds that require that version. The directory protocol is exact within one catalog and powerless across two catalogs.

**Disposition: autofix.** Define one deployment-overlay-owned, versioned fleet catalog containing active digest version, ordered readers, secret generation, `(Domain, CommandType)` adapter key, adapter/operation/descriptor version, and retention assignment. Every host compares a catalog digest at readiness; mismatch fails readiness. Bind digest keys to AD-24 as `runtime-required`; old material may be revoked only after both AD-24 acknowledgement and AD-25's zero-reference retirement condition hold.

### H4 - Correlation identity is a command-status join key but has no shared validator, generator, or propagation contract (high)

**Spine evidence.** AD-17 says the gateway's one status tracking field is currently `CorrelationId` and explicitly warns not to assume it equals `MessageId` (`_bmad-output/planning-artifacts/architecture.md:255-265`). The Identity convention only says identifiers use “ULID-safe handling where envelope semantics require sortable ids” and bans `Guid.TryParse` (`:464-466`). No AD defines the header, accepted grammar, max length, normalization, minting owner, hop propagation, or relationship to W3C trace context.

**Repository evidence.** Gateway middleware accepts a broad alphanumeric/hyphen token up to 128 characters and mints a sortable unique ID (`src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs:7-45`). Admin host middleware accepts only values parseable as `Guid`, mints a new GUID otherwise, and directly uses the forbidden parser (`src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs:6-26`). `CommandEnvelope` treats `MessageId` as command/status/archive identity but describes `CorrelationId` only as request tracing (`src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs:10-18,21-31,45-65`), reinforcing the unsettled status-key transition.

**Conforming Unit A.** Admin accepts a legacy GUID from `X-Correlation-ID`, persists it in an attributable audit record, and forwards it. GUIDs are nonempty and the architecture never expressly says this HTTP header is a sortable envelope identifier.

**Conforming Unit B.** Gateway rejects or normalizes the same value under a ULID-only interpretation, mints a new ULID, and uses the new value for `SubmitCommandResponse.statusKey`. It obeys the ULID-safe Identity convention and AD-17.

**Incompatibility.** Audit, response header, command envelope, status `Location`, and trace refer to different identifiers for one mutation. A different broad token can reverse which host accepts it. Neither unit violates a stated cross-host rule because none exists.

**Disposition: autofix.** Bind `X-Correlation-ID` to one validator/generator in `Contracts`; define canonical ULID and any bounded legacy grammar once; mint only at the first application boundary; echo and propagate unchanged; record it in envelope/status/audit/logs; state that `traceparent` is separate and additive. Resolve whether command status is keyed by `MessageId` or `CorrelationId` rather than leaving “today” as the contract.

### H5 - Erasure has two state authorities and no ordered completion rule (high)

**Spine evidence.** AD-7 defines read-model plus checkpoint erasure as one tenant/domain/aggregate/projection operation (`_bmad-output/planning-artifacts/architecture.md:129-133`). AD-23 independently gives EventStore the `IErasureStateProvider` seam while Parties owns legal orchestration/certificates (`:370-378`). AD-20 requires rebuilds to complete before promotion (`:311-315`). No AD orders key invalidation and projection deletion, declares one authoritative erasure state, or defines checkpoint behavior for an intentionally unreadable erased event.

**Repository/spec evidence.** The read-model erasure spec acknowledges no canonical writer (`Unsupported`), an accepted write-after-erase TOCTOU residual, and a caller-authoritative slot manifest (`_bmad-output/implementation-artifacts/spec-1-9-read-model-and-projection-checkpoint-erasure.md:100-110`). The payload-protection spec instead requires exactly one `IErasureStateProvider` and blocks reads/writes from `Invalidating` onward (`_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:938-950`). It defines per-scope erasure as wrapped-DEK/cache invalidation and distinguishes online invalidation from completed crypto-erasure (`:1370-1385`). Its runtime inventory requires no checkpoint advance for unreadable projection input (`:2153-2159`).

**Conforming Unit A.** Payload protection transitions the scope to `Invalidated`, deletes wrapped DEKs/cache entries, and truthfully reports “online access invalidated.” It does not delete plaintext read models because AD-23 does not assign that step to the provider.

**Conforming Unit B.** Projection lifecycle deletes the caller-listed registered read models and companion checkpoint/rebuild keys under AD-7, returning success. It does not invalidate event/snapshot keys because AD-7 does not assign cryptographic state.

**Incompatibility.** Each unit has a different terminal truth and neither constitutes complete erasure. Unit A can leave derived plaintext visible; Unit B can leave source events decryptable. After Unit A, a canonical AD-20 rebuild encounters unreadable events and, under the approved no-advance rule, can never reach the completion needed for promotion. After Unit B, normal delivery can recreate erased read models because its own spec accepts that residual.

**Disposition: discuss across EventStore and Parties, then amend AD-7/AD-23.** Choose one erasure-state authority and an ordered saga. Define which read-model/checkpoint targets are enumerated by the platform, when key invalidation occurs, what evidence permits a terminal certificate, and a normalized `Erased` projection outcome that advances past deliberately erased input without producing data. Distinguish logical read-model erasure, online crypto-invalidation, and full backup/replica expiry in the architecture.

### H6 - `ProjectionVersion` has provenance but no semantics, so producer and consumer can both comply and disagree (high)

**Spine evidence.** AD-15 allows `ProjectionVersion` only from persisted `IReadModelFreshness` and keeps it distinct from ETag (`_bmad-output/planning-artifacts/architecture.md:227-241`). AD-20 says rebuild projection versions/checkpoints must equal canonical replay (`:311-315`). Neither defines whether version is an aggregate sequence, projection checkpoint, content hash, composite value, or comparable token; no component owns its formatting.

**Repository evidence.** The shared interface explicitly calls `ProjectionVersion` an optional opaque string and gives both monotonic sequence and content hash as examples (`src/Hexalith.EventStore.Client/Projections/IReadModelFreshness.cs:22-34`). Server checkpoint keys are projection-scoped internal strings (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:14-24,602-615`), while Client owns a different marker namespace (`src/Hexalith.EventStore.Client/Projections/ReadModelBatchKeys.cs:3-14`). Admin deliberately duplicates Server command-status/archive state-key strings and requires manual synchronized edits (`src/Hexalith.EventStore.Admin.Server/Helpers/AdminStateStoreKeys.cs:3-31`).

**Conforming Unit A.** A projection persists a content hash as `ProjectionVersion`; replay of identical logical content produces the same version and satisfies AD-15/AD-20.

**Conforming Unit B.** Admin/UI treats `ProjectionVersion` as a monotonic last-applied aggregate or delivery sequence to compare “before” and “after”; this also fits the shared interface's example and the projection-confirmed rules.

**Incompatibility.** A valid hash cannot be ordered against a checkpoint, so Unit B cannot determine progress or verify replay convergence for Unit A. Separate projects can also re-key checkpoint/status storage independently because the architecture describes the scope but not the authoritative key factory. Every value has persisted provenance, yet the cross-unit claim is meaningless.

**Disposition: discuss, then autofix.** Either declare `ProjectionVersion` opaque and forbid ordering/progress semantics everywhere, adding a separate typed checkpoint/position, or define the exact monotonic meaning for aggregate-scoped and shared projections plus one platform formatter/comparer. Place checkpoint, marker, rebuild, command-status, and archive key derivation behind shared platform seams; Admin and Client must not restate Server-private formats.

### H7 - Poison/dead-letter behavior and the Operations host have no architecture owner (high)

**Spine evidence.** AD-8 defines at-least-once, unordered delivery, deduplication, gap behavior, and evidence of success (`_bmad-output/planning-artifacts/architecture.md:135-139`) but never defines permanent-invalid classification, retry bound, acknowledgement, dead-letter topic identity, tenant/domain scoping, durable acceptance, replay target, or checkpoint effect. `Hexalith.EventStore.Operations` is absent from the Structural Seed (`:515-537`), AppHost topology (`:539-582`), and capability map (`:584-596`).

**Repository evidence.** The DomainService subscriber returns `200 OK` for invalid payload and says DAPR should drop it unless a dead-letter topic is attached (`src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs:34-42,47-61`). Operations subscribes to a default `deadletter.work.events`, replays to app `works` / method `work/events`, and embeds those domain-specific defaults in platform options (`src/Hexalith.EventStore.Operations/Configuration/EventStoreOperationsOptions.cs:6-27`). Its capture route admits that failures exceeding a finite retry budget lose the dead letter (`src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs:60-69,78-119`), and its operator authorization merely checks caller app id plus any nonblank bearer token (`:227-235`). The host exists and maps a state-writing actor and endpoints (`src/Hexalith.EventStore.Operations/Program.cs:13-46`), but the canonical AppHost resource graph contains no Operations resource (`src/Hexalith.EventStore.AppHost/Program.cs:84-104,148-240`).

**Conforming Unit A.** A subscription SDK classifies invalid payload as permanent, acknowledges `200`, logs it, and advances/no-ops according to AD-8. Since no AD requires durable poison retention, this is compliant.

**Conforming Unit B.** An Operations deployment expects the subscriber/broker to route permanent failures to `deadletter.{tenant}.{domain}.events`, durably captures them before acknowledgement, and exposes replay to a configured consumer. This is also compatible with AD-8 and AD-10 when correctly authenticated.

**Incompatibility.** Unit A's `200` prevents Unit B from ever seeing the poison message; Unit B's current default topic/target handles one `work` domain and cannot consume the per-tenant/domain topics documented elsewhere. A retryable capture outage can still lose the only copy. The architecture promises operational backlog capability (FR34-FR35) without owning the host that implements it.

**Disposition: discuss, then add a poison-delivery AD.** Bind normalized outcomes (`Processed`, `Duplicate`, `PermanentInvalid`, `Retryable`, `DeadLetterAccepted`), which outcomes may advance checkpoints, the retry/dead-letter handshake, topic derivation, capture durability/ack order, tenant-safe identity extraction, replay authorization, configured target lookup, and telemetry. Add Operations to the seed, topology, ACL/scopes, AppHost/deployment/release identity, and capability map, or explicitly remove/defer it and prohibit claims that it is part of the platform.

### M1 - Domain and projection route ownership is not unique across app IDs (medium)

**Spine evidence.** AD-2 says each domain service owns its aggregate/projection implementation (`_bmad-output/planning-artifacts/architecture.md:99-103`); AD-19 requires one normalized entry per admitted `(Domain, ProjectionType)` (`:286-309`). Neither says a domain has exactly one app ID or a route key has exactly one serving host.

**Repository evidence.** The domain invoker resolves a registration but falls back to `command.Domain` as the app ID (`src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:78-84,106-113`). The DomainService SDK explicitly permits a host to override its own `/project` route (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:198-205,227-238`).

**Conforming Unit A.** App `tenants` serves commands and projection `TenantList` for domain `tenants`.

**Conforming Unit B.** App `tenants-projections` serves only `TenantList` for the same domain as an independently scalable projection host. Both are domain-centric and both can update AppHost/YAML atomically under AD-9.

**Incompatibility.** Discovery admits two handlers for one `(Domain, ProjectionType)`, while AD-19 requires exactly one normalized entry. A dispatcher either invokes both and becomes indeterminate, or chooses one using an unstated ordering rule.

**Disposition: autofix.** Define a versioned platform route catalog with exactly one app-id/method owner per `(tenant scope, domain, service version, operation)` and per `(domain, projection type)`. Duplicate ownership fails readiness; projection offload requires a new explicit AD rather than a second registration.

### M2 - The current aggregate actor exposes both fenced and unfenced mutation entry points (medium, ratification gap)

**Spine evidence.** AD-5 says `AggregateActor` is the sole append owner and accepts only an internal command envelope plus current-fence context after successful AD-25 admission (`_bmad-output/planning-artifacts/architecture.md:117-121`). AD-25 requires exactly the current non-zero fence at every mutation/side-effect boundary (`:400-415`). The spine does not state how legacy public actor methods are retired or prohibited at the interface boundary.

**Repository evidence.** `IAggregateActor` exposes both `ProcessFencedCommandAsync(FencedCommandEnvelope)` and `ProcessCommandAsync(CommandEnvelope)` (`src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs:11-25`). The implementation sends the fenced method to `ProcessCommandCoreAsync` with context, but the unfenced methods pass `null` (`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:144-150,346-368`). Fence validation explicitly returns when context is null (`:4167-4179`).

**Conforming Unit A.** New gateway/admission code calls only `ProcessFencedCommandAsync` and satisfies AD-5/AD-25.

**Conforming Unit B.** A legacy internal caller uses the still-public actor interface's `ProcessCommandAsync`, interpreting “after successful admission” as a caller precondition and keeping fence context inside its own unit. Its surrounding story can claim AD conformance without a compiler-visible carrier requirement.

**Incompatibility.** Unit B crosses the actor mutation boundary without evidence the actor can verify. Current code then treats absence as permission. This is presently an implementation nonconformance as well as a ratification hole; the architecture does not force downstream builders to remove the legacy seam.

**Disposition: autofix and implementation follow-up.** State that the actor interface has exactly one mutation method whose request contains the protected execution context; missing/zero/invalid context fails closed. Unfenced methods are forbidden after AD-25 activation and may remain only on an explicitly Development/test-only adapter that cannot be registered in production.

## Passes and closures

- **Deterministic completeness:** reported lint result is zero findings. This review found no duplicate/nonmonotonic AD IDs, missing Binds/Prevents/Rule fields, or placeholder-based mechanical failure.
- **AD-11/AD-22 release identity remains closed.** The manifest, canonical codec, content-addressed subject/evidence, immutable failed tags, exact OCI index graph, one-use authority, role receipts, and Consumer-owner receipt rules remain single-owner and sufficiently exact (`_bmad-output/planning-artifacts/architecture.md:153-165,323-368`). No conforming Unit A/Unit B pair could choose different deployed identities.
- **AD-24 base secret-store/provider contract remains closed.** `openbao`, `secretstores.hashicorp.vault/v1`, overlay ownership, value-free catalog, default-deny scopes, bootstrap boundary, `GetSecret` lifecycle, bounded cache, and publish-overlap-acknowledge-revoke are exact (`_bmad-output/planning-artifacts/architecture.md:380-398`). The remaining AD-25 rotation issue is the missing cross-AD digest-key binding in H3, not a reopening of the base secret-store choice.
- **AD-19 normalized dispatch matrix is internally exact.** The admitted key, duplicate/missing/unknown normalization, explicit `Advanced`/`NotAdvanced`, and “no alternate shape” rule leave no semantic choice within that matrix (`_bmad-output/planning-artifacts/architecture.md:286-309`). H6 concerns the meaning of a separate `ProjectionVersion`; M1 concerns unique route ownership before the matrix can be applied.
- **AD-14/AD-15 provenance ownership remains closed.** `ProjectionBacked`, `HandlerComputed`, `Unknown`, the ETag/version separation, and fail-unknown rendering are unambiguous (`_bmad-output/planning-artifacts/architecture.md:181-241`). H6 does not dispute provenance; it shows that two genuine persisted versions may still mean incompatible things.
- **AD-17 Location authority and AD-18 outbound DAPR sidecar headers remain closed.** The gateway owns absolute command-status `Location`, and a single handler replaces outbound `dapr-app-id`/`dapr-api-token` (`_bmad-output/planning-artifacts/architecture.md:255-284`). H1 is the different, currently unnamed sidecar-to-application credential; H4 is the tracking value itself.
- **AD-25 tombstone and migration phase shapes remain closed.** The tombstone fields, indistinguishable expiry response, promotion phase order, redirect, and legacy fail-closed rules are precise (`_bmad-output/planning-artifacts/architecture.md:417-449`). H3 attacks deployment-wide catalog agreement outside those otherwise sound per-record protocols.

## Required gate disposition

The spine should remain failed until at least C1 and C2 are decided and incorporated, because they permit already-real production divergence. H1-H7 should be closed or explicitly deferred with interim prohibitions and named owners before stories can claim architecture conformance on those surfaces. M1-M2 are suitable direct clarifications once their owners confirm the intended single route/mutation entry points.
