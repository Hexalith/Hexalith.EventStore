---
title: 'Dual-Principal Query Envelope & Safe-Denial Boundary (6.1-P2)'
type: 'feature'
created: '2026-07-18'
status: 'done'
review_loop_iteration: 2
context: []
baseline_commit: '3796e27ed8343e46ce7d50cda01616a2980712f0'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `QueryEnvelope` carries a single flat `UserId` + `IsGlobalAdmin` — no distinction between the original end-user actor, the authenticated calling workload, delegation, or scopes/audience. Consuming platforms (Hexalith.Projects Story 6.1) cannot implement dual-principal authorization or an indistinguishable forbidden-vs-nonexistent denial for list/open queries on this seam. This is the Hexalith.Projects 6.1-P2 external-prerequisite work package.

**Approach:** Additively extend the identity chain (`QueriesController` → `SubmitQuery` → `QueryRouter` → `QueryEnvelope`) with distinct original-actor, authenticated-workload, delegation, and scopes/audience fields, sourced from JWT claims at the gateway boundary. Add a new opt-in safe-denial adapter/policy type that a caller can apply to specific list/open query routes to make Forbidden and NotFound externally indistinguishable, without changing today's default Forbidden/NotFound behavior anywhere else.

## Boundaries & Constraints

**Always:**
- `QueryEnvelope` is `[DataContract]`-serialized via `DataContractSerializer` for the DAPR actor proxy (see its class remarks) — new `[DataMember]`s must be appended after `Paging`, never inserted, and must have safe defaults so older/newer envelope versions round-trip.
- New identity fields are additive optional parameters on `QueryEnvelope`/`SubmitQuery`; existing single-principal callers (Tenants, Parties, all current tests) compile and behave unchanged with no field populated.
- The safe-denial capability is a new opt-in type (e.g. adapter/policy) applied per query route — it must not alter `QueryRouterResult.NotFound`, `QueryAdapterFailureReason.Forbidden`, or `DaprTenantQueryService.ClassifyFailedEnvelope`'s existing mapping (`src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs:141-172`). `SubmitQueryHandlerTests.cs:227` and the admin `DaprTenantQueryServiceTests.cs:290` Forbidden-vs-NotFound assertions must keep passing unmodified.
- Unresolvable/missing identity or denial-policy misconfiguration fails closed (deny), never open (AD-10).
- This lives in the platform (`Contracts`/`Server`), not a domain module (AD-2); domain modules stay domain-centric.

**Ask First:**
- The exact JWT claim names/mapping for original-actor vs. authenticated-workload vs. delegation vs. scopes/audience — no OBO/delegation flow exists in this repo today (only the single `sub` claim at `QueriesController.cs:59` and the DAPR-internal `dapr_caller_app_id` synthesized identity at `DaprInternalAuthenticationHandler.cs:34-39`). Propose a claim mapping (RFC 8693 `act`/`may_act` is a reasonable precedent) and confirm before implementing.
- Whether the unified safe-denial response should be byte-identical to a genuine NotFound, or a new distinct-but-uniform category — confirm the exact wire shape before implementing.

