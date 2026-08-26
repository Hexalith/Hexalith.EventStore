# Deferred Work

## Deferred from: Story 1.20 pre-gate paired-contract audit (2026-07-25)

- source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`
  summary: The final owner-record limitation-ID comparison is asymmetric across the WORM boundary. Block 15's `validate_final_owner_record` dedupes the record's IDs with `LC_ALL=C sort -u` before diffing them against the expected set, while block 16's `validate_committed_owner_record` uses a plain `LC_ALL=C sort` and separately asserts uniqueness in jq (`length == (map(.id) | unique | length)`). A final approval record carrying a duplicate limitation ID therefore passes approval validation and only fails during A/B/C verification.
  evidence: Block 15 `diff -u "$EXPECTED_LIMITATION_IDS" <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort -u)` versus block 16 `diff -u "$A_EXPECTED_LIMITATION_IDS" <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort)`. Block 15 also derives its expected set from the generated approval subject while block 16 compares against a hard-coded literal list; the two were verified equal on 2026-07-25 at 9 capability IDs and 32 limitation IDs with no duplicates, so the asymmetry cannot fire against the current packet literal.
  severity: low
  status: accepted (deliberate acceptance 2026-07-25) — not fixed during the Story 1.20 closure run because the defect is unreachable with the committed approval-subject literal, and every packet edit forces a new candidate SHA and a complete ~50-minute Phase-1 re-gate. Fix by making block 15 use the same plain `sort` plus an explicit jq uniqueness assertion, so a duplicate ID is rejected before the irreversible WORM upload rather than after it. Fold into the next packet change rather than spending a dedicated cycle.

## Deferred from: release-skip race diagnosis (2026-07-21, run 29799288142)

- source_spec: none
  owner_repo: `Hexalith.Builds` — reusable `.github/workflows/domain-release.yml` (currently pinned in this repo as `builds-execution-sha: cf04c419378dfe1bd3c41a9244b5e3283092056e`). NOT owned by `Hexalith.EventStore`; the EventStore `release.yml` only calls the reusable workflow, so this fix cannot land here.
  summary: The reusable release workflow silently produces a **green run that publishes nothing** when `main` advances between release dispatch and the `Semantic Release` step. `actions/checkout` pins the dispatched `github.sha`; semantic-release then does its own `git fetch`, sees the live `origin/main` is ahead, prints `ℹ The local branch main is behind the remote one, therefore a new version won't be published.`, and exits 0. The operator sees a successful Release run and reasonably assumes a release was cut. Harden `domain-release.yml` to **fail loudly** (or emit an unmissable error annotation + non-success outcome) when the checked-out release SHA is no longer the live `main` tip at semantic-release time, instead of a silent no-op green.
  evidence: Run https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29799288142 — dispatched 03:43:45Z on `41f5ed0f` (then the live tip; `verify-source` passed). At 03:52:04Z an automated submodule-bump commit `4245f0f8` ("fix: update submodule references…") landed on `main` (the concurrent bmad-loop auto-push hazard — see project memory `concurrent-bmad-loop-git`). At 04:04:06Z the release job checked out the pinned `41f5ed0f`; at 04:06:33Z semantic-release aborted with the "branch is behind remote" message. Job conclusion: success. No `v3.79.0` tag/release/packages were produced despite releasable `feat:`/`fix:` commits since `v3.78.0`. EventStore's own `verify-source` gate only re-checks the tip at run *start*, leaving a ~20-min window; closing the race durably requires a re-assert-tip-then-fail step inside the reusable workflow (Builds), or preventing pushes to `main` during a release. Immediate remediation for this incident was an operator re-dispatch once `main` was quiescent (run 29800856877).

## Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md`
  summary: Add a guardrail test asserting the `postgres:18.4` tag in `.github/workflows/integration.yml`'s "Pull PostgreSQL container image" step matches `Oq8PostgresqlFixture.PostgresImage`, so the two literals cannot silently drift.
  evidence: Blind-hunter review of the CI fix -- the workflow comment asks a human to keep the tag in sync but nothing enforces it; the repo already has this pattern for release authority (`ContainerPublishingGovernanceTests.cs`) but not for this image tag.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md`
  summary: Add a `docker` ecosystem entry to `.github/dependabot.yml` so `postgres:18.4` bumps get automated PRs like the existing `nuget`/`npm`/`github-actions` ecosystems.
  evidence: Blind-hunter review of the CI fix -- Dependabot currently cannot see or bump the Postgres image tag in either the workflow or the fixture, so the sync in the item above would otherwise be 100% manual forever.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md`
  summary: Pin the live-sidecar PostgreSQL image by digest (`postgres@sha256:...`) instead of the mutable `18.4` tag, with a documented rotation process.
  evidence: Blind-hunter review of the CI fix -- a mutable tag gives no guarantee the bits pulled today match the bits validated previously; digest pinning needs coordinated changes to both the workflow and `Oq8PostgresqlFixture.cs`, out of scope for the minimal unblock-CI fix.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md`
  summary: Cache the pulled `postgres:18.4` image (or layer) across `integration.yml` runs instead of re-pulling on every push/PR to `main`.
  evidence: Blind-hunter review of the CI fix -- the image rarely changes but is currently re-pulled in full on every job run with no `actions/cache` or registry mirror.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md`
  summary: Evaluate replacing `Oq8PostgresqlFixture`'s manual `docker run`/`docker image inspect` orchestration with GitHub Actions' native `services:` container support (or an equivalent declarative approach), which would pull, health-check, and manage the Postgres container without a hand-rolled prerequisite check.
  evidence: Blind-hunter review of the CI fix -- this fix patches the one workflow that currently exercises the fixture; the next new workflow, self-hosted runner, or Tier-3 job that reuses `Oq8PostgresqlFixture` will hit the identical "no such image" failure unless the fixture's own contract is revisited.

## Deferred from: immutable manual release hardening (2026-07-20)

- source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md`
  summary: Generalize the reusable publication preflight's hard-coded EventStore package count of exactly 14 so other callers can supply their own immutable expected inventory size without weakening EventStore's manifest contract.
  evidence: The shared validator currently enforces `len(package_ids) == 14`; that is correct for EventStore but makes the otherwise reusable release workflow product-specific.
- source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md`
  summary: Give each container mapping its own frozen repository identity and phase evidence when multiple container mappings share one release invocation.
  evidence: The current EventStore caller has one approved mapping, while the shared publisher reuses one preflight evidence directory and frozen identity; a second mapping would collide with the first mapping's repository identity.
- source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md`
  summary: Close the non-atomic gap between the final Zot tag-absence proof and the subsequent registry write.
  evidence: The final read-only `HEAD` check fails closed on collisions and ambiguous responses, but another writer can still create the tag after absence is observed and before .NET SDK publication begins; Zot absence and write are not atomic.

## Existing deferred work

- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md`
  summary: Make a safe-denial route registration's `Domain`/`QueryType` casing mismatch against real wire values operator-visible (today only the registered-route list itself is logged, not whether any entry actually matches a query type that ever occurs).
  evidence: Round-2 blind-hunter review of 6.1-P2 -- ordinal case-sensitive route matching means a typo'd-casing registration silently gets no safe-denial protection, same failure mode the round-1 startup-logging fix targeted, but closing it needs a canonical registry of valid domain/queryType pairs to cross-check against, which doesn't exist anywhere in this codebase today -- out of proportion for a patch-level fix.
- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md`
  summary: Add an end-to-end test tying `DualPrincipalClaimsHelper`'s claim-type assumptions (`azp`/`act`/`scope`/`aud`/`client_id`, dependent on `MapInboundClaims=false`) to the real `JwtBearerHandler`/Keycloak token-issuance pipeline, not just hand-constructed `ClaimsPrincipal` unit tests.
  evidence: Round-2 blind-hunter review of 6.1-P2 -- this story is the first time these claim types become authorization-relevant (previously only `sub` mattered); nothing today would catch a regression if `MapInboundClaims` were flipped or a future middleware renamed these claim types. Belongs in the existing Keycloak E2E integration-test tier (`tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ESecurityTests.cs`), a heavier lift than this patch round's unit-level fixes.
- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md`
  summary: Close the timing side-channel for the safe-denial adapter (Forbidden vs. genuine not-found currently have different latency profiles -- actor-activation-then-403 vs. actor-lookup-failure -- with no constant-time/padding normalization).
  evidence: Blind-hunter review of 6.1-P2 found no timing normalization despite the story's original AC naming "timing-observable behavior" indistinguishability; full closure requires platform-level work (DAPR actor activation, network jitter) beyond what a query-router decorator controls, so the AC was narrowed to shape/status indistinguishability only and this was split out as separate future hardening.
- source_spec: none
  summary: Expose an authoritative persisted global-position/watermark to projections and `QueryCursorScope`, consumed by Hexalith.Projects Story 6.1-P2's watermark-replay/restart requirement.
  evidence: Split from the 6.1-P2 dual-principal query envelope + safe-denial boundary work at Jerome's direction 2026-07-18 — the watermark is largely independent of the identity/envelope and safe-denial work (which are coupled to each other) and can ship separately; a `GlobalPosition` already exists per-event at persistence time (`EventEnvelopeAssertions.cs`, `EventEnvelopeBuilder.cs`) but nothing today exposes it as an authoritative watermark.
- 2026-07-05: Epic D retrospective follow-through requires a dedicated REST generator hardening story or backlog item. Scope it from the D5/D7 deferred items below rather than scattering generator diagnostics into unrelated security, correctness, or UI stories. Minimum scope: unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.
- 2026-07-05: Query freshness/projection metadata needed a platform-owned gateway contract before UI or generated REST stories could treat stale/current state or projection version as production-backed evidence. **RESOLVED 2026-07-11 by Story 2.8 / AD-15** for EventStore route provenance, route-aware ETags, and fail-safe consumers. Genuine persisted-age evidence remains the separate D6 handoff; the Tenants producer cleanup remains Story 4.7.
- 2026-07-05: Generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details before accepting a live-smoke blocker. → tracked as Story 3.8 (Epic 3, companion to 3.1); re-homed from TEST-1.1 on 2026-07-07. **RESOLVED 2026-07-07 by Story 3.8** — `scripts/generated-api-smoke-preflight.sh`; AC10 live-topology gate met (generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, support-safe failure details).
- 2026-07-01: Packaging governance tests hard-code external dependency patch versions. Consider a lower-maintenance guard that still proves central version pins and emitted package metadata stay aligned, so routine published package bumps do not require brittle test-only edits.
- source_spec: `_bmad-output/implementation-artifacts/spec-1-2-domain-query-handler-routing.md`
  summary: Handler-backed query routes need explicit provenance so the gateway can decide whether projection ETags are valid for the response.
  evidence: `HandlerAwareQueryRouter` already used the same `QueryRouterResult` shape as projection routes before this story, and `QueriesController` falls back to request/domain projection ETag lookup when no projection type is supplied; changing that safely needs a separate route-provenance contract rather than metadata passthrough alone.
  status: reconciled 2026-07-11 — see the reconciliation section below.

## Route-provenance contract reconciliation (updated 2026-07-11)

- The EventStore platform portion of AD-15 is owned and implemented by **Story 2.8**: additive provenance contract, authoritative router stamping, route-first conditional evaluation, projection-only freshness/ETag evidence, and fail-safe client/generated REST behavior.
- The 2026-07-05 gateway-contract prerequisite is superseded for metadata propagation by **AD-14** + Stories 1.2/1.3/2.2, and for EventStore route provenance enforcement by **AD-15** + Story 2.8.
- **Story 4.7 is now Tenants-only follow-up.** It retains the producer cleanup that stops aliasing `ProjectionVersion := ETag` in `references/Hexalith.Tenants/.../TenantQueryResult.cs`; no EventStore platform enforcement remains assigned to Story 4.7.
- The **D6 read-model-freshness handoff** remains a separate deferred platform item (persisted projection-age metadata). Until a route sources genuine freshness it is `HandlerComputed`/`Unknown` under AD-15 and consumers render `unknown`.

## Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)

- Malformed projection payload throws inside `CounterStatusResult.ParseCountFromPayload` (`Convert.FromBase64String` / `JsonDocument.Parse` / `GetInt32`) instead of failing safe. Pre-existing behavior carried over from the deleted `CounterQueryService`; becomes invisible once the refresh-error patch lands.
- Concurrent/re-entrant refresh has no in-flight guard, and the in-flight `GetAsync` is never cancelled on component disposal (leading to a possible post-dispose `StateHasChanged`). `GetAsync` already exposes an unused `CancellationToken`. Demo-UI hardening across the four Counter components; `SilentReloadPattern` partially mitigates via debounce.
- REST generator silently drops a `record struct` contract carrying `[RestRoute]` (the `TypeKind != Class` check returns null) with no HESREST diagnostic — inconsistent with every other unsupported-shape path, which reports a diagnostic. Add a diagnostic or explicitly support the shape.
- Referenced-message discovery (`RestApiMessageParser.ParseReferenced`) is driven off `CompilationProvider` and emits a reference-equality `ImmutableArray`, so it re-runs the referenced-assembly walk on every compilation and weakens IDE incrementality. Consistent with the generator's pre-existing CompilationProvider usage; perf-only. Consider an equatable model/comparer if editor responsiveness regresses.
- Blazor components treat "no projection yet" only as HTTP 404; a gateway `Success==false` semantic failure surfaces as `EventStoreGatewayException.StatusCode == 200` and falls through to the generic catch. Matches the old code's 404-only behavior, so no regression, but the empty-state contract could be made explicit.
- AC8 scope hygiene: the generator command-route mapping (`TryFindUnmappedCommandRouteParameter`) was changed and a command diagnostic test added inside a query-only story (defensible as generator enablement), and the broader D5 branch/working-tree carries CI/CD, `tools/release-*`, `.releaserc.json`, and submodule-pointer changes that belong to D7/D8. Split those out of the D5 change set.
- `CounterHistoryGrid` inserts a history row on every refresh, including HTTP 304 (no change), producing duplicate rows. Deferred (user decision B3): intent is ambiguous — value-change log (skip 304s) vs. polling/ETag-activity log (current behavior is fine). Decide the grid's purpose before changing it.

## Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)

- Command contracts with duplicate JSON property names are not diagnosed; the new duplicate JSON-name check only runs for queries, so generated command serialization/model-binding can still fail later. Deferred as command/generator hardening outside the D5 query proof.
- Referenced contracts that rely on convention routing rather than `[RestRoute]` are not discovered by `ParseReferenced`, even though source contracts without `[RestRoute]` still get default routes. Deferred as generator hardening outside the D5 query proof.
- Query JSON names are deduplicated with `StringComparer.Ordinal`; names differing only by case can still bind ambiguously through query string/model-binding conventions. Deferred as generator hardening outside the D5 query proof.

## Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)

- Route-template validator (`RestApiRouteTemplateParser.GetTemplateError`) false-rejects legitimate inline route constraints containing braces, e.g. `{id:regex(^\d{3}$)}`: `close` binds to the constraint's inner `}`, so the parameter text contains `{` and is rejected as "unescaped brace". Generator hardening; no D5 route uses constraints.
- `RestApiControllerEmitter.RouteParameterMatchesProperty` compares the C# Name with `OrdinalIgnoreCase` but the JsonName with `Ordinal`, while route binding is case-insensitive. A route token matching a property's JsonName only case-insensitively is not excluded from the emitted query payload → phantom / double-bound parameter. Generator hardening; not exercised by D5.
- Query-binding expression (`RestApiControllerEmitter.GetQueryBindingExpression`) silently falls back to aggregate `"index"` / empty entity when `AggregateSource`/`EntitySource` is neither `Constant` nor `Route` (malformed `[RestQueryBinding]` or a future enum member); the validator only guards the `"Route"`-missing case, so no HESREST diagnostic is emitted. Same silent-drop class the diagnostics work aims to close. Generator hardening; `[RestQueryBinding]` not used by D5.
- `[RestQueryBinding]` with `Constant` entity source and no supplied value produces a silent empty-string entity id (`binding.EntityValue ?? string.Empty` → `Literal("")`). Generator hardening; not used by D5.
- `CounterStatusResult.FromQueryResult` returns a fabricated `count 0` when the gateway reports `IsNotModified` but `cachedResult` is null. The four sample components avoid this (they pass a null ETag on first load), but the general-purpose `EventStoreProjectionQueryClient.GetAsync` accepts an arbitrary `If-None-Match` and has no guard. Demo-UI hardening.
- Empty (as opposed to absent) `DAPR_HTTP_PORT` yields the base address `http://localhost:` and `new Uri(...)` throws `UriFormatException` at startup with an opaque message, in both `Sample.Api` and `Sample.BlazorUI`. The `?? "3500"` fallback only guards null. Minor robustness.
- `InboundBearerForwardingHandler` forwards a multi-valued inbound `Authorization` header comma-joined (via the `StringValues`→`string` implicit conversion), producing a malformed bearer that is rejected opaquely by the gateway rather than up front. Adversarial/rare.

## Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)

- HIGH — **RESOLVED/SUPERSEDED.** AD-14 added `QueryRouterResult.Metadata` and the carrier path; Story 2.8 / AD-15 now stamps route provenance, preserves genuine producer freshness/version evidence only for `ProjectionBacked`, and gates generated headers accordingly. D6 remains responsible for additional persisted-age production sources, while Story 4.7 retains only the Tenants `ProjectionVersion := ETag` producer cleanup.
- MEDIUM — Generator: `{tenantId}` route parameter is unvalidated under `RestTenantSource.System`. `IsTenantParameter` (`src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1143`) is name-based and not gated on tenant source, so the tenant-named route segment is excluded from route/body mismatch checks and, under System source, is decorative — a URL/body tenant mismatch silently executes against the body tenant (still bearer-authorized, so no escalation). Recommend validating tenant-named route params against the body when tenant source ≠ Route.
- MEDIUM — External REST error-semantics coverage gap. The 2054-line `TenantsQueryControllerIntegrationTests` was replaced by a 296-line generated-controller test covering 401/request-shape/freshness/ETag-304 but not 403/RBAC, gateway-failure → problem-details, or invalid-cursor at the generated surface. Add once the transport-fault and 400-vs-500 patches land.
- LOW — Generator silently falls back to aggregate `"index"` for invalid `[RestQueryBinding]` sources (None / out-of-range enum / empty Constant) with no HESREST diagnostic, and `RestApiQueryBindingDescriptor.GetHashCode` can NRE on a null constant value. Re-logged from the D5 review; now exercised by D7 `[RestQueryBinding]` usage so worth prioritizing.
- LOW — **RESOLVED 2026-07-31 by Story 2.12.** `Hexalith.Tenants.csproj` now gives Gateway and DomainService complementary source/package edges under the shared dependency-mode contract. The current graph cannot mix source Gateway with package DomainService. After the Tenants solution restore hit `MSB3202` on forbidden/uninitialized nested submodule projects, package-mode validation covered all 17 tracked Tenants projects individually with zero warnings or errors.
- LOW (supply-chain) — `PackageGovernanceTests` now require the local workflows to reference shared `Hexalith.Builds` reusable workflows by mutable `@main` (previously enforced full-SHA pinning). Deliberate org CI decision ("main for stability"), not D7 work. Whoever controls `Hexalith.Builds@main` controls this repo's release step (holds `NUGET_API_KEY`); pinning is delegated to the shared repo and unenforced here.

## Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)

- MEDIUM — `ListByCursorAsync` (`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:482-488`) lets a `StatusCode = 200` `EventStoreGatewayException` (null/JSON-`null`/shape-mismatch payload from the generic `SubmitQueryAsync<T>`) escape into the Blazor circuit — it catches only `IsUnauthorized` and `IsUnavailableOrInvalid` (`>= 400`), unlike the sibling methods' unfiltered catch-all. Undermines AC7 fail-closed. Pre-existing pattern carried through the migration (filters unchanged by D7); trivially patchable by mirroring the sibling catch-all. Low likelihood — needs a malformed 200 (projection/contract bug).
- LOW — Tenant-list path has no invalid-cursor recovery (`TenantQueryGateway.cs:485-487, 936`): `IsUnavailableOrInvalid` treats every `>= 400` alike, so a 400 invalid/expired list cursor surfaces as "gateway unavailable" instead of resetting to page 1 (as `GetTenantAuditAsync` does via `IsInvalidAuditCursor`). Low likelihood — list cursors are server-issued protected cursors.
- (re-confirms existing D7 entry) LOW — empty `Constant` `[RestQueryBinding]` value emits an empty aggregate id with no HESREST diagnostic (`RestApiControllerEmitter.cs:376`). Same silent-drop class as the already-listed generator-diagnostic hardening item; the GetHashCode-NRE sub-claim was refuted (`GetString` never returns null).
- (re-confirms existing D7 entry) — **RESOLVED/SUPERSEDED by Story 2.8 / AD-15** for EventStore provenance enforcement and generated header gating; see the reconciled HIGH item above. D6 and the Tenants-only Story 4.7 producer cleanup remain separate.

## Deferred from: integration + E2E test-suite recovery (2026-07-06, spec-integration-e2e-test-recovery)

- MEDIUM (CI coverage) — `HotReloadTests` (`tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs`) now owns and disposes an isolated Aspire fixture per test, contains three live tests, and successfully exercises DCP sample stop/start on this WSL2 host. The remaining pre-existing gap is CI execution: no PR/push workflow runs the Tier-3 `Hexalith.EventStore.IntegrationTests` project, so hot-reload readiness regressions can merge without exercising the real stop/restart path. The dedicated Aspire-in-CI follow-up in `sprint-change-proposal-2026-06-22-ci-release-retier.md` retains ownership.
- LOW (test isolation) — Only `AggregateActor` type name is per-run randomized (`EventStore__Actors__AggregateActorTypeName`); `ProjectionActor`/`ETagActor`/`GlobalPositionActor` use fixed const type names (`QueryRouter.ProjectionActorTypeName`, `ETagActor.ETagActorTypeName`, `GlobalPositionActor.ActorTypeName`). On a shared, long-lived Dapr placement (e.g. a sibling repo's AppHost also built on EventStore), a stale/dead host for those fixed names makes actor invocations block ~60s (client `HttpClient.Timeout`) instead of resolving. Integration runs need either a dedicated placement per run or per-run randomization of those three actor type names. Root cause of the initial "projection queries hang 60s" symptom (compounded by the now-fixed `QueryResult` deserialization bug).

## Deferred from: follow-up review of spec-1-3-generic-read-models-and-query-cursors (2026-07-06)

- source_spec: `_bmad-output/implementation-artifacts/spec-1-3-generic-read-models-and-query-cursors.md`
  summary: The default full-replay projection path forwards generic paging but only enforces cursors, silently ignoring `Offset`/`PageSize` it cannot honor, and the caching actor keys on paging the default actor never applies — so distinct un-honorable offsets create identical duplicate cache entries and, past the 32-entry cap, evict other query types on the shared actor.
  evidence: `EventReplayProjectionActor.ExecuteQueryAsync` (`src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:90-92`) hard-fails a nonblank `Paging.Cursor` with the `invalid-cursor` sentinel (→ HTTP 400) but takes no action on `Paging.Offset`/`PageSize`; a validator-passing `paging={offset:50}` returns the entire unpaged singleton state with no signal offset was dropped. `CachingProjectionActor` (`Actors/CachingProjectionActor.cs:62-66,204-209`) folds `ComputePagingChecksum(envelope.Paging)` into `CacheEntryKey`, so each distinct ignored offset/pageSize occupies a separate identical entry and the `MaxCacheEntries=32` overflow guard's `_payloadCache.Clear()` then evicts unrelated cached query types on the same shared actor. Two independent reviewers converged on this paging-enforcement asymmetry; low severity (bounded per-actor, singleton state, cursor-only was the story's in-scope path), but a genuine platform-level consistency gap for offset paging against actors/handlers that do not honor it. Cursor-only forwarding to domain handlers/projections for downstream validation is by-design per the intent contract; this entry concerns only the un-honorable-offset asymmetry and its cache-fragmentation consequence.

## Deferred from: review of spec-1-4-projection-and-domain-event-consumer-seams (2026-07-06)

- source_spec: `_bmad-output/implementation-artifacts/spec-1-4-projection-and-domain-event-consumer-seams.md`
  summary: Domain-event processor message-level markers cannot make multiple independently side-effecting handlers atomic; a later handler failure may replay earlier successful handlers.
  evidence: `EventStoreDomainEventProcessor.DispatchAsync` invokes all registered `IEventStoreDomainEventHandler<TEvent>` handlers sequentially under one message marker. If handler A commits a side effect and handler B throws, the processor releases the message marker so DAPR can redeliver, and handler A can run again. Stronger guarantees need per-handler markers or a transactional/composite handler contract; this story documents handler idempotency and keeps the marker seam message-level.

## Deferred from: follow-up review of spec-1-4-projection-and-domain-event-consumer-seams (2026-07-06, review pass 2)

- source_spec: `_bmad-output/implementation-artifacts/spec-1-4-projection-and-domain-event-consumer-seams.md`
  summary: A misconfigured (non-existent) `PayloadAggregateIdPropertyName` silently drops every consumed event as an aggregate mismatch, indistinguishable from a legitimate value mismatch and with no distinct diagnostic.
  evidence: `EventStoreDomainEventProcessor.TryGetPayloadId` resolves the configured property via reflection; a name that is not a public instance property resolves to a cached null `PropertyInfo`, so `TryGetPayloadId` returns false for every payload. The caller (the `_payloadAggregateIdPropertyName is not null && (!TryGetPayloadId(...) || !string.Equals(...))` branch in `ProcessAsync`) then treats every event as `SkippedAggregateMismatch`, acknowledges it (HTTP 200), and logs only at Information — so a typo silently discards the entire subscription's traffic. The property-resolution feature pre-dates this story (this diff only re-keyed the reflection cache by (event type, property name)); the silent-drop-on-missing-property behavior was surfaced incidentally by the review, not introduced here. A fix would distinguish "property not resolvable on a resolved event type" (misconfiguration → fail fast at startup or emit a distinct warning) from a legitimate per-event value mismatch.

## Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)

