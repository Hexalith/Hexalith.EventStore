# Platform / Public-Contract Review — PRD Validation 2026-10-07

## Verdict

**PRD platform contract: NOT ACCEPTED as complete. Two Critical, seven High.** The 2026-10-07 PRD has absorbed every 2026-09-10 platform-contract finding at requirement level: NFR2 states one canonical tenant contract, FR12-C3/C4 and §8.2 make `MessageId` the only status selector, NFR7/G-APPEND refuse to count the append race as closed, and NFR12/NFR3/FR36 are now defined by capability and backed by gates. Where code still contradicts those rules (generator tenant handling, the generator `MessageId ?? CorrelationId` fallback, the shared actor-state store), the PRD records it truthfully with a failing gate and a named owner. The independent pass found a different class of gap: public-boundary behaviour that code ships today and that no mandatory gate would catch. (1) The public gateway maps Dapr actor callbacks with no app-layer authentication, so a client that can reach its port can call aggregate-actor methods directly. (2) The gateway status endpoint still resolves `CorrelationId` values to status records, which contradicts §8.2. G-STATUS-ID tests only `Location`, so it could pass while that contradiction is live. Other gaps are at contract level. NFR2 does not say whether a credential grant can set the request tenant scope, and AD-27 says it cannot. NFR3 contradicts the workload-assertion profile in AD-36. Generated command endpoints have no retry-safe identity or idempotency-key channel. The amended NFR1 UI-host exception is not closed in the PRD and contradicts AD-16 as currently written. Dead-letter CloudEvents use a caller-reusable `CorrelationId` as their `id`. The readiness `Reject` stays correct and was not re-judged. All findings concern contract precision and gate coverage, and none recommends a second reviewer.

**Finding counts:** Critical 2 · High 7 · Medium 11 · Low 5

## Reviewed Baseline

- Repository `HEAD`: `40c92e085d8a6463d469c1b340c410fec84a690f` (worktree dirty; see note below).
- `prd.md` SHA-256 `d5632ba71c838ba7f0b8ca61a24889cb21edb4506531e823d8c0dfb12b7e6ae6` (unmodified against `HEAD`).
- `architecture.md` SHA-256 `7fd805a871883a7594b9df89c9146b1e84a42a86ddab5d6ca500fbe13387fc09` (`status: draft`).
- The uncommitted legacy-replay routing changes are not judged as the baseline: `src/Hexalith.EventStore.Client/Aggregates/{AggregateReplayer,EventStoreAggregate,LegacyReplayInput}.cs`, the new `IAdmittedLegacyAggregateReplay.cs`, and `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` (+103/−25). At `HEAD`, `DomainServiceRequestRouter.Replay`/`ReplayAsync` refuse versioned replay (`RefuseVersionedReplay`, lines 172 and 221 of the `HEAD` blob).
- Method: read-only. File:line evidence comes from grep/read of committed sources. Nothing was built and no tests were run, except one 2-line `dotnet run` scratch probe of `ToLowerInvariant` (M1).

**Severity rule used.** Critical means either of two things. The code contradicts a public-boundary rule and no mandatory gate would detect it, or the code lets unauthenticated, cross-tenant, or silent-loss behaviour through. Contradictions the PRD already records under a failing mandatory gate with a named owner are listed in "Captured contradictions" and are not counted.

## Critical

### C1 — The public gateway exposes Dapr actor callbacks with no app-layer authentication, and no PRD gate binds that surface

- **PRD location:** §7 NFR1 (line 361); §8.2 bullet 6 (line 457); §6.5 FR28 (line 302); §10 SM10 (line 543); §11.2 NFR1 row (line 609); §11.4 G-AUTH-HOSTS (line 686).
- **Quoted text:** NFR1: "Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture … the fail-closed default is never weakened to reach either exception." G-AUTH-HOSTS: "One versioned JWT contract/fingerprint consumed by every externally reachable or JWT-binding host … with negative Production/break-glass/algorithm/issuer/audience/signature/lifetime/role/tenant tests".
- **Evidence:**
  - `src/Hexalith.EventStore/Program.cs:47-48` maps `MapSubscribeHandler()` and `MapActorsHandlers()` on the same pipeline as the public REST API. No `RequireAuthorization`, app-channel-token middleware, or fallback policy covers them: `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:105` calls a bare `AddAuthorization()`, and no `/actors` guard exists anywhere in `Hexalith.EventStore`, `.Server`, or `.ServiceDefaults`. Operations, by contrast, guards its routes with `DaprAppChannelTokenMiddleware` (`src/Hexalith.EventStore.Operations/Program.cs:34-39`), but only when a token is configured.
  - The gateway hosts the aggregate actors in-process (`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:240-252`).
  - `IAggregateActor.GetEventsAsync(long fromSequence)` (`src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs:44`) takes no tenant or caller context. Its actor ID is the tenant/domain/aggregate identity.
  - Taken together, any HTTP client that reaches the gateway port can address `PUT /actors/AggregateActor/{id}/method/...` directly, bypassing gateway authentication, NFR2 tenant validation, and AD-5 single activation. This is a by-inspection finding and was not exercised.
  - The only anonymous-endpoint tests enumerate endpoints that carry `IAllowAnonymous` metadata (`tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs:639-654`; `tests/Hexalith.EventStore.Server.Tests/HealthChecks/DefaultHealthEndpointResponseWriterTests.cs:21-35`). Endpoints that are anonymous only because there is no fallback policy, such as the gateway's `/actors/*`, are invisible to that test design.
  - Architecture records the gap only as peer exposure ("the gateway and Operations map `MapSubscribeHandler` and `MapActorsHandlers` without authorization; `/**` ACLs let peers reach …", `architecture.md:548`). It does not mention the external port.