**Never:**
- Do not implement the global-position/watermark capability — deferred separately (`_bmad-output/implementation-artifacts/deferred-work.md`).
- Do not implement 6.1-P3 (production identity/auth contract approval) — separate work package.
- Do not expose reflection, internal API access, or an inferred/synthesized identity to consumers — public contract only.
- Do not change denial behavior for any query route that does not explicitly opt into the new safe-denial adapter.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Same-tenant list query, opted-in route | Valid envelope, resource exists, tenant/scope match | Normal payload | N/A |
| Forbidden (wrong tenant/scope), opted-in route | Valid envelope, resource exists but caller lacks scope | Same denial shape as NotFound | Denial is externally indistinguishable from NotFound |
| Nonexistent resource, opted-in route | Valid envelope, resource does not exist | Same denial shape as Forbidden | Denial is externally indistinguishable from Forbidden |
| Cross-tenant negative control | Envelope's TenantId does not match resource's tenant | Denied via safe-denial shape | No tenant data or existence leaked |
| Non-opted-in route, existing Forbidden case | Same as today | Unchanged: distinct Forbidden (403) | Existing `DaprTenantQueryService`/tests unaffected |
| Legacy caller, no dual-principal fields populated | Envelope with only `UserId`/`IsGlobalAdmin` set | Behaves exactly as today | No new validation failure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs` -- add additive dual-principal identity `[DataMember]`s
- `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` -- carry new identity fields gateway → router
- `src/Hexalith.EventStore/Controllers/QueriesController.cs:58-96` -- extract new claims from `ClaimsPrincipal User` (today only `sub` + `GlobalAdministratorHelper`)
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:96-105` -- construct extended `QueryEnvelope` from `SubmitQuery`
- `src/Hexalith.EventStore.Contracts/Queries/QueryAdapterFailureReason.cs` -- add a new safe-denial reason constant; do not repurpose `Forbidden`
- `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs` -- reference shape the new adapter wraps, unmodified
- `src/Hexalith.EventStore/Authorization/GlobalAdministratorHelper.cs` -- existing claims-extraction precedent to model new helper(s) after
- New adapter/policy type under `src/Hexalith.EventStore.Server/Queries/` (name TBD during implementation) -- opt-in safe-denial boundary
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs` -- second `IQueryRouter`-chain site that builds a `QueryEnvelope` from `SubmitQuery` for capability-declared/domain-handler-routed queries (`AddEventStoreDomainQueryRouting`) -- must thread the same dual-principal fields; this is the actual seam `IDomainQueryHandler` implementations receive, i.e. the seam Hexalith.Projects 6.1 consumes

## Tasks & Acceptance

**Execution:**
- [x] `QueryEnvelope.cs` -- add dual-principal fields as trailing, optional `[DataMember]`s with a new `[JsonConstructor]` overload -- preserves DataContractSerializer + System.Text.Json compatibility
- [x] `SubmitQuery.cs`, `QueriesController.cs`, `QueryRouter.cs` -- thread new identity from gateway claims through to the envelope
- [x] New safe-denial adapter/policy type + registration seam for opting a query route in -- fulfils the AD-19(Projects) boundary contract without touching default behavior
- [x] Unit tests for the I/O matrix above, including the two existing-behavior regression tests staying green
- [x] Cross-tenant negative-control test proving no existence/tenant leakage through timing, payload shape, or error text
- [x] `HandlerAwareQueryRouter.cs` -- thread the same 5 dual-principal fields into its `QueryEnvelope` construction -- otherwise `IDomainQueryHandler` implementations (the consumer this story exists for) never receive them; add a test mirroring `QueryRouterTests.RouteQueryAsync_ForwardsDualPrincipalFieldsToQueryEnvelope` for this router
- [x] `SafeDenialQueryRouter` -- also recognize and unify the second existing not-found shape (`Success:false, NotFound:false, ErrorMessage:"No projection state available for this aggregate"`, see `SubmitQueryHandlerTests.Handle_MissingProjectionState_ThrowsQueryNotFoundException`) into the same canonical shape as Forbidden and hard-not-found, on opted-in routes only -- today only one of two genuine not-found shapes is treated as the unification target, so the other leaks "this is genuinely absent" to an external observer
- [x] `QueryEnvelope`/`SubmitQuery` -- prevent a `with`-expression from bypassing the constructor's `Scopes`/`Audience` `.ToArray()` normalization (e.g. normalize in the property accessor or use an immutable-safe backing type), and override `Equals`/`GetHashCode` (or switch to a value-equatable collection type) so two instances with content-equal `Scopes`/`Audience` compare equal instead of using array reference equality
- [x] `SafeDenialQueryRouter.RouteQueryAsync` -- guard null/whitespace `Domain`/`QueryType` before calling `policy.IsOptedIn(...)` so a non-validating inner router can't cause an unhandled exception instead of a safe pass-through
- [x] `SafeDenialQueryRoutingServiceCollectionExtensions` -- a second call to `AddEventStoreSafeDenialQueryRouting` must merge/append its route list into the existing registry, not silently no-op via `TryAddSingleton`
- [x] `SafeDenialQueryRouter` -- align the Forbidden-sentinel comparison to `StringComparison.Ordinal`, matching `DaprTenantQueryService`'s comparison (`SubmitQueryHandler` itself actually uses `OrdinalIgnoreCase` -- corrected 2026-07-18, see round-2 Spec Change Log entry) and the exact-casing sentinel constants the actor always emits
- [x] `DualPrincipalClaimsHelper` -- bound the number of `Scopes`/`Audience` entries threaded onto every `QueryEnvelope` (all queries carry these now, not just opted-in ones) to a sane maximum, since they're attacker-influenceable-claim-controlled and serialized per request
- [x] `SafeDenialQueryRouteRegistry` (or its registration extension) -- log the registered opted-in routes at startup/registration time so a typo'd domain/queryType silently getting no protection is operator-visible instead of silent
- [x] `QueryEnvelope` DataContractSerializer round-trip tests -- add a case for an explicitly empty (non-null, zero-length) `Scopes`/`Audience` array, not just non-empty and null
- [x] `SafeDenialQueryRoutingServiceCollectionExtensionsTests` -- add a test that registers `IQueryRouter` via an implementation type (e.g. `services.TryAddScoped<IQueryRouter, StubQueryRouter>()`, matching how `AddEventStoreServer()` really registers it), not only via factory, to cover the `ActivatorUtilities.CreateInstance` resolution branch
- [x] `SafeDenialQueryRouter` -- add a one-line doc/comment on the `SafeDenialForbiddenUnified` log line clarifying it is an accepted internal-only (log/SIEM) signal that does not violate the "externally indistinguishable" guarantee, since server-side logs are not exposed to the caller
- [x] `SafeDenialQueryRoutingServiceCollectionExtensions` -- when a prior `ISafeDenialQueryRoutePolicy` registration exists but is not a `SafeDenialQueryRouteRegistry` (i.e. a caller registered a custom policy directly), throw a clear exception instead of silently discarding it -- the merge logic only handles "called twice via this extension," not "a custom policy was already registered"
- [x] `DualPrincipalClaimsHelper` -- bound the *length* of each individual `Scopes`/`Audience` claim value (not just the entry count) before/while splitting, so one oversized single claim value can't bypass the 64-entry cap
- [x] `SafeDenialQueryRouter` -- canonicalize the not-found short-circuit path too: return the same clean `QueryRouterResult(Success:false, Payload:null, NotFound:true)` shape used for Forbidden/MissingProjectionState instead of returning the inner router's result as-is, since nothing guarantees an arbitrary wrapped `IQueryRouter` already omits `ErrorMessage`/`Metadata` on a NotFound result
- [x] `SafeDenialQueryRouteRegistry.Routes` -- return a genuinely read-only view (e.g. wrap in `ReadOnlyCollection`/copy), not the live internal `HashSet` merely typed as `IReadOnlyCollection`, to match its own "immutable snapshot" doc comment
- [x] `SafeDenialQueryRoutingServiceCollectionExtensions` -- fix repeated-call decorator nesting: each call should end up with one `SafeDenialQueryRouter` wrapping the true original inner router, not N nested `SafeDenialQueryRouter` layers around each other (currently behaviorally correct but architecturally wasteful, confirmed by round-2 review)
- [x] `SafeDenialQueryRoutingServiceCollectionExtensionsTests` -- add a test resolving `IEnumerable<IHostedService>` from a fully built `ServiceProvider` after calling `AddEventStoreSafeDenialQueryRouting`, asserting `SafeDenialQueryRouteStartupLogger` is actually registered -- the existing test only unit-tests the logger class directly, never the DI wiring line itself
- [x] `SafeDenialQueryRouterTests` -- add a test proving `StringComparison.Ordinal` (not `OrdinalIgnoreCase`) is load-bearing for the Forbidden/MissingProjectionState sentinel match (e.g. a differently-cased `ErrorMessage` must NOT be unified) -- today no test would catch a regression to `OrdinalIgnoreCase`
- [x] `SafeDenialQueryRouter` -- log the blank-`Domain`/`QueryType` safe pass-through path (mirrors the operator-visibility goal already applied to route registration), since a malformed `SubmitQuery` reaching this deep silently today gives no operator signal
- [x] `SafeDenialQueryRouter` -- update the stale class-level XML doc/remarks (still say "only ever narrows a Forbidden outcome") to reflect that `MissingProjectionState` is also narrowed as of round 1
- [x] `SafeDenialQueryRouter`/`Log.SafeDenialForbiddenUnified` -- remove the unreachable default parameter value on the `[LoggerMessage]`-generated method (the single call site always passes `reason` explicitly)
- [x] `QueryAdapterFailureReasonTests` (or equivalent) -- add a distinctness test for `SafeDenialMissingProjectionState` vs. `MissingProjectionState`, mirroring the existing `SafeDenialForbidden` vs. `Forbidden` distinctness test

**Acceptance Criteria:**
- Given an envelope with no new fields populated, when routed through the existing pipeline, then behavior is byte-identical to today.
- Given a route opted into the safe-denial adapter, when the resource is forbidden vs. nonexistent (either not-found shape), then the response is externally indistinguishable in shape and status. Timing-side-channel closure (constant-time response) is explicitly out of scope for this story -- see Spec Change Log -- this AC does not require it.
- Given a route not opted into the adapter, when Forbidden or NotFound occurs, then `QueryRouterResult`/`DaprTenantQueryService` behavior and their existing tests are unchanged.
- Given a cross-tenant query attempt, when evaluated, then no tenant existence or content is observable in the denial response shape.
- Given a query routed through `HandlerAwareQueryRouter` (capability-declared/domain-handler routing), when dual-principal fields are populated on the incoming `SubmitQuery`, then they are forwarded onto the `QueryEnvelope` an `IDomainQueryHandler` receives, identically to the projection-actor `QueryRouter` path.

## Spec Change Log

- **2026-07-18, review_loop_iteration 1.** Triggering findings from the parallel review (blind-hunter, edge-case-hunter, verification-gap):
  - `bad_spec`: the "timing-observable behavior" Acceptance Criterion demanded a constant-time guarantee this story never scoped an implementation approach for; full timing-channel closure is a platform-level concern (DAPR actor activation latency, network jitter) beyond what a query-router decorator can control. **Amended:** the AC now requires shape/status indistinguishability only; timing-channel closure is explicitly out of scope and deferred (`_bmad-output/implementation-artifacts/deferred-work.md`).
  - `bad_spec`: the Code Map named only `QueryRouter.cs`, missing that `HandlerAwareQueryRouter.cs` is a second, parallel `IQueryRouter`-chain site that builds `QueryEnvelope` for capability-declared/domain-handler routing -- the exact seam `IDomainQueryHandler` (this story's real consumer) receives. Verification-gap review proved the dual-principal fields never reach that seam as implemented. **Amended:** Code Map and Tasks now include it.
  - `patch` (bundled into this same re-derivation rather than a separate post-loopback pass, since the same implementer is already re-engaging): the second existing not-found shape not unified by the safe-denial adapter; `with`-expression bypass of array normalization; array reference-equality in `Equals`/`GetHashCode`; missing null/whitespace guard before `policy.IsOptedIn`; silent route-registration drop on double-registration; inconsistent `OrdinalIgnoreCase` comparison; unbounded `Scopes`/`Audience` list size; no startup visibility for misconfigured routes; missing empty-array DataContractSerializer test; missing DI-composition test for the `ActivatorUtilities.CreateInstance` branch; undocumented log-line trade-off. Full task-level detail is in `## Tasks & Acceptance` above.
  - `reject` (spec-compliant as implemented, or pre-existing/accepted trade-off, no action taken): `aud`-fallback claim-order dependence and the `IsDelegated` heuristic's false-positive risk both match the Ask-First resolution exactly as approved 2026-07-18; `FirstClaimValue` reading only the first matching claim matches standard singular-claim OIDC convention; claims-trusted-at-face-value is the same pre-existing trust boundary `UserId` already had; discarding `Metadata`/`ProjectionType` on denial is the safe conservative default.
  - **KEEP** (preserve exactly as implemented, do not redesign): the additive `QueryEnvelope`/`SubmitQuery` field-threading pattern (trailing optional `[DataMember]`s, new delegating constructor, safe null/false defaults); the opt-in `ISafeDenialQueryRoutePolicy`/`SafeDenialQueryRouteRegistry`/`SafeDenialQueryRouter` decorator shape; the resolved claim mapping in `DualPrincipalClaimsHelper` (`sub`/`azp`/`client_id`/`act`/`scope`/`scp`/`aud`); the decision that the unified denial response reuses the exact existing NotFound shape rather than a new category; all currently-passing tests and the full existing test suite's green state (`SubmitQueryHandlerTests.cs`, `DaprTenantQueryServiceTests.cs` untouched).