- source_spec: `_bmad-output/implementation-artifacts/spec-1-5-domain-module-hosting-observability.md`
  summary: Query/projection-handler domain discovery for telemetry only recognizes handlers that carry `[EventStoreDomain]` or expose a public parameterless constructor; a DI-constructed handler with no attribute is silently dropped (its domain gets no diagnostics), and the parameterless path reflectively instantiates handler types at host-build time.
  evidence: `GetHandlerDomainNames<THandler>` (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:214-240`) yields a domain name only when the handler type has `[EventStoreDomain]` or a public parameterless ctor it can `Activator.CreateInstance`; a handler with only a dependency-injecting ctor and no attribute hits `continue` and is skipped, so its domain never gets an `EventStoreDomainDiagnostics`/keyed service/OTel source — and `GetRequiredKeyedService<EventStoreDomainDiagnostics>(domain)` then throws for that domain's own code while its admission telemetry is silently absent. The parameterless path also executes the handler constructor at host-build time (aborts startup if it throws; leaks a throwaway if the handler is `IDisposable`), and the `handler switch { IDomainQueryHandler => ..., IDomainProjectionHandler => ... }` matches the query branch first, so a type implementing both handler interfaces with divergent domains never registers its projection domain. The spec's residual-risk note accepts the `[EventStoreDomain]` requirement for dependency-heavy handlers; a robust fix resolves domain names from DI-materialized handlers or a static metadata seam rather than reflective construction. Two independent reviewers converged; genuine but requires a design decision, hence deferred rather than auto-patched.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption-2.md`
  summary: Health endpoints do not declare an explicit anonymous-access contract, so a future global fallback authorization policy could block DAPR app-health probes even when the probe targets `/alive`.
  evidence: Story 1.6 follow-up switches the public Aspire domain-module sidecar app-health default from `/ready` to `/alive`, resolving the sidecar-dependent readiness feedback loop, but `MapDefaultEndpoints` still maps `/health`, `/alive`, and `/ready` without explicit `AllowAnonymous()` metadata. Current EventStore tests show unauthenticated health calls succeed under today's auth setup, but adding a global fallback policy later could make DAPR mark modules unhealthy unless health endpoint anonymity is made an intentional contract.
  resolution: Contract defined as architecture invariant AD-16 (health/liveness/readiness endpoints `/health`, `/alive`, `/ready` are explicitly `AllowAnonymous` + support-safe; any global fallback authorization policy lands in the same-or-earlier slice and is never weakened to reach probes) via `sprint-change-proposal-2026-07-07-health-endpoint-anonymous-access-contract.md`. Enforcement carried by Stories 5.3, 5.5, and 7.3 (AD-16 acceptance criteria + positive-probe/negative-protected-endpoint test). status: RESOLVED 2026-07-07 (correct-course).

- source_spec: `_bmad-output/implementation-artifacts/spec-1-5-domain-module-hosting-observability.md`
  summary: A single `EventStoreDomainDiagnostics` instance is owned and disposed by the registry yet also returned from the keyed and single-domain DI factories, so the container double/triple-disposes it at teardown — harmless today only because `ActivitySource`/`Meter` disposal is idempotent.
  evidence: `EventStoreDomainDiagnosticsRegistry` constructs each `EventStoreDomainDiagnostics` with `new` and disposes them in its own `Dispose()` (`src/Hexalith.EventStore.DomainService/EventStoreDomainDiagnosticsRegistry.cs`), while the keyed factory and the single-domain non-keyed factory (`src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetryExtensions.cs`) both return the registry-owned instance. MS.DI tracks any `IDisposable` returned from a singleton factory and disposes it at container teardown, so the same instance is disposed by the registry plus once per resolved factory. It is benign today only because `ActivitySource.Dispose()`/`Meter.Dispose()` are idempotent and the type holds no other disposable state — an incidental guarantee, not a guarded one. Resolve by clarifying ownership: either the registry does not dispose instances it hands out through DI, or the factories return non-owned wrappers / the type gets an idempotent dispose guard. Both reviewers independently flagged this; low current severity.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md`
  summary: Domain-module guardrails still cannot broadly ban all direct DAPR/host wiring while the initialized Tenants domain-service host carries transitional `AddDaprClient`, `UseCloudEvents`, controller, MediatR, and router composition.
  evidence: Story 1.7 strengthens the clean Sample reference and platform-owned state/cursor/telemetry/health/endpoint checks, but a broad scan for all DAPR/host wiring markers would fail current `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`. Enforce the broader rule after the remaining Tenants host composition has moved behind EventStore platform seams or has an explicit permanent exception.

- source_spec: `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md`
  summary: Domain-module endpoint guardrails still rely on lightweight same-file route resolution and do not prove canonical route values passed through cross-file constants or variables.
  evidence: The follow-up review hardened direct literal, same-file constant, repeated-constant, and simple `MapGroup` route detection in `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`, but a domain module could still hide `/process`, `/replay-state`, `/query`, `/project`, or `/admin/operational-index-metadata` behind a route value imported from another type or computed variable. Closing this completely needs a Roslyn-level syntax/semantic guardrail or an explicit convention that forbids indirect canonical endpoint route values in scanned domain roots. The same "lightweight scan cannot be complete or sound on arbitrary C#" class also covers the receiver-agnostic state-access soundness gap surfaced by the 2026-07-07 correct-course review: `ContainsInvocationOnCallResult` (`DomainModuleAuthoringGuardrailTests.cs:806-820`) matches `).<marker>(` on any call result with generic state-method names (`GetStateAsync`/`SaveStateAsync`/`SetStateAsync`/`ClearCacheAsync`), a potential false-positive on unrelated domain method chains. Both directions (false-negative indirection and false-positive over-match) are closed only by a Roslyn/convention-level guardrail, not further regex refinement — see accepted entry DW-1.

### DW-1: Follow-up review still recommended for 1-7-domainservice-packaging-and-guardrails after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-1-7-domainservice-packaging-and-guardrails.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-071516-abab; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted 2026-07-07 (correct-course, sprint-change-proposal-2026-07-07-story-1-7-followup-review-disposition)
resolution: A terminating follow-up review pass was run per the Epic 1 retro action item. Deliverable green — `DomainModuleAuthoringGuardrailTests` 25/25, `ReleasePackageManifestTests` 8/8. All remaining findings are the regex-scan-completeness/soundness class already captured by the two substantive Story 1.7 deferred entries above (broad DAPR/host-wiring ban; cross-file/computed canonical route resolution). A fifth regex patch would re-arm the non-converging loop and fail the retro completion criterion ("no open follow-up-review-only item for Story 1.7"). Future closure of the finding class = a scoped Roslyn/convention-level guardrail story, not another follow-up review. `spec-1-7` `followup_review_recommended` cleared to false.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md`
  summary: `RestQueryBindingAttribute` runtime construction permits `EntitySource = None` with a non-null entity value even though the generator rejects that shape.
  evidence: The attribute stores `EntityValue` unchanged when `entitySource == RestQueryBindingSource.None`, while `RestApiControllerEmitter` treats a value with `None` as invalid metadata; the mismatch pre-dates Story 2.1 and needs generator/contract hardening rather than a contract-seam patch.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md`
  summary: `RestQueryBindingAttribute` preserves padded route/constant binding values that can fail generator route-parameter lookup later.
  evidence: `ValidateValue` accepts and preserves values such as `" tenantId "`, while the generator route lookup compares binding route names to parsed route parameters without trimming; the issue is real but existed before this story and belongs with REST generator binding hardening.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md`
  summary: Undefined `RestTenantSource` values can flow through the generator as non-standard tenant-source text.
  evidence: `RoslynAttributeValueReader.GetEnumName` returns the numeric text for an out-of-range enum value, and generated `ResolveTenant` only handles `System` and `Route` specially before falling back to claims behavior; robust handling needs a generator diagnostic or explicit invalid-enum policy outside this contract-seam pass.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
  summary: Generated command endpoints do not emit the canonical 1 MiB request-body limit used by platform gateway command and query controllers.
  evidence: `CommandsController.Submit`, `QueriesController.Submit`, validation controllers, replay, and stream endpoints declare `[RequestSizeLimit(1_048_576)]`, but generated command actions in `RestApiControllerEmitter.AppendCommandAction` accept `[FromBody]` contract payloads without a generated `RequestSizeLimit` attribute. The gap existed in the prior generator and was surfaced incidentally during the Story 2.2 follow-up review; fixing it needs a deliberate generated API-host payload-size policy.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
  summary: Generated command problem mapping drops safe domain-rejection extensions such as `rejectionType` and `correctiveAction`.
  evidence: `DomainCommandRejectedExceptionHandler` emits `GatewayProblemDetailsExtensions.RejectionType` and `CorrectiveAction`, and `EventStoreGatewayClient` captures arbitrary non-standard ProblemDetails extensions in `EventStoreGatewayException.Extensions`, but generated controllers currently forward only correlation, tenant, reason, reasonCode, and filtered validation errors. The omission pre-dates this review pass and needs a deliberate generated API extension allowlist.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
  summary: Generated command success responses hard-code `/api/v1/commands/status/{id}` as a relative status `Location`.
  evidence: `RestApiControllerEmitter.AppendCommandAction` writes `Response.Headers["Location"] = "/api/v1/commands/status/" + Uri.EscapeDataString(...)`, while the platform `CommandsController.Submit` builds an absolute URI from the current request host. Dedicated generated API hosts may not expose that relative status route, so status-location policy needs a focused generated-host design.
  resolution: Policy defined as architecture invariant AD-17 (absolute-to-gateway, fail-closed when unconfigured, single-sourced gateway status key) via `sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`. status: **RESOLVED 2026-07-07 by Story 2.6** — `RestApiControllerEmitter.AppendCommandAction` no longer emits any hard-coded relative `/api/v1/commands/status/` literal; the generated command 202 resolves an **absolute** `Location` at request time through the injected `ICommandStatusLocationBuilder` (`Hexalith.EventStore.Client.Gateway`), and emits **no** `Location` header when the gateway status base is unconfigured (fail-closed per AD-10). Absorbs rest-generator-hardening Second-Wave item **S2**. Evidence: `RestApiControllerGenerationTests` + `RestApiGeneratedControllerErrorSemanticsTests` (110/110).

- source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
  summary: Generated query actions map caught `ArgumentException` messages directly into client-facing ProblemDetails.
  evidence: `RestApiControllerEmitter.AppendQueryAction` catches `ArgumentException` and calls `CreateProblem(..., ex.Message)`, bypassing the support-safe display-text filtering used for gateway exceptions. The catch existed before Story 2.2 and should be hardened separately with a fixed safe message or shared sanitizer.

### DW-2: Follow-up review still recommended for 2-2-rest-api-generator-discovery-and-controller-emission after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-112402-3779; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required. Reviews converged to 0 HIGH; all substantive residuals are separately tracked (generated command request-size limit, command-rejection extension forwarding, status Location policy, query ArgumentException sanitization) under the REST generator hardening and command-status Location action items (owner: Winston). Evidence: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` → 108/108 passed on 2026-07-07 at HEAD fc0f1de8. See `sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md`
  summary: Generated Sample API command success responses expose the generator's relative `/api/v1/commands/status/{id}` status location even though the external API host does not itself map that status route.
  evidence: `SampleApiGeneratedControllerRuntimeTests` proves the compiled Sample API generated command action emits the existing generated `Location` header; `Sample.Api` maps only generated controllers and default endpoints, so polling that relative status URL depends on an external routing/proxy policy not owned by this proof story.
  resolution: Policy defined as architecture invariant AD-17 (absolute-to-gateway, fail-closed when unconfigured, single-sourced gateway status key) via `sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`. status: **RESOLVED 2026-07-07 by Story 2.6** — `Sample.Api` opts into the absolute status base via `AddEventStoreCommandStatusLocation` (config `EventStore:GatewayStatusBase`) and defaults fail-closed; `SampleApiGeneratedControllerRuntimeTests` now proves **both** absolute-when-configured (`https://gateway.example/api/v1/commands/status/{statusId}`, never relative) and no-`Location`-when-unconfigured against the real compiled `CounterRestController` (Sample.Tests 116/116).

- source_spec: `_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md`
  summary: Sample DAPR app-id handlers append `dapr-app-id` and `dapr-api-token` headers without replacing preexisting values.
  evidence: `samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs` and `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs` call `TryAddWithoutValidation` for DAPR routing headers, so a caller-provided conflicting value could produce duplicate sidecar routing/token headers; this handler behavior predates the generated Sample API host proof and needs a focused outbound-DAPR-header policy fix.
  status: reconciled 2026-07-07 (sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy). Policy decided and formalized as architecture invariant **AD-18** (Outbound Sidecar Control-Plane Headers Are Handler-Owned): replace-not-append, handler-owned, innermost handler, caller/inbound values never routed. Scope is wider than the two files named here — the byte-identical defect also lives in `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs`. Enforcement owned by **Story 2.7** (centralize a single handler in `Hexalith.EventStore.Client` via `AddEventStoreGatewayClient(appId, apiToken?)`, delete the 3 in-repo copies, add pre-existing-header replacement test + a guardrail structural test). The identical `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` copy is a **coordinated submodule follow-up requiring maintainer approval** (Story 2.4 lineage), not modified under Story 2.7.

### DW-3: Follow-up review still recommended for 2-3-sample-external-api-host-proof after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-2-3-sample-external-api-host-proof.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-112402-3779; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required. Reviews converged to 0 HIGH; the substantive residuals (status Location dependency, Sample DAPR app-id header append-vs-replace) are separately tracked under the command-status Location policy (owner: Winston) and outbound DAPR routing-header policy (owner: Amelia) action items. Evidence: `dotnet test tests/Hexalith.EventStore.Sample.Tests/` → 115/115 passed on 2026-07-07 at HEAD fc0f1de8. See `sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md`.

- source_spec: `_bmad-output/implementation-artifacts/spec-2-5-scoped-metadata-rich-projection-notifications.md`
  summary: Raw SignalR hub leave calls do not validate projection type or tenant id before building and removing malformed group names.
  evidence: `ProjectionChangedHub.LeaveGroupCoreAsync` validates scoped suffixes added by Story 2.5 but still lacks the projection/tenant null, blank, and colon guards that `JoinGroupCoreAsync` applies; malformed raw `LeaveGroup` or `LeaveGroupScoped` calls can reach `RemoveFromGroupAsync` and debug logs with invalid group names. The leave path and its projection/tenant validation gap pre-date this story, while the scoped-suffix validation was the only changed behavior here.
  status: **RESOLVED 2026-07-07 by sprint-change-proposal-2026-07-07-signalr-hub-leave-validation** — `LeaveGroupCoreAsync` now applies the same `ArgumentException.ThrowIfNullOrWhiteSpace(projectionType/tenantId)` + colon guards as `JoinGroupCoreAsync` (leave stays authorization-free by design). Covered by 5 new `ProjectionChangedHubTests` (leave/scoped-leave projection+tenant colon and null/blank), Server.Tests green (34/34). Satisfies Epic 2 retro Action #6 completion gate (same safe group rules as join).

## Deferred from: code review of sprint-change-proposal-2026-07-07-signalr-hub-leave-validation (2026-07-07)

- source_spec: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-signalr-hub-leave-validation.md`
  reviewed_commit: `a9fff8d7` (fix(signalr): validate projectionType/tenantId on hub leave path — committed to main by the concurrent bmad-loop mid-review; reviewed content byte-identical).
  summary: `projectionType`/`tenantId` have no length bound and no control-character/newline rejection on either the SignalR join or leave path — only `scope` is capped (`MaxGroupScopeLength = 64`) and only colons + whitespace-only are rejected.
  evidence: `ProjectionChangedHub.JoinGroupCoreAsync` (`:85-90`) and the mirrored `LeaveGroupCoreAsync` (`:161-170`, added by this change) validate null/blank + colon only. An oversized or control-char value (e.g. `new string('a', 100000)`, `"order\nlist"`, a NUL segment) passes both guards, is built into a group name by `BuildGroupName`, reaches `Groups.AddToGroupAsync`/`RemoveFromGroupAsync`, and is emitted to the Debug structured logs `ClientJoinedGroup`/`ClientLeftGroup` (`Log` EventIds 1080/1081). Impact is low — on leave it is a harmless idempotent no-op and the log is Debug-level structured (field capture, not string interpolation, so no log-record forgery); on join the unbounded key is retained in the static `_connectionGroups` set. This change is faithfully symmetric with the already-shipped join guards, so the gap is pre-existing and lives on both paths.
  disposition: defer to a scoped follow-up that hardens BOTH join and leave together (do not single-path patch). When it lands, also (a) decide the null/blank client-error contract — both paths currently throw `ArgumentException` (generic SignalR client error) rather than the descriptive `HubException` used for colon violations; and (b) add symmetric `LeaveGroupScoped` tenant-id-colon + null/blank tests to match the raw `LeaveGroup` coverage.

## Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)

_All items LOW / non-blocking. Story 2.7 accepted (all AC1–AC7 met; Release build re-verified clean via a forced non-incremental Admin.UI rebuild, 0 warnings). Five of six are defense-in-depth hardening of the AD-18 guardrail test — not defects in the delivered production code. Reviewed working tree floated on HEAD `bb4ee369` (`chore(release): 3.50.4`, CHANGELOG-only; committed to main by the concurrent bmad-loop mid-review) with the 2.7 change set uncommitted; story baseline `25def99e`._

- **Guardrail evadable by a non-`*Dapr*`-named handler or a non-literal setter.** The reflection guards (`SampleApiStructuralTests.SampleHostAssemblies_DeclareNoLocalDaprRoutingHandler`, `DaprRoutingHeaderOwnershipTests.AdminUiAssembly_DeclaresNoLocalDaprRoutingHandler`) key on `type.Name.Contains("Dapr")`, and the source scan (`DaprRoutingHeaderOwnershipGuardTests`, `:526`) matches only the literal `TryAddWithoutValidation("dapr-app-id"|"dapr-api-token")`. A future host handler named e.g. `SidecarHandler` that sets the header via `Headers.Add(...)`, a `const` name, `DefaultRequestHeaders`, or mixed casing (`"Dapr-App-Id"`) escapes both layers. Catches the realistic regression (a verbatim copy of the deleted `DaprAppIdHandler`) but not the full AD-18 surface. **Disposition:** do NOT extend the regex (standing regex-guardrail disposition — regex follow-ups don't converge); fold into a future scoped Roslyn/convention guardrail story.
- **Guard `hostRoots` is a hardcoded 3-entry list** (`DaprRoutingHeaderOwnershipGuardTests.cs:543`). A new host directory is scanned only by the repo-wide literal-`TryAddWithoutValidation` `setterFiles` backstop, so a new host using a non-literal setter slips the per-host loop. Same Roslyn-guardrail consolidation.
- **Source-scan enumerates `*.cs` only** (`DaprRoutingHeaderOwnershipGuardTests.cs:551`), missing DAPR-header sets inside `.razor` `@code` blocks. Theoretical — routing handlers are not authored in razor markup.
- **Guard-test robustness** (`DaprRoutingHeaderOwnershipGuardTests.cs:599`; `SampleApiStructuralTests.cs:33`). `RepositoryRoot()` throws `DirectoryNotFoundException` (rather than skipping) when the test binary runs detached from the source tree; the reflection guards call `Assembly.GetTypes()` without catching `ReflectionTypeLoadException`. Low likelihood; minor robustness.
- **Public `AddEventStoreDaprServiceInvocation` does not format-validate `appId`** (`EventStoreServiceCollectionExtensions.cs:68`) — only `ThrowIfNullOrWhiteSpace`. On the published `Hexalith.EventStore.Client` package, an external consumer passing a whitespace/control-char `appId` gets fail-fast broken routing (a CR/LF value is dropped by `TryAddWithoutValidation`). In-repo callers pass safe literals, so no live impact. The one item on new public surface worth a later hardening patch (independent of the regex-guard disposition).
- **`apiToken` whitespace handling inconsistent with `appId`** (`DaprServiceInvocationHandler.cs:16`). `apiToken is { Length: > 0 }` forwards a whitespace-only token (`"   "`) as authoritative, whereas `appId` is `ThrowIfNullOrWhiteSpace`-guarded. Matches the deleted handlers' `!string.IsNullOrEmpty` behavior (no regression); operator error fails fast at the sidecar. Optional consistency nit.

## Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-11)

- No production code calls `IProjectionStateEraser` — only the DI registration in `ServiceCollectionExtensions.cs:56`. The end-to-end read-model/checkpoint drift fix is unreachable from any wired in-tree path; it depends on a future Admin/GDPR-1 erasure trigger. Deferred as expected — the caller is exactly what the governing-contract decision (see Story 1.9 Review Findings) resolves. Do not add a caller in isolation before that decision.

## Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Complete the in-progress Story 1.9 erasure refactor so the Server and Server.Tests projects compile again.
  evidence: Pre-existing Story 1.9 working-tree changes delete `IProjectionStateEraser`, `ProjectionStateEraser`, and `ReadModelEraseTarget` while production registration and `StorageKeyIsolationTests` still reference them; `ProjectionCheckpointTracker` also exposes an internal capability through a public class.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Preserve or explicitly version the released erasure API surface being removed by Story 1.9.
  evidence: The pre-existing Story 1.9 diff removes released interface members and public erasure types without an API-compatibility gate, creating source and binary breaks that the current concrete-class tests cannot detect.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Make Story 1.9 erasure capability DI fail closed for custom stores and checkpoint trackers.
  evidence: The pre-existing registrations can bind a default DAPR eraser behind a custom non-capable `IReadModelStore` or unconditionally cast a custom `IProjectionCheckpointTracker`, risking wrong-backend mutation or resolution-time failure instead of `Unsupported`.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Wire and verify Story 1.9 projection slot discovery and canonical read-model address ownership end to end.
  evidence: The pre-existing slot/address types lack reliable registration and contract coverage, declarations can be skipped by handler-registration early returns, and no production writer currently proves it uses the same canonical key factory as erasure.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Finish the Story 1.9 persisted erasure coordinator and lifecycle/admin boundary before exposing partial seams.
  evidence: The active story requires resumable coordination, rebuild-checkpoint deletion, delivery-last ordering, lifecycle serialization, active-rebuild refusal, structured outcomes, and an authenticated boundary, but the reviewed pre-existing diff does not yet provide those runtime components.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md`
  summary: Make in-memory read-model writes atomic with batch accessor ETag compare-and-set operations.
  evidence: Pre-existing `SaveAsync`, `TrySaveAsync`, `TryEraseAsync`, and `SeedRaw` paths do not share the batch accessor's `_gate`, so a true concurrent write can occur between the fake accessor's ETag check and assignment and be overwritten while conditional success is reported.

## Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)

- source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md`
  summary: Add committed-state tests for new fail-closed / drain-identity paths (distinct messageId≠correlationId drain, Expired-outcome actor commit, SubmitCommandHandler identity guards).
  evidence: Message-keyed drain handoff + advisory-status identity is only exercised where messageId ≡ correlationId; the actor commit of a staged Expired idempotency mutation (AggregateActor.cs:173-176) is undriven; SubmitCommandHandler fail-closed guards (SubmitCommandHandler.cs:65-71,117-124) are untested.

- source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md`
  summary: Cover AdminTraceQueryController correlation-index resolution/ambiguity path and accept the advisory-index not-found degradation.
  evidence: The resolve→ambiguity-409→message-primary-read branch (AdminTraceQueryController.cs:59-78) is unexercised because Dw3TestUtilities.cs:185 builds the controller with a null index; not-found-when-index-missing is inherent to an advisory index queried by correlationId (state scan forbidden).

- source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md`
  summary: Bound the correlation-index overflow marker so a hot shared correlationId is not permanently ambiguous.
  evidence: DaprCommandCorrelationIndex.cs:81 refreshes OverflowExpiresAt on every over-capacity AddAsync, so a steadily-loaded correlationId stays Ambiguous (409) indefinitely even after the original 128 entries expire.

- source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md`
  summary: (Story 4.4) Prevent domain re-execution when a Recoverable (stored-but-unpublished) idempotency record expires after the retention window.
  evidence: IdempotencyChecker.ClassifyAsync applies the bounded ExpiresAt to Recoverable records too (expiry check precedes the disposition branch), so a retry after 24h is treated as a miss and could re-execute the domain. Broader recovery is owned by Story 4.4.

- source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md`
  summary: Correct the drain activity message-id telemetry tag for legacy correlation-keyed drain records.
  evidence: DrainUnpublishedEventsAsync sets eventstore.message_id to the tracking id (a correlationId for legacy records) before the real message id is added, undermining message-id-primary telemetry.

## Deferred from: code review of story-1.9 (2026-07-13)

- Retained legacy aggregate-wide checkpoint feeds the empty-stream drift branch (`ProjectionUpdateOrchestrator.cs:129`). An erased/recreated identity that had a legacy checkpoint and later reads an empty stream logs spurious `CheckpointDriftDetected` (diagnostic noise only — no mutation, no suppressed delivery). Direct consequence of the human-approved Option A retained-legacy-key relaxation; revisit if diagnostic noise is a problem or if a bounded legacy-key cleanup is added.
- `ProjectionUpdateOrchestrator` narrowed `public`→`internal` and dead erase surface. The visibility narrowing is disclosed/justified (verified no external consumer; DI via interfaces; no PublicAPI baseline). `IProjectionReadModelAddressFactory.CreateAggregateOwnedManifest` and `ProjectionEraseOutcomeKind.Denied` are currently unused; they become live only if the slot-completeness decision (Review Finding) adopts manifest-based erasure. Remove or wire per that decision.

## Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13)

- DAPR batch accessor infers key existence from ETag presence (`DaprReadModelBatchStateAccessor.cs:23`): `string.IsNullOrEmpty(etag) ? absent : present`. An ETag-less store or value reads as absent even when a value is returned. Masked on Redis (always returns ETags), and the resumable CAS protocol fundamentally requires ETags, so no impact on the supported backend. Revisit only if a non-ETag state store is ever qualified; existence should then key off value presence, not the ETag.
- Corrupt/tampered base64 in a stored envelope throws `FormatException` out of `GetAsync`/reconcile (`ReadModelBatchEnvelope.PreviousBytes`/`CandidateBytes`, lines 72-78): only `JsonException` is guarded in `FromBytes`, but the subsequent `Convert.FromBase64String` on `prev`/`cand` is unguarded. Requires storage corruption/tampering (outside the normal contract). Cheap one-line hardening (guard the base64 decode → treat as unreadable/legacy) if robustness against corrupted state is later required.
- Orphaned foreign envelope from an abandoned/never-retried batch permanently blocks any other batch touching that logical key (`ReadModelBatchProtocol.cs:262-269`, InstallAsync foreign-envelope branch → `OptimisticConflict` with no cleanup of the foreign envelope). Inherent to the resumable no-TTL design where prepared/aborting markers and envelopes are retained until reconciled. Decision 5 explicitly defers a bounded retention/cleanup horizon to Story 1.13 (together with its production delivery-checkpoint/dedup contract).

## Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13, decision follow-up)

- source_spec: `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md`
  summary: (HARD GATE for Story 1.12/1.13) Run the `ReadModelBatchLiveSidecarTests` lane in a working Tier-3 (real Redis/DAPR) environment before wiring the coordinated batch into production projection dispatch, and add the omitted Task-8 scenarios.
  evidence: The live lane is the ONLY real-backend evidence for AC2/AC3/AC7/AC8 and for the story's founding premise (a void DAPR/Redis transaction can partially commit), but it never executed here (VSTest host exit 144 during collection-fixture startup; pre-existing `DaprETagServiceLiveSidecarTests` fails identically). Deterministic fakes/recorder are "request-shape evidence only, never completion proof." The authored `ReadModelBatchLiveSidecarTests.cs` also currently omits injected partial-prefix old-view visibility, conflict/abort restoration, and post-dispatch cancellation reconciliation despite Task 8 being checked. Decision 2026-07-13: deterministic evidence accepted to advance Story 1.10; the live run + missing-scenario authoring HARD-GATE the Story 1.12/1.13 production wiring. Also add deterministic transaction partial-commit coverage (see the [Review][Patch] item on `VerifyTransactionAsync`), which does NOT need the live env.

## Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)

- Body-ETag fallback can surface a non-gateway validator on a `ProjectionBacked` route with no gateway ETag (`QueriesController.cs:210` `ETag: gatewayETag ?? producerMetadata?.ETag`; `EventStoreGatewayClient.cs:343` `ETag = eTag ?? normalized.ETag`). Reachable only when a non-conformant producer claims `ProjectionBacked`, omits `ProjectionType` (skipping the gateway ETag fetch) AND fabricates an ETag; the platform projection actor never sets `metadata.ETag`, and the leaked value cannot drive a false 304 (that needs the gateway-computed `currentETag`). Hardening: only surface the gateway-issued ETag as the opaque validator; do not fall back to producer body ETag. [edge-case-hunter]
- Real-path handler-vs-projection route→provenance proof (`QueryResponseProvenanceE2ETests`) runs in no CI workflow — it is Tier-3 (gated out; the dev ran it manually in source-debug mode), and the Tier-2 persistence test injects the route result rather than exercising `HandlerAwareQueryRouter`'s real handler-vs-projection selection. Pre-existing Tier-3-not-gated constraint tracked by Epic 3 Story 3.1. Lighter-weight guard: a Tier-2 `Server.Tests` test that resolves the real `HandlerAwareQueryRouter` + handler registry and asserts stamped provenance per route. [verification-gap+blind-hunter+acceptance-auditor]
- Single-source the canonical provenance-name formatter — `QueriesController.cs:127` emits the `X-Hexalith-Query-Provenance` header via `Provenance.ToString()` while the client (`GetProvenanceHeader`) and generated controller use an explicit canonical `nameof` switch. Safe today only because provenance is normalized to a defined value before that line; a future path that reaches it with an out-of-range value would emit a numeric string the strict parser maps to `Unknown`. [blind-hunter+acceptance-auditor]
- Duplicated projection-evidence sanitization across two assemblies — server `QueriesController.NormalizeProducerMetadata` and client `EventStoreGatewayClient.NormalizeMetadata` independently null `{ETag, IsNotModified, IsStale, ProjectionVersion}` for non-projection routes. Any future field added to "projection evidence" must be cleared in both, in two assemblies, or a leak/asymmetry appears. Consider a shared helper. [blind-hunter]
- Minor test-hardening — the converter `Write` out-of-range default branch (`QueryResponseProvenanceJsonConverter.Write` → `nameof(Unknown)`) and the `EnforceFreshnessPolicy` non-`ProjectionBacked` → 400 branch lack direct assertions; both are fail-safe downstream (values normalized before serialization; freshness fails closed regardless), so low reachability. [verification-gap]
- Weak-ETag rejection is not route-aware — `EventStoreGatewayClient.GetETag` (called at `:174` on the 200 path, `:404` throw) rejects a weak `ETag` before provenance is known, failing the whole query even for a non-projection route that would discard the ETag. The EventStore server only ever emits strong ETags, so this is robustness against a header-rewriting intermediary, not a live path. [edge-case-hunter]

## Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)

- `HasFailures` blast radius on named-metadata rejection — a single domain service returning malformed/version-skewed named-projection metadata sets `hasFailures`, which makes `AdminOperationalIndexHostedService.StartAsync` skip ALL admin index writes AND the named-route catalog `Replace` for every app in the refresh; this is a once-at-startup load with no periodic retry, so named dispatch is disabled process-wide until restart. The atomic all-or-nothing publish is spec-mandated (§2); the cross-app coupling + missing refresh cadence is the broader concern. [src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:37] [verification-gap]
- `DomainProjectionHandlerResult.AlreadyCompleted()` has no state overload — a hand-written state-bearing named handler that returns `AlreadyCompleted()` on retry yields null state, so the coordinator advances the projection checkpoint without completing the deferred actor/ETag write (Resolved Contract #3/#5). The legacy adapter (always `Completed`+state) and batch-persistence handlers (null state, no actor write) are unaffected, so reach is narrow. Recommend adding an `AlreadyCompleted(JsonElement? state)` factory overload. [src/Hexalith.EventStore.DomainService/DomainProjectionHandlerResult.cs:25] [acceptance-auditor]
- `ProjectionDeliveryRetryWorkItem.CreateWorkId` omits app id / service version / fingerprint — `WorkId = SHA-256(tenant/domain/aggregate/headSequence)`, so two `(appId, serviceVersion)` bindings serving the same domain+head collide on one ledger item; the second binding's app/version consistency check then fails and it defers forever. Affects blue/green or multi-version rollout of the same domain. [src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorkItem.cs:44] [blind-hunter]
- `DomainProjectionCatalogRegistry` is in-memory and empty after a domain-service restart — until the gateway re-queries `/admin/operational-index-metadata` (a startup-only load), `Contains(fingerprint)` is false → `/project/v2` returns 400 `UnsupportedCapability` → the coordinator defers/retries. Overlaps the metadata refresh-cadence gap above. [src/Hexalith.EventStore.DomainService/DomainProjectionCatalogRegistry.cs:8] [edge-case-hunter]
- Retry taxonomy remainder (from code-review decision D2) — poison retry ceiling / dead-letter, catalog fingerprint/version re-bind, permanent-`4xx` handling, and terminal-only ledger cleanup for the named-projection delivery retry subsystem. Drift-ahead was made terminal in Story 1.12; the rest is deferred to Story 1.13 (poison/duplicate/dedup horizon) plus a dedicated retry-cleanup-policy story. Note a `/project/v2` `4xx` can be a transient metadata-refresh race, so terminal-`4xx` classification must be designed alongside the dedup horizon, not assumed permanent. [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:227] [blind-hunter+edge-case-hunter]

## Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-14)

- The active-rebuild gate remains a pre-existing check-then-act race: `ProjectionEraseCoordinator` snapshots `HasActiveOperatorRebuildForDomainAsync` before lifecycle admission, so a rebuild can become active between the check and the actor call while `allowFreshBegin` remains true. Closing this requires rebuild admission to share a persisted lifecycle fence rather than relying on the existing point-in-time store query. [`src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs:143`]

## Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-14, chunk 1)

- HTTP 200 with a literal `null` metadata body is treated as a successful empty load (`AdminOperationalIndexHostedService.cs:96`), so existing admin indexes can be rewritten from an incomplete response. This behavior predates Story 1.12; harden the legacy metadata loader to classify a null success body as a failed load before any index write or catalog replacement.

## Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)

- Activation outbox completion treats a completed named-dispatch call as durable even when `TryDispatchAsync` returns `false`, and the `finally` block can remove the activation after a later legacy delivery failure. Preserve the activation until every required delivery surface has durably completed. [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:183`]
- The query lifecycle overlay recognizes `Rebuilding` but leaves an in-flight `Erasing` projection indistinguishable from its prior payload state. Define and expose the pre-existing erase-query visibility policy in the erasure lifecycle scope. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:199`]
- A blank head-event `MessageId` logs and returns from named dispatch without persisting retry work, so an aggregate with no later trigger can remain unprojected indefinitely. Add a durable malformed-identity disposition or recovery trigger. [`src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:143`]
- Async projection-handler convention discovery is all-or-nothing: the presence of one manual `IAsyncDomainProjectionHandler` registration disables discovery of every other implementation. Deduplicate per implementation instead of suppressing the full scan. [`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:375`]
- Lifecycle actor source files contain multiple enum, record, interface, and persisted-state declarations despite the repository's one-type-per-file rule. Split the pre-existing declarations during a scoped structural cleanup. [`src/Hexalith.EventStore.Server/Actors/IProjectionLifecycleActor.cs:12`]
- Operational-index metadata request binding can deserialize `Domains` as null and then dereference `request.Domains.Count`, returning an internal error instead of a bounded malformed-request response. Add null-safe request validation. [`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:248`]
- Erase lifecycle admission does not reject blank operation/digest values, and an unknown persisted phase can fall through to a fresh erase admission. Validate erase identity and fail closed on undefined lifecycle phases. [`src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs:68`]

## Deferred from: code review of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-16)

- source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
  summary: Make dev-auto review finalization conditional on fail-closed artifact decisions instead of unconditionally marking every reviewed spec `done`.
  evidence: Story 1.20 requires any `final_decision: still blocked` or `authorize_consumer_migration: false` result to remain non-`done`, but `.agents/skills/bmad-dev-auto/step-04-review.md` currently sets `status: done` unconditionally after review; a generic guard and workflow test are needed so later automation cannot mistake a non-authorizing proof packet for completed closure.

## Deferred from: exact-SHA gate of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-16)

- source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
  status: implementation-complete/evidence-confirmed
  summary: Repair or explicitly disposition the named-projection lifecycle cleanup defect before selecting an approved parity runtime.
  historical_evidence: Clean detached candidate `85877902f8d60a466ab90cd8b68b53838863db1c` built Release with 0 warnings/errors and passed the broad unit lanes, but `Hexalith.EventStore.Server.LiveSidecar.Tests.dll` finished 42 passed / 2 failed. The isolated `NamedProjectionDispatchLiveSidecarTests` run finished 5 passed / 1 failed, and `NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger` reproduced alone at 0 passed / 1 failed because the Redis lifecycle hash remained present instead of returning to the idle/absent baseline. The initial full lane also reported an unreleased lifecycle lease in `ConcurrentDuplicateReverseAndConflict_StayEquivalentToOneInOrderDelivery`.
  closure_evidence: Corrective commit `7b73a2f5cde990b0a026ec280f7620d067b3d110` is present in exact clean detached commit `772cdfefa8163704de0f57042af5b0507c1ac771`. At that commit the exact formerly failing normal-delivery method passed 1/1, `NamedProjectionDispatchLiveSidecarTests` passed 6/6, and the complete live-sidecar lane passed 44/44. Story 1.16's separate named durable follow-up-review disposition remains open; it is not an implementation failure.

## Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)

- source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
  status: implementation-complete/evidence-confirmed
  owner: EventStore build/release maintainer
  summary: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20's tested runtime SHA.
  historical_evidence:
    - `global.json` formerly pinned SDK `10.0.299` (pre-baseline seed);
    - the installed SDK and host/runtime were later observed as `10.0.302` / `10.0.9` before ASP.NET caught up;
    - effective central ASP.NET pins were `10.0.9`.
  closure_evidence:
    - SDK correction `d6c849aaf8f77f967377f72b763bd44b3131a713`, ASP.NET correction `3a43d5e6151ebc51e945bf1b6cecda92fd198a09`, and validation hardening `8c70efb08b1bf2fcd077ad930c5827d1ab1594da` are present in commit `772cdfefa8163704de0f57042af5b0507c1ac771`;
    - the exact executable preflight observed repository and installed SDK `10.0.302`, effective ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10`.
  consequence: The baseline mismatch no longer blocks the current readiness audit. A later candidate, package build, or publication must repeat the executable preflight and fails closed if the baseline regresses.
  closure:
    1. Update the repository seed and central ASP.NET pins to the AD-11 baseline, or record named architecture-owner approval for a newer replacement.
    2. Install and capture the matching SDK/runtime.
    3. Restore and build `Hexalith.EventStore.slnx` in Release.
    4. Run the focused package/runtime validation owned by the correction.
    5. Commit the correction independently.
    6. Use that resulting commit, or a reviewed descendant, as the Story 1.20 candidate runtime.
  reopen_trigger: Any Story 1.20 packet update, package build, container publication, or owner-approval request that names a runtime without satisfying this baseline.