- **Why critical:** The code lets unauthenticated, cross-tenant read (and, through the keyless legacy path, mutation) through on a public host. The PRD's only routing is an ownership note to Story 5.14 in §11.2. G-AUTH-HOSTS is JWT-only, and SM10's `nfr1_surfaces` inventory has no completeness rule that would force Dapr framework or actor-callback routes into it.
- **Suggested fix:**
  - Add an NFR1 clause to §7.1 and bind it in §11.4, either as a new gate or as an explicit extension of G-AUTH-HOSTS. Every endpoint on every host must carry an authorization requirement or appear in the closed anonymous list.
  - Dapr framework routes (subscription discovery, pub/sub delivery, actor and reminder callbacks) must be AD-28 sidecar-channel authenticated, and readiness must fail when the token is unconfigured outside Development.
  - Require an all-endpoints test that fails on any endpoint carrying neither `IAllowAnonymous` nor authorization metadata, run against the gateway, Operations, Admin Server Host, Admin UI, and generated-host fixtures.
  - Define the `nfr1_surfaces` universe so that framework routes are mandatory members.

### C2 — `CorrelationId` still selects public command-status resources at the gateway, and G-STATUS-ID cannot detect it

- **PRD location:** §8.2 bullet 4 (line 455); §7.1 FR12-C4 (line 404); §11.4 G-STATUS-ID (line 681); OR21 (line 735).
- **Quoted text:** §8.2: "A public command-status resource is selected only by a `MessageId` valid under the endpoint's declared contract version. `CorrelationId` remains diagnostic and cannot be substituted for status identity". FR12-C4: "A `CorrelationId` never selects a status resource, including when it differs from or is present without `MessageId`." G-STATUS-ID: "… proving only `MessageId` can select status `Location`."
- **Evidence:**
  - `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:112-146` resolves the path segment through `ICommandCorrelationIndex.ResolveAsync`, described there as "the bounded index is the sole compatibility lookup for a correlation identifier". On a unique hit it returns that command's status. The public OpenAPI documentation advertises "message or correlation identifier" (`CommandStatusController.cs:53,56,177`).
  - The index is registered by default (`src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:163`).
  - Tests pin the behaviour: `tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs:97` (`GetStatus_UniqueCorrelationIndexResolution_ReadsMessagePrimaryRecord`).
  - Done Story 4.2 mandates this lookup: "a bounded tenant-scoped one-to-many correlation index … ambiguity directs the caller to `MessageId`" (`epics.md:3365-3366`).
  - Architecture AD-17 ("`CorrelationId` … never selects a command record") and AD-32 ("never … used for status identity", `architecture.md:381`) agree with the PRD, not with the code.
  - Story 2.15's acceptance criteria remove only "every `MessageId ?? CorrelationId` fallback … from the generator, its tests, and its documentation" (`epics.md` Story 2.15).
- **Why critical:** A stated public-boundary rule is contradicted by shipped, test-pinned behaviour. The mandatory gate and its owning story cover only the generator `Location` path, so G-STATUS-ID could pass with the contradiction still live. Tenant scoping is preserved, so this is not a cross-tenant leak.
- **Suggested fix:** Choose one rule and apply it everywhere.
  - Option (a): retire the correlation lookup under NFR12, with a deprecation path and a SemVer-major release, and extend FR12-C4, G-STATUS-ID, and Story 2.15 to the gateway status resource and the Story 4.2 text.
  - Option (b): narrow §8.2 and FR12-C4 so they explicitly admit one bounded, tenant-scoped, deprecated compatibility lookup with a stated exit, and reconcile AD-17, AD-32, and Story 4.2 to that wording.

## High

### H1 — NFR2 does not say whether a credential grant can set the request tenant scope; AD-27 says it never can, and the default generated mode does