- **2026-07-18, review_loop_iteration 2.** Round-1 fixes were independently re-reviewed by all three layers; all 11 items confirmed genuinely implemented and tested (would fail if reverted), except the `Ordinal`-comparison and startup-logger-DI-wiring fixes, which had no regression-catching test (folded into this round's patch list). All findings this round are `patch` -- none required amending frozen intent or non-frozen AC/Code Map again:
  - `patch`: a custom `ISafeDenialQueryRoutePolicy` registered before `AddEventStoreSafeDenialQueryRouting` is silently discarded by the merge logic (found independently by two review layers -- same bug class round 1 was supposed to close, just an uncovered case); per-entry claim-value length unbounded (only entry count was bounded); `SafeDenialQueryRouter`'s not-found short-circuit doesn't canonicalize an arbitrary inner router's result, only Forbidden/MissingProjectionState are canonicalized; `SafeDenialQueryRouteRegistry.Routes` exposes a live mutable `HashSet` despite its "immutable snapshot" doc; repeated registration nests decorators instead of flattening (currently harmless, confirmed by passing tests, but architecturally wasteful); missing regression tests for the `Ordinal` comparison and the `AddHostedService` DI wiring; missing operator-visible logging on the blank-Domain/QueryType pass-through; stale XML doc on `SafeDenialQueryRouter`; unreachable default parameter on the log method; missing `SafeDenialMissingProjectionState` distinctness test. Full task-level detail is in `## Tasks & Acceptance` above.
  - **Correction, not a code change:** this spec's own round-1 Tasks entry claimed `SubmitQueryHandler` uses `Ordinal` comparison for the Forbidden sentinel; round-2 review found it actually uses `OrdinalIgnoreCase`. The `Ordinal` code choice itself is still correct (matches `DaprTenantQueryService` and the actor-emitted exact-casing constants) -- only this spec's stated rationale was wrong, corrected in the Tasks list above.
  - `defer` (real, but needs infrastructure beyond this story's proportionate scope -- logged to `deferred-work.md`): an operator-visible warning when a registered safe-denial route's `Domain`/`QueryType` casing doesn't match real wire values would need a canonical query-type registry that doesn't exist anywhere in this codebase today; an end-to-end test tying `DualPrincipalClaimsHelper`'s claim-type assumptions (`azp`/`act`/`scope`/`aud`/`client_id`, and the `MapInboundClaims=false` config they depend on) to the real `JwtBearerHandler`/Keycloak pipeline belongs in the existing Keycloak E2E integration-test tier, not a unit-test-level patch.
  - `reject`: `DualPrincipalClaimsHelper.Extract` performing multiple claims-collection scans per query is a performance observation with no correctness impact, not a bug.

## Design Notes

RFC 8693 (OAuth 2.0 Token Exchange) `act` (actor) and `may_act` claims are the standard precedent for keeping a delegating principal distinct from the original subject — worth evaluating as the claim shape for original-actor vs. authenticated-workload before inventing a bespoke one.

**Ask-First gates resolved 2026-07-18 (Jerome):**
- Claim mapping: `OriginalActorId` = `sub` (same source as today's `UserId`); `AuthenticatedWorkloadId` = `azp` claim, falling back to `client_id`/first `aud` entry when `azp` is absent; `Delegation` = true when an `act` claim (RFC 8693) is present or `azp` differs from the token's issuing client; `Scopes` = `scope`/`scp` claim split on whitespace; `Audience` = `aud` claim. No existing OBO/service-account flow exists in this repo today (Keycloak realm's two clients both have `serviceAccountsEnabled: false`), so workload/actor naturally converge to the same value until a real service-account/OBO flow is added later.
- Denial wire shape: the unified forbidden/nonexistent response on opted-in routes reuses the exact existing NotFound status/body/error shape (byte-identical), rather than introducing a new "denied" category — strongest indistinguishability guarantee since it's a real, already-exercised code path.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: builds clean, `TreatWarningsAsErrors=true`
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests` -- expected: envelope serialization round-trip tests pass
- `dotnet test tests/Hexalith.EventStore.Server.Tests` -- expected: existing Forbidden/NotFound tests plus new safe-denial tests pass
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests` -- expected: `DaprTenantQueryServiceTests` unchanged and passing

## Suggested Review Order

**Dual-principal identity contract**

- The new record carrying the five identity facets this whole story exists to add.
  [`DualPrincipalIdentity.cs:13`](../../src/Hexalith.EventStore/Authorization/DualPrincipalIdentity.cs#L13)

- Additive `[DataMember]`s appended after `Paging`, with custom `Equals`/`GetHashCode` for content-equal array comparison.
  [`QueryEnvelope.cs:229`](../../src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs#L229)

**Claim extraction at the gateway boundary**

- Maps `sub`/`azp`/`client_id`/`act`/`scope`/`scp`/`aud` to the dual-principal identity; bounded both by entry count and per-entry length.
  [`DualPrincipalClaimsHelper.cs:63`](../../src/Hexalith.EventStore/Authorization/DualPrincipalClaimsHelper.cs#L63)

- Only place `ClaimsPrincipal` claims enter the pipeline; extends the pre-existing `sub`-only extraction.
  [`QueriesController.cs:71`](../../src/Hexalith.EventStore/Controllers/QueriesController.cs#L71)

**Identity threading through both router chains**

- Projection-actor routing path forwards the five fields into the constructed envelope.
  [`QueryRouter.cs:107`](../../src/Hexalith.EventStore.Server/Queries/QueryRouter.cs#L107)

- Capability-declared/domain-handler routing path — the actual seam `IDomainQueryHandler` (Projects' real consumer) receives; missed in round 1, fixed in round 2 of review.
  [`HandlerAwareQueryRouter.cs:57`](../../src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs#L57)

**Safe-denial boundary**

- Core decorator: unifies Forbidden and both not-found shapes into one canonical response, opt-in only, fail-safe on malformed input.
  [`SafeDenialQueryRouter.cs:28`](../../src/Hexalith.EventStore.Server/Queries/SafeDenialQueryRouter.cs#L28)

- Opt-in policy contract a caller can also implement directly instead of using the default registry.
  [`ISafeDenialQueryRoutePolicy.cs:7`](../../src/Hexalith.EventStore.Server/Queries/ISafeDenialQueryRoutePolicy.cs#L7)

- Default `HashSet`-backed policy; `Routes` now returns a genuinely read-only view.
  [`SafeDenialQueryRouteRegistry.cs:11`](../../src/Hexalith.EventStore.Server/Queries/SafeDenialQueryRouteRegistry.cs#L11)

- Registration seam: merges routes across repeated calls, flattens decorator nesting via a marker, rejects unmergeable custom policies loudly.
  [`SafeDenialQueryRoutingServiceCollectionExtensions.cs:23`](../../src/Hexalith.EventStore.Server/Queries/SafeDenialQueryRoutingServiceCollectionExtensions.cs#L23)

- New internal-only diagnostic reason constants, distinct from the public `Forbidden`/`MissingProjectionState` sentinels they narrow.
  [`QueryAdapterFailureReason.cs:71`](../../src/Hexalith.EventStore.Contracts/Queries/QueryAdapterFailureReason.cs#L71)

**Regression boundary (must stay unchanged)**

- Existing Forbidden-vs-NotFound distinction for non-opted-in routes; proves this story didn't touch default behavior.
  [`SubmitQueryHandlerTests.cs:223`](../../tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs#L223)

**Supporting tests**

- Core safe-denial behavior matrix: unification, non-opt-in pass-through, cross-tenant negative control.
  [`SafeDenialQueryRouterTests.cs`](../../tests/Hexalith.EventStore.Server.Tests/Queries/SafeDenialQueryRouterTests.cs)

- Claim-mapping and bounding behavior.
  [`DualPrincipalClaimsHelperTests.cs`](../../tests/Hexalith.EventStore.Server.Tests/Authorization/DualPrincipalClaimsHelperTests.cs)

- Registration merge/flatten/custom-policy-rejection behavior.
  [`SafeDenialQueryRoutingServiceCollectionExtensionsTests.cs`](../../tests/Hexalith.EventStore.Server.Tests/Queries/SafeDenialQueryRoutingServiceCollectionExtensionsTests.cs)