## Deferred from: Story 1.20 current-HEAD source-topology gate (2026-07-17)

- source_spec: `_bmad-output/implementation-artifacts/2-7-tenants-compatibility-and-package-mode-validation.md`
  status: implementation-complete/evidence-confirmed
  owner: EventStore Story 2.7
  summary: Reconcile stale sample domain registrations and prove Tenants handler routing in the real source topology before selecting the Story 1.20 runtime.
  evidence:
    - the original packet harness forced `UseHexalithProjectReferences=false`, compiled no `tenants` AppHost resource, and then timed out waiting for it;
    - the corrected exact-source run at `772cdfefa8163704de0f57042af5b0507c1ac771` compiled the Tenants resource and reproduced twice as 0/1 with HTTP 404 / `query_projection_missing`;
    - Tenants operational-metadata calls returned HTTP 200, but merged base configuration still registers `orders` and `inventory` against the sample service while the current sample discovers only `counter` and `greeting`;
    - an absent configured binding makes `AdminOperationalIndexHostedService` log Event 6101 and skip every derived index write, including `admin:query-types:tenants`, so `list-tenants` falls back to a nonexistent projection.
  closure_evidence:
    - root cause fixed on `main` by commit `fd8ab24da230058f2f239765b68d5e0a135b4b76`, which removed the stale `tenant-a|orders|v1` and `tenant-b|inventory|v1` registrations from `src/Hexalith.EventStore/appsettings.Development.json` (no `orders`/`inventory` remain in the EventStore host `DomainServices:Registrations`; the residual `orders`/`inventory` in `KeycloakRealms/hexalith-realm.json` are unrelated JWT auth attributes);
    - proved 2026-07-20 on clean source SHA `4f4906b3f30a3d4ed2658effc1c4f189f2f647c0` (contains `fd8ab24d`) in Debug/project-reference mode with only root-declared submodules initialized;
    - command: `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -p:UseHexalithProjectReferences=true -p:UseSharedCompilation=false --filter "FullyQualifiedName~QueryResponseProvenanceE2ETests"`;
    - build compiled `Hexalith.Tenants` and the `tenants` AppHost resource from source (project references, not package mode); topology started on a placement freed of the concurrent Tenants Aspire session;
    - result: `QueryResponseProvenanceE2ETests.LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence` PASSED (Total 1, Passed 1, Failed 0, Skipped 0); the test restarts EventStore with `admin:query-types:tenants` cleared, waits for the `tenants` resource healthy, then asserts `list-tenants` returns HTTP 200 with `HandlerComputed` provenance and no ETag/projection-version/is-stale evidence, and that persisted Redis `admin:query-types:tenants` rebuilt from live metadata contains `list-tenants`;
    - raw artifacts captured this session as `source-topology-provenance.trx` / `source-topology-provenance.2.log` (ephemeral scratchpad); re-capture durably if `4f4906b3` (or a descendant preserving this behavior) is the selected Story 1.20 runtime.
  consequence: The stale-registration / `query_projection_missing` blocker no longer prevents crediting the handler-provenance lane at source SHA `4f4906b3`. This closure records prerequisite evidence only; it does NOT authorize Story 1.20 consumer migration, and all Tenants/EventStore/Builds identity changes remain blocked per closure step 4. Per reopen_trigger, any Story 1.20 runtime selection of a different SHA must re-run this exact-source proof against that SHA.
  closure:
    1. Compile the query-provenance E2E with `UseHexalithProjectReferences=true` and only root-declared submodules initialized.
    2. Remove or correctly environment-scope stale sample registrations, or otherwise reconcile absent configured bindings without weakening fail-closed handling for genuine metadata failures.
    3. Prove `admin:query-types:tenants` contains `list-tenants` and the exact live E2E returns 200 with `HandlerComputed` provenance and no projection evidence.
    4. Keep all Tenants/EventStore/Builds identity changes blocked until Story 1.20 separately authorizes migration.
  reopen_trigger: Any selected runtime or parity packet that credits the handler-provenance lane without a healthy compiled Tenants resource and a positive exact-source result.

## Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate.md (2026-07-18)

- Reconcile the stale `Hexalith.EventStore.Server.Tests` CA2007 baseline exception in `_bmad-output/project-context.md:65`. The exception predates this review, while Story 3.1 now records an unfiltered Release run with 2,626 passed, 25 skipped, and no failure; leaving the old statement active can cause future agents to exclude a blocking deterministic lane from baseline validation.

## Deferred from: code review of spec-1-11-complete-projection-freshness-lifecycle (2026-07-16)

- Reconfirmed the existing Story 1.19 erase-query visibility gap at candidate `8aa6d0f0a417034d0c46eb9506fb7196a013401b`: a stable `Erasing` lifecycle falls through `QueryRouter.ApplyPersistedLifecycle`, so producer `Current` can remain projection-confirmed and mutation-eligible while read-model targets are being erased. The policy choice (for example `Unknown`, `Unavailable`, or rejecting the query) remains intentionally deferred under the earlier Story 1.19 ledger entry; this review adds no duplicate implementation owner. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:248`]

## Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-17)

- Commit `ba203bde` is unbuildable in isolation: it converts the only local definition of `Microsoft.Extensions.TimeProvider.Testing` to `PackageVersion Update` while `references/Hexalith.Builds` was still pinned at `edbaeaed`, whose central props do not define the package, so CPM restore of `Server.Tests`, `Server.LiveSidecar.Tests`, and `Admin.Server.Tests` fails with NU1010 at that commit; coherence arrives only with `ea6ce49b`'s Builds bump to `cfafcbf1`. Bisect/rollback hazard only — history is already on `main`, so no rewrite; note it when bisecting across 2026-07-17.
- The GitHub login-format regex `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$` in the proof-packet allowlist predicate (and mirrored in spec verification command 2) accepts trailing and consecutive hyphens that GitHub forbids (`jpiquot-`, `a--b`). Pre-existing packet behavior, currently moot because the spec predicate pins exact membership `["jpiquot"]`; tighten to `^(?=[A-Za-z0-9-]{1,39}$)[A-Za-z0-9](?:-?[A-Za-z0-9])*$` next time the packet validator is opened under an approved gate-logic change (the lookahead keeps GitHub's 39-character cap; the bare `^[A-Za-z0-9](?:-?[A-Za-z0-9]){0,38}$` form previously recorded here admits logins up to 77 characters). [`_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`]
- **RESOLVED 2026-07-31 by Story 3.5.** The former EventStore-local `Microsoft.Playwright` declaration was removed; effective MSBuild evaluation now resolves it exactly once from Hexalith.Builds, and the import-only wrapper guard rejects future local masks.

## Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18)

- The shared AI-instruction baseline rewrite (`CLAUDE.md`/`AGENTS.md`/`.github/copilot-instructions.md`, commit `4ee739d6`) dropped two safeguards the previous text carried: the Agent Skills clause banning skills whose *resolved canonical path* (symlink target) lies inside `references/`, and the standalone-clone rule authorizing initialization of root-declared `references/` submodules — a fresh standalone EventStore clone now has no permitted path to the mandatory `hexalith-llm-instructions.md` baseline while being ordered to stop without it (bootstrap deadlock). The baseline is shared normalized text owned upstream in Hexalith.AI.Tools; route the fix there and re-propagate, do not edit the three entry points unilaterally.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:21,208` attributes the domain-centric rule to a CLAUDE.md "Domain-Module Authoring" section that does not exist (the rule lives in `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`); pre-existing at baseline `a9718a21`, the guardrail itself enforces the rule on code and is unaffected — fix the citation next time the test file is opened.
- The Story 1.20 exact-membership literal `["jpiquot"]`×4 now exists in four synchronized places (both packet validators, spec Verification command 2, the allowlist itself), reducing the retained key-set/non-empty/uniqueness predicates to dead code, and the evidence-commit-A validator still lacks the login-format regex the candidate-gate validator carries. Consolidate (single source or documented sync list) at the next approved gate-logic change, together with the ledgered regex tightening above.

## Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)