- **PRD location:** §7 NFR2 (line 362); §3.3 UJ2 (line 170); §8.2 bullet 3 (line 454).
- **Quoted text:** NFR2: "Every tenant-scoped request and `eventstore:tenant` grant must resolve exactly one explicit tenant … Missing, duplicate, conflicting-after-normalization …". UJ2: "the host resolves exactly one authorized tenant".
- **Architecture:** AD-27: "the scope comes from the request route, body, or headers … tenant grants in a credential authorize but never set the scope" (`architecture.md` AD-27 Rule).
- **Evidence:**
  - The default `RestApiAttribute(tenantSource = RestTenantSource.Claims)` (`src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs:19`; `RestTenantSource.cs:9`) makes the generated controller use the sole `eventstore:tenant` grant as the request tenant (`src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:432-444`).
  - The gateway status endpoint uses every grant as a lookup scope (`CommandStatusController.cs:90-114`).
  - Admin Server fills a missing tenant with the caller's first grant (`src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs:31-37`).
  - NFR2 conflates two cardinalities: exactly one request scope, and the grant set, where a principal may legitimately hold many grants. "Duplicate" is undefined: it could mean duplicate request values or duplicate grants after normalization.
  - Story 2.14's acceptance criteria do not mention the `Claims` mode.
- **Suggested fix:** State the scope-source rule. Either the scope comes from route, body, or header only and the `Claims` mode is classified as a breaking change under NFR12, or sole-grant derivation is admitted explicitly as a cataloged mode with AD-27 amended to match. Define grant-set semantics: many grants are allowed, each is canonicalized, and duplicates after normalization are collapsed rather than rejected. Mirror the rule in UJ2, §8.2, and Story 2.14.

### H2 — G-TENANT's required evidence is narrower than NFR2 and narrower than Story 2.14's extended scope

- **PRD location:** §11.4 G-TENANT (line 680); §7.1 FR12-C2 (line 402).
- **Quoted text:** G-TENANT: "compiled generated-controller and Tenants runtime tests for mixed-case request/grant normalization plus missing, duplicate … reserved `system` inputs".
- **Evidence:**
  - Story 2.14 was extended on 2026-10-07 to the gateway `ClaimsTenantValidator`, Admin Server tenant checks, the SignalR hub, and admin storage (`epics.md` Story 2.14, third acceptance criterion and "Current reconciliation").
  - The gateway lets a global administrator reach any tenant, including `system` and an empty tenant (`src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs:22-25`), and compares tenants with `Ordinal` without normalizing (`:44-45`). The SignalR hub uses the same validator (`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:15,27`). Admin Server compares with `Ordinal` (`AdminTenantAuthorizationFilter.cs:42`).
  - So G-TENANT and FR12-C2 can pass on generator and Tenants tests while the gateway, the hub, and Admin Server still violate NFR2.
- **Suggested fix:** Make G-TENANT's governed universe every NFR2 boundary by name: gateway command, query, status, stream, and admin-storage routes; the SignalR hub; Admin Server; generated controllers; and the Tenants host. Add a §7.1 clause for the non-generator boundaries owned by Story 2.14, and require identical accept/reject results across all of them.

### H3 — Generated command endpoints have no retry-safe identity or idempotency-key contract

- **PRD location:** §6.2 FR12 (line 256); §6.4 FR27 (line 288); §7 NFR7 class (e) (line 367); UJ2 (line 170).
- **Quoted text:** FR27: "provide an EventStore-owned, tenant-scoped durable admission contract accepting only a trusted, versioned canonical-intent descriptor …". FR12 says nothing about how an external caller supplies a key or a `MessageId`.
- **Evidence:**
  - Every generated command action mints a new `MessageId` server-side (`RestApiControllerEmitter.cs:266-267`, `UniqueIdHelper.GenerateSortableUniqueStringId()`) and never forwards `IdempotencyKey` or `CorrelationId`.
  - Durable admission runs only when a key is present (`src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs:24-27`; `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:65`). Keyless commands run the legacy path in every environment.
  - Architecture AD-5 requires "a non-empty opaque idempotency key, a successful AD-25 admission, and the current nonzero fence" for every production mutation, and says unfenced entry points "fail closed or remain unmapped outside Development".
  - As a result, every client retry through a generated API is a new command, which is NFR7 class (e) duplicate side effects. Once AD-5 is enforced, generated APIs must either fail closed or bypass admission.
  - The roles of `MessageId` (FR23 duplicate replay) and `IdempotencyKey` (FR27 admission) are not defined for consumers.
- **Suggested fix:** Add an FR12 clause defining one caller-supplied idempotency channel (for example an `Idempotency-Key` header mapped to `IdempotencyKey`) and whether callers may supply `MessageId` under the declared version. Define how `MessageId` dedupe relates to keyed admission. State that keyless production mutations fail closed, extend NFR7(e)/SM11 to keyless entry points, and bind the clause to G-STATUS-ID or G-OQ8.

### H4 — NFR3 contradicts the AD-10/AD-36 credential profiles, and the PRD has no workload-versus-delegated-user call contract