- Commit `01830544` ("fix: add commit message validation requirement with commitlint") is misdescribed and mixed: 377 of its 378 lines are the unrelated Story 3.5 artifact `3-5-shared-package-catalog-and-source-package-reference-modes.md`, and its 1-line Copilot entry-point edit is what desynchronized the three shared entry points (see the loop-4 Decision items in the spec). Third mixed-bundle recurrence recorded by this spec's reviews; history is already on `main`, so no rewrite — note it when bisecting across 2026-07-18.
- Story 3.5's dependency-mode truth table has no row for build configurations other than Debug/Release (e.g. `Staging`, case variants) with `UseHexalithProjectReferences` unset, and its required test list omits the case — implementers may choose either reference-graph edge with no specified expectation. Owned by Story 3.5's active cycle; route into its review, do not patch from a 1.20 review. [`_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md`]
- Story 3.5's contract does not define precedence when explicit `UseNuGetDeps` and explicit `UseHexalithProjectReferences` conflict ("preserve its existing mapping" vs "normalize … one authoritative boolean" with no truth-table row, AC, or test naming the winner) — contradictory caller properties could activate both or neither reference edge. Owned by Story 3.5's active cycle. [`_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md`]
- The seven root-submodule source bumps ratified into `ea6ce49b` are compile-verified only: the per-project unit-test CI runs in package mode (`UseHexalithProjectReferences` defaults false), and the only source-mode lane is the filtered `tenants-source-mode` launch-settings job — a behavioral regression in bumped Commons/FrontComposer/PolymorphicSerializations/Tenants source that still compiles leaves all CI green. Consider a periodic/advisory source-mode lane running a representative unit-test subset, or record source-mode validation evidence in the ratifying artifact; CI-lane design belongs with Story 3.5's dual-mode validation scope.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md`
  summary: Pin `commitlint.config.mjs` to LF in `.gitattributes` or make its exact-content contract line-ending agnostic.
  evidence: The existing contract compares LF bytes while `.gitattributes` leaves the config under `text=auto`; a Windows or `core.autocrlf=true` checkout can materialize CRLF and fail independently of policy content.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md`
  summary: Add durable process-level commitlint behavior fixtures for valid, subject-case, header-length, and body-line policies.
  evidence: The existing Contracts test pins the three-line config text but does not execute commitlint, so a future grouped `@commitlint/*` update could change delegated defaults without the regression guard detecting it.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md`
  summary: Reconcile the documented literal lowercase-start rule with commitlint's weaker default `subject-case` behavior.
  evidence: The restored default rejects `fix: Update status` but accepts descriptions beginning with digits or symbols before uppercase text, despite shared guidance requiring the description to start with a lowercase letter.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md`
  summary: RESOLVED 2026-07-20 — the repository excludes `chore` and uses specific non-release types, including `build(deps)` for automated dependency maintenance.
  evidence: `commitlint.config.mjs`, `CONTRIBUTING.md`, project context, and Dependabot prefixes now agree with the shared Git instruction that prohibits `chore`.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd-2.md`
  summary: Add an unoverridden Release property test that binds `Version` and `PackageVersion` to the repository release version.
  evidence: The external `Hexalith.Builds` gitlink change updates `HexalithEventStoreVersion` from 3.74.0 to 3.75.0, while current tests do not assert the default evaluated version and CI package validation overrides it explicitly, allowing a stale catalog value to pass.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29720431798-fix-ci-cd.md`
  summary: Correct the nonexistent baseline SHA recorded by the Story 4.8 implementation artifact.
  evidence: `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md:2` records `afcc167ef277...`, while the valid baseline is `afcc167e0c539b09ecad978a58da2f756123f34e`; this originated in commit `73140382` and is unrelated to the CI gitlink repair.

## Deferred from: idempotency result-payload gating CI fix (2026-07-20)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md`
  summary: `idempotency-conflict` and `idempotency-key-expired` error-catalog entries in `ErrorReferenceEndpoints.ErrorModels` omit `detail`/`reasonCode` example fields that their real exception handlers (`IdempotencyConflictExceptionHandler`, `IdempotencyKeyExpiredExceptionHandler`) actually set.
  evidence: Adversarial review of the CI fix -- pre-existing gap unrelated to today's regression; the new `idempotency-admission-failure` entry added by this fix includes both fields (matching its handler), highlighting the sibling entries' inconsistency.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md`
  summary: No test verifies `ErrorReferenceEndpoints.ErrorModels` example content (status code, fields) against what each real `IExceptionHandler` actually emits at runtime -- only slug presence/absence is asserted.
  evidence: Adversarial review of the CI fix -- `AllProblemTypeUris_HaveCorrespondingErrorModel` / `AllErrorModels_HaveCorrespondingProblemTypeUri` would not have caught this fix's own status-code simplification (503 documented as primary while `idempotency_outcome_unknown` returns 409), a class of drift the catalog exists to prevent.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md`
  summary: `SubmitCommandResult` has no field distinguishing why `ResultPayload` is null (still in-flight, domain-rejected, or a durable non-retryable `PublishFailed`) -- all three look identical to callers from the response body alone.
  evidence: Adversarial review of the CI fix -- pre-existing API design gap in the exact code path this fix restores gating for; not caused by this change.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md`
  summary: `docs/reference/command-api.md` § "Stable Idempotency Outcomes" and `ErrorReferenceEndpoints.ErrorModels` are two independently maintained sources of truth for the same idempotency-admission failure taxonomy, with no cross-link between them.
  evidence: Adversarial review of the CI fix -- introduced when today's `19465ef8` commit added `ProblemTypeUris.IdempotencyAdmissionFailure` and the docs table without updating the error catalog; this fix closes the catalog gap but does not unify the two sources.

## Deferred from: code review of spec-gh-29740868410-fix-ci-cd.md (2026-07-20)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29740868410-fix-ci-cd.md`
  summary: No unit test asserts that AggregateActor's several `ResultPayloadWithheld` formulas (`CreatePublishFailedResult` and the terminal-completion/concurrency-conflict paths) produce the correct value for each terminal branch.
  evidence: PR #319 (`6945714b`) made `CommandProcessingResult.ResultPayloadWithheld` the sole authority for whether `SubmitCommandHandler` returns a command's result payload, but only the consumer side (`SubmitCommandHandlerResultPayloadTests`) has coverage; the producer side in `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (around lines 2018, 2264, 2297, 2358) has no test proving its withheld formula is correct per branch, so a regression there would go undetected by any current suite.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29740868410-fix-ci-cd.md`
  summary: `SubmitCommandHandler.Log.ResultPayloadDropped`'s message text ("...because final command status was not Completed...") is stale under the flag-driven withholding logic and can be logged even when the reported `FinalStatus` is `Completed`.
  evidence: PR #319 (`6945714b`) changed the drop decision from `finalStatus?.Status == CommandStatus.Completed` to `!processingResult.ResultPayloadWithheld` (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:538`) but left the EventId=1107 log message template (line 777) referencing the old status-based reasoning, so the warning can misstate why the payload was withheld. NOTE: superseded 2026-07-20 by the idempotency result-payload gating fix above, which reverted the drop decision back to the durable status-store read -- this item no longer applies to current code but is kept for historical trace.

## Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)

- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [MEDIUM] Fail-closed publisher/validator/authority/smoke suite is not a PR/required check -- `Tools/test-publish-containers.ps1` runs only in Hexalith.Builds `build-release.yml` (push-to-main at reviewed SHA `9ec0a032`; `workflow_dispatch`-only, i.e. worse, at current HEAD); no `pull_request`-triggered Builds workflow runs it.
  evidence: Story 3.12 code review (verification-gap layer) -- a PR to Hexalith.Builds that inverts `_validate_platforms`, drops the expiry check, or loosens owner-role validation merges green because the suite is not in PR CI; the regression surfaces only on a later push/dispatch, after merge. Owned by the Hexalith.Builds maintainer. Persists at live HEAD.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [MEDIUM] Unbounded registry response read in `RegistryClient._get` (`oci_registry_validator.py:420 response.read()`) has no size cap, unlike the 256/128 KiB caps in the authority-URL fetch -- memory-exhaustion vector from a hostile/malfunctioning registry response or config blob.
  evidence: Story 3.12 code review (blind-hunter + edge-case-hunter). Verified still present at live HEAD (submodule `dfb2f3fd`). Defense-in-depth: the Zot registry is authenticated, but the asymmetry with the authority fetch shows the cap was considered and omitted here. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [MEDIUM] No negative test proves a failing OCI validator or smoke aborts the publish -- gating relies entirely on `set -euo pipefail` in `publish-containers.sh` (present and functional), but every test runs the script with passing fake validate/smoke executables.
  evidence: Story 3.12 code review (verification-gap layer; edge-case-hunter confirmed pipefail is present). A regression changing `"$validator" ...` to `... || true`, capturing status in `$(...)`, or backgrounding it would publish a single-platform/digest-mismatched/dead-on-arm64 image with exit 0, and no current test observes the lost gating. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [MEDIUM] Validator->smoke evidence schema contract is untested -- `oci_registry_validator.write_evidence` (producer) and `smoke_container_platforms._load_children` (consumer) are each asserted only against their own hand-written `oci-validation.json` fixtures.
  evidence: Story 3.12 code review (verification-gap layer). Renaming a child key (e.g. `digest`->`child_digest`) or changing the `platforms` shape on either side leaves both suites green while the real release breaks at smoke -- or silently skips a platform if the loader is simultaneously loosened. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [LOW] The Builds-identity gate is behaviorally tested only for the SHA-mismatch branch; the repository-identity, authority-URL, and owner-allowlist branches in `domain-release.yml` are only substring-asserted, not provoked with negative env permutations.
  evidence: Story 3.12 code review (verification-gap layer). Lower priority: the gate is production-proven working (v3.77.2 run 29694935552 step succeeded), so this is defense against a future logic regression in the repo comparison, not a current defect. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [LOW] Redirect `Location` with an invalid or out-of-range port (e.g. `:99999`, `:abc`) makes `parsed.port` raise `ValueError` inside the redirect handler, which is not caught (only `URLError`/`TimeoutError` are) -- validator aborts with a raw traceback instead of a clean `unresolved-*` failure.
  evidence: Story 3.12 code review (edge-case-hunter) [`oci_registry_validator.py:41-71`]. Still fail-closed (aborts), but not the support-safe deterministic reason code the design advertises. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [LOW] Several reachable fail-closed reason codes lack negative fixtures: `wrong-schema-version`, `wrong-child-schema-version`, `child-media-type-mismatch`, `unsupported-child/config-media-type`, `malformed-config-descriptor`, `config-digest-mismatch`, and the immutable-side `index-content-type-mismatch` / `immutable_body != tag_body` branches.
  evidence: Story 3.12 code review (verification-gap layer). Defense-in-depth for a supply-chain path; the headline reason codes are covered, these branches are not independently provoked. Owned by the Hexalith.Builds maintainer.
- source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
  summary: [LOW] Non-domain exceptions (OSError/TypeError from evidence/log file I/O, `path.read_bytes()`) can escape the `main()` handlers that catch only `ValidationError`/`AuthorityError`/`SmokeFailure`, emitting raw tracebacks; and support-safe log redaction (`_support_safe`) misses JSON-shaped secrets (`"password": "..."`) with 30-day evidence-artifact retention.
  evidence: Story 3.12 code review (blind-hunter). Both still fail-closed (exit != 0) and low-exposure today (smoke container carries only the non-secret JWT key), but neither matches the "deterministic support-safe" contract. Owned by the Hexalith.Builds maintainer.

## Deferred from: Story 2.10 Tier 1 logging regression unblock (2026-07-21)

- source_spec: `_bmad-output/implementation-artifacts/spec-2-10-unblock-server-logging-regressions.md`
  summary: Make `InformationLevelOnly_TracingChainStillComplete` exercise an actor logger that actually disables Debug logging instead of capturing every level and filtering the resulting list afterward.
  evidence: Review confirmed the pre-existing test's `TestLogger<T>.IsEnabled` always returns true, so it cannot detect incorrect runtime gating through `IsEnabled(Debug)` even though its post-capture assertions exclude Debug entries; this limitation predates and is not caused by the pooled-state capture correction.

## Deferred from: code review of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-22, runtime/unit chunk)

- source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
  summary: Validate `DomainServiceOptions.MaxEventsPerResult` and `MaxEventSizeBytes` bounds during startup instead of allowing zero or negative limits to reject otherwise valid responses at invocation time.
  evidence: The fields and response-limit behavior predate this review range. The new startup validation at `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:107` covers only `InvocationTimeoutSeconds`; adding the two existing bounds is real hardening but is not caused by the Story 1.20 corrective change.

## Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)

- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [MEDIUM] `InboundBearerForwardingHandler` appends `Authorization` with a bare `TryAddWithoutValidation` and no preceding `Headers.Remove(...)` — the exact append-not-replace anti-pattern AD-18 exists to eliminate, sitting in the same outbound handler chain as the platform handler that deliberately does Remove-then-add. It also coerces the multi-valued `Request.Headers.Authorization` (`StringValues`) to `string?`, so a caller sending two `Authorization` headers is forwarded as one comma-joined value.
  evidence: Story 2.5 code review (blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/InboundBearerForwardingHandler.cs:14`], contrast `src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs:12-17`. The file is untouched by patch commit `846f988a` and the Api host sets no default `Authorization` on its gateway client today, so this is latent, not live; it becomes live the moment anything configures a default `Authorization` (as the sibling UI host already does via `AddFrontComposerGatewayAuthorization()`). The only test for this handler covers the single-value case. Owned by the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [LOW] `DAPR_API_TOKEN` is read raw with no trim or normalization, so a mounted secret carrying a trailing newline, or a whitespace-only value, is forwarded verbatim to the sidecar; the platform handler's `apiToken is { Length: > 0 }` test passes for `" "`.
  evidence: Story 2.5 code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:69`]. Pre-existing: identical to the deleted `DaprAppIdHandler`'s `!string.IsNullOrEmpty` behavior, so not caused by this patch. Owned by the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [LOW] The DAPR API token is captured by value at registration time, so a token rotated at runtime (secret remount, config reload) stays stale until process restart; an `IOptionsMonitor`-based handler factory would pick up rotations.
  evidence: Story 2.5 code review (edge-case-hunter) [`src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:70`]. Pre-existing platform design, unchanged by this patch and identical to the deleted host-local handler. Owned by Hexalith.EventStore.
- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [LOW] `DaprServiceInvocationExtension_ReplacesUntrustedRoutingHeaders` builds a synthetic named client `"dapr"` that no Tenants production code registers, so it exercises zero Tenants code and only re-tests EventStore platform behavior; the rename traded the Tenants suite's only test of Tenants-owned outbound routing for a third copy of a platform test.
  evidence: Story 2.5 code review (blind-hunter + verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs:134`]. Near-duplicate of `tests/Hexalith.EventStore.Client.Tests/Registration/DaprServiceInvocationRegistrationTests.cs` and `.../Handlers/DaprServiceInvocationHandlerTests.cs`, whose upstream versions are stronger (they inject the conflicting value through an outer handler — the actual AD-18 threat — rather than host-owned `DefaultRequestHeaders`). Test-quality only; the real-chain assertions survive in the sibling test at the same file. Owned by the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [MEDIUM] AD-18 is opt-in and fail-open at the platform seam — `AddEventStoreGatewayClient` registers no routing-header handler, so a host that omits the separate `AddEventStoreDaprServiceInvocation` call silently gets no `dapr-app-id`/`dapr-api-token` ownership with no compile-time error, no startup validation and no runtime diagnostic. Same fail-open shape as the `ApiScope` trap already on record. Harden the platform so the seam fails closed (or emits a startup diagnostic) rather than relying on per-host convention plus source-text guard tests.
  evidence: Story 2.5 code review (blind-hunter + acceptance-auditor) [`src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:43-48` registers only `ICommandStatusLocationBuilder` and the typed client; the handler is added exclusively at line 63]. Owner decision 2026-07-26: out of Story 2.5 scope (that story reviews the Tenants host boundary, not platform design) — carry as a dedicated Hexalith.EventStore platform hardening story. Note `project-context.md:46` currently misstates this wiring as "wired by `AddEventStoreGatewayClient`"; that text is corrected under Story 2.5.
- source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
  summary: [LOW] `SampleApiLaunchSettingsTests.ExtractBlock` matches an LF-only marker (`";\n\nif (security is not null)"`) against `src/Hexalith.EventStore.AppHost/Program.cs`, so the test fails on any working tree where that file is checked out or rewritten with CRLF line endings. Make the marker line-ending agnostic (normalize the text, or match on a CRLF-tolerant pattern) the way the sibling `TenantsApiLaunchSettingsTests` does with its `#endif` marker.
  evidence: Discovered during Story 2.5 review-loop verification (2026-07-26), NOT caused by that story — it concerns sample-api, not tenants-api. `dotnet test tests/Hexalith.EventStore.AppHost.Tests/...` = 50/51, the single failure being `AppHost_RegistersSampleApiAsExternalServiceInvocationOnlyHost` at `SampleApiLaunchSettingsTests.cs:41` via `ExtractBlock` at `:93` ("Expected sample-api resource registration assignment before the security block"). Root cause confirmed: `file src/Hexalith.EventStore.AppHost/Program.cs` reports CRLF terminators on disk while `git show HEAD:...` is LF, so `git status` reads clean (`* text=auto` normalizes on compare) and `IndexOf` returns -1. A fresh LF checkout (CI) passes; a CRLF working tree fails. The Tenants-specific lane in the same project passes 4/4. Owned by Hexalith.EventStore.

## Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26)

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] A `304` carrying `Lifecycle: Degraded` renders a normal `Ready` surface, while the identical evidence on a `200` renders `Degraded` — the same authoritative "projection degraded" claim produces two different user-visible surfaces depending only on cache validation.
  evidence: Story 2.6 code review (blind-hunter + edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1169`]. On 200 the five call sites return `*.Degraded(...)` on `IsDegraded == true`; on 304 `ResolveNotModifiedFreshness` routes into `ResolveFreshness`, which returns `Unknown`, and every `Resolve*KindForFreshness` mapping collapses non-`Stale` to `Ready`/`Empty`. Pre-existing: the pre-diff predicate `metadata?.IsDegraded == true || metadata?.IsStale is not null` also routed a degraded 304 into `ResolveFreshness` for the same `Unknown` result, so the diff does not cause it. Reachable through the shipped client, which maps a degraded 304 to `IsDegraded = true` at `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:154-166`. Newly relevant because the diff added a theory that pins `Degraded -> TenantDetailSurfaceKind.Ready` as expected behaviour. Owned by the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [LOW] `IsTenantManagementApiRoute` hardcodes the `api/tenants`, `api/users`, and `api/global-administrators` prefixes with no shared constant or link to the attribute that declares them, so the guard goes blind if the REST base changes.
  evidence: Story 2.6 code review (blind-hunter + edge-case-hunter + verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:559`]. Verified correct for today's surface: all 17 `[RestRoute]` templates in `src/Hexalith.Tenants.Contracts` hang off `[assembly: RestApi("api/tenants", ...)]` at `src/Hexalith.Tenants.Api/RestApiAssemblyInfo.cs:5`, plus absolute `~/api/users/{userId}/tenants` and `~/api/global-administrators`. The prefix is an attribute argument, so a change there silently defeats the matcher. Low impact because the preceding `ControllerActionDescriptor` check already catches every generated controller — the route matcher only adds value against hand-written minimal APIs, and shapes like `api/v1/tenants` or `api/tenant-configuration/...` escape it regardless. Owned by the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [LOW] Deleting the UI source-marker scan leaves marker enforcement for the Tenants UI host running only in the EventStore repository's test suite, so the Tenants repo's own CI can no longer enforce it standalone.
  evidence: Story 2.6 code review (blind-hunter + edge-case-hunter + verification-gap + acceptance-auditor). The diff removed the `forbiddenMarkers` array from `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`. Mitigation verified: `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:119-135` retains every deleted marker plus `AddMvc(`/`AddMvcCore(`, and scans `references/Hexalith.Tenants/src/Hexalith.Tenants.UI` at line 621. Residual gap: within the Tenants repo alone, `builder.Services.AddControllers()` plus `app.MapControllers()` with no controller type declared passes all three replacement tests (no controller types to reflect over, no `ControllerActionDescriptor` endpoints to enumerate). Split ownership between Hexalith.EventStore and the Hexalith.Tenants maintainer.
- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [LOW] Two guardrail tests were promoted into behaviours that no longer fit the Tier-1 unit tier — one boots a full Blazor Server host via `WebApplicationFactory`, the other spawns two `dotnet msbuild` child processes — inside a project the Tenants CI runs in its unit lane.
  evidence: Story 2.6 code review (verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224` and `:500`]. `tests/Hexalith.Tenants.UI.Tests` is listed in the Tenants `.github/workflows/ci.yml` Tier-1 unit list, while every other `WebApplicationFactory` usage in that repository lives in `tests/Hexalith.Tenants.IntegrationTests`. The unit tier now depends on an installed SDK on `PATH`, a valid restore, and the exact five-levels-up `ProjectRoot()` layout at test runtime. Owned by the Hexalith.Tenants maintainer.

## Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, D3 owner decision)

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [RESOLVED 2026-07-27] Platform prerequisite — preserve `ReadModelFreshnessState` as the independent threshold/age view and introduce the existing `ProjectionLifecycleState` alongside it in consumer UI snapshots/rows, so `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` receive distinct canonical treatment without corrupting freshness semantics.
  evidence: The 2026-07-27 Story 2.6 owner decision superseded D3 and kept AC3 unchanged. The Tenants working-tree patch threads `ProjectionLifecycleState` through all five gateway/UI surfaces, localizes and renders the four operational states in `TruthStateBadge`, and pins their icon/label/color semantics plus fail-closed lifecycle-action remediation. Focused badge/action tests pass 31/31 and the full UI suite passes 1090/1090. The separate D6 `Aging` reachability and mutation-gate item remains open below; this resolved entry covers only operational lifecycle representation.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-30204107907-fix-ci-cd.md`
  summary: [MEDIUM] Active CI operator documentation still names the superseded Hexalith.Builds release SHA and is not guarded against drifting from the immutable release pin.
  evidence: Verification-gap review found `docs/ci.md:158` and `docs/ci-secrets-checklist.md:54` still name `cf04c419378dfe1bd3c41a9244b5e3283092056e`, while `.github/workflows/release.yml` and the repaired governance test authorize `f75daebd4c522c081a6f62e274cf25e07971de69`. `DocumentationAndContainerDefaultsDescribeTheExactReleaseContract` reads both documents but does not bind either to the approved SHA. Updating those documents is real follow-up work but outside this spec's frozen two-test-file boundary.

## Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)

Scope note: Story 2.6's production change is 29 lines (provenance gate + lifecycle precedence in
`ResolveFreshness`/`ResolveNotModifiedFreshness`, plus four `IsStale` -> `freshness is Stale` call-site
rewrites). The entries below were found while reviewing the *current state* of the four File List files at
Tenants `42f0c5c` and are **not** caused by that change; they arrived from the support-safe copy, tenant
configuration, cursor paging, and Memories search stories. All are owned by the Hexalith.Tenants maintainer
unless stated otherwise.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [HIGH] `EnrichRowsAsync` publishes member/owner counts as `TenantCountValue.Known(...)` from a detail payload that `LoadTenantDetailAsync` returns raw — no tenant-identity check, no `Members` null guard, no `IsDegraded` check. A detail projection returning the wrong tenant attributes another tenant's member and owner counts to the row; a payload omitting `members` throws `NullReferenceException` that escapes the gateway into the Blazor render.
  evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter + verification-gap, independently) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1009-1022` and `:1028-1034`]. The sibling search path guards all three cases at `:715-722` (`!string.Equals(detail.TenantId, candidate.TenantId, StringComparison.Ordinal) || detail.Name is null || detail.Members is null`), which is direct evidence the guards are considered necessary. `TenantDetail.Members` is a positional `IReadOnlyList<TenantMember>` with no null validation (`src/Hexalith.Tenants.Contracts/Queries/TenantDetail.cs:13`), so `{"members": null}` deserializes to null. The NRE escape path is confirmed: `EnrichRowsAsync` catches only `EventStoreGatewayException` (`:1018`), `ListByCursorAsync` likewise (`:849`), and `ListTenantsAsync` returns from the non-search path at `:500` outside its `try`. No test feeds a mismatched or null-member detail into the enrichment loop.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [HIGH] A single row's detail-enrichment failure with any status outside `{403,404,503}` unwinds past the already-successful list fetch and maps the whole page to `TenantListSurfaceKind.Error`. The most reachable trigger is the client's own `EventStoreGatewayException(200, "Query response did not contain a payload.")`.
  evidence: Story 2.6 second-pass code review (edge-case-hunter + blind-hunter + verification-gap) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1024-1026`]. `EventStoreGatewayClient.SubmitQueryAsync<T>` throws status-200 gateway exceptions at `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:223-247` and `:186-192`, and a malformed 304 raises 502 at `:148-152` — none match the `when` filter. The parallel search path degrades correctly instead, accepting any `EventStoreGatewayException` via `IsHydrationAvailabilityFailure` (`:812`). Related: even within the filter, one routine `404` (tenant deleted between the index and detail projections) forces the whole surface to `Freshness = Unknown`, which `TenantLifecycleAvailabilityInput.Evaluate` (`State/TenantDetail/TenantLifecycleAvailability.cs:42`) treats as blocking for every lifecycle operation. Only `403` is tested (`TenantQueryGatewayTests.cs:1373`).

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] Non-`EventStoreGatewayException` failures escape four of the six public read methods. `GetTenantAsync` and `GetConfigurationProjectionProofAsync` have a generic `catch (Exception)`; the user-tenants, global-admins, audit and list paths do not, so an unhandled exception crashes the interactive Blazor circuit instead of degrading.
  evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter + verification-gap) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:283`, `:368`, `:391`, `:405`, `:849` versus `:118`+`:134`]. Two concrete reachable triggers: (1) `HttpContent.ReadFromJsonAsync` throws `NotSupportedException` on a non-JSON `Content-Type` — the common shape when an auth redirect or reverse proxy answers `200 text/html` — and `EventStoreGatewayClient.ReadQueryResponseAsync` wraps only `JsonException` (`:455-470`) while `SendTranslatingAsync` translates only `HttpRequestException`/`TaskCanceledException` (`:291-303`); (2) `PaginatedResult<T>` is a positional record with no null validation (`PaginatedResult.cs:6`), so a `{"items": null}` body dereferences null at `:244`, `:338`, `:451`. `GlobalAdministratorsPage.LoadAsync` (`:674-687`) and `MyTenantsPanel.LoadAsync` have no try/catch of their own.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] Three further 304 evidence asymmetries beyond the `Degraded` one already on record: a 304 carrying no lifecycle evidence re-affirms the previous claim (so `Current` survives indefinitely where the identical 200 yields `Unknown`); per-row `Freshness` is never rewritten on any 304 branch, so row badges contradict the surface banner; and a `Degraded` surface cannot be cleared by refreshing because the server ETag is content-derived and does not change when only lifecycle recovers.
  evidence: Story 2.6 second-pass code review (edge-case-hunter + blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163-1173`, the four 304 branches at `:232-244`, `:313-325`, `:429-435`, `:872-886`, and `:1270-1272`]. Confirmed pre-existing: the pre-2.6 predicate `metadata?.IsDegraded == true || metadata?.IsStale is not null ? ResolveFreshness(metadata) : previous` produced the same carry-forward and the same 200/304 divergence; Story 2.6 widened the trigger set with the lifecycle clause but did not introduce the shape. The 200 paths rewrite rows explicitly (`:262`, `:353`, `:467`) while the 304 branches use `previous with { ... }`, which copies `Rows` by reference; both `TenantDataGrid.razor:76` and `GlobalAdministratorsPage.razor:346` bind the per-row value. Server ETag is per-projection, not per-response-state (`QueriesController.cs:113-115`).
  status: RESOLVED 2026-07-30 by the Story 2.6 fourth-pass review. Retained rows were already rewritten in the 2026-07-27 patch; all retained-snapshot lifecycle resolution now consumes only explicit, valid `304` lifecycle evidence and otherwise fails closed to `Unknown`, so neither `Current` nor `Degraded` is sticky. A gateway regression proves a projection-backed lifecycle-less `304` cannot inherit the previous value.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] `ResolveTenantListKindForFreshness` lacks the surface-kind whitelist its three siblings open with, so an `Error`, `Unauthorized` or `Loading` previous falls through to `previous.Rows.Count == 0 ? Empty : Ready` — the surface would assert "you have no tenants" on top of a failed or denied read.
  evidence: Story 2.6 second-pass code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1267-1283` versus `:1185`, `:1207`, `:1229`]. Currently unreachable because `TenantListSnapshot.Error()` and `.Unauthorized()` both set `ETag = null` (`State/TenantList/TenantListSnapshot.cs:73-94`) so no 304 can follow, but the resolver itself carries no guard and the gateway is the reusable seam. Latent rather than live.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] `ReadModelFreshnessState.Aging` is absent from the mutation-blocking set. `TenantLifecycleAvailabilityInput.Evaluate` blocks on `Freshness is Stale or Unknown`, so once the deferred persisted-projection-age work makes `Aging` reachable, aging read-model evidence will silently *permit* tenant enable/disable mutations — the opposite of the AD-15 posture applied to `Unknown` and `Stale`.
  evidence: Story 2.6 second-pass code review (blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs:42`]. `ResolveFreshness` cannot currently emit `Aging` (`ProjectionLifecycleState` has no `Aging` member and the switch maps everything unmatched to `Unknown`), yet `Aging` branches already exist in `Components/Shared/TruthStateBadge.razor:37,45` with tests at `Components/TruthStateBadgeTests.cs:21` and `State/TenantLifecycleAvailabilityTests.cs:62`. Directly coupled to the existing D6 read-model freshness handoff and to the D3 platform deferral recorded above — close this together with them. Split ownership between Hexalith.EventStore (freshness model) and the Hexalith.Tenants maintainer (gate).

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [LOW] A search term containing any control character is silently nulled by `CanonicalizeListRequest`, so the gateway returns an unfiltered full list with `Notice = None` while the user's search box still shows their query — the grid renders every tenant on the page as if it matched.
  evidence: Story 2.6 second-pass code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:550-552`, taken with the plain cursor branch at `:499-501`]. `TenantsWorkspace.ApplyVisibleRows` (`TenantsWorkspace.razor:611-640`) applies only status and sort client-side, never the search term. Every other search failure sets `TenantListReason.SearchUnavailable` via `FallBackFromSearchAsync:769-773`, a notice the UI does render (`TenantsWorkspace.razor:376`) — this path should do the same.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [LOW] Test-quality cluster in `TenantQueryGatewayTests.cs`, all pre-existing: two tests pass only because the stub's `Queue<object>` underflows (`InvalidOperationException` is absorbed by a catch-all, and is separately on `IsSearchAvailabilityFailure`'s swallow list, so any under-enqueued search test silently asserts the fallback path); the fixture string `"index-only content that must never render"` is asserted nowhere; `TenantDetailSnapshot.ErrorMessage` has zero consumers in the UI so its sanitization assertions run against a tenant id; the French resource values `"Administrateurs globaux charges"` and `"Donnees d'administrateurs globaux perimees"` are missing accents and the test pins the wrong expected value; and two composition tests couple CI to documentation prose and to one exact workflow substring.
  evidence: Story 2.6 second-pass code review (blind-hunter, corroborated by verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:501`, `:2074`, `:2092`, `:2223`; `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Resources/TenantsResources.fr.resx:2617` and `:2623`; `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:357` and `:472`]. The accent defect is locked in: fixing the resx now breaks the test.

## Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass — routed to Story 2.11)

- source_spec: `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md`
  summary: [HIGH] Mutation-gate fail-open in the ratified 2.11 consumer logic — the tenant correction surface enables a mutation that `ProjectionLifecyclePolicy.CanMutate` denies. `CanMutate` requires `provenance == ProjectionBacked && lifecycle == Current`, but `ResolveFreshness` also returns `Current` on the legacy fall-through (`Lifecycle == Unknown` with `IsStale == false`). `TenantCorrectionStartIntent` gates only on `Freshness is Current`, so a producer emitting no lifecycle header but legacy `IsStale: false` unlocks a correction the platform policy forbids.
  evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter), mechanism verified against source 2026-07-26. `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs:83` (`CanMutate` = `isAuthorized && IsProjectionConfirmed(provenance, lifecycle)`, and `IsProjectionConfirmed` requires `lifecycle == Current`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1156-1160` (legacy `IsStale switch { false => Current }`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs:88`. Reachable through the shipped producer: `QueriesController.cs:136-139` omits the lifecycle header entirely when `Lifecycle == Unknown`. NOT fixable as a mechanical patch — `TenantAuditRow` carries neither `Provenance` nor `Lifecycle`, so the intent cannot call `CanMutate`; closing it needs a design choice (widen the row, add a gateway-computed `CanMutate` flag, or stop deriving `Current` from legacy evidence). Story 2.6's ratified-overlap section assigns this defect to Story 2.11 and requires resolution before 2.11 leaves `review`. Owned by Story 2.11 / the Hexalith.Tenants maintainer.
  status: RESOLVED 2026-07-27 by Story 2.11, Tenants `5eed7a97b87988e2f1e286a0483490ca7ef75d2b`, contained by the maintainer-authored and published merge `d2e5a1211f469041fdc593fd4e4678755f6863c8`; the EventStore gitlink pinned that merge at acceptance. The "not fixable as a mechanical patch" note was superseded by Tenants `55e6000`, which added `Lifecycle` to `TenantAuditRow`; the chosen option was widening the row. `TenantAuditRow` now also carries `Provenance` (failing closed to `Unknown` for absent or out-of-range values), set on the 200, `304`, and missing-payload audit paths, and `TenantCorrectionStartIntent.Evaluate:88` requires `ProjectionLifecyclePolicy.IsProjectionConfirmed(Provenance, Lifecycle)` in addition to `Freshness is Current`. The exhaustive one-way invariant proves that an available intent always satisfies `CanMutate`; correction-specific checks may still deny a platform-eligible mutation. Evidence: fail-closed cases for the legacy `IsStale == false` fall-through, missing/`HandlerComputed`/`Unknown`/invalid provenance, all five non-`Current` lifecycles, complete 200/304 row transport, missing-payload reset, and an untrusted-evidence component regression. Review-patch UI evidence is 292/292 focused and 1226/1226 full-suite, with Release UI/integration builds clean and the Tier-3 persisted consumer path 1/1. The broader mutation-gate follow-up was resolved on 2026-07-30 by the owner-approved Story 2.6 scope expansion.

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] Local build environment hazard, not a code defect — package-mode builds on this workstation block indefinitely on an interactive NuGet credential prompt, and orphaned MSBuild worker nodes accumulate until new builds wedge on reuse. Any unattended run that reaches package mode (bmad-loop dev session, local CI, release rehearsal) hangs silently and presents as "still running" rather than failing.
  evidence: Observed directly during the Story 2.6 second-pass review, 2026-07-26. A `dotnet build -c Release` of `Hexalith.Tenants.UI.Tests` accumulated 2h36m elapsed against 7 seconds of CPU, parked in `futex_wait_queue` with `NuGet.Credentials.dll` open; a separate `dotnet restore Hexalith.Builds.slnx --interactive` sat 5h06m in state `Sl+` (terminal foreground, awaiting input). 119 orphaned `MSBuild.dll --nodemode` processes were alive, load average 37.88; after clearing them the same test project built in 7.03s. Mitigations that worked: Debug source mode with `-p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false`. Note `-p:HexalithCommonsFromSource=false` is required — otherwise source-built `Hexalith.Commons.UniqueIds` 3.82.0 collides with the cached 2.28.2 package and the build fails `CS1704`. Suggested durable fixes: a non-interactive credential default (`NUGET_CREDENTIALPROVIDER_*`) and `-nodeReuse:false` in local build guidance. Owned by Hexalith.EventStore tooling.

## Deferred from: focused UX acceptance review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, Sally)

- source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
  summary: [MEDIUM] Four legacy Fluent v4 / FAST tokens survive in three Tenants UI stylesheets, so the "accent" callout treatment never tracks the active theme. `hexalith-ux-instructions.md` forbids `--accent-*` and `--neutral-foreground-*` outright — they belong to the previous major version and do not resolve under Fluent V5, so every occurrence falls through to its system-colour fallback and renders `LinkText` / `GrayText` in every theme with the intended accent silently absent. The UX instruction's own escape hatch requires these files to be tracked as an explicit, allowlisted migration backlog rather than silently exempted; this entry is that tracking. Migrate each to a Fluent 2 design token (or a Fluent primitive) and keep the `@media (forced-colors: active)` fallbacks.
  evidence: Story 2.6 focused UX acceptance review (Sally), verified against source at Tenants `11d6992`, 2026-07-26. Occurrences: `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Pages/TenantAuditPage.razor.css:20` (`var(--accent-stroke-rest, LinkText)`) and `:27` (`var(--neutral-foreground-hint, GrayText)`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Members/RemoveTenantMemberFlow.razor.css:22` and `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Configuration/SetTenantConfigurationFlow.razor.css:49` (both `var(--accent-fill-rest, LinkText)`). All four carry an `fc-css-exception` marker, but each marker justifies only the **layout** (border-inline-start + padding Fluent has no primitive for) — none declares or justifies the **token** choice, so `DomainUiFluentConformanceTests` passes them while the theme-tracking rule stays broken. NOT attributable to Story 2.6: neither the published `11d6992` change nor the 2026-07-27 lifecycle-presentation patch touches these three stylesheets; the lifecycle-state styles remain clean. Suggested durable fix: extend the conformance guard to reject legacy v4/FAST token names outright, so the marker cannot mask them. Owned by the Hexalith.Tenants maintainer.

## Deferred from: code review of 2-11-query-provenance-consumption-in-generated-rest-and-tenants (2026-07-27)

- source_spec: `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md`
  summary: [HIGH] Member, configuration, metadata, tenant-lifecycle, and global-administrator projection gates still accept `Freshness == Current` without requiring projection-confirmed lifecycle/provenance, so legacy-current/unknown-lifecycle responses can arm mutations outside the correction-start surface.
  evidence: Story 2.11 code review (verification-gap), confirmed against `MemberAccessReview.razor:391`, `EditTenantMetadataFlow.razor:233`, `TenantLifecycleCommandFlow.razor:200`, and `GlobalAdministratorCorrectionSnapshot.cs:293,369-371`. Pre-existing and explicitly outside the owner-confirmed correction-start scope; the story records the affected surfaces and says a separate scope decision is required.
  status: RESOLVED 2026-07-30 by the owner-approved Story 2.6 scope expansion. Existing member and global-administrator lifecycle gates remain fail-closed; configuration set/remove and metadata edit now consume lifecycle directly; lifecycle availability rejects `Unknown` and every other non-`Current` value and re-evaluates open flows. The full Tenants UI suite passes 1583/1583.

## Deferred from: code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [MEDIUM] No blocking CI job restores, builds, or tests the Debug/source dependency lane, so the source half of Story 2.12's new Gateway conditional is never evaluated by an automated gate. A broken or renamed EventStore source path in the conditional `ProjectReference` surfaces only when a developer runs source mode locally.
  evidence: Story 2.12 code review (verification-gap), verified 2026-07-28. `references/Hexalith.Tenants/Directory.Build.props:53-56` defaults `UseHexalithProjectReferences` to `false` in every unset case; `references/Hexalith.Builds/.github/workflows/domain-ci.yml:163/167/187/222` runs restore, build, and test without ever passing `-p:UseHexalithProjectReferences=true`; the only Tenants hit for that property outside tests is `scripts/publish-partial-release.sh:42`, which sets it `false`. The two tests that do evaluate both modes (`TenantsUiCompositionTests.cs:420`, `TenantsApiStructuralTests.cs:40`) target the UI and Api projects, neither of which has an edge to `src/Hexalith.Tenants`, and the latter runs in the aspire tier with `continue-on-error` defaulting to `true`. The new `PackageGovernanceTests` host rule does run blocking, but it is mode-independent XML text, so "green in both modes" carries no mode-specific information. Suggested durable fix: a `[Theory(true/false)]` over `src/Hexalith.Tenants/Hexalith.Tenants.csproj` asserting `type: project` in source mode and `type: package` in package mode, reusing the existing `ReadResolvedDependencyValuesAsync` helper. Owned by the Hexalith.Tenants maintainer.

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [MEDIUM] Nothing durably detects EventStore gitlink drift away from a validated commit, nor a wrong-but-resolvable `HexalithEventStoreVersion`. The amended AC2/AC3 identity gate exists only as hand-run scripts in a SHA-named evidence directory, invoked by no workflow and no test. The approved sprint change proposal placed the Tenants CI reachability check out of scope as a candidate follow-up; this entry is that tracking.
  evidence: Story 2.12 code review (verification-gap), verified 2026-07-28. A repo-wide grep for `ac2-guard`, `analyze-assets`, and `setup-lane` outside `evidence/story-2-12/578770679b9d…/` returns nothing. Tenants CI's only submodule handling is `Github/initialize-build/action.yml` → `git -c submodule.recurse=false submodule update --init`, which fails only when a gitlink is *unfetchable* — a commit on an unmerged feature branch initializes, restores, and passes every job while violating AC2's reachability requirement. On the producer side, `references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1:70-91` asserts only catalog membership and non-blankness and never queries a feed (`validate-central-package-versions.ps1` has zero `nuget.org` occurrences), so an unpublished version is caught only by breaking a downstream repository — which already happened once with `999.1.20-proof.fa2d1c9910f8` — and a published-but-wrong version is caught nowhere. The risk is not hypothetical: the umbrella gitlink left the accepted SHA within a day, inside this story's own final commit (see the corresponding `[Review][Decision]` item in the story). Suggested durable fixes: promote `ac2-guard.sh` into `references/Hexalith.Tenants/scripts/` and call it from a Tenants CI step; extend the Builds catalog test with a flat-container existence check reusing the pattern at `Github/publish-containers/publication_preflight.py:402`. Owned by the Hexalith.Tenants and Hexalith.Builds maintainers.

## Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [MEDIUM] The retained `578770679b9d…` lane logs were produced by the pre-fix `ac2-guard.sh` and are committed beside the post-fix script, with no marker distinguishing them. The guard's stdout format is unchanged by the 2026-07-28 hardening, so `logs/ac2-guard-{src,pkg}-lane.log` is byte-identical to what the corrected script would print; an auditor cannot tell "assertion 5 ran correctly" from "assertion 5 was a tautology".
  evidence: Story 2.12 delta code review (blind-hunter + verification-gap + acceptance-auditor), verified 2026-07-28. The receipt states the limitation in prose but the artifact pair carries no marker. `logs/pkg-assets.txt` is the honest counter-example — it lacks the new `invocation:` line, which does reveal its provenance. Deferred because the SHA is superseded by `f9e51c66…`, whose logs were produced by the corrected scripts. Suggested durable fix: have the lane scripts print their own sha256 into their output so a log always identifies the instrument that produced it.

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [LOW] `ac3-catalog.txt`'s line-oriented grep cannot fail for `Hexalith.EventStore.RestApi.Generators`, whose `Condition` sits on an XML continuation line, and no durable guard asserts `PackageReference` conditions outside the domain host.
  evidence: Story 2.12 delta code review (blind-hunter), verified 2026-07-28 at `logs/ac3-catalog.txt:81`. The condition genuinely exists in `src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj`, and the AC4 host rule reads effective conditions for `src/Hexalith.Tenants/Hexalith.Tenants.csproj`; but `No_EventStore_project_reference_is_reachable_in_package_mode` checks conditions only on `ProjectReference` items, checking `PackageReference` items solely for local version authority. Suggested durable fix: extend that rule to assert the complementary package condition on every owned project, not only the host.

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [LOW] `setup-lane.sh` hardcodes `REFS=/home/administrator/projects/hexalith` and its new safety allowlist confines destinations to `/home/*/tmp-story-2-12/*`, so the already-deferred "promote `ac2-guard.sh` into Tenants CI" item cannot be executed as written — the lane the guard depends on cannot be created by that CI without editing the script.
  evidence: Story 2.12 delta code review (blind-hunter), verified 2026-07-28 at `evidence/story-2-12/578770679b9d…/setup-lane.sh:8,29-32`. This under-specifies the drift-detector follow-up recorded in the 2026-07-28 entry above; scope the two together. Owned by the Hexalith.Tenants maintainer.

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [MEDIUM] Nothing in EventStore invokes `analyze-assets.py`, `ac2-guard.sh`, or `setup-lane.sh`, and not one of their fail-closed branches has ever executed. Every retained run terminates in `ASSETS_OK` / `AC2_GUARD_OK`, so the unknown-mode exit, the missing-expected-version exit, the vacuous-pass guard and both mode-specific zero-edge guards are unverified against a failing input.
  evidence: Story 2.12 delta code review (verification-gap + edge-case-hunter), verified 2026-07-28. A repo-wide grep for `analyze-assets`, `ac2-guard`, and `setup-lane` outside the `evidence/story-2-12/` directories returns only prose in the story file, the receipts, and this ledger — no workflow, test, or script invokes them. Same root cause as the drift-detector entry above. Suggested durable fix: drive the scripts from xUnit tests over small synthetic `project.assets.json` fixtures and deliberately-broken lanes, asserting exit codes — the pattern `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs` already uses for the AWK verifier.

- source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md`
  summary: [MEDIUM] The strengthened Story 2.12 AC4 guard has four bypasses. (1) `No_EventStore_project_reference_is_reachable_in_package_mode` iterates `GetOwnedProjectFiles`, which enumerates `*.csproj` only, while the sibling rule in the same file uses `GetPackageReferenceGovernanceFiles`, which also appends every `Directory.Build.*` — and `tests/Directory.Build.props` already declares 6 `PackageReference` items, so an ungated EventStore `ProjectReference` placed there gives every test project a live project edge in package mode with both tests still green. (2) The rule tests the effective condition with `string.Contains`, so `!('$(HexalithEventStoreFromSource)' == 'true')` and `'$(HexalithEventStoreFromSource)' == 'true' Or '$(Configuration)' == 'Release'` both satisfy it while remaining live in package mode. (3) `EventStoreReferences` matches on `Include` only, so an `Update=`-form `PackageReference` carrying a version is invisible to `HasLocalVersionAuthority`. (4) The rule accepts `UseHexalithProjectReferences == 'true'` as equivalent source intent, but `Directory.Build.props:60` sets `HexalithEventStoreFromSource` only when that property is true **and** the EventStore Contracts csproj exists, so a reference gated only on the former is live in a configuration where the complementary `PackageReference` is also live.
  evidence: Story 2.12 delta code review (blind-hunter + edge-case-hunter + verification-gap), verified 2026-07-28 against `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs:217,226-227,258-261,304-307` and `references/Hexalith.Tenants/Directory.Build.props:59-61`. Deferred by explicit owner decision on 2026-07-28: the fix lives in Hexalith.Tenants, and committing it would move Story 2.12's acceptance off `f9e51c66745557da4f267ab40f32294f2f27fae7` and re-trigger the AC5 maintainer-acceptance cycle for a third time; the guard as shipped is still a large improvement over the four-literal-name version it replaced. Suggested durable fix, smallest first: swap `GetOwnedProjectFiles` for `GetPackageReferenceGovernanceFiles` in the reachability rule, then parse the condition rather than substring-matching it, then extend `EventStoreReferences` to read `Update=` as the pre-existing rules at `:120` and `:1342` already do. Owned by the Hexalith.Tenants maintainer.

## Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [LOW] A tracked stale `.lscache` under `Server.Tests` still enumerates `Fixtures/DaprTestContainerCollection.cs` and `Fixtures/DaprTestContainerFixture.cs`, committed residue from the very re-tier this story certifies. The AC1 guard cannot see it.
  evidence: Story 3.1 closure code review (acceptance-auditor + blind-hunter), verified 2026-07-28 at `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj.lscache:185-187`. The file is tracked (`git ls-files` confirms) and last touched by `9bafe1af`, pre-split. `ReleasePackageManifestTests.cs:429-446` filters candidates to `.cs`/`.csproj` only, so the guard structurally cannot flag it; the story's AC1 evidence (filename search + `.cs` grep) has the same blind spot. Not compiled, so lane separation is unaffected. 34 `.lscache` files are tracked repo-wide, so removing one is a repo-wide convention decision rather than a story-scoped fix. Suggested durable fix: untrack `*.lscache` via `.gitignore`, or widen the guard's file filter.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [HIGH, FrontComposer-owned] The release-pin lockstep guard lives in a workflow that does not gate the release it protects. `CiGovernanceTests` carries `[Trait("Category","Governance")]`, which executes only in `quality.yml`; `release.yml` triggers on `workflow_run: workflows: [CI]` and neither needs nor triggers on Quality.
  evidence: Story 3.1 closure code review (verification-gap + edge-case-hunter), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs:438-482`, `.github/workflows/quality.yml:119`, `.github/workflows/release.yml:25-26`, and `ci.yml:32-35` (which explicitly excludes `Shell.Tests`). Demonstrated: at FrontComposer `78705260` the pin was `7708256e` while the gitlink was `79f82acc`; CI run `29804662443` = success, Quality run `29804662064` = failure with Gate 2b red. A release dispatched from that head would have been authorized by green CI while executing Builds actions from a different revision than the build inputs — split-brain. The guard also went red at the commit that introduced it (`48862e9a`, PR #74) and stayed red across two commits on main with nothing blocking either push. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: make the release trigger depend on the Quality lane, or move `CiGovernanceTests` into a project inside `ci.yml`'s `unit-test-projects`.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM, FrontComposer-owned] Nothing validates that a newly pinned reusable workflow's input contract matches the caller. All three lockstep assertions compare SHAs, never contracts, so a Builds bump that renames or adds a required input stays green until a real publication attempt fails.
  evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/.../CiGovernanceTests.cs:454-489`. No test opens `references/Hexalith.Builds/.github/workflows/domain-release.yml`, even though `quality.yml:39-40` initializes that submodule so the file is present. This particular bump is contract-safe — `domain-release.yml` is byte-identical between `7708256e` and `79f82acc` — but that is established by nothing in the repository. The guard's own comment claims it ties the pin to "a guaranteed-real, locally-present git commit", yet `git ls-tree HEAD` reads the gitlink without resolving the commit object. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: parse the submodule copy of `domain-release.yml` and diff its declared `inputs`/`secrets` against the caller's `with:`/`secrets:` blocks.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM] The `generated-api-smoke-preflight.sh` exit-code contract is recorded as AC6 evidence (exit 3 = `topology-not-running`) but is executed by no automated check in either repo. Its own shell validation exists and is never invoked.
  evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28. `grep -rn "generated-api-smoke-preflight" .github/workflows/` returns nothing. `scripts/tests/generated-api-smoke-preflight.test.sh` (Story 3.8 AC8) is referenced only by itself. `tests/Hexalith.EventStore.Testing.Integration.Tests/GeneratedApiSmokePreflightDiagnosticsTests.cs:16-102` asserts only redaction, output categories, message classification and port constants — it never executes the script and never asserts an exit code. Returning `1` instead of `3` would break every documented consumer with no test failing. Deferred rather than patched because AC7 forbids workflow changes in this story and the script is owned by Story 3.8. Suggested durable fix: run the existing `scripts/tests/generated-api-smoke-preflight.test.sh` as a step in `ci.yml`.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM, FrontComposer-owned] FrontComposer commit `b6efcad5` uses type `fix:`, so semantic-release will cut a patch NuGet release whose only delta is a workflow pin and a governance ledger JSON.
  evidence: Story 3.1 closure code review (edge-case-hunter), verified 2026-07-28 against `references/Hexalith.FrontComposer/.releaserc.json:5-9` — `@semantic-release/commit-analyzer` with `preset: conventionalcommits` and no `releaseRules` scope or path filter. This is the same class of defect as the project rule "Don't use `feat` for refactors (false minor bump + NuGet publish)"; `ci:` or `build:` would have been the non-releasing type. The commit is already on `origin/main`, so correcting it requires a revert-and-recommit by its owner. Suggested durable fix: add `releaseRules` excluding CI-scoped commits, or use a non-releasing type for pin/ledger maintenance.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM] EventStore and FrontComposer enforce opposite invariants on the release pin. EventStore asserts the pinned Builds release SHA must **differ** from its `references/Hexalith.Builds` gitlink; FrontComposer asserts they must be **equal**. Applying the Story 3.1 fix pattern to EventStore would break EventStore's guard.
  evidence: Story 3.1 closure code review (edge-case-hunter), verified 2026-07-28 against `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:310` (`gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha)`) versus `references/Hexalith.FrontComposer/tests/.../CiGovernanceTests.cs:474-482` (three-way equality). Both are defensible in isolation — EventStore decouples release authority from the dev-time gitlink, FrontComposer locks them together — but no workspace-level document states which rule applies where. Suggested durable fix: document one workspace-wide rule in Hexalith.Builds and have each guard cite it.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [LOW, FrontComposer-owned] Asymmetric supply-chain pinning: `release.yml` pins Builds to an exact SHA under the REL-6 identity rationale, while `ci.yml` and `quality.yml` both consume Builds at `@main` — the lanes that authorize the release run unpinned Builds code.
  evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/.github/workflows/release.yml:89`, `ci.yml:25`, `quality.yml:40`. Related: the release pin currently trails Builds `main` by a large margin, so the release path executes Builds logic no other lane validated. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: assert a bounded `git rev-list --count <pin>..origin/main` distance, or pin all three lanes.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM, FrontComposer-owned] The release-pin fix duplicates work already tracked as REL-6 and landed direct-to-main against FrontComposer's own frozen spec, which lists "Committing directly to `main` instead of a `fix/` branch + PR" under **Ask First**.
  evidence: Story 3.1 closure code review (blind-hunter), verified 2026-07-28. `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/spec-fix-release-builds-execution-sha.md` is status `done` and frozen-after-approval with that Ask First entry; `b6efcad5` is a direct-to-main commit (`origin/main == b6efcad5`, no merge commit). The drift was not a fresh discovery: it is logged as REL-6 in `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/deferred-work.md:1853` and named in that repo's `sprint-status.yaml:467` as a known pre-existing baseline failure, yet the Story 3.1 record presents it as newly root-caused and its own to fix. Owned by the Hexalith.FrontComposer maintainer.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM, FrontComposer-owned] Analyzer-ledger hardening bundle — the CA1707 fail-closed gate compares only an aggregate count and one hash, so re-attestation is indistinguishable from laundering a violation.
  evidence: Story 3.1 closure code review (edge-case-hunter + verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/.../AnalyzerPolicyGovernanceTests.cs:552-597,793-808` and `_bmad-output/contracts/analyzer-policy-exception-ledger-v1.json:26,37,74-75,105`. Five distinct gaps: (1) the failure message prints only the new count and hash, never the added/removed `path:line:token` set, so offsetting additions and removals are invisible; (2) the inventory is computed from `git ls-files --cached` plus working-tree bytes, so an attested hash can encode uncommitted content and untracked test files go uncounted; (3) a new test project under `tests/` silently expands the CA1707 exemption with no `testProjectRoots` assertion; (4) this re-attestation refreshed only `identifierInventory`, leaving the census `sourceCommit` and `implementationCount` stale so the ledger self-contradicts; (5) the linked `naming-ca1707-test-convention` disposition names "test source inventory drifts" as its revalidation trigger, but its `decisionDate`/evidence were not bumped, so an owner revalidation obligation was discharged by a hash paste. Owned by the Hexalith.FrontComposer maintainer (Story 11.19+). Suggested durable fix, smallest first: emit the token delta in the failure message; assert every added token is a method-declaration identifier on a `[Fact]`/`[Theory]` member (the ledger already carries the Roslyn machinery); compute the inventory from `git show HEAD:<path>` and fail on a dirty tree.

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [MEDIUM] The live-sidecar lane has no placement isolation from other local EventStore hosts. Its actor type names are fixed, non-namespaced constants, so any concurrently running EventStore-derived app joins the same DAPR placement ring and a random subset of test actor IDs hash-routes to a foreign — possibly dead — host, failing the run with `connection refused` on an app port the fixture does not own.
  evidence: Observed live during the Story 3.1 AC6 re-run, 2026-07-28, at root `589da8b9`. Four consecutive `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests` runs on byte-identical, un-rebuilt binaries produced `3 failed / 46 passed`, then `49/49`, `49/49`, `49/49`. All three failures were in `Integration/NamedProjectionDispatchLiveSidecarTests`, clustered within 300 ms; two raised `Dapr.DaprApiException … dial tcp 127.0.0.1:37313: connect: connection refused` on `ProjectionActor.DiscardProjectionAsync` and `ProjectionLifecycleActor.BeginDeliveryWriteAsync`, and the third (`ReadStateJsonAsync … should not be null but was`) is the downstream state consequence. Port `37313` had no listener (`ss -ltnp`) and is not the fixture's own app port — `DaprTestContainerFixture.cs:875` asserts its Kestrel binding, and each test's opening `fixture.ThrowIfHostStopped()` did not trip. `docker logs dapr_placement` over the window shows eight foreign app-ids in namespace `default` (`eventstore` ×10 status reports, `tenants-api`, `tenants`, `sample`, `memories`, `eventstore-admin`, `eventstore-admin-ui`, `commandapi`) interleaved with the fixture's per-run `eventstore-live-<guid>` hosts. DAPR partitions the placement ring by namespace **and actor type**, and `ProjectionActor`/`ProjectionLifecycleActor`/`AggregateActor` are shared fixed names, so foreign hosts join the ring the fixture believes it owns; freshly generated per-run actor IDs explain why the fault is probabilistic rather than reproducible. This is the same shared-placement / fixed-name-actor constraint already known for this repo's Tier-3 suite. Classified as an environment blocker under the story's "Environment is not product behavior" guardrail — no product or test code differs between the failing and passing runs — and deferred rather than patched because Story 3.1 AC7 forbids changing the fixture, trait taxonomy, or DAPR readiness thresholds absent a proven product defect. CI exposure is lower but non-zero: `integration.yml` runs `dapr-init` on a fresh runner with no foreign EventStore hosts, so contention there would require self-inflicted concurrency. Suggested durable fix, smallest first: give the fixture a dedicated placement instance on a private port instead of the shared `dapr_placement` container; failing that, namespace the fixture's daprd (`--namespace eventstore-live-<guid>`) so its ring cannot be joined; failing that, have `VerifyPrerequisitesAsync` enumerate foreign placement members and fail closed with an environment-blocker diagnostic instead of surfacing a bare `connection refused` mid-suite.

## Deferred from: post-merge code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)

- source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md`
  summary: [LOW] The `docs/ci.md` publication-pin literal is unguarded, so the exact drift the Story 3.1 closure fixed (`cf04c419…` → `f75daebd…`) will recur silently at the next Builds pin advance.
  evidence: Story 3.1 post-merge code review (edge-case-hunter + verification-gap), verified 2026-07-28. No test references `docs/ci.md` (`grep -rn 'docs/ci.md' tests/ --include='*.cs'` returns nothing); the only enforced copy of the pin is `ApprovedBuildsReleaseSha` at `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:13`, asserted against `.github/workflows/release.yml` only. The document is cited by Story 3.1 as a `source_files` release-flow authority and already went stale once this way. Suggested durable fix: extend `ContainerPublishingGovernanceTests` to assert the 40-hex pin quoted in `docs/ci.md` equals `ApprovedBuildsReleaseSha`, so the doc participates in the same lockstep the workflow already has.

## Deferred from: code review of story-3.2 (2026-07-29)

- source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`
  summary: [MEDIUM] The `RequestTimeout` *behavioral* contract is unverified in every lane. Story 3.2 closed the "the value is plumbed" half; the "the value takes effect" half — a slow or unreachable actor actually failing open at the configured window — is asserted by no test anywhere.
  evidence: Story 3.2 code review (verification-gap), verified 2026-07-29. `grep -rn "RequestTimeout" --include=*.cs` excluding `references/` touches the ETag path in exactly three places: the production assignment at `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:24` and the two unit assertions at `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs:43` and `:70`. Both assertions only inspect the `ActorProxyOptions` object handed to a **substituted** factory. The live-sidecar tests (`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/DaprETagServiceLiveSidecarTests.cs:69,95`) supply 30s but assert only `actual.ShouldBe(expectedETag)` and `actual.ShouldBeNull()` — neither asserts elapsed time nor a timeout-induced fail-open, and no slow-actor simulation exists on this path. The documented invariant at `DaprETagService.cs:19-22` ("a slow or unreachable actor never blocks the projection read path") is relied on by `CachingProjectionActor.cs:49`. Regression that would ship green: if `ActorProxyOptions.RequestTimeout` stopped being honoured by the proxy (a plausible `Dapr.Actors` upgrade side effect — pinned at 1.18.5), a hung ETag actor would block the projection read path indefinitely while every test stays green. Suggested durable fix: a live-sidecar fact pointing `DaprETagService` at an unreachable/paused endpoint with a short override (~1s), asserting both `ShouldBeNull()` and elapsed time bounded near the configured window.

- source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`
  summary: [MEDIUM] `DaprETagService` always passes a non-null `ActorProxyOptions`, which wholesale-replaces the DI-configured `ActorProxyFactory.DefaultOptions` — so the ETag path can target the environment-variable default endpoint and an empty API token while every other actor call site honours the configured values. Constructor-time environment parsing also sits outside the fail-open boundary.
  evidence: Story 3.2 code review (edge-case-hunter), 2026-07-29. Pre-dates FR18 — before CP-5 the field was `static readonly` but equally non-null, so the bypass is not a Story 3.2 regression. `Dapr.Actors.Client.ActorProxyFactory.CreateActorProxy<T>(actorId, actorType, options)` resolves `options ?? defaultOptions`, consulting `DefaultOptions` only when `options` is null (decompiled 1.18.5 — re-verify before acting). `DaprETagService.cs:23-25` always supplies its own instance, so `HttpEndpoint`, `DaprApiToken`, `JsonSerializerOptions` and `UseJsonSerialization` fall back to `ActorProxyOptions`' own initializers (`DaprDefaults.GetDefaultHttpEndpoint(null)` / `GetDefaultDaprApiToken(null)`, which read **environment variables only** given a null `IConfiguration`). Meanwhile `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:196-200` sets `options.HttpEndpoint` from `configuration["DAPR_HTTP_PORT"]`, and `AddActors` copies that into `factory.DefaultOptions`. Practical exposure is limited to deployments where `DAPR_HTTP_PORT`/`DAPR_API_TOKEN` arrive via appsettings, user-secrets or command line rather than the process environment — which the codebase does treat as a config key (`src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:110`). Consequence there: 401/connect failure → generic catch at `DaprETagService.cs:57` → fail-open null on every fetch, ETag/304 caching permanently dead behind a Warning log. Related: the `new ActorProxyOptions { … }` field initializer runs `int.Parse` on `DAPR_HTTP_PORT` and `new Uri(...)` on `DAPR_HTTP_ENDPOINT` inside the **constructor**, before `GetCurrentETagAsync`'s try/catch exists, so a malformed value faults scope resolution under `TryAddScoped` instead of degrading. Suggested durable fix: seed `_proxyOptions` from the factory's configured defaults and override only `RequestTimeout`.

- source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`
  summary: [LOW] The `requestTimeout` override parameter validates nothing. `TimeSpan.Zero`, any negative value, or a value above `int.MaxValue` ms throws inside the `try` and is swallowed into a permanent silent fail-open; `Timeout.InfiniteTimeSpan` is accepted and removes the fail-open bound the class documents as its core invariant.
  evidence: Story 3.2 code review (edge-case-hunter), 2026-07-29. `Dapr.Actors.DaprHttpInteractor..ctor` does `httpClient.Timeout = requestTimeout ?? httpClient.Timeout`, and `HttpClient.Timeout` throws `ArgumentOutOfRangeException` for zero, negatives other than `Timeout.InfiniteTimeSpan`, and values above `int.MaxValue` ms. That throw occurs at `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:45-46`, inside the `try`, so the generic `catch` at `:57` returns null forever. For the infinite case, `ActorProxyOptions.RequestTimeout`'s own XML doc confirms it disables timeouts; combined with `proxy.GetCurrentETagAsync()` taking no `CancellationToken` (`:48`, deliberate — see the remoting-interface comment at `:36-44`), neither bound survives and `CachingProjectionActor.QueryAsync` (`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs:50-52`) can block indefinitely. Real-world reachability is currently nil: all 13 `new DaprETagService(...)` sites in the repo are tests, and DI supplies nothing for the optional parameter. Rated LOW for that reason; it becomes material the moment a production caller supplies a value. Also note the in-file comment at `:42` still hardcodes "the 3 s RequestTimeout" although the window is now instance-dependent. Suggested durable fix: `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout.Value, TimeSpan.Zero)` plus an upper bound in the constructor, so a bad value fails fast instead of failing open forever.

- source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`
  summary: [LOW] Two unguarded seams around the Tenants dependency: nothing asserts the `references/Hexalith.Tenants` gitlink is coherent with the `HexalithTenantsVersion` package pin, and no test exercises the DI construction path that supplies `DaprETagService`'s 3s production default.
  evidence: Story 3.2 code review (verification-gap + edge-case-hunter), verified 2026-07-29. (1) `grep -rn "HexalithTenantsVersion" --include=*.cs tests/` returns nothing; the only `.gitmodules`-reading test (`tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs:80-81`) checks solely for the `Hexalith.AI.Tools` path, and the sole source-mode lane (`.github/workflows/ci.yml:64-108`) runs exactly one filtered class, `TenantsApiLaunchSettingsTests`. A published Tenants package diverging from the pinned submodule source would ship undetected — this bit the repo in this very diff, where the Builds gitlink advanced the pin 5.0.0 → 5.1.0 with no story record. (2) `grep -rn "GetRequiredService<IETagService>\|GetService<IETagService>" tests/ src/` returns no hits: all 15 deterministic facts and both live facts use `new DaprETagService(...)`. The 3s production default therefore rests entirely on the container's `HasDefaultValue` fallback for the unregistered optional `TimeSpan?` at `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54`, asserted nowhere. Story Task 1's "confirm no `TimeSpan`/`ActorProxyOptions` service is registered" was a one-time manual read, not a durable guard: a future `services.AddSingleton(TimeSpan…)` from any Hexalith module would silently retime every production ETag fetch with all tests green. Suggested durable fix: a gitlink/pin coherence assertion in the packaging governance suite, and one fact that resolves `IETagService` from a real `ServiceCollection` and asserts the effective timeout.

## Deferred from: code review of story-3.3 (2026-07-29)

- source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
  summary: [MEDIUM] `RepositoryProjectPaths.GetReferencedModuleProjectPath` probes a root-level `<root>/Hexalith.<Module>/` checkout at higher precedence than `references/`, and its docstring falsely claims the candidate order mirrors `Directory.Build.props`.
  evidence: Story 3.3 code review (blind-hunter, verified 2026-07-29 at HEAD `1d42528b`). `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:80` is candidate 4, `Path.Combine(root, moduleDirectory, relative)` (commented "root-level sibling module checkout"), evaluated before candidate 5 `standalone` = `<root>/references/<module>/…` at `:81`. The docstring at `:44-51` states the helper probes "every checkout layout in the same order as the `$(Hexalith*Root)` auto-detection in `Directory.Build.props`" — that is false: `Directory.Build.props:23` puts `references/Hexalith.Tenants/src` first (with an explicit comment that it "takes precedence… over any sibling/standalone clone"), `:40` puts `references/Hexalith.Commons` first, and `grep -c 'MSBuildThisFileDirectory)Hexalith\.' Directory.Build.props` returns **0** — MSBuild has no root-level probe at all. Consequence: a stray root-level `Hexalith.Tenants/` directory silently shadows `references/Hexalith.Tenants`, which is exactly the root-level path assumption FR19 exists to retire, and the AppHost could launch a different csproj than it builds. Not patched because AC4 forbids replacing the flexible resolver absent a proven break, Dev Notes `:169` explicitly accepts it ("the required invariant is that `references/` remains the fallback/convention"), and AC1 verified no such directory exists today. Text-based stale-path scans structurally cannot see it because the path is composed via `Path.Combine`. Suggested durable fix, smallest first: correct the docstring to describe actual precedence; then reorder candidate 4 below `standalone` so `references/` wins, guarded by a test.

- source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
  summary: [MEDIUM] AC4 was closed without any test proving that a **present** module under `references/` resolves; 5 of the 7 resolver candidates are entirely uncovered.
  evidence: Story 3.3 code review (blind-hunter + acceptance-auditor), verified 2026-07-29. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs` has 9 tests (5 `[Fact]` + 4 `[InlineData]`), of which exactly one touches the referenced-module helper: `GetReferencedModuleProjectPath_WhenModuleMissing_ReturnsReferencesFallback` (`:31-46`). That case returns the `standalone` path only because **nothing on disk exists** — it exercises the no-match `return standalone` at `RepositoryProjectPaths.cs:94`, never candidate 5 itself. Candidates 2, 3, 4, 6 and 7 (`:78-83`) have no coverage. A verification-and-reconciliation story for FR19 therefore added zero coverage to the single helper FR19's Aspire clause depends on. Suggested durable fix: drive the helper against a temporary directory tree so each layout can be materialised and asserted; this requires making the repository root injectable, since `GetRepositoryRoot()` (`:104`) derives from `AppContext.BaseDirectory`.

- source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
  summary: [LOW] `GetReferencedModuleProjectPath` omits the path-segment validation and root-containment check that its sibling `GetProjectPath` enforces, so a rooted segment would escape the repository root.
  evidence: Story 3.3 code review (blind-hunter), verified 2026-07-29. `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:30` calls `ValidateRelativePathSegments` and `:38-40` rejects any result that does not resolve under the repository root; the referenced-module helper at `:55-64` performs only null/empty checks on `moduleDirectory` and an array-length check on `moduleRelativePath`. `Path.Combine` discards everything preceding a rooted segment, so a rooted `moduleRelativePath` element yields an arbitrary absolute path with no containment check. `GetProjectPath_WhenSegmentIsRooted_ThrowsArgumentException` (`RepositoryProjectPathsTests.cs:68-74`) has no counterpart for the helper AC4 explicitly names. Rated LOW because every current call site passes compile-time literals (`EventStorePlatformProjectMetadata.cs`, 3 sites). Suggested durable fix: call `ValidateRelativePathSegments(moduleRelativePath)` and apply the same rooted-prefix containment assertion to the returned candidate.

- source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
  summary: [MEDIUM] `SampleApiLaunchSettingsTests` hand-rolls newline-sensitive YAML parsing that survives CRLF only by accident, while its sibling parses the same file with YamlDotNet.
  evidence: Story 3.3 code review (edge-case-hunter + verification-gap), verified 2026-07-29. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:103` splits on a bare `'\n'`, `:124` rejoins on `'\n'`, and `:135` re-splits on `'\n'`, consuming `File.ReadAllText` at `:67-72`. It tolerates CRLF only because the `.Trim()` calls at `:107` and `:137` incidentally strip the stray `\r` — nothing in the design guarantees it, and a CR-only, NEL, LS or PS terminator collapses the split to a single element, yielding zero policies and failing `:75 ShouldBe(1)`. `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` is `w/lf` today, but `.editorconfig` has only `[*]` and `[*.cs]` sections — no `[*.yaml]` override — so its `end_of_line = crlf` applies and a conforming editor will rewrite the file. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:107-151` already parses this identical file with YamlDotNet, which is an existing `PackageReference` in the same test project. Suggested durable fix: replace `ExtractYamlPolicies`/`ExtractOperations` (`:99-167`) with the sibling's YamlDotNet parsing.

- source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md`
  summary: [LOW] Three tracked shell scripts lack the executable bit, so direct invocation fails with exit 126; discovered during Story 3.3 Task 6, worked around, and never filed.
  evidence: Story 3.3 code review (blind-hunter + acceptance-auditor), verified 2026-07-29. `git ls-files -s scripts/*.sh` reports mode `100644` for `scripts/ci-local.sh`, `scripts/check-deferred-work.sh` and `scripts/validate-release-secrets.sh`, against `100755` for `scripts/check-doc-versions.sh`, `scripts/generated-api-smoke-preflight.sh`, `scripts/validate-docs.sh`, `scripts/validate-evidence.sh` and `scripts/validate-publication-preflight.sh`. Story 3.3 `:283` records the resulting exit 126 on `./scripts/ci-local.sh --tier 1 --skip-build` and routes around it by invoking the script through `bash`, but files nothing — so the next caller hits the same wall, and any CI step or hook that invokes these three directly rather than via an interpreter breaks. Suggested durable fix: `git update-index --chmod=+x scripts/ci-local.sh scripts/check-deferred-work.sh scripts/validate-release-secrets.sh`, plus a packaging-governance assertion that every `scripts/*.sh` is mode `100755`.

## Deferred from: code review of story-3.4 (2026-07-30)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] No CI lane ever executes the source-mode (`HEXALITH_TENANTS_SOURCE`) AppHost topology, so the Tenants security dependents that `AspireSecurityResourceNamingTests` conditionally asserts are verified nowhere.
  evidence: Story 3.4 review pass 4 (verification-gap), verified 2026-07-30. `.github/workflows/ci.yml:34` runs `tests/Hexalith.EventStore.AppHost.Tests` through the Builds reusable workflow with no `UseHexalithProjectReferences` override, so `Directory.Build.props:51` defaults it to `false` and neither `tenants` nor `tenants-api` exists in the built model. The single source-mode job (`.github/workflows/ci.yml:99-105`) runs `dotnet test --filter FullyQualifiedName~TenantsApiLaunchSettingsTests`, which excludes the naming class entirely. The pre-existing condition is that narrow source-mode filter, not the new test; its `if (builder.Resources.Any(... "tenants" ...))` guard at `AspireSecurityResourceNamingTests.cs:80-88` silently shrinks the expected dependent set instead of failing. Demonstration: delete `_ = tenants.WithJwtBearerSecurity(security);` from `src/Hexalith.EventStore.AppHost/Program.cs:159` and both CI jobs stay green. Suggested durable fix, smallest first: extend the source-mode job's `--filter` to include `AspireSecurityResourceNamingTests`; longer term, give the source-mode lane a real suite rather than one filtered class.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] Documentation and agent guidance invoke `aspire run --project` / `aspire publish --project`, a flag the pinned Aspire CLI 13.4.6 does not accept.
  evidence: Story 3.4 review pass 4 (verification-gap), verified 2026-07-30 against `aspire --version` = `13.4.6+87fe259e`. `aspire run --help` and `aspire publish --help` list only `--apphost`. `--project` survives at roughly fifteen sites, including `deploy/README.md:195,202,282,290,333,341`, `docs/getting-started/quickstart.md:29`, `docs/getting-started/first-domain-service.md:203`, `docs/brownfield/development-guide.md:79,96`, `docs/guides/deployment-docker-compose.md:106,113`, `docs/guides/deployment-kubernetes.md:193,199`, `docs/guides/deployment-azure-container-apps.md:148,154`, `docs/guides/troubleshooting.md:552`, and `.claude/agents/aspire.md:62`. Story 3.4 rewrote two of these incidentally while correcting role identities, which leaves `docs/guides/troubleshooting.md` internally inconsistent. This is CLI-flag drift unrelated to the security role identity, and nothing in the repository verifies documented CLI invocations. Suggested durable fix: a docs-validation assertion that no tracked guidance passes `--project` to `aspire run`/`aspire publish`, plus a sweep of the listed sites.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] New evidence only, no status change: the premise behind the earlier quickstart port deferral -- that the default non-persistent AppHost picks Keycloak host ports dynamically -- is contradicted by the implementation.
  evidence: Story 3.4 review pass 4 (blind-hunter, verified 2026-07-30). `KeycloakFastStartPorts.ResolveDynamic` (`src/Hexalith.EventStore.Aspire/KeycloakFastStartPorts.cs:72-78`) calls `FindAvailablePort(DefaultHttpPort /* 8180 */, [])` and `FindAvailablePort(DefaultManagementPort /* 8543 */, [httpPort])`, each of which returns the preferred port unless it is occupied; `HexalithEventStoreSecurityExtensions.cs:82-86` binds both endpoints proxyless in the non-persistent branch as well. The extension's own comment at `:47-52` states the default "prefers 8180/8543 and moving forward when either port is busy". `docs/getting-started/quickstart.md:44`'s `localhost:8180` is therefore correct for the default topology in the ordinary case. Recorded as new evidence against an existing entry; the orchestrator owns that entry's status and resolution. Review pass 4 separately corrected the same wrong premise where this story had introduced it into `docs/guides/troubleshooting.md`.

## Deferred from: code review of story-3.4 (2026-07-30, review pass 5)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] The Docker Compose deployment guide still shows stale dependency-version examples (Keycloak 26.4, Aspire SDK 13.1.x) that no check covers.
  evidence: Story 3.4 review pass 3 deferral, never propagated to this ledger; re-verified 2026-07-30 at review pass 5. `docs/guides/deployment-docker-compose.md:144` shows `image: "quay.io/keycloak/keycloak:26.4"` and `:155` says "Exact field names and structure depend on the Aspire SDK version (currently 13.1.x)", while the story's own scratch `aspire publish` evidence records Keycloak 26.6 in the generated artifact and the pinned CLI/AppHost as 13.4.6. `scripts/check-doc-versions.sh` validates only the four Dapr rows in `docs/reference/nuget-packages.md`, so nothing detects this drift. Refreshing dependency-version examples is pre-existing documentation maintenance, independent of the security role-identity reconciliation. Suggested durable fix: extend `scripts/check-doc-versions.sh` to assert documented Keycloak image tags and Aspire SDK versions against `Directory.Packages.props` / the generated Compose artifact, then refresh the two sites.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] New evidence only, no status change: the `aspire run --project` drift also exists in production source and in more sites than the earlier entry counts.
  evidence: Story 3.4 review pass 5 (blind-hunter, verified 2026-07-30). `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:150` emits `"    aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"` in a runtime diagnostic message, so the invalid flag reaches users from shipped code, not only from documentation; a documentation-only sweep would leave it behind. `docs/guides/configuration-reference.md:597,600,610` additionally use `dotnet run --project src/Hexalith.EventStore.AppHost`, the same form this story replaced in `docs/guides/troubleshooting.md`, and `scripts/generated-api-smoke-preflight.sh` passes `--project` at three sites. The tracked total is therefore higher than the "roughly fifteen sites" the earlier entry records. Recorded as new evidence against that entry; the orchestrator owns its status and resolution.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md`
  summary: [MEDIUM] Building the real AppHost model inside a unit test mutates a machine-wide temp directory, so the AppHost suite can disturb a concurrently running `aspire run`.
  evidence: Story 3.4 review pass 5 (blind-hunter, verified 2026-07-30). `AspireSecurityResourceNamingTests` calls `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`, which executes `src/Hexalith.EventStore.AppHost/Program.cs` top to bottom, including `ResolveIsolatedDaprComponentPath` at `Program.cs:249-266`. That helper deletes every `*.yaml` under `Path.GetTempPath()/hexalith-eventstore-dapr-components/statestore` (`:259-261`) and re-copies the component. The path is isolated from the repository, not per process, and `AspireEnvironmentMutationCollection` serialises only in-process environment mutation. Running the AppHost test assembly while a live topology is up therefore deletes and recreates the component file the running sidecars were started from; the end state is byte-identical, so the window is narrow, but a sidecar starting inside it can fail to read its state-store component. Pre-existing AppHost behaviour; the new exposure is that a normal test run now executes it. Suggested durable fix: give `ResolveIsolatedDaprComponentPath` a per-instance subdirectory (or an env-var override the test can point at a temp path), which requires touching production AppHost source and so falls outside this story's boundary.

### DW-4: Follow-up review still recommended for 3-4-aspire-security-resource-naming after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-3-4-aspire-security-resource-naming.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260730-064902-1608; this entry preserves the lingering recommendation for a deliberate later review.
status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-fix-mcp-startup.md`
  summary: The vendor-managed WSL `codex-node-repl` launcher has no process-group shutdown contract for its Windows `node_repl.exe` descendant, so a native runtime that ignores forwarded termination could outlive a forcibly killed JavaScript launcher.
  evidence: Review of the MCP startup fix found that `/mnt/c/Users/JeromePiquot/AppData/Roaming/npm/codex-node-repl.js` spawns `node_repl.exe` and forwards only SIGINT, SIGTERM, and SIGHUP to that immediate child; the user-scoped proxy now maps SIGQUIT, preserves caller signal status, and bounds its own immediate-child shutdown, but cannot guarantee descendant cleanup without modifying the vendor bridge or establishing a tested cross-WSL process-group mechanism, both outside this fix's boundary.

## Deferred from: code review of story-3.6 (2026-07-31, follow-up review pass)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md`
  summary: [MEDIUM] Semantic-release uploads GitHub Release assets with the unscoped glob `nupkgs/*.nupkg`, so the exact-scope guarantee AC3 pins on the NuGet push command has no equivalent on the second publication channel.
  evidence: Story 3.6 follow-up review (2026-07-31). `.releaserc.json:12` publishes to NuGet with `dotnet nuget push "./nupkgs/Hexalith.EventStore.*.nupkg"`, and `ReleasePackageManifestTests.Semantic_release_publish_command_pushes_scoped_packages` now asserts that glob appears exactly once and that the unscoped form is absent. `.releaserc.json:18` still declares `"assets": ["nupkgs/*.nupkg"]` for the `@semantic-release/github` plugin, which is untouched by this story's diff and uncovered by the new exact-scope assertion. The live risk is currently mitigated, not eliminated: `tools/validate-release-packages.py` runs in `prepareCmd` before publish and fails closed on any archive in `./nupkgs` outside the 14-entry manifest, so the unscoped glob can only match manifest packages on a successful release. It is pre-existing configuration, independent of the archive-metadata contract this story delivered. Suggested durable fix: narrow the asset glob to `nupkgs/Hexalith.EventStore.*.nupkg` and extend the publish-governance test to assert both publication channels are EventStore-scoped.

## Deferred from: code review of story-3.6 (2026-07-31, follow-up review pass 2)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md`
  summary: [LOW] New evidence only, no status change: the 2026-07-31 story-3.6 asset-glob entry above cites a test name that does not exist, so its evidence trail is unfollowable.
  evidence: Story 3.6 follow-up review pass 2 (2026-07-31, verified against `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`). That entry attributes the exact-scope publish assertion to `ReleasePackageManifestTests.Semantic_release_publish_command_pushes_scoped_packages`; no test of that name exists in the repository. The assertion is real but lives in `Semantic_release_delegates_package_inventory_to_manifest_scripts`, which now also pins that `tools/pack-release-packages.py` precedes `tools/validate-release-packages.py` in `prepareCmd` — the command ordering the earlier entry's stated mitigation depends on and which nothing previously asserted. The deferred finding itself (the unscoped `nupkgs/*.nupkg` GitHub asset glob at `.releaserc.json:18`) is unchanged and still open. Recorded as new evidence against that entry; the orchestrator owns its status and resolution.

### DW-5: Follow-up review still recommended for 3-6-manifest-driven-release-packaging after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-3-6-manifest-driven-release-packaging.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260731-203343-5b29; this entry preserves the lingering recommendation for a deliberate later review.
status: open

## Deferred from: code review of 8-1-shared-payload-protection-security-spec-and-adr (2026-08-01)

- [MEDIUM] Reconcile `epic-2: in-progress` with all listed Epic 2 stories and its retrospective marked `done`. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:79`. Pre-existing sprint-tracking inconsistency outside Story 8.1.
- [LOW] Add the intentionally preserved `awaiting-operator` value to the sprint-status schema comments. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:19,205`. Pre-existing schema-comment drift outside Story 8.1.
- [MEDIUM] Separate unrelated Epic 1-7 tracking changes from the Story 8.1 baseline evidence so scope attribution is reviewable. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:51`. The shared sprint file accumulated concurrent story updates after the recorded baseline.

## Deferred from: code review of story-3.13 (2026-08-04)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: [LOW] `review_loop_iteration: 1` was not incremented despite two documented hardening passes recorded in the same file's Spec Change Log.
  evidence: `spec-3-13-deployed-runtime-parity-closure.md:7` frontmatter still reads `review_loop_iteration: 1`, while the Spec Change Log records "Applied all 15 code-review patches" and, separately, "Applied the second review-hardening pass ... 115 focused mutation cases," both dated 2026-08-04. Cosmetic drift, not blocking.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: [LOW] The story's File List omits several evidence files the tests and crosswalk already depend on and validate.
  evidence: `3-13-deployed-runtime-parity-closure.md:588` lists only a bare evidence-directory reference plus `reviewer-roster.json`, but `DeployedRuntimeParityClosureTests.cs` and `identity-crosswalk.json` reference and validate additional files (e.g. `deployment-authority.json`, `deployment-authority-source.json`, `release-provenance.json`, and further smoke/log files) not named in the File List. Documentation completeness only, not a functional gap.

## Deferred from: code review of story-4.3 loop 1 (2026-08-07)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [MEDIUM] Three event-payload writers disagree on serializer options, so persisted payload casing depends on which path produced the event.
  evidence: `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:29`, `src/Hexalith.EventStore.Server/Events/EventPersister.cs:71` and `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` all call `JsonSerializer.SerializeToUtf8Bytes` with implicit options. On the deployed DAPR topology `DomainServiceWireResult` is the real writer — `DaprDomainServiceInvoker.cs:192-198` wraps its bytes as `SerializedEventPayload` and `EventPersister.cs:70-71` passes them through untouched — so a future change to `EventPersister`'s options is inert in production. Harmless today (all readers are case-insensitive after Story 4.3), but any converter or naming-policy change applies to one writer only. Story 4.3 was deliberately narrowed to readers-only on 2026-08-07 rather than change a cross-process wire format; unifying the writers needs its own story with a rollout plan.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] The shared payload options are frozen with no extension seam, so a domain cannot register a converter for its own payload types.
  evidence: `EventStorePayloadSerialization.Options` is made read-only in its static initializer with the reflection resolver baked in. Domains needing enum-as-string, value-object or polymorphic payload converters have no supported way to contribute one, and no `JsonSerializerContext` can be chained in. This is not a regression — every call site previously used its own bare Web options with the same limitation — but centralizing makes it a single explicit decision point. AOT/trimming remain out of scope per the Epic 4 context (reflection-based dispatch is load-bearing).
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] `EventStoreProjection.Project`'s typed overload silently skips events with no matching Apply method.
  evidence: `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs:89-92` does `evt.GetType().Name` plus a plain `TryGetValue` with no fallback and no diagnostic, so an unmatched typed event is dropped without a log or throw and the read model is built with missing state. The sibling JSON path throws. Explicitly excluded from Story 4.3 scope to keep the change to type-name resolution and serializer options.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] The static Apply-method registry cache is unbounded and roots `Type` objects for process lifetime.
  evidence: The per-state registry cache keyed by `Type` is never evicted, which blocks collectible-assembly unload in plugin or hot-reload hosts. Pre-existing shape — both `DomainProcessorStateRehydrator` and `EventStoreProjection` already held process-wide static caches before Story 4.3; the story consolidates them without changing the lifetime policy.

## Deferred from: code review of story-4.3 loop 2 (2026-08-07)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [MEDIUM] `AggregateReconstructionErrorCategory` has no member for apply-method ambiguity, so replay reports it as `UnknownEventType`.
  evidence: `AggregateReplayer` maps `AmbiguousApplyMethodException` onto `AggregateReconstructionErrorCategory.UnknownEventType`, but the type is not unknown — it is known twice, and the two failures have completely different operator remediations ("this stream references a type I have never heard of" versus "your state type has colliding Apply overloads"). Admin and RFC 7807 consumers cannot distinguish them. Adding an `AmbiguousEventType` member changes a public enum in `Hexalith.EventStore.Contracts`, which is a package contract change and was outside Story 4.3's stated scope.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] Private `JsonSerializerDefaults.Web` options copies survive outside the four Client reader paths that Story 4.3 unified.
  evidence: `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs:18` and `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:35` each construct their own `new(JsonSerializerDefaults.Web)`. Both are behaviourally identical to the shared instance today, so there is no live drift, but they are outside the guardrail and would not follow a future converter change. Story 4.3 deliberately scoped to event/command payload binding; these serialize dispatch envelopes, not payloads.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] The anchored suffix scan is O(registered keys) per event with no memoization.
  evidence: A stored name that misses both exact maps walks the whole `SuffixKeys` list on every event of every replay or projection pass. Impact is much smaller than it looks — Story 4.3 registers the fully qualified name, so the exact-match lookup now hits on the normal path where it previously always missed, making the scan the rare branch rather than the hot one. A per-table resolution cache keyed on the stored name would restore O(1) if it ever matters. No benchmark accompanies the change despite `perf/` existing.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] Checked-in generated API reference does not list the new public types.
  evidence: `docs/reference/api/Hexalith.EventStore.Client/Hexalith.EventStore.Client.Aggregates.md` lists `MissingApplyMethodException` but not the new public `AmbiguousApplyMethodException`, and there is no generated page for the new public namespace `Hexalith.EventStore.Contracts.Serialization`. These files are generated with `ApiReferenceBuild=true`, so the fix is a regeneration pass rather than a hand edit.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] `AssemblyScanner` falls back to `GetTypes()` when `GetExportedTypes()` throws, weakening the "internal fixtures cannot leak into discovery tests" assumption.
  evidence: `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs:187` falls back to `GetTypes()` on failure, which returns non-exported types. Test fixtures declared `internal` specifically to stay invisible to assembly-wide discovery tests would become visible on that path. Pre-existing scanner behaviour, not introduced by Story 4.3.

## Deferred from: code review of story-4.3 (2026-08-08)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`
  summary: [LOW] Typed-instance rehydrate path builds `MissingApplyMethodException` with `evt.GetType().Name` instead of the CLR full name.
  evidence: `DomainProcessorStateRehydrator.cs:192-194` — on the runtime-instance path a cross-namespace near-miss reports the colliding short name. Pre-existing diagnostic shape on this path; Story 4.3 did not own MissingApplyMethodException message fidelity for typed instances. Envelope path already passes the stored full name.

## Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [HIGH] Dead-letter `cloudevent.id` is keyed on `CorrelationId`, so two dead-letters sharing a correlation id can be deduplicated away by subscribers.
  evidence: `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:56` sets `["cloudevent.id"] = safeMessage.CorrelationId`, unlike event publication which uses the per-event `MessageId` (`EventPublisher.cs:200`). Pre-existing since the dead-letter path was introduced, but Story 4.4 makes it load-bearing: drain exhaustion is now a terminal data-loss sink, so a deduplicated exhaustion dead-letter means committed events vanish with no trace. Fix by carrying a per-message id override so exhaustion publishes with `cloudevent.id` = the reduced command's `MessageId`.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] `ReplayController` accepts `PublishFailed` unconditionally and never consults the new `Retryable` signal, so replaying a command whose drain reminder is still armed can publish the same committed range twice.
  evidence: `src/Hexalith.EventStore/Controllers/ReplayController.cs:35-40` includes `PublishFailed` in `_replayableStatuses` and the gate at `:204` does not read `Retryable`. Story 4.4 adds the field that would make this decidable but does not wire it into the replay gate. Pre-existing double-publication risk; the new field makes a fix cheap.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] No Tier-3 or live-sidecar proof exists for what is fundamentally a crash-window story; `OnActivateAsync` is reached only by reflection.
  evidence: All activation coverage is against `Substitute.For<IActorStateManager>()`, and `PublicationRecoveryActivationTests.InvokeOnActivateAsync` resolves the hook via `GetMethod(..., Instance|NonPublic)`, which never proves DAPR invokes it, never exercises real reminder registration, and turns a rename into a runtime rather than compile failure. `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` and `tests/Hexalith.EventStore.IntegrationTests` both exist and received nothing. A real "kill between commit and reminder registration, restart, observe publication" proof belongs there. Tier-3 needs Docker/Aspire and is outside this spec's Verification list.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] `docs/guides/configuration-reference.md` claims exponential backoff between the minimum and maximum drain periods, but drain reminders register a constant period.
  evidence: `configuration-reference.md:92` documents exponential backoff, while `AggregateActor.GetDrainReminderSchedule` registers a fixed `DrainPeriod` and only clamps it against `MaxDrainPeriod`. Pre-existing drift from Story 4.2. It becomes more consequential with a bounded attempt count, since the wall-clock budget before permanent dead-lettering is then `MaxDrainAttempts * DrainPeriod` (~8 minutes on defaults) rather than a growing backoff window.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [LOW] `DrainReasonCodes` is `internal` but its values are now part of the public HTTP and wire contract, forcing consumers and tests to hard-code string literals.
  evidence: `CommandStatusResponse.RecoveryReasonCode` is returned to external HTTP clients and `DeadLetterMessage.ReasonCode` travels on the dead-letter topic, yet the bounded vocabulary lives only in `internal static class DrainReasonCodes`. The new tests already hard-code `"drain_attempts_exhausted"` and `"drain_publish_failed"` as bare strings. Consider promoting the constants to `Hexalith.EventStore.Contracts` and adding a test pinning the bounded set.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [LOW] `DeadLetterEntry` carries no extensions field, so the admin surface cannot see the not-replay-eligible marker on an exhaustion dead-letter and its Retry action has no guard.
  evidence: `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs:17` has no extensions property, and `DaprDeadLetterCommandService.RetryDeadLettersAsync` plus the Admin UI Retry action do not consult it. An operator can therefore replay the reduced envelope (empty payload, synthetic user id) as though it were the original command.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [LOW] Actor test fixtures are duplicated across test files, so any `AggregateActor` constructor change now requires four synchronized edits.
  evidence: `CreateActorForBoundedDrain` is copy-pasted with a near-identical body and a parallel context record into both `EventDrainRecoveryTests` and `PublicationRecoveryActivationTests`, alongside the pre-existing `CreateActor`/`CreateActorWithTimerManager`. `AggregateActor` now takes ten constructor arguments. One shared builder in `tests/Hexalith.EventStore.Server.Tests/TestUtilities` would remove the drift risk.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] The two `DeadLetterMessage` producers now disagree about the dead-letter contract, and the suite that encodes it only exercises one of them.
  evidence: `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:148` encodes "Dead-letter should contain full command envelope for replay" as an invariant of the contract. Story 4.4's `DeadLetterMessage.FromDrainExhaustion` deliberately emits a reduced, non-replayable envelope, but that suite exercises only `FromException`, so the two producers contradict each other with nothing reconciling them. Either scope the invariant to replay-eligible producers or assert the reduced shape explicitly.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [LOW] `Retryable` is left null for every non-drain status, which collides with the documented meaning of null as "written before this field existed".
  evidence: `CommandStatusRecord` and `docs/operations/drain-failure-reason-codes.md` define null as a legacy record predating the field, but `WriteAdvisoryStatusAsync` writes null for Received, Processing, happy-path Completed and tenant Rejected. A consumer cannot distinguish "legacy record" from "current record, retryability not applicable". `GetStatus_LegacyRecordWithoutRecoveryFields_KeepsRetryableNullRatherThanFalse` cannot observe the collision because its fixture is byte-identical to what the code writes today for a fresh non-drain status.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [LOW] Log EventId allocation across the partial `Log` classes is unguarded, so a duplicate EventId can be introduced silently.
  evidence: Story 4.4 adds EventIds 2010-2019 in `AggregateActor` (with `2019` declared out of order between `2015` and `2016`) and `5006` in `IdempotencyChecker`, but `AggregateActor` is a partial class whose `Log` block is one of several and nothing in the repo asserts uniqueness of EventIds within a category. A cheap reflection test over the `LoggerMessage` attributes would pin the ranges.

## Deferred from: obsolete main-rebase conflict draft closure (2026-08-08)

- source_spec: `_bmad-output/implementation-artifacts/spec-resolve-main-rebase-conflicts.md`
  summary: Forensic note for orphan commits `f3e036bf0cae72b50508a3e729f24a052a7c4e95` / `026b039b237372774d998af8f5b77c58db00d348` that a July draft assumed were unpushed on local `main`. They are not tip ancestors; winning sibling `f6db558c768ae413712560019beab488d9974d66` (same subject/parent) is already on `origin/main`. Closed as obsolete without rebase or cherry-pick — replaying the orphans would regress resilience, fixtures, Story 1.20 status, and submodule pins. Preserve the SHAs if reflog GC later drops tip reachability.
  evidence: `git merge-base --is-ancestor f6db558c768ae413712560019beab488d9974d66 origin/main` succeeds; `main` and `origin/main` both at `37fdcd1fc8a238b676441b1f5a5ef5fd4370d27e`; orphans still exist as objects (`git cat-file -t f3e036bf0cae72b50508a3e729f24a052a7c4e95` / `026b039b237372774d998af8f5b77c58db00d348` → `commit`); tip gitlinks remain Builds `824d7ef100455423aabbcd399c8364074000b2e0`, Memories `da5df10092461e5473d0e8fc09eacbb4a8e08d3a`, Tenants `323baf8871e70be3fde92072f32b758af950bc8c`.
  status: forensic-only (closed obsolete; non-actionable)

## Deferred from: Story 3.13 review (2026-08-08)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `package-availability.json` embeds machine-local absolute search roots under `/home/administrator/...`, which are non-portable durable evidence.
  evidence: Blind-hunter review of the Story 3.13 packet; fail-closed package recovery already records 404/unavailable, but retained search roots remain host-specific.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Epic 3 context rewrite thins earlier concrete cross-story constraints without an explicit supersession note.
  evidence: Review of `epic-3-context.md` in the baseline..HEAD scoped diff; historical live-sidecar/DaprETag specificity was reduced while adding 3.12/3.13 guidance.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Expected AC4 acceptance scaffolding (`acceptances/{subject_sha256}` layout / receipt schema example) is narrative-only and not checked into hashed manifests.
  evidence: Blind-hunter review; 0/3 acceptances are intentional while fail-closed, but reopen tooling has no committed empty convention.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Support-safety hostname privacy only special-cases `.internal`/`.local`, so other private DNS names can bypass the literal-IP private check.
  evidence: Edge-case hunter on `HostLooksPrivate` / `AddressIsPrivate` in `DeployedRuntimeParityClosureTests.cs`.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Retained `smoke-results.json` can declare top-level `"result": "pass"` while runtime-verification/crosswalk mark execution unverified/fail.
  evidence: Blind-hunter review; product gate is already fail-closed via runtime-verification, but the smoke summary over-claims relative to that gate. Fixing requires hash-bound evidence edits beyond this patch pass.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Crosswalk `approval_contract.required_receipt_fields` omits `schema` while the verifier’s `RequiredReceiptFields` requires it.
  evidence: Blind-hunter review; correcting the crosswalk would rehash core evidence and was deferred to avoid churn while 0/3 acceptances remain.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Review-subject blocker text still claims smoke logs lack cleanup facts after cleanup=pass appears in retained logs/runtime-verification.
  evidence: Blind-hunter review; blocker wording is hash-bound and should be narrowed only when evidence is intentionally republished.

## Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md`
  summary: Governed inline CI checkouts (`semantic-release-governance`, `tenants-source-mode`) set `persist-credentials: false`, but Contracts helpers never assert it and valid fixtures omit it.
  evidence: Verification-gap/edge-case review — deleting those lines leaves Shared_ci / Semantic_release_governance / mutation tests green; out of scope for the mixed-job false-positive fix.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md`
  summary: `Non_manifest_src_projects_cannot_produce_release_packages` only substring-matches `<IsPackable>false</IsPackable>` instead of evaluating MSBuild packability like the manifest-side check.
  evidence: Verification-gap review — conditional or later overriding true values can evade the complement gate; unrelated packaging growth since the story baseline.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md`
  summary: Release `verify-source` job body and fail-closed release inputs (`expected-package-count`, `timeout-minutes`) lack Contracts assertions comparable to the CI job-scoped guards.
  evidence: Edge-case review of post-baseline release topology changes — not caused by this mixed-job guardrail story.
- source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md`
  summary: CommitMessagePolicy markdown helpers can throw on malformed percent-encoding (`Uri.UnescapeDataString`) and can treat tab-indented fences as operative preflight blocks.
  evidence: Edge-case review of CommitMessagePolicyTests helper growth after baseline; adjacent to Copilot delegation but not required by frozen intent.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Redact absolute local_search_roots from retained package-availability.json (and refresh checksums/bindings) so support-safe evidence does not embed host filesystem paths.
  evidence: Blind-hunter review of Story 3.13; package-availability.json lists /home/administrator/... roots while JsonEvidenceIsSupportSafe currently accepts them.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Add missing-key removal mutations for validators whose NullReferenceException catch filters are only exercised by value mutations today.
  evidence: Blind-hunter review of Story 3.13; prior hardening added catch filters but theories still mutate values rather than removing keys.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-29763400936-fix-release-post-publish-status.md`
  summary: Fixture imports `undici` as a top-level module without a direct package.json dependency, so hoisting/layout changes could bind a different major than the plugin expects.
  evidence: Blind-hunter review — lockfile already shows multiple undici majors; fixture only asserts resolve-path equality with the plugin.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-29763400936-fix-release-post-publish-status.md`
  summary: Semantic-release governance job structural contracts do not pin omitted write permissions or forbid token env overrides on the Node fixture steps.
  evidence: Edge-case hunter review — job could gain contents/pull-requests write without failing AssertSemanticReleaseGovernanceJobIsBlocking; outside frozen success-notification scope.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md`
  summary: Scrub remaining predecessor SDK patch tokens from Hexalith.FrontComposer tracked BMAD review `.diff` artifacts.
  evidence: Split from the SDK 10.0.302 cleanup so the root EventStore leftover pass can ship alone; FrontComposer still has four predecessor SDK patch-token hits in `_bmad-output/implementation-artifacts/.11-17d-group{3,4}-review.diff` under the same 1A/2B/3A-strict policy.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md`
  summary: Scrub remaining predecessor SDK patch tokens from Hexalith.Memories BMAD artifacts, including the below-min I/O matrix rewrite to a non-predecessor.
  evidence: Split from the SDK 10.0.302 cleanup so the root EventStore leftover pass can ship alone; Memories still has four hits in `spec-run-tests-and-fix-failures.md` and `27-1-access-telemetry-retention-ownership-decision.md` under the same 1A/2B/3A-strict policy.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Dev Agent Debug Log still cites stale focused/suite test totals that no longer match the 140-test verifier count.
  evidence: Blind-hunter review of the Story 3.13 record found 115/117 and suite 999/1001 figures while the proof packet and latest hardening pass report 140 focused tests; documentation-only drift, not a verifier defect.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Several Task parent checkboxes remain unchecked while child boxes and later tasks are marked complete.
  evidence: Blind-hunter review showed Tasks 4–7 and 9 parents unchecked despite checked children; AC2/AC4 intentionally remain open, so this is progress-tracking hygiene rather than a functional gap.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.5 LiveSidecar ownership prose was added in docs/ci.md within the same baseline range as Story 3.13's ownership rewrite.
  evidence: Spec Code Map allows only the Story 3.12-to-1.20 ownership paragraph change in docs/ci.md; the Story 4.5 paragraph is concurrent scope leakage outside 3.13's single-goal delivery and should be owned by Story 4.5 tracking.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Add an `acceptances/{subject_sha256}/` scaffold or receipt template beside the roster before AC4 collection.
  evidence: Blind-hunter review on 2026-08-09; AC4 still requires three content-bound receipts and 0/3 remain missing, but fail-closed review does not need the scaffold to stay non-done.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Re-measure the full Contracts.Tests suite after the ninth hardening pass and refresh Dev Agent / proof-packet totals if they drift.
  evidence: Blind-hunter review on 2026-08-09; last recorded full-suite measurement was 1001 on 2026-08-05 while focused coverage continued to grow.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Reviewer roster maps both eventstore-owner and release-owner to the same github:jpiquot identity.
  evidence: AC4 asks for distinct EventStore-owner and Release-owner acceptances, but the hash-bound roster and verifier currently authorize the same identity for both roles; separation of duties is not enforced and was not renegotiated in frozen intent.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Same working tree advances Epic 4 tracker rows and Story 4.5 LiveSidecar docs/ci prose beside Story 3.13.
  evidence: Story 3.13 Code Map limits docs/ci.md edits to the deployed-closure ownership paragraph and forbids scope leakage, yet the baseline diff also includes Epic 4 status moves and LiveSidecar prose outside that ownership scope.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Retained fail-closed runtime-verification.json remains schema v1 without pass-path v2 command/smoke_results shape.
  evidence: ValidateRuntimeExecution now requires hexalith.eventstore.story-3-13-runtime-verification/v2 with command and smoke_results, while the live fail-closed citation is still v1; reopen owners lack a documented migration to a closable pass packet.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: release_authority.verification reports fail under a hash-check method without separating scope failure.
  evidence: The crosswalk marks result fail while the method text says hash-checked durable predecessor authority, even though the concrete blocker is deployment_authorized false / quarantine-only scope rather than a failed hash.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 activation recovery can permanently starve publication-index entries beyond the fixed head scan and work budgets.
  evidence: `RearmOutstandingPublicationsAsync` always restarts at the first entry, persists no cursor, and schedules no continuation while a continuously active actor may never receive another activation.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 can report `Retryable=true` when only a recovery-index entry exists and reminder registration failed.
  evidence: The advisory status uses `drainReminderArmed || recoveryEntryTracked`, but an index entry does not itself activate an idle actor or guarantee an automatic retry.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Newly written normal command statuses leave `Retryable` null even though the public contract reserves null for legacy records.
  evidence: Most `WriteAdvisoryStatusAsync` call sites omit the new recovery parameters while `CommandStatusRecord` and `drain-failure-reason-codes.md` define null as a pre-field compatibility state.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: A successful Story 4.4 drain reports one fewer attempt than was actually executed.
  evidence: The success status writes `DrainAttemptCount: record.RetryCount`; the current successful reminder attempt is not included.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 defers exhaustion dead-lettering until the reminder after the retry count reaches its configured cap.
  evidence: Failure handling persists the capped count and returns; `CompleteDrainExhaustionAsync` runs only at the start of the next reminder, leaving terminal work and capacity retained for another interval.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Negative or overflowing persisted drain retry counts can evade or break the bounded-attempt guarantee.
  evidence: `UnpublishedEventsRecord.IncrementRetry` performs unchecked addition and the reminder path validates only `RetryCount >= MaxDrainAttempts`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: A crash after dead-letter broker acceptance but before the `DeadLettered` state save can publish the same exhausted range twice.
  evidence: The dead-letter sink and actor-state update are not atomic or idempotently coupled, despite Story 4.4's claim that the ordering prevents duplicate dead letters.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Persisted duplicate publication-index entries survive normalization and can leave stale capacity behind.
  evidence: `Normalize` removes only null elements, while refresh and removal operate on the first matching message ID despite the type contract promising de-duplication.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4's trailing optional parameters on public positional records are binary-breaking for already compiled consumers.
  evidence: The changes replace prior constructor and `Deconstruct` signatures on `CommandStatusRecord`, `CommandStatusResponse`, `DeadLetterMessage`, and `UnpublishedEventsRecord` without forwarding compatibility members.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The Story 4.5 evidence validator hashes current worktree files instead of the source blobs captured by its baseline commit.
  evidence: `validate_source_binding` reads `workspace / relative`, so ordinary later edits make the committed supposedly re-runnable evidence package fail independently of the captured revision.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: BMAD project-context sync can write `AGENTS.md` outside the selected project through a compass area path.
  evidence: `cmd_sync` joins unvalidated absolute or parent-traversing `area` values to `project_root` without resolving and enforcing containment.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: BMAD project-context sync can duplicate or remove user-authored text when managed markers are missing, reversed, or duplicated.
  evidence: `apply_block` validates neither marker cardinality nor ordering before slicing or appending the managed block.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: BMAD project-context sync does not re-anchor relative Markdown links that include fragments or query strings.
  evidence: `rewrite_links` only processes targets that literally end in `.md`, so links such as `decision.md#rationale` break when moved to another directory.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The installed BMAD project-context implementation lacks regression coverage for its filesystem-writing and resolution paths.
  evidence: The local suite contains three smoke tests and conditionally skips the referenced full Layer-1 suite, leaving resolve, sweep, compass, sync, remote, and cache behavior unverified.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 tests do not prove the publication-recovery index is staged before the event commit batch.
  evidence: The current test verifies only that `SetStateAsync("publication-index", ...)` occurred; moving it after the first `SaveStateAsync` would preserve the assertion while reopening the crash window.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 recovery fields are not verified through the hosted command-status HTTP wire contract.
  evidence: Tests inspect `OkObjectResult.Value` or persistence JSON but do not assert camel-case `retryable`, `recoveryReasonCode`, and `drainAttemptCount` properties in an actual endpoint response.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 4.4 drain-exhaustion safety fields are not verified after production dead-letter serialization.
  evidence: Tests inspect typed records and mocked publish calls but do not prove the wire payload carries the non-replayable flag, reason, committed range, and attempt count.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The BMAD renderer's documented empty `customization.workflow.open_spec` override has no regression test.
  evidence: Restoring the prior empty-value rejection would prevent `bmad-build` activation without failing any discovered renderer test.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: BMAD sprint planning permits the same numeric epic-story identity to produce multiple rows when titles differ.
  evidence: Duplicate detection compares the full title-derived key instead of the `(epic_num, story_num)` identity.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: BMAD sprint planning can crash outside its structured error contract on a malformed `development_status` value.
  evidence: `build_status` converts any truthy value with `dict(...)` without first requiring a mapping.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The Story 4.5 durability-race classifier does not reject contradictory actor acceptance and rejection/conflict signals.
  evidence: Simultaneous `ActorAccepted` and `ActorRejected` or `ActorConflictSignalled` values can flow into a nominal classification as internally consistent.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The Story 4.5 evidence validator can report success when Python assertions are disabled.
  evidence: Validation is implemented with `assert` statements and has no `__debug__` guard, so `python -O validate-evidence.py` removes the checks.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The Story 4.5 evidence validator accepts truthy non-boolean invariant values.
  evidence: `assert all(race["invariants"].values())` accepts strings such as `"false"` instead of requiring every value to be exactly `True`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The Story 4.5 source-binding validator does not fail when an evidence-relevant source path is omitted.
  evidence: `validate_source_binding` verifies only the rows present in `source-state.md` and has no exact expected-path set.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-31400593510-fix-ci-cd.md`
  summary: Live-lane packaging guardrail still matches only the exact `dotnet test tests/Hexalith.EventStore.Server.Tests/` substring and does not cover `.csproj`, unquoted alternate path, or `--project` equivalents.
  evidence: Blind-hunter and edge-case review of the CI fix noted realistic alternate invocation forms that would evade the exact-substring forbid while still running Server.Tests as a suite; hardening was deferred to keep this hotfix scoped to the failing CI assertions and Design Notes golden shapes.

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-31400593510-fix-ci-cd.md`
  summary: Builds `dapr-init` still uses one shared version for CLI install and runtime init, so EventStore cannot pin CLI 1.18.0 with runtime 1.18.1 without a submodule change.
  evidence: Integration failure 31413307050 and Ask First in the approved spec; restoring shared 1.18.0 unblocks CI but leaves CLI/runtime decoupling as a Builds enhancement.
  status: resolved 2026-08-11 — `runtime-version` now independently selects the Dapr runtime while omitted callers retain `version` as the legacy fallback.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The concurrent integration workflow's Dapr 1.18.0 runtime pin cannot reproduce the OQ8 packet's validator-pinned Dapr 1.18.1 fresh capture.
  evidence: The live fixture records the actual `daprd --version`, while the OQ8 validator requires 1.18.1 and the current workflow passes the shared 1.18.0 pin to `dapr-init`; resolving it requires the separately owned Builds CLI/runtime decoupling change.
  status: resolved 2026-08-11 — Integration now passes runtime 1.18.2 independently from CLI 1.18.0, fresh validation requires that explicit runtime, and immutable Story 4.14 remains pinned to observed runtime 1.18.1.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The concurrent live-lane guardrail can miss equivalent full Server.Tests invocations.
  evidence: Exact substring and selector-presence checks do not reject `.csproj`, `--project`, normalized/quoted paths, or a second unfiltered direct xUnit assembly invocation; robust command-level parsing belongs to the CI guardrail follow-up.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The concurrent Dapr-version guard can miss job-level or differently quoted YAML overrides.
  evidence: Its regex inspects only single-quoted definitions and checks the environment-variable reference separately from the initialization step, so an effective override can evade the assertion; structural YAML validation is required.

## Deferred from: Story 4.15 Step 4 review (2026-08-11)

- source_spec: `_bmad-output/implementation-artifacts/spec-gh-31415412092-fix-ci-cd.md`
  summary: Execute the shared `dapr-init` legacy runtime fallback with a fake Dapr executable at action level.
  evidence: Structural guards prove the fallback expression, but an action-level harness would additionally prove the resolved legacy value reaches the quoted `dapr init --runtime-version` invocation.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Make the selector-based Git subprocess harness portable to Windows.
  evidence: Current bounded nonblocking pipe handling uses POSIX selector behavior and is exercised only on non-Windows hosts.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Add one overall deadline spanning all Git identity subprocesses.
  evidence: Each Git call is bounded independently, but many sequential calls can exceed an operator's intended total validation deadline.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bound every validator input file before parsing or hashing it.
  evidence: Git output is bounded, but evidence, document, and JSON input sizes are not governed by one fail-closed limit.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Write sanitized fresh-capture outputs atomically.
  evidence: A process interruption can currently leave a partial test, support, or validation document in the capture directory.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Reject symlinks throughout fresh capture inputs and output directories.
  evidence: Final closure artifacts reject symlinks, while the fresh capture path and raw CTRF inputs do not apply the same policy.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Add cryptographic reviewer identity and attestation to Story 4.15 receipts.
  evidence: Receipts are content-bound by hash and exact reviewer text but do not authenticate who produced the approval.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Retain bounded raw execution logs or equivalent replayable command evidence for pre-review commands.
  evidence: The execution record preserves command identities and counts but not the underlying output needed to independently audit each reported result.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bind the closure-assembly commit identity after the Story 4.15 artifacts land.
  evidence: The packet binds the landed OQ8 capability commit and current path equivalence, but not the later commit that contains the closure layer itself.