- **PRD location:** §7 NFR3 (line 363); §6.5 FR28 (line 302); §8.2 bullets 5-6 (lines 456-457).
- **Quoted text:** NFR3: "Role and tenant validation also remain mandatory in every mode and are owned by NFR1 and NFR2." FR28: "require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints".
- **Evidence:**
  - AD-10 defines three versioned validation profiles, each with its own per-host fingerprint: human bearer, AD-36 workload assertion with tenant, role, and administrator claims forbidden, and resource-bound delegation (`architecture.md:186`).
  - AD-36 says a workload principal "never carries tenant, role, or administrator claims", and every sidecar-routed call is exactly one of delegated-user or workload (`architecture.md` AD-36 Rule).
  - Read literally, NFR3 makes every workload assertion nonconforming. The PRD never names the call kinds that domain modules depend on: Tenants domain-service hosts, trusted-effect submitters, and generated hosts that forward a bearer.
  - Generated API hosts hand-roll delegated-user bearer forwarding (`samples/Hexalith.EventStore.Sample.Api/Services/InboundBearerForwardingHandler.cs:9-17`, wired at `Program.cs:36-39`). No platform handler exists in `src/Hexalith.EventStore.Client/Handlers/`.
- **Suggested fix:** Restate NFR3 per validation profile, with mandatory and forbidden claims for each. Promote the AD-36 call-kind rule (one kind per call, never both, workload principals never satisfy human policies) into FR28 or §8.2. Require G-AUTH-HOSTS evidence per profile fingerprint, and require a platform-owned delegated-user forwarding handler, in line with FR1/SM8.

### H5 — The amended NFR1 UI-host exception is not closed in the PRD and contradicts AD-16; other shipped anonymous endpoints fit neither exception

- **PRD location:** §7 NFR1 (line 361); §9.2 bullet 4 (line 507); §11.2 NFR1 row (line 609); SM10 (line 543).
- **Quoted text:** NFR1: "on interactive UI hosts only, an enumerated set of static framework assets and authentication-protocol callback endpoints that carry no tenant, operational, or user data, each explicitly pinned `AllowAnonymous`, support-safe, and enumerated by endpoint-metadata tests (AD-16)".
- **Evidence:**
  - The PRD never enumerates the set. The tests are designated as the enumeration, which makes the contract open-ended.
  - "Static framework assets" is undefined. `MapStaticAssets` publishes app-owned `wwwroot` and RCL `_content/*` files as well as `_framework/*`. "Authentication-protocol callback endpoints" names no protocol or paths, and interactive login (IAM-1) is backlog, so nothing exists to enumerate today.
  - AD-16 still reads "Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata. Endpoint metadata tests enumerate exactly those" (`architecture.md:244`). Architecture records the amendment as pending (`architecture.md:548`).
  - The Admin UI has no fallback policy (`src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:37`), maps static assets and Razor components without authorization (`:183`, `:188-189`), and maps `/dapr/subscribe` anonymously (`:173`).
  - The gateway also serves anonymous `/problems/*` documents (`src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs:149-164`) and `/openapi/v1.json` plus `/swagger`, which are enabled by default (`src/Hexalith.EventStore/Program.cs:36-42`). Neither fits either NFR1 exception class.
  - No story owns the UI-asset enumeration tests.
- **Suggested fix:**
  - Enumerate the class in the PRD by rule. For example: GET/HEAD only; static-asset manifest entries under `_framework/` and `_content/` with no auth state; and OIDC `signin-oidc`/`signout-callback-oidc` admitted only when IAM-1 lands.
  - State that the exception stays inert until a UI host enters a production profile.
  - Decide explicitly whether documentation endpoints (problem-type references, OpenAPI) are anonymous exceptions or must authenticate.
  - Require the AD-16 amendment in the same change, and assign an owning story and test.

### H6 — Dead-letter CloudEvents use the caller-reusable `CorrelationId` as their `id`

- **PRD location:** §6.4 FR23 (line 286); §6.7 FR34 (line 323); §7 NFR6 (line 366); NFR12 "CloudEvent wire contracts" (line 372).
- **Quoted text:** FR23: "CloudEvent IDs must use the event `MessageId`". NFR6: "subscribers must deduplicate by `MessageId`".
- **Evidence:**
  - `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:56` sets `["cloudevent.id"] = safeMessage.CorrelationId` under source `eventstore/{tenant}/{domain}`.
  - Callers may supply and reuse a correlation ID across commands (`src/Hexalith.EventStore/Controllers/CommandsController.cs:184`; AD-32 propagates it unchanged).
  - CloudEvents requires `source` + `id` to be unique per distinct event. A consumer or broker that deduplicates by `id`, as NFR6 directs, can therefore silently collapse distinct dead letters that share a correlation.
  - Publication failure is swallowed as non-blocking (`DeadLetterPublisher.cs:77` catch block).
  - FR23 covers only events, FR34 is silent on dead-letter identity, and AD-8 requires durable poison records "keyed by stable `MessageId`".