## Deferred from: code review of spec-4-4-committed-event-publication-recovery (2026-08-11)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [HIGH] Dead-letter republish if mark-save fails after broker accept (reconfirmed, group-1 review).
  evidence: `CompleteDrainExhaustionAsync` publishes then `MarkDeadLettered` + `SaveStateAsync`; if save fails after broker acceptance, the next exhaustion turn publishes again. Already on ledger from prior 4.4 / mislabeled 3.13 entries; reconfirmed against `AggregateActor.cs:1674-1709`.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] `Normalize` does not dedupe duplicate MessageIds (reconfirmed, group-1 review).
  evidence: `UnpublishedPublicationIndex.Normalize` drops nulls only; duplicate MessageIds inflate `Count` toward capacity. Already on ledger; reconfirmed at `UnpublishedPublicationIndex.cs:148-157`.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md`
  summary: [MEDIUM] Commit-batch index staging order is not asserted by tests (reconfirmed, group-1 review).
  evidence: Existing commit-batch test asserts `SetStateAsync(publication-index)` occurred but not before the first commit `SaveStateAsync`. Already on ledger; reconfirmed against `AggregateActor.cs:688-727`.

## Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)

- ResolveWithin uses Ordinal StartsWith on Path.GetFullPath roots — case-insensitive hosts can theoretically mismatch path identity; Linux CI primary path is Ordinal-correct. [`DeployedRuntimeParityClosureTests.cs:5255`]
- FieldNameIsSupportSafe fragment matching can false-positive legitimate names (e.g. tokenizer) — no colliding fields in the Story 3.13 evidence schema today. [`DeployedRuntimeParityClosureTests.cs:4134`]
- LimitationsContainMutationProhibitions accepts weak keyword substrings — unrelated prose containing package/registry can pass. [`DeployedRuntimeParityClosureTests.cs:4282`]
- ResolveWithin TOCTOU between RejectReparsePoint and later file open — theoretical race after reparse checks. [`DeployedRuntimeParityClosureTests.cs:5260`]
- RunGit/ComputePinnedBuildsToolSha256 sync-over-async via GetAwaiter().GetResult() — test-helper style; not on product await paths. [`DeployedRuntimeParityClosureTests.cs:5334`]
- ValueIsSupportSafe misses private IPs embedded in non-URI free text — retained evidence is primarily structured JSON/URI values. [`DeployedRuntimeParityClosureTests.cs:4155`]

## Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)

- Sealed evidence packet no longer validates at HEAD — validate_source_binding hashes worktree files, and docs/ci.md plus docs/concepts/architecture-overview.md drifted via later commits; exits 1 with AssertionError: docs/ci.md, while all 17 rows were OK at 2321205b. Duplicates and confirms the pre-existing entry above. [`evidence/story-4-5/0776785f.../validate-evidence.py:178`]
- No test or CI step ever executes validate-evidence.py, unlike every sibling evidence directory which is pinned by a blocking Contracts.Tests/Packaging fact; the hash binding can decouple with all required checks green. Blocked on the item above. [`evidence/story-4-5/0776785f.../validate-evidence.py:203`]
- No binding between a committed capture and the receipt of the run that produced it — append-durability-race.json armedAtUtc falls inside the post-mutation window, not the race-test-results.json window; disclosed in prose but not machine-checked. [`evidence/story-4-5/0776785f.../commands.md:71`]
- retryCount derives from unfiltered AllocationAttempts while AppendDurabilityRaceControl is a singleton registered into both the primary and replica hosts; mitigated by serial collection execution and disclosed in allocatorIdentityLimitation. [`AppendDurabilityRaceSession.cs:148`]
- MetadataKey_StaleEtagUpdate_IsRejected no longer touches a metadata key; renaming would break commands.md, the validate-evidence.py MUTATIONS map, and the committed receipts. [`ActorConcurrencyConflictTests.cs:130`]
- Redaction gate uses `! rg …`, so an rg failure (exit 2) inverts to success and reports clean having scanned nothing. [`evidence/story-4-5/0776785f.../commands.md:135`]
- commands.md leaks errexit from the mutation wrapper, uses `exit 2` in the canonical-overwrite guard (closes an interactive shell), and runs the redact/hash block on unguarded variables. [`evidence/story-4-5/0776785f.../commands.md:89`]
- concurrency-conflict.md Common Causes bullet 1 describes an optimistic-transaction rejection that cannot arise on the current actor commit path, since nothing supplies an etag there; page is otherwise correctly hedged. [`docs/reference/problems/concurrency-conflict.md:14`]
- The ADD-fencing decision recorded as Deferred has no tracked owner or trigger — no append-fencing story exists in epics.md or sprint-status.yaml. The decision row is at `architecture.md:603`; the earlier `:558` citation pointed at a mermaid line. SUPERSEDED: tracked as the single structured entry "The ADD-fencing decision recorded in `architecture.md` still has no tracked owner story or trigger" in the loop-3 block below; do not action this bullet separately. [`_bmad-output/planning-artifacts/architecture.md:603`]

### Resolved by the 2026-08-25 re-capture (kept for history, no action)

- Story 4.5 provider profile is a source literal validated against itself (daprRuntime "1.18.1", redisImage "redis:6") and the two deterministic classes lack Collection/Trait attributes — deferred to the approved append-fencing follow-up, which must re-capture across multiple provider profiles anyway, so fixing runtime attribution and test placement is cheapest as part of that multi-profile capture. [`AppendDurabilityRaceLiveSidecarTests.cs:414`] RESOLVED 2026-08-26: the owner authorized fixing both in the loop-3 re-capture. Provider facts are now read at capture time (`daprd --version`, `docker inspect dapr_redis`, `redis-cli config get`) and both deterministic classes carry `[Collection("DaprTestContainer")]` + `[Trait("Category", "LiveSidecar")]`.
- Story 4.5 evidence packet left partially updated by the 2026-08-11 review: harness and validator patched (D1/D2/D4/D5) but the live re-capture could not run — DaprTestContainerFixture probes localhost:50005/50006 while Dapr CLI 1.18 publishes placement/scheduler on 6050/6060, and the local control plane is 1.18.2 against a packet claiming 1.18.1. validate-evidence.py fails until a fresh capture regenerates the receipts, source-state.md, and evidence-sha256.txt. [`DaprTestContainerFixture.cs:47`] RESOLVED 2026-08-26: the fixture now probes both candidate port pairs; `~/.dapr/bin/daprd --version` is genuinely 1.18.1 (only the placement/scheduler container images are 1.18.2, now disclosed in environment.md); the full re-capture and re-seal completed and `validate-evidence.py` exits 0.

## Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)

- Classifier completeness lives only in a docstring — "covers all twenty reachable classification names" is true today (verified: 20 distinct names, 22-row table covers all 20), but adding a 21st branch would fail no test. [`AppendDurabilityRaceClassifierTests.cs:44`]
- The dead-arm removal substitutes the literal `true` for the `rawConflictRejected` variable in the `RecognizedRejectionOrConflict` position; correct under the current infrastructure gate, but widening that gate would silently report a new status as a recognized conflict rejection, and no table row can cover the unreachable input. [`AppendDurabilityRaceClassifier.cs:151`]
- No schema history documents the `append-durability-race.json` 2 to 3 or `generic-etag-control.json` 1 to 2 version bumps that `validate-evidence.py` now hard-asserts; a future reader cannot distinguish a schema-3 capture from a schema-2 one without diffing the test source. [`validate-evidence.py:92`]
- `generic-probe-not-attempted` encodes harness state as a provider observation and can appear in a genuine non-mutation capture when `gateWaitException` short-circuits the probe block, not only when the key-addressability perturbation is armed. [`AppendDurabilityRaceLiveSidecarTests.cs:320`]

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Add an AppHost model test for the two new drain-bound environment forwards.
  evidence: Direct options-binding tests cover `MaxDrainAttempts` and `MaxOutstandingPublicationEntries`, but no normally run test proves the AppHost forwards either parent value to the `eventstore` resource.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Restrict append-durability race conflict recognition to known concurrency exception identities.
  evidence: `AppendDurabilityRaceLiveSidecarTests` currently treats every `InvalidOperationException` as a recognized concurrency conflict, so unrelated infrastructure failures can satisfy the evidence gate.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Reject contradictory sequence-two durability classifications when either writer reported rejection.
  evidence: `AppendDurabilityRaceClassifier` classifies both surviving writes plus one retry as consistent without checking the raw response or actor rejection flags.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Validate provider package signature evidence and canonical NuGet.org URLs.
  evidence: `RuntimeIdentityValidator.ValidatePackageManifest` requires the signature field and `nuget_url` property names but does not validate their values, allowing arbitrary signature objects or off-domain package URLs.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Fetch enough Git history for OQ8 integration evidence validation.
  evidence: `.github/workflows/integration.yml` checks out with `fetch-depth: 1`, while `validate-oq8-platform-evidence.py` requires the older landed source object `4b0a7b1d3628a857f131cfbff99030714aefc747` for tree, ancestry, and file checks.

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Validate OCI layer descriptors in the retained image graph.
  evidence: The retained `child-linux-*.manifest.raw` files carry seven layer descriptors each whose digests and sizes are never checked; `"layers"` appears in `DeployedRuntimeParityClosureTests.cs` only as `new JsonArray()` in synthetic fixtures, so the pass path validates layer-less manifests no registry would accept.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Exercise the release-provenance and deployment-authority validators against a real artifact.
  evidence: `ValidateRelease` and the deployment-authority path validate `release-provenance.json`, `deployment-authority.json`, and `deployment-authority-source.json`, none of which exist in the 21-file committed evidence directory; those code paths have only ever seen synthetic fixtures.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Publish the structured runtime-log schema outside the Story 3.13 test file.
  evidence: Retained smoke logs are line-oriented text (`platform=`, `container_state=running|0`, `attempts=18`) while the pass-path validators parse JSON objects with `child_digest`, `readiness_result`, and `failure_class`. Reopen trigger 5 asks the Hexalith.Builds smoke-contract owner to emit records against a schema specified nowhere outside `DeployedRuntimeParityClosureTests.cs`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Anchor and scaffold the AC4 acceptance-receipt location.
  evidence: `approval_contract.external_receipt_location` is the relative string `acceptances/{subject_sha256}` with no stated root, `required_receipt_fields` binds to no roster version, and the directory does not exist, so AC4 receipt collection cannot begin.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Bind the outer evidence manifest's own bytes to a hash.
  evidence: `evidence-sha256.txt` is absent from `evidence-core-sha256.txt` and unbound in `review-subject.json`. Mitigated because its entry set is structurally pinned by `ExpectedOuterFiles` and its listed hashes are recomputed against live bytes, so the practical exposure is narrow.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Disclose concurrent Epic 4 and docs changes carried inside the Story 3.13 review range.
  evidence: Epic 4 tracker rows and Story 4.5/4.14/OQ8/DAPR-pin prose in `docs/ci.md` land inside `1d6e9321..HEAD` from `fe715c70`, `ab1666dd`, `b927472a`, `35a1eecd`, and `86308550`. The proof packet's non-mutation attestation is scoped only to submodule gitlinks, so it under-discloses what its own reviewed range changed.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Handle child-process termination failure and cover the git wait timeout.
  evidence: `WaitForProcessExit` orphans a child when both the kill and the 5-second post-kill wait fail, and no test drives a git invocation past the 30-second window, so neither the previous nor the hardened timeout behavior is observed by the suite.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Refresh retained evidence `checked_at` timestamps after byte rewrites.
  evidence: `package-availability.json` declares `checked_at: 2026-08-04T11:17:05Z` and `registry-readback.json` declares `2026-08-04T11:48:07Z`, but both files were rewritten on 2026-08-09 for host-path redaction and the `cli_candidate_consequence` string.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: HIGH - Story 1.21 must repair Epic 1 frozen evidence corrupted by the SDK-token sweep in `089369bb`, under its own authority record.
  evidence: `089369bb` ("docs: clear remaining root predecessor SDK patch tokens", 25 files) rewrote `10.0.301` to `10.0.302` inside owner-approved Story 1.20 evidence. Story 3.13 restored only its own `fa2d1c99...` tree at `3d6dea69`. Genuine content mismatches remain at HEAD in `critical-evidence-sha256.txt` for `38f85086fc25...`, `4983299103bf...`, and `ec0d35a082bc...` (one `environment.txt` each). Story 3.13 must not write predecessor bytes again, so this needs a separate scoped story. Verified not affected: Story 3.13's `predecessor-tree-sha256.txt` passes 40/40, and the `nuget-sha256.txt` failures are missing proof packages, not corruption.
  status: resolved 2026-08-20 by Story 1.21. Durable evidence-owner receipt `25a01f60f8f231babb3db860dc8a59d2d46264f6cefe6db7f461fa615316d732` records the observed interactive approval and binds subject `ee5fb076bac380faa0b01ccd7aa96ec9f77955faa96f45c34aafc75d7bc8d26e` plus frozen-block digest `26c7a378bffb3a90eee0fe037aeeeec2e16a290ed91fadd5b4a4db6219db7e92`. The subject restored exactly the three pinned parent blobs; all critical manifests pass 33/33, packages remain independently unavailable at 0/14 per tree, broader Story 1.20 drift is zero, and `bmad:murat` verification binds result `e22e1aef2d24fea81d49ce5e9f495d4ff8d02e989da8b37c1937b517a477e3ab`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Reseal or revert Story 4.5's self-invalidating evidence packet.
  evidence: `evidence/story-4-5/0776785f.../validate-evidence.py` was modified by `3e365150` after the packet was sealed at `86308550`; `sha256sum -c evidence-sha256.txt` now reports a genuine content mismatch. Found incidentally during the Story 3.13 evidence-integrity sweep; not a Story 3.13 defect.

## Deferred from: code review of spec-frontcomposer-11-24-runtime-identity-successor (2026-08-12)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md`
  summary: Exercise every provider-state response through the normal provider-verification lane.
  evidence: The registry test invokes most of the 19 state seams but discards their results, while the sole real-Kestrel Pact test covers only `command-unauthorized`; incorrect HTTP-visible results for the remaining states can therefore remain green.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md`
  summary: Add an intentionally mismatching Pact test for contract-failure classification and process exit.
  evidence: The only executable `PactInteractionVerifier.VerifyAsync` test covers a matching Pact; no test proves native exit code `1` becomes `interaction.contract-failed`, a failed final verdict, and process exit code `4`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md`
  summary: Verify Pact playback continues after runtime-identity drift when host startup succeeds.
  evidence: Identity tests cover validator flags and the 19-input application test injects startup failure, so no executable test proves a mismatched identity still runs all 19 interactions and retains the identity failure verdict.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md`
  summary: Add an AppHost model test for the drain-bound environment forwarding contract.
  evidence: Direct option-binding tests cover `MaxDrainAttempts` and `MaxOutstandingPublicationEntries`, but no normal test proves AppHost forwards either value to the EventStore resource environment.

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-13)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Pin the restored sprint-status decision comments with a guard.
  evidence: 116 comment lines across 24 keys were restored from baseline `1d6e9321`, but only the three Story 1.20 lines are protected by a test. The restoring finding's own text warns "the next YAML round-trip will delete the rest again". The restoration is correct; the guard gap predates this chunk.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Record a disposition for every checked chunk-2 patch bullet.
  evidence: Eighteen of the twenty-five `[Review][Patch]` bullets in the chunk-2 block are checked `[x]` while still reading as the raw finding; only seven carry an "APPLIED 2026-08-12" note, so the record does not say what changed for the rest.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Make `RebindIndex` fail loudly instead of throwing from `Directory.Move`.
  evidence: `DeployedRuntimeParityClosureTests.cs:6441` moves the evidence directory to a digest-named path; when a mutation leaves the index bytes unchanged, or the `manifests` array is empty, the move raises `IOException` rather than exercising the rejection path the case was written for.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Share one `archive_root` separator normalizer between the package validators.
  evidence: `ValidatePackageBytes` (`DeployedRuntimeParityClosureTests.cs:2771`) and `ExpectedCoreFilesFor` normalize `archive_root` independently, so repeated or platform-alternate trailing separators can make the two validators disagree on the same recovered 14-archive set.

## Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Replace the five Windows early-return vacuous passes in the container-publishing governance suite with real skips.
  evidence: `ContainerPublishingGovernanceTests.cs` returns early at lines 207, 239, 266, 287, 443 and 485 under `OperatingSystem.IsWindows()`. An early return is an xUnit pass, so AC1's "zero-skipped coverage" is satisfied by construction on Windows. Only line 287 is new in this chunk; the other five predate it.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Clean up the release-evidence codec hygiene cluster.
  evidence: `_nuspec_identity(package_bytes)` is called with a `Path` and immediately does `zipfile.ZipFile(Path(package_bytes))`; `_parse_timestamp` uses `value.replace("Z", "+00:00")`, replacing every `Z` rather than a trailing designator; `validate_identity:441` compares the index digest to `children[0]` only, never to `children[1]` nor the two children to each other; `EXPECTED_PACKAGE_COUNT = 14` is a fourth uncross-checked copy of the package count; and `validate_packet_files` re-hashes each Builds helper immediately after `_verify_bound_file` performed the identical check. All are gated by the codec re-freeze decision.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Give `observations.json` semantic validation instead of checksum-only coverage.
  evidence: The codec never opens `observations.json`; it is bound only through `packet-sha256.txt`, which is regenerated whenever the packet is rebuilt. The GitHub Release asset list and the "all 14 visible on NuGet.org" claim therefore rest on an unvalidated file. Cross-checked by hand during this review: all 14 `github_release.assets` digests and sizes do match `packages[].sha256`/`size`, so the claim is factually true today.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Decide whether the OCI image index should carry provenance annotations.
  evidence: `_PublishMultiArchContainers` passes no labels to `CreateImageIndex`, and `validate_packet_files` checks the index only for `schemaVersion`, `mediaType` and two descriptors. The multi-arch tag — the artifact a registry UI surfaces — has no `org.opencontainers.image.*` metadata, and no test asserts either way. The spec requires labels on the child configs only, so this is out of the current contract.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Cross-check the three JSON canonicalisers against one shared fixture.
  evidence: `canonical_bytes` (`release_evidence_codec.py:69`, compact, `ensure_ascii=False`), `_publisher_canonical_bytes` (`:489`, `indent=2`, default `ensure_ascii=True`) and the C# `CanonicalJsonBytes` (`CorrectiveOciProvenanceReleaseTests.cs:1011`, `Utf8JsonWriter` default `JavaScriptEncoder`) can diverge on non-ASCII and HTML-sensitive characters. The tests work around this by re-canonicalising `release-identity.json` through Python only; no test asserts the three encoders agree byte-for-byte.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Bound nuspec parsing against oversized archives and entity expansion.
  evidence: `_nuspec_identity` (`release_evidence_codec.py:466`) calls `element_tree.fromstring` on a nuspec read straight out of a retained `.nupkg` with no size cap and no entity-expansion defence. The packet bytes are repository-controlled today, so this is hardening rather than a live exposure.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Prove the issue-comment snapshot is complete before asserting "exactly one authority and one receipt".
  evidence: `release_evidence_codec.py:770-790` validates ordering, uniqueness and issue affinity of the retained snapshot but has no total-count or last-page marker, so a truncated or paginated snapshot can satisfy the exactly-one authority and exactly-one receipt claims on incomplete data.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Decouple the authority-window theory from the frozen timestamps and split the seven-scenario mutation Fact.
  evidence: `RetainedAuthorityRejectsInvalidWindowAndEditedRecord` InlineData sits exactly one second off the frozen `created_at` (`2026-08-20T11:06:06Z`); if the window check ever passes, the assertion falls through to an opaque summary-mismatch error instead of the intended message. Separately, `CanonicalReleaseIdentityBindsRetainedBytesAndRejectsMutations` packs seven independent mutation scenarios into one ~180-line `[Fact]`, so the first failure hides the other six.

## Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, D5 disposition)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  owner_repo: `Hexalith.Builds` — reusable `.github/workflows/domain-release.yml`. NOT owned by `Hexalith.EventStore`; the EventStore `release.yml` only calls the reusable workflow, so this fix cannot land here.
  summary: Split the governed release path into its own reusable workflow file so legacy callers stop having to grant `attestations: write` and `id-token: write`.
  evidence: GitHub validates the maximum permissions across every job in a called workflow, including jobs that never run. Because `governed-release` (`domain-release.yml:478`) declares both scopes, every caller must grant them — EventStore's `release.yml` now does. The legacy `release` job (`:240`) declares no `permissions:` block, so it inherits the caller's set and executes in the protected `production` environment holding both write scopes unused. The obvious narrow fix — an explicit `permissions:` block on the legacy job — is blocked by an existing Builds contract test, `test_governed_release_workflow.GovernedOffParityTests.test_only_the_governed_job_requests_attestation_permissions`, which asserts `assertNotIn("permissions:", job_slice(workflow, "release"))`; that shape was tried during this review and reverted. Splitting the two paths into separate reusable workflow files removes the coupling without contradicting that contract. Epic 3 explicitly withholds signing/SBOM/attestation authority, so the grant should not persist longer than necessary.
  severity: medium
  status: accepted — ratified for now (Story 3.14 D5 option A); nothing is signed or attested because `governed-release: false` keeps the governed job skipped, and `ContainerPublishingGovernanceTests` pins that input.

## Deferred from: CI/CD xUnit v3 restore failure fix (2026-08-21)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-32485211318-fix-ci-cd.md`
  summary: Unify Roslynator package families under a single `roslynator` family in Hexalith.Builds central package audit.
  evidence: `Roslynator.Analyzers` and `Roslynator.Formatting.Analyzers` are declared under separate single-package families (`package:roslynator.analyzers` and `package:roslynator.formatting.analyzers`) rather than a coordinated suite in `Get-PackageFamily` (`audit-central-package-versions.ps1`).

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-32485211318-fix-ci-cd.md`
  summary: Link `FsCheck.Xunit.v3` to the `xunit` rollback group in Hexalith.Builds package audit.
  evidence: `FsCheck.Xunit.v3` is tracked in a separate family (`package:fscheck.xunit.v3`) rather than being linked to the `xunit` family or rollback group, which risks partial upgrades across dependent testing packages.

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Commit `56aa0fec` does not describe the change it carries; the Story 3.13 disposition verifier landed under an unrelated `release_evidence_handlers` subject.
  evidence: `56aa0fec` is titled `feat(release_evidence_handlers): add v3 codec for corrective release packet and initial handler setup`, yet it carries the 2,514-line Story 3.13 disposition verifier in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`, the disposition envelope and its sidecar, the story record, both spec files, `docs/ci.md`, and `sprint-status.yaml` — 8 of its 23 files are Story 3.13 scope. Conventional Commits is a tracked project rule and semantic-release consumes these subjects. Already merged to `main`, so not fixable without history rewrite.
  severity: medium

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `ForeignLineageTokens` is hand-maintained with no completeness guard.
  evidence: `DeployedRuntimeParityClosureTests.cs:113-127` omits the two explicitly voided subject digests `394292a2…` and `93d70d51…` and the historical proof-packet digest `349e0998…`. Compounding this, the retained subject's own `limitations[4]` names `394292a2` and `fa2d1c99` as void facts, and `limitations` is not among the six sections `RejectForeignLineage` scans (`DispositionIdentitySections:198-206`), so those tokens would not be caught there either. Already recorded on the spec Defer list.
  severity: medium

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Malformed provenance labels beyond the declared three can neither pass nor be declared.
  evidence: `DeployedRuntimeParityClosureTests.cs:4313-4360` rejects any retained config label whose value equals `MalformedLabelValue` but is absent from `MalformedProvenanceLabels`, while the cardinality check `malformed.Length != platforms.Length * MalformedProvenanceLabels.Length` simultaneously forbids declaring the extra rows. Not live for the frozen `v3.94.1` configs (exactly 3 labels × 2 platforms, verified on disk); a robustness gap only for a successor candidate whose configs differ.
  severity: low

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Two canonicalizers define one authority with no equivalence test.
  evidence: Python `canonical_bytes` (`tools/release_evidence_handlers/v3.py:76`, reached via the 11-line `tools/release_evidence_codec.py` facade) authors the envelope bytes, while C# `CanonicalDispositionBytes` verifies them; nothing tests that the two agree for non-ASCII input or line separators. Already recorded on the spec Defer list, but the Code Map still points at the pre-facade `release_evidence_codec.py:74`.
  severity: medium

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Receipt `source_url` requires a GitHub commit anchor that cannot exist; the existing deferral's "pre-existing pattern inherited" rationale is false and is corrected here.
  evidence: `DeployedRuntimeParityClosureTests.cs:4910-4912` requires `…/commit/<SelectedSourceSha>#story-3-13-disposition-<envelopeHash>-<role>`. GitHub mints `#commitcomment-<id>`, so the 3/3 story-completable path is reachable only from `CreateDispositionReceipts` fixtures. The spec Defer list attributes this to a pattern "inherited from `ValidateAcceptances`", but that helper uses a different, subject-keyed anchor `#story-3-13-<subjectHash>-<role>` (`:6613`) — the disposition anchor format is newly authored by commit `56aa0fec`. The durable source record lives in `sources/` inside the same directory as the receipt, so anyone who can author the receipt can author its source: the cross-check proves consistency, not independence. Contrast `LoadReviewerRoster:7010-7024`, which constrains `authority_source` to an https github.com issue-comment URL. Owner decision 2026-08-21: keep the deferral, correct the rationale; non-blocking while 3/3 receipts remain uncollected.
  severity: medium

## Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Mark or re-tier the heavyweight container-publish theories so the CI-gating Contracts lane is not paying for real `dotnet publish` cycles.
  evidence: `ContainerPublicationRejectsMissingProvenanceInputs` runs two full `dotnet publish -t:PublishContainer` cycles at an 8-minute budget each and `ContainerPublicationRejectsMalformedProvenanceInputs` runs four `dotnet msbuild -t:ValidateContainerProvenanceInputs` invocations, all inside `Hexalith.EventStore.Contracts.Tests`, which `.github/workflows/ci.yml` runs as a blocking deterministic gate. Nothing marks them excludable from a fast lane, and the two theories use inconsistent proof strategies (real publish versus direct private-target invocation) for the same guard. Pre-existing: the real-publish pattern arrived in an earlier Story 3.14 round.
  severity: low

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: Document how a second corrective release adds a `v4` evidence handler; the v3 handler is a deliberate single-packet allowlist with no successor and no procedure.
  evidence: `tools/release_evidence_handlers/v3.py:15` pins `EXPECTED_PACKET_CODEC_SHA256 = 814502bd…` and `:211` additionally rejects `codec["version"] != CODEC_VERSION`, so v3 accepts exactly the one retained codec digest of the frozen v3.96.2 packet. `tools/validate-corrective-release-evidence.py:14` has a single `HANDLERS` entry. Separately, `V3_PUBLICATION_PREFLIGHT_SHA256 = 830af8af…` is the *executed* `eadddc7b` shared preflight; the currently pinned `a07078ad` (and the development gitlink `307a043`) hash to `fe5ffc3f…`, so the legacy role-evidence branch is already closed to anything produced by today's pin. That is correct fail-closed behaviour for the frozen packet but leaves the next corrective release with no documented path, and no `docs/` page describes the `release_evidence_handlers` package or the dispatch table at all.
  severity: medium

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: The deferred-work ledger itself records a stale publication pin.
  evidence: `deferred-work.md:14` describes the release-skip race entry's owner_repo as "currently pinned in this repo as `builds-execution-sha: cf04c419378dfe1bd3c41a9244b5e3283092056e`"; the caller has since rotated through `63409393…` to `a07078ad…`. The ledger is append-only legacy-advisory format, so this is recorded rather than edited in place.
  severity: low

## Deferred from: bmad-build review closure of Story 3.13 (2026-08-22)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Replace the fixture-only Story 3.13 durable-source URL anchor with a GitHub-minted immutable acceptance reference before collecting the three production receipts.
  evidence: `RejectDispositionReceipt` requires `#story-3-13-disposition-<envelope-sha256>-<role>` on a commit URL, but GitHub commit-comment anchors use `#commitcomment-<id>`. The retained source record currently proves consistency with its receipt, not independent external existence. The owner accepted deferral while the disposition remains at 0/3 receipts; this does not authorize Story 3.13 completion.

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `PathIsWithin` (backing the new `disposition.location`/`disposition.directory` guards) has no reparse-point resolution.
  evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:8187-8194` compares `Path.GetFullPath(...)` results with an ordinal `StartsWith`, unlike `ResolveWithin` elsewhere in the file, which is reparse-point safe. This reproduces this story's own already-deferred `ResolveWithin` ordinal-`StartsWith`/TOCTOU weakness class in a brand-new guard rather than reusing the hardened helper. Not live risk for the current developer-authored evidence tree; a robustness gap if the disposition directory is ever attacker-influenced.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `review_loop_iteration` frontmatter metadata does not track the number of review passes the spec itself narrates.
  evidence: `spec-3-13-deployed-runtime-parity-closure.md:7` stays `1` although the 2026-08-22 diff alone narrates three distinct passes (the 2026-08-21 loop, its loop-1 historical ledger, and the 2026-08-22 closure). Cosmetic; the same field was previously corrected from `7` to `13` in an earlier chunk of this story.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: "Review Closure" sections collapse many granular historical findings into a few broad bullets, discarding per-finding traceability.
  evidence: The "Review Closure (2026-08-22)" section (`spec-3-13-deployed-runtime-parity-closure.md:210-218`) resolves roughly twenty individually-numbered findings from the preceding historical ledger via 5 broad bullets, one of which alone bundles eight unrelated changes, while explicitly leaving every underlying checkbox unchecked ("authoritative over the unchecked historical rows above"). A future auditor cannot trace a specific historical finding to its specific resolution. Same documentation-completeness gap already noted once before in this story's chunk-2 review ("eighteen of the twenty-five chunk-2 patch bullets are checked with no disposition text").
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The `depends_on_corrective_release` / `corrective_release_owner: "3.14"` intentional-pairing claim in `docs/ci.md` is untested.
  evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4549-4558` correctly gives `depends_on_corrective_release` its own diagnostic separate from the authorization-flag group, but `docs/ci.md:375`'s claim that a `true` value paired with `corrective_release_owner: "3.14"` is an intentional, non-authorizing scheduling reference (not a dependency) is asserted by no test.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `MalformedProvenanceLabels` (a cached static field) and its new platform/config-file counterpart (a recomputed local) are inconsistent, and the local re-reads/re-parses `index.raw` from disk on every call.
  evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4317-4318` derives `platforms`/`configFiles` locally from the retained index children on each invocation of `RejectDispositionDefects`, instead of caching once like the sibling `MalformedProvenanceLabels` field. Minor repeated I/O, not a correctness defect for the current two-platform fixture.
  severity: low

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 3)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The Story 3.15 Test Architect receipt (`bmad:murat`) has no externally-checkable anchor comparable to the two GitHub-issue-comment-backed owner receipts.
  evidence: `evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/acceptances/bb58d691.../test-architect.json` is sourced from a `bmad-test-architect-record` (self-attested by the assembling tooling), unlike the `eventstore-owner`/`release-owner` receipts, which are independently verifiable via `gh api repos/Hexalith/Hexalith.EventStore/issues/comments/<id>`. Reproduces the same durable-receipt-anchor gap already tracked above for Story 3.13's disposition receipts, now recurring for Story 3.15; this is the project's established pattern for `bmad:`-role receipts generally, not a defect unique to this diff.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `closure.json` declares `deployed_runtime_parity: "available"` and a non-null `selected_deployed_identity` even when `acceptances.receipts` is empty.
  evidence: Confirmed unchanged from `HEAD` (pre-existing, not introduced by the 2026-08-22 loop-3 diff) via `git show HEAD:.../closure.json`. The real gate is `_exact_list(receipts, 3, ...)` in `tools/deployed_runtime_parity_handlers/v1.py:360`, which fails closed regardless of those two fields' declared values, so there is no functional hole — but a consumer reading the JSON file directly instead of running `validate-corrected-deployed-runtime-parity.py` would misread pre-acceptance state as already authorized.
  severity: low

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `RejectDispositionManifest`'s new directory allow-list branch (`actualDirectories.All(allowedDirectories.Contains)`) has no negative test planting an unlisted stray directory.
  evidence: Every existing negative case for the disposition-manifest closed inventory (`resealed-stray-file`, `resealed-stray-acceptance-file`, `role-filename-mismatch`, `undeclared-sidecar`, `stale-envelope-directory`) plants a file, never an unlisted directory (e.g. `acceptances/<envelope-hash>/junk/`), so the directory-allow-list branch added in this diff is unproven. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5381-5394`.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `RejectDispositionManifest`'s directory/file enumeration is not reparse-point-safe, the same weakness class already deferred above for `PathIsWithin` (loop 2), now present at a second call site.
  evidence: `Directory.GetDirectories(dispositionRoot, "*", SearchOption.AllDirectories)` and `DispositionFilesUnder`'s `Directory.GetFiles(...)` call, unlike `ResolveWithin` elsewhere in the file, follow reparse points without exclusion. A symlink planted inside the disposition directory could evade the closed-inventory check the same way it could evade `PathIsWithin`. Requires repo write access to exploit. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5386-5392, 5401-5405`.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `DispositionSpecificLimitations` hardcodes three full sentences of frozen evidence prose as C# string literals with no automated cross-check against the frozen JSON file.
  evidence: A third, positionally-coupled source of truth for text that also lives in the frozen `review-subject.json`/envelope evidence — the same duplicate-source-of-truth pattern this diff fixed for the retained manifest arrays (`RetainedManifestFiles`/`-EntryCounts`/`-Bases`, tupled together) but left unfixed here. Evidence is frozen, so live drift risk is theoretical unless a future revalidation trigger amends it. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:188-206`.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `role-filename-mismatch`, `undeclared-sidecar`, and `stale-envelope-directory` silently moved from a soft acceptance-layer diagnostic to a hard whole-envelope failure, undocumented in Design Notes.
  evidence: Previously these three mutations left `Verified: true` and only blocked `story_may_be_done`, under reason codes `acceptance.receipt_set`/`acceptance.receipt_directory`. This diff moves them to the disposition-manifest layer, where they now set `Verified: false` under `disposition.manifest`. A real contract change to what `Verified` means for any caller, and it appears to be a strengthening rather than a regression, but the spec's Design Notes do not call out the shift. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:857-927`.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The "Suggested Review Order" section's absolute line-number anchors into four sibling files have no re-derivation task tied to them.
  evidence: Anchors into `3-13-deployed-runtime-parity-closure.md:802`, `docs/ci.md:357`, `sprint-status.yaml:225`, and `deferred-work.md:1410` (this file) will rot on the next edit to any of those files, the same anchor-rot bug class this spec elsewhere treats as requiring an explicit "re-derive Code Map anchors" checklist item. `spec-3-13-deployed-runtime-parity-closure.md#suggested-review-order` (cited by heading, not by line: the original `:331-354` citation had already rotted at authoring time, and the section moved again during loop 5).
  severity: low

## Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 3.15's lifecycle surfaces disagree as committed and no test cross-checks them.
  evidence: `sprint-status.yaml:227` moved `backlog` -> `in-progress` while `spec-3-15-corrected-deployed-runtime-parity-closure.md:5` carries the in-review token and `docs/ci.md` declares 3.15 validation passing with a selected identity. No Story 3.15 story record carries a `Status:` line, and `CorrectedDeployedRuntimeParityClosureTests.cs` contains no sprint/spec cross-check. Already tracked as loop 3's open patch at `spec-3-13-deployed-runtime-parity-closure.md:249`; owned by Story 3.15.
  severity: medium

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: One of the three Story 3.15 digests published in `docs/ci.md` is bound by no test.
  evidence: `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` asserts the subject `bb58d691...` and selected identity `4b141085...`, but the predecessor digest `4d1a0c33...` at `docs/ci.md:382-383` is asserted nowhere against ci.md — it appears only as a test constant, in validator stdout assertions, and as `PREDECESSOR_SHA256` in `tools/deployed_runtime_parity_handlers/v1.py:27`. A stale copy leaves the suite green while naming a predecessor the verifier would reject.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The date-rollover fix uses an optional `validationTime` parameter, preserving the silence that caused the original defect.
  evidence: `DispositionStoryMayBeDone(..., DateTimeOffset? validationTime = null)` at `DeployedRuntimeParityClosureTests.cs:4204-4212` defaults to real `DateTimeOffset.UtcNow`. Four of five call sites (`:2970`, `:3241`, `:3248`, `:3655`) still take that default; only `:3781` threads the fixture time. Correct for those fixtures today; the recurrence trap is that a future fixture-time test can forget to thread it and silently stop firing.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Ledger hygiene across the six appended blocks, with nothing validating any of it.
  evidence: `source_spec` switches from absolute to repo-relative paths mid-file; the "bmad-build review closure of Story 3.13 (2026-08-22)" entry near `deferred-work.md:1409` is the only new entry with no `severity:` key; two entries filed under a Story 3.13 heading declare a Story 3.15 `source_spec`, so heading-grouped and `source_spec`-grouped sweeps disagree; and the reparse-point weakness is deferred twice with no shared id. The DW6 governance suite is 19/19 skipped (`[Fact(Skip = "ATDD red phase -- DW6 deferred-work governance checker and story artifacts are not implemented.")]`), and the executable AWK gate in `ProofPacketValidatorIntegrityTests.cs:864-870` is scoped to three Story 1.20 headings only.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The historical-tree location guard is proven against a fabricated repository layout.
  evidence: `DispositionInsideHistoricalEvidenceTreeFailsClosed` (`DeployedRuntimeParityClosureTests.cs:3088,3101`) passes `cleanupRoot`, a temp directory, as `repositoryRoot`, unlike its sibling `MisnamedDispositionDirectoryFailsClosed` which passes the real root. The disjunct is exercised against a synthesized `nested/<sha>` path rather than the real frozen `fa2d1c99...` tree, and passes only because `disposition.location` is diagnosed before any missing-repository-file check.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: The new checksum-manifest mutation theory omits non-string and whitespace-only `file` values.
  evidence: `DuplicateOrEmptyChecksumManifestDeclarationFailsClosed` (`DeployedRuntimeParityClosureTests.cs:3456-3474`) covers duplicate and empty `file` values only. A non-string JSON value (e.g. `42`) or a whitespace-only value would route to `internal.exception` or to a different diagnostic rather than the `envelope.retained_checksum_manifests` code the theory asserts.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: `docs/ci.md` flattens the known receipt-authenticity asymmetry for Story 3.15.
  evidence: `docs/ci.md:397-399` states the EventStore owner, Release owner and Test Architect "have provided real authenticated receipts". The two owner receipts are GitHub-issue-comment backed and independently checkable via `gh api`; the `bmad:murat` Test Architect receipt is sourced from a `bmad-test-architect-record`, i.e. self-attested by the same tooling that assembled the packet — an asymmetry this same ledger already records for Story 3.13's disposition receipts.
  severity: low