- **Suggested fix:** Extend FR23 or FR34-C1 so that every CloudEvent EventStore publishes, including dead letters, has a unique, stable, `MessageId`-derived `id`, and state that correlation is never message identity. Classify the wire change under NFR12 and make dead-letter publication durability explicit.

### H7 — A projection-backed `304` can be returned for changed data, and the PRD neither forbids it nor gates it

- **PRD location:** §6.1 FR4 (line 239); §7 NFR8 (line 368); §7.1 FR4-C3/C6 (lines 389, 392).
- **Quoted text:** NFR8: "freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed".
- **Evidence:** Architecture records that "Under `Direct`, a lost regeneration may return `304 Not Modified` on changed data" and routes the fix only to "Story 5.5 owner confirms by test, then routes retry or repair …" (`architecture.md:549`; AD-8 "Freshness signals", line 170). The client accepts a `304` as projection-confirmed whenever provenance is `ProjectionBacked` and a strong ETag is present (`src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:205-228`). The PRD therefore labels as authoritative a response that architecture knows can be stale.
- **Suggested fix:** Add an FR4 clause: a projection-backed `304` is allowed only when its validator derives from persisted read-model or checkpoint state, and otherwise the response is `200` or degraded. Name the owner and bind the clause under G-CLAUSE.

## Medium

### M1 — NFR2's "invariant lowercase" normalization admits non-ASCII aliases

- **PRD:** NFR2 (line 362): "invariant lowercase case normalization before comparison and authorization, then validates the … grammar … Whitespace or other characters are not trimmed or repaired."
- **Evidence:** .NET `"Kcme".ToLowerInvariant() == "kcme"` returns `True` (verified with a scratch `dotnet run` probe). U+212A KELVIN SIGN folds to ASCII `k` and then passes the grammar, so a non-ASCII spelling, or a non-ASCII grant, reaches tenant `kcme`. This contradicts "not repaired".
- **Fix:** Specify ASCII-only case folding (A–Z to a–z) and reject any non-ASCII code point before normalization. Add Kelvin-sign and dotted-I negatives to G-TENANT.

### M2 — The MessageId version is undefined for shared surfaces, and "version-ambiguous" is undefined

- **PRD:** Glossary "MessageId Contract Version" (line 198); FR12 (line 256): "An absent, blank, invalid-for-version, unavailable, or version-ambiguous `MessageId` must omit `Location`".
- **Evidence:** AD-17 assigns a version per command-contract declaration (`architecture.md:252`). The generic `POST /api/v1/commands` and the shared `GET /api/v1/commands/status/{messageId}` have no declared version. The gateway validator accepts the v1 grammar for every command (`src/Hexalith.EventStore/Validation/SubmitCommandRequestValidator.cs:35-40`). Every v2 value is also a valid v1 value, so ambiguity cannot be decided syntactically.
- **Fix:** Assign a version, or a resolution rule, to the generic submit and status surfaces, and define "version-ambiguous" operationally, for example as "the endpoint has no single declared version".

### M3 — NFR5 reads as configurable defaults; code lets operators raise the limits that architecture calls ceilings

- **PRD:** NFR5 (line 365): "at most 16 entries and 2048 total UTF-8 bytes, as configured by `ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataEntries` and `DefaultMaxDetailMetadataBytes`".
- **Evidence:** The options validators check only `> 0` (`src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs:125-131`; `src/Hexalith.EventStore/SignalRHub/SignalROptions.cs:59-65`). AD-8 says the limits are "`Contracts`-owned ceilings … options may lower but never raise them", counting "keys plus values", with receivers rejecting and broadcasters clipping (`architecture.md` AD-8).
- **Fix:** Restate NFR5 as non-raisable ceilings over keys plus values, with receiver-reject and broadcaster-clip behaviour, and add raising-attempt negatives to Story 2.16's evidence.

### M4 — The `ProjectionVersion` token semantics are missing from the PRD

- **PRD:** FR4 (line 239) lists "projection version" as propagated metadata only.
- **Evidence:** AD-15 makes it "an optional, bounded, header-safe, opaque equality token … never parse, order, or increment" (`architecture.md` AD-15). The PRD forbids inferring lifecycle from ETags but says nothing about ordering or parsing `ProjectionVersion`. This is a consumer-visible header protected by NFR12.
- **Fix:** Add an FR4 clause carrying AD-15's equality-only, scope-bounded, may-change-on-rebuild semantics.

### M5 — There is no canonical public error contract, and generated hosts diverge from the gateway

- **PRD:** NFR12 (line 372) inventories "Problem Details codes"; FR12-C1 (line 401) requires "safe Problem Details", which is not testable as written.
- **Evidence:** Generated controllers emit `Type = "about:blank"` for their own failures (`RestApiControllerEmitter.cs:667`). The gateway uses a catalog of 20 URIs (`src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs:8-…`) with `code`/`category`/`retryable`/`clientAction` extensions (`src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs:86`). The same tenant rejection therefore carries different `type`s depending on the host.
- **Fix:** Define the stable error contract (type-URI catalog, required extensions, retryability, `Retry-After` rules, support-safe detail), and require the generator to match the gateway catalog.

### M6 — The gateway's own 202 `Location` is built from the request `Host`

- **PRD:** FR12 (line 256) regulates only "An accepted generated command"; AD-17 says "built from trusted gateway configuration".
- **Evidence:** `src/Hexalith.EventStore/Controllers/CommandsController.cs:193-195` builds `$"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{statusKey}"` with `AllowedHosts: "*"` (`src/Hexalith.EventStore/appsettings.json:9`). The header reflects any caller-supplied `Host`.
- **Fix:** Extend the absolute, trusted-configuration `Location` rule to every public 202, including the gateway, or require a validated forwarded-host configuration.

### M7 — NFR12 has no deprecation window or signalling, and approval is not tied to the version bump

- **PRD:** NFR12 (line 372): "deprecations require a documented replacement and migration path; removals … require an approved breaking-change proposal and SemVer-major release." G-COMPAT (line 685).
- **Evidence:** There is no minimum support or deprecation period, no `[Obsolete]`/diagnostic-ID, OpenAPI `deprecated`, or `Deprecation`/`Sunset` header requirement, and nothing binds the approval record to the commit-derived semantic-release major bump. No API baseline tooling exists (no `PublicAPI*.txt`, `EnablePackageValidation`, or ApiCompat in the repository), which is consistent with G-COMPAT's FAIL.
- **Fix:** Add a support window, deprecation signalling per surface kind, and a release-lane check that a major bump carries an approved proposal digest.

### M8 — The NFR7 class (c) "supported operating envelope" is undefined in the PRD

- **PRD:** NFR7 (line 367) "within the supported operating envelope"; G-APPEND (line 682); §9 safety boundary (line 486).
- **Evidence:** AD-5 defines the envelope: per-actor-host `actorStateStore` components, physical targets bound into the AD-26 digest, one-app-ID database grants, and re-proof on every DAPR minor-version change (`architecture.md:150`). G-APPEND binds none of these. Today the AppHost shares `statestore` (`actorStateStore: true`, `keyPrefix: none`) across `eventstore`, `eventstore-admin`, and `tenants` (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:39-53`). C1's unauthenticated actor callbacks add a second-activation path.
- **Fix:** Promote the envelope's mandatory elements, AD-26 digest binding, and runtime re-proof trigger into G-APPEND's evidence and invalidation columns.

### M9 — FR25 still mandates mutable `@main` shared gates, so a sealed result is not reproducible

- **PRD:** FR25 (line 276) "use shared Hexalith.Builds security gates through `@main`"; Glossary "Assurance Control" (1) (line 201) "on the exact head SHA and workflow-file digest"; OR18 (line 757) covers only the OCI platform set.
- **Evidence:** `.github/workflows/ci.yml:18,184`, `codeql.yml:20`, `commitlint.yml:19`, and `dependency-review.yml:19` call `Hexalith.Builds@main`. A seal binds the caller workflow's digest but not the reusable-workflow content resolved from `@main`. The release path is SHA-pinned instead (`release.yml:127,159` via the pinned builds-execution checkout).
- **Fix:** Classify each shared workflow as authorizing or advisory, require a SHA pin for anything a seal or release consumes, and bind the resolved reusable-workflow SHAs into seal evidence. Amend FR25 accordingly.

### M10 — AD-18 outbound control-plane header ownership has no PRD requirement

- **PRD:** None. No FR, NFR, or §8.2 bullet mentions `dapr-app-id`/`dapr-api-token` or inbound header steering.
- **Evidence:** The handler replaces rather than appends (`src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs`, the `Remove`/`TryAddWithoutValidation` pairs) and is guarded structurally (`tests/Hexalith.EventStore.Client.Tests/Registration/DaprRoutingHeaderOwnershipGuardTests.cs`). Architecture still records that omitting `.AddEventStoreDaprServiceInvocation` "is currently fail-open" (AD-18). That residual has no PRD owner or gate.
- **Fix:** Add a §8.2 bullet and a FR28 clause covering discard, replace, and caller-own-token, plus fail-closed detection of missing registration.

### M11 — OR30 (MediatR licence) is "blocking for the next release" but no gate consumes it

- **PRD:** OR30 (line 744).
- **Evidence:** MediatR is referenced by the released `Server` and `Gateway` packages (`src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj:37`; `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj:49`) at the Builds-catalog pin (`references/Hexalith.Builds/Props/Directory.Packages.props:194`, `14.2.0`). The shipped host suppresses the licence log (`src/Hexalith.EventStore/appsettings.json:6`). Neither G-PUBLICATION-AUTH, G-COMPAT, nor G-READINESS references OR30. The evidence form is undefined, and package consumers such as Tenants inherit the obligation with no disclosure.
- **Fix:** Bind OR30 into G-PUBLICATION-AUTH's `release-available` preconditions with a named record, and require either removing the log suppression or documenting a rationale.

## Low

- **L1 — FR3's endpoint list is incomplete** (line 238). The SDK also maps `/project/v2`, `/project/v2/reconcile`, `/project/rebuild/{,stage/,commit/,abort/,verify/}v1`, and `/project/rebuild/shared/v1` (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:244-449`). **Fix:** make the NFR12 / AD-33 inventory the authoritative endpoint list and call FR3's five a minimum.
- **L2 — G-STATUS-ID's evidence column still names "Approved corrective or reopened Story 2.9"** while its owner is Story 2.15 (line 681). **Fix:** name Story 2.15 in the evidence column.
- **L3 — The §11.3 architecture row is stale** (line 648). It binds SHA-256 `7e3dbc7b…`, but the current spine is `7fd805a8…` (updated 2026-10-07). G-BASELINE already fails, so this has no readiness effect. **Fix:** refresh only with the OR14 sequence.
- **L4 — The event-evolution rollout contract exists only in architecture.** The AD-13 "V2 admission fence stays until the writer and every serving reader and consumer are compatible" rule and the fail-closed evolution-service outcomes have no FR33-C5/C6 wording visible to consumers (line 413-414). The uncommitted legacy-replay routing is not judged. **Fix:** add a one-line mixed-version rollout consequence to FR33-C6.
- **L5 — FR16 does not state that `PubSub` projection-change transport is non-production** (line 260). AD-8 makes `Direct` the only production-eligible transport (`ProjectionChangeNotifierOptions.cs` default `Transport = Direct`; validator line 120-123 refuses `PubSub` without a binding issuer). **Fix:** add that limit to FR16.