## Deferred from: Story 3.13 acceptance collection (2026-08-24)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
  summary: Story 3.13's three role-bound acceptances are a self-attestation, not independent three-party review.
  evidence: The packet-bound roster maps `eventstore-owner` and `release-owner` to the same account (`github:jpiquot`), and `test-architect` to `bmad:murat`, a tooling-attested record with no external anchor. The 3/3 gate at `evidence/story-3-13/disposition/6cee8dad.../acceptances/a7ecd455.../` is therefore satisfied by one human plus a bmad record. The two owner receipts are genuinely GitHub-minted and independently re-fetchable (comments 5395155800 / 5395155988 on issue 351), so the evidence is authentic; what is absent is reviewer independence. Same pattern already tracked for Story 3.15's `bmad:murat` receipt.
  severity: medium

## Deferred from: code review of spec-3-14-corrective-oci-provenance-release.md (2026-08-24)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: The development-gitlink guard encodes "deliberately independent of the release pin" as a permanent inequality, so a legitimate submodule bump onto the release pin fails a test with no failure meaning.
  evidence: `ContainerPublishingGovernanceTests.cs:539` asserts `gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha)`. The documented property is that the `uses:` ref and `builds-execution-sha` agree with each other and that the gitlink is read independently (`git ls-tree`), not that the gitlink may never equal `a07078ad…`. The current gitlink is `2f46aaee…`, so the assertion cannot fire today; it becomes a false red the first time an ordinary `build(deps)` bump happens to land on the pinned revision. Pre-existing — introduced before the chunk-3 range (`94591f35`..`da52e2c8`) and untouched by it.
  severity: low

- source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  summary: No executable guard asserts that the release caller's pinned reusable-workflow SHA is reachable on the Hexalith.Builds remote rather than only in a local object store.
  evidence: `.github/workflows/release.yml:103,110` pin `builds-execution-sha`, and the story record's warning that a rotation target must exist on the remote was deleted in `f2d2575c` with nothing replacing it. This is the defect that produced the chunk-A+B blocking Decision, when `63409393…` was pinned while it existed only on an unpushed branch (it has since been merged to Builds `main` and superseded by `a07078ad…`). Deferred 2026-08-24 by owner decision: an unresolvable `uses:` SHA already fails the Release dispatch at startup — the quarantined run `32347773728` failure mode — so nothing publishes silently, and every candidate guard costs either network plus auth inside the Tier-1 CI-gating Contracts lane or a `origin/main` remote-tracking ref that a CI submodule checkout may not populate (and which reds on force-pushes it should not judge). The recurring drift class is closed separately by binding the `docs/ci.md` pin prose to `ApprovedBuildsReleaseSha`.
  severity: low

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The Story 3.13 closure-packet gate `ValidateAcceptances` still enforces the unmintable `#story-3-13-<hash>-<role>` commit anchor that only a fixture can satisfy.
  evidence: `DeployedRuntimeParityClosureTests.cs:7378` builds `ApprovedSourceSha + "#story-3-13-" + subjectHash + "-" + role` and `:7405,:7409` require `retained-immutable-external-record` and `acceptance-source/v1`, while the disposition path moved to `/v2` and `github-issue-comment` (`:74,:5400-5412`). Live at `:1011` and at `:6144` inside the `story_may_be_done` gate. A genuine GitHub-collected receipt is rejected by it; the synthetic `CreateAcceptanceReceipts` fixture is its only witness. Same defect class Story 3.13 was reopened to remove.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `author_association` requirements diverge between the registry authority source and acceptance receipts, and the divergence was resolved downward to keep real evidence passing.
  evidence: `_validate_receipts` requires MEMBER/OWNER/COLLABORATOR (`v1.py:794,:809`); the retained roster comment `registry/role-registry-source.json` is CONTRIBUTOR, so `_validate_registry` admits CONTRIBUTOR too. Owner decision needed: tighten the registry to match the receipts (requires a new roster comment) or record the weaker bar as intended.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The OCI `created` provenance labels are self-comparing in tests and unchecked by the codec, and the retained child configs carry a malformed truncated value.
  evidence: `CorrectiveOciProvenanceReleaseTests.cs:118` sets `expected ??= ExpectedLabels(observedCreated)` where `observedCreated` is read from the first child config, so child 1 compares to itself; `v3.py:134 _expected_labels` omits `created` from the five enforced keys. Both retained configs carry `org.opencontainers.image.created = "2026-08-20T11"`, truncated at the first colon, inside the selected identity.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Two Production-smoke guards are green by construction -- `redirect_count` and `observed_runtime_platform` can never disagree with what they are checked against.
  evidence: `capture-corrected-deployed-runtime-parity-smokes.py` invokes `curl` without `--location`, so `num_redirects` is structurally 0 and the verifier's `redirect_count != 0` check cannot fire; `observed_runtime_platform` comes from `docker image inspect {{.Os}}/{{.Architecture}}`, i.e. the metadata `--platform` already selected. Separately, `smokes/*.log` are canonical JSON restatements of `smoke-results.json`, so the log-versus-summary comparison is between two hand-written documents rather than a retained transcript.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `FrozenStory314PacketRemainsByteForByteUnchanged` hashes a single file despite asserting whole-packet immutability.
  evidence: The test re-hashes only `release-identity.json` and runs the 3.14 validator; every other file in the frozen packet could be rewritten with the test still green.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The `_bmad-output/test-artifacts/` gate artifacts backing the Test Architect receipt disagree with the matrix they summarize and cite a nonexistent test method.
  evidence: `traceability-matrix.md` lists S315-UNIT-001 as `CheckedInTechnicalPacketFailsClosedUntilThreeReceiptsExist`, which exists nowhere; every listed line number is 1-3 low, indicating the matrix was generated before the final edits. `e2e-trace-summary.json` reports `cases: 19` against the matrix's 48, `pct: 100` on zero P1/P2/P3 totals, and `evaluator: Administrator` while the matrix signs off as `bmad:murat`. The files also sit outside the hash-closed packet and are bound by nothing.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The Hexalith.Builds gitlink was rotated to the tip of origin/main while the release workflow pin was left behind.
  evidence: The gitlink moved to `22a578b5` (== `origin/main`), which changes `Github/publish-containers/publication_preflight.py` and `publish-containers.sh` -- the executed release helpers -- but `.github/workflows/release.yml` still pins `a07078ad`. The Builds-side counterpart of this diff's preflight tightening is therefore not in the executed release path, and rotation is supposed to happen from the pin rather than from main.

### bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: high
  summary: BLOCKING, OWNER DECISION -- the next Release run fails at container publish, after NuGet packages are already pushed, because the mandatory `ContainerProvenanceCreated` input is not supplied by the pinned Builds publisher.
  evidence: This change removed the `ContainerProvenanceCreated` fallback from `Directory.Build.targets` and added a hard `<Error>` at `Directory.Build.targets:67` requiring an exact UTC RFC 3339 second. `.github/workflows/release.yml:91` pins `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@a07078ad74d3727bc5a6b6d85d47d56a6e5c9fec`, and at that SHA `Github/publish-containers/publish-containers.sh:181-182` passes only `ContainerProvenanceSourceSha` and `ContainerProvenanceReleaseVersion`. The flag is passed only from Builds `22a578b5`, which is the submodule gitlink -- and a reusable workflow resolves from its `uses:` ref, not from the gitlink, so bumping the gitlink does not change what CI executes. Reproduced directly: `dotnet msbuild src/Hexalith.EventStore/Hexalith.EventStore.csproj -t:ValidateContainerProvenanceInputs -p:ContainerProvenanceSourceSha=... -p:ContainerProvenanceReleaseVersion=3.96.2` emits `Directory.Build.targets(67,5): error : ContainerProvenanceCreated must be an exact UTC RFC 3339 second.` Both SHAs are reachable on Builds `origin/main` (the reachability concern is separate, below). Resolution requires an owner decision between rotating the release pin (which also forces the gitlink further ahead, because `ContainerPublishingGovernanceTests` asserts gitlink != `ApprovedBuildsReleaseSha`) and restoring the fallback until the pin rotates. Deliberately not actioned in this loop: rotating a CI pin is outward-facing and belongs to the Story 3.14 lane.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: high
  summary: OWNER ACTION -- Story 3.15 has no dedicated acceptance issue, and the three superseded receipts were spliced onto Story 3.14's thread.
  evidence: Both superseded owner sources under `evidence/story-3-15/superseded-acceptances/bb58d691.../sources/` are anchored on `https://github.com/Hexalith/Hexalith.EventStore/issues/346#issuecomment-...`, i.e. Story 3.14's acceptance thread -- the cross-lineage reuse Story 3.13 was reopened to prevent. The verifier now rejects issues 324 and 346 by number, so re-collection requires a dedicated Story 3.15 issue. Opening it and requesting acceptances is an Ask First action and was not performed.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: No guard asserts the pinned Builds release SHA is reachable on the Builds remote; the only availability check reads the local clone.
  evidence: `ContainerPublishingGovernanceTests` asserts the pin only as a string, and `CorrectiveOciProvenanceReleaseTests` runs `git cat-file -e <sha>^{commit}` inside `references/Hexalith.Builds`, which a commit on an unpushed local branch also satisfies. A pin that exists only locally makes the reusable-workflow `uses:` ref unresolvable at dispatch -- the defect that already shipped once with `63409393`. Verified today that `a07078ad` and `22a578b5` are both contained in `origin/main`, so this is a missing guard rather than a live break.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: The `.gitattributes` normalization guard enumerates only `*.raw`, leaving 56 `.nupkg` and every digest-bound `.json`/`.txt`/`.log` file unguarded.
  evidence: `DigestBearingRawOciEvidenceIsBinary` enumerates `git ls-files "*.raw"` only. Deleting the `story-3-15/**/*.nupkg binary` line while keeping `story-3-15/** text eol=lf` turns all 14 story-3-15 packages into text, breaking every `packages.items[*].sha256` binding on a `core.autocrlf=true` checkout, with the suite still green on Linux CI. This loop added `*.py text eol=lf` for the SHA-pinned verifiers, but the enumerating guard was not generalized.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: The multi-RID `org.opencontainers.image.created` assertion compares the artifact to itself.
  evidence: `CorrectiveOciProvenanceReleaseTests` sets `expected ??= ExpectedLabels(observedCreated)` where `observedCreated` is read out of the first child config, so the emitted label is compared against itself rather than against the `-p:ContainerProvenanceCreated` input. Replacing the label value with a build-time `UtcNow` keeps the suite green. The indirection was necessary while the MSBuild fallback made the value unpredictable; now that the input is mandatory it is a stale weakening.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: The Story 3.13 closure acceptance contract still requires the commit anchor that GitHub cannot mint, while the disposition lane was migrated to `#issuecomment-<id>`.
  evidence: `DeployedRuntimeParityClosureTests.ValidateAcceptances` still builds `<commit-url>#story-3-13-<subject>-<role>` and requires source schema `.../v1`, so only the fixture can satisfy it; the disposition path was moved to the GitHub-minted anchor and `/v2`. The two acceptance surfaces now use different, mutually unsatisfiable anchor contracts. Out of lane for Story 3.15 and left to the 3.13 lane.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: The `_bmad-output/test-artifacts/` gate PASS was withdrawn this loop but not regenerated.
  evidence: `gate-decision.json`, `e2e-trace-summary.json`, and `traceability-matrix.md` were scored at `source_sha` 516f2489 against subject `bb58d691` and reported `PASS` with a vacuous `p1_status: MET` over an empty P1 set. They are now explicitly marked SUPERSEDED with a banner rather than regenerated, because the trace workflow owns their production.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: v3's timestamp parser is looser than v1's, so the frozen predecessor's timestamps are validated by the weaker rule.
  evidence: `v1._parse_time` requires a strict `YYYY-MM-DDThh:mm:ss[.ffffff]Z` shape and rejects naive datetimes. The v3 code that `v1` delegates predecessor validation to still uses `value.replace("Z", "+00:00")` (`tools/release_evidence_handlers/v3.py:456-465`), which replaces every `Z` in the string, accepts a space separator, and accepts arbitrary non-UTC offsets. The hardening stops at the module boundary. Deferred because `v3.py` is the frozen Story 3.14 verifier and any edit re-mints the Story 3.15 subject.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: Retained-file `size` has no upper bound and every retained and discovered file is read whole into memory.
  evidence: `_binding` requires `size` to be a positive integer with no cap, and `_verify_file` / `_validate_inventory` `read_bytes()` each retained file plus every file the inventory `rglob` walk discovers. A packet declaring or containing multi-gigabyte files exhausts memory before any verdict is reached. Bounded in practice by the packet being local and produced by the assembler.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: The dispatch-table consistency guards cannot fire with the current single-entry constant tables.
  evidence: `_verify_dispatch_table` (`tools/validate-corrected-deployed-runtime-parity.py:44-52`) and the two set-comparison checks in `_load_handler` (`tools/validate-corrective-release-evidence.py:66,72`) guard against a future misconfiguration -- registering a handler without pinning it -- that no current table can express, and no test constructs the inconsistent state. Accepted as future-proofing rather than removed.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: The Production smoke results file is never checked for canonical byte form.
  evidence: `_validate_smokes` binds the results file by digest and validates its fields, but unlike the receipt, subject, and registry paths it never asserts `results_bytes == canonical_bytes(results)`. Non-canonical whitespace simply yields a different subject digest rather than a forgery vector, so this is a consistency gap rather than a hole.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: All verifier failures collapse to exit code 1, so "the verifier itself was modified" is indistinguishable from "the evidence did not validate".
  evidence: `main()` catches `(OSError, DispatchError, ValueError, json.JSONDecodeError)` and returns 1 for all of them. For a supply-chain gate, `DispatchError` (tampered or unpinned handler) deserves a distinct exit status from `EvidenceError`. Deferred because `docs/ci.md` and the test suite assert the current exit contract.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: `"closure.json"` is hardcoded into the closed technical inventory while the CLI accepts an arbitrary evidence path and an independent `--packet-root`.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:746` excludes the literal `closure.json` from the stray-file sweep. A closure file under a different name at the packet root fails with a misleading "files outside the closed technical inventory"; a `--packet-root` pointing elsewhere leaves the closure file uncovered by the inventory entirely. Neither the argparse help nor the docstring records the constraint.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: The `summary_bindings` deletion reduces `validate_packet_files`' standalone behavior inside a line range the Code Map freezes, leaving a vestigial `summaries` dict.
  evidence: The diff removes the one-shared-two-platform-summary check from `v3.validate_packet_files`, a public entry point, inside the Code Map's frozen `v3.py:863-974` "preserve v3 behavior" range. It is redundant today only because every present caller invokes `validate_identity` first (`v1.py:445-452`, `validate-corrective-release-evidence.py:108/115`), where the identical constraint is enforced at `v3.py:397`. Confirmed independently by three review layers as not-lost-verification; carried here as a frozen-range and dead-code note. The now-purposeless `summaries` cache at `v3.py:944-952` invites the reader to assume a guard is still present.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The pinned release publisher cannot supply the newly mandatory container creation timestamp, while a governance test encodes release-pin/gitlink inequality as policy.
  evidence: `.github/workflows/release.yml` pins Builds `a07078ad...`, whose publisher omits `ContainerProvenanceCreated`; `Directory.Build.targets` rejects that omission, and NuGet publication precedes container publication. `ContainerPublishingGovernanceTests.cs` separately requires the release pin to differ from the development gitlink, obstructing the straightforward alignment fix. This belongs to the Story 3.14 release lane.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The legacy release job retains unused `attestations: write` and `id-token: write` permissions.
  evidence: `.github/workflows/release.yml` grants both permissions to the production release job although the current legacy path does not consume them. Removing or splitting them changes release-workflow authority and is outside Story 3.15's evidence-only boundary.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Container provenance URL validation accepts unrelated hosts and malformed percent escapes instead of enforcing the repository-derived canonical URLs.
  evidence: `Directory.Build.targets` checks only an HTTPS-shaped regex; values such as an unrelated repository URL or a path containing `%ZZ` pass and can enter OCI labels. Canonical URI derivation and validation belong to the corrective-publisher lane rather than this parity packet.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The container provenance creation-time regex accepts calendar-impossible dates.
  evidence: `Directory.Build.targets` bounds month and day fields independently, so a value such as `2026-02-31T09:15:00Z` satisfies the claimed RFC 3339 validation. Correct calendar validation is a publisher/input-contract follow-up.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The multi-RID provenance test compares the produced creation label to its first observed value rather than the supplied creation instant.
  evidence: `CorrectiveOciProvenanceReleaseTests.RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` feeds `observedCreated` into `ExpectedLabels`, so two children can share the same wrong valid timestamp and still pass. The test should compare both labels directly with its `Created` input.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The container default-tag test never observes the tag value it claims was defaulted.
  evidence: `ContainerPublicationDefaultsTagToProvenanceVersion` runs only `ValidateContainerProvenanceInputs` and checks exit zero; deleting or breaking the default assignment can leave that test green. A future publisher test should inspect the evaluated tag or produced archive.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The frozen Story 3.14 timestamp parser remains weaker than the strict Story 3.15 parser.
  evidence: `tools/release_evidence_handlers/v3.py` uses `value.replace("Z", "+00:00")` with `datetime.fromisoformat`, admitting spaces, arbitrary offsets, and other shapes that v1 rejects. Tightening the frozen predecessor contract is separate Story 3.14 evidence maintenance.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The packet inventory does not require the validated closure path to be the packet root's `closure.json`.
  evidence: `_validate_inventory` permits literal `closure.json` but checks only unexpected actual files, while the CLI accepts independent evidence and packet-root paths. A copied packet root without its own closure can validate against an external closure, which is a CLI/inventory contract follow-up.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The checked-in traceability gate artifacts remain superseded and do not cover the final Story 3.15 subject or current focused suite.
  evidence: `_bmad-output/test-artifacts/gate-decision.json`, `e2e-trace-summary.json`, and `traceability-matrix.md` explicitly describe a superseded collection while retaining PASS-shaped fields. Regeneration belongs to the trace workflow and is not evidence created by the parity verifier.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: A remaining Story 3.13 acceptance path still requires an unmintable synthetic commit-fragment source contract.
  evidence: `DeployedRuntimeParityClosureTests.ValidateAcceptances` retains the `#story-3-13-<hash>-<role>` commit anchor and v1 schema at live call sites, while genuine GitHub acceptances use issue-comment sources. This is Story 3.13 compatibility debt, not part of positive Story 3.15 closure.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The changed v3 issue-number normalization has no direct regression test.
  evidence: `release_evidence_handlers.v3.repository_issue_html_url` rejects padded and non-ASCII digits, but the existing `AuthorityHtmlUrlFollowsTheAcceptedIssueUrl` test exercises the sibling codec implementation. A v3-focused mutation test is needed in the predecessor-maintenance lane.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: `redirect_count == 0` is structurally unfireable and the new test now pins that property.
  evidence: The capture never passes `--location`, so curl's `num_redirects` is always 0; both the producer's `redirect_count == 0` and the verifier's `item["redirect_count"] != 0` (`tools/deployed_runtime_parity_handlers/v1.py:639`) can never fire. `CorrectedDeployedRuntimeParitySmokeCaptureTests.cs` now asserts `line.ShouldNotContain("--location")`, converting an already-acknowledged deferral into an asserted invariant.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: The post-execution import-shadow backstop runs only on the success path and no test reaches it with a repository module loaded.
  evidence: `_verify_no_repository_import_shadows` (`tools/validate-corrected-deployed-runtime-parity.py:164`, called at `:280`) sits inside the `try` after `validate_packet_files` succeeds, so it can invalidate a verdict but cannot prevent a shadow module's side effects. `RepositoryLocalStandardLibraryShadowCannotExecute` fails earlier at the receipt-count check, so making the backstop a no-op changes no test outcome. The `sys.path` half of the protection is genuinely covered.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: v3's timestamp parser is looser than v1's, so frozen-predecessor timestamps are checked by the weaker rule.
  evidence: `tools/release_evidence_handlers/v3.py:456-465`. Carried forward from loop 4; re-confirmed unchanged at HEAD.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: No size bound on retained files, and the nuspec decompression-bomb half of the earlier entry is still open.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:161-185` reads every retained and discovered file whole into memory with no upper bound on `size`. `tools/release_evidence_handlers/v3.py:436` still performs an uncapped `archive.read(nuspecs[0])`; loop 4's hardening closed only the entity-expansion half, so a small `.nupkg` declaring a huge `.nuspec` entry still expands unbounded before any check.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: All failure modes collapse to exit 1 and, for loader failures, to a single message that hides the chained cause.
  evidence: `tools/validate-corrected-deployed-runtime-parity.py:195-197` re-raises every `_load_verified_module` exception as `DispatchError("trusted live handler could not be loaded")`, and `main()` prints only `str(error)`, so a syntax error, a missing dependency and a tampered handler are indistinguishable. A tampered verifier is likewise indistinguishable from invalid evidence.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: Roughly 90 lines of security-critical loader code are duplicated across the two dispatchers with no sync test.
  evidence: `_is_repository_path`, `_module_is_repository_local`, `_begin/_end_trusted_import_environment`, `_load_verified_module` and `_verify_imported_file` exist in both `tools/validate-corrected-deployed-runtime-parity.py:113-243` and `tools/validate-corrective-release-evidence.py:85-162`, with divergent signatures (`relative` vs `path`) and the release copy missing the docstrings the parity copy carries. Nothing asserts the twins stay in sync, and the bytes-`TypeError` defect is present in both.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: Several distinct fail-closed branches share one message, so no test can show which clause fired.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:855` raises `GitHub acceptance source is not authenticated to the rostered owner` for eight or-ed conditions, and is the single expected message for both `ReceiptSourceAnchoredOnForeignLineageIssueFailsClosed` and all three cases of `ReceiptSourceIdentityMustResolveToOneComment`. The registry path has the same shape.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: The two timestamp-rejected owner comments are named in three documents but retained nowhere, and the `dab64f5f` pair was never annotated.
  evidence: `3-15-corrected-deployed-runtime-parity-closure.md:70-76`, the proof packet and `docs/ci.md` all state that comments `5409140199` and `5409147909` were marked `SUPERSEDED -- INVALID TIMESTAMP-MISMATCH ATTEMPT`, but no bytes for either are retained under `evidence/story-3-15/`, so the claim is unverifiable from the repository. The `dab64f5f` owner comments `5408186984`/`5408189299` received no equivalent annotation and remain acceptance-shaped JSON on the now-allowlisted `#352` thread; their rejection rests solely on `subject_sha256` inequality.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: The Code Map's frozen fence was extended and its line anchors were not refreshed.
  evidence: The Code Map marks `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235` as frozen ("do not extend its frozen candidate contract"). This change inserted 156 lines at `:895`, growing the file 1291 -> 1447, so the `1124-1235` anchor now lands on `CopyDirectory`/`LoadIdentity`/`MutateNuspecRepositoryUrl` instead of the helpers it named. The three added tests are dispatcher-trust tests rather than candidate-contract extensions, so the letter of the note holds, but the anchors are stale.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: Two submodule gitlink bumps rode into a Story 3.15 evidence commit undeclared.
  evidence: Commit `67c645ab` bumps `references/Hexalith.FrontComposer` `a229be7e` -> `596e286f` and `references/Hexalith.Tenants` `09c746b3` -> `daf6c76c` alongside the spec entry that asserts "No replacement acceptance, deployment, publication, registry, consumer, predecessor, commit, or push action was performed." Neither the spec change log, the story record, `deferred-work.md` nor `docs/ci.md` mentions them. Both targets were verified contained in their submodules' `origin/main`, so no dangling gitlink is published -- this is unrecorded scope, the known concurrent-loop absorption pattern.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: low
  summary: `RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` is build-state dependent, not code dependent.
  evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:55-88` shells out to `dotnet publish -p:RuntimeIdentifiers="linux-musl-x64;linux-musl-arm64"` with no preceding RID-aware restore. It failed once with `NETSDK1047: Assets file ... doesn't have a target for 'net10.0/linux-musl-x64'` and passed on an immediate identical re-run, so its result depends on whether `obj/project.assets.json` already carries those RIDs.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  severity: medium
  summary: Nothing enforces deferred-work ledger format -- every governance test is skipped.
  evidence: All `Dw6*` cases in `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/` carry `[Fact(Skip = ...)]` (Dw6Bookkeeping 4, Dw6LedgerSweep 4, Dw6CheckerReport 5, Dw6GovernanceVocabulary 6) and both `Dw4DeferredWorkDispositionAtddTests` cases are skipped as well. This is why the loop-6 block could be appended with missing `severity:` fields, absolute machine-local `source_spec` paths, and duplicate entries without any gate objecting.

## Deferred from: code review of spec-4-5-append-durability-race-evidence.md (2026-08-25)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: Contender discrimination in the append-durability race depends on two undocumented sentinel values.
  evidence: `IsExactActorContender` (`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs:609-620`) requires `candidate.GlobalPosition > 0`, while the raw probe deliberately writes `globalPosition: 0`; nothing documents 0 as reserved. Separately `session.sessionId` is the same ULID as `rawContender.messageId` in every capture (`01KZG95BBK9G9M0Q859KR65N4T` in the committed one), so the session and the raw contender are indistinguishable in log correlation. Not reachable as a misclassification today because `UserId`, `DomainServiceVersion` and the `story-4-5-contender` extension also discriminate.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: The sealed LiveSidecar receipt exercises a PostgreSQL profile that the packet's provider record does not declare.
  evidence: `evidence/story-4-5/0776785f.../live-sidecar-test-results.json` contains an `Oq8Postgresql` test collection and an `IdempotencyAdmissionOq8PostgresqlTests` case, but `providerProfile` and `environment.md` document only Dapr 1.18.1, `state.redis` and `redis:6`. No Postgres image, version or connection profile is captured anywhere in the packet, so the 75-test receipt is not fully characterized by the environment it ships with.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: medium
  summary: A durability packet records no Redis durability configuration.
  evidence: `providerProfile.redisImage` is the floating tag `"redis:6"` rather than an image digest, and no `appendonly`, `save`, or `INFO persistence` output is captured in `append-durability-race.json` or `environment.md`. The story's headline claim is that a durable write was silently lost, and the Redis persistence settings are precisely the configuration that claim depends on; a re-capture on a differently-configured `redis:6` would be indistinguishable from the reviewed one. Belongs with the deferred multi-provider fencing capture, which must record per-provider durability settings anyway.

### Deferred from: code review of spec-3-14-corrective-oci-provenance-release.md (2026-08-25, chunk 4)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  severity: low
  summary: The trusted-import shadow check in the Story 3.14 dispatcher detects only after the handler has already executed.
  evidence: `_verify_no_repository_import_shadows` is called at the end of the `try` in `validate()`, after `validate_identity` and `validate_packet_files` have run, and is skipped entirely when validation raises. A repository-local shadow that did load would have executed its top-level code before being reported. Deferred: largely subsumed by the isolated-mode re-exec applied 2026-08-26, which removes the window in which any repository-local shadow can resolve at all (verified: a planted `tools/json.py` executed and the packet still validated `exit 0` before the fix, and no longer executes after it). Relocating the remaining check into `finally` would mask the original exception it is meant to accompany, so it is left as post-hoc defence in depth.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  severity: low
  summary: `deploy/README.md` still teaches `staging-latest` mutable tags and names a workflow that does not exist.
  evidence: `deploy/README.md:370` describes "the `deploy-staging.yml` workflow uses `staging-latest` mutable tags for staging deployments", but `.github/workflows/` contains no `deploy-staging.yml`, and this loop removed `staging-latest` as the container-tag default in `Directory.Build.targets:17` (updating `docs/brownfield/deployment-guide.md` but not this file). Deferred: pre-existing documentation drift, and `_bmad-output/planning-artifacts/epics.md:5139` already scopes deployment-documentation digest teaching to backlog Story 7.9.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md`
  severity: low
  summary: The reservation-input governance assertions were narrowed to one exact four-space `with:` block.
  evidence: `ContainerPublishingGovernanceTests` moved `release-version:`, `reserved-version:`, `release-authority-issue-url:` and `release-authority-owner:` from whole-file `ShouldNotContain` into the extracted `    with:` block, so a job whose `with:` is written at another indentation would not be examined. Deferred: theoretical — `.github/workflows/release.yml` has exactly two jobs and one `with:` mapping, and the new `Count(line => line.Equals("    with:")) == 1` assertion added in the same change fails closed if a second four-space mapping ever appears.

## Deferred from: code review of spec-4-5-append-durability-race-evidence (re-capture and re-seal, 2026-08-26)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: medium
  summary: No CI step executes the Story 4.5 evidence validator, so the packet's source binding decays silently under unrelated commits.
  evidence: `validate-evidence.py` is operator-discipline only. Every sibling committed-evidence directory is pinned either by a blocking test in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` or by a workflow step (`tools/validate-oq8-platform-evidence.py` from `integration.yml:118`). Deferred by explicit owner decision: wiring a step would change `.github/`, and AC6 requires `git diff 0776785f..HEAD -- src .github` to print nothing. Preserving AC6 wins; the decay cost is accepted. Re-sealing is the recovery, and it was performed on 2026-08-26.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: `Hexalith.EventStore.Gateway` and `Hexalith.EventStore.TestSubscriber` emit `bin/Debug` output paths inside a Release solution build.
  evidence: Neither project is a member of `Hexalith.EventStore.slnx`; both are reached only through `ProjectReference`, so the solution configuration never flows to them and `--configuration Release --no-incremental` still resolves them to `bin/Debug/net10.0/`. Both `.csproj` files live under `src/`, which AC6 freezes byte-for-byte, so Story 4.5 records the condition instead of fixing it. `validate-evidence.py` allowlists exactly those two names, re-checks that they really are non-members of the `.slnx`, rejects a `bin/Debug` path from anything else, and separately requires the LiveSidecar test assembly to be a Release output.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: medium
  summary: The ADD-fencing decision recorded in `architecture.md` still has no tracked owner story or trigger.
  evidence: `architecture.md:603` commits append-path storage fencing to "a separately approved implementation story", but no append-fencing story exists in `epics.md` or `sprint-status.yaml` (`grep -i fenc` finds only Story 4.11's admission fence). Nothing schedules the multi-provider re-capture the decision depends on. Carried forward from the 2026-08-11 review, still open after the 2026-08-26 re-seal.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: `Oq8PostgresqlFixture` hard-codes control-plane ports `50005`/`50006` and cannot start where `dapr init` publishes `6050`/`6060`.
  evidence: `Fixtures/Oq8PostgresqlFixture.cs:38-39` and its `VerifyPrerequisitesAsync` connect to `IPAddress.Loopback` on those exact ports, unlike `DaprTestContainerFixture`, which now probes both candidate pairs. The file is hash-bound by the sealed Story 4.14 (`evidence/story-4-14/.../source-state.json`) and Story 4.15 packets and validated by `tools/validate-oq8-platform-evidence.py` from `integration.yml`, so Story 4.5 must not edit it. The 2026-08-26 capture forwarded `50005->6050` and `50006->6060` instead, which is documented in `environment.md`. Owned by Story 4.14/4.15.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: Classifier completeness for the Story 4.5 race outcome model lives only in a docstring.
  evidence: `AppendDurabilityRaceClassifierTests` covers all twenty reachable classification names, but a twenty-first branch added to `AppendDurabilityRaceClassifier` would fail no test. Carried forward from the loop-2 review; unchanged by the 2026-08-26 re-seal.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md`
  severity: low
  summary: `MetadataKey_StaleEtagUpdate_IsRejected` and `ActorConcurrencyConflictTests` still read as actor-state evidence although the test keys on a generic-state key.
  evidence: The method keys on `story-4-5-generic-etag-{Guid:N}`, and both the class docstring and the story report now say so explicitly, but the method and class names were not changed. Renaming would invalidate `commands.md`, the `MUTATIONS` map in `validate-evidence.py`, and every committed receipt, so it is deferred to the append-fencing follow-up that re-captures anyway.