## Captured Contradictions (code still contradicts the PRD; PRD gates truthfully FAIL; not counted)

- **Generated tenant boundary (NFR2, FR12-C2, G-TENANT, Story 2.14).** The `System` source returns literal `"system"`, route values are forwarded raw with `Ordinal` comparison, and the claim source returns the raw grant (`RestApiControllerEmitter.cs:407-445`). The gateway's own grammar validator rejects uppercase instead of normalizing it (`SubmitCommandRequestValidator.cs:43-48`), so the generator path is fail-closed for mixed case and is not a cross-tenant hole, because the gateway re-authorizes against grants (AD-3). The narrower-gate problem is H2.
- **Generator `Location` fallback (FR12-C3, G-STATUS-ID, Story 2.15).** `string __hexalithStatusKey = __hexalithResponse.MessageId ?? __hexalithResponse.CorrelationId;` (`RestApiControllerEmitter.cs:281`) is pinned by `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:65`. When both values are null, `CommandStatusLocationBuilder.TryBuild` throws (`src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs:15`), and the generated `catch` handles only `EventStoreGatewayException`, so an accepted command becomes a 500 rather than "omit or safe reject". Story 2.15 should list this negative.
- **Append race (NFR7(c), G-APPEND, Story 4.16).** There is no fence and no enforced envelope. The shared actor store is at `statestore.yaml:39-53`. The precision gap is M8.
- **Compatibility baselines (NFR12, G-COMPAT, Story 3.18).** There is no API or wire baseline tooling. The 14-package inventory matches `tools/release-packages.json`.
- **All-host JWT (NFR3, G-AUTH-HOSTS, Story 5.11).** Unchanged. The profile conflict is H4.
- **Consumer removal, publication authority, OQ8 bytes (G-CONSUMER, G-PUBLICATION-AUTH, G-OQ8).** The manifests, validators, canonical profile, and governing bytes are all absent. Each gate is a truthful FAIL.

## Verified Conforming (no finding)

- **Route provenance (AD-14/AD-15).** The gateway strips `ETag`/`IsNotModified`/`IsStale`/`ProjectionVersion` from non-projection-backed responses (`src/Hexalith.EventStore/Controllers/QueriesController.cs:215-235`) and refuses freshness requirements without projection-backed provenance (`:268`). The client rejects a `304` without projection-backed provenance and a strong ETag (`EventStoreGatewayClient.cs:205-213`).
- **Health probes (NFR1/AD-16).** `/health`, `/alive`, and `/ready` are pinned `AllowAnonymous` (`src/Hexalith.EventStore.ServiceDefaults/Extensions.cs:184-186`). They use the status-only default writer outside Development (`:179-182`) and are enumerated by `DefaultHealthEndpointResponseWriterTests.cs:21-35` and `HostBootstrapTests.cs:639-654`.
- **Outbound headers (AD-18).** The handler uses replace semantics and the caller's own token (`DaprServiceInvocationHandler.cs`), with structural guard tests present.
- **Event CloudEvent identity (FR23).** `["cloudevent.id"] = eventEnvelope.MessageId` (`src/Hexalith.EventStore.Server/Events/EventPublisher.cs:214`).
- **Central package versions (FR21, §8.1).** No local `PackageVersion` or `VersionOverride` exists outside evidence probes. `Directory.Packages.props` imports the Builds catalog.
- **Status endpoint tenant scoping.** Lookups iterate only the caller's own grants and return 404 on a mismatch (`CommandStatusController.cs:114-177`). This is not a cross-tenant read, apart from the scope-source issue in H1.

## Delta vs 2026-09-10

The 2026-09-10 validation report (`validation-report.md`, Platform contract items) raised the findings below. The later `review-platform-contract.md`, the "Sealed Story 5.4 Rebind", accepted the PRD-side fixes with 0 findings. This pass re-checked each item against `HEAD 40c92e08`.

| 2026-09-10 finding (severity) | PRD side now | Code / guard now | Status |
| --- | --- | --- | --- |
| Generated APIs contradict the canonical tenant boundary (Critical) | NFR2 corrected contract, FR12-C2, G-TENANT, OR20, and owner Story 2.14 are present | Unchanged: `RestApiControllerEmitter.cs:407-445` still emits `"system"`, raw route values, and the raw sole claim. The gateway compares with `Ordinal`, normalizes nothing, and admits global admins to `system` (`ClaimsTenantValidator.cs:22-45`) | **PRD resolved; code open (captured).** New residuals: H1 (scope source and AD-27 conflict), H2 (gate narrower than Story 2.14), M1 (Unicode fold) |
| `Location` can use `CorrelationId` as status identity (Critical) | FR12-C3/C4, §8.2, G-STATUS-ID, OR21, and owner Story 2.15 are present | Fallback still at `RestApiControllerEmitter.cs:281`, pinned by `RestApiControllerGenerationTests.cs:65` | **PRD resolved for the generator; code open (captured).** New: C2, because the gateway status resource itself resolves `CorrelationId` and the gate does not cover it |
| Silent concurrent append loss unguarded (Critical) | NFR7 delivered-only rule, G-APPEND with no waiver, OR4, and Story 4.16 on the envelope-first path | No fence or envelope; `statestore` shared by 3 app IDs (`statestore.yaml:39-53`) | **PRD resolved; code open (captured).** New: M8 (envelope undefined in PRD), plus the C1 second-activation vector |
| Backward compatibility excludes most public surfaces (High) | NFR12 is inventory-backed across source, binary, wire, HTTP, and package surfaces; G-COMPAT; OR22; Story 3.18 | No baseline tooling | **PRD resolved; code open.** New: M7 (deprecation window and signalling) |
| Authentication conformance does not cover every host (High) | NFR3 is capability-defined; G-AUTH-HOSTS; OR23; Story 5.11 | Unchanged | **PRD resolved; code open.** New: H4 (NFR3 vs AD-10/AD-36 profiles) |
| Consumer-removal authority weaker than architecture (High) | FR36 five outcomes, consumer-owner role, G-CONSUMER, OR24, Story 3.20 | Manifest and validator absent | **PRD resolved; truthful FAIL** |
| Bound reject result is not a coherent baseline (High) | Frontmatter now separates readiness (2026-10-06, `a6fc951e`, `examined-dirty-unapproved`) from PRD validation (2026-09-10) | §11.3 still binds the stale architecture digest `7e3dbc7b…` (L3) | **Partially resolved**; G-BASELINE FAIL is truthful |
| Ownership traceability is not delivery traceability (Medium) | G-MVP-COVERAGE manifest, G-CLAUSE, §7.1 ledger | Manifest and validator absent | **PRD resolved; truthful FAIL.** New: C1/H5, because the `nfr1_surfaces` denominator has no completeness rule |
| Shared-workflow governance mixes mutable and immutable authority (Medium) | FR25 still mandates `@main`; OR18 covers only the OCI platform set | `@main` in `ci.yml`, `codeql.yml`, `commitlint.yml`, and `dependency-review.yml` | **Open**, now sharpened by M9 (seals cannot bind `@main` content) |
| OQ8 normative bytes unavailable (Critical, cross-lens) | G-OQ8, OR11, Story 4.17 | Bytes still absent | **Truthful FAIL; unchanged** |

**New since 2026-09-10 (not raised before):** C1, C2, H1, H3, H4, H5, H6, H7, M2-M6, M10, M11, L1, L4, L5. The NFR1 2026-10-07 amendment introduced H5. AD-36 (2026-10-07) exposed H4. The other new findings come from code paths the earlier lenses did not inspect: actor callbacks, the gateway status resource, dead-letter publication, generated-command identity, and options ceilings.
