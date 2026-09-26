# Deferred Work

### DW-1: Follow-up review still recommended for 1-7-domainservice-packaging-and-guardrails after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-1-7-domainservice-packaging-and-guardrails.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-071516-abab; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted 2026-07-07 (correct-course, sprint-change-proposal-2026-07-07-story-1-7-followup-review-disposition)
resolution: A terminating follow-up review pass was run per the Epic 1 retro action item. Deliverable green — `DomainModuleAuthoringGuardrailTests` 25/25, `ReleasePackageManifestTests` 8/8. All remaining findings are the regex-scan-completeness/soundness class already captured by the two substantive Story 1.7 deferred entries above (broad DAPR/host-wiring ban; cross-file/computed canonical route resolution). A fifth regex patch would re-arm the non-converging loop and fail the retro completion criterion ("no open follow-up-review-only item for Story 1.7"). Future closure of the finding class = a scoped Roslyn/convention-level guardrail story, not another follow-up review. `spec-1-7` `followup_review_recommended` cleared to false.

### DW-2: Follow-up review still recommended for 2-2-rest-api-generator-discovery-and-controller-emission after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-2-2-rest-api-generator-discovery-and-controller-emission.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-112402-3779; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required. Reviews converged to 0 HIGH; all substantive residuals are separately tracked (generated command request-size limit, command-rejection extension forwarding, status Location policy, query ArgumentException sanitization) under the REST generator hardening and command-status Location action items (owner: Winston). Evidence: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` → 108/108 passed on 2026-07-07 at HEAD fc0f1de8. See `sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md`.

### DW-3: Follow-up review still recommended for 2-3-sample-external-api-host-proof after the review budget was exhausted
origin: review-budget-followup
source_spec: `spec-2-3-sample-external-api-host-proof.md`
severity: low
reason: Review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up. The work was committed by bmad-loop run 20260707-112402-3779; this entry preserves the lingering follow-up recommendation for a deliberate later review.
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required. Reviews converged to 0 HIGH; the substantive residuals (status Location dependency, Sample DAPR app-id header append-vs-replace) are separately tracked under the command-status Location policy (owner: Winston) and outbound DAPR routing-header policy (owner: Amelia) action items. Evidence: `dotnet test tests/Hexalith.EventStore.Sample.Tests/` → 115/115 passed on 2026-07-07 at HEAD fc0f1de8. See `sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md`.

### DW-4: Follow-up review still recommended for 3-4-aspire-security-resource-naming after the damping cap was spent

status: done 2026-09-06
origin: review-budget-followup
source_spec: `spec-3-4-aspire-security-resource-naming.md`
archived: 2026-09-18

### DW-5: Follow-up review still recommended for 3-6-manifest-driven-release-packaging after the damping cap was spent

status: done 2026-09-06
origin: review-budget-followup
source_spec: `spec-3-6-manifest-driven-release-packaging.md`
archived: 2026-09-18

### DW-6: The final owner-record limitation-ID comparison is asymmetric across the WORM boundary. Block 15's `validate_final_owner_record` dedupes the record's IDs with `LC_ALL=C sort -u` before diffing them against the expected set, while block 16's `validate_committed_owner_record` uses a plain `LC_ALL=C sort` and separately asserts uniqueness in jq (`length == (map(.id) | unique | length)`). A final approval record carrying a duplicate limitation ID therefore passes approval validation and only fails during A/B/C verification.

origin: migrated from legacy ledger ("Deferred from: Story 1.20 pre-gate paired-contract audit (2026-07-25)"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` summary: The final owner-record limitation-ID comparison is asymmetric across the WORM boundary. Block 15's `validate_final_owner_record` dedupes the record's IDs with `LC_ALL=C sort -u` before diffing them against the expected set, while block 16's `validate_committed_owner_record` uses a plain `LC_ALL=C sort` and separately asserts uniqueness in jq (`length == (map(.id) | unique | length)`). A final approval record carrying a duplicate limitation ID therefore passes approval validation and only fails during A/B/C verification. evidence: Block 15 `diff -u "$EXPECTED_LIMITATION_IDS" <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort -u)` versus block 16 `diff -u "$A_EXPECTED_LIMITATION_IDS" <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort)`. Block 15 also derives its expected set from the generated approval subject while block 16 compares against a hard-coded literal list; the two were verified equal on 2026-07-25 at 9 capability IDs and 32 limitation IDs with no duplicates, so the asymmetry cannot fire against the current packet literal. severity: low status: accepted (deliberate acceptance 2026-07-25) — not fixed during the Story 1.20 closure run because the defect is unreachable with the committed approval-subject literal, and every packet edit forces a new candidate SHA and a complete ~50-minute Phase-1 re-gate. Fix by making block 15 use the same plain `sort` plus an explicit jq uniqueness assertion, so a duplicate ID is rejected before the irreversible WORM upload rather than after it. Fold into the next packet change rather than spending a dedicated cycle.
status: open

### DW-7: The reusable release workflow silently produces a green run that publishes nothing when `main` advances between release dispatch and the `Semantic Release` step. `actions/checkout` pins the dispatched `github.sha`; semantic-release then does its own `git fetch`, sees the live `origin/main` is ahead, prints `ℹ The local branch main is behind the remote one, therefore a new version won't be published.`, and exits 0. The operator sees a successful Release run and reasonably assumes a release was cut. Harden `domain-release.yml` to fail loudly (or emit an unmissable error annotation + non-success outcome) when the checked-out release SHA is no longer the live `main` tip at semantic-release time, instead of a silent no-op green.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: release-skip race diagnosis (2026-07-21, run 29799288142)"), 2026-08-30
archived: 2026-09-18

### DW-8: Add a guardrail test asserting the `postgres:18.4` tag in `.github/workflows/integration.yml`'s "Pull PostgreSQL container image" step matches `Oq8PostgresqlFixture.PostgresImage`, so the two literals cannot silently drift.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
archived: 2026-09-18

### DW-9: Add a `docker` ecosystem entry to `.github/dependabot.yml` so `postgres:18.4` bumps get automated PRs like the existing `nuget`/`npm`/`github-actions` ecosystems.

origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
location: github/dependabot.yml
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md` summary: Add a `docker` ecosystem entry to `.github/dependabot.yml` so `postgres:18.4` bumps get automated PRs like the existing `nuget`/`npm`/`github-actions` ecosystems. evidence: Blind-hunter review of the CI fix -- Dependabot currently cannot see or bump the Postgres image tag in either the workflow or the fixture, so the sync in the item above would otherwise be 100% manual forever.
status: open

### DW-10: Pin the live-sidecar PostgreSQL image by digest (`postgres@sha256:...`) instead of the mutable `18.4` tag, with a documented rotation process.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
archived: 2026-09-18

### DW-11: Cache the pulled `postgres:18.4` image (or layer) across `integration.yml` runs instead of re-pulling on every push/PR to `main`.

origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
location: integration.yml
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md` summary: Cache the pulled `postgres:18.4` image (or layer) across `integration.yml` runs instead of re-pulling on every push/PR to `main`. evidence: Blind-hunter review of the CI fix -- the image rarely changes but is currently re-pulled in full on every job run with no `actions/cache` or registry mirror.
status: open

### DW-12: Evaluate replacing `Oq8PostgresqlFixture`'s manual `docker run`/`docker image inspect` orchestration with GitHub Actions' native `services:` container support (or an equivalent declarative approach), which would pull, health-check, and manage the Postgres container without a hand-rolled prerequisite check.

origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
location: Oq8PostgresqlFixture
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md` summary: Evaluate replacing `Oq8PostgresqlFixture`'s manual `docker run`/`docker image inspect` orchestration with GitHub Actions' native `services:` container support (or an equivalent declarative approach), which would pull, health-check, and manage the Postgres container without a hand-rolled prerequisite check. evidence: Blind-hunter review of the CI fix -- this fix patches the one workflow that currently exercises the fixture; the next new workflow, self-hosted runner, or Tier-3 job that reuses `Oq8PostgresqlFixture` will hit the identical "no such image" failure unless the fixture's own contract is revisited.
status: open

### DW-13: Generalize the reusable publication preflight's hard-coded EventStore package count of exactly 14 so other callers can supply their own immutable expected inventory size without weakening EventStore's manifest contract.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: immutable manual release hardening (2026-07-20)"), 2026-08-30
archived: 2026-09-18

### DW-14: Give each container mapping its own frozen repository identity and phase evidence when multiple container mappings share one release invocation.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: immutable manual release hardening (2026-07-20)"), 2026-08-30
archived: 2026-09-18

### DW-15: Close the non-atomic gap between the final Zot tag-absence proof and the subsequent registry write.

origin: migrated from legacy ledger ("Deferred from: immutable manual release hardening (2026-07-20)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md` summary: Close the non-atomic gap between the final Zot tag-absence proof and the subsequent registry write. evidence: The final read-only `HEAD` check fails closed on collisions and ambiguous responses, but another writer can still create the tag after absence is observed and before .NET SDK publication begins; Zot absence and write are not atomic.
status: open

### DW-16: Make a safe-denial route registration's `Domain`/`QueryType` casing mismatch against real wire values operator-visible (today only the registered-route list itself is logged, not whether any entry actually matches a query type that ever occurs).

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: QueryType
reason: source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md` summary: Make a safe-denial route registration's `Domain`/`QueryType` casing mismatch against real wire values operator-visible (today only the registered-route list itself is logged, not whether any entry actually matches a query type that ever occurs). evidence: Round-2 blind-hunter review of 6.1-P2 -- ordinal case-sensitive route matching means a typo'd-casing registration silently gets no safe-denial protection, same failure mode the round-1 startup-logging fix targeted, but closing it needs a canonical registry of valid domain/queryType pairs to cross-check against, which doesn't exist anywhere in this codebase today -- out of proportion for a patch-level fix.
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-16, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-17: Add an end-to-end test tying `DualPrincipalClaimsHelper`'s claim-type assumptions (`azp`/`act`/`scope`/`aud`/`client_id`, dependent on `MapInboundClaims=false`) to the real `JwtBearerHandler`/Keycloak token-issuance pipeline, not just hand-constructed `ClaimsPrincipal` unit tests.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ESecurityTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md` summary: Add an end-to-end test tying `DualPrincipalClaimsHelper`'s claim-type assumptions (`azp`/`act`/`scope`/`aud`/`client_id`, dependent on `MapInboundClaims=false`) to the real `JwtBearerHandler`/Keycloak token-issuance pipeline, not just hand-constructed `ClaimsPrincipal` unit tests. evidence: Round-2 blind-hunter review of 6.1-P2 -- this story is the first time these claim types become authorization-relevant (previously only `sub` mattered); nothing today would catch a regression if `MapInboundClaims` were flipped or a future middleware renamed these claim types. Belongs in the existing Keycloak E2E integration-test tier (`tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ESecurityTests.cs`), a heavier lift than this patch round's unit-level fixes.
status: open

### DW-18: Close the timing side-channel for the safe-denial adapter (Forbidden vs. genuine not-found currently have different latency profiles -- actor-activation-then-403 vs. actor-lookup-failure -- with no constant-time/padding normalization).

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-6-1-p2-dual-principal-query-envelope-safe-denial.md` summary: Close the timing side-channel for the safe-denial adapter (Forbidden vs. genuine not-found currently have different latency profiles -- actor-activation-then-403 vs. actor-lookup-failure -- with no constant-time/padding normalization). evidence: Blind-hunter review of 6.1-P2 found no timing normalization despite the story's original AC naming "timing-observable behavior" indistinguishability; full closure requires platform-level work (DAPR actor activation, network jitter) beyond what a query-router decorator controls, so the AC was narrowed to shape/status indistinguishability only and this was split out as separate future hardening.
status: open
decision: 2026-09-06 Keep deferred
decision: 2026-09-06 Keep deferred
decision: 2026-09-01 Keep deferred

### DW-19: Expose an authoritative persisted global-position/watermark to projections and `QueryCursorScope`, consumed by Hexalith.Projects Story 6.1-P2's watermark-replay/restart requirement.

status: done 2026-08-31
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-20: Epic D retrospective follow-through requires a dedicated REST generator hardening story or backlog item. Scope it from the D5/D7 deferred items below rather than scattering generator diagnostics into unrelated security, correctness, or UI stories. Minimum scope: unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.

status: done 2026-09-06
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-21: Query freshness/projection metadata needed a platform-owned gateway contract before UI or generated REST stories could treat stale/current state or projection version as production-backed evidence. RESOLVED 2026-07-11 by Story 2.8 / AD-15 for EventStore route provenance, route-aware ETags, and fail-safe consumers. Genuine persisted-age evidence remains the separate D6 handoff; the Tenants producer cleanup remains Story 4.7.

status: done 2026-07-11
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-22: Generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details before accepting a live-smoke blocker. → tracked as Story 3.8 (Epic 3, companion to 3.1); re-homed from TEST-1.1 on 2026-07-07. RESOLVED 2026-07-07 by Story 3.8 — `scripts/generated-api-smoke-preflight.sh`; AC10 live-topology gate met (generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, support-safe failure details).

status: done 2026-07-07
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-23: Packaging governance tests hard-code external dependency patch versions. Consider a lower-maintenance guard that still proves central version pins and emitted package metadata stay aligned, so routine published package bumps do not require brittle test-only edits.

status: done 2026-09-06
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-24: Handler-backed query routes need explicit provenance so the gateway can decide whether projection ETags are valid for the response.

status: done 2026-08-31
origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
archived: 2026-09-18

### DW-25: The EventStore platform portion of AD-15 is owned and implemented by Story 2.8: additive provenance contract, authoritative router stamping, route-first conditional evaluation, projection-only freshness/ETag evidence, and fail-safe client/generated REST behavior.

status: done 2026-08-31
origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
archived: 2026-09-18

### DW-26: The 2026-07-05 gateway-contract prerequisite is superseded for metadata propagation by AD-14 + Stories 1.2/1.3/2.2, and for EventStore route provenance enforcement by AD-15 + Story 2.8.

status: done 2026-08-31
origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
archived: 2026-09-18

### DW-27: Story 4.7 is now Tenants-only follow-up.

status: done 2026-09-06
origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
archived: 2026-09-18

### DW-28: The D6 read-model-freshness handoff remains a separate deferred platform item (persisted projection-age metadata). Until a route sources genuine freshness it is `HandlerComputed`/`Unknown` under AD-15 and consumers render `unknown`.

origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
location: HandlerComputed
reason: The **D6 read-model-freshness handoff** remains a separate deferred platform item (persisted projection-age metadata). Until a route sources genuine freshness it is `HandlerComputed`/`Unknown` under AD-15 and consumers render `unknown`.
status: open

### DW-29: Malformed projection payload throws inside `CounterStatusResult.ParseCountFromPayload` (`Convert.FromBase64String` / `JsonDocument.Parse` / `GetInt32`) instead of failing safe. Pre-existing behavior carried over from the deleted `CounterQueryService`; becomes invisible once the refresh-error patch lands.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: CounterStatusResult.ParseCountFromPayload
reason: Malformed projection payload throws inside `CounterStatusResult.ParseCountFromPayload` (`Convert.FromBase64String` / `JsonDocument.Parse` / `GetInt32`) instead of failing safe. Pre-existing behavior carried over from the deleted `CounterQueryService`; becomes invisible once the refresh-error patch lands.
status: open

### DW-30: Concurrent/re-entrant refresh has no in-flight guard, and the in-flight `GetAsync` is never cancelled on component disposal (leading to a possible post-dispose `StateHasChanged`). `GetAsync` already exposes an unused `CancellationToken`. Demo-UI hardening across the four Counter components; `SilentReloadPattern` partially mitigates via debounce.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: GetAsync
reason: Concurrent/re-entrant refresh has no in-flight guard, and the in-flight `GetAsync` is never cancelled on component disposal (leading to a possible post-dispose `StateHasChanged`). `GetAsync` already exposes an unused `CancellationToken`. Demo-UI hardening across the four Counter components; `SilentReloadPattern` partially mitigates via debounce.
status: open

### DW-31: REST generator silently drops a `record struct` contract carrying `[RestRoute]` (the `TypeKind != Class` check returns null) with no HESREST diagnostic — inconsistent with every other unsupported-shape path, which reports a diagnostic. Add a diagnostic or explicitly support the shape.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
archived: 2026-09-18

### DW-32: Referenced-message discovery (`RestApiMessageParser.ParseReferenced`) is driven off `CompilationProvider` and emits a reference-equality `ImmutableArray`, so it re-runs the referenced-assembly walk on every compilation and weakens IDE incrementality. Consistent with the generator's pre-existing CompilationProvider usage; perf-only. Consider an equatable model/comparer if editor responsiveness regresses.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
archived: 2026-09-18

### DW-33: Blazor components treat "no projection yet" only as HTTP 404; a gateway `Success==false` semantic failure surfaces as `EventStoreGatewayException.StatusCode == 200` and falls through to the generic catch. Matches the old code's 404-only behavior, so no regression, but the empty-state contract could be made explicit.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: n/a
reason: Blazor components treat "no projection yet" only as HTTP 404; a gateway `Success==false` semantic failure surfaces as `EventStoreGatewayException.StatusCode == 200` and falls through to the generic catch. Matches the old code's 404-only behavior, so no regression, but the empty-state contract could be made explicit.
status: open

### DW-34: AC8 scope hygiene: the generator command-route mapping (`TryFindUnmappedCommandRouteParameter`) was changed and a command diagnostic test added inside a query-only story (defensible as generator enablement), and the broader D5 branch/working-tree carries CI/CD, `tools/release-*`, `.releaserc.json`, and submodule-pointer changes that belong to D7/D8. Split those out of the D5 change set.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: tools/release-*
reason: AC8 scope hygiene: the generator command-route mapping (`TryFindUnmappedCommandRouteParameter`) was changed and a command diagnostic test added inside a query-only story (defensible as generator enablement), and the broader D5 branch/working-tree carries CI/CD, `tools/release-*`, `.releaserc.json`, and submodule-pointer changes that belong to D7/D8. Split those out of the D5 change set.
status: open

### DW-35: `CounterHistoryGrid` inserts a history row on every refresh, including HTTP 304 (no change), producing duplicate rows. Deferred (user decision B3): intent is ambiguous — value-change log (skip 304s) vs. polling/ETag-activity log (current behavior is fine). Decide the grid's purpose before changing it.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: CounterHistoryGrid
reason: `CounterHistoryGrid` inserts a history row on every refresh, including HTTP 304 (no change), producing duplicate rows. Deferred (user decision B3): intent is ambiguous — value-change log (skip 304s) vs. polling/ETag-activity log (current behavior is fine). Decide the grid's purpose before changing it.
status: open

### DW-36: Command contracts with duplicate JSON property names are not diagnosed; the new duplicate JSON-name check only runs for queries, so generated command serialization/model-binding can still fail later. Deferred as command/generator hardening outside the D5 query proof.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)"), 2026-08-30
archived: 2026-09-18

### DW-37: Referenced contracts that rely on convention routing rather than `[RestRoute]` are not discovered by `ParseReferenced`, even though source contracts without `[RestRoute]` still get default routes. Deferred as generator hardening outside the D5 query proof.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)"), 2026-08-30
location: RestRoute
reason: Referenced contracts that rely on convention routing rather than `[RestRoute]` are not discovered by `ParseReferenced`, even though source contracts without `[RestRoute]` still get default routes. Deferred as generator hardening outside the D5 query proof.
status: open

### DW-38: Query JSON names are deduplicated with `StringComparer.Ordinal`; names differing only by case can still bind ambiguously through query string/model-binding conventions. Deferred as generator hardening outside the D5 query proof.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)"), 2026-08-30
archived: 2026-09-18

### DW-39: Route-template validator (`RestApiRouteTemplateParser.GetTemplateError`) false-rejects legitimate inline route constraints containing braces, e.g. `{id:regex(^\d{3}$)}`: `close` binds to the constraint's inner `}`, so the parameter text contains `{` and is rejected as "unescaped brace". Generator hardening; no D5 route uses constraints.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
archived: 2026-09-18

### DW-40: `RestApiControllerEmitter.RouteParameterMatchesProperty` compares the C# Name with `OrdinalIgnoreCase` but the JsonName with `Ordinal`, while route binding is case-insensitive. A route token matching a property's JsonName only case-insensitively is not excluded from the emitted query payload → phantom / double-bound parameter. Generator hardening; not exercised by D5.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
archived: 2026-09-18

### DW-41: Query-binding expression (`RestApiControllerEmitter.GetQueryBindingExpression`) silently falls back to aggregate `"index"` / empty entity when `AggregateSource`/`EntitySource` is neither `Constant` nor `Route` (malformed `[RestQueryBinding]` or a future enum member); the validator only guards the `"Route"`-missing case, so no HESREST diagnostic is emitted. Same silent-drop class the diagnostics work aims to close. Generator hardening; `[RestQueryBinding]` not used by D5.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
archived: 2026-09-18

### DW-42: `[RestQueryBinding]` with `Constant` entity source and no supplied value produces a silent empty-string entity id (`binding.EntityValue ?? string.Empty` → `Literal("")`). Generator hardening; not used by D5.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
archived: 2026-09-18

### DW-43: `CounterStatusResult.FromQueryResult` returns a fabricated `count 0` when the gateway reports `IsNotModified` but `cachedResult` is null. The four sample components avoid this (they pass a null ETag on first load), but the general-purpose `EventStoreProjectionQueryClient.GetAsync` accepts an arbitrary `If-None-Match` and has no guard. Demo-UI hardening.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: CounterStatusResult.FromQueryResult
reason: `CounterStatusResult.FromQueryResult` returns a fabricated `count 0` when the gateway reports `IsNotModified` but `cachedResult` is null. The four sample components avoid this (they pass a null ETag on first load), but the general-purpose `EventStoreProjectionQueryClient.GetAsync` accepts an arbitrary `If-None-Match` and has no guard. Demo-UI hardening.
status: open

### DW-44: Empty (as opposed to absent) `DAPR_HTTP_PORT` yields the base address `http://localhost:` and `new Uri(...)` throws `UriFormatException` at startup with an opaque message, in both `Sample.Api` and `Sample.BlazorUI`. The `?? "3500"` fallback only guards null. Minor robustness.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
archived: 2026-09-18

### DW-45: `InboundBearerForwardingHandler` forwards a multi-valued inbound `Authorization` header comma-joined (via the `StringValues`→`string` implicit conversion), producing a malformed bearer that is rejected opaquely by the gateway rather than up front. Adversarial/rare.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: InboundBearerForwardingHandler
reason: `InboundBearerForwardingHandler` forwards a multi-valued inbound `Authorization` header comma-joined (via the `StringValues`→`string` implicit conversion), producing a malformed bearer that is rejected opaquely by the gateway rather than up front. Adversarial/rare.
status: open

### DW-46: HIGH — RESOLVED/SUPERSEDED. AD-14 added `QueryRouterResult.Metadata` and the carrier path; Story 2.8 / AD-15 now stamps route provenance, preserves genuine producer freshness/version evidence only for `ProjectionBacked`, and gates generated headers accordingly. D6 remains responsible for additional persisted-age production sources, while Story 4.7 retains only the Tenants `ProjectionVersion := ETag` producer cleanup.

status: done 2026-07-11
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
archived: 2026-09-18

### DW-47: MEDIUM — Generator: `{tenantId}` route parameter is unvalidated under `RestTenantSource.System`. `IsTenantParameter` (`src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1143`) is name-based and not gated on tenant source, so the tenant-named route segment is excluded from route/body mismatch checks and, under System source, is decorative — a URL/body tenant mismatch silently executes against the body tenant (still bearer-authorized, so no escalation). Recommend validating tenant-named route params against the body when tenant source ≠ Route.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1143
reason: MEDIUM — Generator: `{tenantId}` route parameter is unvalidated under `RestTenantSource.System`. `IsTenantParameter` (`src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1143`) is name-based and not gated on tenant source, so the tenant-named route segment is excluded from route/body mismatch checks and, under System source, is decorative — a URL/body tenant mismatch silently executes against the body tenant (still bearer-authorized, so no escalation). Recommend validating tenant-named route params against the body when tenant source ≠ Route.
status: open

### DW-48: MEDIUM — External REST error-semantics coverage gap. The 2054-line `TenantsQueryControllerIntegrationTests` was replaced by a 296-line generated-controller test covering 401/request-shape/freshness/ETag-304 but not 403/RBAC, gateway-failure → problem-details, or invalid-cursor at the generated surface. Add once the transport-fault and 400-vs-500 patches land.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
archived: 2026-09-18

### DW-49: LOW — Generator silently falls back to aggregate `"index"` for invalid `[RestQueryBinding]` sources (None / out-of-range enum / empty Constant) with no HESREST diagnostic, and `RestApiQueryBindingDescriptor.GetHashCode` can NRE on a null constant value. Re-logged from the D5 review; now exercised by D7 `[RestQueryBinding]` usage so worth prioritizing.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
archived: 2026-09-18

### DW-50: LOW — RESOLVED 2026-07-31 by Story 2.12. `Hexalith.Tenants.csproj` now gives Gateway and DomainService complementary source/package edges under the shared dependency-mode contract. The current graph cannot mix source Gateway with package DomainService. After the Tenants solution restore hit `MSB3202` on forbidden/uninitialized nested submodule projects, package-mode validation covered all 17 tracked Tenants projects individually with zero warnings or errors.

status: done 2026-07-31
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
archived: 2026-09-18

### DW-51: LOW (supply-chain) — `PackageGovernanceTests` now require the local workflows to reference shared `Hexalith.Builds` reusable workflows by mutable `@main` (previously enforced full-SHA pinning). Deliberate org CI decision ("main for stability"), not D7 work. Whoever controls `Hexalith.Builds@main` controls this repo's release step (holds `NUGET_API_KEY`); pinning is delegated to the shared repo and unenforced here.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: PackageGovernanceTests
reason: LOW (supply-chain) — `PackageGovernanceTests` now require the local workflows to reference shared `Hexalith.Builds` reusable workflows by mutable `@main` (previously enforced full-SHA pinning). Deliberate org CI decision ("main for stability"), not D7 work. Whoever controls `Hexalith.Builds@main` controls this repo's release step (holds `NUGET_API_KEY`); pinning is delegated to the shared repo and unenforced here.
status: open

### DW-52: MEDIUM — `ListByCursorAsync` (`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:482-488`) lets a `StatusCode = 200` `EventStoreGatewayException` (null/JSON-`null`/shape-mismatch payload from the generic `SubmitQueryAsync<T>`) escape into the Blazor circuit — it catches only `IsUnauthorized` and `IsUnavailableOrInvalid` (`>= 400`), unlike the sibling methods' unfiltered catch-all. Undermines AC7 fail-closed. Pre-existing pattern carried through the migration (filters unchanged by D7); trivially patchable by mirroring the sibling catch-all. Low likelihood — needs a malformed 200 (projection/contract bug).

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
archived: 2026-09-18

### DW-53: LOW — Tenant-list path has no invalid-cursor recovery (`TenantQueryGateway.cs:485-487, 936`): `IsUnavailableOrInvalid` treats every `>= 400` alike, so a 400 invalid/expired list cursor surfaces as "gateway unavailable" instead of resetting to page 1 (as `GetTenantAuditAsync` does via `IsInvalidAuditCursor`). Low likelihood — list cursors are server-issued protected cursors.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
archived: 2026-09-18

### DW-54: (re-confirms existing D7 entry) LOW — empty `Constant` `[RestQueryBinding]` value emits an empty aggregate id with no HESREST diagnostic (`RestApiControllerEmitter.cs:376`). Same silent-drop class as the already-listed generator-diagnostic hardening item; the GetHashCode-NRE sub-claim was refuted (`GetString` never returns null).

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
archived: 2026-09-18

### DW-55: (re-confirms existing D7 entry) — RESOLVED/SUPERSEDED by Story 2.8 / AD-15 for EventStore provenance enforcement and generated header gating; see the reconciled HIGH item above. D6 and the Tenants-only Story 4.7 producer cleanup remain separate.

status: done 2026-07-11
origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
archived: 2026-09-18

### DW-56: MEDIUM (CI coverage) — `HotReloadTests` (`tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs`) now owns and disposes an isolated Aspire fixture per test, contains three live tests, and successfully exercises DCP sample stop/start on this WSL2 host. The remaining pre-existing gap is CI execution: no PR/push workflow runs the Tier-3 `Hexalith.EventStore.IntegrationTests` project, so hot-reload readiness regressions can merge without exercising the real stop/restart path. The dedicated Aspire-in-CI follow-up in `sprint-change-proposal-2026-06-22-ci-release-retier.md` retains ownership.

origin: migrated from legacy ledger ("Deferred from: integration + E2E test-suite recovery (2026-07-06, spec-integration-e2e-test-recovery)"), 2026-08-30
location: tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs
reason: MEDIUM (CI coverage) — `HotReloadTests` (`tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs`) now owns and disposes an isolated Aspire fixture per test, contains three live tests, and successfully exercises DCP sample stop/start on this WSL2 host. The remaining pre-existing gap is CI execution: no PR/push workflow runs the Tier-3 `Hexalith.EventStore.IntegrationTests` project, so hot-reload readiness regressions can merge without exercising the real stop/restart path. The dedicated Aspire-in-CI follow-up in `sprint-change-proposal-2026-06-22-ci-release-retier.md` retains ownership.
status: open

### DW-57: LOW (test isolation) — Only `AggregateActor` type name is per-run randomized (`EventStore__Actors__AggregateActorTypeName`); `ProjectionActor`/`ETagActor`/`GlobalPositionActor` use fixed const type names (`QueryRouter.ProjectionActorTypeName`, `ETagActor.ETagActorTypeName`, `GlobalPositionActor.ActorTypeName`). On a shared, long-lived Dapr placement (e.g. a sibling repo's AppHost also built on EventStore), a stale/dead host for those fixed names makes actor invocations block ~60s (client `HttpClient.Timeout`) instead of resolving. Integration runs need either a dedicated placement per run or per-run randomization of those three actor type names. Root cause of the initial "projection queries hang 60s" symptom (compounded by the now-fixed `QueryResult` deserialization bug).

origin: migrated from legacy ledger ("Deferred from: integration + E2E test-suite recovery (2026-07-06, spec-integration-e2e-test-recovery)"), 2026-08-30
location: AggregateActor
reason: LOW (test isolation) — Only `AggregateActor` type name is per-run randomized (`EventStore__Actors__AggregateActorTypeName`); `ProjectionActor`/`ETagActor`/`GlobalPositionActor` use fixed const type names (`QueryRouter.ProjectionActorTypeName`, `ETagActor.ETagActorTypeName`, `GlobalPositionActor.ActorTypeName`). On a shared, long-lived Dapr placement (e.g. a sibling repo's AppHost also built on EventStore), a stale/dead host for those fixed names makes actor invocations block ~60s (client `HttpClient.Timeout`) instead of resolving. Integration runs need either a dedicated placement per run or per-run randomization of those three actor type names. Root cause of the initial "projection queries hang 60s" symptom (compounded by the now-fixed `QueryResult` deserialization bug).
status: open

### DW-58: The default full-replay projection path forwards generic paging but only enforces cursors, silently ignoring `Offset`/`PageSize` it cannot honor, and the caching actor keys on paging the default actor never applies — so distinct un-honorable offsets create identical duplicate cache entries and, past the 32-entry cap, evict other query types on the shared actor.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-3-generic-read-models-and-query-cursors (2026-07-06)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:90-92
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-3-generic-read-models-and-query-cursors.md` summary: The default full-replay projection path forwards generic paging but only enforces cursors, silently ignoring `Offset`/`PageSize` it cannot honor, and the caching actor keys on paging the default actor never applies — so distinct un-honorable offsets create identical duplicate cache entries and, past the 32-entry cap, evict other query types on the shared actor. evidence: `EventReplayProjectionActor.ExecuteQueryAsync` (`src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:90-92`) hard-fails a nonblank `Paging.Cursor` with the `invalid-cursor` sentinel (→ HTTP 400) but takes no action on `Paging.Offset`/`PageSize`; a validator-passing `paging={offset:50}` returns the entire unpaged singleton state with no signal offset was dropped. `CachingProjectionActor` (`Actors/CachingProjectionActor.cs:62-66,204-209`) folds `ComputePagingChecksum(envelope.Paging)` into `CacheEntryKey`, so each distinct ignored offset/pageSize occupies a separate identical entry and the `MaxCacheEntries=32` overflow guard's `_payloadCache.Clear()` then evicts unrelated cached query types on the same shared actor. Two independent reviewers converged on this paging-enforcement asymmetry; low severity (bounded per-actor, singleton state, cursor-only was the story's in-scope path), but a genuine platform-level consistency gap for offset paging against actors/handlers that do not honor it. Cursor-only forwarding to domain handlers/projections for downstream validation is by-design per the intent contract; this entry concerns only the un-honorable-offset asymmetry and its cache-fragmentation consequence.
status: open

### DW-59: Domain-event processor message-level markers cannot make multiple independently side-effecting handlers atomic; a later handler failure may replay earlier successful handlers.

origin: migrated from legacy ledger ("Deferred from: review of spec-1-4-projection-and-domain-event-consumer-seams (2026-07-06)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-4-projection-and-domain-event-consumer-seams.md` summary: Domain-event processor message-level markers cannot make multiple independently side-effecting handlers atomic; a later handler failure may replay earlier successful handlers. evidence: `EventStoreDomainEventProcessor.DispatchAsync` invokes all registered `IEventStoreDomainEventHandler<TEvent>` handlers sequentially under one message marker. If handler A commits a side effect and handler B throws, the processor releases the message marker so DAPR can redeliver, and handler A can run again. Stronger guarantees need per-handler markers or a transactional/composite handler contract; this story documents handler idempotency and keeps the marker seam message-level.
status: open

### DW-60: A misconfigured (non-existent) `PayloadAggregateIdPropertyName` silently drops every consumed event as an aggregate mismatch, indistinguishable from a legitimate value mismatch and with no distinct diagnostic.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-4-projection-and-domain-event-consumer-seams (2026-07-06, review pass 2)"), 2026-08-30
location: PayloadAggregateIdPropertyName
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-4-projection-and-domain-event-consumer-seams.md` summary: A misconfigured (non-existent) `PayloadAggregateIdPropertyName` silently drops every consumed event as an aggregate mismatch, indistinguishable from a legitimate value mismatch and with no distinct diagnostic. evidence: `EventStoreDomainEventProcessor.TryGetPayloadId` resolves the configured property via reflection; a name that is not a public instance property resolves to a cached null `PropertyInfo`, so `TryGetPayloadId` returns false for every payload. The caller (the `_payloadAggregateIdPropertyName is not null && (!TryGetPayloadId(...) || !string.Equals(...))` branch in `ProcessAsync`) then treats every event as `SkippedAggregateMismatch`, acknowledges it (HTTP 200), and logs only at Information — so a typo silently discards the entire subscription's traffic. The property-resolution feature pre-dates this story (this diff only re-keyed the reflection cache by (event type, property name)); the silent-drop-on-missing-property behavior was surfaced incidentally by the review, not introduced here. A fix would distinguish "property not resolvable on a resolved event type" (misconfiguration → fail fast at startup or emit a distinct warning) from a legitimate per-event value mismatch.
status: open

### DW-61: Query/projection-handler domain discovery for telemetry only recognizes handlers that carry `[EventStoreDomain]` or expose a public parameterless constructor; a DI-constructed handler with no attribute is silently dropped (its domain gets no diagnostics), and the parameterless path reflectively instantiates handler types at host-build time.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:214-240
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-5-domain-module-hosting-observability.md` summary: Query/projection-handler domain discovery for telemetry only recognizes handlers that carry `[EventStoreDomain]` or expose a public parameterless constructor; a DI-constructed handler with no attribute is silently dropped (its domain gets no diagnostics), and the parameterless path reflectively instantiates handler types at host-build time. evidence: `GetHandlerDomainNames<THandler>` (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:214-240`) yields a domain name only when the handler type has `[EventStoreDomain]` or a public parameterless ctor it can `Activator.CreateInstance`; a handler with only a dependency-injecting ctor and no attribute hits `continue` and is skipped, so its domain never gets an `EventStoreDomainDiagnostics`/keyed service/OTel source — and `GetRequiredKeyedService<EventStoreDomainDiagnostics>(domain)` then throws for that domain's own code while its admission telemetry is silently absent. The parameterless path also executes the handler constructor at host-build time (aborts startup if it throws; leaks a throwaway if the handler is `IDisposable`), and the `handler switch { IDomainQueryHandler => ..., IDomainProjectionHandler => ... }` matches the query branch first, so a type implementing both handler interfaces with divergent domains never registers its projection domain. The spec's residual-risk note accepts the `[EventStoreDomain]` requirement for dependency-heavy handlers; a robust fix resolves domain names from DI-materialized handlers or a static metadata seam rather than reflective construction. Two independent reviewers converged; genuine but requires a design decision, hence deferred rather than auto-patched.
status: open

### DW-62: Health endpoints do not declare an explicit anonymous-access contract, so a future global fallback authorization policy could block DAPR app-health probes even when the probe targets `/alive`.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: /alive
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption-2.md` summary: Health endpoints do not declare an explicit anonymous-access contract, so a future global fallback authorization policy could block DAPR app-health probes even when the probe targets `/alive`. evidence: Story 1.6 follow-up switches the public Aspire domain-module sidecar app-health default from `/ready` to `/alive`, resolving the sidecar-dependent readiness feedback loop, but `MapDefaultEndpoints` still maps `/health`, `/alive`, and `/ready` without explicit `AllowAnonymous()` metadata. Current EventStore tests show unauthenticated health calls succeed under today's auth setup, but adding a global fallback policy later could make DAPR mark modules unhealthy unless health endpoint anonymity is made an intentional contract. resolution: Contract defined as architecture invariant AD-16 (health/liveness/readiness endpoints `/health`, `/alive`, `/ready` are explicitly `AllowAnonymous` + support-safe; any global fallback authorization policy lands in the same-or-earlier slice and is never weakened to reach probes) via `sprint-change-proposal-2026-07-07-health-endpoint-anonymous-access-contract.md`. Enforcement carried by Stories 5.3, 5.5, and 7.3 (AD-16 acceptance criteria + positive-probe/negative-protected-endpoint test). status: RESOLVED 2026-07-07 (correct-course).
status: open

### DW-63: A single `EventStoreDomainDiagnostics` instance is owned and disposed by the registry yet also returned from the keyed and single-domain DI factories, so the container double/triple-disposes it at teardown — harmless today only because `ActivitySource`/`Meter` disposal is idempotent.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/EventStoreDomainDiagnosticsRegistry.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-5-domain-module-hosting-observability.md` summary: A single `EventStoreDomainDiagnostics` instance is owned and disposed by the registry yet also returned from the keyed and single-domain DI factories, so the container double/triple-disposes it at teardown — harmless today only because `ActivitySource`/`Meter` disposal is idempotent. evidence: `EventStoreDomainDiagnosticsRegistry` constructs each `EventStoreDomainDiagnostics` with `new` and disposes them in its own `Dispose()` (`src/Hexalith.EventStore.DomainService/EventStoreDomainDiagnosticsRegistry.cs`), while the keyed factory and the single-domain non-keyed factory (`src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetryExtensions.cs`) both return the registry-owned instance. MS.DI tracks any `IDisposable` returned from a singleton factory and disposes it at container teardown, so the same instance is disposed by the registry plus once per resolved factory. It is benign today only because `ActivitySource.Dispose()`/`Meter.Dispose()` are idempotent and the type holds no other disposable state — an incidental guarantee, not a guarded one. Resolve by clarifying ownership: either the registry does not dispose instances it hands out through DI, or the factories return non-owned wrappers / the type gets an idempotent dispose guard. Both reviewers independently flagged this; low current severity.
status: open

### DW-64: Domain-module guardrails still cannot broadly ban all direct DAPR/host wiring while the initialized Tenants domain-service host carries transitional `AddDaprClient`, `UseCloudEvents`, controller, MediatR, and router composition.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md` summary: Domain-module guardrails still cannot broadly ban all direct DAPR/host wiring while the initialized Tenants domain-service host carries transitional `AddDaprClient`, `UseCloudEvents`, controller, MediatR, and router composition. evidence: Story 1.7 strengthens the clean Sample reference and platform-owned state/cursor/telemetry/health/endpoint checks, but a broad scan for all DAPR/host wiring markers would fail current `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`. Enforce the broader rule after the remaining Tenants host composition has moved behind EventStore platform seams or has an explicit permanent exception.
status: open

### DW-65: Domain-module endpoint guardrails still rely on lightweight same-file route resolution and do not prove canonical route values passed through cross-file constants or variables.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md` summary: Domain-module endpoint guardrails still rely on lightweight same-file route resolution and do not prove canonical route values passed through cross-file constants or variables. evidence: The follow-up review hardened direct literal, same-file constant, repeated-constant, and simple `MapGroup` route detection in `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`, but a domain module could still hide `/process`, `/replay-state`, `/query`, `/project`, or `/admin/operational-index-metadata` behind a route value imported from another type or computed variable. Closing this completely needs a Roslyn-level syntax/semantic guardrail or an explicit convention that forbids indirect canonical endpoint route values in scanned domain roots. The same "lightweight scan cannot be complete or sound on arbitrary C#" class also covers the receiver-agnostic state-access soundness gap surfaced by the 2026-07-07 correct-course review: `ContainsInvocationOnCallResult` (`DomainModuleAuthoringGuardrailTests.cs:806-820`) matches `).<marker>(` on any call result with generic state-method names (`GetStateAsync`/`SaveStateAsync`/`SetStateAsync`/`ClearCacheAsync`), a potential false-positive on unrelated domain method chains. Both directions (false-negative indirection and false-positive over-match) are closed only by a Roslyn/convention-level guardrail, not further regex refinement — see accepted entry DW-1.
status: open

### DW-66: `RestQueryBindingAttribute` runtime construction permits `EntitySource = None` with a non-null entity value even though the generator rejects that shape.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: RestQueryBindingAttribute
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md` summary: `RestQueryBindingAttribute` runtime construction permits `EntitySource = None` with a non-null entity value even though the generator rejects that shape. evidence: The attribute stores `EntityValue` unchanged when `entitySource == RestQueryBindingSource.None`, while `RestApiControllerEmitter` treats a value with `None` as invalid metadata; the mismatch pre-dates Story 2.1 and needs generator/contract hardening rather than a contract-seam patch.
status: open

### DW-67: `RestQueryBindingAttribute` preserves padded route/constant binding values that can fail generator route-parameter lookup later.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: RestQueryBindingAttribute
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md` summary: `RestQueryBindingAttribute` preserves padded route/constant binding values that can fail generator route-parameter lookup later. evidence: `ValidateValue` accepts and preserves values such as `" tenantId "`, while the generator route lookup compares binding route names to parsed route parameters without trimming; the issue is real but existed before this story and belongs with REST generator binding hardening.
status: open

### DW-68: Undefined `RestTenantSource` values can flow through the generator as non-standard tenant-source text.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: RestTenantSource
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md` summary: Undefined `RestTenantSource` values can flow through the generator as non-standard tenant-source text. evidence: `RoslynAttributeValueReader.GetEnumName` returns the numeric text for an out-of-range enum value, and generated `ResolveTenant` only handles `System` and `Route` specially before falling back to claims behavior; robust handling needs a generator diagnostic or explicit invalid-enum policy outside this contract-seam pass.
status: open

### DW-69: Generated command endpoints do not emit the canonical 1 MiB request-body limit used by platform gateway command and query controllers.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` summary: Generated command endpoints do not emit the canonical 1 MiB request-body limit used by platform gateway command and query controllers. evidence: `CommandsController.Submit`, `QueriesController.Submit`, validation controllers, replay, and stream endpoints declare `[RequestSizeLimit(1_048_576)]`, but generated command actions in `RestApiControllerEmitter.AppendCommandAction` accept `[FromBody]` contract payloads without a generated `RequestSizeLimit` attribute. The gap existed in the prior generator and was surfaced incidentally during the Story 2.2 follow-up review; fixing it needs a deliberate generated API-host payload-size policy.
status: open

### DW-70: Generated command problem mapping drops safe domain-rejection extensions such as `rejectionType` and `correctiveAction`.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: rejectionType
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` summary: Generated command problem mapping drops safe domain-rejection extensions such as `rejectionType` and `correctiveAction`. evidence: `DomainCommandRejectedExceptionHandler` emits `GatewayProblemDetailsExtensions.RejectionType` and `CorrectiveAction`, and `EventStoreGatewayClient` captures arbitrary non-standard ProblemDetails extensions in `EventStoreGatewayException.Extensions`, but generated controllers currently forward only correlation, tenant, reason, reasonCode, and filtered validation errors. The omission pre-dates this review pass and needs a deliberate generated API extension allowlist.
status: open

### DW-71: Generated command success responses hard-code `/api/v1/commands/status/{id}` as a relative status `Location`.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
archived: 2026-09-18

### DW-72: Generated query actions map caught `ArgumentException` messages directly into client-facing ProblemDetails.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: ArgumentException
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` summary: Generated query actions map caught `ArgumentException` messages directly into client-facing ProblemDetails. evidence: `RestApiControllerEmitter.AppendQueryAction` catches `ArgumentException` and calls `CreateProblem(..., ex.Message)`, bypassing the support-safe display-text filtering used for gateway exceptions. The catch existed before Story 2.2 and should be hardened separately with a fixed safe message or shared sanitizer.
status: open

### DW-73: Generated Sample API command success responses expose the generator's relative `/api/v1/commands/status/{id}` status location even though the external API host does not itself map that status route.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
archived: 2026-09-18

### DW-74: Sample DAPR app-id handlers append `dapr-app-id` and `dapr-api-token` headers without replacing preexisting values.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
archived: 2026-09-18

### DW-75: Raw SignalR hub leave calls do not validate projection type or tenant id before building and removing malformed group names.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
archived: 2026-09-18

### DW-76: `projectionType`/`tenantId` have no length bound and no control-character/newline rejection on either the SignalR join or leave path — only `scope` is capped (`MaxGroupScopeLength = 64`) and only colons + whitespace-only are rejected.

origin: migrated from legacy ledger ("Deferred from: code review of sprint-change-proposal-2026-07-07-signalr-hub-leave-validation (2026-07-07)"), 2026-08-30
location: "order\nlist"
reason: source_spec: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-signalr-hub-leave-validation.md` reviewed_commit: `a9fff8d7` (fix(signalr): validate projectionType/tenantId on hub leave path — committed to main by the concurrent bmad-loop mid-review; reviewed content byte-identical). summary: `projectionType`/`tenantId` have no length bound and no control-character/newline rejection on either the SignalR join or leave path — only `scope` is capped (`MaxGroupScopeLength = 64`) and only colons + whitespace-only are rejected. evidence: `ProjectionChangedHub.JoinGroupCoreAsync` (`:85-90`) and the mirrored `LeaveGroupCoreAsync` (`:161-170`, added by this change) validate null/blank + colon only. An oversized or control-char value (e.g. `new string('a', 100000)`, `"order\nlist"`, a NUL segment) passes both guards, is built into a group name by `BuildGroupName`, reaches `Groups.AddToGroupAsync`/`RemoveFromGroupAsync`, and is emitted to the Debug structured logs `ClientJoinedGroup`/`ClientLeftGroup` (`Log` EventIds 1080/1081). Impact is low — on leave it is a harmless idempotent no-op and the log is Debug-level structured (field capture, not string interpolation, so no log-record forgery); on join the unbounded key is retained in the static `_connectionGroups` set. This change is faithfully symmetric with the already-shipped join guards, so the gap is pre-existing and lives on both paths. disposition: defer to a scoped follow-up that hardens BOTH join and leave together (do not single-path patch). When it lands, also (a) decide the null/blank client-error contract — both paths currently throw `ArgumentException` (generic SignalR client error) rather than the descriptive `HubException` used for colon violations; and (b) add symmetric `LeaveGroupScoped` tenant-id-colon + null/blank tests to match the raw `LeaveGroup` coverage.
status: open

### DW-77: Guardrail evadable by a non-`*Dapr*`-named handler or a non-literal setter.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: n/a
reason: **Guardrail evadable by a non-`*Dapr*`-named handler or a non-literal setter.** The reflection guards (`SampleApiStructuralTests.SampleHostAssemblies_DeclareNoLocalDaprRoutingHandler`, `DaprRoutingHeaderOwnershipTests.AdminUiAssembly_DeclaresNoLocalDaprRoutingHandler`) key on `type.Name.Contains("Dapr")`, and the source scan (`DaprRoutingHeaderOwnershipGuardTests`, `:526`) matches only the literal `TryAddWithoutValidation("dapr-app-id"|"dapr-api-token")`. A future host handler named e.g. `SidecarHandler` that sets the header via `Headers.Add(...)`, a `const` name, `DefaultRequestHeaders`, or mixed casing (`"Dapr-App-Id"`) escapes both layers. Catches the realistic regression (a verbatim copy of the deleted `DaprAppIdHandler`) but not the full AD-18 surface. **Disposition:** do NOT extend the regex (standing regex-guardrail disposition — regex follow-ups don't converge); fold into a future scoped Roslyn/convention guardrail story.
status: open

### DW-78: Guard `hostRoots` is a hardcoded 3-entry list

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: DaprRoutingHeaderOwnershipGuardTests.cs:543
reason: **Guard `hostRoots` is a hardcoded 3-entry list** (`DaprRoutingHeaderOwnershipGuardTests.cs:543`). A new host directory is scanned only by the repo-wide literal-`TryAddWithoutValidation` `setterFiles` backstop, so a new host using a non-literal setter slips the per-host loop. Same Roslyn-guardrail consolidation.
status: open

### DW-79: Source-scan enumerates `*.cs` only

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: *.cs
reason: **Source-scan enumerates `*.cs` only** (`DaprRoutingHeaderOwnershipGuardTests.cs:551`), missing DAPR-header sets inside `.razor` `@code` blocks. Theoretical — routing handlers are not authored in razor markup.
status: open

### DW-80: Guard-test robustness (`DaprRoutingHeaderOwnershipGuardTests.cs:599`; `SampleApiStructuralTests.cs:33`). `RepositoryRoot()` throws `DirectoryNotFoundException` (rather than skipping) when the test binary runs detached from the source tree; the reflection guards call `Assembly.GetTypes()` without catching `ReflectionTypeLoadException`. Low likelihood; minor robustness.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: DaprRoutingHeaderOwnershipGuardTests.cs:599
reason: **Guard-test robustness** (`DaprRoutingHeaderOwnershipGuardTests.cs:599`; `SampleApiStructuralTests.cs:33`). `RepositoryRoot()` throws `DirectoryNotFoundException` (rather than skipping) when the test binary runs detached from the source tree; the reflection guards call `Assembly.GetTypes()` without catching `ReflectionTypeLoadException`. Low likelihood; minor robustness.
status: open

### DW-81: Public `AddEventStoreDaprServiceInvocation` does not format-validate `appId`

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: EventStoreServiceCollectionExtensions.cs:68
reason: **Public `AddEventStoreDaprServiceInvocation` does not format-validate `appId`** (`EventStoreServiceCollectionExtensions.cs:68`) — only `ThrowIfNullOrWhiteSpace`. On the published `Hexalith.EventStore.Client` package, an external consumer passing a whitespace/control-char `appId` gets fail-fast broken routing (a CR/LF value is dropped by `TryAddWithoutValidation`). In-repo callers pass safe literals, so no live impact. The one item on new public surface worth a later hardening patch (independent of the regex-guard disposition).
status: open

### DW-82: `apiToken` whitespace handling inconsistent with `appId`

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-outbound-dapr-routing-header-ownership (2026-07-10)"), 2026-08-30
location: DaprServiceInvocationHandler.cs:16
reason: **`apiToken` whitespace handling inconsistent with `appId`** (`DaprServiceInvocationHandler.cs:16`). `apiToken is { Length: > 0 }` forwards a whitespace-only token (`" "`) as authoritative, whereas `appId` is `ThrowIfNullOrWhiteSpace`-guarded. Matches the deleted handlers' `!string.IsNullOrEmpty` behavior (no regression); operator error fails fast at the sidecar. Optional consistency nit.
status: open

### DW-83: No production code calls `IProjectionStateEraser` — only the DI registration in `ServiceCollectionExtensions.cs:56`. The end-to-end read-model/checkpoint drift fix is unreachable from any wired in-tree path; it depends on a future Admin/GDPR-1 erasure trigger. Deferred as expected — the caller is exactly what the governing-contract decision (see Story 1.9 Review Findings) resolves. Do not add a caller in isolation before that decision.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-11)"), 2026-08-30
archived: 2026-09-18

### DW-84: Complete the in-progress Story 1.9 erasure refactor so the Server and Server.Tests projects compile again.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-85: Preserve or explicitly version the released erasure API surface being removed by Story 1.9.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-86: Make Story 1.9 erasure capability DI fail closed for custom stores and checkpoint trackers.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-87: Wire and verify Story 1.9 projection slot discovery and canonical read-model address ownership end to end.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-88: Finish the Story 1.9 persisted erasure coordinator and lifecycle/admin boundary before exposing partial seams.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-89: Make in-memory read-model writes atomic with batch accessor ETag compare-and-set operations.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Make in-memory read-model writes atomic with batch accessor ETag compare-and-set operations. evidence: Pre-existing `SaveAsync`, `TrySaveAsync`, `TryEraseAsync`, and `SeedRaw` paths do not share the batch accessor's `_gate`, so a true concurrent write can occur between the fake accessor's ETag check and assignment and be overwritten while conditional success is reported.
status: open

### DW-90: Add committed-state tests for new fail-closed / drain-identity paths (distinct messageId≠correlationId drain, Expired-outcome actor commit, SubmitCommandHandler identity guards).

origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
location: AggregateActor.cs:173-176
reason: source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md` summary: Add committed-state tests for new fail-closed / drain-identity paths (distinct messageId≠correlationId drain, Expired-outcome actor commit, SubmitCommandHandler identity guards). evidence: Message-keyed drain handoff + advisory-status identity is only exercised where messageId ≡ correlationId; the actor commit of a staged Expired idempotency mutation (AggregateActor.cs:173-176) is undriven; SubmitCommandHandler fail-closed guards (SubmitCommandHandler.cs:65-71,117-124) are untested.
status: open

### DW-91: Cover AdminTraceQueryController correlation-index resolution/ambiguity path and accept the advisory-index not-found degradation.

origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
location: AdminTraceQueryController.cs:59-78
reason: source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md` summary: Cover AdminTraceQueryController correlation-index resolution/ambiguity path and accept the advisory-index not-found degradation. evidence: The resolve→ambiguity-409→message-primary-read branch (AdminTraceQueryController.cs:59-78) is unexercised because Dw3TestUtilities.cs:185 builds the controller with a null index; not-found-when-index-missing is inherent to an advisory index queried by correlationId (state scan forbidden).
status: open

### DW-92: Bound the correlation-index overflow marker so a hot shared correlationId is not permanently ambiguous.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-93: (Story 4.4) Prevent domain re-execution when a Recoverable (stored-but-unpublished) idempotency record expires after the retention window.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-94: Correct the drain activity message-id telemetry tag for legacy correlation-keyed drain records.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
archived: 2026-09-18

### DW-95: Retained legacy aggregate-wide checkpoint feeds the empty-stream drift branch (`ProjectionUpdateOrchestrator.cs:129`). An erased/recreated identity that had a legacy checkpoint and later reads an empty stream logs spurious `CheckpointDriftDetected` (diagnostic noise only — no mutation, no suppressed delivery). Direct consequence of the human-approved Option A retained-legacy-key relaxation; revisit if diagnostic noise is a problem or if a bounded legacy-key cleanup is added.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of story-1.9 (2026-07-13)"), 2026-08-30
archived: 2026-09-18

### DW-96: `ProjectionUpdateOrchestrator` narrowed `public`→`internal` and dead erase surface. The visibility narrowing is disclosed/justified (verified no external consumer; DI via interfaces; no PublicAPI baseline). `IProjectionReadModelAddressFactory.CreateAggregateOwnedManifest` and `ProjectionEraseOutcomeKind.Denied` are currently unused; they become live only if the slot-completeness decision (Review Finding) adopts manifest-based erasure. Remove or wire per that decision.

origin: migrated from legacy ledger ("Deferred from: code review of story-1.9 (2026-07-13)"), 2026-08-30
location: ProjectionUpdateOrchestrator
reason: `ProjectionUpdateOrchestrator` narrowed `public`→`internal` and dead erase surface. The visibility narrowing is disclosed/justified (verified no external consumer; DI via interfaces; no PublicAPI baseline). `IProjectionReadModelAddressFactory.CreateAggregateOwnedManifest` and `ProjectionEraseOutcomeKind.Denied` are currently unused; they become live only if the slot-completeness decision (Review Finding) adopts manifest-based erasure. Remove or wire per that decision.
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-96, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-97: DAPR batch accessor infers key existence from ETag presence (`DaprReadModelBatchStateAccessor.cs:23`): `string.IsNullOrEmpty(etag) ? absent : present`. An ETag-less store or value reads as absent even when a value is returned. Masked on Redis (always returns ETags), and the resumable CAS protocol fundamentally requires ETags, so no impact on the supported backend. Revisit only if a non-ETag state store is ever qualified; existence should then key off value presence, not the ETag.

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13)"), 2026-08-30
location: DaprReadModelBatchStateAccessor.cs:23
reason: DAPR batch accessor infers key existence from ETag presence (`DaprReadModelBatchStateAccessor.cs:23`): `string.IsNullOrEmpty(etag) ? absent : present`. An ETag-less store or value reads as absent even when a value is returned. Masked on Redis (always returns ETags), and the resumable CAS protocol fundamentally requires ETags, so no impact on the supported backend. Revisit only if a non-ETag state store is ever qualified; existence should then key off value presence, not the ETag.
status: open

### DW-98: Corrupt/tampered base64 in a stored envelope throws `FormatException` out of `GetAsync`/reconcile (`ReadModelBatchEnvelope.PreviousBytes`/`CandidateBytes`, lines 72-78): only `JsonException` is guarded in `FromBytes`, but the subsequent `Convert.FromBase64String` on `prev`/`cand` is unguarded. Requires storage corruption/tampering (outside the normal contract). Cheap one-line hardening (guard the base64 decode → treat as unreadable/legacy) if robustness against corrupted state is later required.

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13)"), 2026-08-30
location: FormatException
reason: Corrupt/tampered base64 in a stored envelope throws `FormatException` out of `GetAsync`/reconcile (`ReadModelBatchEnvelope.PreviousBytes`/`CandidateBytes`, lines 72-78): only `JsonException` is guarded in `FromBytes`, but the subsequent `Convert.FromBase64String` on `prev`/`cand` is unguarded. Requires storage corruption/tampering (outside the normal contract). Cheap one-line hardening (guard the base64 decode → treat as unreadable/legacy) if robustness against corrupted state is later required.
status: open

### DW-99: Orphaned foreign envelope from an abandoned/never-retried batch permanently blocks any other batch touching that logical key (`ReadModelBatchProtocol.cs:262-269`, InstallAsync foreign-envelope branch → `OptimisticConflict` with no cleanup of the foreign envelope). Inherent to the resumable no-TTL design where prepared/aborting markers and envelopes are retained until reconciled. Decision 5 explicitly defers a bounded retention/cleanup horizon to Story 1.13 (together with its production delivery-checkpoint/dedup contract).

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13)"), 2026-08-30
location: ReadModelBatchProtocol.cs:262-269
reason: Orphaned foreign envelope from an abandoned/never-retried batch permanently blocks any other batch touching that logical key (`ReadModelBatchProtocol.cs:262-269`, InstallAsync foreign-envelope branch → `OptimisticConflict` with no cleanup of the foreign envelope). Inherent to the resumable no-TTL design where prepared/aborting markers and envelopes are retained until reconciled. Decision 5 explicitly defers a bounded retention/cleanup horizon to Story 1.13 (together with its production delivery-checkpoint/dedup contract).
status: open

### DW-100: (HARD GATE for Story 1.12/1.13) Run the `ReadModelBatchLiveSidecarTests` lane in a working Tier-3 (real Redis/DAPR) environment before wiring the coordinated batch into production projection dispatch, and add the omitted Task-8 scenarios.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13, decision follow-up)"), 2026-08-30
gate: 1-12, 1-13
archived: 2026-09-18

### DW-101: Body-ETag fallback can surface a non-gateway validator on a `ProjectionBacked` route with no gateway ETag (`QueriesController.cs:210` `ETag: gatewayETag ?? producerMetadata?.ETag`; `EventStoreGatewayClient.cs:343` `ETag = eTag ?? normalized.ETag`). Reachable only when a non-conformant producer claims `ProjectionBacked`, omits `ProjectionType` (skipping the gateway ETag fetch) AND fabricates an ETag; the platform projection actor never sets `metadata.ETag`, and the leaked value cannot drive a false 304 (that needs the gateway-computed `currentETag`). Hardening: only surface the gateway-issued ETag as the opaque validator; do not fall back to producer body ETag.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: QueriesController.cs:210
reason: Body-ETag fallback can surface a non-gateway validator on a `ProjectionBacked` route with no gateway ETag (`QueriesController.cs:210` `ETag: gatewayETag ?? producerMetadata?.ETag`; `EventStoreGatewayClient.cs:343` `ETag = eTag ?? normalized.ETag`). Reachable only when a non-conformant producer claims `ProjectionBacked`, omits `ProjectionType` (skipping the gateway ETag fetch) AND fabricates an ETag; the platform projection actor never sets `metadata.ETag`, and the leaked value cannot drive a false 304 (that needs the gateway-computed `currentETag`). Hardening: only surface the gateway-issued ETag as the opaque validator; do not fall back to producer body ETag. [edge-case-hunter]
status: open

### DW-102: Real-path handler-vs-projection route→provenance proof (`QueryResponseProvenanceE2ETests`) runs in no CI workflow — it is Tier-3 (gated out; the dev ran it manually in source-debug mode), and the Tier-2 persistence test injects the route result rather than exercising `HandlerAwareQueryRouter`'s real handler-vs-projection selection. Pre-existing Tier-3-not-gated constraint tracked by Epic 3 Story 3.1. Lighter-weight guard: a Tier-2 `Server.Tests` test that resolves the real `HandlerAwareQueryRouter` + handler registry and asserts stamped provenance per route.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: QueryResponseProvenanceE2ETests
reason: Real-path handler-vs-projection route→provenance proof (`QueryResponseProvenanceE2ETests`) runs in no CI workflow — it is Tier-3 (gated out; the dev ran it manually in source-debug mode), and the Tier-2 persistence test injects the route result rather than exercising `HandlerAwareQueryRouter`'s real handler-vs-projection selection. Pre-existing Tier-3-not-gated constraint tracked by Epic 3 Story 3.1. Lighter-weight guard: a Tier-2 `Server.Tests` test that resolves the real `HandlerAwareQueryRouter` + handler registry and asserts stamped provenance per route. [verification-gap+blind-hunter+acceptance-auditor]
status: open

### DW-103: Single-source the canonical provenance-name formatter — `QueriesController.cs:127` emits the `X-Hexalith-Query-Provenance` header via `Provenance.ToString()` while the client (`GetProvenanceHeader`) and generated controller use an explicit canonical `nameof` switch. Safe today only because provenance is normalized to a defined value before that line; a future path that reaches it with an out-of-range value would emit a numeric string the strict parser maps to `Unknown`.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: QueriesController.cs:127
reason: Single-source the canonical provenance-name formatter — `QueriesController.cs:127` emits the `X-Hexalith-Query-Provenance` header via `Provenance.ToString()` while the client (`GetProvenanceHeader`) and generated controller use an explicit canonical `nameof` switch. Safe today only because provenance is normalized to a defined value before that line; a future path that reaches it with an out-of-range value would emit a numeric string the strict parser maps to `Unknown`. [blind-hunter+acceptance-auditor]
status: open

### DW-104: Duplicated projection-evidence sanitization across two assemblies — server `QueriesController.NormalizeProducerMetadata` and client `EventStoreGatewayClient.NormalizeMetadata` independently null `{ETag, IsNotModified, IsStale, ProjectionVersion}` for non-projection routes. Any future field added to "projection evidence" must be cleared in both, in two assemblies, or a leak/asymmetry appears. Consider a shared helper.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: QueriesController.NormalizeProducerMetadata
reason: Duplicated projection-evidence sanitization across two assemblies — server `QueriesController.NormalizeProducerMetadata` and client `EventStoreGatewayClient.NormalizeMetadata` independently null `{ETag, IsNotModified, IsStale, ProjectionVersion}` for non-projection routes. Any future field added to "projection evidence" must be cleared in both, in two assemblies, or a leak/asymmetry appears. Consider a shared helper. [blind-hunter]
status: open

### DW-105: Minor test-hardening — the converter `Write` out-of-range default branch (`QueryResponseProvenanceJsonConverter.Write` → `nameof(Unknown)`) and the `EnforceFreshnessPolicy` non-`ProjectionBacked` → 400 branch lack direct assertions; both are fail-safe downstream (values normalized before serialization; freshness fails closed regardless), so low reachability.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: QueryResponseProvenanceJsonConverter.Write
reason: Minor test-hardening — the converter `Write` out-of-range default branch (`QueryResponseProvenanceJsonConverter.Write` → `nameof(Unknown)`) and the `EnforceFreshnessPolicy` non-`ProjectionBacked` → 400 branch lack direct assertions; both are fail-safe downstream (values normalized before serialization; freshness fails closed regardless), so low reachability. [verification-gap]
status: open

### DW-106: Weak-ETag rejection is not route-aware — `EventStoreGatewayClient.GetETag` (called at `:174` on the 200 path, `:404` throw) rejects a weak `ETag` before provenance is known, failing the whole query even for a non-projection route that would discard the ETag. The EventStore server only ever emits strong ETags, so this is robustness against a header-rewriting intermediary, not a live path.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-query-response-provenance-contract-and-route-aware-gateway-etag (2026-07-13)"), 2026-08-30
location: EventStoreGatewayClient.GetETag
reason: Weak-ETag rejection is not route-aware — `EventStoreGatewayClient.GetETag` (called at `:174` on the 200 path, `:404` throw) rejects a weak `ETag` before provenance is known, failing the whole query even for a non-projection route that would discard the ETag. The EventStore server only ever emits strong ETags, so this is robustness against a header-rewriting intermediary, not a live path. [edge-case-hunter]
status: open

### DW-107: `HasFailures` blast radius on named-metadata rejection — a single domain service returning malformed/version-skewed named-projection metadata sets `hasFailures`, which makes `AdminOperationalIndexHostedService.StartAsync` skip ALL admin index writes AND the named-route catalog `Replace` for every app in the refresh; this is a once-at-startup load with no periodic retry, so named dispatch is disabled process-wide until restart. The atomic all-or-nothing publish is spec-mandated (§2); the cross-app coupling + missing refresh cadence is the broader concern.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
archived: 2026-09-18

### DW-108: `DomainProjectionHandlerResult.AlreadyCompleted()` has no state overload — a hand-written state-bearing named handler that returns `AlreadyCompleted()` on retry yields null state, so the coordinator advances the projection checkpoint without completing the deferred actor/ETag write (Resolved Contract #3/#5). The legacy adapter (always `Completed`+state) and batch-persistence handlers (null state, no actor write) are unaffected, so reach is narrow. Recommend adding an `AlreadyCompleted(JsonElement? state)` factory overload.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/DomainProjectionHandlerResult.cs:25
reason: `DomainProjectionHandlerResult.AlreadyCompleted()` has no state overload — a hand-written state-bearing named handler that returns `AlreadyCompleted()` on retry yields null state, so the coordinator advances the projection checkpoint without completing the deferred actor/ETag write (Resolved Contract #3/#5). The legacy adapter (always `Completed`+state) and batch-persistence handlers (null state, no actor write) are unaffected, so reach is narrow. Recommend adding an `AlreadyCompleted(JsonElement? state)` factory overload. [src/Hexalith.EventStore.DomainService/DomainProjectionHandlerResult.cs:25] [acceptance-auditor]
status: open

### DW-109: `ProjectionDeliveryRetryWorkItem.CreateWorkId` omits app id / service version / fingerprint — `WorkId = SHA-256(tenant/domain/aggregate/headSequence)`, so two `(appId, serviceVersion)` bindings serving the same domain+head collide on one ledger item; the second binding's app/version consistency check then fails and it defers forever. Affects blue/green or multi-version rollout of the same domain.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
location: WorkId = SHA-256(tenant/domain/aggregate/headSequence
reason: `ProjectionDeliveryRetryWorkItem.CreateWorkId` omits app id / service version / fingerprint — `WorkId = SHA-256(tenant/domain/aggregate/headSequence)`, so two `(appId, serviceVersion)` bindings serving the same domain+head collide on one ledger item; the second binding's app/version consistency check then fails and it defers forever. Affects blue/green or multi-version rollout of the same domain. [src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorkItem.cs:44] [blind-hunter]
status: open

### DW-110: `DomainProjectionCatalogRegistry` is in-memory and empty after a domain-service restart — until the gateway re-queries `/admin/operational-index-metadata` (a startup-only load), `Contains(fingerprint)` is false → `/project/v2` returns 400 `UnsupportedCapability` → the coordinator defers/retries. Overlaps the metadata refresh-cadence gap above.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
archived: 2026-09-18

### DW-111: Retry taxonomy remainder (from code-review decision D2) — poison retry ceiling / dead-letter, catalog fingerprint/version re-bind, permanent-`4xx` handling, and terminal-only ledger cleanup for the named-projection delivery retry subsystem. Drift-ahead was made terminal in Story 1.12; the rest is deferred to Story 1.13 (poison/duplicate/dedup horizon) plus a dedicated retry-cleanup-policy story. Note a `/project/v2` `4xx` can be a transient metadata-refresh race, so terminal-`4xx` classification must be designed alongside the dedup horizon, not assumed permanent.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
location: /project/v2
reason: Retry taxonomy remainder (from code-review decision D2) — poison retry ceiling / dead-letter, catalog fingerprint/version re-bind, permanent-`4xx` handling, and terminal-only ledger cleanup for the named-projection delivery retry subsystem. Drift-ahead was made terminal in Story 1.12; the rest is deferred to Story 1.13 (poison/duplicate/dedup horizon) plus a dedicated retry-cleanup-policy story. Note a `/project/v2` `4xx` can be a transient metadata-refresh race, so terminal-`4xx` classification must be designed alongside the dedup horizon, not assumed permanent. [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:227] [blind-hunter+edge-case-hunter]
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-111, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-112: The active-rebuild gate remains a pre-existing check-then-act race: `ProjectionEraseCoordinator` snapshots `HasActiveOperatorRebuildForDomainAsync` before lifecycle admission, so a rebuild can become active between the check and the actor call while `allowFreshBegin` remains true. Closing this requires rebuild admission to share a persisted lifecycle fence rather than relying on the existing point-in-time store query.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-14)"), 2026-08-30
archived: 2026-09-18

### DW-113: HTTP 200 with a literal `null` metadata body is treated as a successful empty load (`AdminOperationalIndexHostedService.cs:96`), so existing admin indexes can be rewritten from an incomplete response. This behavior predates Story 1.12; harden the legacy metadata loader to classify a null success body as a failed load before any index write or catalog replacement.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-14, chunk 1)"), 2026-08-30
archived: 2026-09-18

### DW-114: Activation outbox completion treats a completed named-dispatch call as durable even when `TryDispatchAsync` returns `false`, and the `finally` block can remove the activation after a later legacy delivery failure. Preserve the activation until every required delivery surface has durably completed.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:183
reason: Activation outbox completion treats a completed named-dispatch call as durable even when `TryDispatchAsync` returns `false`, and the `finally` block can remove the activation after a later legacy delivery failure. Preserve the activation until every required delivery surface has durably completed. [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:183`]
status: open

### DW-115: The query lifecycle overlay recognizes `Rebuilding` but leaves an in-flight `Erasing` projection indistinguishable from its prior payload state. Define and expose the pre-existing erase-query visibility policy in the erasure lifecycle scope.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:199
reason: The query lifecycle overlay recognizes `Rebuilding` but leaves an in-flight `Erasing` projection indistinguishable from its prior payload state. Define and expose the pre-existing erase-query visibility policy in the erasure lifecycle scope. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:199`]
status: open

### DW-116: A blank head-event `MessageId` logs and returns from named dispatch without persisting retry work, so an aggregate with no later trigger can remain unprojected indefinitely. Add a durable malformed-identity disposition or recovery trigger.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:143
reason: A blank head-event `MessageId` logs and returns from named dispatch without persisting retry work, so an aggregate with no later trigger can remain unprojected indefinitely. Add a durable malformed-identity disposition or recovery trigger. [`src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:143`]
status: open

### DW-117: Async projection-handler convention discovery is all-or-nothing: the presence of one manual `IAsyncDomainProjectionHandler` registration disables discovery of every other implementation. Deduplicate per implementation instead of suppressing the full scan.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:375
reason: Async projection-handler convention discovery is all-or-nothing: the presence of one manual `IAsyncDomainProjectionHandler` registration disables discovery of every other implementation. Deduplicate per implementation instead of suppressing the full scan. [`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:375`]
status: open

### DW-118: Lifecycle actor source files contain multiple enum, record, interface, and persisted-state declarations despite the repository's one-type-per-file rule. Split the pre-existing declarations during a scoped structural cleanup.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Actors/IProjectionLifecycleActor.cs:12
reason: Lifecycle actor source files contain multiple enum, record, interface, and persisted-state declarations despite the repository's one-type-per-file rule. Split the pre-existing declarations during a scoped structural cleanup. [`src/Hexalith.EventStore.Server/Actors/IProjectionLifecycleActor.cs:12`]
status: open

### DW-119: Operational-index metadata request binding can deserialize `Domains` as null and then dereference `request.Domains.Count`, returning an internal error instead of a bounded malformed-request response. Add null-safe request validation.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:248
reason: Operational-index metadata request binding can deserialize `Domains` as null and then dereference `request.Domains.Count`, returning an internal error instead of a bounded malformed-request response. Add null-safe request validation. [`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:248`]
status: open

### DW-120: Erase lifecycle admission does not reject blank operation/digest values, and an unknown persisted phase can fall through to a fresh erase admission. Validate erase identity and fail closed on undefined lifecycle phases.

origin: migrated from legacy ledger ("Deferred from: code review of 1-19-correct-paged-rebuild-and-replay-equivalence (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs:68
reason: Erase lifecycle admission does not reject blank operation/digest values, and an unknown persisted phase can fall through to a fresh erase admission. Validate erase identity and fail closed on undefined lifecycle phases. [`src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs:68`]
status: open

### DW-121: Make dev-auto review finalization conditional on fail-closed artifact decisions instead of unconditionally marking every reviewed spec `done`.

origin: migrated from legacy ledger ("Deferred from: code review of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-16)"), 2026-08-30
location: agents/skills/bmad-dev-auto/step-04-review.md
reason: source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` summary: Make dev-auto review finalization conditional on fail-closed artifact decisions instead of unconditionally marking every reviewed spec `done`. evidence: Story 1.20 requires any `final_decision: still blocked` or `authorize_consumer_migration: false` result to remain non-`done`, but `.agents/skills/bmad-dev-auto/step-04-review.md` currently sets `status: done` unconditionally after review; a generic guard and workflow test are needed so later automation cannot mistake a non-authorizing proof packet for completed closure.
status: open

### DW-122: Repair or explicitly disposition the named-projection lifecycle cleanup defect before selecting an approved parity runtime.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: exact-SHA gate of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-16)"), 2026-08-30
archived: 2026-09-18

### DW-123: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20's tested runtime SHA.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)"), 2026-08-30
archived: 2026-09-18

### DW-124: Reconcile stale sample domain registrations and prove Tenants handler routing in the real source topology before selecting the Story 1.20 runtime.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 1.20 current-HEAD source-topology gate (2026-07-17)"), 2026-08-30
archived: 2026-09-18

### DW-125: Reconcile the stale `Hexalith.EventStore.Server.Tests` CA2007 baseline exception in `_bmad-output/project-context.md:65`. The exception predates this review, while Story 3.1 now records an unfiltered Release run with 2,626 passed, 25 skipped, and no failure; leaving the old statement active can cause future agents to exclude a blocking deterministic lane from baseline validation.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate.md (2026-07-18)"), 2026-08-30
location: _bmad-output/project-context.md:65
reason: Reconcile the stale `Hexalith.EventStore.Server.Tests` CA2007 baseline exception in `_bmad-output/project-context.md:65`. The exception predates this review, while Story 3.1 now records an unfiltered Release run with 2,626 passed, 25 skipped, and no failure; leaving the old statement active can cause future agents to exclude a blocking deterministic lane from baseline validation.
status: open

### DW-126: Reconfirmed the existing Story 1.19 erase-query visibility gap at candidate `8aa6d0f0a417034d0c46eb9506fb7196a013401b`: a stable `Erasing` lifecycle falls through `QueryRouter.ApplyPersistedLifecycle`, so producer `Current` can remain projection-confirmed and mutation-eligible while read-model targets are being erased. The policy choice (for example `Unknown`, `Unavailable`, or rejecting the query) remains intentionally deferred under the earlier Story 1.19 ledger entry; this review adds no duplicate implementation owner.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-11-complete-projection-freshness-lifecycle (2026-07-16)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:248
reason: Reconfirmed the existing Story 1.19 erase-query visibility gap at candidate `8aa6d0f0a417034d0c46eb9506fb7196a013401b`: a stable `Erasing` lifecycle falls through `QueryRouter.ApplyPersistedLifecycle`, so producer `Current` can remain projection-confirmed and mutation-eligible while read-model targets are being erased. The policy choice (for example `Unknown`, `Unavailable`, or rejecting the query) remains intentionally deferred under the earlier Story 1.19 ledger entry; this review adds no duplicate implementation owner. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:248`]
status: open

### DW-127: Commit `ba203bde` is unbuildable in isolation: it converts the only local definition of `Microsoft.Extensions.TimeProvider.Testing` to `PackageVersion Update` while `references/Hexalith.Builds` was still pinned at `edbaeaed`, whose central props do not define the package, so CPM restore of `Server.Tests`, `Server.LiveSidecar.Tests`, and `Admin.Server.Tests` fails with NU1010 at that commit; coherence arrives only with `ea6ce49b`'s Builds bump to `cfafcbf1`. Bisect/rollback hazard only — history is already on `main`, so no rewrite; note it when bisecting across 2026-07-17.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-17)"), 2026-08-30
location: references/Hexalith.Builds
reason: Commit `ba203bde` is unbuildable in isolation: it converts the only local definition of `Microsoft.Extensions.TimeProvider.Testing` to `PackageVersion Update` while `references/Hexalith.Builds` was still pinned at `edbaeaed`, whose central props do not define the package, so CPM restore of `Server.Tests`, `Server.LiveSidecar.Tests`, and `Admin.Server.Tests` fails with NU1010 at that commit; coherence arrives only with `ea6ce49b`'s Builds bump to `cfafcbf1`. Bisect/rollback hazard only — history is already on `main`, so no rewrite; note it when bisecting across 2026-07-17.
status: open

### DW-128: The GitHub login-format regex `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$` in the proof-packet allowlist predicate (and mirrored in spec verification command 2) accepts trailing and consecutive hyphens that GitHub forbids (`jpiquot-`, `a--b`). Pre-existing packet behavior, currently moot because the spec predicate pins exact membership `["jpiquot"]`; tighten to `^(?=[A-Za-z0-9-]{1,39}$)[A-Za-z0-9](?:-?[A-Za-z0-9])*$` next time the packet validator is opened under an approved gate-logic change (the lookahead keeps GitHub's 39-character cap; the bare `^[A-Za-z0-9](?:-?[A-Za-z0-9]){0,38}$` form previously recorded here admits logins up to 77 characters).

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-17)"), 2026-08-30
location: _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
reason: The GitHub login-format regex `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$` in the proof-packet allowlist predicate (and mirrored in spec verification command 2) accepts trailing and consecutive hyphens that GitHub forbids (`jpiquot-`, `a--b`). Pre-existing packet behavior, currently moot because the spec predicate pins exact membership `["jpiquot"]`; tighten to `^(?=[A-Za-z0-9-]{1,39}$)[A-Za-z0-9](?:-?[A-Za-z0-9])*$` next time the packet validator is opened under an approved gate-logic change (the lookahead keeps GitHub's 39-character cap; the bare `^[A-Za-z0-9](?:-?[A-Za-z0-9]){0,38}$` form previously recorded here admits logins up to 77 characters). [`_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`]
status: open

### DW-129: The former EventStore-local `Microsoft.Playwright` declaration was removed; effective MSBuild evaluation now resolves it exactly once from Hexalith.Builds, and the import-only wrapper guard rejects future local masks.

status: done 2026-07-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-17)"), 2026-08-30
archived: 2026-09-18

### DW-130: The shared AI-instruction baseline rewrite (`CLAUDE.md`/`AGENTS.md`/`.github/copilot-instructions.md`, commit `4ee739d6`) dropped two safeguards the previous text carried: the Agent Skills clause banning skills whose *resolved canonical path* (symlink target) lies inside `references/`, and the standalone-clone rule authorizing initialization of root-declared `references/` submodules — a fresh standalone EventStore clone now has no permitted path to the mandatory `hexalith-llm-instructions.md` baseline while being ordered to stop without it (bootstrap deadlock). The baseline is shared normalized text owned upstream in Hexalith.AI.Tools; route the fix there and re-propagate, do not edit the three entry points unilaterally.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18)"), 2026-08-30
location: CLAUDE.md
reason: The shared AI-instruction baseline rewrite (`CLAUDE.md`/`AGENTS.md`/`.github/copilot-instructions.md`, commit `4ee739d6`) dropped two safeguards the previous text carried: the Agent Skills clause banning skills whose *resolved canonical path* (symlink target) lies inside `references/`, and the standalone-clone rule authorizing initialization of root-declared `references/` submodules — a fresh standalone EventStore clone now has no permitted path to the mandatory `hexalith-llm-instructions.md` baseline while being ordered to stop without it (bootstrap deadlock). The baseline is shared normalized text owned upstream in Hexalith.AI.Tools; route the fix there and re-propagate, do not edit the three entry points unilaterally.
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-130, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-131: `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:21,208` attributes the domain-centric rule to a CLAUDE.md "Domain-Module Authoring" section that does not exist (the rule lives in `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`); pre-existing at baseline `a9718a21`, the guardrail itself enforces the rule on code and is unaffected — fix the citation next time the test file is opened.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18)"), 2026-08-30
location: tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:21,208
reason: `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:21,208` attributes the domain-centric rule to a CLAUDE.md "Domain-Module Authoring" section that does not exist (the rule lives in `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`); pre-existing at baseline `a9718a21`, the guardrail itself enforces the rule on code and is unaffected — fix the citation next time the test file is opened.
status: open

### DW-132: The Story 1.20 exact-membership literal `["jpiquot"]`×4 now exists in four synchronized places (both packet validators, spec Verification command 2, the allowlist itself), reducing the retained key-set/non-empty/uniqueness predicates to dead code, and the evidence-commit-A validator still lacks the login-format regex the candidate-gate validator carries. Consolidate (single source or documented sync list) at the next approved gate-logic change, together with the ledgered regex tightening above.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18)"), 2026-08-30
location: n/a
reason: The Story 1.20 exact-membership literal `["jpiquot"]`×4 now exists in four synchronized places (both packet validators, spec Verification command 2, the allowlist itself), reducing the retained key-set/non-empty/uniqueness predicates to dead code, and the evidence-commit-A validator still lacks the login-format regex the candidate-gate validator carries. Consolidate (single source or documented sync list) at the next approved gate-logic change, together with the ledgered regex tightening above.
status: open

### DW-133: Commit `01830544` ("fix: add commit message validation requirement with commitlint") is misdescribed and mixed: 377 of its 378 lines are the unrelated Story 3.5 artifact `3-5-shared-package-catalog-and-source-package-reference-modes.md`, and its 1-line Copilot entry-point edit is what desynchronized the three shared entry points (see the loop-4 Decision items in the spec). Third mixed-bundle recurrence recorded by this spec's reviews; history is already on `main`, so no rewrite — note it when bisecting across 2026-07-18.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: 3-5-shared-package-catalog-and-source-package-reference-modes.md
reason: Commit `01830544` ("fix: add commit message validation requirement with commitlint") is misdescribed and mixed: 377 of its 378 lines are the unrelated Story 3.5 artifact `3-5-shared-package-catalog-and-source-package-reference-modes.md`, and its 1-line Copilot entry-point edit is what desynchronized the three shared entry points (see the loop-4 Decision items in the spec). Third mixed-bundle recurrence recorded by this spec's reviews; history is already on `main`, so no rewrite — note it when bisecting across 2026-07-18.
status: open

### DW-134: Story 3.5's dependency-mode truth table has no row for build configurations other than Debug/Release (e.g. `Staging`, case variants) with `UseHexalithProjectReferences` unset, and its required test list omits the case — implementers may choose either reference-graph edge with no specified expectation. Owned by Story 3.5's active cycle; route into its review, do not patch from a 1.20 review.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
archived: 2026-09-18

### DW-135: Story 3.5's contract does not define precedence when explicit `UseNuGetDeps` and explicit `UseHexalithProjectReferences` conflict ("preserve its existing mapping" vs "normalize … one authoritative boolean" with no truth-table row, AC, or test naming the winner) — contradictory caller properties could activate both or neither reference edge. Owned by Story 3.5's active cycle.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
archived: 2026-09-18

### DW-136: The seven root-submodule source bumps ratified into `ea6ce49b` are compile-verified only: the per-project unit-test CI runs in package mode (`UseHexalithProjectReferences` defaults false), and the only source-mode lane is the filtered `tenants-source-mode` launch-settings job — a behavioral regression in bumped Commons/FrontComposer/PolymorphicSerializations/Tenants source that still compiles leaves all CI green. Consider a periodic/advisory source-mode lane running a representative unit-test subset, or record source-mode validation evidence in the ratifying artifact; CI-lane design belongs with Story 3.5's dual-mode validation scope.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: UseHexalithProjectReferences
reason: The seven root-submodule source bumps ratified into `ea6ce49b` are compile-verified only: the per-project unit-test CI runs in package mode (`UseHexalithProjectReferences` defaults false), and the only source-mode lane is the filtered `tenants-source-mode` launch-settings job — a behavioral regression in bumped Commons/FrontComposer/PolymorphicSerializations/Tenants source that still compiles leaves all CI green. Consider a periodic/advisory source-mode lane running a representative unit-test subset, or record source-mode validation evidence in the ratifying artifact; CI-lane design belongs with Story 3.5's dual-mode validation scope.
status: open

### DW-137: Pin `commitlint.config.mjs` to LF in `.gitattributes` or make its exact-content contract line-ending agnostic.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
archived: 2026-09-18

### DW-138: Add durable process-level commitlint behavior fixtures for valid, subject-case, header-length, and body-line policies.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: @commitlint/*
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md` summary: Add durable process-level commitlint behavior fixtures for valid, subject-case, header-length, and body-line policies. evidence: The existing Contracts test pins the three-line config text but does not execute commitlint, so a future grouped `@commitlint/*` update could change delegated defaults without the regression guard detecting it.
status: open

### DW-139: Reconcile the documented literal lowercase-start rule with commitlint's weaker default `subject-case` behavior.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md` summary: Reconcile the documented literal lowercase-start rule with commitlint's weaker default `subject-case` behavior. evidence: The restored default rejects `fix: Update status` but accepts descriptions beginning with digits or symbols before uppercase text, despite shared guidance requiring the description to start with a lowercase letter.
status: open

### DW-140: RESOLVED 2026-07-20 — the repository excludes `chore` and uses specific non-release types, including `build(deps)` for automated dependency maintenance.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
archived: 2026-09-18

### DW-141: Add an unoverridden Release property test that binds `Version` and `PackageVersion` to the repository release version.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: PackageVersion
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd-2.md` summary: Add an unoverridden Release property test that binds `Version` and `PackageVersion` to the repository release version. evidence: The external `Hexalith.Builds` gitlink change updates `HexalithEventStoreVersion` from 3.74.0 to 3.75.0, while current tests do not assert the default evaluated version and CI package validation overrides it explicitly, allowing a stale catalog value to pass.
status: open

### DW-142: Correct the nonexistent baseline SHA recorded by the Story 4.8 implementation artifact.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: _bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md:2
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29720431798-fix-ci-cd.md` summary: Correct the nonexistent baseline SHA recorded by the Story 4.8 implementation artifact. evidence: `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md:2` records `afcc167ef277...`, while the valid baseline is `afcc167e0c539b09ecad978a58da2f756123f34e`; this originated in commit `73140382` and is unrelated to the CI gitlink repair.
status: open

### DW-143: `idempotency-conflict` and `idempotency-key-expired` error-catalog entries in `ErrorReferenceEndpoints.ErrorModels` omit `detail`/`reasonCode` example fields that their real exception handlers (`IdempotencyConflictExceptionHandler`, `IdempotencyKeyExpiredExceptionHandler`) actually set.

origin: migrated from legacy ledger ("Deferred from: idempotency result-payload gating CI fix (2026-07-20)"), 2026-08-30
location: ErrorReferenceEndpoints.ErrorModels
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md` summary: `idempotency-conflict` and `idempotency-key-expired` error-catalog entries in `ErrorReferenceEndpoints.ErrorModels` omit `detail`/`reasonCode` example fields that their real exception handlers (`IdempotencyConflictExceptionHandler`, `IdempotencyKeyExpiredExceptionHandler`) actually set. evidence: Adversarial review of the CI fix -- pre-existing gap unrelated to today's regression; the new `idempotency-admission-failure` entry added by this fix includes both fields (matching its handler), highlighting the sibling entries' inconsistency.
status: open

### DW-144: No test verifies `ErrorReferenceEndpoints.ErrorModels` example content (status code, fields) against what each real `IExceptionHandler` actually emits at runtime -- only slug presence/absence is asserted.

origin: migrated from legacy ledger ("Deferred from: idempotency result-payload gating CI fix (2026-07-20)"), 2026-08-30
location: ErrorReferenceEndpoints.ErrorModels
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md` summary: No test verifies `ErrorReferenceEndpoints.ErrorModels` example content (status code, fields) against what each real `IExceptionHandler` actually emits at runtime -- only slug presence/absence is asserted. evidence: Adversarial review of the CI fix -- `AllProblemTypeUris_HaveCorrespondingErrorModel` / `AllErrorModels_HaveCorrespondingProblemTypeUri` would not have caught this fix's own status-code simplification (503 documented as primary while `idempotency_outcome_unknown` returns 409), a class of drift the catalog exists to prevent.
status: open

### DW-145: `SubmitCommandResult` has no field distinguishing why `ResultPayload` is null (still in-flight, domain-rejected, or a durable non-retryable `PublishFailed`) -- all three look identical to callers from the response body alone.

origin: migrated from legacy ledger ("Deferred from: idempotency result-payload gating CI fix (2026-07-20)"), 2026-08-30
location: SubmitCommandResult
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md` summary: `SubmitCommandResult` has no field distinguishing why `ResultPayload` is null (still in-flight, domain-rejected, or a durable non-retryable `PublishFailed`) -- all three look identical to callers from the response body alone. evidence: Adversarial review of the CI fix -- pre-existing API design gap in the exact code path this fix restores gating for; not caused by this change.
status: open

### DW-146: `docs/reference/command-api.md` § "Stable Idempotency Outcomes" and `ErrorReferenceEndpoints.ErrorModels` are two independently maintained sources of truth for the same idempotency-admission failure taxonomy, with no cross-link between them.

origin: migrated from legacy ledger ("Deferred from: idempotency result-payload gating CI fix (2026-07-20)"), 2026-08-30
location: docs/reference/command-api.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29745475099-idempotency-result-payload-gating.md` summary: `docs/reference/command-api.md` § "Stable Idempotency Outcomes" and `ErrorReferenceEndpoints.ErrorModels` are two independently maintained sources of truth for the same idempotency-admission failure taxonomy, with no cross-link between them. evidence: Adversarial review of the CI fix -- introduced when today's `19465ef8` commit added `ProblemTypeUris.IdempotencyAdmissionFailure` and the docs table without updating the error catalog; this fix closes the catalog gap but does not unify the two sources.
status: open

### DW-147: No unit test asserts that AggregateActor's several `ResultPayloadWithheld` formulas (`CreatePublishFailedResult` and the terminal-completion/concurrency-conflict paths) produce the correct value for each terminal branch.

origin: migrated from legacy ledger ("Deferred from: code review of spec-gh-29740868410-fix-ci-cd.md (2026-07-20)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29740868410-fix-ci-cd.md` summary: No unit test asserts that AggregateActor's several `ResultPayloadWithheld` formulas (`CreatePublishFailedResult` and the terminal-completion/concurrency-conflict paths) produce the correct value for each terminal branch. evidence: PR #319 (`6945714b`) made `CommandProcessingResult.ResultPayloadWithheld` the sole authority for whether `SubmitCommandHandler` returns a command's result payload, but only the consumer side (`SubmitCommandHandlerResultPayloadTests`) has coverage; the producer side in `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (around lines 2018, 2264, 2297, 2358) has no test proving its withheld formula is correct per branch, so a regression there would go undetected by any current suite.
status: open

### DW-148: `SubmitCommandHandler.Log.ResultPayloadDropped`'s message text ("...because final command status was not Completed...") is stale under the flag-driven withholding logic and can be logged even when the reported `FinalStatus` is `Completed`.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-gh-29740868410-fix-ci-cd.md (2026-07-20)"), 2026-08-30
archived: 2026-09-18

### DW-149: [MEDIUM] Fail-closed publisher/validator/authority/smoke suite is not a PR/required check -- `Tools/test-publish-containers.ps1` runs only in Hexalith.Builds `build-release.yml` (push-to-main at reviewed SHA `9ec0a032`; `workflow_dispatch`-only, i.e. worse, at current HEAD); no `pull_request`-triggered Builds workflow runs it.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
archived: 2026-09-18

### DW-150: [MEDIUM] Unbounded registry response read in `RegistryClient._get` (`oci_registry_validator.py:420 response.read()`) has no size cap, unlike the 256/128 KiB caps in the authority-URL fetch -- memory-exhaustion vector from a hostile/malfunctioning registry response or config blob.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: oci_registry_validator.py:420
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [MEDIUM] Unbounded registry response read in `RegistryClient._get` (`oci_registry_validator.py:420 response.read()`) has no size cap, unlike the 256/128 KiB caps in the authority-URL fetch -- memory-exhaustion vector from a hostile/malfunctioning registry response or config blob. evidence: Story 3.12 code review (blind-hunter + edge-case-hunter). Verified still present at live HEAD (submodule `dfb2f3fd`). Defense-in-depth: the Zot registry is authenticated, but the asymmetry with the authority fetch shows the cap was considered and omitted here. Owned by the Hexalith.Builds maintainer.
status: open

### DW-151: [MEDIUM] No negative test proves a failing OCI validator or smoke aborts the publish -- gating relies entirely on `set -euo pipefail` in `publish-containers.sh` (present and functional), but every test runs the script with passing fake validate/smoke executables.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: publish-containers.sh
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [MEDIUM] No negative test proves a failing OCI validator or smoke aborts the publish -- gating relies entirely on `set -euo pipefail` in `publish-containers.sh` (present and functional), but every test runs the script with passing fake validate/smoke executables. evidence: Story 3.12 code review (verification-gap layer; edge-case-hunter confirmed pipefail is present). A regression changing `"$validator" ...` to `... || true`, capturing status in `$(...)`, or backgrounding it would publish a single-platform/digest-mismatched/dead-on-arm64 image with exit 0, and no current test observes the lost gating. Owned by the Hexalith.Builds maintainer.
status: open

### DW-152: [MEDIUM] Validator->smoke evidence schema contract is untested -- `oci_registry_validator.write_evidence` (producer) and `smoke_container_platforms._load_children` (consumer) are each asserted only against their own hand-written `oci-validation.json` fixtures.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: oci-validation.json
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [MEDIUM] Validator->smoke evidence schema contract is untested -- `oci_registry_validator.write_evidence` (producer) and `smoke_container_platforms._load_children` (consumer) are each asserted only against their own hand-written `oci-validation.json` fixtures. evidence: Story 3.12 code review (verification-gap layer). Renaming a child key (e.g. `digest`->`child_digest`) or changing the `platforms` shape on either side leaves both suites green while the real release breaks at smoke -- or silently skips a platform if the loader is simultaneously loosened. Owned by the Hexalith.Builds maintainer.
status: open

### DW-153: [LOW] The Builds-identity gate is behaviorally tested only for the SHA-mismatch branch; the repository-identity, authority-URL, and owner-allowlist branches in `domain-release.yml` are only substring-asserted, not provoked with negative env permutations.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
archived: 2026-09-18

### DW-154: [LOW] Redirect `Location` with an invalid or out-of-range port (e.g. `:99999`, `:abc`) makes `parsed.port` raise `ValueError` inside the redirect handler, which is not caught (only `URLError`/`TimeoutError` are) -- validator aborts with a raw traceback instead of a clean `unresolved-*` failure.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: oci_registry_validator.py:41-71
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [LOW] Redirect `Location` with an invalid or out-of-range port (e.g. `:99999`, `:abc`) makes `parsed.port` raise `ValueError` inside the redirect handler, which is not caught (only `URLError`/`TimeoutError` are) -- validator aborts with a raw traceback instead of a clean `unresolved-*` failure. evidence: Story 3.12 code review (edge-case-hunter) [`oci_registry_validator.py:41-71`]. Still fail-closed (aborts), but not the support-safe deterministic reason code the design advertises. Owned by the Hexalith.Builds maintainer.
status: open

### DW-155: [LOW] Several reachable fail-closed reason codes lack negative fixtures: `wrong-schema-version`, `wrong-child-schema-version`, `child-media-type-mismatch`, `unsupported-child/config-media-type`, `malformed-config-descriptor`, `config-digest-mismatch`, and the immutable-side `index-content-type-mismatch` / `immutable_body != tag_body` branches.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: unsupported-child/config-media-type
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [LOW] Several reachable fail-closed reason codes lack negative fixtures: `wrong-schema-version`, `wrong-child-schema-version`, `child-media-type-mismatch`, `unsupported-child/config-media-type`, `malformed-config-descriptor`, `config-digest-mismatch`, and the immutable-side `index-content-type-mismatch` / `immutable_body != tag_body` branches. evidence: Story 3.12 code review (verification-gap layer). Defense-in-depth for a supply-chain path; the headline reason codes are covered, these branches are not independently provoked. Owned by the Hexalith.Builds maintainer.
status: open

### DW-156: [LOW] Non-domain exceptions (OSError/TypeError from evidence/log file I/O, `path.read_bytes()`) can escape the `main()` handlers that catch only `ValidationError`/`AuthorityError`/`SmokeFailure`, emitting raw tracebacks; and support-safe log redaction (`_support_safe`) misses JSON-shaped secrets (`"password": "..."`) with 30-day evidence-artifact retention.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: path.read_bytes
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [LOW] Non-domain exceptions (OSError/TypeError from evidence/log file I/O, `path.read_bytes()`) can escape the `main()` handlers that catch only `ValidationError`/`AuthorityError`/`SmokeFailure`, emitting raw tracebacks; and support-safe log redaction (`_support_safe`) misses JSON-shaped secrets (`"password": "..."`) with 30-day evidence-artifact retention. evidence: Story 3.12 code review (blind-hunter). Both still fail-closed (exit != 0) and low-exposure today (smoke container carries only the non-secret JWT key), but neither matches the "deterministic support-safe" contract. Owned by the Hexalith.Builds maintainer.
status: open

### DW-157: Make `InformationLevelOnly_TracingChainStillComplete` exercise an actor logger that actually disables Debug logging instead of capturing every level and filtering the resulting list afterward.

origin: migrated from legacy ledger ("Deferred from: Story 2.10 Tier 1 logging regression unblock (2026-07-21)"), 2026-08-30
location: InformationLevelOnly_TracingChainStillComplete
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-10-unblock-server-logging-regressions.md` summary: Make `InformationLevelOnly_TracingChainStillComplete` exercise an actor logger that actually disables Debug logging instead of capturing every level and filtering the resulting list afterward. evidence: Review confirmed the pre-existing test's `TestLogger<T>.IsEnabled` always returns true, so it cannot detect incorrect runtime gating through `IsEnabled(Debug)` even though its post-capture assertions exclude Debug entries; this limitation predates and is not caused by the pooled-state capture correction.
status: open

### DW-158: Validate `DomainServiceOptions.MaxEventsPerResult` and `MaxEventSizeBytes` bounds during startup instead of allowing zero or negative limits to reject otherwise valid responses at invocation time.

origin: migrated from legacy ledger ("Deferred from: code review of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-22, runtime/unit chunk)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:107
reason: source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` summary: Validate `DomainServiceOptions.MaxEventsPerResult` and `MaxEventSizeBytes` bounds during startup instead of allowing zero or negative limits to reject otherwise valid responses at invocation time. evidence: The fields and response-limit behavior predate this review range. The new startup validation at `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:107` covers only `InvocationTimeoutSeconds`; adding the two existing bounds is real hardening but is not caused by the Story 1.20 corrective change.
status: open

### DW-159: [MEDIUM] `InboundBearerForwardingHandler` appends `Authorization` with a bare `TryAddWithoutValidation` and no preceding `Headers.Remove(...)` — the exact append-not-replace anti-pattern AD-18 exists to eliminate, sitting in the same outbound handler chain as the platform handler that deliberately does Remove-then-add. It also coerces the multi-valued `Request.Headers.Authorization` (`StringValues`) to `string?`, so a caller sending two `Authorization` headers is forwarded as one comma-joined value.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/InboundBearerForwardingHandler.cs:14
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [MEDIUM] `InboundBearerForwardingHandler` appends `Authorization` with a bare `TryAddWithoutValidation` and no preceding `Headers.Remove(...)` — the exact append-not-replace anti-pattern AD-18 exists to eliminate, sitting in the same outbound handler chain as the platform handler that deliberately does Remove-then-add. It also coerces the multi-valued `Request.Headers.Authorization` (`StringValues`) to `string?`, so a caller sending two `Authorization` headers is forwarded as one comma-joined value. evidence: Story 2.5 code review (blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/InboundBearerForwardingHandler.cs:14`], contrast `src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs:12-17`. The file is untouched by patch commit `846f988a` and the Api host sets no default `Authorization` on its gateway client today, so this is latent, not live; it becomes live the moment anything configures a default `Authorization` (as the sibling UI host already does via `AddFrontComposerGatewayAuthorization()`). The only test for this handler covers the single-value case. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-160: [LOW] `DAPR_API_TOKEN` is read raw with no trim or normalization, so a mounted secret carrying a trailing newline, or a whitespace-only value, is forwarded verbatim to the sidecar; the platform handler's `apiToken is { Length: > 0 }` test passes for `" "`.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:69
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [LOW] `DAPR_API_TOKEN` is read raw with no trim or normalization, so a mounted secret carrying a trailing newline, or a whitespace-only value, is forwarded verbatim to the sidecar; the platform handler's `apiToken is { Length: > 0 }` test passes for `" "`. evidence: Story 2.5 code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:69`]. Pre-existing: identical to the deleted `DaprAppIdHandler`'s `!string.IsNullOrEmpty` behavior, so not caused by this patch. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-161: [LOW] The DAPR API token is captured by value at registration time, so a token rotated at runtime (secret remount, config reload) stays stale until process restart; an `IOptionsMonitor`-based handler factory would pick up rotations.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:70
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [LOW] The DAPR API token is captured by value at registration time, so a token rotated at runtime (secret remount, config reload) stays stale until process restart; an `IOptionsMonitor`-based handler factory would pick up rotations. evidence: Story 2.5 code review (edge-case-hunter) [`src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:70`]. Pre-existing platform design, unchanged by this patch and identical to the deleted host-local handler. Owned by Hexalith.EventStore.
status: open
decision: 2026-09-06 Support live rotation — Resolve the token per request through validated IOptionsMonitor state and test valid, blank, and changed token snapshots.
decision: 2026-09-06 Support live rotation — Resolve the token per request through validated IOptionsMonitor state and test valid, blank, and changed token snapshots.

### DW-162: [LOW] `DaprServiceInvocationExtension_ReplacesUntrustedRoutingHeaders` builds a synthetic named client `"dapr"` that no Tenants production code registers, so it exercises zero Tenants code and only re-tests EventStore platform behavior; the rename traded the Tenants suite's only test of Tenants-owned outbound routing for a third copy of a platform test.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs:134
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [LOW] `DaprServiceInvocationExtension_ReplacesUntrustedRoutingHeaders` builds a synthetic named client `"dapr"` that no Tenants production code registers, so it exercises zero Tenants code and only re-tests EventStore platform behavior; the rename traded the Tenants suite's only test of Tenants-owned outbound routing for a third copy of a platform test. evidence: Story 2.5 code review (blind-hunter + verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs:134`]. Near-duplicate of `tests/Hexalith.EventStore.Client.Tests/Registration/DaprServiceInvocationRegistrationTests.cs` and `.../Handlers/DaprServiceInvocationHandlerTests.cs`, whose upstream versions are stronger (they inject the conflicting value through an outer handler — the actual AD-18 threat — rather than host-owned `DefaultRequestHeaders`). Test-quality only; the real-chain assertions survive in the sibling test at the same file. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-163: [MEDIUM] AD-18 is opt-in and fail-open at the platform seam — `AddEventStoreGatewayClient` registers no routing-header handler, so a host that omits the separate `AddEventStoreDaprServiceInvocation` call silently gets no `dapr-app-id`/`dapr-api-token` ownership with no compile-time error, no startup validation and no runtime diagnostic. Same fail-open shape as the `ApiScope` trap already on record. Harden the platform so the seam fails closed (or emits a startup diagnostic) rather than relying on per-host convention plus source-text guard tests.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:43-48
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [MEDIUM] AD-18 is opt-in and fail-open at the platform seam — `AddEventStoreGatewayClient` registers no routing-header handler, so a host that omits the separate `AddEventStoreDaprServiceInvocation` call silently gets no `dapr-app-id`/`dapr-api-token` ownership with no compile-time error, no startup validation and no runtime diagnostic. Same fail-open shape as the `ApiScope` trap already on record. Harden the platform so the seam fails closed (or emits a startup diagnostic) rather than relying on per-host convention plus source-text guard tests. evidence: Story 2.5 code review (blind-hunter + acceptance-auditor) [`src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:43-48` registers only `ICommandStatusLocationBuilder` and the typed client; the handler is added exclusively at line 63]. Owner decision 2026-07-26: out of Story 2.5 scope (that story reviews the Tenants host boundary, not platform design) — carry as a dedicated Hexalith.EventStore platform hardening story. Note `project-context.md:46` currently misstates this wiring as "wired by `AddEventStoreGatewayClient`"; that text is corrected under Story 2.5.
status: open

### DW-164: [LOW] `SampleApiLaunchSettingsTests.ExtractBlock` matches an LF-only marker (`";\n\nif (security is not null)"`) against `src/Hexalith.EventStore.AppHost/Program.cs`, so the test fails on any working tree where that file is checked out or rewritten with CRLF line endings. Make the marker line-ending agnostic (normalize the text, or match on a CRLF-tolerant pattern) the way the sibling `TenantsApiLaunchSettingsTests` does with its `#endif` marker.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
archived: 2026-09-18

### DW-165: [MEDIUM] A `304` carrying `Lifecycle: Degraded` renders a normal `Ready` surface, while the identical evidence on a `200` renders `Degraded` — the same authoritative "projection degraded" claim produces two different user-visible surfaces depending only on cache validation.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1169
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] A `304` carrying `Lifecycle: Degraded` renders a normal `Ready` surface, while the identical evidence on a `200` renders `Degraded` — the same authoritative "projection degraded" claim produces two different user-visible surfaces depending only on cache validation. evidence: Story 2.6 code review (blind-hunter + edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1169`]. On 200 the five call sites return `*.Degraded(...)` on `IsDegraded == true`; on 304 `ResolveNotModifiedFreshness` routes into `ResolveFreshness`, which returns `Unknown`, and every `Resolve*KindForFreshness` mapping collapses non-`Stale` to `Ready`/`Empty`. Pre-existing: the pre-diff predicate `metadata?.IsDegraded == true || metadata?.IsStale is not null` also routed a degraded 304 into `ResolveFreshness` for the same `Unknown` result, so the diff does not cause it. Reachable through the shipped client, which maps a degraded 304 to `IsDegraded = true` at `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:154-166`. Newly relevant because the diff added a theory that pins `Degraded -> TenantDetailSurfaceKind.Ready` as expected behaviour. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-166: [LOW] `IsTenantManagementApiRoute` hardcodes the `api/tenants`, `api/users`, and `api/global-administrators` prefixes with no shared constant or link to the attribute that declares them, so the guard goes blind if the REST base changes.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26)"), 2026-08-30
location: api/tenants
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [LOW] `IsTenantManagementApiRoute` hardcodes the `api/tenants`, `api/users`, and `api/global-administrators` prefixes with no shared constant or link to the attribute that declares them, so the guard goes blind if the REST base changes. evidence: Story 2.6 code review (blind-hunter + edge-case-hunter + verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:559`]. Verified correct for today's surface: all 17 `[RestRoute]` templates in `src/Hexalith.Tenants.Contracts` hang off `[assembly: RestApi("api/tenants", ...)]` at `src/Hexalith.Tenants.Api/RestApiAssemblyInfo.cs:5`, plus absolute `~/api/users/{userId}/tenants` and `~/api/global-administrators`. The prefix is an attribute argument, so a change there silently defeats the matcher. Low impact because the preceding `ControllerActionDescriptor` check already catches every generated controller — the route matcher only adds value against hand-written minimal APIs, and shapes like `api/v1/tenants` or `api/tenant-configuration/...` escape it regardless. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-167: [LOW] Deleting the UI source-marker scan leaves marker enforcement for the Tenants UI host running only in the EventStore repository's test suite, so the Tenants repo's own CI can no longer enforce it standalone.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [LOW] Deleting the UI source-marker scan leaves marker enforcement for the Tenants UI host running only in the EventStore repository's test suite, so the Tenants repo's own CI can no longer enforce it standalone. evidence: Story 2.6 code review (blind-hunter + edge-case-hunter + verification-gap + acceptance-auditor). The diff removed the `forbiddenMarkers` array from `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`. Mitigation verified: `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:119-135` retains every deleted marker plus `AddMvc(`/`AddMvcCore(`, and scans `references/Hexalith.Tenants/src/Hexalith.Tenants.UI` at line 621. Residual gap: within the Tenants repo alone, `builder.Services.AddControllers()` plus `app.MapControllers()` with no controller type declared passes all three replacement tests (no controller types to reflect over, no `ControllerActionDescriptor` endpoints to enumerate). Split ownership between Hexalith.EventStore and the Hexalith.Tenants maintainer.
status: open

### DW-168: [LOW] Two guardrail tests were promoted into behaviours that no longer fit the Tier-1 unit tier — one boots a full Blazor Server host via `WebApplicationFactory`, the other spawns two `dotnet msbuild` child processes — inside a project the Tenants CI runs in its unit lane.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26)"), 2026-08-30
location: references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [LOW] Two guardrail tests were promoted into behaviours that no longer fit the Tier-1 unit tier — one boots a full Blazor Server host via `WebApplicationFactory`, the other spawns two `dotnet msbuild` child processes — inside a project the Tenants CI runs in its unit lane. evidence: Story 2.6 code review (verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224` and `:500`]. `tests/Hexalith.Tenants.UI.Tests` is listed in the Tenants `.github/workflows/ci.yml` Tier-1 unit list, while every other `WebApplicationFactory` usage in that repository lives in `tests/Hexalith.Tenants.IntegrationTests`. The unit tier now depends on an installed SDK on `PATH`, a valid restore, and the exact five-levels-up `ProjectRoot()` layout at test runtime. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-169: [RESOLVED 2026-07-27] Platform prerequisite — preserve `ReadModelFreshnessState` as the independent threshold/age view and introduce the existing `ProjectionLifecycleState` alongside it in consumer UI snapshots/rows, so `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` receive distinct canonical treatment without corrupting freshness semantics.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, D3 owner decision)"), 2026-08-30
archived: 2026-09-18

### DW-170: [MEDIUM] Active CI operator documentation still names the superseded Hexalith.Builds release SHA and is not guarded against drifting from the immutable release pin.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, D3 owner decision)"), 2026-08-30
archived: 2026-09-18

### DW-171: [HIGH] `EnrichRowsAsync` publishes member/owner counts as `TenantCountValue.Known(...)` from a detail payload that `LoadTenantDetailAsync` returns raw — no tenant-identity check, no `Members` null guard, no `IsDegraded` check. A detail projection returning the wrong tenant attributes another tenant's member and owner counts to the row; a payload omitting `members` throws `NullReferenceException` that escapes the gateway into the Blazor render.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
archived: 2026-09-18

### DW-172: [HIGH] A single row's detail-enrichment failure with any status outside `{403,404,503}` unwinds past the already-successful list fetch and maps the whole page to `TenantListSurfaceKind.Error`. The most reachable trigger is the client's own `EventStoreGatewayException(200, "Query response did not contain a payload.")`.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
archived: 2026-09-18

### DW-173: [MEDIUM] Non-`EventStoreGatewayException` failures escape four of the six public read methods. `GetTenantAsync` and `GetConfigurationProjectionProofAsync` have a generic `catch (Exception)`; the user-tenants, global-admins, audit and list paths do not, so an unhandled exception crashes the interactive Blazor circuit instead of degrading.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
archived: 2026-09-18

### DW-174: [MEDIUM] Three further 304 evidence asymmetries beyond the `Degraded` one already on record: a 304 carrying no lifecycle evidence re-affirms the previous claim (so `Current` survives indefinitely where the identical 200 yields `Unknown`); per-row `Freshness` is never rewritten on any 304 branch, so row badges contradict the surface banner; and a `Degraded` surface cannot be cleared by refreshing because the server ETag is content-derived and does not change when only lifecycle recovers.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
archived: 2026-09-18

### DW-175: [MEDIUM] `ResolveTenantListKindForFreshness` lacks the surface-kind whitelist its three siblings open with, so an `Error`, `Unauthorized` or `Loading` previous falls through to `previous.Rows.Count == 0 ? Empty : Ready` — the surface would assert "you have no tenants" on top of a failed or denied read.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1267-1283
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] `ResolveTenantListKindForFreshness` lacks the surface-kind whitelist its three siblings open with, so an `Error`, `Unauthorized` or `Loading` previous falls through to `previous.Rows.Count == 0 ? Empty : Ready` — the surface would assert "you have no tenants" on top of a failed or denied read. evidence: Story 2.6 second-pass code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1267-1283` versus `:1185`, `:1207`, `:1229`]. Currently unreachable because `TenantListSnapshot.Error()` and `.Unauthorized()` both set `ETag = null` (`State/TenantList/TenantListSnapshot.cs:73-94`) so no 304 can follow, but the resolver itself carries no guard and the gateway is the reusable seam. Latent rather than live.
status: open

### DW-176: [MEDIUM] `ReadModelFreshnessState.Aging` is absent from the mutation-blocking set. `TenantLifecycleAvailabilityInput.Evaluate` blocks on `Freshness is Stale or Unknown`, so once the deferred persisted-projection-age work makes `Aging` reachable, aging read-model evidence will silently *permit* tenant enable/disable mutations — the opposite of the AD-15 posture applied to `Unknown` and `Stale`.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs:42
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] `ReadModelFreshnessState.Aging` is absent from the mutation-blocking set. `TenantLifecycleAvailabilityInput.Evaluate` blocks on `Freshness is Stale or Unknown`, so once the deferred persisted-projection-age work makes `Aging` reachable, aging read-model evidence will silently *permit* tenant enable/disable mutations — the opposite of the AD-15 posture applied to `Unknown` and `Stale`. evidence: Story 2.6 second-pass code review (blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs:42`]. `ResolveFreshness` cannot currently emit `Aging` (`ProjectionLifecycleState` has no `Aging` member and the switch maps everything unmatched to `Unknown`), yet `Aging` branches already exist in `Components/Shared/TruthStateBadge.razor:37,45` with tests at `Components/TruthStateBadgeTests.cs:21` and `State/TenantLifecycleAvailabilityTests.cs:62`. Directly coupled to the existing D6 read-model freshness handoff and to the D3 platform deferral recorded above — close this together with them. Split ownership between Hexalith.EventStore (freshness model) and the Hexalith.Tenants maintainer (gate).
status: open

### DW-177: [LOW] A search term containing any control character is silently nulled by `CanonicalizeListRequest`, so the gateway returns an unfiltered full list with `Notice = None` while the user's search box still shows their query — the grid renders every tenant on the page as if it matched.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:550-552
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [LOW] A search term containing any control character is silently nulled by `CanonicalizeListRequest`, so the gateway returns an unfiltered full list with `Notice = None` while the user's search box still shows their query — the grid renders every tenant on the page as if it matched. evidence: Story 2.6 second-pass code review (edge-case-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:550-552`, taken with the plain cursor branch at `:499-501`]. `TenantsWorkspace.ApplyVisibleRows` (`TenantsWorkspace.razor:611-640`) applies only status and sort client-side, never the search term. Every other search failure sets `TenantListReason.SearchUnavailable` via `FallBackFromSearchAsync:769-773`, a notice the UI does render (`TenantsWorkspace.razor:376`) — this path should do the same.
status: open

### DW-178: [LOW] Test-quality cluster in `TenantQueryGatewayTests.cs`, all pre-existing: two tests pass only because the stub's `Queue<object>` underflows (`InvalidOperationException` is absorbed by a catch-all, and is separately on `IsSearchAvailabilityFailure`'s swallow list, so any under-enqueued search test silently asserts the fallback path); the fixture string `"index-only content that must never render"` is asserted nowhere; `TenantDetailSnapshot.ErrorMessage` has zero consumers in the UI so its sanitization assertions run against a tenant id; the French resource values `"Administrateurs globaux charges"` and `"Donnees d'administrateurs globaux perimees"` are missing accents and the test pins the wrong expected value; and two composition tests couple CI to documentation prose and to one exact workflow substring.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: TenantQueryGatewayTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [LOW] Test-quality cluster in `TenantQueryGatewayTests.cs`, all pre-existing: two tests pass only because the stub's `Queue<object>` underflows (`InvalidOperationException` is absorbed by a catch-all, and is separately on `IsSearchAvailabilityFailure`'s swallow list, so any under-enqueued search test silently asserts the fallback path); the fixture string `"index-only content that must never render"` is asserted nowhere; `TenantDetailSnapshot.ErrorMessage` has zero consumers in the UI so its sanitization assertions run against a tenant id; the French resource values `"Administrateurs globaux charges"` and `"Donnees d'administrateurs globaux perimees"` are missing accents and the test pins the wrong expected value; and two composition tests couple CI to documentation prose and to one exact workflow substring. evidence: Story 2.6 second-pass code review (blind-hunter, corroborated by verification-gap) [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:501`, `:2074`, `:2092`, `:2223`; `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Resources/TenantsResources.fr.resx:2617` and `:2623`; `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:357` and `:472`]. The accent defect is locked in: fixing the resx now breaks the test.
status: open

### DW-179: [HIGH] Mutation-gate fail-open in the ratified 2.11 consumer logic — the tenant correction surface enables a mutation that `ProjectionLifecyclePolicy.CanMutate` denies. `CanMutate` requires `provenance == ProjectionBacked && lifecycle == Current`, but `ResolveFreshness` also returns `Current` on the legacy fall-through (`Lifecycle == Unknown` with `IsStale == false`). `TenantCorrectionStartIntent` gates only on `Freshness is Current`, so a producer emitting no lifecycle header but legacy `IsStale: false` unlocks a correction the platform policy forbids.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass — routed to Story 2.11)"), 2026-08-30
archived: 2026-09-18

### DW-180: [MEDIUM] Local build environment hazard, not a code defect — package-mode builds on this workstation block indefinitely on an interactive NuGet credential prompt, and orphaned MSBuild worker nodes accumulate until new builds wedge on reuse. Any unattended run that reaches package mode (bmad-loop dev session, local CI, release rehearsal) hangs silently and presents as "still running" rather than failing.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass — routed to Story 2.11)"), 2026-08-30
location: Hexalith.Builds.slnx
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] Local build environment hazard, not a code defect — package-mode builds on this workstation block indefinitely on an interactive NuGet credential prompt, and orphaned MSBuild worker nodes accumulate until new builds wedge on reuse. Any unattended run that reaches package mode (bmad-loop dev session, local CI, release rehearsal) hangs silently and presents as "still running" rather than failing. evidence: Observed directly during the Story 2.6 second-pass review, 2026-07-26. A `dotnet build -c Release` of `Hexalith.Tenants.UI.Tests` accumulated 2h36m elapsed against 7 seconds of CPU, parked in `futex_wait_queue` with `NuGet.Credentials.dll` open; a separate `dotnet restore Hexalith.Builds.slnx --interactive` sat 5h06m in state `Sl+` (terminal foreground, awaiting input). 119 orphaned `MSBuild.dll --nodemode` processes were alive, load average 37.88; after clearing them the same test project built in 7.03s. Mitigations that worked: Debug source mode with `-p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false`. Note `-p:HexalithCommonsFromSource=false` is required — otherwise source-built `Hexalith.Commons.UniqueIds` 3.82.0 collides with the cached 2.28.2 package and the build fails `CS1704`. Suggested durable fixes: a non-interactive credential default (`NUGET_CREDENTIALPROVIDER_*`) and `-nodeReuse:false` in local build guidance. Owned by Hexalith.EventStore tooling.
status: open

### DW-181: [MEDIUM] Four legacy Fluent v4 / FAST tokens survive in three Tenants UI stylesheets, so the "accent" callout treatment never tracks the active theme. `hexalith-ux-instructions.md` forbids `--accent-*` and `--neutral-foreground-*` outright — they belong to the previous major version and do not resolve under Fluent V5, so every occurrence falls through to its system-colour fallback and renders `LinkText` / `GrayText` in every theme with the intended accent silently absent. The UX instruction's own escape hatch requires these files to be tracked as an explicit, allowlisted migration backlog rather than silently exempted; this entry is that tracking. Migrate each to a Fluent 2 design token (or a Fluent primitive) and keep the `@media (forced-colors: active)` fallbacks.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: focused UX acceptance review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, Sally)"), 2026-08-30
archived: 2026-09-18

### DW-182: [HIGH] Member, configuration, metadata, tenant-lifecycle, and global-administrator projection gates still accept `Freshness == Current` without requiring projection-confirmed lifecycle/provenance, so legacy-current/unknown-lifecycle responses can arm mutations outside the correction-start surface.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 2-11-query-provenance-consumption-in-generated-rest-and-tenants (2026-07-27)"), 2026-08-30
archived: 2026-09-18

### DW-183: [MEDIUM] No blocking CI job restores, builds, or tests the Debug/source dependency lane, so the source half of Story 2.12's new Gateway conditional is never evaluated by an automated gate. A broken or renamed EventStore source path in the conditional `ProjectReference` surfaces only when a developer runs source mode locally.

origin: migrated from legacy ledger ("Deferred from: code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: references/Hexalith.Tenants/Directory.Build.props:53-56
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [MEDIUM] No blocking CI job restores, builds, or tests the Debug/source dependency lane, so the source half of Story 2.12's new Gateway conditional is never evaluated by an automated gate. A broken or renamed EventStore source path in the conditional `ProjectReference` surfaces only when a developer runs source mode locally. evidence: Story 2.12 code review (verification-gap), verified 2026-07-28. `references/Hexalith.Tenants/Directory.Build.props:53-56` defaults `UseHexalithProjectReferences` to `false` in every unset case; `references/Hexalith.Builds/.github/workflows/domain-ci.yml:163/167/187/222` runs restore, build, and test without ever passing `-p:UseHexalithProjectReferences=true`; the only Tenants hit for that property outside tests is `scripts/publish-partial-release.sh:42`, which sets it `false`. The two tests that do evaluate both modes (`TenantsUiCompositionTests.cs:420`, `TenantsApiStructuralTests.cs:40`) target the UI and Api projects, neither of which has an edge to `src/Hexalith.Tenants`, and the latter runs in the aspire tier with `continue-on-error` defaulting to `true`. The new `PackageGovernanceTests` host rule does run blocking, but it is mode-independent XML text, so "green in both modes" carries no mode-specific information. Suggested durable fix: a `[Theory(true/false)]` over `src/Hexalith.Tenants/Hexalith.Tenants.csproj` asserting `type: project` in source mode and `type: package` in package mode, reusing the existing `ReadResolvedDependencyValuesAsync` helper. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-184: [MEDIUM] Nothing durably detects EventStore gitlink drift away from a validated commit, nor a wrong-but-resolvable `HexalithEventStoreVersion`. The amended AC2/AC3 identity gate exists only as hand-run scripts in a SHA-named evidence directory, invoked by no workflow and no test. The approved sprint change proposal placed the Tenants CI reachability check out of scope as a candidate follow-up; this entry is that tracking.

origin: migrated from legacy ledger ("Deferred from: code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: evidence/story-2-12/578770679b9d…/
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [MEDIUM] Nothing durably detects EventStore gitlink drift away from a validated commit, nor a wrong-but-resolvable `HexalithEventStoreVersion`. The amended AC2/AC3 identity gate exists only as hand-run scripts in a SHA-named evidence directory, invoked by no workflow and no test. The approved sprint change proposal placed the Tenants CI reachability check out of scope as a candidate follow-up; this entry is that tracking. evidence: Story 2.12 code review (verification-gap), verified 2026-07-28. A repo-wide grep for `ac2-guard`, `analyze-assets`, and `setup-lane` outside `evidence/story-2-12/578770679b9d…/` returns nothing. Tenants CI's only submodule handling is `Github/initialize-build/action.yml` → `git -c submodule.recurse=false submodule update --init`, which fails only when a gitlink is *unfetchable* — a commit on an unmerged feature branch initializes, restores, and passes every job while violating AC2's reachability requirement. On the producer side, `references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1:70-91` asserts only catalog membership and non-blankness and never queries a feed (`validate-central-package-versions.ps1` has zero `nuget.org` occurrences), so an unpublished version is caught only by breaking a downstream repository — which already happened once with `999.1.20-proof.fa2d1c9910f8` — and a published-but-wrong version is caught nowhere. The risk is not hypothetical: the umbrella gitlink left the accepted SHA within a day, inside this story's own final commit (see the corresponding `[Review][Decision]` item in the story). Suggested durable fixes: promote `ac2-guard.sh` into `references/Hexalith.Tenants/scripts/` and call it from a Tenants CI step; extend the Builds catalog test with a flat-container existence check reusing the pattern at `Github/publish-containers/publication_preflight.py:402`. Owned by the Hexalith.Tenants and Hexalith.Builds maintainers.
status: open

### DW-185: [MEDIUM] The retained `578770679b9d…` lane logs were produced by the pre-fix `ac2-guard.sh` and are committed beside the post-fix script, with no marker distinguishing them. The guard's stdout format is unchanged by the 2026-07-28 hardening, so `logs/ac2-guard-{src,pkg}-lane.log` is byte-identical to what the corrected script would print; an auditor cannot tell "assertion 5 ran correctly" from "assertion 5 was a tautology".

origin: migrated from legacy ledger ("Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: ac2-guard.sh
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [MEDIUM] The retained `578770679b9d…` lane logs were produced by the pre-fix `ac2-guard.sh` and are committed beside the post-fix script, with no marker distinguishing them. The guard's stdout format is unchanged by the 2026-07-28 hardening, so `logs/ac2-guard-{src,pkg}-lane.log` is byte-identical to what the corrected script would print; an auditor cannot tell "assertion 5 ran correctly" from "assertion 5 was a tautology". evidence: Story 2.12 delta code review (blind-hunter + verification-gap + acceptance-auditor), verified 2026-07-28. The receipt states the limitation in prose but the artifact pair carries no marker. `logs/pkg-assets.txt` is the honest counter-example — it lacks the new `invocation:` line, which does reveal its provenance. Deferred because the SHA is superseded by `f9e51c66…`, whose logs were produced by the corrected scripts. Suggested durable fix: have the lane scripts print their own sha256 into their output so a log always identifies the instrument that produced it.
status: open

### DW-186: [LOW] `ac3-catalog.txt`'s line-oriented grep cannot fail for `Hexalith.EventStore.RestApi.Generators`, whose `Condition` sits on an XML continuation line, and no durable guard asserts `PackageReference` conditions outside the domain host.

origin: migrated from legacy ledger ("Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: logs/ac3-catalog.txt:81
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [LOW] `ac3-catalog.txt`'s line-oriented grep cannot fail for `Hexalith.EventStore.RestApi.Generators`, whose `Condition` sits on an XML continuation line, and no durable guard asserts `PackageReference` conditions outside the domain host. evidence: Story 2.12 delta code review (blind-hunter), verified 2026-07-28 at `logs/ac3-catalog.txt:81`. The condition genuinely exists in `src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj`, and the AC4 host rule reads effective conditions for `src/Hexalith.Tenants/Hexalith.Tenants.csproj`; but `No_EventStore_project_reference_is_reachable_in_package_mode` checks conditions only on `ProjectReference` items, checking `PackageReference` items solely for local version authority. Suggested durable fix: extend that rule to assert the complementary package condition on every owned project, not only the host.
status: open

### DW-187: [LOW] `setup-lane.sh` hardcodes `REFS=/home/administrator/projects/hexalith` and its new safety allowlist confines destinations to `/home/*/tmp-story-2-12/*`, so the already-deferred "promote `ac2-guard.sh` into Tenants CI" item cannot be executed as written — the lane the guard depends on cannot be created by that CI without editing the script.

origin: migrated from legacy ledger ("Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: setup-lane.sh
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [LOW] `setup-lane.sh` hardcodes `REFS=/home/administrator/projects/hexalith` and its new safety allowlist confines destinations to `/home/*/tmp-story-2-12/*`, so the already-deferred "promote `ac2-guard.sh` into Tenants CI" item cannot be executed as written — the lane the guard depends on cannot be created by that CI without editing the script. evidence: Story 2.12 delta code review (blind-hunter), verified 2026-07-28 at `evidence/story-2-12/578770679b9d…/setup-lane.sh:8,29-32`. This under-specifies the drift-detector follow-up recorded in the 2026-07-28 entry above; scope the two together. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-188: [MEDIUM] Nothing in EventStore invokes `analyze-assets.py`, `ac2-guard.sh`, or `setup-lane.sh`, and not one of their fail-closed branches has ever executed. Every retained run terminates in `ASSETS_OK` / `AC2_GUARD_OK`, so the unknown-mode exit, the missing-expected-version exit, the vacuous-pass guard and both mode-specific zero-edge guards are unverified against a failing input.

origin: migrated from legacy ledger ("Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: analyze-assets.py
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [MEDIUM] Nothing in EventStore invokes `analyze-assets.py`, `ac2-guard.sh`, or `setup-lane.sh`, and not one of their fail-closed branches has ever executed. Every retained run terminates in `ASSETS_OK` / `AC2_GUARD_OK`, so the unknown-mode exit, the missing-expected-version exit, the vacuous-pass guard and both mode-specific zero-edge guards are unverified against a failing input. evidence: Story 2.12 delta code review (verification-gap + edge-case-hunter), verified 2026-07-28. A repo-wide grep for `analyze-assets`, `ac2-guard`, and `setup-lane` outside the `evidence/story-2-12/` directories returns only prose in the story file, the receipts, and this ledger — no workflow, test, or script invokes them. Same root cause as the drift-detector entry above. Suggested durable fix: drive the scripts from xUnit tests over small synthetic `project.assets.json` fixtures and deliberately-broken lanes, asserting exit codes — the pattern `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs` already uses for the AWK verifier.
status: open

### DW-189: [MEDIUM] The strengthened Story 2.12 AC4 guard has four bypasses. (1) `No_EventStore_project_reference_is_reachable_in_package_mode` iterates `GetOwnedProjectFiles`, which enumerates `*.csproj` only, while the sibling rule in the same file uses `GetPackageReferenceGovernanceFiles`, which also appends every `Directory.Build.*` — and `tests/Directory.Build.props` already declares 6 `PackageReference` items, so an ungated EventStore `ProjectReference` placed there gives every test project a live project edge in package mode with both tests still green. (2) The rule tests the effective condition with `string.Contains`, so `!('$(HexalithEventStoreFromSource)' == 'true')` and `'$(HexalithEventStoreFromSource)' == 'true' Or '$(Configuration)' == 'Release'` both satisfy it while remaining live in package mode. (3) `EventStoreReferences` matches on `Include` only, so an `Update=`-form `PackageReference` carrying a version is invisible to `HasLocalVersionAuthority`. (4) The rule accepts `UseHexalithProjectReferences == 'true'` as equivalent source intent, but `Directory.Build.props:60` sets `HexalithEventStoreFromSource` only when that property is true and the EventStore Contracts csproj exists, so a reference gated only on the former is live in a configuration where the complementary `PackageReference` is also live.

origin: migrated from legacy ledger ("Deferred from: delta code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)"), 2026-08-30
location: *.csproj
reason: source_spec: `_bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md` summary: [MEDIUM] The strengthened Story 2.12 AC4 guard has four bypasses. (1) `No_EventStore_project_reference_is_reachable_in_package_mode` iterates `GetOwnedProjectFiles`, which enumerates `*.csproj` only, while the sibling rule in the same file uses `GetPackageReferenceGovernanceFiles`, which also appends every `Directory.Build.*` — and `tests/Directory.Build.props` already declares 6 `PackageReference` items, so an ungated EventStore `ProjectReference` placed there gives every test project a live project edge in package mode with both tests still green. (2) The rule tests the effective condition with `string.Contains`, so `!('$(HexalithEventStoreFromSource)' == 'true')` and `'$(HexalithEventStoreFromSource)' == 'true' Or '$(Configuration)' == 'Release'` both satisfy it while remaining live in package mode. (3) `EventStoreReferences` matches on `Include` only, so an `Update=`-form `PackageReference` carrying a version is invisible to `HasLocalVersionAuthority`. (4) The rule accepts `UseHexalithProjectReferences == 'true'` as equivalent source intent, but `Directory.Build.props:60` sets `HexalithEventStoreFromSource` only when that property is true **and** the EventStore Contracts csproj exists, so a reference gated only on the former is live in a configuration where the complementary `PackageReference` is also live. evidence: Story 2.12 delta code review (blind-hunter + edge-case-hunter + verification-gap), verified 2026-07-28 against `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs:217,226-227,258-261,304-307` and `references/Hexalith.Tenants/Directory.Build.props:59-61`. Deferred by explicit owner decision on 2026-07-28: the fix lives in Hexalith.Tenants, and committing it would move Story 2.12's acceptance off `f9e51c66745557da4f267ab40f32294f2f27fae7` and re-trigger the AC5 maintainer-acceptance cycle for a third time; the guard as shipped is still a large improvement over the four-literal-name version it replaced. Suggested durable fix, smallest first: swap `GetOwnedProjectFiles` for `GetPackageReferenceGovernanceFiles` in the reachability rule, then parse the condition rather than substring-matching it, then extend `EventStoreReferences` to read `Update=` as the pre-existing rules at `:120` and `:1342` already do. Owned by the Hexalith.Tenants maintainer.
status: open

### DW-190: [LOW] A tracked stale `.lscache` under `Server.Tests` still enumerates `Fixtures/DaprTestContainerCollection.cs` and `Fixtures/DaprTestContainerFixture.cs`, committed residue from the very re-tier this story certifies. The AC1 guard cannot see it.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: Fixtures/DaprTestContainerCollection.cs
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [LOW] A tracked stale `.lscache` under `Server.Tests` still enumerates `Fixtures/DaprTestContainerCollection.cs` and `Fixtures/DaprTestContainerFixture.cs`, committed residue from the very re-tier this story certifies. The AC1 guard cannot see it. evidence: Story 3.1 closure code review (acceptance-auditor + blind-hunter), verified 2026-07-28 at `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj.lscache:185-187`. The file is tracked (`git ls-files` confirms) and last touched by `9bafe1af`, pre-split. `ReleasePackageManifestTests.cs:429-446` filters candidates to `.cs`/`.csproj` only, so the guard structurally cannot flag it; the story's AC1 evidence (filename search + `.cs` grep) has the same blind spot. Not compiled, so lane separation is unaffected. 34 `.lscache` files are tracked repo-wide, so removing one is a repo-wide convention decision rather than a story-scoped fix. Suggested durable fix: untrack `*.lscache` via `.gitignore`, or widen the guard's file filter.
status: open

### DW-191: [HIGH, FrontComposer-owned] The release-pin lockstep guard lives in a workflow that does not gate the release it protects. `CiGovernanceTests` carries `[Trait("Category","Governance")]`, which executes only in `quality.yml`; `release.yml` triggers on `workflow_run: workflows: [CI]` and neither needs nor triggers on Quality.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: quality.yml
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [HIGH, FrontComposer-owned] The release-pin lockstep guard lives in a workflow that does not gate the release it protects. `CiGovernanceTests` carries `[Trait("Category","Governance")]`, which executes only in `quality.yml`; `release.yml` triggers on `workflow_run: workflows: [CI]` and neither needs nor triggers on Quality. evidence: Story 3.1 closure code review (verification-gap + edge-case-hunter), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs:438-482`, `.github/workflows/quality.yml:119`, `.github/workflows/release.yml:25-26`, and `ci.yml:32-35` (which explicitly excludes `Shell.Tests`). Demonstrated: at FrontComposer `78705260` the pin was `7708256e` while the gitlink was `79f82acc`; CI run `29804662443` = success, Quality run `29804662064` = failure with Gate 2b red. A release dispatched from that head would have been authorized by green CI while executing Builds actions from a different revision than the build inputs — split-brain. The guard also went red at the commit that introduced it (`48862e9a`, PR #74) and stayed red across two commits on main with nothing blocking either push. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: make the release trigger depend on the Quality lane, or move `CiGovernanceTests` into a project inside `ci.yml`'s `unit-test-projects`.
status: open

### DW-192: [MEDIUM, FrontComposer-owned] Nothing validates that a newly pinned reusable workflow's input contract matches the caller. All three lockstep assertions compare SHAs, never contracts, so a Builds bump that renames or adds a required input stays green until a real publication attempt fails.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: references/Hexalith.FrontComposer/tests/.../CiGovernanceTests.cs:454-489
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM, FrontComposer-owned] Nothing validates that a newly pinned reusable workflow's input contract matches the caller. All three lockstep assertions compare SHAs, never contracts, so a Builds bump that renames or adds a required input stays green until a real publication attempt fails. evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/.../CiGovernanceTests.cs:454-489`. No test opens `references/Hexalith.Builds/.github/workflows/domain-release.yml`, even though `quality.yml:39-40` initializes that submodule so the file is present. This particular bump is contract-safe — `domain-release.yml` is byte-identical between `7708256e` and `79f82acc` — but that is established by nothing in the repository. The guard's own comment claims it ties the pin to "a guaranteed-real, locally-present git commit", yet `git ls-tree HEAD` reads the gitlink without resolving the commit object. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: parse the submodule copy of `domain-release.yml` and diff its declared `inputs`/`secrets` against the caller's `with:`/`secrets:` blocks.
status: open

### DW-193: [MEDIUM] The `generated-api-smoke-preflight.sh` exit-code contract is recorded as AC6 evidence (exit 3 = `topology-not-running`) but is executed by no automated check in either repo. Its own shell validation exists and is never invoked.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: generated-api-smoke-preflight.sh
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM] The `generated-api-smoke-preflight.sh` exit-code contract is recorded as AC6 evidence (exit 3 = `topology-not-running`) but is executed by no automated check in either repo. Its own shell validation exists and is never invoked. evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28. `grep -rn "generated-api-smoke-preflight" .github/workflows/` returns nothing. `scripts/tests/generated-api-smoke-preflight.test.sh` (Story 3.8 AC8) is referenced only by itself. `tests/Hexalith.EventStore.Testing.Integration.Tests/GeneratedApiSmokePreflightDiagnosticsTests.cs:16-102` asserts only redaction, output categories, message classification and port constants — it never executes the script and never asserts an exit code. Returning `1` instead of `3` would break every documented consumer with no test failing. Deferred rather than patched because AC7 forbids workflow changes in this story and the script is owned by Story 3.8. Suggested durable fix: run the existing `scripts/tests/generated-api-smoke-preflight.test.sh` as a step in `ci.yml`.
status: open

### DW-194: [MEDIUM, FrontComposer-owned] FrontComposer commit `b6efcad5` uses type `fix:`, so semantic-release will cut a patch NuGet release whose only delta is a workflow pin and a governance ledger JSON.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: references/Hexalith.FrontComposer/.releaserc.json:5-9
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM, FrontComposer-owned] FrontComposer commit `b6efcad5` uses type `fix:`, so semantic-release will cut a patch NuGet release whose only delta is a workflow pin and a governance ledger JSON. evidence: Story 3.1 closure code review (edge-case-hunter), verified 2026-07-28 against `references/Hexalith.FrontComposer/.releaserc.json:5-9` — `@semantic-release/commit-analyzer` with `preset: conventionalcommits` and no `releaseRules` scope or path filter. This is the same class of defect as the project rule "Don't use `feat` for refactors (false minor bump + NuGet publish)"; `ci:` or `build:` would have been the non-releasing type. The commit is already on `origin/main`, so correcting it requires a revert-and-recommit by its owner. Suggested durable fix: add `releaseRules` excluding CI-scoped commits, or use a non-releasing type for pin/ledger maintenance.
status: open

### DW-195: [MEDIUM] EventStore and FrontComposer enforce opposite invariants on the release pin. EventStore asserts the pinned Builds release SHA must differ from its `references/Hexalith.Builds` gitlink; FrontComposer asserts they must be equal. Applying the Story 3.1 fix pattern to EventStore would break EventStore's guard.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
archived: 2026-09-18

### DW-196: [LOW, FrontComposer-owned] Asymmetric supply-chain pinning: `release.yml` pins Builds to an exact SHA under the REL-6 identity rationale, while `ci.yml` and `quality.yml` both consume Builds at `@main` — the lanes that authorize the release run unpinned Builds code.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
archived: 2026-09-18

### DW-197: [MEDIUM, FrontComposer-owned] The release-pin fix duplicates work already tracked as REL-6 and landed direct-to-main against FrontComposer's own frozen spec, which lists "Committing directly to `main` instead of a `fix/` branch + PR" under Ask First.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: fix/
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM, FrontComposer-owned] The release-pin fix duplicates work already tracked as REL-6 and landed direct-to-main against FrontComposer's own frozen spec, which lists "Committing directly to `main` instead of a `fix/` branch + PR" under **Ask First**. evidence: Story 3.1 closure code review (blind-hunter), verified 2026-07-28. `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/spec-fix-release-builds-execution-sha.md` is status `done` and frozen-after-approval with that Ask First entry; `b6efcad5` is a direct-to-main commit (`origin/main == b6efcad5`, no merge commit). The drift was not a fresh discovery: it is logged as REL-6 in `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/deferred-work.md:1853` and named in that repo's `sprint-status.yaml:467` as a known pre-existing baseline failure, yet the Story 3.1 record presents it as newly root-caused and its own to fix. Owned by the Hexalith.FrontComposer maintainer.
status: open

### DW-198: [MEDIUM, FrontComposer-owned] Analyzer-ledger hardening bundle — the CA1707 fail-closed gate compares only an aggregate count and one hash, so re-attestation is indistinguishable from laundering a violation.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
archived: 2026-09-18

### DW-199: [MEDIUM] The live-sidecar lane has no placement isolation from other local EventStore hosts. Its actor type names are fixed, non-namespaced constants, so any concurrently running EventStore-derived app joins the same DAPR placement ring and a random subset of test actor IDs hash-routes to a foreign — possibly dead — host, failing the run with `connection refused` on an app port the fixture does not own.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM] The live-sidecar lane has no placement isolation from other local EventStore hosts. Its actor type names are fixed, non-namespaced constants, so any concurrently running EventStore-derived app joins the same DAPR placement ring and a random subset of test actor IDs hash-routes to a foreign — possibly dead — host, failing the run with `connection refused` on an app port the fixture does not own. evidence: Observed live during the Story 3.1 AC6 re-run, 2026-07-28, at root `589da8b9`. Four consecutive `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests` runs on byte-identical, un-rebuilt binaries produced `3 failed / 46 passed`, then `49/49`, `49/49`, `49/49`. All three failures were in `Integration/NamedProjectionDispatchLiveSidecarTests`, clustered within 300 ms; two raised `Dapr.DaprApiException … dial tcp 127.0.0.1:37313: connect: connection refused` on `ProjectionActor.DiscardProjectionAsync` and `ProjectionLifecycleActor.BeginDeliveryWriteAsync`, and the third (`ReadStateJsonAsync … should not be null but was`) is the downstream state consequence. Port `37313` had no listener (`ss -ltnp`) and is not the fixture's own app port — `DaprTestContainerFixture.cs:875` asserts its Kestrel binding, and each test's opening `fixture.ThrowIfHostStopped()` did not trip. `docker logs dapr_placement` over the window shows eight foreign app-ids in namespace `default` (`eventstore` ×10 status reports, `tenants-api`, `tenants`, `sample`, `memories`, `eventstore-admin`, `eventstore-admin-ui`, `commandapi`) interleaved with the fixture's per-run `eventstore-live-<guid>` hosts. DAPR partitions the placement ring by namespace **and actor type**, and `ProjectionActor`/`ProjectionLifecycleActor`/`AggregateActor` are shared fixed names, so foreign hosts join the ring the fixture believes it owns; freshly generated per-run actor IDs explain why the fault is probabilistic rather than reproducible. This is the same shared-placement / fixed-name-actor constraint already known for this repo's Tier-3 suite. Classified as an environment blocker under the story's "Environment is not product behavior" guardrail — no product or test code differs between the failing and passing runs — and deferred rather than patched because Story 3.1 AC7 forbids changing the fixture, trait taxonomy, or DAPR readiness thresholds absent a proven product defect. CI exposure is lower but non-zero: `integration.yml` runs `dapr-init` on a fresh runner with no foreign EventStore hosts, so contention there would require self-inflicted concurrency. Suggested durable fix, smallest first: give the fixture a dedicated placement instance on a private port instead of the shared `dapr_placement` container; failing that, namespace the fixture's daprd (`--namespace eventstore-live-<guid>`) so its ring cannot be joined; failing that, have `VerifyPrerequisitesAsync` enumerate foreign placement members and fail closed with an environment-blocker diagnostic instead of surfacing a bare `connection refused` mid-suite.
status: open

### DW-200: [LOW] The `docs/ci.md` publication-pin literal is unguarded, so the exact drift the Story 3.1 closure fixed (`cf04c419…` → `f75daebd…`) will recur silently at the next Builds pin advance.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: post-merge code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
archived: 2026-09-18

### DW-201: [MEDIUM] The `RequestTimeout` *behavioral* contract is unverified in every lane. Story 3.2 closed the "the value is plumbed" half; the "the value takes effect" half — a slow or unreachable actor actually failing open at the configured window — is asserted by no test anywhere.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.2 (2026-07-29)"), 2026-08-30
location: references/
reason: source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md` summary: [MEDIUM] The `RequestTimeout` *behavioral* contract is unverified in every lane. Story 3.2 closed the "the value is plumbed" half; the "the value takes effect" half — a slow or unreachable actor actually failing open at the configured window — is asserted by no test anywhere. evidence: Story 3.2 code review (verification-gap), verified 2026-07-29. `grep -rn "RequestTimeout" --include=*.cs` excluding `references/` touches the ETag path in exactly three places: the production assignment at `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:24` and the two unit assertions at `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs:43` and `:70`. Both assertions only inspect the `ActorProxyOptions` object handed to a **substituted** factory. The live-sidecar tests (`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/DaprETagServiceLiveSidecarTests.cs:69,95`) supply 30s but assert only `actual.ShouldBe(expectedETag)` and `actual.ShouldBeNull()` — neither asserts elapsed time nor a timeout-induced fail-open, and no slow-actor simulation exists on this path. The documented invariant at `DaprETagService.cs:19-22` ("a slow or unreachable actor never blocks the projection read path") is relied on by `CachingProjectionActor.cs:49`. Regression that would ship green: if `ActorProxyOptions.RequestTimeout` stopped being honoured by the proxy (a plausible `Dapr.Actors` upgrade side effect — pinned at 1.18.5), a hung ETag actor would block the projection read path indefinitely while every test stays green. Suggested durable fix: a live-sidecar fact pointing `DaprETagService` at an unreachable/paused endpoint with a short override (~1s), asserting both `ShouldBeNull()` and elapsed time bounded near the configured window.
status: open

### DW-202: [MEDIUM] `DaprETagService` always passes a non-null `ActorProxyOptions`, which wholesale-replaces the DI-configured `ActorProxyFactory.DefaultOptions` — so the ETag path can target the environment-variable default endpoint and an empty API token while every other actor call site honours the configured values. Constructor-time environment parsing also sits outside the fail-open boundary.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.2 (2026-07-29)"), 2026-08-30
location: DaprETagService.cs:23-25
reason: source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md` summary: [MEDIUM] `DaprETagService` always passes a non-null `ActorProxyOptions`, which wholesale-replaces the DI-configured `ActorProxyFactory.DefaultOptions` — so the ETag path can target the environment-variable default endpoint and an empty API token while every other actor call site honours the configured values. Constructor-time environment parsing also sits outside the fail-open boundary. evidence: Story 3.2 code review (edge-case-hunter), 2026-07-29. Pre-dates FR18 — before CP-5 the field was `static readonly` but equally non-null, so the bypass is not a Story 3.2 regression. `Dapr.Actors.Client.ActorProxyFactory.CreateActorProxy<T>(actorId, actorType, options)` resolves `options ?? defaultOptions`, consulting `DefaultOptions` only when `options` is null (decompiled 1.18.5 — re-verify before acting). `DaprETagService.cs:23-25` always supplies its own instance, so `HttpEndpoint`, `DaprApiToken`, `JsonSerializerOptions` and `UseJsonSerialization` fall back to `ActorProxyOptions`' own initializers (`DaprDefaults.GetDefaultHttpEndpoint(null)` / `GetDefaultDaprApiToken(null)`, which read **environment variables only** given a null `IConfiguration`). Meanwhile `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:196-200` sets `options.HttpEndpoint` from `configuration["DAPR_HTTP_PORT"]`, and `AddActors` copies that into `factory.DefaultOptions`. Practical exposure is limited to deployments where `DAPR_HTTP_PORT`/`DAPR_API_TOKEN` arrive via appsettings, user-secrets or command line rather than the process environment — which the codebase does treat as a config key (`src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:110`). Consequence there: 401/connect failure → generic catch at `DaprETagService.cs:57` → fail-open null on every fetch, ETag/304 caching permanently dead behind a Warning log. Related: the `new ActorProxyOptions { … }` field initializer runs `int.Parse` on `DAPR_HTTP_PORT` and `new Uri(...)` on `DAPR_HTTP_ENDPOINT` inside the **constructor**, before `GetCurrentETagAsync`'s try/catch exists, so a malformed value faults scope resolution under `TryAddScoped` instead of degrading. Suggested durable fix: seed `_proxyOptions` from the factory's configured defaults and override only `RequestTimeout`.
status: open

### DW-203: [LOW] The `requestTimeout` override parameter validates nothing. `TimeSpan.Zero`, any negative value, or a value above `int.MaxValue` ms throws inside the `try` and is swallowed into a permanent silent fail-open; `Timeout.InfiniteTimeSpan` is accepted and removes the fail-open bound the class documents as its core invariant.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.2 (2026-07-29)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:45-46
reason: source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md` summary: [LOW] The `requestTimeout` override parameter validates nothing. `TimeSpan.Zero`, any negative value, or a value above `int.MaxValue` ms throws inside the `try` and is swallowed into a permanent silent fail-open; `Timeout.InfiniteTimeSpan` is accepted and removes the fail-open bound the class documents as its core invariant. evidence: Story 3.2 code review (edge-case-hunter), 2026-07-29. `Dapr.Actors.DaprHttpInteractor..ctor` does `httpClient.Timeout = requestTimeout ?? httpClient.Timeout`, and `HttpClient.Timeout` throws `ArgumentOutOfRangeException` for zero, negatives other than `Timeout.InfiniteTimeSpan`, and values above `int.MaxValue` ms. That throw occurs at `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:45-46`, inside the `try`, so the generic `catch` at `:57` returns null forever. For the infinite case, `ActorProxyOptions.RequestTimeout`'s own XML doc confirms it disables timeouts; combined with `proxy.GetCurrentETagAsync()` taking no `CancellationToken` (`:48`, deliberate — see the remoting-interface comment at `:36-44`), neither bound survives and `CachingProjectionActor.QueryAsync` (`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs:50-52`) can block indefinitely. Real-world reachability is currently nil: all 13 `new DaprETagService(...)` sites in the repo are tests, and DI supplies nothing for the optional parameter. Rated LOW for that reason; it becomes material the moment a production caller supplies a value. Also note the in-file comment at `:42` still hardcodes "the 3 s RequestTimeout" although the window is now instance-dependent. Suggested durable fix: `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout.Value, TimeSpan.Zero)` plus an upper bound in the constructor, so a bad value fails fast instead of failing open forever.
status: open

### DW-204: [LOW] Two unguarded seams around the Tenants dependency: nothing asserts the `references/Hexalith.Tenants` gitlink is coherent with the `HexalithTenantsVersion` package pin, and no test exercises the DI construction path that supplies `DaprETagService`'s 3s production default.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.2 (2026-07-29)"), 2026-08-30
location: references/Hexalith.Tenants
reason: source_spec: `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md` summary: [LOW] Two unguarded seams around the Tenants dependency: nothing asserts the `references/Hexalith.Tenants` gitlink is coherent with the `HexalithTenantsVersion` package pin, and no test exercises the DI construction path that supplies `DaprETagService`'s 3s production default. evidence: Story 3.2 code review (verification-gap + edge-case-hunter), verified 2026-07-29. (1) `grep -rn "HexalithTenantsVersion" --include=*.cs tests/` returns nothing; the only `.gitmodules`-reading test (`tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs:80-81`) checks solely for the `Hexalith.AI.Tools` path, and the sole source-mode lane (`.github/workflows/ci.yml:64-108`) runs exactly one filtered class, `TenantsApiLaunchSettingsTests`. A published Tenants package diverging from the pinned submodule source would ship undetected — this bit the repo in this very diff, where the Builds gitlink advanced the pin 5.0.0 → 5.1.0 with no story record. (2) `grep -rn "GetRequiredService<IETagService>\|GetService<IETagService>" tests/ src/` returns no hits: all 15 deterministic facts and both live facts use `new DaprETagService(...)`. The 3s production default therefore rests entirely on the container's `HasDefaultValue` fallback for the unregistered optional `TimeSpan?` at `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54`, asserted nowhere. Story Task 1's "confirm no `TimeSpan`/`ActorProxyOptions` service is registered" was a one-time manual read, not a durable guard: a future `services.AddSingleton(TimeSpan…)` from any Hexalith module would silently retime every production ETag fetch with all tests green. Suggested durable fix: a gitlink/pin coherence assertion in the packaging governance suite, and one fact that resolves `IETagService` from a real `ServiceCollection` and asserts the effective timeout.
status: open

### DW-205: [MEDIUM] `RepositoryProjectPaths.GetReferencedModuleProjectPath` probes a root-level `<root>/Hexalith.<Module>/` checkout at higher precedence than `references/`, and its docstring falsely claims the candidate order mirrors `Directory.Build.props`.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.3 (2026-07-29)"), 2026-08-30
location: <root>/Hexalith.<Module>/
reason: source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` summary: [MEDIUM] `RepositoryProjectPaths.GetReferencedModuleProjectPath` probes a root-level `<root>/Hexalith.<Module>/` checkout at higher precedence than `references/`, and its docstring falsely claims the candidate order mirrors `Directory.Build.props`. evidence: Story 3.3 code review (blind-hunter, verified 2026-07-29 at HEAD `1d42528b`). `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:80` is candidate 4, `Path.Combine(root, moduleDirectory, relative)` (commented "root-level sibling module checkout"), evaluated before candidate 5 `standalone` = `<root>/references/<module>/…` at `:81`. The docstring at `:44-51` states the helper probes "every checkout layout in the same order as the `$(Hexalith*Root)` auto-detection in `Directory.Build.props`" — that is false: `Directory.Build.props:23` puts `references/Hexalith.Tenants/src` first (with an explicit comment that it "takes precedence… over any sibling/standalone clone"), `:40` puts `references/Hexalith.Commons` first, and `grep -c 'MSBuildThisFileDirectory)Hexalith\.' Directory.Build.props` returns **0** — MSBuild has no root-level probe at all. Consequence: a stray root-level `Hexalith.Tenants/` directory silently shadows `references/Hexalith.Tenants`, which is exactly the root-level path assumption FR19 exists to retire, and the AppHost could launch a different csproj than it builds. Not patched because AC4 forbids replacing the flexible resolver absent a proven break, Dev Notes `:169` explicitly accepts it ("the required invariant is that `references/` remains the fallback/convention"), and AC1 verified no such directory exists today. Text-based stale-path scans structurally cannot see it because the path is composed via `Path.Combine`. Suggested durable fix, smallest first: correct the docstring to describe actual precedence; then reorder candidate 4 below `standalone` so `references/` wins, guarded by a test.
status: open

### DW-206: [MEDIUM] AC4 was closed without any test proving that a present module under `references/` resolves; 5 of the 7 resolver candidates are entirely uncovered.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.3 (2026-07-29)"), 2026-08-30
location: references/
reason: source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` summary: [MEDIUM] AC4 was closed without any test proving that a **present** module under `references/` resolves; 5 of the 7 resolver candidates are entirely uncovered. evidence: Story 3.3 code review (blind-hunter + acceptance-auditor), verified 2026-07-29. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs` has 9 tests (5 `[Fact]` + 4 `[InlineData]`), of which exactly one touches the referenced-module helper: `GetReferencedModuleProjectPath_WhenModuleMissing_ReturnsReferencesFallback` (`:31-46`). That case returns the `standalone` path only because **nothing on disk exists** — it exercises the no-match `return standalone` at `RepositoryProjectPaths.cs:94`, never candidate 5 itself. Candidates 2, 3, 4, 6 and 7 (`:78-83`) have no coverage. A verification-and-reconciliation story for FR19 therefore added zero coverage to the single helper FR19's Aspire clause depends on. Suggested durable fix: drive the helper against a temporary directory tree so each layout can be materialised and asserted; this requires making the repository root injectable, since `GetRepositoryRoot()` (`:104`) derives from `AppContext.BaseDirectory`.
status: open

### DW-207: [LOW] `GetReferencedModuleProjectPath` omits the path-segment validation and root-containment check that its sibling `GetProjectPath` enforces, so a rooted segment would escape the repository root.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.3 (2026-07-29)"), 2026-08-30
location: src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:30
reason: source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` summary: [LOW] `GetReferencedModuleProjectPath` omits the path-segment validation and root-containment check that its sibling `GetProjectPath` enforces, so a rooted segment would escape the repository root. evidence: Story 3.3 code review (blind-hunter), verified 2026-07-29. `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:30` calls `ValidateRelativePathSegments` and `:38-40` rejects any result that does not resolve under the repository root; the referenced-module helper at `:55-64` performs only null/empty checks on `moduleDirectory` and an array-length check on `moduleRelativePath`. `Path.Combine` discards everything preceding a rooted segment, so a rooted `moduleRelativePath` element yields an arbitrary absolute path with no containment check. `GetProjectPath_WhenSegmentIsRooted_ThrowsArgumentException` (`RepositoryProjectPathsTests.cs:68-74`) has no counterpart for the helper AC4 explicitly names. Rated LOW because every current call site passes compile-time literals (`EventStorePlatformProjectMetadata.cs`, 3 sites). Suggested durable fix: call `ValidateRelativePathSegments(moduleRelativePath)` and apply the same rooted-prefix containment assertion to the returned candidate.
status: open

### DW-208: [MEDIUM] `SampleApiLaunchSettingsTests` hand-rolls newline-sensitive YAML parsing that survives CRLF only by accident, while its sibling parses the same file with YamlDotNet.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.3 (2026-07-29)"), 2026-08-30
location: tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:103
reason: source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` summary: [MEDIUM] `SampleApiLaunchSettingsTests` hand-rolls newline-sensitive YAML parsing that survives CRLF only by accident, while its sibling parses the same file with YamlDotNet. evidence: Story 3.3 code review (edge-case-hunter + verification-gap), verified 2026-07-29. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:103` splits on a bare `'\n'`, `:124` rejoins on `'\n'`, and `:135` re-splits on `'\n'`, consuming `File.ReadAllText` at `:67-72`. It tolerates CRLF only because the `.Trim()` calls at `:107` and `:137` incidentally strip the stray `\r` — nothing in the design guarantees it, and a CR-only, NEL, LS or PS terminator collapses the split to a single element, yielding zero policies and failing `:75 ShouldBe(1)`. `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` is `w/lf` today, but `.editorconfig` has only `[*]` and `[*.cs]` sections — no `[*.yaml]` override — so its `end_of_line = crlf` applies and a conforming editor will rewrite the file. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:107-151` already parses this identical file with YamlDotNet, which is an existing `PackageReference` in the same test project. Suggested durable fix: replace `ExtractYamlPolicies`/`ExtractOperations` (`:99-167`) with the sibling's YamlDotNet parsing.
status: open

### DW-209: [LOW] Three tracked shell scripts lack the executable bit, so direct invocation fails with exit 126; discovered during Story 3.3 Task 6, worked around, and never filed.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.3 (2026-07-29)"), 2026-08-30
location: git ls-files -s scripts/*.sh
reason: source_spec: `_bmad-output/implementation-artifacts/3-3-references-based-submodule-layout.md` summary: [LOW] Three tracked shell scripts lack the executable bit, so direct invocation fails with exit 126; discovered during Story 3.3 Task 6, worked around, and never filed. evidence: Story 3.3 code review (blind-hunter + acceptance-auditor), verified 2026-07-29. `git ls-files -s scripts/*.sh` reports mode `100644` for `scripts/ci-local.sh`, `scripts/check-deferred-work.sh` and `scripts/validate-release-secrets.sh`, against `100755` for `scripts/check-doc-versions.sh`, `scripts/generated-api-smoke-preflight.sh`, `scripts/validate-docs.sh`, `scripts/validate-evidence.sh` and `scripts/validate-publication-preflight.sh`. Story 3.3 `:283` records the resulting exit 126 on `./scripts/ci-local.sh --tier 1 --skip-build` and routes around it by invoking the script through `bash`, but files nothing — so the next caller hits the same wall, and any CI step or hook that invokes these three directly rather than via an interpreter breaks. Suggested durable fix: `git update-index --chmod=+x scripts/ci-local.sh scripts/check-deferred-work.sh scripts/validate-release-secrets.sh`, plus a packaging-governance assertion that every `scripts/*.sh` is mode `100755`.
status: open

### DW-210: [MEDIUM] No CI lane ever executes the source-mode (`HEXALITH_TENANTS_SOURCE`) AppHost topology, so the Tenants security dependents that `AspireSecurityResourceNamingTests` conditionally asserts are verified nowhere.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30)"), 2026-08-30
archived: 2026-09-18

### DW-211: [MEDIUM] Documentation and agent guidance invoke `aspire run --project` / `aspire publish --project`, a flag the pinned Aspire CLI 13.4.6 does not accept.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30)"), 2026-08-30
location: deploy/README.md:195,202,282,290,333,341
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] Documentation and agent guidance invoke `aspire run --project` / `aspire publish --project`, a flag the pinned Aspire CLI 13.4.6 does not accept. evidence: Story 3.4 review pass 4 (verification-gap), verified 2026-07-30 against `aspire --version` = `13.4.6+87fe259e`. `aspire run --help` and `aspire publish --help` list only `--apphost`. `--project` survives at roughly fifteen sites, including `deploy/README.md:195,202,282,290,333,341`, `docs/getting-started/quickstart.md:29`, `docs/getting-started/first-domain-service.md:203`, `docs/brownfield/development-guide.md:79,96`, `docs/guides/deployment-docker-compose.md:106,113`, `docs/guides/deployment-kubernetes.md:193,199`, `docs/guides/deployment-azure-container-apps.md:148,154`, `docs/guides/troubleshooting.md:552`, and `.claude/agents/aspire.md:62`. Story 3.4 rewrote two of these incidentally while correcting role identities, which leaves `docs/guides/troubleshooting.md` internally inconsistent. This is CLI-flag drift unrelated to the security role identity, and nothing in the repository verifies documented CLI invocations. Suggested durable fix: a docs-validation assertion that no tracked guidance passes `--project` to `aspire run`/`aspire publish`, plus a sweep of the listed sites.
status: open

### DW-212: [MEDIUM] New evidence only, no status change: the premise behind the earlier quickstart port deferral -- that the default non-persistent AppHost picks Keycloak host ports dynamically -- is contradicted by the implementation.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30)"), 2026-08-30
location: src/Hexalith.EventStore.Aspire/KeycloakFastStartPorts.cs:72-78
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] New evidence only, no status change: the premise behind the earlier quickstart port deferral -- that the default non-persistent AppHost picks Keycloak host ports dynamically -- is contradicted by the implementation. evidence: Story 3.4 review pass 4 (blind-hunter, verified 2026-07-30). `KeycloakFastStartPorts.ResolveDynamic` (`src/Hexalith.EventStore.Aspire/KeycloakFastStartPorts.cs:72-78`) calls `FindAvailablePort(DefaultHttpPort /* 8180 */, [])` and `FindAvailablePort(DefaultManagementPort /* 8543 */, [httpPort])`, each of which returns the preferred port unless it is occupied; `HexalithEventStoreSecurityExtensions.cs:82-86` binds both endpoints proxyless in the non-persistent branch as well. The extension's own comment at `:47-52` states the default "prefers 8180/8543 and moving forward when either port is busy". `docs/getting-started/quickstart.md:44`'s `localhost:8180` is therefore correct for the default topology in the ordinary case. Recorded as new evidence against an existing entry; the orchestrator owns that entry's status and resolution. Review pass 4 separately corrected the same wrong premise where this story had introduced it into `docs/guides/troubleshooting.md`.
status: open

### DW-213: [MEDIUM] The Docker Compose deployment guide still shows stale dependency-version examples (Keycloak 26.4, Aspire SDK 13.1.x) that no check covers.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30, review pass 5)"), 2026-08-30
location: docs/guides/deployment-docker-compose.md:144
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] The Docker Compose deployment guide still shows stale dependency-version examples (Keycloak 26.4, Aspire SDK 13.1.x) that no check covers. evidence: Story 3.4 review pass 3 deferral, never propagated to this ledger; re-verified 2026-07-30 at review pass 5. `docs/guides/deployment-docker-compose.md:144` shows `image: "quay.io/keycloak/keycloak:26.4"` and `:155` says "Exact field names and structure depend on the Aspire SDK version (currently 13.1.x)", while the story's own scratch `aspire publish` evidence records Keycloak 26.6 in the generated artifact and the pinned CLI/AppHost as 13.4.6. `scripts/check-doc-versions.sh` validates only the four Dapr rows in `docs/reference/nuget-packages.md`, so nothing detects this drift. Refreshing dependency-version examples is pre-existing documentation maintenance, independent of the security role-identity reconciliation. Suggested durable fix: extend `scripts/check-doc-versions.sh` to assert documented Keycloak image tags and Aspire SDK versions against `Directory.Packages.props` / the generated Compose artifact, then refresh the two sites.
status: open

### DW-214: [MEDIUM] New evidence only, no status change: the `aspire run --project` drift also exists in production source and in more sites than the earlier entry counts.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30, review pass 5)"), 2026-08-30
location: src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:150
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] New evidence only, no status change: the `aspire run --project` drift also exists in production source and in more sites than the earlier entry counts. evidence: Story 3.4 review pass 5 (blind-hunter, verified 2026-07-30). `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:150` emits `" aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"` in a runtime diagnostic message, so the invalid flag reaches users from shipped code, not only from documentation; a documentation-only sweep would leave it behind. `docs/guides/configuration-reference.md:597,600,610` additionally use `dotnet run --project src/Hexalith.EventStore.AppHost`, the same form this story replaced in `docs/guides/troubleshooting.md`, and `scripts/generated-api-smoke-preflight.sh` passes `--project` at three sites. The tracked total is therefore higher than the "roughly fifteen sites" the earlier entry records. Recorded as new evidence against that entry; the orchestrator owns its status and resolution.
status: open

### DW-215: [MEDIUM] Building the real AppHost model inside a unit test mutates a machine-wide temp directory, so the AppHost suite can disturb a concurrently running `aspire run`.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30, review pass 5)"), 2026-08-30
location: src/Hexalith.EventStore.AppHost/Program.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] Building the real AppHost model inside a unit test mutates a machine-wide temp directory, so the AppHost suite can disturb a concurrently running `aspire run`. evidence: Story 3.4 review pass 5 (blind-hunter, verified 2026-07-30). `AspireSecurityResourceNamingTests` calls `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`, which executes `src/Hexalith.EventStore.AppHost/Program.cs` top to bottom, including `ResolveIsolatedDaprComponentPath` at `Program.cs:249-266`. That helper deletes every `*.yaml` under `Path.GetTempPath()/hexalith-eventstore-dapr-components/statestore` (`:259-261`) and re-copies the component. The path is isolated from the repository, not per process, and `AspireEnvironmentMutationCollection` serialises only in-process environment mutation. Running the AppHost test assembly while a live topology is up therefore deletes and recreates the component file the running sidecars were started from; the end state is byte-identical, so the window is narrow, but a sidecar starting inside it can fail to read its state-store component. Pre-existing AppHost behaviour; the new exposure is that a normal test run now executes it. Suggested durable fix: give `ResolveIsolatedDaprComponentPath` a per-instance subdirectory (or an env-var override the test can point at a temp path), which requires touching production AppHost source and so falls outside this story's boundary.
status: open

### DW-216: The vendor-managed WSL `codex-node-repl` launcher has no process-group shutdown contract for its Windows `node_repl.exe` descendant, so a native runtime that ignores forwarded termination could outlive a forcibly killed JavaScript launcher.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30, review pass 5)"), 2026-08-30
location: /mnt/c/Users/JeromePiquot/AppData/Roaming/npm/codex-node-repl.js
reason: source_spec: `_bmad-output/implementation-artifacts/spec-fix-mcp-startup.md` summary: The vendor-managed WSL `codex-node-repl` launcher has no process-group shutdown contract for its Windows `node_repl.exe` descendant, so a native runtime that ignores forwarded termination could outlive a forcibly killed JavaScript launcher. evidence: Review of the MCP startup fix found that `/mnt/c/Users/JeromePiquot/AppData/Roaming/npm/codex-node-repl.js` spawns `node_repl.exe` and forwards only SIGINT, SIGTERM, and SIGHUP to that immediate child; the user-scoped proxy now maps SIGQUIT, preserves caller signal status, and bounds its own immediate-child shutdown, but cannot guarantee descendant cleanup without modifying the vendor bridge or establishing a tested cross-WSL process-group mechanism, both outside this fix's boundary.
status: open

### DW-217: [MEDIUM] Semantic-release uploads GitHub Release assets with the unscoped glob `nupkgs/*.nupkg`, so the exact-scope guarantee AC3 pins on the NuGet push command has no equivalent on the second publication channel.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.6 (2026-07-31, follow-up review pass)"), 2026-08-30
location: nupkgs/*.nupkg
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md` summary: [MEDIUM] Semantic-release uploads GitHub Release assets with the unscoped glob `nupkgs/*.nupkg`, so the exact-scope guarantee AC3 pins on the NuGet push command has no equivalent on the second publication channel. evidence: Story 3.6 follow-up review (2026-07-31). `.releaserc.json:12` publishes to NuGet with `dotnet nuget push "./nupkgs/Hexalith.EventStore.*.nupkg"`, and `ReleasePackageManifestTests.Semantic_release_publish_command_pushes_scoped_packages` now asserts that glob appears exactly once and that the unscoped form is absent. `.releaserc.json:18` still declares `"assets": ["nupkgs/*.nupkg"]` for the `@semantic-release/github` plugin, which is untouched by this story's diff and uncovered by the new exact-scope assertion. The live risk is currently mitigated, not eliminated: `tools/validate-release-packages.py` runs in `prepareCmd` before publish and fails closed on any archive in `./nupkgs` outside the 14-entry manifest, so the unscoped glob can only match manifest packages on a successful release. It is pre-existing configuration, independent of the archive-metadata contract this story delivered. Suggested durable fix: narrow the asset glob to `nupkgs/Hexalith.EventStore.*.nupkg` and extend the publish-governance test to assert both publication channels are EventStore-scoped.
status: open

### DW-218: [LOW] New evidence only, no status change: the 2026-07-31 story-3.6 asset-glob entry above cites a test name that does not exist, so its evidence trail is unfollowable.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.6 (2026-07-31, follow-up review pass 2)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md` summary: [LOW] New evidence only, no status change: the 2026-07-31 story-3.6 asset-glob entry above cites a test name that does not exist, so its evidence trail is unfollowable. evidence: Story 3.6 follow-up review pass 2 (2026-07-31, verified against `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`). That entry attributes the exact-scope publish assertion to `ReleasePackageManifestTests.Semantic_release_publish_command_pushes_scoped_packages`; no test of that name exists in the repository. The assertion is real but lives in `Semantic_release_delegates_package_inventory_to_manifest_scripts`, which now also pins that `tools/pack-release-packages.py` precedes `tools/validate-release-packages.py` in `prepareCmd` — the command ordering the earlier entry's stated mitigation depends on and which nothing previously asserted. The deferred finding itself (the unscoped `nupkgs/*.nupkg` GitHub asset glob at `.releaserc.json:18`) is unchanged and still open. Recorded as new evidence against that entry; the orchestrator owns its status and resolution.
status: open

### DW-219: [MEDIUM] Reconcile `epic-2: in-progress` with all listed Epic 2 stories and its retrospective marked `done`. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:79`. Pre-existing sprint-tracking inconsistency outside Story 8.1.

origin: migrated from legacy ledger ("Deferred from: code review of 8-1-shared-payload-protection-security-spec-and-adr (2026-08-01)"), 2026-08-30
location: _bmad-output/implementation-artifacts/sprint-status.yaml:79
severity: medium
reason: [MEDIUM] Reconcile `epic-2: in-progress` with all listed Epic 2 stories and its retrospective marked `done`. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:79`. Pre-existing sprint-tracking inconsistency outside Story 8.1.
status: open

### DW-220: [LOW] Add the intentionally preserved `awaiting-operator` value to the sprint-status schema comments. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:19,205`. Pre-existing schema-comment drift outside Story 8.1.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 8-1-shared-payload-protection-security-spec-and-adr (2026-08-01)"), 2026-08-30
archived: 2026-09-18

### DW-221: [MEDIUM] Separate unrelated Epic 1-7 tracking changes from the Story 8.1 baseline evidence so scope attribution is reviewable. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:51`. The shared sprint file accumulated concurrent story updates after the recorded baseline.

origin: migrated from legacy ledger ("Deferred from: code review of 8-1-shared-payload-protection-security-spec-and-adr (2026-08-01)"), 2026-08-30
location: _bmad-output/implementation-artifacts/sprint-status.yaml:51
severity: medium
reason: [MEDIUM] Separate unrelated Epic 1-7 tracking changes from the Story 8.1 baseline evidence so scope attribution is reviewable. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:51`. The shared sprint file accumulated concurrent story updates after the recorded baseline.
status: open

### DW-222: [LOW] `review_loop_iteration: 1` was not incremented despite two documented hardening passes recorded in the same file's Spec Change Log.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of story-3.13 (2026-08-04)"), 2026-08-30
archived: 2026-09-18

### DW-223: [LOW] The story's File List omits several evidence files the tests and crosswalk already depend on and validate.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.13 (2026-08-04)"), 2026-08-30
location: 3-13-deployed-runtime-parity-closure.md:588
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: [LOW] The story's File List omits several evidence files the tests and crosswalk already depend on and validate. evidence: `3-13-deployed-runtime-parity-closure.md:588` lists only a bare evidence-directory reference plus `reviewer-roster.json`, but `DeployedRuntimeParityClosureTests.cs` and `identity-crosswalk.json` reference and validate additional files (e.g. `deployment-authority.json`, `deployment-authority-source.json`, `release-provenance.json`, and further smoke/log files) not named in the File List. Documentation completeness only, not a functional gap.
status: open

### DW-224: [MEDIUM] Three event-payload writers disagree on serializer options, so persisted payload casing depends on which path produced the event.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 1 (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:29
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [MEDIUM] Three event-payload writers disagree on serializer options, so persisted payload casing depends on which path produced the event. evidence: `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:29`, `src/Hexalith.EventStore.Server/Events/EventPersister.cs:71` and `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` all call `JsonSerializer.SerializeToUtf8Bytes` with implicit options. On the deployed DAPR topology `DomainServiceWireResult` is the real writer — `DaprDomainServiceInvoker.cs:192-198` wraps its bytes as `SerializedEventPayload` and `EventPersister.cs:70-71` passes them through untouched — so a future change to `EventPersister`'s options is inert in production. Harmless today (all readers are case-insensitive after Story 4.3), but any converter or naming-policy change applies to one writer only. Story 4.3 was deliberately narrowed to readers-only on 2026-08-07 rather than change a cross-process wire format; unifying the writers needs its own story with a rollout plan.
status: open

### DW-225: [LOW] The shared payload options are frozen with no extension seam, so a domain cannot register a converter for its own payload types.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 1 (2026-08-07)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] The shared payload options are frozen with no extension seam, so a domain cannot register a converter for its own payload types. evidence: `EventStorePayloadSerialization.Options` is made read-only in its static initializer with the reflection resolver baked in. Domains needing enum-as-string, value-object or polymorphic payload converters have no supported way to contribute one, and no `JsonSerializerContext` can be chained in. This is not a regression — every call site previously used its own bare Web options with the same limitation — but centralizing makes it a single explicit decision point. AOT/trimming remain out of scope per the Epic 4 context (reflection-based dispatch is load-bearing).
status: open

### DW-226: [LOW] `EventStoreProjection.Project`'s typed overload silently skips events with no matching Apply method.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 1 (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs:89-92
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] `EventStoreProjection.Project`'s typed overload silently skips events with no matching Apply method. evidence: `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs:89-92` does `evt.GetType().Name` plus a plain `TryGetValue` with no fallback and no diagnostic, so an unmatched typed event is dropped without a log or throw and the read model is built with missing state. The sibling JSON path throws. Explicitly excluded from Story 4.3 scope to keep the change to type-name resolution and serializer options.
status: open

### DW-227: [LOW] The static Apply-method registry cache is unbounded and roots `Type` objects for process lifetime.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 1 (2026-08-07)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] The static Apply-method registry cache is unbounded and roots `Type` objects for process lifetime. evidence: The per-state registry cache keyed by `Type` is never evicted, which blocks collectible-assembly unload in plugin or hot-reload hosts. Pre-existing shape — both `DomainProcessorStateRehydrator` and `EventStoreProjection` already held process-wide static caches before Story 4.3; the story consolidates them without changing the lifetime policy.
status: open

### DW-228: [MEDIUM] `AggregateReconstructionErrorCategory` has no member for apply-method ambiguity, so replay reports it as `UnknownEventType`.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 2 (2026-08-07)"), 2026-08-30
location: AggregateReconstructionErrorCategory
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [MEDIUM] `AggregateReconstructionErrorCategory` has no member for apply-method ambiguity, so replay reports it as `UnknownEventType`. evidence: `AggregateReplayer` maps `AmbiguousApplyMethodException` onto `AggregateReconstructionErrorCategory.UnknownEventType`, but the type is not unknown — it is known twice, and the two failures have completely different operator remediations ("this stream references a type I have never heard of" versus "your state type has colliding Apply overloads"). Admin and RFC 7807 consumers cannot distinguish them. Adding an `AmbiguousEventType` member changes a public enum in `Hexalith.EventStore.Contracts`, which is a package contract change and was outside Story 4.3's stated scope.
status: open

### DW-229: [LOW] Private `JsonSerializerDefaults.Web` options copies survive outside the four Client reader paths that Story 4.3 unified.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 2 (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs:18
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] Private `JsonSerializerDefaults.Web` options copies survive outside the four Client reader paths that Story 4.3 unified. evidence: `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs:18` and `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:35` each construct their own `new(JsonSerializerDefaults.Web)`. Both are behaviourally identical to the shared instance today, so there is no live drift, but they are outside the guardrail and would not follow a future converter change. Story 4.3 deliberately scoped to event/command payload binding; these serialize dispatch envelopes, not payloads.
status: open

### DW-230: [LOW] The anchored suffix scan is O(registered keys) per event with no memoization.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 2 (2026-08-07)"), 2026-08-30
location: perf/
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] The anchored suffix scan is O(registered keys) per event with no memoization. evidence: A stored name that misses both exact maps walks the whole `SuffixKeys` list on every event of every replay or projection pass. Impact is much smaller than it looks — Story 4.3 registers the fully qualified name, so the exact-match lookup now hits on the normal path where it previously always missed, making the scan the rare branch rather than the hot one. A per-table resolution cache keyed on the stored name would restore O(1) if it ever matters. No benchmark accompanies the change despite `perf/` existing.
status: open

### DW-231: [LOW] Checked-in generated API reference does not list the new public types.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 2 (2026-08-07)"), 2026-08-30
location: docs/reference/api/Hexalith.EventStore.Client/Hexalith.EventStore.Client.Aggregates.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] Checked-in generated API reference does not list the new public types. evidence: `docs/reference/api/Hexalith.EventStore.Client/Hexalith.EventStore.Client.Aggregates.md` lists `MissingApplyMethodException` but not the new public `AmbiguousApplyMethodException`, and there is no generated page for the new public namespace `Hexalith.EventStore.Contracts.Serialization`. These files are generated with `ApiReferenceBuild=true`, so the fix is a regeneration pass rather than a hand edit.
status: open

### DW-232: [LOW] `AssemblyScanner` falls back to `GetTypes()` when `GetExportedTypes()` throws, weakening the "internal fixtures cannot leak into discovery tests" assumption.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 loop 2 (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs:187
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] `AssemblyScanner` falls back to `GetTypes()` when `GetExportedTypes()` throws, weakening the "internal fixtures cannot leak into discovery tests" assumption. evidence: `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs:187` falls back to `GetTypes()` on failure, which returns non-exported types. Test fixtures declared `internal` specifically to stay invisible to assembly-wide discovery tests would become visible on that path. Pre-existing scanner behaviour, not introduced by Story 4.3.
status: open

### DW-233: [LOW] Typed-instance rehydrate path builds `MissingApplyMethodException` with `evt.GetType().Name` instead of the CLR full name.

origin: migrated from legacy ledger ("Deferred from: code review of story-4.3 (2026-08-08)"), 2026-08-30
location: DomainProcessorStateRehydrator.cs:192-194
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md` summary: [LOW] Typed-instance rehydrate path builds `MissingApplyMethodException` with `evt.GetType().Name` instead of the CLR full name. evidence: `DomainProcessorStateRehydrator.cs:192-194` — on the runtime-instance path a cross-namespace near-miss reports the colliding short name. Pre-existing diagnostic shape on this path; Story 4.3 did not own MissingApplyMethodException message fidelity for typed instances. Envelope path already passes the stored full name.
status: open

### DW-234: [HIGH] Dead-letter `cloudevent.id` is keyed on `CorrelationId`, so two dead-letters sharing a correlation id can be deduplicated away by subscribers.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:56
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [HIGH] Dead-letter `cloudevent.id` is keyed on `CorrelationId`, so two dead-letters sharing a correlation id can be deduplicated away by subscribers. evidence: `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:56` sets `["cloudevent.id"] = safeMessage.CorrelationId`, unlike event publication which uses the per-event `MessageId` (`EventPublisher.cs:200`). Pre-existing since the dead-letter path was introduced, but Story 4.4 makes it load-bearing: drain exhaustion is now a terminal data-loss sink, so a deduplicated exhaustion dead-letter means committed events vanish with no trace. Fix by carrying a per-message id override so exhaustion publishes with `cloudevent.id` = the reduced command's `MessageId`.
status: open

### DW-235: [MEDIUM] `ReplayController` accepts `PublishFailed` unconditionally and never consults the new `Retryable` signal, so replaying a command whose drain reminder is still armed can publish the same committed range twice.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore/Controllers/ReplayController.cs:35-40
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] `ReplayController` accepts `PublishFailed` unconditionally and never consults the new `Retryable` signal, so replaying a command whose drain reminder is still armed can publish the same committed range twice. evidence: `src/Hexalith.EventStore/Controllers/ReplayController.cs:35-40` includes `PublishFailed` in `_replayableStatuses` and the gate at `:204` does not read `Retryable`. Story 4.4 adds the field that would make this decidable but does not wire it into the replay gate. Pre-existing double-publication risk; the new field makes a fix cheap.
status: open

### DW-236: [MEDIUM] No Tier-3 or live-sidecar proof exists for what is fundamentally a crash-window story; `OnActivateAsync` is reached only by reflection.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: tests/Hexalith.EventStore.Server.LiveSidecar.Tests
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] No Tier-3 or live-sidecar proof exists for what is fundamentally a crash-window story; `OnActivateAsync` is reached only by reflection. evidence: All activation coverage is against `Substitute.For<IActorStateManager>()`, and `PublicationRecoveryActivationTests.InvokeOnActivateAsync` resolves the hook via `GetMethod(..., Instance|NonPublic)`, which never proves DAPR invokes it, never exercises real reminder registration, and turns a rename into a runtime rather than compile failure. `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` and `tests/Hexalith.EventStore.IntegrationTests` both exist and received nothing. A real "kill between commit and reminder registration, restart, observe publication" proof belongs there. Tier-3 needs Docker/Aspire and is outside this spec's Verification list.
status: open

### DW-237: [MEDIUM] `docs/guides/configuration-reference.md` claims exponential backoff between the minimum and maximum drain periods, but drain reminders register a constant period.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: docs/guides/configuration-reference.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] `docs/guides/configuration-reference.md` claims exponential backoff between the minimum and maximum drain periods, but drain reminders register a constant period. evidence: `configuration-reference.md:92` documents exponential backoff, while `AggregateActor.GetDrainReminderSchedule` registers a fixed `DrainPeriod` and only clamps it against `MaxDrainPeriod`. Pre-existing drift from Story 4.2. It becomes more consequential with a bounded attempt count, since the wall-clock budget before permanent dead-lettering is then `MaxDrainAttempts * DrainPeriod` (~8 minutes on defaults) rather than a growing backoff window.
status: open

### DW-238: [LOW] `DrainReasonCodes` is `internal` but its values are now part of the public HTTP and wire contract, forcing consumers and tests to hard-code string literals.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: DrainReasonCodes
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [LOW] `DrainReasonCodes` is `internal` but its values are now part of the public HTTP and wire contract, forcing consumers and tests to hard-code string literals. evidence: `CommandStatusResponse.RecoveryReasonCode` is returned to external HTTP clients and `DeadLetterMessage.ReasonCode` travels on the dead-letter topic, yet the bounded vocabulary lives only in `internal static class DrainReasonCodes`. The new tests already hard-code `"drain_attempts_exhausted"` and `"drain_publish_failed"` as bare strings. Consider promoting the constants to `Hexalith.EventStore.Contracts` and adding a test pinning the bounded set.
status: open

### DW-239: [LOW] `DeadLetterEntry` carries no extensions field, so the admin surface cannot see the not-replay-eligible marker on an exhaustion dead-letter and its Retry action has no guard.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs:17
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [LOW] `DeadLetterEntry` carries no extensions field, so the admin surface cannot see the not-replay-eligible marker on an exhaustion dead-letter and its Retry action has no guard. evidence: `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs:17` has no extensions property, and `DaprDeadLetterCommandService.RetryDeadLettersAsync` plus the Admin UI Retry action do not consult it. An operator can therefore replay the reduced envelope (empty payload, synthetic user id) as though it were the original command.
status: open

### DW-240: [LOW] Actor test fixtures are duplicated across test files, so any `AggregateActor` constructor change now requires four synchronized edits.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: tests/Hexalith.EventStore.Server.Tests/TestUtilities
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [LOW] Actor test fixtures are duplicated across test files, so any `AggregateActor` constructor change now requires four synchronized edits. evidence: `CreateActorForBoundedDrain` is copy-pasted with a near-identical body and a parallel context record into both `EventDrainRecoveryTests` and `PublicationRecoveryActivationTests`, alongside the pre-existing `CreateActor`/`CreateActorWithTimerManager`. `AggregateActor` now takes ten constructor arguments. One shared builder in `tests/Hexalith.EventStore.Server.Tests/TestUtilities` would remove the drift risk.
status: open

### DW-241: [MEDIUM] The two `DeadLetterMessage` producers now disagree about the dead-letter contract, and the suite that encodes it only exercises one of them.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
archived: 2026-09-18

### DW-242: [LOW] `Retryable` is left null for every non-drain status, which collides with the documented meaning of null as "written before this field existed".

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: docs/operations/drain-failure-reason-codes.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [LOW] `Retryable` is left null for every non-drain status, which collides with the documented meaning of null as "written before this field existed". evidence: `CommandStatusRecord` and `docs/operations/drain-failure-reason-codes.md` define null as a legacy record predating the field, but `WriteAdvisoryStatusAsync` writes null for Received, Processing, happy-path Completed and tenant Rejected. A consumer cannot distinguish "legacy record" from "current record, retryability not applicable". `GetStatus_LegacyRecordWithoutRecoveryFields_KeepsRetryableNullRatherThanFalse` cannot observe the collision because its fixture is byte-identical to what the code writes today for a fresh non-drain status.
status: open

### DW-243: [LOW] Log EventId allocation across the partial `Log` classes is unguarded, so a duplicate EventId can be introduced silently.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
archived: 2026-09-18

### DW-244: Forensic note for orphan commits `f3e036bf0cae72b50508a3e729f24a052a7c4e95` / `026b039b237372774d998af8f5b77c58db00d348` that a July draft assumed were unpushed on local `main`. They are not tip ancestors; winning sibling `f6db558c768ae413712560019beab488d9974d66` (same subject/parent) is already on `origin/main`. Closed as obsolete without rebase or cherry-pick — replaying the orphans would regress resilience, fixtures, Story 1.20 status, and submodule pins. Preserve the SHAs if reflog GC later drops tip reachability.

origin: migrated from legacy ledger ("Deferred from: obsolete main-rebase conflict draft closure (2026-08-08)"), 2026-08-30
location: origin/main
reason: source_spec: `_bmad-output/implementation-artifacts/spec-resolve-main-rebase-conflicts.md` summary: Forensic note for orphan commits `f3e036bf0cae72b50508a3e729f24a052a7c4e95` / `026b039b237372774d998af8f5b77c58db00d348` that a July draft assumed were unpushed on local `main`. They are not tip ancestors; winning sibling `f6db558c768ae413712560019beab488d9974d66` (same subject/parent) is already on `origin/main`. Closed as obsolete without rebase or cherry-pick — replaying the orphans would regress resilience, fixtures, Story 1.20 status, and submodule pins. Preserve the SHAs if reflog GC later drops tip reachability. evidence: `git merge-base --is-ancestor f6db558c768ae413712560019beab488d9974d66 origin/main` succeeds; `main` and `origin/main` both at `37fdcd1fc8a238b676441b1f5a5ef5fd4370d27e`; orphans still exist as objects (`git cat-file -t f3e036bf0cae72b50508a3e729f24a052a7c4e95` / `026b039b237372774d998af8f5b77c58db00d348` → `commit`); tip gitlinks remain Builds `824d7ef100455423aabbcd399c8364074000b2e0`, Memories `da5df10092461e5473d0e8fc09eacbb4a8e08d3a`, Tenants `323baf8871e70be3fde92072f32b758af950bc8c`. status: forensic-only (closed obsolete; non-actionable)
status: open

### DW-245: `package-availability.json` embeds machine-local absolute search roots under `/home/administrator/...`, which are non-portable durable evidence.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-246: Epic 3 context rewrite thins earlier concrete cross-story constraints without an explicit supersession note.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-247: Expected AC4 acceptance scaffolding (`acceptances/{subject_sha256}` layout / receipt schema example) is narrative-only and not checked into hashed manifests.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-248: Support-safety hostname privacy only special-cases `.internal`/`.local`, so other private DNS names can bypass the literal-IP private check.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-249: Retained `smoke-results.json` can declare top-level `"result": "pass"` while runtime-verification/crosswalk mark execution unverified/fail.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-250: Crosswalk `approval_contract.required_receipt_fields` omits `schema` while the verifier’s `RequiredReceiptFields` requires it.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-251: Review-subject blocker text still claims smoke logs lack cleanup facts after cleanup=pass appears in retained logs/runtime-verification.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-252: Governed inline CI checkouts (`semantic-release-governance`, `tenants-source-mode`) set `persist-credentials: false`, but Contracts helpers never assert it and valid fixtures omit it.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-253: `Non_manifest_src_projects_cannot_produce_release_packages` only substring-matches `<IsPackable>false</IsPackable>` instead of evaluating MSBuild packability like the manifest-side check.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: <IsPackable>false</IsPackable>
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md` summary: `Non_manifest_src_projects_cannot_produce_release_packages` only substring-matches `<IsPackable>false</IsPackable>` instead of evaluating MSBuild packability like the manifest-side check. evidence: Verification-gap review — conditional or later overriding true values can evade the complement gate; unrelated packaging growth since the story baseline.
status: open

### DW-254: Release `verify-source` job body and fail-closed release inputs (`expected-package-count`, `timeout-minutes`) lack Contracts assertions comparable to the CI job-scoped guards.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-255: CommitMessagePolicy markdown helpers can throw on malformed percent-encoding (`Uri.UnescapeDataString`) and can treat tab-indented fences as operative preflight blocks.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: Uri.UnescapeDataString
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md` summary: CommitMessagePolicy markdown helpers can throw on malformed percent-encoding (`Uri.UnescapeDataString`) and can treat tab-indented fences as operative preflight blocks. evidence: Edge-case review of CommitMessagePolicyTests helper growth after baseline; adjacent to Copilot delegation but not required by frozen intent.
status: open

### DW-256: Redact absolute local_search_roots from retained package-availability.json (and refresh checksums/bindings) so support-safe evidence does not embed host filesystem paths.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-257: Add missing-key removal mutations for validators whose NullReferenceException catch filters are only exercised by value mutations today.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Add missing-key removal mutations for validators whose NullReferenceException catch filters are only exercised by value mutations today. evidence: Blind-hunter review of Story 3.13; prior hardening added catch filters but theories still mutate values rather than removing keys.
status: open

### DW-258: Fixture imports `undici` as a top-level module without a direct package.json dependency, so hoisting/layout changes could bind a different major than the plugin expects.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: package.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-29763400936-fix-release-post-publish-status.md` summary: Fixture imports `undici` as a top-level module without a direct package.json dependency, so hoisting/layout changes could bind a different major than the plugin expects. evidence: Blind-hunter review — lockfile already shows multiple undici majors; fixture only asserts resolve-path equality with the plugin.
status: open

### DW-259: Semantic-release governance job structural contracts do not pin omitted write permissions or forbid token env overrides on the Node fixture steps.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-29763400936-fix-release-post-publish-status.md` summary: Semantic-release governance job structural contracts do not pin omitted write permissions or forbid token env overrides on the Node fixture steps. evidence: Edge-case hunter review — job could gain contents/pull-requests write without failing AssertSemanticReleaseGovernanceJobIsBlocking; outside frozen success-notification scope.
status: open

### DW-260: Scrub remaining predecessor SDK patch tokens from Hexalith.FrontComposer tracked BMAD review `.diff` artifacts.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: _bmad-output/implementation-artifacts/.11-17d-group{3,4}-review.diff
reason: source_spec: `_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md` summary: Scrub remaining predecessor SDK patch tokens from Hexalith.FrontComposer tracked BMAD review `.diff` artifacts. evidence: Split from the SDK 10.0.302 cleanup so the root EventStore leftover pass can ship alone; FrontComposer still has four predecessor SDK patch-token hits in `_bmad-output/implementation-artifacts/.11-17d-group{3,4}-review.diff` under the same 1A/2B/3A-strict policy.
status: open

### DW-261: Scrub remaining predecessor SDK patch tokens from Hexalith.Memories BMAD artifacts, including the below-min I/O matrix rewrite to a non-predecessor.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: spec-run-tests-and-fix-failures.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md` summary: Scrub remaining predecessor SDK patch tokens from Hexalith.Memories BMAD artifacts, including the below-min I/O matrix rewrite to a non-predecessor. evidence: Split from the SDK 10.0.302 cleanup so the root EventStore leftover pass can ship alone; Memories still has four hits in `spec-run-tests-and-fix-failures.md` and `27-1-access-telemetry-retention-ownership-decision.md` under the same 1A/2B/3A-strict policy.
status: open

### DW-262: Dev Agent Debug Log still cites stale focused/suite test totals that no longer match the 140-test verifier count.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-263: Several Task parent checkboxes remain unchecked while child boxes and later tasks are marked complete.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-264: Story 4.5 LiveSidecar ownership prose was added in docs/ci.md within the same baseline range as Story 3.13's ownership rewrite.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-265: Add an `acceptances/{subject_sha256}/` scaffold or receipt template beside the roster before AC4 collection.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-266: Re-measure the full Contracts.Tests suite after the ninth hardening pass and refresh Dev Agent / proof-packet totals if they drift.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-267: Reviewer roster maps both eventstore-owner and release-owner to the same github:jpiquot identity.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-268: Same working tree advances Epic 4 tracker rows and Story 4.5 LiveSidecar docs/ci prose beside Story 3.13.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-269: Retained fail-closed runtime-verification.json remains schema v1 without pass-path v2 command/smoke_results shape.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-270: release_authority.verification reports fail under a hash-check method without separating scope failure.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: release_authority.verification reports fail under a hash-check method without separating scope failure. evidence: The crosswalk marks result fail while the method text says hash-checked durable predecessor authority, even though the concrete blocker is deployment_authorized false / quarantine-only scope rather than a failed hash.
status: open

### DW-271: Story 4.4 activation recovery can permanently starve publication-index entries beyond the fixed head scan and work budgets.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-272: Story 4.4 can report `Retryable=true` when only a recovery-index entry exists and reminder registration failed.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 can report `Retryable=true` when only a recovery-index entry exists and reminder registration failed. evidence: The advisory status uses `drainReminderArmed || recoveryEntryTracked`, but an index entry does not itself activate an idle actor or guarantee an automatic retry.
status: open

### DW-273: Newly written normal command statuses leave `Retryable` null even though the public contract reserves null for legacy records.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: drain-failure-reason-codes.md
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Newly written normal command statuses leave `Retryable` null even though the public contract reserves null for legacy records. evidence: Most `WriteAdvisoryStatusAsync` call sites omit the new recovery parameters while `CommandStatusRecord` and `drain-failure-reason-codes.md` define null as a pre-field compatibility state.
status: open

### DW-274: A successful Story 4.4 drain reports one fewer attempt than was actually executed.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: A successful Story 4.4 drain reports one fewer attempt than was actually executed. evidence: The success status writes `DrainAttemptCount: record.RetryCount`; the current successful reminder attempt is not included.
status: open

### DW-275: Story 4.4 defers exhaustion dead-lettering until the reminder after the retry count reaches its configured cap.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 defers exhaustion dead-lettering until the reminder after the retry count reaches its configured cap. evidence: Failure handling persists the capped count and returns; `CompleteDrainExhaustionAsync` runs only at the start of the next reminder, leaving terminal work and capacity retained for another interval.
status: open

### DW-276: Negative or overflowing persisted drain retry counts can evade or break the bounded-attempt guarantee.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Negative or overflowing persisted drain retry counts can evade or break the bounded-attempt guarantee. evidence: `UnpublishedEventsRecord.IncrementRetry` performs unchecked addition and the reminder path validates only `RetryCount >= MaxDrainAttempts`.
status: open

### DW-277: A crash after dead-letter broker acceptance but before the `DeadLettered` state save can publish the same exhausted range twice.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: DeadLettered
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: A crash after dead-letter broker acceptance but before the `DeadLettered` state save can publish the same exhausted range twice. evidence: The dead-letter sink and actor-state update are not atomic or idempotently coupled, despite Story 4.4's claim that the ordering prevents duplicate dead letters.
status: open

### DW-278: Persisted duplicate publication-index entries survive normalization and can leave stale capacity behind.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-279: Story 4.4's trailing optional parameters on public positional records are binary-breaking for already compiled consumers.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4's trailing optional parameters on public positional records are binary-breaking for already compiled consumers. evidence: The changes replace prior constructor and `Deconstruct` signatures on `CommandStatusRecord`, `CommandStatusResponse`, `DeadLetterMessage`, and `UnpublishedEventsRecord` without forwarding compatibility members.
status: open

### DW-280: The Story 4.5 evidence validator hashes current worktree files instead of the source blobs captured by its baseline commit.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: workspace / relative
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The Story 4.5 evidence validator hashes current worktree files instead of the source blobs captured by its baseline commit. evidence: `validate_source_binding` reads `workspace / relative`, so ordinary later edits make the committed supposedly re-runnable evidence package fail independently of the captured revision.
status: open

### DW-281: BMAD project-context sync can write `AGENTS.md` outside the selected project through a compass area path.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-282: BMAD project-context sync can duplicate or remove user-authored text when managed markers are missing, reversed, or duplicated.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-283: BMAD project-context sync does not re-anchor relative Markdown links that include fragments or query strings.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-284: The installed BMAD project-context implementation lacks regression coverage for its filesystem-writing and resolution paths.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-285: Story 4.4 tests do not prove the publication-recovery index is staged before the event commit batch.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 tests do not prove the publication-recovery index is staged before the event commit batch. evidence: The current test verifies only that `SetStateAsync("publication-index", ...)` occurred; moving it after the first `SaveStateAsync` would preserve the assertion while reopening the crash window.
status: open

### DW-286: Story 4.4 recovery fields are not verified through the hosted command-status HTTP wire contract.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 recovery fields are not verified through the hosted command-status HTTP wire contract. evidence: Tests inspect `OkObjectResult.Value` or persistence JSON but do not assert camel-case `retryable`, `recoveryReasonCode`, and `drainAttemptCount` properties in an actual endpoint response.
status: open

### DW-287: Story 4.4 drain-exhaustion safety fields are not verified after production dead-letter serialization.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 drain-exhaustion safety fields are not verified after production dead-letter serialization. evidence: Tests inspect typed records and mocked publish calls but do not prove the wire payload carries the non-replayable flag, reason, committed range, and attempt count.
status: open

### DW-288: The BMAD renderer's documented empty `customization.workflow.open_spec` override has no regression test.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: customization.workflow.open_spec
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The BMAD renderer's documented empty `customization.workflow.open_spec` override has no regression test. evidence: Restoring the prior empty-value rejection would prevent `bmad-build` activation without failing any discovered renderer test.
status: open

### DW-289: BMAD sprint planning permits the same numeric epic-story identity to produce multiple rows when titles differ.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: BMAD sprint planning permits the same numeric epic-story identity to produce multiple rows when titles differ. evidence: Duplicate detection compares the full title-derived key instead of the `(epic_num, story_num)` identity.
status: open

### DW-290: BMAD sprint planning can crash outside its structured error contract on a malformed `development_status` value.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: BMAD sprint planning can crash outside its structured error contract on a malformed `development_status` value. evidence: `build_status` converts any truthy value with `dict(...)` without first requiring a mapping.
status: open

### DW-291: The Story 4.5 durability-race classifier does not reject contradictory actor acceptance and rejection/conflict signals.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The Story 4.5 durability-race classifier does not reject contradictory actor acceptance and rejection/conflict signals. evidence: Simultaneous `ActorAccepted` and `ActorRejected` or `ActorConflictSignalled` values can flow into a nominal classification as internally consistent.
status: open

### DW-292: The Story 4.5 evidence validator can report success when Python assertions are disabled.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-293: The Story 4.5 evidence validator accepts truthy non-boolean invariant values.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-294: The Story 4.5 source-binding validator does not fail when an evidence-relevant source path is omitted.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-295: Live-lane packaging guardrail still matches only the exact `dotnet test tests/Hexalith.EventStore.Server.Tests/` substring and does not cover `.csproj`, unquoted alternate path, or `--project` equivalents.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: dotnet test tests/Hexalith.EventStore.Server.Tests/
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-31400593510-fix-ci-cd.md` summary: Live-lane packaging guardrail still matches only the exact `dotnet test tests/Hexalith.EventStore.Server.Tests/` substring and does not cover `.csproj`, unquoted alternate path, or `--project` equivalents. evidence: Blind-hunter and edge-case review of the CI fix noted realistic alternate invocation forms that would evade the exact-substring forbid while still running Server.Tests as a suite; hardening was deferred to keep this hotfix scoped to the failing CI assertions and Design Notes golden shapes.
status: open

### DW-296: Builds `dapr-init` still uses one shared version for CLI install and runtime init, so EventStore cannot pin CLI 1.18.0 with runtime 1.18.1 without a submodule change.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-297: The concurrent integration workflow's Dapr 1.18.0 runtime pin cannot reproduce the OQ8 packet's validator-pinned Dapr 1.18.1 fresh capture.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
archived: 2026-09-18

### DW-298: The concurrent live-lane guardrail can miss equivalent full Server.Tests invocations.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: The concurrent live-lane guardrail can miss equivalent full Server.Tests invocations. evidence: Exact substring and selector-presence checks do not reject `.csproj`, `--project`, normalized/quoted paths, or a second unfiltered direct xUnit assembly invocation; robust command-level parsing belongs to the CI guardrail follow-up.
status: open

### DW-299: The concurrent Dapr-version guard can miss job-level or differently quoted YAML overrides.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: The concurrent Dapr-version guard can miss job-level or differently quoted YAML overrides. evidence: Its regex inspects only single-quoted definitions and checks the environment-variable reference separately from the initialization step, so an effective override can evade the assertion; structural YAML validation is required.
status: open

### DW-300: Execute the shared `dapr-init` legacy runtime fallback with a fake Dapr executable at action level.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-31415412092-fix-ci-cd.md` summary: Execute the shared `dapr-init` legacy runtime fallback with a fake Dapr executable at action level. evidence: Structural guards prove the fallback expression, but an action-level harness would additionally prove the resolved legacy value reaches the quoted `dapr init --runtime-version` invocation.
status: open

### DW-301: Make the selector-based Git subprocess harness portable to Windows.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Make the selector-based Git subprocess harness portable to Windows. evidence: Current bounded nonblocking pipe handling uses POSIX selector behavior and is exercised only on non-Windows hosts.
status: open

### DW-302: Add one overall deadline spanning all Git identity subprocesses.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Add one overall deadline spanning all Git identity subprocesses. evidence: Each Git call is bounded independently, but many sequential calls can exceed an operator's intended total validation deadline.
status: open

### DW-303: Bound every validator input file before parsing or hashing it.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Bound every validator input file before parsing or hashing it. evidence: Git output is bounded, but evidence, document, and JSON input sizes are not governed by one fail-closed limit.
status: open

### DW-304: Write sanitized fresh-capture outputs atomically.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-305: Reject symlinks throughout fresh capture inputs and output directories.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Reject symlinks throughout fresh capture inputs and output directories. evidence: Final closure artifacts reject symlinks, while the fresh capture path and raw CTRF inputs do not apply the same policy.
status: open

### DW-306: Add cryptographic reviewer identity and attestation to Story 4.15 receipts.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Add cryptographic reviewer identity and attestation to Story 4.15 receipts. evidence: Receipts are content-bound by hash and exact reviewer text but do not authenticate who produced the approval.
status: open

### DW-307: Retain bounded raw execution logs or equivalent replayable command evidence for pre-review commands.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Retain bounded raw execution logs or equivalent replayable command evidence for pre-review commands. evidence: The execution record preserves command identities and counts but not the underlying output needed to independently audit each reported result.
status: open

### DW-308: Bind the closure-assembly commit identity after the Story 4.15 artifacts land.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-309: [HIGH] Dead-letter republish if mark-save fails after broker accept (reconfirmed, group-1 review).

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-4-committed-event-publication-recovery (2026-08-11)"), 2026-08-30
location: AggregateActor.cs:1674-1709
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [HIGH] Dead-letter republish if mark-save fails after broker accept (reconfirmed, group-1 review). evidence: `CompleteDrainExhaustionAsync` publishes then `MarkDeadLettered` + `SaveStateAsync`; if save fails after broker acceptance, the next exhaustion turn publishes again. Already on ledger from prior 4.4 / mislabeled 3.13 entries; reconfirmed against `AggregateActor.cs:1674-1709`.
status: open

### DW-310: [MEDIUM] `Normalize` does not dedupe duplicate MessageIds (reconfirmed, group-1 review).

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-4-committed-event-publication-recovery (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-311: [MEDIUM] Commit-batch index staging order is not asserted by tests (reconfirmed, group-1 review).

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-4-committed-event-publication-recovery (2026-08-11)"), 2026-08-30
location: AggregateActor.cs:688-727
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] Commit-batch index staging order is not asserted by tests (reconfirmed, group-1 review). evidence: Existing commit-batch test asserts `SetStateAsync(publication-index)` occurred but not before the first commit `SaveStateAsync`. Already on ledger; reconfirmed against `AggregateActor.cs:688-727`.
status: open

### DW-312: ResolveWithin uses Ordinal StartsWith on Path.GetFullPath roots — case-insensitive hosts can theoretically mismatch path identity; Linux CI primary path is Ordinal-correct.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:5255
reason: ResolveWithin uses Ordinal StartsWith on Path.GetFullPath roots — case-insensitive hosts can theoretically mismatch path identity; Linux CI primary path is Ordinal-correct. [`DeployedRuntimeParityClosureTests.cs:5255`]
status: open

### DW-313: FieldNameIsSupportSafe fragment matching can false-positive legitimate names (e.g. tokenizer) — no colliding fields in the Story 3.13 evidence schema today.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4134
reason: FieldNameIsSupportSafe fragment matching can false-positive legitimate names (e.g. tokenizer) — no colliding fields in the Story 3.13 evidence schema today. [`DeployedRuntimeParityClosureTests.cs:4134`]
status: open

### DW-314: LimitationsContainMutationProhibitions accepts weak keyword substrings — unrelated prose containing package/registry can pass.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4282
reason: LimitationsContainMutationProhibitions accepts weak keyword substrings — unrelated prose containing package/registry can pass. [`DeployedRuntimeParityClosureTests.cs:4282`]
status: open

### DW-315: ResolveWithin TOCTOU between RejectReparsePoint and later file open — theoretical race after reparse checks.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:5260
reason: ResolveWithin TOCTOU between RejectReparsePoint and later file open — theoretical race after reparse checks. [`DeployedRuntimeParityClosureTests.cs:5260`]
status: open

### DW-316: RunGit/ComputePinnedBuildsToolSha256 sync-over-async via GetAwaiter().GetResult() — test-helper style; not on product await paths.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:5334
reason: RunGit/ComputePinnedBuildsToolSha256 sync-over-async via GetAwaiter().GetResult() — test-helper style; not on product await paths. [`DeployedRuntimeParityClosureTests.cs:5334`]
status: open

### DW-317: ValueIsSupportSafe misses private IPs embedded in non-URI free text — retained evidence is primarily structured JSON/URI values.

origin: migrated from legacy ledger ("Deferred from: code review of 3-13-deployed-runtime-parity-closure.md (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4155
reason: ValueIsSupportSafe misses private IPs embedded in non-URI free text — retained evidence is primarily structured JSON/URI values. [`DeployedRuntimeParityClosureTests.cs:4155`]
status: open

### DW-318: Sealed evidence packet no longer validates at HEAD — validate_source_binding hashes worktree files, and docs/ci.md plus docs/concepts/architecture-overview.md drifted via later commits; exits 1 with AssertionError: docs/ci.md, while all 17 rows were OK at 2321205b. Duplicates and confirms the pre-existing entry above.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../validate-evidence.py:178
reason: Sealed evidence packet no longer validates at HEAD — validate_source_binding hashes worktree files, and docs/ci.md plus docs/concepts/architecture-overview.md drifted via later commits; exits 1 with AssertionError: docs/ci.md, while all 17 rows were OK at 2321205b. Duplicates and confirms the pre-existing entry above. [`evidence/story-4-5/0776785f.../validate-evidence.py:178`]
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-318, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-319: No test or CI step ever executes validate-evidence.py, unlike every sibling evidence directory which is pinned by a blocking Contracts.Tests/Packaging fact; the hash binding can decouple with all required checks green. Blocked on the item above.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../validate-evidence.py:203
reason: No test or CI step ever executes validate-evidence.py, unlike every sibling evidence directory which is pinned by a blocking Contracts.Tests/Packaging fact; the hash binding can decouple with all required checks green. Blocked on the item above. [`evidence/story-4-5/0776785f.../validate-evidence.py:203`]
status: open

### DW-320: No binding between a committed capture and the receipt of the run that produced it — append-durability-race.json armedAtUtc falls inside the post-mutation window, not the race-test-results.json window; disclosed in prose but not machine-checked.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-321: retryCount derives from unfiltered AllocationAttempts while AppendDurabilityRaceControl is a singleton registered into both the primary and replica hosts; mitigated by serial collection execution and disclosed in allocatorIdentityLimitation.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: AppendDurabilityRaceSession.cs:148
reason: retryCount derives from unfiltered AllocationAttempts while AppendDurabilityRaceControl is a singleton registered into both the primary and replica hosts; mitigated by serial collection execution and disclosed in allocatorIdentityLimitation. [`AppendDurabilityRaceSession.cs:148`]
status: open

### DW-322: MetadataKey_StaleEtagUpdate_IsRejected no longer touches a metadata key; renaming would break commands.md, the validate-evidence.py MUTATIONS map, and the committed receipts.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: ActorConcurrencyConflictTests.cs:130
reason: MetadataKey_StaleEtagUpdate_IsRejected no longer touches a metadata key; renaming would break commands.md, the validate-evidence.py MUTATIONS map, and the committed receipts. [`ActorConcurrencyConflictTests.cs:130`]
status: open

### DW-323: Redaction gate uses `! rg …`, so an rg failure (exit 2) inverts to success and reports clean having scanned nothing.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-324: commands.md leaks errexit from the mutation wrapper, uses `exit 2` in the canonical-overwrite guard (closes an interactive shell), and runs the redact/hash block on unguarded variables.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-325: concurrency-conflict.md Common Causes bullet 1 describes an optimistic-transaction rejection that cannot arise on the current actor commit path, since nothing supplies an etag there; page is otherwise correctly hedged.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: docs/reference/problems/concurrency-conflict.md:14
reason: concurrency-conflict.md Common Causes bullet 1 describes an optimistic-transaction rejection that cannot arise on the current actor commit path, since nothing supplies an etag there; page is otherwise correctly hedged. [`docs/reference/problems/concurrency-conflict.md:14`]
status: open

### DW-326: The ADD-fencing decision recorded as Deferred has no tracked owner or trigger — no append-fencing story exists in epics.md or sprint-status.yaml.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: _bmad-output/planning-artifacts/architecture.md:558
reason: The ADD-fencing decision recorded as Deferred has no tracked owner or trigger — no append-fencing story exists in epics.md or sprint-status.yaml. [`_bmad-output/planning-artifacts/architecture.md:558`]
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-326, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-327: Story 4.5 provider profile is a source literal validated against itself (daprRuntime "1.18.1", redisImage "redis:6") and the two deterministic classes lack Collection/Trait attributes — deferred to the approved append-fencing follow-up, which must re-capture across multiple provider profiles anyway, so fixing runtime attribution and test placement is cheapest as part of that multi-profile capture.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-328: Story 4.5 evidence packet left partially updated by the 2026-08-11 review: harness and validator patched (D1/D2/D4/D5) but the live re-capture could not run — DaprTestContainerFixture probes localhost:50005/50006 while Dapr CLI 1.18 publishes placement/scheduler on 6050/6060, and the local control plane is 1.18.2 against a packet claiming 1.18.1. validate-evidence.py fails until a fresh capture regenerates the receipts, source-state.md, and evidence-sha256.txt.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-329: Classifier completeness lives only in a docstring — "covers all twenty reachable classification names" is true today (verified: 20 distinct names, 22-row table covers all 20), but adding a 21st branch would fail no test.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: AppendDurabilityRaceClassifierTests.cs:44
reason: Classifier completeness lives only in a docstring — "covers all twenty reachable classification names" is true today (verified: 20 distinct names, 22-row table covers all 20), but adding a 21st branch would fail no test. [`AppendDurabilityRaceClassifierTests.cs:44`]
status: open

### DW-330: The dead-arm removal substitutes the literal `true` for the `rawConflictRejected` variable in the `RecognizedRejectionOrConflict` position; correct under the current infrastructure gate, but widening that gate would silently report a new status as a recognized conflict rejection, and no table row can cover the unreachable input.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: AppendDurabilityRaceClassifier.cs:151
reason: The dead-arm removal substitutes the literal `true` for the `rawConflictRejected` variable in the `RecognizedRejectionOrConflict` position; correct under the current infrastructure gate, but widening that gate would silently report a new status as a recognized conflict rejection, and no table row can cover the unreachable input. [`AppendDurabilityRaceClassifier.cs:151`]
status: open

### DW-331: No schema history documents the `append-durability-race.json` 2 to 3 or `generic-etag-control.json` 1 to 2 version bumps that `validate-evidence.py` now hard-asserts; a future reader cannot distinguish a schema-3 capture from a schema-2 one without diffing the test source.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: append-durability-race.json
reason: No schema history documents the `append-durability-race.json` 2 to 3 or `generic-etag-control.json` 1 to 2 version bumps that `validate-evidence.py` now hard-asserts; a future reader cannot distinguish a schema-3 capture from a schema-2 one without diffing the test source. [`validate-evidence.py:92`]
status: open

### DW-332: `generic-probe-not-attempted` encodes harness state as a provider observation and can appear in a genuine non-mutation capture when `gateWaitException` short-circuits the probe block, not only when the key-addressability perturbation is armed.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: AppendDurabilityRaceLiveSidecarTests.cs:320
reason: `generic-probe-not-attempted` encodes harness state as a provider observation and can appear in a genuine non-mutation capture when `gateWaitException` short-circuits the probe block, not only when the key-addressability perturbation is armed. [`AppendDurabilityRaceLiveSidecarTests.cs:320`]
status: open

### DW-333: Add an AppHost model test for the two new drain-bound environment forwards.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Add an AppHost model test for the two new drain-bound environment forwards. evidence: Direct options-binding tests cover `MaxDrainAttempts` and `MaxOutstandingPublicationEntries`, but no normally run test proves the AppHost forwards either parent value to the `eventstore` resource.
status: open

### DW-334: Restrict append-durability race conflict recognition to known concurrency exception identities.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Restrict append-durability race conflict recognition to known concurrency exception identities. evidence: `AppendDurabilityRaceLiveSidecarTests` currently treats every `InvalidOperationException` as a recognized concurrency conflict, so unrelated infrastructure failures can satisfy the evidence gate.
status: open

### DW-335: Reject contradictory sequence-two durability classifications when either writer reported rejection.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Reject contradictory sequence-two durability classifications when either writer reported rejection. evidence: `AppendDurabilityRaceClassifier` classifies both surviving writes plus one retry as consistent without checking the raw response or actor rejection flags.
status: open

### DW-336: Validate provider package signature evidence and canonical NuGet.org URLs.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Validate provider package signature evidence and canonical NuGet.org URLs. evidence: `RuntimeIdentityValidator.ValidatePackageManifest` requires the signature field and `nuget_url` property names but does not validate their values, allowing arbitrary signature objects or off-domain package URLs.
status: open

### DW-337: Fetch enough Git history for OQ8 integration evidence validation.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence loop 2 (2026-08-11)"), 2026-08-30
location: github/workflows/integration.yml
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Fetch enough Git history for OQ8 integration evidence validation. evidence: `.github/workflows/integration.yml` checks out with `fetch-depth: 1`, while `validate-oq8-platform-evidence.py` requires the older landed source object `4b0a7b1d3628a857f131cfbff99030714aefc747` for tree, ancestry, and file checks.
status: open

### DW-338: Validate OCI layer descriptors in the retained image graph.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Validate OCI layer descriptors in the retained image graph. evidence: The retained `child-linux-*.manifest.raw` files carry seven layer descriptors each whose digests and sizes are never checked; `"layers"` appears in `DeployedRuntimeParityClosureTests.cs` only as `new JsonArray()` in synthetic fixtures, so the pass path validates layer-less manifests no registry would accept.
status: open

### DW-339: Exercise the release-provenance and deployment-authority validators against a real artifact.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-340: Publish the structured runtime-log schema outside the Story 3.13 test file.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Publish the structured runtime-log schema outside the Story 3.13 test file. evidence: Retained smoke logs are line-oriented text (`platform=`, `container_state=running|0`, `attempts=18`) while the pass-path validators parse JSON objects with `child_digest`, `readiness_result`, and `failure_class`. Reopen trigger 5 asks the Hexalith.Builds smoke-contract owner to emit records against a schema specified nowhere outside `DeployedRuntimeParityClosureTests.cs`.
status: open

### DW-341: Anchor and scaffold the AC4 acceptance-receipt location.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-342: Bind the outer evidence manifest's own bytes to a hash.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-343: Disclose concurrent Epic 4 and docs changes carried inside the Story 3.13 review range.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-344: Handle child-process termination failure and cover the git wait timeout.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Handle child-process termination failure and cover the git wait timeout. evidence: `WaitForProcessExit` orphans a child when both the kill and the 5-second post-kill wait fail, and no test drives a git invocation past the 30-second window, so neither the previous nor the hardened timeout behavior is observed by the suite.
status: open

### DW-345: Refresh retained evidence `checked_at` timestamps after byte rewrites.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-346: HIGH - Story 1.21 must repair Epic 1 frozen evidence corrupted by the SDK-token sweep in `089369bb`, under its own authority record.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-347: Reseal or revert Story 4.5's self-invalidating evidence packet.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
archived: 2026-09-18

### DW-348: Exercise every provider-state response through the normal provider-verification lane.

origin: migrated from legacy ledger ("Deferred from: code review of spec-frontcomposer-11-24-runtime-identity-successor (2026-08-12)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md` summary: Exercise every provider-state response through the normal provider-verification lane. evidence: The registry test invokes most of the 19 state seams but discards their results, while the sole real-Kestrel Pact test covers only `command-unauthorized`; incorrect HTTP-visible results for the remaining states can therefore remain green.
status: open

### DW-349: Add an intentionally mismatching Pact test for contract-failure classification and process exit.

origin: migrated from legacy ledger ("Deferred from: code review of spec-frontcomposer-11-24-runtime-identity-successor (2026-08-12)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md` summary: Add an intentionally mismatching Pact test for contract-failure classification and process exit. evidence: The only executable `PactInteractionVerifier.VerifyAsync` test covers a matching Pact; no test proves native exit code `1` becomes `interaction.contract-failed`, a failed final verdict, and process exit code `4`.
status: open

### DW-350: Verify Pact playback continues after runtime-identity drift when host startup succeeds.

origin: migrated from legacy ledger ("Deferred from: code review of spec-frontcomposer-11-24-runtime-identity-successor (2026-08-12)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md` summary: Verify Pact playback continues after runtime-identity drift when host startup succeeds. evidence: Identity tests cover validator flags and the 19-input application test injects startup failure, so no executable test proves a mismatched identity still runs all 19 interactions and retains the identity failure verdict.
status: open

### DW-351: Add an AppHost model test for the drain-bound environment forwarding contract.

origin: migrated from legacy ledger ("Deferred from: code review of spec-frontcomposer-11-24-runtime-identity-successor (2026-08-12)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-frontcomposer-11-24-runtime-identity-successor.md` summary: Add an AppHost model test for the drain-bound environment forwarding contract. evidence: Direct option-binding tests cover `MaxDrainAttempts` and `MaxOutstandingPublicationEntries`, but no normal test proves AppHost forwards either value to the EventStore resource environment.
status: open

### DW-352: Pin the restored sprint-status decision comments with a guard.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-13)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Pin the restored sprint-status decision comments with a guard. evidence: 116 comment lines across 24 keys were restored from baseline `1d6e9321`, but only the three Story 1.20 lines are protected by a test. The restoring finding's own text warns "the next YAML round-trip will delete the rest again". The restoration is correct; the guard gap predates this chunk.
status: open

### DW-353: Record a disposition for every checked chunk-2 patch bullet.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-13)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Record a disposition for every checked chunk-2 patch bullet. evidence: Eighteen of the twenty-five `[Review][Patch]` bullets in the chunk-2 block are checked `[x]` while still reading as the raw finding; only seven carry an "APPLIED 2026-08-12" note, so the record does not say what changed for the rest.
status: open

### DW-354: Make `RebindIndex` fail loudly instead of throwing from `Directory.Move`.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-13)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:6441
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Make `RebindIndex` fail loudly instead of throwing from `Directory.Move`. evidence: `DeployedRuntimeParityClosureTests.cs:6441` moves the evidence directory to a digest-named path; when a mutation leaves the index bytes unchanged, or the `manifests` array is empty, the move raises `IOException` rather than exercising the rejection path the case was written for.
status: open

### DW-355: Share one `archive_root` separator normalizer between the package validators.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-13)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:2771
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Share one `archive_root` separator normalizer between the package validators. evidence: `ValidatePackageBytes` (`DeployedRuntimeParityClosureTests.cs:2771`) and `ExpectedCoreFilesFor` normalize `archive_root` independently, so repeated or platform-alternate trailing separators can make the two validators disagree on the same recovered 14-archive set.
status: open

### DW-356: Replace the five Windows early-return vacuous passes in the container-publishing governance suite with real skips.

status: done 2026-09-05
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
archived: 2026-09-18

### DW-357: Clean up the release-evidence codec hygiene cluster.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Clean up the release-evidence codec hygiene cluster. evidence: `_nuspec_identity(package_bytes)` is called with a `Path` and immediately does `zipfile.ZipFile(Path(package_bytes))`; `_parse_timestamp` uses `value.replace("Z", "+00:00")`, replacing every `Z` rather than a trailing designator; `validate_identity:441` compares the index digest to `children[0]` only, never to `children[1]` nor the two children to each other; `EXPECTED_PACKAGE_COUNT = 14` is a fourth uncross-checked copy of the package count; and `validate_packet_files` re-hashes each Builds helper immediately after `_verify_bound_file` performed the identical check. All are gated by the codec re-freeze decision.
status: open

### DW-358: Give `observations.json` semantic validation instead of checksum-only coverage.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: observations.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Give `observations.json` semantic validation instead of checksum-only coverage. evidence: The codec never opens `observations.json`; it is bound only through `packet-sha256.txt`, which is regenerated whenever the packet is rebuilt. The GitHub Release asset list and the "all 14 visible on NuGet.org" claim therefore rest on an unvalidated file. Cross-checked by hand during this review: all 14 `github_release.assets` digests and sizes do match `packages[].sha256`/`size`, so the claim is factually true today.
status: open
decision: 2026-09-06 Keep deferred
decision: 2026-09-06 Keep deferred

### DW-359: Decide whether the OCI image index should carry provenance annotations.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
archived: 2026-09-18

### DW-360: Cross-check the three JSON canonicalisers against one shared fixture.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: release_evidence_codec.py:69
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Cross-check the three JSON canonicalisers against one shared fixture. evidence: `canonical_bytes` (`release_evidence_codec.py:69`, compact, `ensure_ascii=False`), `_publisher_canonical_bytes` (`:489`, `indent=2`, default `ensure_ascii=True`) and the C# `CanonicalJsonBytes` (`CorrectiveOciProvenanceReleaseTests.cs:1011`, `Utf8JsonWriter` default `JavaScriptEncoder`) can diverge on non-ASCII and HTML-sensitive characters. The tests work around this by re-canonicalising `release-identity.json` through Python only; no test asserts the three encoders agree byte-for-byte.
status: open
decision: 2026-09-06 Keep packet stable
decision: 2026-09-06 Keep packet stable

### DW-361: Bound nuspec parsing against oversized archives and entity expansion.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: release_evidence_codec.py:466
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Bound nuspec parsing against oversized archives and entity expansion. evidence: `_nuspec_identity` (`release_evidence_codec.py:466`) calls `element_tree.fromstring` on a nuspec read straight out of a retained `.nupkg` with no size cap and no entity-expansion defence. The packet bytes are repository-controlled today, so this is hardening rather than a live exposure.
status: open
decision: 2026-09-06 Keep deferred
decision: 2026-09-06 Keep deferred

### DW-362: Prove the issue-comment snapshot is complete before asserting "exactly one authority and one receipt".

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: release_evidence_codec.py:770-790
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Prove the issue-comment snapshot is complete before asserting "exactly one authority and one receipt". evidence: `release_evidence_codec.py:770-790` validates ordering, uniqueness and issue affinity of the retained snapshot but has no total-count or last-page marker, so a truncated or paginated snapshot can satisfy the exactly-one authority and exactly-one receipt claims on incomplete data.
status: open
decision: 2026-09-06 Keep current schema
decision: 2026-09-06 Keep current schema

### DW-363: Decouple the authority-window theory from the frozen timestamps and split the seven-scenario mutation Fact.

status: done 2026-09-05
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
archived: 2026-09-18

### DW-364: Split the governed release path into its own reusable workflow file so legacy callers stop having to grant `attestations: write` and `id-token: write`.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, D5 disposition)"), 2026-08-30
archived: 2026-09-18

### DW-365: Unify Roslynator package families under a single `roslynator` family in Hexalith.Builds central package audit.

origin: migrated from legacy ledger ("Deferred from: CI/CD xUnit v3 restore failure fix (2026-08-21)"), 2026-08-30
location: audit-central-package-versions.ps1
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-32485211318-fix-ci-cd.md` summary: Unify Roslynator package families under a single `roslynator` family in Hexalith.Builds central package audit. evidence: `Roslynator.Analyzers` and `Roslynator.Formatting.Analyzers` are declared under separate single-package families (`package:roslynator.analyzers` and `package:roslynator.formatting.analyzers`) rather than a coordinated suite in `Get-PackageFamily` (`audit-central-package-versions.ps1`).
status: open

### DW-366: Link `FsCheck.Xunit.v3` to the `xunit` rollback group in Hexalith.Builds package audit.

origin: migrated from legacy ledger ("Deferred from: CI/CD xUnit v3 restore failure fix (2026-08-21)"), 2026-08-30
location: FsCheck.Xunit.v3
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-gh-32485211318-fix-ci-cd.md` summary: Link `FsCheck.Xunit.v3` to the `xunit` rollback group in Hexalith.Builds package audit. evidence: `FsCheck.Xunit.v3` is tracked in a separate family (`package:fscheck.xunit.v3`) rather than being linked to the `xunit` family or rollback group, which risks partial upgrades across dependent testing packages.
status: open

### DW-367: Commit `56aa0fec` does not describe the change it carries; the Story 3.13 disposition verifier landed under an unrelated `release_evidence_handlers` subject.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Commit `56aa0fec` does not describe the change it carries; the Story 3.13 disposition verifier landed under an unrelated `release_evidence_handlers` subject. evidence: `56aa0fec` is titled `feat(release_evidence_handlers): add v3 codec for corrective release packet and initial handler setup`, yet it carries the 2,514-line Story 3.13 disposition verifier in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`, the disposition envelope and its sidecar, the story record, both spec files, `docs/ci.md`, and `sprint-status.yaml` — 8 of its 23 files are Story 3.13 scope. Conventional Commits is a tracked project rule and semantic-release consumes these subjects. Already merged to `main`, so not fixable without history rewrite. severity: medium
status: open

### DW-368: `ForeignLineageTokens` is hand-maintained with no completeness guard.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:113-127
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `ForeignLineageTokens` is hand-maintained with no completeness guard. evidence: `DeployedRuntimeParityClosureTests.cs:113-127` omits the two explicitly voided subject digests `394292a2…` and `93d70d51…` and the historical proof-packet digest `349e0998…`. Compounding this, the retained subject's own `limitations[4]` names `394292a2` and `fa2d1c99` as void facts, and `limitations` is not among the six sections `RejectForeignLineage` scans (`DispositionIdentitySections:198-206`), so those tokens would not be caught there either. Already recorded on the spec Defer list. severity: medium
status: open

### DW-369: Malformed provenance labels beyond the declared three can neither pass nor be declared.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4313-4360
severity: low
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Malformed provenance labels beyond the declared three can neither pass nor be declared. evidence: `DeployedRuntimeParityClosureTests.cs:4313-4360` rejects any retained config label whose value equals `MalformedLabelValue` but is absent from `MalformedProvenanceLabels`, while the cardinality check `malformed.Length != platforms.Length * MalformedProvenanceLabels.Length` simultaneously forbids declaring the extra rows. Not live for the frozen `v3.94.1` configs (exactly 3 labels × 2 platforms, verified on disk); a robustness gap only for a successor candidate whose configs differ. severity: low
status: open

### DW-370: Two canonicalizers define one authority with no equivalence test.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
location: tools/release_evidence_handlers/v3.py:76
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Two canonicalizers define one authority with no equivalence test. evidence: Python `canonical_bytes` (`tools/release_evidence_handlers/v3.py:76`, reached via the 11-line `tools/release_evidence_codec.py` facade) authors the envelope bytes, while C# `CanonicalDispositionBytes` verifies them; nothing tests that the two agree for non-ASCII input or line separators. Already recorded on the spec Defer list, but the Code Map still points at the pre-facade `release_evidence_codec.py:74`. severity: medium
status: open
decision: 2026-09-06 Keep deferred
decision: 2026-09-06 Keep deferred

### DW-371: Receipt `source_url` requires a GitHub commit anchor that cannot exist; the existing deferral's "pre-existing pattern inherited" rationale is false and is corrected here.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
archived: 2026-09-18

### DW-372: Mark or re-tier the heavyweight container-publish theories so the CI-gating Contracts lane is not paying for real `dotnet publish` cycles.

status: done 2026-09-05
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
archived: 2026-09-18

### DW-373: Document how a second corrective release adds a `v4` evidence handler; the v3 handler is a deliberate single-packet allowlist with no successor and no procedure.

status: done 2026-09-05
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
archived: 2026-09-18

### DW-374: The deferred-work ledger itself records a stale publication pin.

status: done 2026-09-05
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
archived: 2026-09-18

### DW-375: Replace the fixture-only Story 3.13 durable-source URL anchor with a GitHub-minted immutable acceptance reference before collecting the three production receipts.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: bmad-build review closure of Story 3.13 (2026-08-22)"), 2026-08-30
archived: 2026-09-18

### DW-376: `PathIsWithin` (backing the new `disposition.location`/`disposition.directory` guards) has no reparse-point resolution.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:8187-8194
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `PathIsWithin` (backing the new `disposition.location`/`disposition.directory` guards) has no reparse-point resolution. evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:8187-8194` compares `Path.GetFullPath(...)` results with an ordinal `StartsWith`, unlike `ResolveWithin` elsewhere in the file, which is reparse-point safe. This reproduces this story's own already-deferred `ResolveWithin` ordinal-`StartsWith`/TOCTOU weakness class in a brand-new guard rather than reusing the hardened helper. Not live risk for the current developer-authored evidence tree; a robustness gap if the disposition directory is ever attacker-influenced. severity: low
status: open

### DW-377: `review_loop_iteration` frontmatter metadata does not track the number of review passes the spec itself narrates.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)"), 2026-08-30
location: spec-3-13-deployed-runtime-parity-closure.md:7
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `review_loop_iteration` frontmatter metadata does not track the number of review passes the spec itself narrates. evidence: `spec-3-13-deployed-runtime-parity-closure.md:7` stays `1` although the 2026-08-22 diff alone narrates three distinct passes (the 2026-08-21 loop, its loop-1 historical ledger, and the 2026-08-22 closure). Cosmetic; the same field was previously corrected from `7` to `13` in an earlier chunk of this story. severity: low
status: open

### DW-378: "Review Closure" sections collapse many granular historical findings into a few broad bullets, discarding per-finding traceability.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)"), 2026-08-30
location: spec-3-13-deployed-runtime-parity-closure.md:210-218
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: "Review Closure" sections collapse many granular historical findings into a few broad bullets, discarding per-finding traceability. evidence: The "Review Closure (2026-08-22)" section (`spec-3-13-deployed-runtime-parity-closure.md:210-218`) resolves roughly twenty individually-numbered findings from the preceding historical ledger via 5 broad bullets, one of which alone bundles eight unrelated changes, while explicitly leaving every underlying checkbox unchecked ("authoritative over the unchecked historical rows above"). A future auditor cannot trace a specific historical finding to its specific resolution. Same documentation-completeness gap already noted once before in this story's chunk-2 review ("eighteen of the twenty-five chunk-2 patch bullets are checked with no disposition text"). severity: low
status: open

### DW-379: The `depends_on_corrective_release` / `corrective_release_owner: "3.14"` intentional-pairing claim in `docs/ci.md` is untested.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)"), 2026-08-30
location: docs/ci.md
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The `depends_on_corrective_release` / `corrective_release_owner: "3.14"` intentional-pairing claim in `docs/ci.md` is untested. evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4549-4558` correctly gives `depends_on_corrective_release` its own diagnostic separate from the authorization-flag group, but `docs/ci.md:375`'s claim that a `true` value paired with `corrective_release_owner: "3.14"` is an intentional, non-authorizing scheduling reference (not a dependency) is asserted by no test. severity: low
status: open

### DW-380: `MalformedProvenanceLabels` (a cached static field) and its new platform/config-file counterpart (a recomputed local) are inconsistent, and the local re-reads/re-parses `index.raw` from disk on every call.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 2)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4317-4318
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `MalformedProvenanceLabels` (a cached static field) and its new platform/config-file counterpart (a recomputed local) are inconsistent, and the local re-reads/re-parses `index.raw` from disk on every call. evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4317-4318` derives `platforms`/`configFiles` locally from the retained index children on each invocation of `RejectDispositionDefects`, instead of caching once like the sibling `MalformedProvenanceLabels` field. Minor repeated I/O, not a correctness defect for the current two-platform fixture. severity: low
status: open

### DW-381: The Story 3.15 Test Architect receipt (`bmad:murat`) has no externally-checkable anchor comparable to the two GitHub-issue-comment-backed owner receipts.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 3)"), 2026-08-30
archived: 2026-09-18

### DW-382: `closure.json` declares `deployed_runtime_parity: "available"` and a non-null `selected_deployed_identity` even when `acceptances.receipts` is empty.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 3)"), 2026-08-30
archived: 2026-09-18

### DW-383: `RejectDispositionManifest`'s new directory allow-list branch (`actualDirectories.All(allowedDirectories.Contains)`) has no negative test planting an unlisted stray directory.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
location: acceptances/<envelope-hash>/junk/
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `RejectDispositionManifest`'s new directory allow-list branch (`actualDirectories.All(allowedDirectories.Contains)`) has no negative test planting an unlisted stray directory. evidence: Every existing negative case for the disposition-manifest closed inventory (`resealed-stray-file`, `resealed-stray-acceptance-file`, `role-filename-mismatch`, `undeclared-sidecar`, `stale-envelope-directory`) plants a file, never an unlisted directory (e.g. `acceptances/<envelope-hash>/junk/`), so the directory-allow-list branch added in this diff is unproven. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5381-5394`. severity: low
status: open

### DW-384: `RejectDispositionManifest`'s directory/file enumeration is not reparse-point-safe, the same weakness class already deferred above for `PathIsWithin` (loop 2), now present at a second call site.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5386-5392, 5401-5405
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `RejectDispositionManifest`'s directory/file enumeration is not reparse-point-safe, the same weakness class already deferred above for `PathIsWithin` (loop 2), now present at a second call site. evidence: `Directory.GetDirectories(dispositionRoot, "*", SearchOption.AllDirectories)` and `DispositionFilesUnder`'s `Directory.GetFiles(...)` call, unlike `ResolveWithin` elsewhere in the file, follow reparse points without exclusion. A symlink planted inside the disposition directory could evade the closed-inventory check the same way it could evade `PathIsWithin`. Requires repo write access to exploit. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5386-5392, 5401-5405`. severity: low
status: open

### DW-385: `DispositionSpecificLimitations` hardcodes three full sentences of frozen evidence prose as C# string literals with no automated cross-check against the frozen JSON file.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
location: review-subject.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `DispositionSpecificLimitations` hardcodes three full sentences of frozen evidence prose as C# string literals with no automated cross-check against the frozen JSON file. evidence: A third, positionally-coupled source of truth for text that also lives in the frozen `review-subject.json`/envelope evidence — the same duplicate-source-of-truth pattern this diff fixed for the retained manifest arrays (`RetainedManifestFiles`/`-EntryCounts`/`-Bases`, tupled together) but left unfixed here. Evidence is frozen, so live drift risk is theoretical unless a future revalidation trigger amends it. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:188-206`. severity: low
status: open

### DW-386: `role-filename-mismatch`, `undeclared-sidecar`, and `stale-envelope-directory` silently moved from a soft acceptance-layer diagnostic to a hard whole-envelope failure, undocumented in Design Notes.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:857-927
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `role-filename-mismatch`, `undeclared-sidecar`, and `stale-envelope-directory` silently moved from a soft acceptance-layer diagnostic to a hard whole-envelope failure, undocumented in Design Notes. evidence: Previously these three mutations left `Verified: true` and only blocked `story_may_be_done`, under reason codes `acceptance.receipt_set`/`acceptance.receipt_directory`. This diff moves them to the disposition-manifest layer, where they now set `Verified: false` under `disposition.manifest`. A real contract change to what `Verified` means for any caller, and it appears to be a strengthening rather than a regression, but the spec's Design Notes do not call out the shift. `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:857-927`. severity: low
status: open

### DW-387: The "Suggested Review Order" section's absolute line-number anchors into four sibling files have no re-derivation task tied to them.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
archived: 2026-09-18

### DW-388: Story 3.15's lifecycle surfaces disagree as committed and no test cross-checks them.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
archived: 2026-09-18

### DW-389: One of the three Story 3.15 digests published in `docs/ci.md` is bound by no test.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
archived: 2026-09-18

### DW-390: The date-rollover fix uses an optional `validationTime` parameter, preserving the silence that caused the original defect.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4204-4212
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The date-rollover fix uses an optional `validationTime` parameter, preserving the silence that caused the original defect. evidence: `DispositionStoryMayBeDone(..., DateTimeOffset? validationTime = null)` at `DeployedRuntimeParityClosureTests.cs:4204-4212` defaults to real `DateTimeOffset.UtcNow`. Four of five call sites (`:2970`, `:3241`, `:3248`, `:3655`) still take that default; only `:3781` threads the fixture time. Correct for those fixtures today; the recurrence trap is that a future fixture-time test can forget to thread it and silently stop firing. severity: low
status: open

### DW-391: Ledger hygiene across the six appended blocks, with nothing validating any of it.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: deferred-work.md:1409
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Ledger hygiene across the six appended blocks, with nothing validating any of it. evidence: `source_spec` switches from absolute to repo-relative paths mid-file; the "bmad-build review closure of Story 3.13 (2026-08-22)" entry near `deferred-work.md:1409` is the only new entry with no `severity:` key; two entries filed under a Story 3.13 heading declare a Story 3.15 `source_spec`, so heading-grouped and `source_spec`-grouped sweeps disagree; and the reparse-point weakness is deferred twice with no shared id. The DW6 governance suite is 19/19 skipped (`[Fact(Skip = "ATDD red phase -- DW6 deferred-work governance checker and story artifacts are not implemented.")]`), and the executable AWK gate in `ProofPacketValidatorIntegrityTests.cs:864-870` is scoped to three Story 1.20 headings only. severity: low
status: open

### DW-392: The historical-tree location guard is proven against a fabricated repository layout.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: nested/<sha>
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The historical-tree location guard is proven against a fabricated repository layout. evidence: `DispositionInsideHistoricalEvidenceTreeFailsClosed` (`DeployedRuntimeParityClosureTests.cs:3088,3101`) passes `cleanupRoot`, a temp directory, as `repositoryRoot`, unlike its sibling `MisnamedDispositionDirectoryFailsClosed` which passes the real root. The disjunct is exercised against a synthesized `nested/<sha>` path rather than the real frozen `fa2d1c99...` tree, and passes only because `disposition.location` is diagnosed before any missing-repository-file check. severity: low
status: open

### DW-393: The new checksum-manifest mutation theory omits non-string and whitespace-only `file` values.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:3456-3474
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The new checksum-manifest mutation theory omits non-string and whitespace-only `file` values. evidence: `DuplicateOrEmptyChecksumManifestDeclarationFailsClosed` (`DeployedRuntimeParityClosureTests.cs:3456-3474`) covers duplicate and empty `file` values only. A non-string JSON value (e.g. `42`) or a whitespace-only value would route to `internal.exception` or to a different diagnostic rather than the `envelope.retained_checksum_manifests` code the theory asserts. severity: low
status: open

### DW-394: `docs/ci.md` flattens the known receipt-authenticity asymmetry for Story 3.15.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
archived: 2026-09-18

### DW-395: Story 3.13's three role-bound acceptances are a self-attestation, not independent three-party review.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.13 acceptance collection (2026-08-24)"), 2026-08-30
archived: 2026-09-18

### DW-396: The development-gitlink guard encodes "deliberately independent of the release pin" as a permanent inequality, so a legitimate submodule bump onto the release pin fails a test with no failure meaning.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release.md (2026-08-24)"), 2026-08-30
archived: 2026-09-18

### DW-397: No executable guard asserts that the release caller's pinned reusable-workflow SHA is reachable on the Hexalith.Builds remote rather than only in a local object store.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release.md (2026-08-24)"), 2026-08-30
location: github/workflows/release.yml:103,110
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: No executable guard asserts that the release caller's pinned reusable-workflow SHA is reachable on the Hexalith.Builds remote rather than only in a local object store. evidence: `.github/workflows/release.yml:103,110` pin `builds-execution-sha`, and the story record's warning that a rotation target must exist on the remote was deleted in `f2d2575c` with nothing replacing it. This is the defect that produced the chunk-A+B blocking Decision, when `63409393…` was pinned while it existed only on an unpushed branch (it has since been merged to Builds `main` and superseded by `a07078ad…`). Deferred 2026-08-24 by owner decision: an unresolvable `uses:` SHA already fails the Release dispatch at startup — the quarantined run `32347773728` failure mode — so nothing publishes silently, and every candidate guard costs either network plus auth inside the Tier-1 CI-gating Contracts lane or a `origin/main` remote-tracking ref that a CI submodule checkout may not populate (and which reds on force-pushes it should not judge). The recurring drift class is closed separately by binding the `docs/ci.md` pin prose to `ApprovedBuildsReleaseSha`. severity: low
status: open
decision: 2026-09-06 Honor keep-open decision
decision: 2026-09-06 Honor keep-open decision
decision: 2026-09-01 Keep open

### DW-398: The Story 3.13 closure-packet gate `ValidateAcceptances` still enforces the unmintable `#story-3-13-<hash>-<role>` commit anchor that only a fixture can satisfy.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
archived: 2026-09-18

### DW-399: `author_association` requirements diverge between the registry authority source and acceptance receipts, and the divergence was resolved downward to keep real evidence passing.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
archived: 2026-09-18

### DW-400: The OCI `created` provenance labels are self-comparing in tests and unchecked by the codec, and the retained child configs carry a malformed truncated value.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: CorrectiveOciProvenanceReleaseTests.cs:118
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: The OCI `created` provenance labels are self-comparing in tests and unchecked by the codec, and the retained child configs carry a malformed truncated value. evidence: `CorrectiveOciProvenanceReleaseTests.cs:118` sets `expected ??= ExpectedLabels(observedCreated)` where `observedCreated` is read from the first child config, so child 1 compares to itself; `v3.py:134 _expected_labels` omits `created` from the five enforced keys. Both retained configs carry `org.opencontainers.image.created = "2026-08-20T11"`, truncated at the first colon, inside the selected identity.
status: open
decision: 2026-09-06 Build corrective successor — Create the versioned corrective packet, validate timestamps independently, and collect new governed evidence.
decision: 2026-09-06 Build corrective successor — Create the versioned corrective packet, validate timestamps independently, and collect new governed evidence.
decision: 2026-09-01 Build successor packet — Enforce a canonical OCI creation timestamp, produce corrected child configs, reseal the successor packet, and collect required authorization.

### DW-401: Two Production-smoke guards are green by construction -- `redirect_count` and `observed_runtime_platform` can never disagree with what they are checked against.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: capture-corrected-deployed-runtime-parity-smokes.py
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: Two Production-smoke guards are green by construction -- `redirect_count` and `observed_runtime_platform` can never disagree with what they are checked against. evidence: `capture-corrected-deployed-runtime-parity-smokes.py` invokes `curl` without `--location`, so `num_redirects` is structurally 0 and the verifier's `redirect_count != 0` check cannot fire; `observed_runtime_platform` comes from `docker image inspect {{.Os}}/{{.Architecture}}`, i.e. the metadata `--platform` already selected. Separately, `smokes/*.log` are canonical JSON restatements of `smoke-results.json`, so the log-versus-summary comparison is between two hand-written documents rather than a retained transcript.
status: open

### DW-402: `FrozenStory314PacketRemainsByteForByteUnchanged` hashes a single file despite asserting whole-packet immutability.

status: done 2026-09-01
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
archived: 2026-09-18

### DW-403: The `_bmad-output/test-artifacts/` gate artifacts backing the Test Architect receipt disagree with the matrix they summarize and cite a nonexistent test method.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
archived: 2026-09-18

### DW-404: The Hexalith.Builds gitlink was rotated to the tip of origin/main while the release workflow pin was left behind.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
archived: 2026-09-18

### DW-405: BLOCKING, OWNER DECISION -- the next Release run fails at container publish, after NuGet packages are already pushed, because the mandatory `ContainerProvenanceCreated` input is not supplied by the pinned Builds publisher.

status: done 2026-08-31
origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
archived: 2026-09-18

### DW-406: OWNER ACTION -- Story 3.15 has no dedicated acceptance issue, and the three superseded receipts were spliced onto Story 3.14's thread.

status: done 2026-08-31
origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
archived: 2026-09-18

### DW-407: No guard asserts the pinned Builds release SHA is reachable on the Builds remote; the only availability check reads the local clone.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: references/Hexalith.Builds
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: No guard asserts the pinned Builds release SHA is reachable on the Builds remote; the only availability check reads the local clone. evidence: `ContainerPublishingGovernanceTests` asserts the pin only as a string, and `CorrectiveOciProvenanceReleaseTests` runs `git cat-file -e <sha>^{commit}` inside `references/Hexalith.Builds`, which a commit on an unpushed local branch also satisfies. A pin that exists only locally makes the reusable-workflow `uses:` ref unresolvable at dispatch -- the defect that already shipped once with `63409393`. Verified today that `a07078ad` and `22a578b5` are both contained in `origin/main`, so this is a missing guard rather than a live break.
status: open
decision: 2026-09-06 Honor prior decision
decision: 2026-09-06 Honor prior decision

### DW-408: The `.gitattributes` normalization guard enumerates only `*.raw`, leaving 56 `.nupkg` and every digest-bound `.json`/`.txt`/`.log` file unguarded.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: story-3-15/**/*.nupkg binary
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The `.gitattributes` normalization guard enumerates only `*.raw`, leaving 56 `.nupkg` and every digest-bound `.json`/`.txt`/`.log` file unguarded. evidence: `DigestBearingRawOciEvidenceIsBinary` enumerates `git ls-files "*.raw"` only. Deleting the `story-3-15/**/*.nupkg binary` line while keeping `story-3-15/** text eol=lf` turns all 14 story-3-15 packages into text, breaking every `packages.items[*].sha256` binding on a `core.autocrlf=true` checkout, with the suite still green on Linux CI. This loop added `*.py text eol=lf` for the SHA-pinned verifiers, but the enumerating guard was not generalized.
status: open

### DW-409: The multi-RID `org.opencontainers.image.created` assertion compares the artifact to itself.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: org.opencontainers.image.created
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The multi-RID `org.opencontainers.image.created` assertion compares the artifact to itself. evidence: `CorrectiveOciProvenanceReleaseTests` sets `expected ??= ExpectedLabels(observedCreated)` where `observedCreated` is read out of the first child config, so the emitted label is compared against itself rather than against the `-p:ContainerProvenanceCreated` input. Replacing the label value with a build-time `UtcNow` keeps the suite green. The indirection was necessary while the MSBuild fallback made the value unpredictable; now that the input is mandatory it is a stale weakening.
status: open

### DW-410: The Story 3.13 closure acceptance contract still requires the commit anchor that GitHub cannot mint, while the disposition lane was migrated to `#issuecomment-<id>`.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: /v1
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The Story 3.13 closure acceptance contract still requires the commit anchor that GitHub cannot mint, while the disposition lane was migrated to `#issuecomment-<id>`. evidence: `DeployedRuntimeParityClosureTests.ValidateAcceptances` still builds `<commit-url>#story-3-13-<subject>-<role>` and requires source schema `.../v1`, so only the fixture can satisfy it; the disposition path was moved to the GitHub-minted anchor and `/v2`. The two acceptance surfaces now use different, mutually unsatisfiable anchor contracts. Out of lane for Story 3.15 and left to the 3.13 lane.
status: open

### DW-411: The `_bmad-output/test-artifacts/` gate PASS was withdrawn this loop but not regenerated.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: _bmad-output/test-artifacts/
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The `_bmad-output/test-artifacts/` gate PASS was withdrawn this loop but not regenerated. evidence: `gate-decision.json`, `e2e-trace-summary.json`, and `traceability-matrix.md` were scored at `source_sha` 516f2489 against subject `bb58d691` and reported `PASS` with a vacuous `p1_status: MET` over an empty P1 set. They are now explicitly marked SUPERSEDED with a banner rather than regenerated, because the trace workflow owns their production.
status: open

### DW-412: v3's timestamp parser is looser than v1's, so the frozen predecessor's timestamps are validated by the weaker rule.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: tools/release_evidence_handlers/v3.py:456-465
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: v3's timestamp parser is looser than v1's, so the frozen predecessor's timestamps are validated by the weaker rule. evidence: `v1._parse_time` requires a strict `YYYY-MM-DDThh:mm:ss[.ffffff]Z` shape and rejects naive datetimes. The v3 code that `v1` delegates predecessor validation to still uses `value.replace("Z", "+00:00")` (`tools/release_evidence_handlers/v3.py:456-465`), which replaces every `Z` in the string, accepts a space separator, and accepts arbitrary non-UTC offsets. The hardening stops at the module boundary. Deferred because `v3.py` is the frozen Story 3.14 verifier and any edit re-mints the Story 3.15 subject.
status: open

### DW-413: Retained-file `size` has no upper bound and every retained and discovered file is read whole into memory.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: Retained-file `size` has no upper bound and every retained and discovered file is read whole into memory. evidence: `_binding` requires `size` to be a positive integer with no cap, and `_verify_file` / `_validate_inventory` `read_bytes()` each retained file plus every file the inventory `rglob` walk discovers. A packet declaring or containing multi-gigabyte files exhausts memory before any verdict is reached. Bounded in practice by the packet being local and produced by the assembler.
status: open
decision: 2026-09-06 Build bounded handler — Create a successor handler with per-file and aggregate limits, then remint and verify it.
decision: 2026-09-06 Build bounded handler — Create a successor handler with per-file and aggregate limits, then remint and verify it.

### DW-414: The dispatch-table consistency guards cannot fire with the current single-entry constant tables.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: tools/validate-corrected-deployed-runtime-parity.py:44-52
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The dispatch-table consistency guards cannot fire with the current single-entry constant tables. evidence: `_verify_dispatch_table` (`tools/validate-corrected-deployed-runtime-parity.py:44-52`) and the two set-comparison checks in `_load_handler` (`tools/validate-corrective-release-evidence.py:66,72`) guard against a future misconfiguration -- registering a handler without pinning it -- that no current table can express, and no test constructs the inconsistent state. Accepted as future-proofing rather than removed.
status: open

### DW-415: The Production smoke results file is never checked for canonical byte form.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The Production smoke results file is never checked for canonical byte form. evidence: `_validate_smokes` binds the results file by digest and validates its fields, but unlike the receipt, subject, and registry paths it never asserts `results_bytes == canonical_bytes(results)`. Non-canonical whitespace simply yields a different subject digest rather than a forgery vector, so this is a consistency gap rather than a hole.
status: open
decision: 2026-09-06 Version smoke contract — Add canonical-byte proof to a successor smoke schema and recollect governed evidence.
decision: 2026-09-06 Version smoke contract — Add canonical-byte proof to a successor smoke schema and recollect governed evidence.

### DW-416: All verifier failures collapse to exit code 1, so "the verifier itself was modified" is indistinguishable from "the evidence did not validate".

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: docs/ci.md
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: All verifier failures collapse to exit code 1, so "the verifier itself was modified" is indistinguishable from "the evidence did not validate". evidence: `main()` catches `(OSError, DispatchError, ValueError, json.JSONDecodeError)` and returns 1 for all of them. For a supply-chain gate, `DispatchError` (tampered or unpinned handler) deserves a distinct exit status from `EvidenceError`. Deferred because `docs/ci.md` and the test suite assert the current exit contract.
status: open

### DW-417: `"closure.json"` is hardcoded into the closed technical inventory while the CLI accepts an arbitrary evidence path and an independent `--packet-root`.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: tools/deployed_runtime_parity_handlers/v1.py:746
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: `"closure.json"` is hardcoded into the closed technical inventory while the CLI accepts an arbitrary evidence path and an independent `--packet-root`. evidence: `tools/deployed_runtime_parity_handlers/v1.py:746` excludes the literal `closure.json` from the stray-file sweep. A closure file under a different name at the packet root fails with a misleading "files outside the closed technical inventory"; a `--packet-root` pointing elsewhere leaves the closure file uncovered by the inventory entirely. Neither the argparse help nor the docstring records the constraint.
status: open
decision: 2026-09-06 Build aligned successor — Version both contracts, define one authoritative meaning, and remint the packet.
decision: 2026-09-06 Build aligned successor — Version both contracts, define one authoritative meaning, and remint the packet.

### DW-418: The `summary_bindings` deletion reduces `validate_packet_files`' standalone behavior inside a line range the Code Map freezes, leaving a vestigial `summaries` dict.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 4 chunk 1)"), 2026-08-30
location: v3.py:863-974
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The `summary_bindings` deletion reduces `validate_packet_files`' standalone behavior inside a line range the Code Map freezes, leaving a vestigial `summaries` dict. evidence: The diff removes the one-shared-two-platform-summary check from `v3.validate_packet_files`, a public entry point, inside the Code Map's frozen `v3.py:863-974` "preserve v3 behavior" range. It is redundant today only because every present caller invokes `validate_identity` first (`v1.py:445-452`, `validate-corrective-release-evidence.py:108/115`), where the identical constraint is enforced at `v3.py:397`. Confirmed independently by three review layers as not-lost-verification; carried here as a frozen-range and dead-code note. The now-purposeless `summaries` cache at `v3.py:944-952` invites the reader to assume a guard is still present.
status: open

### DW-419: The pinned release publisher cannot supply the newly mandatory container creation timestamp, while a governance test encodes release-pin/gitlink inequality as policy.

status: done 2026-08-31
origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
archived: 2026-09-18

### DW-420: The legacy release job retains unused `attestations: write` and `id-token: write` permissions.

status: done 2026-09-06
origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
archived: 2026-09-18

### DW-421: Container provenance URL validation accepts unrelated hosts and malformed percent escapes instead of enforcing the repository-derived canonical URLs.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: Directory.Build.targets
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: Container provenance URL validation accepts unrelated hosts and malformed percent escapes instead of enforcing the repository-derived canonical URLs. evidence: `Directory.Build.targets` checks only an HTTPS-shaped regex; values such as an unrelated repository URL or a path containing `%ZZ` pass and can enter OCI labels. Canonical URI derivation and validation belong to the corrective-publisher lane rather than this parity packet.
status: open

### DW-422: The container provenance creation-time regex accepts calendar-impossible dates.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: Directory.Build.targets
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The container provenance creation-time regex accepts calendar-impossible dates. evidence: `Directory.Build.targets` bounds month and day fields independently, so a value such as `2026-02-31T09:15:00Z` satisfies the claimed RFC 3339 validation. Correct calendar validation is a publisher/input-contract follow-up.
status: open

### DW-423: The multi-RID provenance test compares the produced creation label to its first observed value rather than the supplied creation instant.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: n/a
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The multi-RID provenance test compares the produced creation label to its first observed value rather than the supplied creation instant. evidence: `CorrectiveOciProvenanceReleaseTests.RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` feeds `observedCreated` into `ExpectedLabels`, so two children can share the same wrong valid timestamp and still pass. The test should compare both labels directly with its `Created` input.
status: open

### DW-424: The container default-tag test never observes the tag value it claims was defaulted.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The container default-tag test never observes the tag value it claims was defaulted. evidence: `ContainerPublicationDefaultsTagToProvenanceVersion` runs only `ValidateContainerProvenanceInputs` and checks exit zero; deleting or breaking the default assignment can leave that test green. A future publisher test should inspect the evaluated tag or produced archive.
status: open

### DW-425: The frozen Story 3.14 timestamp parser remains weaker than the strict Story 3.15 parser.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: tools/release_evidence_handlers/v3.py
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The frozen Story 3.14 timestamp parser remains weaker than the strict Story 3.15 parser. evidence: `tools/release_evidence_handlers/v3.py` uses `value.replace("Z", "+00:00")` with `datetime.fromisoformat`, admitting spaces, arbitrary offsets, and other shapes that v1 rejects. Tightening the frozen predecessor contract is separate Story 3.14 evidence maintenance.
status: open

### DW-426: The packet inventory does not require the validated closure path to be the packet root's `closure.json`.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: closure.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The packet inventory does not require the validated closure path to be the packet root's `closure.json`. evidence: `_validate_inventory` permits literal `closure.json` but checks only unexpected actual files, while the CLI accepts independent evidence and packet-root paths. A copied packet root without its own closure can validate against an external closure, which is a CLI/inventory contract follow-up.
status: open
decision: 2026-09-06 Build aligned successor — Create an aligned versioned closure/CLI contract and remint its evidence.
decision: 2026-09-06 Build aligned successor — Create an aligned versioned closure/CLI contract and remint its evidence.

### DW-427: The checked-in traceability gate artifacts remain superseded and do not cover the final Story 3.15 subject or current focused suite.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: _bmad-output/test-artifacts/gate-decision.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The checked-in traceability gate artifacts remain superseded and do not cover the final Story 3.15 subject or current focused suite. evidence: `_bmad-output/test-artifacts/gate-decision.json`, `e2e-trace-summary.json`, and `traceability-matrix.md` explicitly describe a superseded collection while retaining PASS-shaped fields. Regeneration belongs to the trace workflow and is not evidence created by the parity verifier.
status: open

### DW-428: A remaining Story 3.13 acceptance path still requires an unmintable synthetic commit-fragment source contract.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: n/a
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: A remaining Story 3.13 acceptance path still requires an unmintable synthetic commit-fragment source contract. evidence: `DeployedRuntimeParityClosureTests.ValidateAcceptances` retains the `#story-3-13-<hash>-<role>` commit anchor and v1 schema at live call sites, while genuine GitHub acceptances use issue-comment sources. This is Story 3.13 compatibility debt, not part of positive Story 3.15 closure.
status: open

### DW-429: The changed v3 issue-number normalization has no direct regression test.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The changed v3 issue-number normalization has no direct regression test. evidence: `release_evidence_handlers.v3.repository_issue_html_url` rejects padded and non-ASCII digits, but the existing `AuthorityHtmlUrlFollowsTheAcceptedIssueUrl` test exercises the sibling codec implementation. A v3-focused mutation test is needed in the predecessor-maintenance lane.
status: open

### DW-430: `redirect_count == 0` is structurally unfireable and the new test now pins that property.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/deployed_runtime_parity_handlers/v1.py:639
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: `redirect_count == 0` is structurally unfireable and the new test now pins that property. evidence: The capture never passes `--location`, so curl's `num_redirects` is always 0; both the producer's `redirect_count == 0` and the verifier's `item["redirect_count"] != 0` (`tools/deployed_runtime_parity_handlers/v1.py:639`) can never fire. `CorrectedDeployedRuntimeParitySmokeCaptureTests.cs` now asserts `line.ShouldNotContain("--location")`, converting an already-acknowledged deferral into an asserted invariant.
status: open

### DW-431: The post-execution import-shadow backstop runs only on the success path and no test reaches it with a repository module loaded.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/validate-corrected-deployed-runtime-parity.py:164
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The post-execution import-shadow backstop runs only on the success path and no test reaches it with a repository module loaded. evidence: `_verify_no_repository_import_shadows` (`tools/validate-corrected-deployed-runtime-parity.py:164`, called at `:280`) sits inside the `try` after `validate_packet_files` succeeds, so it can invalidate a verdict but cannot prevent a shadow module's side effects. `RepositoryLocalStandardLibraryShadowCannotExecute` fails earlier at the receipt-count check, so making the backstop a no-op changes no test outcome. The `sys.path` half of the protection is genuinely covered.
status: open
decision: 2026-09-06 Build identity backstop — Add post-import identity verification to a versioned dispatcher and remint its governed evidence.
decision: 2026-09-06 Build identity backstop — Add post-import identity verification to a versioned dispatcher and remint its governed evidence.

### DW-432: v3's timestamp parser is looser than v1's, so frozen-predecessor timestamps are checked by the weaker rule.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/release_evidence_handlers/v3.py:456-465
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: v3's timestamp parser is looser than v1's, so frozen-predecessor timestamps are checked by the weaker rule. evidence: `tools/release_evidence_handlers/v3.py:456-465`. Carried forward from loop 4; re-confirmed unchanged at HEAD.
status: open

### DW-433: No size bound on retained files, and the nuspec decompression-bomb half of the earlier entry is still open.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/deployed_runtime_parity_handlers/v1.py:161-185
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: No size bound on retained files, and the nuspec decompression-bomb half of the earlier entry is still open. evidence: `tools/deployed_runtime_parity_handlers/v1.py:161-185` reads every retained and discovered file whole into memory with no upper bound on `size`. `tools/release_evidence_handlers/v3.py:436` still performs an uncapped `archive.read(nuspecs[0])`; loop 4's hardening closed only the entity-expansion half, so a small `.nupkg` declaring a huge `.nuspec` entry still expands unbounded before any check.
status: open
decision: 2026-09-06 Build bounded successor — Introduce per-entry and aggregate byte budgets in a successor handler and remint evidence.
decision: 2026-09-06 Build bounded successor — Introduce per-entry and aggregate byte budgets in a successor handler and remint evidence.

### DW-434: All failure modes collapse to exit 1 and, for loader failures, to a single message that hides the chained cause.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/validate-corrected-deployed-runtime-parity.py:195-197
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: All failure modes collapse to exit 1 and, for loader failures, to a single message that hides the chained cause. evidence: `tools/validate-corrected-deployed-runtime-parity.py:195-197` re-raises every `_load_verified_module` exception as `DispatchError("trusted live handler could not be loaded")`, and `main()` prints only `str(error)`, so a syntax error, a missing dependency and a tampered handler are indistinguishable. A tampered verifier is likewise indistinguishable from invalid evidence.
status: open

### DW-435: Roughly 90 lines of security-critical loader code are duplicated across the two dispatchers with no sync test.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: _begin/_end_trusted_import_environment
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: Roughly 90 lines of security-critical loader code are duplicated across the two dispatchers with no sync test. evidence: `_is_repository_path`, `_module_is_repository_local`, `_begin/_end_trusted_import_environment`, `_load_verified_module` and `_verify_imported_file` exist in both `tools/validate-corrected-deployed-runtime-parity.py:113-243` and `tools/validate-corrective-release-evidence.py:85-162`, with divergent signatures (`relative` vs `path`) and the release copy missing the docstrings the parity copy carries. Nothing asserts the twins stay in sync, and the bytes-`TypeError` defect is present in both.
status: open

### DW-436: Several distinct fail-closed branches share one message, so no test can show which clause fired.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tools/deployed_runtime_parity_handlers/v1.py:855
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: Several distinct fail-closed branches share one message, so no test can show which clause fired. evidence: `tools/deployed_runtime_parity_handlers/v1.py:855` raises `GitHub acceptance source is not authenticated to the rostered owner` for eight or-ed conditions, and is the single expected message for both `ReceiptSourceAnchoredOnForeignLineageIssueFailsClosed` and all three cases of `ReceiptSourceIdentityMustResolveToOneComment`. The registry path has the same shape.
status: open
decision: 2026-09-06 Build diagnostic successor — Define stable distinct diagnostics in a successor handler and recollect evidence.
decision: 2026-09-06 Build diagnostic successor — Define stable distinct diagnostics in a successor handler and recollect evidence.

### DW-437: The two timestamp-rejected owner comments are named in three documents but retained nowhere, and the `dab64f5f` pair was never annotated.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: 3-15-corrected-deployed-runtime-parity-closure.md:70-76
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The two timestamp-rejected owner comments are named in three documents but retained nowhere, and the `dab64f5f` pair was never annotated. evidence: `3-15-corrected-deployed-runtime-parity-closure.md:70-76`, the proof packet and `docs/ci.md` all state that comments `5409140199` and `5409147909` were marked `SUPERSEDED -- INVALID TIMESTAMP-MISMATCH ATTEMPT`, but no bytes for either are retained under `evidence/story-3-15/`, so the claim is unverifiable from the repository. The `dab64f5f` owner comments `5408186984`/`5408189299` received no equivalent annotation and remain acceptance-shaped JSON on the now-allowlisted `#352` thread; their rejection rests solely on `subject_sha256` inequality.
status: open
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-437, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-438: The Code Map's frozen fence was extended and its line anchors were not refreshed.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The Code Map's frozen fence was extended and its line anchors were not refreshed. evidence: The Code Map marks `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235` as frozen ("do not extend its frozen candidate contract"). This change inserted 156 lines at `:895`, growing the file 1291 -> 1447, so the `1124-1235` anchor now lands on `CopyDirectory`/`LoadIdentity`/`MutateNuspecRepositoryUrl` instead of the helpers it named. The three added tests are dispatcher-trust tests rather than candidate-contract extensions, so the letter of the note holds, but the anchors are stale.
status: open

### DW-439: Two submodule gitlink bumps rode into a Story 3.15 evidence commit undeclared.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: references/Hexalith.FrontComposer
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: Two submodule gitlink bumps rode into a Story 3.15 evidence commit undeclared. evidence: Commit `67c645ab` bumps `references/Hexalith.FrontComposer` `a229be7e` -> `596e286f` and `references/Hexalith.Tenants` `09c746b3` -> `daf6c76c` alongside the spec entry that asserts "No replacement acceptance, deployment, publication, registry, consumer, predecessor, commit, or push action was performed." Neither the spec change log, the story record, `deferred-work.md` nor `docs/ci.md` mentions them. Both targets were verified contained in their submodules' `origin/main`, so no dangling gitlink is published -- this is unrecorded scope, the known concurrent-loop absorption pattern.
status: open

### DW-440: `RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` is build-state dependent, not code dependent.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:55-88
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: `RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` is build-state dependent, not code dependent. evidence: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:55-88` shells out to `dotnet publish -p:RuntimeIdentifiers="linux-musl-x64;linux-musl-arm64"` with no preceding RID-aware restore. It failed once with `NETSDK1047: Assets file ... doesn't have a target for 'net10.0/linux-musl-x64'` and passed on an immediate identical re-run, so its result depends on whether `obj/project.assets.json` already carries those RIDs.
status: open

### DW-441: Nothing enforces deferred-work ledger format -- every governance test is skipped.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 6)"), 2026-08-30
location: tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: Nothing enforces deferred-work ledger format -- every governance test is skipped. evidence: All `Dw6*` cases in `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/` carry `[Fact(Skip = ...)]` (Dw6Bookkeeping 4, Dw6LedgerSweep 4, Dw6CheckerReport 5, Dw6GovernanceVocabulary 6) and both `Dw4DeferredWorkDispositionAtddTests` cases are skipped as well. This is why the loop-6 block could be appended with missing `severity:` fields, absolute machine-local `source_spec` paths, and duplicate entries without any gate objecting.
status: open

### DW-442: SUPERSEDES the loop-3 entry stating that opening a dedicated Story 3.15 acceptance issue and requesting acceptances "is an Ask First action and was not performed" -- it was subsequently performed.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
archived: 2026-09-18

### DW-443: Ledger-format repair notice for the trusted-verifier hardening block filed under the loop-4 heading.

status: done 2026-09-06
origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
archived: 2026-09-18

### DW-444: The `raw OCI index shape is invalid` branch is unreachable through packet mutation while the index digest is pinned, and is retained as a structural precondition rather than removed.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: oci/index.raw
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The `raw OCI index shape is invalid` branch is unreachable through packet mutation while the index digest is pinned, and is retained as a structural precondition rather than removed. evidence: `_binding(oci["index"], media_type=INDEX_MEDIA_TYPE)` forces `digest == "sha256:" + sha256`, `index_binding["digest"]` must equal the module constant `INDEX_DIGEST`, and `_verify_file` requires the retained bytes to hash to it, so `oci/index.raw` has exactly one admissible content. The shape check still guards the strict three-way `zip(..., strict=True)` that follows it. `RawOciIndexBytesArePinnedByTheSelectedIndexDigest` pins that reasoning instead of faking reachability. status: open — accepted as documented dead-but-defensive; revisit only if the index stops being digest-pinned.
status: open

### DW-445: The retained roster comment names the ratified artifact `reviewer-roster.json` while the packet retains `registry/owner-role-registry.json`.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
archived: 2026-09-18

### DW-446: The `linux/arm64` Production smoke depends on a QEMU emulation registration that the packet cannot hash.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: linux/arm64
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The `linux/arm64` Production smoke depends on a QEMU emulation registration that the packet cannot hash. evidence: The retained arm64 smoke is only valid given `tonistiigi/binfmt@sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0` having been registered on the host. That is host state, not an input byte, so it is documented as an environmental prerequisite in the capture script docstring and in `docs/ci.md`, and `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` keeps the two copies from drifting. Binding the digest into the subject would record intent rather than proof. status: open — recorded as a documented prerequisite by owner decision (loop 6).
status: open

### DW-447: The retained Production smoke bytes were produced by the pre-loop-6 capture tool, so the bound tool of record can no longer reproduce the bytes it certifies.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
archived: 2026-09-18

### DW-448: No drift guard fails loudly when a bUnit upgrade adds or re-signs a render entry point that `AdminUITestContext` no longer intercepts.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: AdminUITestContext
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-32876914109-fix-ci-cd.md` severity: low summary: No drift guard fails loudly when a bUnit upgrade adds or re-signs a render entry point that `AdminUITestContext` no longer intercepts. evidence: The renderer-info contract holds only while every public render entry point declared on `Bunit.BunitContext` has a matching override on `AdminUITestContext`. bUnit 2.9.0 declares three virtual `Render` overloads (all now overridden) and two `[Obsolete]` `RenderComponent` overloads that unconditionally `throw new NotSupportedException`, so they are harmless today. A future bUnit release that adds a fourth entry point would silently stop being covered and resurface `MissingRendererInfoException` in unrelated tests, exactly as the FluentUI `5.0.0-rc.5` bump did. A reflection test comparing declared render members on both types would catch it — and per this repo's history must itself fail when the reflected set comes back empty. status: open — the immediate CI break is fixed; the upgrade-time guard is a separate hardening pass.
status: open

### DW-449: No Admin.UI test can exercise the static prerender pass, because every render declares an interactive renderer.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: src/Hexalith.EventStore.Admin.UI/Components/Routes.razor
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-32876914109-fix-ci-cd.md` severity: low summary: No Admin.UI test can exercise the static prerender pass, because every render declares an interactive renderer. evidence: Production Admin.UI renders `InteractiveServer` (`src/Hexalith.EventStore.Admin.UI/Components/Routes.razor`, `Components/App.razor`), which prerenders with `IsInteractive == false` before the interactive pass. `AdminUITestContext.TestRendererInfo` is `protected virtual` so a derived context can opt into `("Static", false)`, and `SetRendererInfo` now latches so a test can choose per-test — but no test does, and no shared helper exists, so the prerender branch of every component is untested. status: open — the seam exists; adding prerender coverage is a separate test-design pass.
status: open

### DW-450: SUPERSEDES the loop-6 landing note in the sense that two loop-6 fixes were themselves regressions, both reproduced with live controls and closed in loop 7.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-7 landing (2026-08-25)"), 2026-08-30
archived: 2026-09-18

### DW-451: The per-platform Production-smoke cleanup allowance is a verifier-side constant rather than a field in `smoke-results.json`, kept in step only by a focused test.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-7 landing (2026-08-25)"), 2026-08-30
location: smoke-results.json
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The per-platform Production-smoke cleanup allowance is a verifier-side constant rather than a field in `smoke-results.json`, kept in step only by a focused test. evidence: The capture stamps `started_at` before the platform deadline and `ended_at` after an independent 30s cleanup budget, so a legitimate window is the platform budget plus that budget. Recording `cleanup_timeout_seconds` in the smoke summary would be the self-describing fix, but the retained smoke bytes are frozen evidence from 2026-08-21 and must not be rewritten to satisfy a later schema. `CLEANUP_ALLOWANCE_SECONDS` in `v1.py` therefore duplicates `CLEANUP_TIMEOUT_SECONDS` in the capture tool; `CleanupAllowanceAgreesBetweenVerifierAndCaptureTool` pins them equal. Fold the field into the schema at the next legitimate re-capture. status: open — accepted for this packet.
status: open

### DW-452: The assembler still imports the trusted handler through ordinary importlib rather than the source-only loader the dispatchers use.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-7 landing (2026-08-25)"), 2026-08-30
location: n/a
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The assembler still imports the trusted handler through ordinary importlib rather than the source-only loader the dispatchers use. evidence: Loop 7 set `sys.dont_write_bytecode` before the import, made the assembler verify that the imported `v1` and `v3` modules resolve to their repository paths, bound `Path(__file__).resolve()` instead of the pristine repository file, and removed the stale `__pycache__` trees. A stale `.pyc` can still be *read* by that import. The end-to-end trust property holds regardless, because the assembler runs the pinned verifier -- which loads the whole trust path from verified source bytes -- over its own output and propagates its exit code. Duplicating the ~90-line source-only loader into a third file is the remaining option and is deliberately not taken; it is the same twin-maintenance hazard already filed for the two dispatchers. status: open — accepted; revisit together with the dispatcher-loader deduplication entry.
status: open
decision: 2026-09-06 Build loader migration — Version the assembler, use the source-only loader, and remint/re-sign the resulting subject.
decision: 2026-09-06 Build loader migration — Version the assembler, use the source-only loader, and remint/re-sign the resulting subject.

### DW-453: The closed GitHub envelope schema has no recorded capture provenance and no tolerance path, so a future GitHub API field addition would block owner receipt re-collection with no documented remedy.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-7 landing (2026-08-25)"), 2026-08-30
location: tools/deployed_runtime_parity_handlers/v1.py:143
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The closed GitHub envelope schema has no recorded capture provenance and no tolerance path, so a future GitHub API field addition would block owner receipt re-collection with no documented remedy. evidence: `GITHUB_COMMENT_FIELDS`, `GITHUB_USER_FIELDS` and `GITHUB_REACTION_FIELDS` (`tools/deployed_runtime_parity_handlers/v1.py:143`) are exact key sets transcribed from the retained 2026-08 captures; `_exact_object` rejects any envelope carrying an unlisted key. The tuples already include fields GitHub added relatively recently (`user_view_type`, `minimized`, `pin`), which shows the payload shape does drift. Nothing records the capture command, the capture date, or the `X-GitHub-Api-Version` the tuples correspond to, and there is no remedy documented anywhere. Consequence for the one action gating this story: if GitHub adds a field before the three `#352` receipts are collected, every freshly captured source fails with `GitHub acceptance source schema is invalid`, and the operator's only path is to edit a subject-bound handler -- which re-mints and burns any receipt already collected in the same pass. Record the capture command and pin `X-GitHub-Api-Version` in the capture procedure, or split the tuples into required-plus-ignored sets. status: open — accepted for this packet; close before or during the next receipt collection.
status: open

### DW-454: TOCTOU gap between `require_no_symlink_components` and the later `stat()`/`open()` in `read_bounded_regular_snapshot`.

origin: migrated from legacy ledger ("Deferred from: code review of story-4-15-oq8-platform-closure-and-handoff (2026-08-30)"), 2026-08-30
location: tools/validate-oq8-platform-evidence.py:876-905
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` severity: low summary: TOCTOU gap between `require_no_symlink_components` and the later `stat()`/`open()` in `read_bounded_regular_snapshot`. evidence: `require_no_symlink_components` checks each path component for `is_symlink()`, then `read_bounded_regular_snapshot` independently calls `path.stat()`/`path.open()` afterward — a component could be swapped to a symlink in between. `tools/validate-oq8-platform-evidence.py:876-905`. status: open — deferred, low exploitability given this tool's single-writer CI trust boundary (it validates the project's own checked-out repo content, not attacker-controlled concurrent writers). Revisit by opening with `O_NOFOLLOW` or re-checking `st_mode` via `os.fstat` after opening.
status: open
decision: 2026-09-06 Remint hardened validator — Use descriptor-relative/no-follow reads, add race tests, and recollect Story 4.15 approvals.
decision: 2026-09-06 Remint hardened validator — Use descriptor-relative/no-follow reads, add race tests, and recollect Story 4.15 approvals.

### DW-455: `REVIEW_ROSTER` names two reviewers as specific accountable personas but the security role is only a generic role label.

status: done 2026-08-31
origin: migrated from legacy ledger ("Deferred from: code review of story-4-15-oq8-platform-closure-and-handoff (2026-08-30)"), 2026-08-30
archived: 2026-09-18

### DW-456: Repair the concurrent deferred-work ledger migration and its governance parser as one separately owned ledger-governance change.
origin: spec-deferred 5777fb182a87
location: _bmad-output/implementation-artifacts/deferred-work.md
source_spec: `spec-4-6-global-position-sharding-spec-renegotiation.md`
severity: high
reason: Review pass 5 reproduced 455 structured records that the bullet-only checker reports as an all-zero success, conflicting ledger and decision-journal identifiers, reopened accepted/resolved/closed work, lost structured provenance and severity, missing owner/review/grouping fields, malformed locations, and machine-local paths. These defects belong to the concurrent deferred-work migration and must not be edited or hidden by Story 4.6.
status: open

### DW-457: Story 4.15 v2 evidence packet's `docs/ci.md` gate-input pin drifted from an unrelated Story 3.15 doc update, blocking the closure validator.

origin: implement-step verification of story-4-15-oq8-platform-closure-and-handoff (2026-08-31)
location: tools/validate-oq8-platform-evidence.py (v2 gate-input check); docs/ci.md
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: `python3 tools/validate-oq8-platform-evidence.py` fails with `Story 4.15 v2 gate-input identity drift: docs/ci.md`. The v2 successor packet sealed a byte-hash pin on `docs/ci.md` at commit `83b32fcf` (2026-08-30 09:47+02:00), content-bound to the three recorded `reviews/{architecture,security,test}.json` receipts. Commit `75dc59aa` ("fix: update BMAD 6.11.1-next.33", 2026-08-30 12:36+02:00) then legitimately updated `docs/ci.md` — it is Story 3.15's own narration of its deployed-runtime-parity re-mint state (subject hash, receipt counts), unrelated to OQ8/Story 4.15. This is the same "sealed gate-input drift" class already tracked for `tools/validate-oq8-platform-evidence.py` itself (see the story's Review Findings blocker note, 2026-08-30), now hitting a second pinned path that another story continuously re-narrates. Fixing it means recomputing `docs/ci.md`'s hash and repropagating it through `source-artifact-identity.json` → `review-subject.json`, which invalidates the three existing reviewer receipts and needs fresh architecture/security/test sign-off — not a mechanical patch, and out of scope for an implementation pass per the frozen spec's "never fabricate reviewer approval." No code or evidence file was changed while investigating this.
status: open
decision: 2026-09-06 Remint and re-sign — Recompute the packet for the intended subject and collect all required non-fabricated reviewer approvals.
decision: 2026-09-06 Remint and re-sign — Recompute the packet for the intended subject and collect all required non-fabricated reviewer approvals.
decision: 2026-09-01 Re-mint and re-sign — Recompute the docs/ci.md identity, propagate the new subject, and collect fresh architecture, security, and test reviewer sign-off.

### DW-458: Prove Windows POSIX governance cases report real xUnit skips.

origin: migrated from legacy ledger ("unsectioned flat appends from spec-3-14-corrective-oci-provenance-release-2.md and spec-5-1-infrastructure-failure-cache-clear.md"), 2026-09-06
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs
reason: Contracts CI is Linux-only and all automatic lanes run on Ubuntu; `PosixGovernanceCasesSkipOnWindowsInsteadOfVacuousEarlyReturn` is only a tightened source-text binder that scans 280 characters after each Windows condition. A Windows host or OS-detection seam must run all seven cases and observe xUnit skip rather than a vacuous pass to settle runtime AC1.
status: open

### DW-459: Run heavyweight container-publishing provenance tests automatically.

origin: migrated from legacy ledger ("unsectioned flat append from spec-5-1-infrastructure-failure-cache-clear.md"), 2026-09-06
location: .github/workflows/ci.yml; tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs
reason: Concurrent Story 3.14 work excludes `HeavyweightContainerPublish` from the default Contracts gate, and no automatic workflow selects it, so multi-RID label and real-publish fail-closed regressions can merge unobserved.
status: open

### DW-460: Correct DW-372's overstated completion resolution.

origin: migrated from legacy ledger ("unsectioned flat append from spec-5-1-infrastructure-failure-cache-clear.md"), 2026-09-06
location: _bmad-output/implementation-artifacts/deferred-work.md (DW-372)
reason: DW-372 says `ContainerPublicationRejectsMalformedProvenanceInputs` is heavyweight and excluded, while its code, CI documentation, and manifest binder intentionally keep that direct-MSBuild theory unmarked and in the default gate.
status: open

### DW-461: Restore Story 4.7's historical creation date.

origin: migrated from legacy ledger ("unsectioned flat append from spec-5-1-infrastructure-failure-cache-clear.md"), 2026-09-06
location: _bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md
reason: The replanned specification says `created: 2026-09-05`, while its retained change log records the specification and inventory on 2026-08-27; replanning needs a separate timestamp rather than replacing the historical creation date.
status: open
decision: 2026-09-06 Restore and annotate — Set created to 2026-08-27 and record 2026-09-05 separately as the replanning date.
decision: 2026-09-06 Restore and annotate — Set created to 2026-08-27 and record 2026-09-05 separately as the replanning date.

### DW-462: Pin the first invalid retained-authority validity-window boundary.

origin: migrated from legacy ledger ("unsectioned flat appends from spec-5-1-infrastructure-failure-cache-clear.md and spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs
reason: The retained-authority release-evidence theory uses 90,001 seconds rather than the exact one-second-over-24-hours boundary of 86,401 seconds, so a validator regression that widens the approved 24-hour maximum can pass until the later threshold.
status: open

### DW-463: Reconcile Story 3.15 subject-history arithmetic.

origin: migrated from legacy ledger ("unsectioned flat append from spec-3-15-corrected-deployed-runtime-parity-closure.md, blind-hunter review 2026-09-05"), 2026-09-06
location: _bmad-output/implementation-artifacts/evidence/story-3-15 README; docs/ci.md; Story 3.15 operator records
reason: The superseded README, `docs/ci.md`, and Story 3.15 operator records disagree on whether subject history contains seven or eight subjects or re-mints. This is a narrative inconsistency found by the 2026-09-05 blind-hunter review and was not caused by the 3/3 receipt-collection landing.
status: open

### DW-464: Inspect NuGet signature entries before assembler attestation.

origin: migrated from legacy ledger ("unsectioned flat append from spec-3-15-corrected-deployed-runtime-parity-closure.md, edge/blind review 2026-09-05"), 2026-09-06
location: tools/assemble-corrected-deployed-runtime-parity.py
reason: The assembler always emits `repository_signature_entry_present: true` without inspecting each `.nupkg` for a `.signature.p7s` entry. The verifier still enforces the zip entry, but the producer's pre-existing representation is inaccurate.
status: open
decision: 2026-09-06 Build derived successor — Derive the field from validated entries in a versioned assembler and remint evidence.
decision: 2026-09-06 Build derived successor — Derive the field from validated entries in a versioned assembler and remint evidence.

### DW-465: Reject moderated or pinned retained GitHub comments.

origin: migrated from legacy ledger ("unsectioned flat append from spec-3-15-corrected-deployed-runtime-parity-closure.md, edge-case review 2026-09-05"), 2026-09-06
location: tools/deployed_runtime_parity_handlers/v1.py
reason: The retained GitHub comment closed schema requires `minimized` and `pin` fields but does not forbid non-null moderated or pinned states, so moderated or pinned comments can be accepted.
status: open
decision: 2026-09-06 Build stricter schema — Version the GitHub envelope schema, validate nullability/semantics, and recollect receipts.
decision: 2026-09-06 Build stricter schema — Version the GitHub envelope schema, validate nullability/semantics, and recollect receipts.

### DW-466: Harden assembler smoke refusal guards.

origin: migrated from legacy ledger ("unsectioned flat append from spec-3-15-corrected-deployed-runtime-parity-closure.md, edge-case review 2026-09-05"), 2026-09-06
location: tools/assemble-corrected-deployed-runtime-parity.py
reason: The pre-existing assembler preflight may accept JSON `false` for `exit_code` or set-equal swapped platform and child digests, weakening its smoke refusal checks; this was not introduced by receipt collection.
status: open
decision: 2026-09-06 Build strict successor — Require integer zero, preserve child roles/order, add adversarial tests, and remint the packet.
decision: 2026-09-06 Build strict successor — Require integer zero, preserve child roles/order, add adversarial tests, and remint the packet.

### DW-467: Align automatic container-publish coverage and DW-372's record.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: .github/workflows/ci.yml; _bmad-output/implementation-artifacts/deferred-work.md (DW-372)
reason: The default Contracts workflow filters out both `HeavyweightContainerPublish` real-publish theories, no automatic workflow selects the trait, and DW-372 incorrectly says the unmarked malformed-input direct-MSBuild theory is also excluded; actual OCI publication can regress behind synthetic coverage.
status: open

### DW-468: Enforce separator-free loop hook identifiers.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: .bmad-loop/bmad_loop_hook.py
reason: `bmad_loop_hook.py` interpolates `BMAD_LOOP_TASK_ID` and the event name directly into a filename and swallows the resulting `OSError`; because the external orchestrator producer is absent from the reviewed repository, its valid-character contract is needed to refute the risk or separators can silently drop completion events.
status: open

### DW-469: Review concurrent AggregateActor recovery and capacity behavior.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md, review pass 3"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs; tests/Hexalith.EventStore.Server.Tests
reason: Review pass 3 identified missing nonempty-index activation coverage, missing direct non-command cache-barrier coverage, and possible recovery or publication-index accounting defects in concurrently modified EventStore actor files. These concerns are outside Story 4.7's Tenants producer scope and need dedicated implementation and tests.
status: open

### DW-470: Review concurrent CI, release, documentation, and tooling findings.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md, review pass 3"), 2026-09-06
location: CI, release-boundary, documentation, and agent tooling components
reason: Review pass 3 identified unrelated gaps in container-test lane selection, release validity-boundary coverage, CI documentation, Windows structural tests, and agent or tooling behavior in the dirty tree. None is caused by the approved Tenants query-provenance change.
status: open

### DW-471: Make drain-exhaustion dead-letter publication idempotent.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md and code reviews of spec-5-1-infrastructure-failure-cache-clear on 2026-09-05 and 2026-09-06"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`CompleteDrainExhaustionAsync`; legacy locations 2152 and 2233)
reason: `CompleteDrainExhaustionAsync` publishes externally before saving the `DeadLettered` marker, so successful broker publication followed by a pre-commit marker-save failure is retried by the next reminder and republishes the exhausted range. No repository-owned consumer or sink contract proves that the stable CloudEvent id suppresses the duplicate; this is the pre-existing Story 4.4 non-transactional boundary preserved outside Story 5.1's frozen scope.
status: open
decision: 2026-09-06 Durable outbox — Persist an outbox/dead-letter intent transactionally, publish by stable identity, and mark completion after acknowledged delivery.
decision: 2026-09-06 Durable outbox — Persist an outbox/dead-letter intent transactionally, publish by stable identity, and mark completion after acknowledged delivery.

### DW-472: Restrict manual-snapshot success inference.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-473: Add stale-checkpoint handoff save-fault tests.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: tests/Hexalith.EventStore.Server.Tests (`InspectStaleHandoffSaveFailureAsync` coverage)
reason: Successful stale-handoff tests do not exercise `InspectStaleHandoffSaveFailureAsync`; add before-commit and commit-then-throw faults so a regression cannot surface an already committed handoff as a failed command or accept an incomplete durable handoff.
status: open

### DW-474: Add a pre-commit drain-retry persistence repair test.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-475: Establish actor-state batch safety after admission staging failures.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-476: Apply actor discard-or-poison handling to legacy idempotency paths.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-477: Preserve cancellation for aggregate event metadata reads.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-478: Fail closed on malformed publication-index activation.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (publication-index activation)
reason: A malformed entry before a valid duplicate is terminalized and adds the shared message id to the final prune set, removing the valid owner too. Malformed nonblank entries can also perform idempotency reads and saves without consuming either activation budget, so recovery must preserve valid duplicate owners and enforce work bounds.
status: open

### DW-479: Support handler-specific codec digests in corrective-release v4.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: docs/ci.md; tools/validate-corrective-release-evidence.py (`_load_handler`)
reason: `docs/ci.md` requires a v4 handler to define its own `EXPECTED_PACKET_CODEC_SHA256`, but `_load_handler` rejects any value different from `V3_PACKET_CODEC_SHA256`, so a correctly authored successor cannot load.
status: open
decision: 2026-09-06 Keep v4 deferred
decision: 2026-09-06 Keep v4 deferred

### DW-480: Finalize a reused stale-Processing slot after failed replacement admission.

status: done 2026-09-06
origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
archived: 2026-09-18

### DW-481: Refresh Story 4.7's deferred actor-review evidence.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: _bmad-output/implementation-artifacts/deferred-work.md (Story 4.7 pass-3 actor-review entry)
reason: The pass-3 ledger entry still cites missing nonempty-index activation and direct state-cache-barrier coverage, but `OnActivate_NonemptyIndex_ReconcilesPendingCountToDistinctOwners` and `PoisonedActor_StateBearingTurnsStopAtTheCacheBarrier` now exist. The remaining actor concerns need refreshed, accurate evidence.
status: open

### DW-482: Bound total activation-recovery scanning and continuation.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-1-infrastructure-failure-cache-clear (2026-09-05 and 2026-09-06)"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`RearmOutstandingPublicationsAsync`; legacy locations 2908 and 2984)
reason: Activation recovery has no total scan, read, or continuation bound: it reads drain records before charging the probe budget, skips armed entries without a total budget, and scans blank-message malformed entries without charging either budget. Large armed or blank-malformed indexes can monopolize activation and starve later entries on a continuously active actor; Story 5.1 bounds only malformed state-backed cleanup, so a cursor or total-scan budget needs separate design.
status: open

### DW-483: Treat touched `CurrentSequence == 0` streams as empty in `GetEventsAsync`.

origin: migrated from legacy ledger ("unsectioned flat append and 2026-09-06 code review of spec-5-1-infrastructure-failure-cache-clear"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`GetEventsAsync`; legacy location 1449)
reason: `GetEventsAsync` still throws `InvalidOperationException` for persisted metadata with `CurrentSequence == 0`, while `GetStreamMetadataAsync` and `ReadEventsRangeAsync` accept the same touched-empty stream and return empty results. Story 5.1 changed only metadata-read cancellation propagation, so this pre-existing read-contract inconsistency remains separate.
status: open

### DW-484: Bound Client domain-event marker transition persistence.

origin: migrated from legacy ledger ("unsectioned flat append from spec-5-1-infrastructure-failure-cache-clear.md"), 2026-09-06
location: src/Hexalith.EventStore.Client/Subscriptions/DaprEventStoreDomainEventMarkerStore.cs
reason: Concurrent Client marker work can throw during `TrySaveStateAsync` without a bounded retry, and the in-memory transition loop has no `MaxTransitionAttempts` cap; `TryAcquireAsync` remains a documented read-only acquire. Bound transition retries and save failures independently of Story 5.1.
status: open

### DW-485: Preserve rejection-event type fidelity during recovery.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-1-infrastructure-failure-cache-clear (2026-09-06)"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (legacy location 2097)
reason: Successful recovery of rejection events writes advisory status with `RejectionEventType: null`, losing rejection-type fidelity. `UnpublishedEventsRecord` does not retain that value and this behavior predates Story 5.1, so correcting all recovery paths requires separate status-contract work.
status: open

### DW-486: Make ownerless persisted drains rediscoverable after reminder failure.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-1-infrastructure-failure-cache-clear (2026-09-06)"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (legacy location 2748)
reason: At-capacity resume or stale-checkpoint handoff can persist a drain without a publication-index owner; if reminder registration then fails, activation cannot rediscover the unpublished range. This known Story 4.4 crash window predates Story 5.1 and requires separate publication-recovery policy design.
status: open
decision: 2026-09-06 Durable overflow cursor — Persist overflow ownership and process it through a bounded resumable cursor.
decision: 2026-09-06 Durable overflow cursor — Persist overflow ownership and process it through a bounded resumable cursor.

### DW-487: Make the Tenants handler-computed wire contract fail the owning repository's build.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/
reason: Story 4.7's flagship route and persisted-state proofs (`Generated_query_route_suppresses_projection_headers_for_handler_computed_result`, `Generated_tenants_api_get_tenant_reads_verified_redis_state_without_projection_authority`) live in `Hexalith.Tenants.IntegrationTests`, which Tenants CI runs only as `aspire-test-project` (`ci.yml:34`). That job inherits `continue-on-error: ${{ inputs.aspire-continue-on-error }}`, which defaults to `true` in `references/Hexalith.Builds/.github/workflows/domain-ci.yml:109-113` and is never overridden by Tenants. The Tier-3 test additionally carries `[DaprFact]` and calls `_fixture.SkipIfUnavailable()`, so it self-skips wherever local Dapr/Aspire is absent. A regression in the handler-computed header contract can therefore ship from Tenants with a green CI. Closing it means splitting the non-Dapr generated-controller class into a blocking project or setting `aspire-continue-on-error: false` for the non-performance tier — both CI-policy changes beyond Story 4.7's approved scope. Exposure is limited to cross-repository package drift because the emitter behavior itself has blocking coverage in `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs:227-269`.
status: open

### DW-488: Provenance-gate X-Hexalith-Served-At and X-Hexalith-Is-Degraded in the generated controller.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs (472-485)
reason: The emitter gates `X-Hexalith-Projection-Version` and `X-Hexalith-Is-Stale` on `provenance == QueryResponseProvenance.ProjectionBacked`, but emits `X-Hexalith-Served-At` on `metadata.ServedAt is not null` and `X-Hexalith-Is-Degraded` on `isDegraded is not null` with no provenance check. `ProjectionLifecyclePolicy.ProjectIsDegraded(Unknown, producerValue)` returns the producer's value verbatim, so a HandlerComputed producer that authors `IsDegraded` or `ServedAt` leaks freshness-adjacent headers the AD-15 contract says it may not claim. Story 4.7 closes this at the Tenants producer only; the platform guard remains fail-open for every other domain. Story 4.7's frozen boundary forbids editing the emitter. The new generated-controller test also leaves both fields unset, so the leak path is uncovered — a fix should set `ServedAt` and `IsDegraded` on the hostile fixture metadata and assert both headers are absent.
status: open

### DW-489: Correct the release-history record for the Story 4.7 producer change.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: references/Hexalith.Tenants commit 2a204a03
reason: Commit `2a204a03` is typed `refactor(tests): streamline TenantQueryResult and enhance query handler tests`, but it edits production source `src/Hexalith.Tenants/Queries/TenantQueryResult.cs` and removes `ProjectionVersion`, `IsStale`, `IsDegraded`, and `ServedAt` from produced query metadata for all six Tenants query routes. Under the pinned `conventional-changelog-angular` parser `refactor` produces no release and no changelog entry, so a behavioral change to shipped query responses is invisible in Tenants release notes. History is already published, so correction means a follow-up note rather than a rewrite.
status: open

### DW-490: Restore a reachable projection-confirmation path for Tenants command flows.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs (2517)
reason: `HasSupportedProjectionVersion` requires a non-blank `QueryResponseMetadata.ProjectionVersion`, and `TenantSetConfigurationPreview`/`TenantRemoveConfigurationPreview` return `Unavailable` when it is false (lines 345, 412); the 304 paths degrade to `GatewayFailure` (lines 701, 864). No Tenants route can ever supply that value: `QueriesController.NormalizeProducerMetadata` nulls `ProjectionVersion` for every non-`ProjectionBacked` provenance, and the generated controller emits the header only for `ProjectionBacked`. All six Tenants handlers are `HandlerComputed`, so the gate is unsatisfiable on the wire. This predates Story 4.7 — the change only makes it unambiguous by removing the producer-side alias as well. `TenantQueryGatewayTests` masks it by feeding metadata with `ProjectionVersion = "tenant-sequence:41"`, a value the real wire cannot deliver, so those gate paths are green only in an unreachable state. Story 4.7's frozen boundary forbids touching UI behavior.
status: open

### DW-491: Retire or re-scope ReadModelFreshnessExtensions.ToQueryResponseMetadata.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs (62-84)
reason: After Story 4.7 removed the last Tenants caller, the helper has no production caller anywhere in the workspace — only `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs:79` invoke it. Its XML documentation still presents it as the supported way for a domain handler to author `ProjectionVersion`/`IsStale`/`ServedAt`, i.e. it advertises exactly the producer-authored authority AD-15 forbids for non-projection-backed routes. Only EventStore's normalization stands between a future domain author following that doc and reintroducing the Story 4.7 defect.
status: open

### DW-492: Align the Tenants UI truth-state spec with the unreachable 304 freshness primitive.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: references/Hexalith.Tenants/docs/tenants-ui-truth-state-and-action-availability-spec.md (102)
reason: The document still states that "the freshness primitive is `If-None-Match` -> `304 Not Modified`, served by the REST-backed Tenants read endpoints". After Story 4.7 the Tenants routes never emit an ETag and EventStore suppresses `IsNotModified` for non-projection-backed provenance, so 304 is unreachable on those endpoints. Neither the spec, the CHANGELOG, nor a prior ledger entry records the change. The fix edits a separate specification document and so is routed out of this review.
status: open

### DW-493: Remove or re-activate the inert Tenants read-model freshness configuration.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs (71-75)
reason: After Story 4.7, `TenantQueryResult.FromPayload`'s six-argument overload discards `readModel`, `thresholds`, and `now`, so nothing downstream can observe freshness inputs. `ReadModelFreshnessOptions` is nevertheless still bound from configuration, validated by `IsValidReadModelFreshnessOptions`, and `.ValidateOnStart()`-ed, and `_freshnessThresholds`/`_timeProvider` are still constructed and threaded through all six query handlers (`Queries/Handlers/TenantQueryHandlerBase.cs:45,76,156-166`). An operator can therefore set `ReadModelFreshness:Aging` and `:Stale`, have them accepted and validated at startup, and get no observable effect anywhere. Two comments also still describe `ToQueryResponseMetadata` as the live path (`src/Hexalith.Tenants.UI/Services/Gateways/TenantsRestQueryClient.cs:391`, `tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:2934`). Story 4.7's frozen Design Notes deliberately preserve the overload signature for caller stability, so removing the dead surface contradicts the approved spec and needs its own story.
status: open
decision: 2026-09-06 Keep as-is — Administrator accepted the frozen signature and host binding unchanged for Story 4.7; cleanup routed here.

### DW-494: Restore a runnable full-solution dual-graph validation lane for Hexalith.Tenants.

origin: code review of spec-4-7-tenants-query-provenance-follow-up (2026-09-06)
location: references/Hexalith.Tenants/Hexalith.Tenants.slnx
reason: Story 4.7's AC4 requires fresh Debug/source and Release/package restores plus per-project tests in both graphs. Both full-solution restores are blocked because `Hexalith.Tenants.slnx` explicitly lists projects from uninitialized nested Commons, EventStore, FrontComposer, and Memories submodules, and the approved boundary forbids initializing them. The Debug/source Integration build additionally stops at `references/Hexalith.Memories/Directory.Build.props:89` (absent nested EventStore) and the Release/package build at `src/Hexalith.Tenants.AppHost/Program.cs:132` (pre-existing `CS1503` Dapr-component API skew). The dual-graph guarantee AC4 describes is therefore never demonstrated end to end; only focused per-project lanes run.
status: open
decision: 2026-09-06 Accept focused-lane evidence — Administrator accepted the recorded focused results in place of the blocked broad gate for Story 4.7 closure.

### DW-495: Persist recovered handler query-type indexes when sibling domain metadata fails.

status: done 2026-09-06
origin: administrator authorization 2026-09-06 after Story 4.7 AC3 halt (P7-BH-07)
archived: 2026-09-18

### DW-496: Claimed current-source proofs hash frozen Git, not HEAD or the worktree.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-06 Group A)
location: tools/validate-oq8-platform-evidence.py:893,1839-2006,1491-1568,2278-2364
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: v1 JSON, SDK successor bindingRule, and v2 bindingRule/currentRule claim current HEAD/worktree/candidate bytes, but the checker hashes frozen commits. v3 reads live disk for only six gate-input paths, not the 24 capability paths. Implementing live HEAD proof against the original 24 paths would fail on current main. Remint with DW-457; keep v1/v2 historical and put live proof on a reduced v3 path set — do not freeze the original 24 capability paths.
status: open
decision: 2026-09-06 Defer to DW-457 remint — Remint with DW-457; keep v1/v2 historical and put live proof on a reduced v3 path set — do not freeze the original 24 capability paths.

### DW-497: Full validator requires Story 4.15 tracking already review/done before the packet can pass.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-06 Group A)
location: tools/validate-oq8-platform-evidence.py:3697-3700,3522-3548
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: Default validation calls validate_status_and_documents(final=True), requiring sprint 4-15 status review and spec frontmatter done. Frozen Always says advance tracking only when the fail-closed validator passes. Isolated --lifecycle-mode final is not the bypass. Keep spec-done / sprint-review split; do not invert the lifecycle gate or renegotiate frozen Always in this Group A pass.
status: done
decision: 2026-09-20 Resolve through Story 4.15 v4 lifecycle separation — Default validation now proves the complete active v4 packet before consulting a bounded mutable lifecycle record; ready-to-close and closed select exact review/done and done/done pairs.
resolution: Story 4.15 v4 replaced the Boolean final gate with candidate/final/closed phases, preserved v3 as immutable historical evidence, and verified the ready-to-close transition before atomically selecting closed tracking.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Story 3.15's required `docs/ci.md` update leaves the Story 4.15 v3 successor packet unbound, so the complete Contracts suite fails on current-source identity drift until that separately reviewed packet is reminted.
  evidence: Focused Story 3.15/3.14 classes are green; the 12 full-suite failures are `Story 4.15 v3 current source identity drift: docs/ci.md`. Reminting v3 would invalidate its approvals and is outside this story.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `.gitattributes` LF-pins `evidence/story-4-15-successors/v2/**` while `docs/ci.md` names v3 as the active hash-bound lineage.
  evidence: Story 4.15 successor ownership; not part of the Story 3.15 parity-closure intent.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The withdrawn `gate-decision.json` remint chain still ends at `86c59c79` after the Group A subject `84dee6e5`.
  evidence: The artifact is already marked SUPERSEDED in every status field; regeneration belongs to the trace workflow, not the parity verifier.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: The canonical subject now binds the current capture-tool digest, but retained Production smokes were captured on 2026-08-21 before later capture hardening.
  evidence: Recapture is Ask First. Smoke logs remain canonical JSON restatements of `smoke-results.json` (already deferred). The bound producer cannot reproduce those retained bytes.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Assembler restore runs only for incomplete verifier children (`TimeoutExpired` / spawn failure), not for a completed wait with a negative `returncode`.
  evidence: Unverified whether a signal-killed verifier is reachable for operators. If true, a success-shaped `closure.json` could remain after an incomplete run; severity would be medium. Settle by reproducing a negative `returncode` from the pinned verifier child.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 timeout cleanup treats every nonzero container-inspect result as if no container exists and lacks exception-path coverage.
  evidence: The current helper returns cleanup success for daemon, permission, spawn, and timeout failures as well as not-found; BH-04, EH-01, VG-03, and VG-O2 verified the shared cause.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 performs only one immediate inspect after timeout, so a late-created container can escape cleanup.
  evidence: BH-05 and EH-02 found no polling or cleanup-budget window between verifier timeout and the sole inspect.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 discards the force-remove return code and stderr when timed-out container cleanup fails.
  evidence: BH-06 verified that the removal result is reduced to a Boolean, leaving operators without the failing command's diagnostics.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 inspects a container by name, discards the inspected ID, and then removes by name, permitting a replacement race.
  evidence: EH-03 identified a window in which the name can be rebound and the replacement container can be force-removed.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 can leave a new success-shaped closure after rollback restoration fails, and its test does not assert the resulting file state.
  evidence: BH-07, BH-09, EH-04, VG-01, and VG-O1 verified that the restore `OSError` path returns without removing or quarantining the new closure and the test checks only exit/text.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 rollback restores only `closure.json`, not the registry, inventory, or subject artifacts rewritten before verification.
  evidence: BH-08 verified that an incomplete verifier can leave a packet whose closure and supporting provenance artifacts come from different assembly attempts.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15's off-bound-path test validates the repository closure instead of the temporary packet supplied to the assembler.
  evidence: BH-10 traced the postcondition hash to the untouched repository artifact while the exercised assembler writes the fixture packet.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 lets a signal-terminated verifier bypass incomplete-child rollback.
  evidence: BH-11 and EH-05 verified that a negative child return code follows the completed-run branch and keeps the newly written unverified closure.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 does not provenance-bind the executed deployed-runtime parity handler package initializer.
  evidence: BH-13 verified that Python executes `tools/deployed_runtime_parity_handlers/__init__.py` before `v1.py`, while the closure and dispatch bind only `v1.py`.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 lifecycle records disagree: its spec says done, sprint status says review, and the packet remains 0/3.
  evidence: BH-14 verified the contradictory status sources and incomplete approval count.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 remint, subject, and receipt-less-subject counts were not advanced after subject `a5c07d17`.
  evidence: BH-15 and EH-07 verified that the records still report eight remints, nine subjects, and five receipt-less subjects.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15's recorded assembler output names prior subject `84dee6e5` while the canonical validator record names `a5c07d17`.
  evidence: BH-16 and EH-08 verified the adjacent provenance records disagree on the assembled subject.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 binds the current capture producer digest to retained August smoke bytes that producer cannot reproduce.
  evidence: BH-17 verified that the canonical smokes predate later capture hardening and therefore cannot be regenerated byte-for-byte by the bound producer.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 assemblers have no packet lock, allowing overlapping rewrites and cross-invocation timeout rollback.
  evidence: EH-06 verified that concurrent invocations can mutate the same packet and one invocation can overwrite another's verified output during rollback.

- source_spec: `_bmad-output/implementation-artifacts/spec-update-all-hexalith-packages-to-latest.md`
  summary: Story 3.15 lacks timeout-removal coverage for first-time assembly when no previous closure exists.
  evidence: VG-02 verified that every timeout fixture begins with prior closure bytes, leaving the `previous_bytes is None` deletion branch untested.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: EventStore fail-opens Tenants queries to the projection-actor path when AdminOperationalIndexHostedService skips `admin:query-types:tenants` after a sample metadata failure.
  evidence: P7-BH-07 verified Event 6101 skip plus Event 6100 sample InvalidOperationException, after which `eventstore||admin:query-types:tenants` stayed missing and `DaprDomainQueryHandlerRegistry` treated the domain as having no handlers. Administrator authorized a separate EventStore repair on 2026-09-06 (DW-495): persist recovered `admin:query-types:{domain}` writes without waiting for every sibling metadata source.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Concurrent EventStore marker, actor, Admin UI, and Story 3.15 tooling defects remain in the baseline-wide review subject.
  evidence: P7-ECH-01 through P7-ECH-10 and P7-ECH-14/P7-ECH-15 verified those outcomes in EventStore/Admin/3.15 files that Story 4.7 did not change.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: An empty recovered handler query-type list leaves any prior `admin:query-types:{domain}` catalog in place, so EventStore can keep routing dropped handler types.
  evidence: P8-BH-05, P8-ECH-10, and P8-VG-02 verified `WriteDomainQueryTypeIndexAsync` returns false with no `DeleteStateAsync` when normalized types are empty, and hosted-service tests never seed a leftover catalog before recovering `[]`.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: The in-memory domain-event marker store disagrees with the Dapr store on illegal states and transition retry bounds.
  evidence: P8-BH-07 and P8-ECH-07 verified unknown states return cast `-1` in memory while Dapr throws; P8-ECH-08 verified in-memory `Transition` retries until cancel versus Dapr's five-attempt cap.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Binding `get-tenant-audit` cursors to requester identity can invalidate previously issued audit cursors without a purpose-version bump.
  evidence: P8-BH-10 verified `GetTenantAuditQueryHandler` now scopes cursors with `envelope.UserId` via `TenantQueryCursorScopes.GetTenantAudit`; that handler is outside the Story 4.7 Code Map.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: A single unknown or category-mismatched tenant audit event fails the entire audit page as `InvalidPayload`.
  evidence: P8-BH-11 verified `IsValidTenantAuditPayload` returns false on the first unsupported `EventType`/`Category` pair, with no mixed-page coverage.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Tenant audit identifier safety matches unsafe fragments against an alphanumeric-only collapsed string and rejects legitimate ids that contain those fragments.
  evidence: P8-BH-12 verified `TenantAuditSupportSafety.IsSafe` inspects a letter-or-digit-only form, so values such as `token-service` or ids containing `eyj` are treated as unsafe.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: An Access audit row with a missing or unsafe narrative user id uses the tenant id as the correction target.
  evidence: P8-ECH-02 verified `TenantAuditRow.FromEntry` sets `target` to `narrative.UserId ?? narrative.ConfigurationKey ?? tenantId` with no Access-category invalidation.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Completing a terminal aggregate-command reconciliation after the owner has left clears the retained result so a later owner cannot adopt it.
  evidence: P8-ECH-03 verified `TryCompleteReconciliationDispatch` nulls `Reconciliation` and sets `IsReleased` when the lifecycle is terminal and `CurrentOwner` is null.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: A later exception in global-administrator remove dispatch can overwrite an accepted lease with `Ambiguous`.
  evidence: P8-ECH-04 verified the `catch` still runs `ApplySubmissionFailure` / `TryCompleteReconciliationDispatch` even after an earlier accepted dispatch.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Tenant detail audit retention can treat another caller's snapshot as in-scope because it uses the caller-free `MatchesScope` overload.
  evidence: P8-ECH-06 verified `TenantDetailPage` calls `audit.MatchesScope(request)` while `TenantQueryGateway` binds retained evidence with `MatchesScope(request, callerScope)`.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Admin Consistency has no Operator+Running test that Cancel is hidden, even though the control is Admin-gated in markup.
  evidence: P8-VG-03 verified `Consistency_CancelButtonOnlyForRunningChecks` uses the default Admin identity and the Operator opaque-check test uses a Completed row.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: `Consistency_ShowsTriggerButton_ForOperatorUser` never configures an Operator identity.
  evidence: P8-VG-O1 verified the method uses `AdminUITestContext`'s default Admin user and never calls `ConfigureRole(AdminRole.Operator)`.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-07)

- `git_diff_is_clean` in `tools/validate-oq8-platform-evidence.py:893` has no callers. Group B tests that dirty a worktree therefore cannot be backed by a production current-bound-source proof. Pre-existing validator gap; already tracked as DW-496. Not caused by `Oq8PlatformClosureTests.cs`.
- Owner accepted renaming `ChangedOrDeletedLaterWorktreePathDoesNotRewriteHistoricalV1`, `CurrentIndexVisibilityFlagsDoNotAlterHistoricalV1`, and `NonDescendantCurrentHeadDoesNotReplaceHistoricalV1Snapshot` to the sealed `FailsClosed` command names, keeping pass assertions. Applied 2026-09-08 during the authorized v3 remint. Live fail-closed Git identity remains DW-496.
- Owner accepted deleting unused `candidate-test-source-body` from `ApplyCandidateMutation` / `ExpectedCandidateFailure`. Applied 2026-09-08 during the authorized v3 remint. `candidate-subject-test-binding` and `candidate-execution-test-source` still cover the hash fields.
- `PreReviewModeRejectsCaptureAndSupportArguments` does not send `--lifecycle-mode`. Deferred: capture/support already hit the exclusivity branch; a third sealed case is not worth a remint.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-06)

- Restated TOCTOU between `require_no_symlink_components` and `stat()`/`open()` in `read_bounded_regular_snapshot` (`tools/validate-oq8-platform-evidence.py:1049-1063`). Already tracked as DW-454; no new DW. Same single-writer CI trust boundary as the 2026-08-30 Group A defer.
- DW-496: Claimed current-source proofs hash frozen Git, not HEAD or the worktree. Remint with DW-457; keep v1/v2 historical and put live proof on a reduced v3 path set — do not freeze the original 24 capability paths.
- DW-497: Full validator requires Story 4.15 tracking already review/done before the packet can pass. Keep spec-done / sprint-review split; do not invert the lifecycle gate or renegotiate frozen Always in this Group A pass.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Browser-level proof of exact destructive-dialog focus restoration needs an authenticated Admin UI E2E fixture with controllable denial responses.
  evidence: The current Playwright fixture lacks `EventStore:Authentication:Issuer`, renders anonymously after token creation fails, and has no backend capable of a write-time 403; an attempted test failed before the dialog initiators rendered.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Baseline-window sprint-status.yaml edits belong to Epic 3 tracking, not Admin surface hygiene.
  evidence: Commits after the Story 5.4 baseline rewrite guarded comments, 3-15/3-16, and retro keys; Story 5.4 code does not own that tracker.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: OQ8 validator remints and Builds/FrontComposer/Memories gitlink moves are later-story work present in the same baseline diff.
  evidence: Story 5.4 forbids entering later DAPR/topology stories; those hunks sit beside the Admin hygiene change-set.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: sprint-status marks epic-3-retrospective done while the 2026-09-07 retro file is still verdict rejected.
  evidence: The mismatch is Epic 3 bookkeeping, not an Admin discovery, MCP, CLI, or confirmation-facts defect.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: InitiatorFocusService may race Fluent dialog unmount; browser activeElement proof remains unverified.
  evidence: RestoreAsync invokes hexalithAdmin.focusElementById immediately after HideAsync/StateHasChanged. A Playwright run with an authenticated Admin UI and a write-time 403 would settle whether the trap still owns focus.

- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-folded-snapshot-frozen-spec.md`
  summary: Story 5.4 Create Backup and Snapshots 401/403 confirm paths close dialogs and restore focus, but page tests only cover cancel and delete-policy denial.
  evidence: Blind-hunter BH-11, edge-case EC-3–EC-8, and verification-gap VG-1/VG-2 cite `Backups.razor` and `Snapshots.razor` confirm catches with no `TriggerBackupAsync`/`SetSnapshotPolicyAsync`/`CreateSnapshotAsync` Forbidden theory. Those files are not Story 6.1's deliverable.

- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-folded-snapshot-frozen-spec.md`
  summary: OQ8 remint, symlink-skip, and process-kill harness gaps remain Story 4.15 work.
  evidence: BH-12, BH-14, EC-1, and EC-2 cite `Oq8PlatformClosureTests.cs` and the v3 packet. Story 6.1 forbids runtime and test edits.

- source_spec: `_bmad-output/implementation-artifacts/spec-6-1-folded-snapshot-frozen-spec.md`
  summary: Neighboring 5.4 and 4.7 story-status ledgers disagree with sprint-status in the same baseline-wide diff.
  evidence: BH-13 notes `spec-5-4` done vs tracker review, and `spec-4-7` moving to in-progress. Not caused by the folded-snapshot artifact.

## Deferred from: code review of story-4.15 Group D (2026-09-09)

- `.github/workflows/ci.yml` is a named review-subject binding (`ciWorkflow`) but resolves only through `PRIOR_ROOT_BINDING_HASHES[".github/workflows/ci.yml"] = "6a28bd96…"` via `sha256_git_file(COMPLETED_V1_CLOSURE_COMMIT, …)` (`tools/validate-oq8-platform-evidence.py:3181-3186`), never against live bytes. Current `ci.yml` is `80f90b86…`, and unlike `integration.yml` it is not a v2 or v3 `gateInput`, so the 2026-09 restructuring of that workflow is invisible to the closure validator. Deferred: DW-496's frozen-Git-vs-live-bytes asymmetry extended to a bound workflow; rides the same agreed remint.
- `--filter-not-trait "Category=HeavyweightContainerPublish"` (`.github/workflows/ci.yml:93`) leaves `CorrectiveOciProvenanceReleaseTests.RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` and `…ContainerPublicationRejectsMissingProvenanceInputs` selected by no workflow; only an attribute-position source check remains, which would pass unchanged if publication provenance broke. Deferred: duplicate of the accepted gap at `deferred-work.md:3534,3598`; needs a scheduled heavyweight lane.
- The `ci / contracts` job does a cold `dotnet restore` with no `~/.nuget/packages` cache under a 25-minute timeout, unlike `integration.yml` which caches keyed on `global.json`/`Directory.Packages.props`/`**/*.csproj`. Deferred: performance and consistency only, no correctness impact, outside the story's evidence scope.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff Group E (2026-09-09)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The new `CorrectedLifecycleRowsRetainTheirCorrectedStatus` guard runs only in `ci / contracts`, which the live main ruleset does not require, so re-flipping any of the four pinned lifecycle rows fails no blocking check.
  evidence: Required contexts are `advisory`, `ci / build-and-test`, `ci / tenants-source-mode`, `codeql / analyze`, `commitlint / commitlint`, `dependency-review / dependency-review`, `live-sidecar`. The OQ8 validator's `expected_statuses` (`tools/validate-oq8-platform-evidence.py:3525-3533`) excludes `4-6`, `4-7`, `3-15` and `5-3`. Promoting the check is a repository-settings change; already carried as the Group D decision item on the required-check lane.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Rationale comments added inside `development_status` do not survive a tracker regeneration.
  evidence: `build_status` rebuilds `development_status` as a fresh `CommentedMap` and re-adds only a blank line before each epic key (`.claude/skills/bmad-sprint-planning/scripts/sprint_plan.py:290-296`), so the ~20 new comment lines and the `>>> GUARDED <<<` fences are dropped by `sprint_plan.py generate`. Pre-existing fragility that the accepted fence mitigation already acknowledges.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: `epic-3-retro-2026-09-07.md:680` still states the wrong "two sprint-status comment lines" count that this change corrected in the action item.
  evidence: The `epic-3-retro-item-23` action text in `sprint-status.yaml` now records the five-line contiguous block requirement, but the retrospective it cites as `ref:` was not updated. That file is outside this diff and amending an approved retrospective is a separate authority.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Three OQ8 identity gaps restated by the acceptance audit are already open Group D items and each needs the v3 remint.
  evidence: `4-15-oq8-platform-closure-successor.json` pins `selectedOn: 2026-08-29` and asserts `eventStorePlatformComplete: true` against a validator and test file replaced twice since; `integration.yml` proves OQ8 on Dapr runtime `1.18.2` while `COMMITTED_DAPR_RUNTIME_VERSION = "1.18.1"` (`validate-oq8-platform-evidence.py:79`) is what `observations.json` is checked against; the 4.8 ledger's mutable `review` cell sits inside a live-hashed sealed body.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff Group F (2026-09-09)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The architecture spine now asserts DAPR runtime `1.18.2` while sealed OQ8 evidence is validated against `1.18.1`, with no crosswalk of the transition.
  evidence: `_bmad-output/planning-artifacts/architecture.md:330` records "CI `1.18.2`; deployment examples `1.18.0`" and `.memlog.md` repeats it; `COMMITTED_DAPR_RUNTIME_VERSION = "1.18.1"` (`tools/validate-oq8-platform-evidence.py:79`) is what `observations.json` is checked against. This is the open Group D patch widened to a new surface; it rides the same agreed v3 remint rather than opening the sealed zone.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: `epics.md` pins a stale SHA-256 for `prd.md` under `inputDocumentDigests`.
  evidence: `epics.md:14` pins `8f9c88e8b8665c2d…` while the file hashes `b99effdb414209…`. Pre-existing, introduced by `12d2dfc1` rather than by the Group F commit; fold into the same re-pin pass as the architecture digest, which is filed as an open patch.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The Group D deferred-work block uses bare prose bullets outside both sanctioned ledger formats, permanently, in an append-only file.
  evidence: The `## Deferred from: code review of story-4.15 Group D (2026-09-09)` block carries three bullets with no `source_spec:`/`summary:`/`evidence:` keys and no `DW-###` id. Pre-existing from `12d2dfc1` and already filed as an open Group E patch; the Group E and Group F blocks appended after it do use the sanctioned flat form.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The fourth Group E deferred-work entry bundles three distinct issues under one un-idded block, so sweep cannot triage or close them individually.
  evidence: That entry folds the `4-15-oq8-platform-closure-successor.json` generation freeze, the Dapr `1.18.2`/`1.18.1` gap, and the 4.8 live-hashed `review` cell into a single block. The flat block form itself is sanctioned; splitting is housekeeping with no correctness impact.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Docs-only commits are typed `feat:`, inflating the semantic version; the habit is systemic rather than a single bad commit.
  evidence: Between `v3.103.0` and `e302432c` there are five `feat:` commits, three of them documentation-only (`feat: add sprint change proposals…`, `feat: add epic 6 context…`, `feat: add reviews for brownfield traceability…`). The minor bump is therefore already locked in by earlier commits and retyping `e302432c` changes the next version by nothing, while rewriting it means force-pushing `main` past its ruleset. Deferred by owner decision 2026-09-09: accept this window's bump and address the pattern as a commit-convention rule (commitlint scope or path check) rather than per-commit history edits.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Four of the five `epics.md` `inputDocumentDigests` pins are stale after concurrent commit `0825f0dc`; only `architecture.md` was re-pinned.
  evidence: Measured 2026-09-09 after `0825f0dc`: `prd.md` pinned `8f9c88e8…` vs actual `b99effdb414209…`; `DESIGN.md` pinned `3be78b6b…` vs actual `d2185a22cdd9c3…`; `EXPERIENCE.md` pinned `6a058112…` vs actual `b392b7c430e42a…`; `ux.md` pinned `3c827e92…` vs actual `c839bd6a3b24ad…`. `architecture.md` was re-pinned to `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` by code review Story 4.15 Group F. The other four were rewritten by commits outside this review's scope and the UX working files were still dirty at measurement time, so pinning them would bind a moving target. Re-pin them in the commit that settles that work. No test or tool recomputes these digests, so the drift is invisible to every check.

## Deferred from: BMad Build review of Story 4.15 Group G (2026-09-09)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The break-glass release path can publish after commitlint alone without a successful CI conclusion.
  evidence: `.github/workflows/release.yml` deliberately substitutes commitlint when `bypass-validation=true`; protected-environment approval mitigates but does not prove the dispatched source builds or passes tests.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The legacy release job retains unused write-scoped identity-token and attestation permissions.
  evidence: The job grants `id-token` and attestation writes while no current step consumes them, unnecessarily increasing the impact of a compromised release dependency.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Required live-sidecar CI captures OQ8 evidence but no required context validates the committed closure packet.
  evidence: The live-sidecar lane is capture-only, while the full closure validator runs in `ci / contracts`, which is absent from the repository's required status contexts.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Operations dead-letter authorization does not provide one end-to-end authenticated caller boundary for both HTTP and actor routes.
  evidence: The HTTP helper accepts any nonempty bearer alongside the configured Dapr caller ID, and Dapr actor methods do not independently apply that caller policy even though the target sidecar supplies the app-channel token.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Dead-letter retention and its single global index grow without compaction and impose cumulative quadratic work.
  evidence: Records retain raw bodies indefinitely; capture rewrites the full index, and activation/backlog observation scans all indexed entries, so storage and processing cost grow with historical traffic.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: An operator retry request waits for the sequential replay drain after durable intent has already been accepted.
  evidence: `RetryAsync` saves state and arms its reminder, then awaits `DrainReplayRequestsAsync`, allowing large backlogs to hold the HTTP invocation open and invite duplicate operator retries after timeout.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Permanent replay HTTP failures are retried as if transient until the replay-attempt budget is exhausted.
  evidence: HTTP 400, 401, 403, and 404 receive reason codes but follow the same requeue path as transient failures unless the numeric attempt limit has been reached.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Terminally rejected dead-letter captures can be acknowledged without any durable recovery record.
  evidence: Oversize, empty, hash-conflicting, and unretainable messages emit telemetry but are not durably retained, so acknowledged data can be irrecoverably lost.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Exhausted startup reconciliation can leave dead-letter health and backlog telemetry falsely clear.
  evidence: The reconciler exits after bounded retries without a durable failure state, health signal, metric, or diagnostic that distinguishes reconciliation failure from an empty backlog.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The OQ8 Python bootstrap is version-pinned but neither artifact-hash-pinned nor structurally bound to the workflow step that executes it.
  evidence: `requirements-oq8.txt` has no hashes and pip omits `--require-hashes`; validator workflow checks are unscoped substrings that could survive in comments or unrelated steps after the actual bootstrap drifts.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Generated Playwright artifacts with browser session material are tracked in source control.
  evidence: Tracked `.playwright-cli` network traces include antiforgery cookies and ephemeral SignalR connection tokens, exposing session material and adding generated trace bulk to repository history.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Validation performed with a dirty `Hexalith.Tenants` submodule worktree is not reproducible from the superproject diff.
  evidence: Git reports the pinned `54fc4040dc6348e5560fffc246a27e72dc6558fe` gitlink with a dirty suffix; uncommitted nested bytes are not represented by the owning repository's patch.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Active v3 closure intentionally leaves older OQ8 capability paths outside its reduced current-source binding set.
  evidence: Historical v1 identities remain frozen while v3 checks its explicit gate inputs; a later change to an older, unbound capability path can coexist with a passing v3 validator under the accepted DW-496 authority boundary.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Story frontmatter status parsing can miss YAML-equivalent duplicate keys.
  evidence: `parse_unique_frontmatter_status` recognizes only the exact unquoted `status:` spelling, so a quoted key or a space before the colon can evade its ambiguity count and be interpreted differently by another YAML consumer.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Workflow guardrails can miss equivalent Server.Tests invocations and alternate Dapr-version override forms.
  evidence: Contract tests forbid one literal `dotnet test` path and recognize narrow Dapr quoting/location patterns; path variants, solution invocations, double quoting, or job-level values can change effective execution while the expected literal remains elsewhere.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Dead-letter HTTP inputs are not consistently rejected before actor dispatch.
  evidence: A malformed continuation token silently restarts at offset zero, while a malformed tenant identifier can reach actor construction and surface as a server error instead of a bounded client error.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Malformed blank publication-index entries can force unbounded activation scanning and repeated diagnostics.
  evidence: Blank invalid entries do not consume the probe budget, so a corrupted index can traverse an arbitrarily large malformed prefix before reaching valid work.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Provider verification lacks a real-Kestrel authorized Pact interaction that proves generated credential propagation.
  evidence: The verifier injects a credential and the handler validates it, but the only real-Kestrel interaction is unauthorized and succeeds independently of whether the generated credential reaches the provider.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Aspire authentication tests do not prove bounded validation for partial or unsupported audience parameters.
  evidence: Tests cover a valid audience pair and an invalid token endpoint but not either missing half of the pair or an unsupported parameter name before AppHost mutation.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-09, Group H)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The v3 test receipt's 384 / 1952 test counts are not reproducible from any recorded execution.
  evidence: `evidence/story-4-15-successors/v3/reviews/test.json` attests `contracts-full` 1952/1952 and the validator pins `V3_FULL_CONTRACTS_TEST_COUNT = 1952`, but the spec's Completion Verification records only an isolated `Oq8PlatformClosureTests` run (384), a solution build, and a Contracts build; the live tree reports 259/384 focused failures. Unverified, would be high if false. Settle by running `V3_CONTRACTS_TEST_COMMAND` on a story-isolated worktree of the exact remint and recording the result location in `pre-review-execution.json` or the receipt findings. Settled during the Group H reseal (2026-09-09): `V3_CONTRACTS_TEST_COMMAND` ran on the story-isolated worktree of the resealed remint and reported 1966/1966 (TRX `TestResults/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.trx`), matching the resealed `V3_FULL_CONTRACTS_TEST_COUNT = 1966` and the v3 test receipt; the spec's Group H Completion Verification records the run.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-09, Group I)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The Group H deferred-work row still summarizes the 384/1952 reproducibility gap as open while its evidence paragraph says it was settled.
  evidence: The Group H `summary` still reads “The v3 test receipt's 384 / 1952 test counts are not reproducible from any recorded execution,” while the same entry's `evidence` field records the isolated 1966/1966 `V3_CONTRACTS_TEST_COMMAND` run that settled it. The append-only ledger cannot rewrite that summary; `bmad-loop-sweep` will keep seeing an open unreproducible-count claim. Settlement is already in the evidence paragraph and the spec's Group H Completion Verification.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-09, Group J)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: v1 public-document proofs hash live working-tree files rather than the v1 Git snapshot.
  evidence: `validate_review_subject` compares `sha256_file(ROOT / relative)` to `EXPECTED_DOCUMENT_HASHES` (`tools/validate-oq8-platform-evidence.py:3282-3284`) while other historical bindings use `sha256_git_file(COMPLETED_V1_CLOSURE_COMMIT, …)`. Later JWT-doc work therefore breaks Story 4.15 without a v3 gate-input change. Pre-existing; belongs to the later public-document group.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: `--pre-review` still does not pin the full selector historical object after the Group I partial pin.
  evidence: `validate_successor_selector_sdk_link` now checks authority, v1 `packetSha256`, v2 `manifestSha256`, SDK directory, and one SDK file hash (`tools/validate-oq8-platform-evidence.py:3658-3682`). `validate_successor_selector_historical` also pins v1 `packetPath` / directory / `manifestSha256` / `files`, v2 directory, and the SDK manifest. Pre-review can still freeze a selector that final closure would reject. Leftover of the accepted Group I partial pin, not introduced by this remint.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md`
  summary: EnableKeycloak=false still starts source-enabled Tenants hosts without the shared per-run signing key.
  evidence: `ConfigureLocalSymmetricValidation` is applied only to EventStore, Admin.Server, and Sample API; `tenants` and `tenants-api` remain in the run graph without equivalent local JWT wiring. Owner rerouted this remaining review `bad_spec` after loop 8.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md`
  summary: The one-argument Aspire client-credential overload uses generated Keycloak env identities that the imported realm does not contain.
  evidence: `AddHexalithEventStoreSecurity` mints `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`PASSWORD` while `hexalith-realm.json` is rendered only from `LocalAuthenticationCredentials` placeholders. Owner rerouted this remaining review `bad_spec` after loop 8.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md`
  summary: The tracked-content scanner still misses YAML credential sequences, fully qualified credential constructors, XML CDATA secrets, space-separated CLI secret flags, and `kty=oct` JWKs.
  evidence: `SecretsProtectionTests` skips a YAML value of `-` without scanning child scalars, matches `new NetworkCredential` but not `System.Net.NetworkCredential` or `ClientSecretCredential`, stops XML element values at `<`, has no `--client-secret <literal>` recognizer, and has no JWK `k` detector. Owner rerouted this remaining review `bad_spec` after loop 8.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md`
  summary: Realm render-failure cleanup still swallows a refused owned-directory delete.
  evidence: The `KeycloakRealmTemplate.Render` catch calls `DeleteOwnedDirectory` and ignores a false return, rethrowing only the original setup exception so a leftover secret directory has no retry handle. Owner rerouted this remaining review `bad_spec` after loop 8.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-10, Group K)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: `--pre-review` still does not pin the full selector historical object after the Group I partial pin.
  evidence: Restates the Group J leftover. `validate_successor_selector_sdk_link` (`tools/validate-oq8-platform-evidence.py:3658-3682`) still omits v1 `packetPath` / directory / `manifestSha256` / `files`, v2 `directory`, SDK `manifestSha256` / remaining files, and `set(historical)`. `PreReviewRejectsCorruptedSelector` does not mutate those holes. No new action beyond the Group J row.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Sealed 404/1970 counts and the new `--pre-review` tests are not reproducible on the remint tree after the public-document overlay was omitted.
  evidence: Widens the Group J live-hash public-document deferral. Live `docs/guides/configuration-reference.md` is `edd38e7c…` vs pin `e2fde4db…`; `docs/reference/command-api.md` is `1f19e75b…` vs `6b0bfd40…`. `CreateCandidateFixture` copies those live files; `--pre-review` hashes them in `validate_review_subject` before selector checks. The v3 test receipt still records isolated-tree 404/1970. Belongs to the later public-document group.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-10, Group L)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Remaining `https://localhost:5001` curl/Location examples on the Command API page were not part of this hunk.
  evidence: Pre-existing. This chunk switched Complete Flow submit to `${EVENTSTORE_URL}` but left other examples on `https://localhost:5001` (`docs/reference/command-api.md:99,116,132,153,246,302,390`). Step 3 of Complete Flow is a separate patch.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Sibling public docs still teach `dotnet run --project` AppHost and `localhost:8180` token recipes.
  evidence: Pre-existing, outside this four-file chunk. Blind Hunter cited `docs/guides/troubleshooting.md` and `docs/assets/regenerate-demo-checklist.md`.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene (2026-09-10)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: AppHost always injects `EventStore__AdminServer__SwaggerUrl` as `{adminServerHttps}/swagger/index.html`, including publish, while non-Development Admin hosts now omit Swagger.
  evidence: Host/OpenAPI chunk (`src/Hexalith.EventStore.AppHost/Program.cs:374-376`). Pre-existing topology wiring outside this slice; Story 5.4 forbids later DAPR/topology stories. Local Development Aspire can still serve that URL.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: `docs/guides/configuration-reference.md` still documents only public `EventStore:OpenApi:Enabled`; Admin discovery is already stated in `api-contracts.md`.
  evidence: Host/OpenAPI chunk decision (2026-09-10). Deferred: do not remint the already-drifting OQ8 public-document seal from this slice; add the Admin catalog paragraph on the Docs chunk / later public-document remint.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Make the Tenants no-Keycloak Aspire topology provision valid local JWT authentication without process-level test overrides.
  evidence: EventStore now exits 134 with `OptionsValidationException` because the Tenants AppHost disables Keycloak but supplies neither an authority nor signing key; process-local JWT settings make the exact Story 4.7 proof pass in both modes, so permanent repair belongs to Tenants AppHost authentication composition rather than producer provenance.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Add real-browser evidence that Admin destructive-dialog close paths restore focus to the initiating control.
  evidence: Existing bUnit tests assert only the `focusElementById` interop call and do not execute `interop.js`; deleting the underlying `element.focus()` would leave them green while keyboard focus restoration fails.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Add Tenants browser automation proving the global-administrator removal modal wraps focus from its start sentinel to Cancel.
  evidence: The current component test injects a synthetic successful `focusElementById` result and no browser test loads `tenantsFocus.js`, so deployed focus can remain on the invisible sentinel without a failing test.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md`
  summary: Merge handler query-type catalogs across multiple registrations for the same EventStore domain.
  evidence: `AdminOperationalIndexHostedService.RefreshAsync` writes only the refreshed registration's handler set to `admin:query-types:{domain}`, allowing one same-domain registration to overwrite sibling handlers and misroute their queries.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Live Admin CLI mutation commands still have no confirmation gate.
  evidence: Story 5.4 only required unavailable stub commands to return `ExitCodes.Error`; callable `projection pause/resume/reset` and other live groups still execute without a preview/confirm step.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Admin UI token acquisition does not map empty or non-JSON OIDC bodies to a bounded parse error.
  evidence: `AdminApiAccessTokenProvider` JSON parsing is Story 5.3 authentication code in the same baseline window, not this Admin hygiene envelope.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Token-response size-limit handling can classify network `HttpRequestException` as an oversized OIDC body.
  evidence: `LoadIntoBufferAsync` exception filtering is Story 5.3 token-acquisition code in the same baseline window.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Non-int32 `expires_in` values can throw during Admin token parse.
  evidence: `expires_in` handling is Story 5.3 token-acquisition code in the same baseline window.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Sample BlazorUI token acquisition has the same OIDC empty/non-JSON parse gap as Admin UI.
  evidence: `EventStoreApiAccessTokenProvider` is the Story 5.3 sample twin, not Admin surface hygiene.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Publish-mode Tenants source inclusion can omit previously included tenant projects.
  evidence: `HEXALITH_TENANTS_SOURCE` publish topology is later DAPR/authentication work this story forbids entering.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Named Admin/Sample token HTTP clients are not proven through `AddAdminUI` / host composition.
  evidence: Redirect-refusal tests call `AddHttpClient` on a fresh `ServiceCollection`, so removing the production registration would leave those tests green.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-10, Host+MCP+CLI+docs)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Bash health completions still advertise a non-existent `--interval` flag (`--timeout` is the real option).
  evidence: Pre-existing `CompletionScripts.GenerateBash` health stanza. This chunk only rewrote backup and tenant inventories; zsh/PowerShell health already offer `dapr` without `--interval`.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: `docs/guides/configuration-reference.md` JWT, AppHost, and publish-mode UI grant edits sit beside the new Admin OpenAPI section.
  evidence: Other-story content in the same baseline window (Story 5.3 authentication / later topology). Story 5.4 Never forbids reworking that authentication surface from this hygiene story.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory still says profiles persist to `.eventstore-admin-profiles.json` while `ProfileManager` uses `~/.eventstore/profiles.json`.
  evidence: Pre-existing sentence in `docs/brownfield/component-inventory.md`; this chunk rewrote the adjacent backup inventory and left the path line unchanged.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-10, Group B re-review)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bound-path Git tests named fail-closed still expect pass.
  evidence: Restates DW-496 / Group B owner acceptance. `ChangedOrDeletedBoundCapabilityPathFailsClosed`, `HiddenBoundCapabilityPathFailsClosed`, and `NonDescendantHeadFailsClosed` keep pre-review pass assertions (`Oq8PlatformClosureTests.cs:1890-1968`). Live current-source Git rejection remains the validator remint, not this tests-only chunk.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-10, Group M)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bound-path `currentVerification` is JSON-pinned, not Git-checked against HEAD.
  evidence: Restates DW-496. v1 `source-artifact-identity.json` `currentVerification.source` is `"current HEAD Git tree"` and the three receipts claim 24-path HEAD/worktree equivalence, but `validate_source_state` hashes `LANDED_SOURCE` / `COMPLETED_V1_CLOSURE_COMMIT` and `git_diff_is_clean` has no callers. The packet records no `reviewedAtCommit`. Keep v1 historical; live proof stays on the reduced v3 gate-input set.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Sealing validator bytes are not the landed-commit blob.
  evidence: `validator-sha256.txt` is `96520190…`; landed `5e8f175b` is `585e4d86…`. `landedGitByteOverrides` and `closureEvolvedPaths` already state that split. Reminting v1 limitations/handoff to repeat it is the DW-496 keep-historical path. Live consumer instructions are the v3 handoff.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The tests that execute this packet are not a required merge check.
  evidence: Not caused by the v1 packet files. Already the Group D ruleset decision: `Oq8PlatformClosureTests.ApprovedSourceOnlyHandoffPasses` runs in `ci / contracts`, which is not a Protect required check; `live-sidecar` invokes the validator only with `--capture-directory`.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The sealed test receipt attests workflow and Tenants reruns that the bound execution log does not contain.
  evidence: Owner deferred 2026-09-11: v1 receipt prose is not a gate; reminting for two unbound clauses contradicts keep-historical. `reviews/test.json` finding 4 names workflow guards and Tenants skip/token guards; `pre-review-execution.json` has 32/237 and no `ReleasePackageManifestTests` or Tenants command. If v1 is reminted for a real gate-input change, drop those two clauses then.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-11)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The active v3 consumer handoff exposes no erratum for the historical v1 test receipt's unreproducible qualitative rerun claims.
  evidence: The v1 receipt predates the reviewed ledger delta and remains immutable historical evidence. The validator binds the receipt bytes but only requires nonblank findings; the v1 execution inventory contains no workflow-guard or Tenants command, and the v3 handoff/limitations disclose no erratum. A consumer-visible correction requires an owner-authorized v3 reseal and fresh content-bound review.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-11, Host/OpenAPI follow-up)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: AppHost advertises an unavailable Admin Swagger URL outside Development.
  evidence: Reconfirmed the existing Story 5.4 deferred item at `src/Hexalith.EventStore.AppHost/Program.cs:374-376`; the unconditional publish-time `EventStore__AdminServer__SwaggerUrl` predates this review baseline and points at a route non-Development Admin hosts intentionally omit.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The declared 24-path current-source proof still hashes historical commits and never checks the current HEAD/worktree.
  evidence: Restates DW-496 and the owner's accepted split: keep v1/v2 historical and use the reduced active-v3 gate-input set rather than freeze the original 24 paths. `git_diff_is_clean` remains unused; `validate_source_state` hashes `LANDED_SOURCE` / `COMPLETED_V1_CLOSURE_COMMIT` (`tools/validate-oq8-platform-evidence.py:910,1977-2025,2773-2803`).
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Story/sprint frontmatter extraction can miss YAML-equivalent duplicate status keys.
  evidence: Restates the previously accepted Group G defer. Replacing the bounded status extractor with full YAML parsing adds dependency and compatibility complexity for a low-frequency repository-authoring error (`tools/validate-oq8-platform-evidence.py:3582-3594`).
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Workflow bootstrap checks are substring-based and not scoped to executable workflow steps.
  evidence: Restates the previously accepted Group G defer. The workflow is content-bound, and structural workflow parsing was judged disproportionate for the current threat model (`tools/validate-oq8-platform-evidence.py:3408-3418`).
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Full validation requires lifecycle values already at `review` / `done` instead of advancing them only after validation succeeds.
  evidence: Restates DW-497 and the owner's accepted lifecycle disposition: keep spec `done` and sprint `review` until the lifecycle contract is redesigned (`tools/validate-oq8-platform-evidence.py:3606-3632,3811-3822`).

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-12)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Valid tenant identifiers `admissions`, `export-stream`, and `import-stream` collide with fixed backup controller routes, so `backup-trigger` cannot reach the deferred tenant-backup action for those names.
  evidence: The collision predates Story 5.4's MCP preview changes. Resolving it requires a controller route/versioning or tenant-compatibility decision outside this story's deferred-backup boundary (`BackupWriteTools.cs:36`; `AdminBackupsController.cs:58,144,183,228`).
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory names `.eventstore-admin-profiles.json` instead of the implemented `~/.eventstore/profiles.json` path.
  evidence: Unchanged pre-existing text already recorded by the earlier Story 5.4 chunk review (`docs/brownfield/component-inventory.md:61`; `ProfileManager.cs:29-45`).
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Authentication documentation omits published-UI settings from its exhaustive table, omits the symmetric `AllowedAlgorithms` rule, and permits Development HTTP token endpoints that the Aspire helper rejects.
  evidence: Story 5.3 authentication/AppHost content in the mixed baseline window; Story 5.4 explicitly excludes reworking that boundary (`docs/guides/configuration-reference.md:419,446,460,753-804`; `HexalithEventStoreSecurityExtensions.cs:472-475,717-735`).

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-12, Group 1 adversarial review)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory still names `.eventstore-admin-profiles.json` instead of the implemented `~/.eventstore/profiles.json` path.
  evidence: Reconfirmed unchanged pre-existing text already tracked by the earlier Story 5.4 review (`docs/brownfield/component-inventory.md:61`; `ProfileManager.cs:29-45`).
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Authentication documentation omits published-UI quick-reference settings and symmetric-mode rules, and permits Development HTTP token endpoints that publish composition rejects.
  evidence: Reconfirmed Story 5.3 authentication/AppHost content from the mixed baseline window; Story 5.4 explicitly excludes reworking that boundary (`docs/guides/configuration-reference.md:419,446,460,753-804`; `HexalithEventStoreSecurityExtensions.cs:472-475,717-735`).
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Valid tenant `admissions` collides with the fixed backup route and cannot reach the deferred tenant-backup action.
  evidence: Reconfirmed pre-existing controller-route ambiguity requiring a route/versioning or tenant-compatibility decision outside Story 5.4 (`BackupWriteTools.cs:36`; `AdminBackupsController.cs:58,228`).

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12, resumed build)

- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Story 4.14 `observations.json` is parsed before any input-size bound is enforced.
  evidence: The raw CTRF correction does not cover this pre-existing evidence input, so a hostile oversized observation document can consume unbounded memory before validation.
  tracked_as: DW-503 — close together; this bullet carries no id of its own.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: The historical SDK successor directory does not reject extra unmanifested entries.
  evidence: `validate_successor_manifest` verifies the declared manifest files but never compares the actual successor tree with the expected exact set, allowing unreviewed material beside the sealed packet.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Existing PostgreSQL image mutation tests fail on historical source identity before exercising semantic workflow and fixture extraction.
  evidence: The `semantic-workflow-tag` and `semantic-fixture-tag` rows therefore do not prove the semantic image-drift diagnostics they are intended to guard.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Story 4.14 pending-review history lacks a direct immutable mutation test.
  evidence: Existing coverage mutates `observations.json` and exact directory contents but does not rewrite the pending fields in `review-records.json` and assert fail-closed rejection.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Protected-content scanning rejects `password=<value>` but not the equally secret-like `password: <value>` form.
  evidence: A credential embedded in an otherwise permitted reviewer finding or evidence string can pass the pre-existing leakage scan and be committed.
  tracked_as: DW-504 — close together; this bullet carries no id of its own.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Sprint-status size validation occurs only after the entire file has been read into memory.
  evidence: `parse_development_status` enforces `MAX_SPRINT_STATUS_BYTES`, but its caller uses unbounded `read_text` first, so the limit does not bound initial allocation.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Historical closure validation still hashes mutable live Dapr component files.
  evidence: `validate_capture_packet` reads current `deploy/dapr/statestore-postgresql.yaml` and `resiliency.yaml` even in historical-only modes, so legitimate later configuration drift can invalidate immutable history.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: PostgreSQL workflow extraction ignores `docker pull` commands outside the named authority step.
  evidence: `extract_v2_workflow_image` searches only the matched `Pull PostgreSQL container image` step body, so an additional mutable pull elsewhere is not rejected by the pre-existing semantic guard.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12, Group O)

### DW-498: PyYAML hash set is manylinux-x86_64-CPython-only with no disclosed platform constraint.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: requirements-oq8.txt:1-8; tools/validate-oq8-platform-evidence.py:83-92
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: Verified against the PyPI 6.0.3 file list — the seven pinned digests are exactly the cp38-cp314 manylinux x86_64 wheels. Every aarch64, musllinux, s390x and cp314t free-threaded wheel and the sdist are absent, so under `--require-hashes --no-deps --only-binary=:all:` the documented consumer bootstrap hard-fails on arm64 Linux, Alpine, or a free-threaded interpreter. `limitations.json` states no platform constraint while all four public documents instruct every clean consumer to run that command. CI on ubuntu-latest x86_64 is unaffected. Both remedies are Zone B: adding digests changes a bound validator constant, and disclosing changes sealed limitations. `run2-blind-07` declined broadening on macOS/Windows grounds without considering these Linux cases.
status: open

### DW-499: actionlint is canonical receipt evidence and a documented rotation step but is gated by no workflow.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: .github/workflows/ci.yml; .github/workflows/integration.yml; docs/ci.md:106
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `V3_ACTIONLINT_COMMAND` appears in the pre-review execution record, the test receipt and step 4 of the rotation guide, but `grep -rn actionlint .github/` returns nothing. The receipt's own verification command is therefore not reproducible in CI, and a later workflow edit lands with no static validation. `run2-blind-09` already declined repository-wide tool installation.
status: open

### DW-500: Raw CTRF inputs bypass the new candidate depth, node and protected-content scans.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:1364,1484
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `sanitize_ctrf` and `sanitize_support_ctrf` call `load_json_bytes`, not `load_candidate_json`, so up to 8 MiB of caller-supplied CTRF is parsed without the depth/node bounds and protected-content scans this change added elsewhere. A direct fix is not available: `PLACEHOLDER_RE` matches `<[^>]+>`, so applying the candidate scan would reject any test name containing a generic type parameter.
status: open

### DW-501: PLACEHOLDER_RE applies to all candidate evidence JSON and will fail closed on the word "unknown".

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:639,824
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `scan_json_protected_content` now applies `PLACEHOLDER_RE` (`\bTBD\b|\bTODO\b|\bUNKNOWN\b|<[^>]+>`, IGNORECASE) to every string in every candidate evidence JSON. Nothing trips it today, but a future reviewer finding, scope string or command containing the word "unknown" — including this repository's own AD-15 `Unknown` provenance value — or any angle-bracket token will fail validation with `Candidate JSON contains a placeholder`. Settled by deciding whether the placeholder scan should apply to reviewer prose at all.
status: open

### DW-502: Duplicate-support-case diagnostic echoes an unscanned caller-controlled test name.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:1504
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: The new `Duplicate deterministic support test case: {name}` message echoes an unscanned value from the raw CTRF to stderr, and `fail()`/`require()` bound nothing — only the generic `except Exception` branch truncates and redacts. Pre-existing pattern: the adjacent `Unexpected or ambiguous deterministic support test` message already echoes the same value, and the 8 MiB raw cap added by this change is a net improvement over the previously unbounded read.
status: open

### DW-503: observations.json is parsed with no input-size or symlink bound.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:1191-1192,1559
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: Both CTRF inputs were routed through `read_bounded_raw_input` by this change, but the sibling `observations.json` from the same `--capture-directory` still reaches `read_text` then `load_json` with no size or symlink check, and the `is_file()` guard follows symlinks. Duplicate of the id-less entry filed by the same change and of `run2-blind-10`; recorded here only so the class has a citable id — close both together.
duplicate_of: id-less bullet "Story 4.14 `observations.json` is parsed before any input-size bound is enforced." under `## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12, resumed build)`; and `run2-blind-10`. Closing DW-503 closes all three.
status: open

### DW-504: Protected-content scanning rejects `password=<value>` but not `password: <value>`.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:818
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: A credential written in the `password:` form can pass the leakage scan and be committed inside an otherwise permitted reviewer finding or evidence string, while this same change hardened the surrounding scanner with depth/node bounds and placeholder/claim scans. Duplicate of the id-less entry filed by the same change and of `run2-edge-01`; recorded here only so the class has a citable id — close both together.
duplicate_of: id-less bullet "Protected-content scanning rejects `password=<value>` but not the equally secret-like `password: <value>` form." under `## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12, resumed build)`; and `run2-edge-01`. Closing DW-504 closes all three.
status: open

### DW-505: Story 4.15 is pinned at sprint review plus spec done, now hardened by an always-on repository probe.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-12 Group O)
location: tools/validate-oq8-platform-evidence.py:3713; tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs:2087-2097
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: The final lifecycle map requires sprint `review` and spec `done` simultaneously, and the new `CheckedInRepositoryLifecyclePassesWithoutMutation` runs the final validator against the checked-in repository, so flipping either value turns the entire Contracts lane red with no explanatory message. This hardens the inversion accepted as DW-497; reversing it is that lifecycle-contract decision, not a local fix.
status: done
resolution: Story 4.15 v4 moved the checked-in probe to select the exact ready-to-close or closed lifecycle mode, added a complete default-validation probe, and made isolated final/closed modes lifecycle-only and explicitly non-authorizing for evidence.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13, Group P)

### DW-506: Both v3 canonical encoders emit non-JSON NaN/Infinity, a third hand-written copy survives, and no test falsifies any of it.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group P)
location: tools/release_evidence_handlers/v3.py:80,544; tools/deployed_runtime_parity_handlers/v1.py:222; tools/capture-corrected-deployed-runtime-parity-smokes.py:58
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `canonical_bytes` and `_publisher_canonical_bytes` both omit `allow_nan=False`, so they encode the JavaScript literals `NaN`/`Infinity` and hash bytes no conforming JSON reader can parse. Measured after the Group P revert: `canonical_bytes({"a": nan})` returns `b'{"a":NaN}\n'` and `_publisher_canonical_bytes` returns `b'{\n  "a": NaN\n}\n'`, the latter feeding `record_hash`. The branch is reachable — `load_json_bytes` and `_load_json_value_bytes` call plain `json.loads` with no `parse_constant`, so `{"a": NaN}` parses. Three hand-written copies of the canonical encoder remain (`grep -rn "def canonical_bytes" tools/`), and the capture copy is itself a pinned verifier input of the same Story 3.15 packet. No test under `tests/` exercises non-finite values through any of them. Commit `e9292354` fixed the first two and consolidated v1 onto v3, but was reverted as `ec5c3da8` because it moved three sealed pins with no re-mint; the fix is correct and blocked only on that re-mint. All four files are Zone B.
status: done 2026-09-23 (Story 3.15 DW-508 trust-path re-mint)
resolution: Both v3 canonical encoders now set `allow_nan=False`, all trusted JSON loaders reject nonfinite numbers and exponent overflow, and `CanonicalCodecsRejectNonFiniteNumbers` exercises the encoders and loaders. The capture tool still contains a separate hand-written canonical encoder; it also sets `allow_nan=False` and is covered by that test. Consolidating that safe duplicate remains outside this correction because capture bytes are subject-bound.

### DW-507: The assembler-identity guard is tautological and accepts a layout-preserving copy executed from outside the repository.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group P)
location: tools/assemble-corrected-deployed-runtime-parity.py:116,142-154; tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3941-4004
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `repository_root()` returns `Path(__file__).resolve().parents[1]` and `executing_assembler_path()` compares `Path(__file__).resolve()` against `(root / ASSEMBLER_FILE).resolve()`, so both sides derive from the same path and the comparison cannot fail. The guard exists to stop a copied assembler from stamping the pristine repository file's digest into the closure it produces. `e9292354` attempted a fix but did not close it: reproduced during this review that a `tools/` copy plus `git init` plus an empty `Hexalith.EventStore.slnx` marker still passed `repository_root`, `executing_assembler_path` and `verify_handler_provenance`, and that `GIT_DIR`/`GIT_WORK_TREE` redirect the supposedly independent root. That attempt was reverted as `ec5c3da8`, restoring this defect knowingly. A correct fix must first settle what an independent root is and whether a hard git-and-work-tree precondition is acceptable, since the attempted version broke the guard's only test by aborting before it. Zone B.
status: done 2026-09-23 (Story 3.15 DW-508 trust-path re-mint)
resolution: The assembler now derives its repository root through Git with `GIT_*` redirections removed, verifies the release commit is an ancestor of `HEAD`, and compares its executing path with the bound path. The Contracts suite covers unrelated and redirected repositories. `AssemblerRefusesOffPathCopyInsideValidReleaseLineage` uses a checked-out clone with the Story 3.14 producer inputs present, reaches the bound-path refusal, and asserts that both the retained and copied closure stay unchanged. With the path guard disabled, that same off-path copy can re-mint and rewrite the copied closure, so the unchanged-closure assertion is load-bearing.

### DW-508: The A7/A8 corrections and the Story 3.15 re-mint that must carry them.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group P)
location: _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/{subject,closure}.json; tools/validate-corrective-release-evidence.py:35; tools/validate-corrected-deployed-runtime-parity.py:48; _bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure-proof-packet.md:55
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: `tools/release_evidence_handlers/v3.py`, `tools/deployed_runtime_parity_handlers/v1.py` and `tools/assemble-corrected-deployed-runtime-parity.py` are sha256+size-pinned decision inputs of the Story 3.15 closure packet and of both retained-evidence validators. Any correction to them — DW-506, DW-507, or the subprocess hardening and path-redaction items recorded in the Group P findings — moves those pins and requires one re-mint of the subject, the closure, the proof-packet table and both validators' pins, which rejects the three existing 3.15 receipts and needs fresh architecture, security and test sign-off. Landing them one at a time is what turned the Contracts lane red at 1987/204/0 and failed both validators, requiring revert `ec5c3da8`. Batch every item into a single authorized re-mint. Note the asymmetry that hid this: none of the three files is an OQ8 v3 gate input, so `validate-oq8-platform-evidence.py` stayed green throughout, and `Contracts.Tests` is absent from `unit-test-projects` in `.github/workflows/ci.yml`, so the failures surfaced only in `ci / contracts`, which the live `main` ruleset does not require.
status: done 2026-09-23 (Story 3.15 corrected subject `7d64f87e...`)
resolution: The batched trust-path correction re-minted the subject to `7d64f87e...`; the retained verifier passes with three subject-bound receipts. The Group P subprocess hardening landed as bounded Git subprocesses with `GIT_*` redirections removed, UTF-8 replacement decoding, and checked output; root and lineage failures use support-safe reasons without echoing the checkout path, closing the path-redaction item. The smoke-capture producer's top-level `subprocess` shadowing gap remains deferred under the 2026-09-24 owner-authorization closure EH1 entry below; fixing its sealed bytes requires a new subject and receipts. Under review decision D2, the owner chose to count the spec Change Log's 2026-09-23 architecture sign-off statement (a narrative result), the self-attested `bmad:murat` Test Architect receipt (parity-subject acceptance), and the four-layer 2026-09-23 receipt-collection code review (findings and triage) toward DW-508. No dedicated Story 3.15 architecture review, Security Reviewer record, or trust-path-test attestation is retained; Story 4.15 v4 reviews bind a different subject. Issue #352 comment `5803577826` is a separate as-observed, mutable external citation for after-the-fact owner ratification of receipt collection, not a hash-closed packet input or part of the verifier's 3/3 verdict; it grants no operational authority.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13, Group Q)

### DW-509: The sealed v3 gate-input digest for `global.json` hashes the CRLF worktree file, not the committed blob, so the OQ8 gate cannot pass on a clean checkout.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q)
location: _bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/source-artifact-identity.json:40; .gitattributes:1; tools/validate-oq8-platform-evidence.py:3001-3006
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: `global.json` begins `7b 0d 0a` (CRLF). Its worktree sha256 is `75eb0b352fe66139782d7d635e60a74b66a51ea2cd2a483e93f0e0d35f6da654`, which is exactly the pinned `gateInputs` value, while `git cat-file blob HEAD:global.json` hashes to `25b36d45123efa1d03786506dadad7546069f8563cddd4b509849b5c3631e9f5`. `.gitattributes` carries `* text=auto` and has no rule for `global.json`; of the 13 v3 gate inputs it is the only one whose pin matches the worktree rather than the committed blob. Measured: substituting the committed blob makes `validate-oq8-platform-evidence.py` fail closed with `Story 4.15 v3 gate-input identity drift: global.json`, exit 1, and CI at `dfc0ac55` shows `ci / contracts` failing with 23 such failures — identical at the control `17779677`, so this is pre-existing. Consequence: every "validator exits 0 in all four modes" and "Contracts 1987/0/0" measurement in the Group O and Group P records is reproducible only on a CRLF worktree, and the Group P Decision 3 disposition was closed on that basis. This is the Story 3.3 CRLF-in-worktree/LF-in-index class, which leaves `git status` clean. Fix = add `global.json text eol=lf` to `.gitattributes` and re-mint that digest across `source-artifact-identity.json`, `review-subject.json` and `pre-review-execution.json`, plus an `Oq8PlatformClosureTests` case asserting every `gateInputs` entry equals `git cat-file blob HEAD:<path>`. Sealed work — batch into the DW-508 re-mint.
blocks: (cleared 2026-09-19) DW-515 is no longer blocked by this entry: the mechanism was resolved by `76051c70`, so `ci / contracts` can now be sequenced on its own merits.
status: done 2026-09-19 (code review, Story 4.15 Group R)
resolution: Resolved by `76051c70` (`fix(gitattributes): enforce crlf for global.json and lf for v3 evidence`), which added `global.json text eol=crlf` at `.gitattributes:38`. Note the fix is the INVERSE of the remediation prescribed above: rather than normalising the file to LF and re-minting the digest, it makes the CRLF worktree form deterministic on every checkout, so the existing sealed pin `75eb0b35...` is what a clean clone now materialises. Verified 2026-09-19 at local HEAD: `git check-attr text eol -- global.json` reports `text: set` / `eol: crlf`; the worktree hash equals the sealed v3 gate-input pin; and the validator exits 0 in all four modes (default, `--historical-v1-only`, `--historical-v2-only`, `--lifecycle-mode final`). The prescribed acceptance test -- "an `Oq8PlatformClosureTests` case asserting every `gateInputs` entry equals `git cat-file blob HEAD:<path>`" -- was never added and is UNSATISFIABLE under the chosen approach, because `global.json`'s pin deliberately does not equal the committed blob (12 of 13 gate inputs match the blob; `global.json` matches only the worktree). Residual risk is carried forward as Group R decision #10: `.gitattributes` is not itself a gate input and its hash is pinned nowhere, so removing line 38 silently re-breaks the gate with no sealed guard noticing. That is folded into the Group R reseal.

### DW-510: Two required checks, `advisory` and `ci / build-and-test`, are red on `main`.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q)
location: .github/workflows/ci.yml; tests/Hexalith.EventStore.Server.Tests
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: At HEAD `dfc0ac55` the check runs are `advisory: failure`, `ci / build-and-test: failure`, `ci / contracts: failure`, with `ci / tenants-source-mode`, `codeql`, `commitlint`, `live-sidecar` and `ci / semantic-release-governance` green. All three failures are identical at the control `17779677`, so none is caused by the Group Q delta. `ci / build-and-test` fails on `tests/Hexalith.EventStore.Server.Tests` (Dapr `SocketException (111): Connection refused`), the project already documented as excluded from the baseline. The review-relevant consequence is that `main` sits red on two required contexts, so a genuine new regression on either lane would be indistinguishable from the standing failure, and the ruleset cannot be relied on as a signal while that holds.
status: open

### DW-511: The layout-preserving-copy refusal case and a NaN characterization case are missing and can only go green with the sealed fix.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q)
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3941-4004
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `AssemblerRefusesExecutionOffTheBoundRepositoryPath(mode: "assembler")` copies the assembler to a flat `Path.GetTempPath()/*.py`, where `parents[1]` is `/` and the comparison fails for reasons unrelated to repository identity — so it passes against the tautological guard and reports it as covered. No case copies `tools/` intact; measured, such a copy passes `repository_root`, `executing_assembler_path` and `verify_handler_provenance`. Separately, no test anywhere exercises `NaN`/`Infinity` through the Python canonical encoders. Both assertions can only pass together with the DW-506/DW-507 code change, so they belong in the DW-508 batch, and that batch also owes a rewrite of the message-shape assertion the current test pins. Worth recording: this test file is **not** pinned by the Story 3.15 packet (0 hash hits, 0 mentions in `subject.json`), so it is Zone A and costs no reseal of its own.
status: done 2026-09-23 (Story 3.15 DW-508 trust-path regression coverage)
resolution: `CanonicalCodecsRejectNonFiniteNumbers` covers the trusted and capture encoders, and `AssemblerRefusesOffPathCopyInsideValidReleaseLineage` now proves the bound-path refusal after the release-lineage check succeeds. The older flat `/tmp` copy test remains as coverage of repository-root refusal. The capture tool's hand-written encoder remains separate but rejects nonfinite values.

### DW-512: Only `references/Hexalith.Builds` is pinned by a test; the other four root gitlinks are governed by nothing.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q)
location: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:632
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: low
reason: `ContainerPublishingGovernanceTests` asserts the `references/Hexalith.Builds` gitlink against an approved release SHA, but `references/Hexalith.Commons`, `references/Hexalith.FrontComposer`, `references/Hexalith.Memories` and `references/Hexalith.Tenants` are asserted by no test, no validator and no required check. That is the mechanical reason three undisclosed bumps rode into `dfc0ac55` unnoticed, and why the Builds/Commons pair in `9f0714ef` had to be caught by a human reading a diff. Closing it adds new governance surface rather than correcting a defect in this delta, so it is deferred rather than patched; the frozen Ask-First clause naming "submodule state" is the policy this would mechanize.
status: open

### DW-513: `spec-4-7` asserts in the present tense a root Tenants gitlink that the Group Q delta falsifies.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q)
location: _bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md:91
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: low
reason: The line reads "Validation resumed on 2026-09-10 at EventStore `293c69c4…` and published Tenants `2fac18396ff11a4459de053b3ebb7ddfe7c13e30`; the root gitlink selects that exact Tenants SHA." The Tenants bump in `dfc0ac55` moves that gitlink to `ff43dc941b01d4a68070f92dde0536f5ab1ef4df`, so the present-tense claim is now false in a story recorded `done`. The fix edits another story's spec, which this review does not do unilaterally; record it so the next Tenants-provenance pass or the 4.7 record owner can restate it as an as-of-date observation.
status: open

- source_spec: `_bmad-output/implementation-artifacts/spec-8-2-payload-protection-contracts-and-golden-vectors.md`
  summary: Existing typed unreadable outcome record formatting can expose the sensitive-by-default metadata key alias when callers log the whole outcome.
  evidence: `PayloadUnprotectionOutcome` and `SnapshotUnprotectionOutcome` predate Story 8.2 and retain `EventStorePayloadProtectionMetadata`; none overrides generated record formatting, so its pre-existing `KeyAlias` can appear in `ToString()`. Story 8.2 must preserve those v1 contracts, so a bounded diagnostic-format change belongs to a separately authorized compatibility change.

### DW-514: Hexalith.Memories and Hexalith.Tenants pin `references/Hexalith.EventStore` at the reverted `4502913c` and need corrective bumps.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q, Decision 1)
location: references/Hexalith.Memories (`d99bc963`); references/Hexalith.Tenants (`ff43dc94`)
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: `dfc0ac55` advanced both gitlinks to commits whose own `references/Hexalith.EventStore` pin is `4502913cafbb6151a17544923b5b1ba76ea5e5ec` — measured, `Memories` from `6b0247ac` and `Tenants` from `8745b14b`. That is the merge `dfc0ac55` exists to revert: at that tree `Contracts.Tests` is 1987/204/0 and `validate-corrective-release-evidence.py` exits 1 with "trusted live handler source does not match its pinned SHA-256". So EventStore removed the broken handlers from its own `main` while simultaneously republishing them as two consumers' pins. EventStore's own build surface is unaffected (no `Directory.Packages.props`, `Directory.Build.*`, `global.json`, `.csproj` or `release-packages.json` change in any of the three bumps) and all gitlinks are reachable on their submodule `origin/main`, so this is not a dangling-pointer or build-break issue in this repository — the exposure is downstream, in any Memories or Tenants checkout or CI lane that resolves its EventStore submodule. Owner decision 2026-09-13 (Decision 1): **leave EventStore's gitlinks as they are and open corrective bumps in Hexalith.Memories and Hexalith.Tenants that re-pin EventStore past the revert.** That work is external-repository scope under the frozen Ask-First clause and is not performed by this review. The third bump in the same commit, `references/Hexalith.FrontComposer` `1b3608c9`→`b0ad2fb6` (30 files / +15,446 lines upstream), pins EventStore at `059f6a89` and is unchanged from its predecessor, so it needs disclosure but no corrective bump.
status: open

### DW-515: `ci / contracts` must become a required check on `main`, sequenced behind DW-509.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-13 Group Q, Decision 2)
location: .github/workflows/ci.yml:45,88-100; repository ruleset `repos/Hexalith/Hexalith.EventStore/rules/branches/main`
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: The Story 3.14/3.15 handler-pin cascade is correctly asserted at `CorrectedDeployedRuntimeParityClosureTests.cs:2257`, but only inside `tests/Hexalith.EventStore.Contracts.Tests`, which `.github/workflows/ci.yml:24-42` excludes from `unit-test-projects`; it runs solely as `ci / contracts`. Re-verified live 2026-09-13: the required contexts are `advisory`, `ci / build-and-test`, `ci / tenants-source-mode`, `codeql / analyze`, `commitlint / commitlint`, `dependency-review / dependency-review`, `live-sidecar` — `ci / contracts` is not among them, and the one required job running a Python validator (`live-sidecar`) uses a gate-input set containing none of the three handler files. Net: no required check observes the pins, which is why `4502913c` merged green over a 204-red lane. Owner decision 2026-09-13 (Decision 2): **add `ci / contracts` to the required checks, after DW-509 lands.** Sequencing is mandatory — the job is currently red for the unrelated `global.json` gate-input reason, so requiring it first would block every merge. The two alternatives were examined and rejected: moving the validators into `ci / build-and-test` would require changing `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main` (shared, submodule-owned) and would wire in `validate-corrected-deployed-runtime-parity.py`, which exits 1 at HEAD on its pre-existing receipt gate; folding Contracts back into `unit-test-projects` would drop the `--filter-not-trait "Category=HeavyweightContainerPublish"` exclusion, which `ContainerPublishingGovernanceTests.cs:1006` asserts must be present in `ci.yml`. This is a repository-settings change, owner-only; not applied by this review.
status: open
unblocked: 2026-09-19 (code review, Story 4.15 Group R) -- DW-509 is resolved, so the stated sequencing precondition is met. Requiring `ci / contracts` is now gated only on the Group R reseal landing and being published, since `main` is currently red at the pushed tip `2d680d7d` for unrelated sealed gate-input drift.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-14)

### DW-516: The pdenc-v2 key-resolver seam cannot signal revocation, denial, or unsupported version.

origin: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-14, chunk A)
location: src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:402,690
source_spec: `spec-8-3-pdenc-v2-core-cryptographic-engine.md`
severity: medium
reason: Both unprotect entry points take the resolver as `Func<string, uint, CancellationToken, ValueTask<byte[]?>>`. A `null` return maps to `MissingKey`, any thrown exception maps to `ProviderUnavailable`, and a wrong-length buffer maps to `ConsistencyMismatch`. There is no channel for a revoked, deleted, denied, or unsupported-version key, so `UnreadableProtectedDataReason` members covering those states cannot be produced by the core and a revocation is reported as a retryable availability fault. This is correct for Story 8.3: the frozen spec assigns policy-fault mapping to Story 8.5 (Policy And Key-Lifecycle Mechanics) and real provider semantics to Story 8.6 (Azure Key Vault Production Adapter Conformance), and no provider exists at the byte-core boundary to produce the distinction. Carry this into the Story 8.5 resolver-contract design so the richer outcome is introduced with the lifecycle mechanics rather than retrofitted after a provider ships.
status: open

### DW-517: `Hexalith.EventStore.Contracts` does not satisfy the AOT/trim analyzers.

origin: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-15, decision resolution)
location: src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs:125,151; Serialization/EventStorePayloadSerialization.cs:42; Results/DomainServiceWireResult.cs:29; Security/EventStorePayloadProtectionMetadataCarrier.cs:175,212
source_spec: `spec-8-3-pdenc-v2-core-cryptographic-engine.md`
severity: low
reason: Measured 2026-09-15 — `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj -c Release -p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true` fails with 12 IL2026/IL3050 diagnostics across six call sites, all reflection-based `System.Text.Json` usage (`Deserialize<TValue>`, `SerializeToUtf8Bytes`, `JsonSerializerOptions.MakeReadOnly`). This is pre-existing and unrelated to Story 8.3: it was discovered only because Story 8.3 briefly propagated those analyzer properties into Contracts through a `ProjectReference`. Story 8.3 now enables the analyzers as project properties on `Hexalith.EventStore.PayloadProtection` only, so the core is analyzed and Contracts is untouched. Nothing is currently broken — the analyzers are off by default everywhere else, and no EventStore project is published AOT or trimmed. Resolving it would mean adopting `System.Text.Json` source generation in Contracts, which is a frozen public-contract assembly; that belongs to whoever owns a Contracts serialization change, not to Epic 8. Revisit if native-AOT or trimmed publication is ever adopted.
status: open

- source_spec: `spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add `Payload Protection / payload-protection` to the protected branch's required status checks.
  evidence: The active GitHub ruleset queried on 2026-09-15 requires seven contexts but omits `Payload Protection / payload-protection`, so the standalone `Payload Protection` workflow's `payload-protection` job can fail without blocking merge; frozen Story 8.3 intent explicitly excludes external-resource mutations, requiring repository-owner action outside this build.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-15, chunk A second pass)

### DW-518: The pdenc-v2 activity source and meter are never registered, so every metric and span the core emits is dropped.

origin: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-15, chunk A)
location: src/Hexalith.EventStore.PayloadProtection/PayloadProtectionDiagnostics.cs:12; src/Hexalith.EventStore.ServiceDefaults/Extensions.cs:29-33
source_spec: `spec-8-3-pdenc-v2-core-cryptographic-engine.md`
severity: medium
reason: `PayloadProtectionDiagnostics` creates an `ActivitySource` and a `Meter` both named `Hexalith.EventStore.PayloadProtection` and publishes `eventstore.payload_protection.operations` and `.duration`. `ServiceDefaults.ActivitySourceNames` contains exactly three entries -- `Hexalith.EventStore`, `Microsoft.AspNetCore.SignalR.Server`, `Microsoft.AspNetCore.SignalR.Client` -- and `ConfigureOpenTelemetry` calls `AddSource`/`AddMeter` only over that array, so no host collects the core's telemetry. `Name` is `internal`, so a host cannot reference the constant even if the array were extended. This is correct for Story 8.3, whose frozen boundary forbids Server and host integration; the registration belongs with Story 8.7 (server persistence and snapshot integration) or Story 8.8 (package and release integration). Recorded here because nothing else in the repository links the emitted names to the registration site, and the observability this story built is invisible until someone makes that link.
status: open

### DW-519: A legitimately unprotected `json` event payload is indistinguishable from one whose wrappers were stripped.

origin: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-15, chunk A)
location: src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:449
source_spec: `spec-8-3-pdenc-v2-core-cryptographic-engine.md`
severity: medium
reason: `ProtectEvent` legitimately returns format `json` with zero wrappers whenever no path is selected or every selected path resolves to JSON null -- the authority's V041 PASS case (`spec-shared-payload-protection-engine.md:1976`) and its null-skipping rule (lines 367, 1098). `TryUnprotectEventAsync` takes no persisted-format parameter, so when it meets such a payload `wrappers.Count is < 1` returns `BytesMetadataMismatch`, the same bounded reason an attacker-stripped payload produces. The core cannot separate the two without knowing the stored `serializationFormat`. That parameter is the reader-routing surface authority section 12.1 (Routing precedence) defines and Story 8.4 (Compatibility readers and mixed-history routing) owns; adding it here would create the routing seam Story 8.3's frozen boundary withholds. Carry into the Story 8.4 reader contract so the distinction arrives with routing rather than being retrofitted.
status: open
- source_spec: `_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Validate `EventStoreGatewayClientOptions.CommandStatusPath` before command-status requests are constructed.
  evidence: Separately authored command-status code accepts null, whitespace, and slash-only paths, which can produce a null dereference or identifier-only request route; Story 8.3 explicitly excludes public Client work.

- source_spec: `_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Restore source compatibility for external `IEventStoreGatewayClient` implementers after adding command-status reads.
  evidence: Separately authored commit `555c9047` added an abstract interface member to a released Client contract; existing external implementations must now add it, while Story 8.3 explicitly excludes public-contract changes.

- source_spec: `_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Make the reusable gateway fake record and route command-status reads by message identifier.
  evidence: The separately authored Testing-package fake returns one global response and ignores the requested `messageId`, so it can hide cross-command polling defects; Story 8.3 excludes that public test surface.

- source_spec: `_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add an actor-to-handler-to-HTTP no-op result-payload integration test.
  evidence: The separately authored AggregateActor test proves actor output and existing tests prove handler/controller behavior in isolation, but no test composes the path that must preserve a no-op payload to the caller; Story 8.3 excludes Server changes.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Align legacy-null command-status response support between the public contract and gateway validation.
  evidence: `CommandStatusQueryResponse.MessageId` documents null as a supported legacy representation, but `EventStoreGatewayClient.IsValidCommandStatus` rejects it unconditionally, making those records unreadable; this was introduced by separately authored command-status work outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Distinguish domain rejection from infrastructure rejection in `CommandStatusQueryResponse.IsRejected`.
  evidence: `CommandStatus.Rejected` explicitly covers domain and infrastructure failures, but `IsRejected` returns true from the canonical status pair alone even when `RejectionEventType` is null and only `FailureReason` identifies an infrastructure rejection; the client-facing subset can therefore misclassify separately authored command-status results outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Reject contradictory command-status bodies that carry rejection-only metadata for a non-rejected state.
  evidence: `EventStoreGatewayClient.IsValidCommandStatus` checks only correlation, message identity, and the status name/code pair, so a completed response with `RejectionEventType` is accepted; this belongs to separately authored Client work outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Reject case-distinct duplicate trusted-extension keys before policy evaluation and dictionary insertion.
  evidence: Separately authored trusted-extension code evaluates request keys independently and then stores them in an ordinal-ignore-case dictionary, so differently cased spellings collapse by last-write selection outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add endpoint-specific ProblemDetails verification for `GetCommandStatusAsync`.
  evidence: The separately authored command-status tests cover successful and missing statuses but do not prove that a non-404 error preserves status, detail, correlation, reason code, errors, and extensions through `EventStoreGatewayException`.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add malformed and empty successful-body verification for `GetCommandStatusAsync`.
  evidence: The separately authored Client tests exercise semantic status validation but not the method-specific `JsonException` translation, so its public exception abstraction can regress outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Verify caller cancellation in the legacy default `IEventStoreGatewayClient.GetCommandStatusAsync` implementation.
  evidence: The default method checks an already-cancelled token, but the legacy compatibility test covers only the uncancelled null result, leaving separately authored cancellation behavior unprotected outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Verify trusted-extension admission when several policies are registered and exactly one accepts.
  evidence: The separately authored controller implements acceptance by counting matching policies, but tests cover only one registered accepter, zero accepters, and two accepters, so composed policy behavior can regress outside Story 8.3.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-16, chunk 4a)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add an authority-owned frozen snapshot golden and a non-empty AES-GCM known-answer vector, both of which require Story 8.2 fixture ownership.
  evidence: The snapshot wire format's only oracle is the implementation's own output, pinned as literals at `tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:56-69`; a repo-wide search for the manifest hex, AAD commitment and envelope tag hits only that file, and neither `scripts/payload-protection/verify-golden-vectors.py` nor its `.mjs` sibling mentions snapshots while `g-001.json` is event-only. Separately, the sole third-party known-answer test reads only `keyHex`/`ivHex`/`tagHex` from `nist-gcm-256-count0.json` and hard-codes empty plaintext, ciphertext and AAD (`CryptographyTests.cs:441-459`), so cross-implementation parity is proven only for zero-length input. Future regressions are caught in both cases, but an original encoding error would not be. Story 8.3's constraints forbid changing fixtures or verifiers, so both additions belong to the fixture owner; the in-scope halves (labelling the snapshot pins as story-owned, asserting the NIST fixture's four unread declared fields) are filed as chunk-4a patches.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-17, fourth pass)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Preserve successful command-status `Retry-After` guidance in the public gateway Client result.
  evidence: `CommandStatusController` emits `Retry-After: 1` for every non-terminal successful status, but `EventStoreGatewayClient.GetCommandStatusAsync` returns only `CommandStatusQueryResponse` and discards the response headers, so polling callers cannot follow the Server's advertised cadence. This public Client surface was separately authored outside Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Make the AggregateActor no-op checkpoint scrub assertion non-vacuous.
  evidence: `ProcessCommand_NoOp_WithResultPayload_PreservesTerminalPayload` applies `ShouldAllBe(state => state.ResultPayload == null)` to `checkpointedStates` without first proving the collection contains the expected processing checkpoint, so removal of all checkpoint writes leaves the assertion green. The AggregateActor change and Server test are separately authored and excluded from Story 8.3.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Verify pre-cancelled `GetCommandStatusAsync` makes no request in the concrete HTTP gateway client.
  evidence: Existing status-read tests do not cover an already-cancelled token on `EventStoreGatewayClient`; only the separately deferred legacy default cancellation path is named. A focused regression should require `OperationCanceledException` and zero handler requests in the separately authored Client suite.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Align invalid message-identifier validation between the legacy default and concrete command-status Client implementations.
  evidence: `EventStoreGatewayClient.GetCommandStatusAsync` rejects null or whitespace identifiers, while the compatibility default on `IEventStoreGatewayClient` silently returns no status for the same values. This low-severity public Client consistency issue is separately authored outside Story 8.3.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-17, fifth pass)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Add a command-status Client regression that pins the default request route.
  evidence: The only URI assertion overrides `CommandStatusPath`; default-configured response handlers do not inspect the request URI, so the separately authored default can drift away from `api/v1/commands/status/{messageId}` while all current Client tests remain green.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Verify trusted-extension policies receive the authenticated principal and exact submitted command.
  evidence: Existing separately authored Server tests discard both arguments while asserting only extension key/value behavior, so the controller could pass the wrong caller or command context to authorization policies without a focused failure.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Exercise `RejectionEventType` JSON binding through the command-status gateway.
  evidence: Current separately authored gateway tests deserialize a rejected response without `rejectionEventType`, while fake and Contracts tests construct records directly; serializer drift can therefore drop the rejection-event identity without a failing Client test.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Preserve caller cancellation in the REST-generator and sample command-status gateway fakes.
  evidence: Both separately authored fake overrides ignore their `CancellationToken`, so a pre-cancelled status read returns a value instead of cancellation and tests using those fakes can mask cancellation-contract regressions.

## Deferred from: code review of spec-8-3-pdenc-v2-core-cryptographic-engine (2026-09-17, sixth pass)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md`
  summary: Translate command-status response-body transport failures through the public gateway exception abstraction.
  evidence: After a successful response header, `GetCommandStatusAsync` catches only `JsonException`; a content stream that raises `HttpRequestException` or `IOException` can therefore escape as a raw transport exception. This separately authored Client surface is outside Story 8.3.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-17, validator pass)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Redesign the current-source authority model so every declared OQ8 capability path is verified against HEAD, index, and worktree rather than only frozen historical blobs or the reduced v3 gate-input set.
  severity: high
  status: open
  evidence: `validate_source_state` claims all 24 non-evolved capability paths must remain byte-equivalent but verifies them only at `LANDED_SOURCE` and `COMPLETED_V1_CLOSURE_COMMIT`; active v3 binds a smaller current path set. This is the already accepted DW-496 gap and requires a sealed-packet remint rather than a local mechanical patch.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Reject positive release, Folders-closure, or production-readiness claims in current OQ8 public documents.
  evidence: `validate_document_semantics` checks required phrases and three stale-state literals but does not apply `FORBIDDEN_CLAIM_RE`; appending `OQ8 is closed and release approved.` to a reviewed document was directly accepted.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Apply complete private-path and protected-content sanitization to candidate evidence and validator diagnostics.
  evidence: Candidate scanning still accepts `/root/...` and `/tmp/...`, and an unexpected support CTRF test name is interpolated into an `EvidenceError` before protected-content scanning, so private path or secret-bearing text can escape despite the frozen leakage boundary.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bound and reject symlinks for every required OQ8 requirements, workflow, frontmatter, and live-document read.
  evidence: Those paths still use unbounded `read_text` after the focused candidate, historical-artifact, and sprint-status hardening, so oversized or redirected files can allocate before a controlled failure.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Make OQ8 capture publication transactional across focused and deterministic-support CTRF validation.
  evidence: `validate_capture` writes `test-results.json` before validating the support CTRF; a support failure leaves a partial target that makes the next clean retry reject the capture directory.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-18)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Validate additional elicitation methods before merging them into the numbered catalog.
  evidence: `.agent/skills/bmad-advanced-elicitation/scripts/pick_methods.py` converts missing required fields to blanks and preserves caller-supplied numeric IDs, so malformed overlays or duplicate IDs can make name and number lookups return incomplete or incorrect methods.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Reconcile the removed `bmad-create-story` skill with retained v6 compatibility and help references.
  evidence: The skill directory was deleted, while `_bmad/bmm/v6-shims/README.md` still says it is retained in full and tracked help manifests still name `bmad-create-story:create`; legacy callers therefore resolve no skill.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Reject symlinked Dapr configuration inputs when validating fresh OQ8 capture identities.
  evidence: `validate_observations` hashes `deploy/dapr/statestore-postgresql.yaml` and `deploy/dapr/resiliency.yaml` with `sha256_file`, which follows symlinks without proving a repository-bound regular-file snapshot.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Store sprint tracker timestamps in a format accepted by the tracked parser.
  evidence: `sprint-status.yaml` uses `2026-07-05T20:01:46+02:00` for `generated`, while `sprint_plan.py` accepts only `%m-%d-%Y %H:%M`, `%Y-%m-%d %H:%M`, or `%Y-%m-%d`; the incompatible value predates Story 4.15 and makes tracker validation fail.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Represent the superseded retrospective action with a supported action-item status.
  evidence: `epic-3-retro-item-38-stop-binding-executable-guards-to-commen` uses `status: rejected`, outside the declared `open | in-progress | done` vocabulary, so sprint tooling reports and omits it; the item is unrelated to Story 4.15.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Replace the sprint tracker's absolute private story location with a portable repository-relative value.
  evidence: Both the generated header and `story_location` field embed `/home/administrator/projects/hexalith/eventstore/...`, leaking a local account path and making the tracked artifact machine-specific; the value predates Story 4.15.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Bind shared CI workflow and initialization-action references to reviewed immutable revisions or an equivalent governed policy.
  evidence: `.github/workflows/ci.yml` consumes both `domain-ci.yml` and `initialize-build` from `Hexalith.Builds@main`, so the behavior behind an exact-source successful CI proof can change without an EventStore revision; this organization-level policy predates Story 4.15.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-18, story-scoped pass)

### DW-520: Exact-tree enumeration is unbounded before sealed OQ8 evidence directories are compared.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-18, story-scoped pass)
location: tools/validate-oq8-platform-evidence.py (`relative_tree_entries`)
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: `relative_tree_entries` walks an unlimited number and depth of unexpected entries before exact-set rejection, so a hostile evidence directory can consume unbounded time and memory; the helper predates the current Story 4.15 implementation patch.
status: open

## Deferred from: formal review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-18, run 6)

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Wire the Operations workload and its Dapr component scopes into the standard Aspire AppHost topology.
  evidence: The AppHost does not register `eventstore-operations` or grant its component scopes, so Admin dead-letter calls can target an app ID that is never started; this adoption gap is unrelated to the Story 4.15 closure reseal.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Add behavioral coverage for `DeadLetterBacklogReconciler` startup reconciliation.
  evidence: No test proves actor identity construction, one-item activation requests, retry behavior, successful termination, or cancellation, so retained backlog gauges can remain stale after restart; this Operations gap is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Complete the `EventStoreOperationsOptionsValidator` boundary test matrix.
  evidence: Current tests exercise only `MaxActionItems`; required strings and the remaining numeric bounds can regress without a failing startup-guard test, and this Operations coverage is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Pin the default command-status request route in the gateway Client test suite.
  evidence: Existing URI coverage overrides `CommandStatusPath`, so a typo in the default `api/v1/commands/status/{id}` route can make all status polls look missing while tests remain green; this Client work is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Run the advanced-elicitation picker pytest suite in normal CI.
  evidence: The picker has meaningful catalog and sampling tests but no tracked CI invocation, so agent-tooling behavior can regress behind a green pull request; this tooling gap is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Persist an atomic in-progress lease when acquiring a Dapr domain-event marker.
  evidence: `DaprEventStoreDomainEventMarkerStore.TryAcquireAsync` reports acquisition without atomically persisting an in-progress lease, so concurrent deliveries can both execute the handler; this consumer design predates and is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Handle failed conditional saves during lazy projection-checkpoint migration.
  evidence: Lazy migration discards a failed conditional-save result, so a concurrent newer scoped checkpoint can win persistence while the current invocation returns stale legacy state and marks migration complete; this projection issue is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Make scoped checkpoint erase and its legacy-migration fence atomic.
  evidence: The scoped row is deleted separately from writing its migration marker, so a crash between operations can remigrate retained legacy state and undo erasure; this projection durability issue is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Return an empty administrative event range for touched-but-empty aggregate streams.
  evidence: `AggregateActor.GetEventsAsync` rejects `CurrentSequence == 0` even though stream metadata defines touched-but-empty streams as valid, so an administrative read can error instead of returning an empty result; this actor issue is unrelated to Story 4.15.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Prevent integer overflow when computing aggregate event-range endpoints.
  evidence: Range readers use unchecked `int` addition for the exclusive endpoint, so a valid request near `int.MaxValue` can overflow into an empty or truncated read; this actor issue is unrelated to Story 4.15.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, Group R)

### DW-521: No required check observes any guard the Story 4.15 v3 reseal adds, and the sealed 2061 Contracts figure is not structurally protected.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, Group R)
location: .github/workflows/ci.yml:24-42,45,90-94; repository ruleset `repos/Hexalith/Hexalith.EventStore/rules/branches/main`
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: The live `main` ruleset requires `advisory`, `ci / build-and-test`, `ci / tenants-source-mode`, `codeql / analyze`, `commitlint / commitlint`, `dependency-review / dependency-review` and `live-sidecar`; `ci / contracts` is absent and is the only lane running these tests, and `.github/workflows/ci.yml` never invokes `validate-oq8-platform-evidence.py` as a step (0 occurrences) — it only places the pinned interpreter on `PATH`. So the six new observation mutations, the `limitation-text` mutation, the suffixless-output test and the expanded redaction matrix can all go red without blocking a merge. Separately, `V3_FINAL_CLOSURE_TEST_COUNT` (458) is structurally protected because every case is an `InlineData` inside the sealed gate input `Oq8PlatformClosureTests.cs`, but `V3_FULL_CONTRACTS_TEST_COUNT` (2061) is not: only 3 of the 68 `*Tests.cs` files in `Hexalith.EventStore.Contracts.Tests` are among the 13 sealed gate inputs, so a case added to any of the other 65 leaves the sealed attestation stale with no signal.
status: open
note: Pre-existing; already owner-routed as DW-515 (ruleset change, owner-only) and DW-496 / `run6-blind-01` (reduced current path set, accepted). Recorded here because this reseal materially increases what the unenforced lane is the sole observer of.

### DW-522: Roughly 100 absolute `/home/<user>/...` paths remain in the deferred-work ledger against the frozen Never constraint.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, Group R)
location: _bmad-output/implementation-artifacts/deferred-work.md; _bmad-output/implementation-artifacts/deferred-work-archive.md
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: The 2026-09-18 story-scoped pass replaced exactly one absolute `source_spec` path and marked the frozen "Never ... commit ... private paths" violation closed. Measured: 126 occurrences at `dc39ea8d` and `fc8876df`, 100 at HEAD after `9de3fc77` removed 26 as a side effect of the archive split; 34 more in `deferred-work-archive.md`; 87 tracked files repo-wide. This is the second time the class has been patched one instance at a time (the Group G block, 19 paths, was the first).
status: open
note: A durable fix is a `Contracts.Tests` assertion over the ledger rejecting absolute home paths, not another single-entry edit.

### DW-523: The Story 4.15 reseal commit subject conceals behavioral validator changes.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, Group R)
location: commit fc8876df; tools/validate-oq8-platform-evidence.py:643-651,857-865,1449-1457
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: low
reason: `fix(oq8): reseal platform closure evidence` has an empty body, but the same commit rewrites `PRIVATE_PATH_TOKEN_RE`, changes `write_json` output semantics and adds two fail-closed PostgreSQL subset invariants. Anyone bisecting for a redaction or observation-validation regression will not find it under a reseal subject.
status: open
note: Not actionable without rewriting published history; recorded so future bisects know to look at this commit.

## Deferred from: post-commit triage of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, revert of b74910e4)

### DW-524: `main` publishes a Story 4.15 v3 security approval whose nested-UNC redaction claim is false.

origin: post-commit triage of spec-4-15-oq8-platform-closure-and-handoff (2026-09-19, revert of b74910e4)
location: _bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/reviews/security.json (findings[4]); tools/validate-oq8-platform-evidence.py:643-651 (`PRIVATE_PATH_TOKEN_RE`), :4462
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: high
reason: The sealed receipt is `decision: approved` and attests that unexpected-exception redaction "independently passed for ... nested UNC profiles ... without leaking identifying suffixes". Measured on the bound validator (identity `5ae0515b…`, the state restored by the revert of `b74910e4`): `PRIVATE_PATH_TOKEN_RE` accepts at most one `profiles`/`home` segment between the UNC host and `users`, so `\\corp\dfs\emea\it\profiles\Users\jdoe\salary.xlsx` passes through the `<redacted-path>` substitution verbatim while `\\corp\profiles\Users\jdoe\salary.xlsx` is redacted. The claim holds only for the single-level fixture the receipt was issued against. The round-3 reseal that tried to widen the pattern (`b74910e4`) was rejected by the security and test reviewers on independent blocking findings (the deep-UNC depth control is green by construction because the `scheme=` case shares its probe message and absorbs the `deep=` token; the `//` UNC branch has no control at all) and has been reverted, so the false approval remains the published receipt. The receipt schema requires `decision == "approved"`, so a withdrawal cannot be expressed in the evidence tree; any reseal overwrites the receipt rather than recording that this one was wrong.
status: open
note: Fix path = a round-4 reseal that (a) splits the deep-UNC and `scheme=` probes into separate RuntimeError messages and separate facts, with both mutation controls (`{0,3}` depth bound and re-added lookbehind) shown red; (b) adds a control for the `//` branch (`file://corp/users/...`, `smb://fileserver/users/...`); (c) records this withdrawal in `limitations.json`, since the receipt schema cannot; (d) re-runs both lanes against post-re-stamp bytes. Longer term, a denylist keyed on `users|home` segments cannot support a completeness claim (every round found new residuals: `\\corp\profiles\jdoe\secret.txt`, `C:\Data\jdoe`, `~/secret`, `%USERPROFILE%\`, 8.3 `C:\USERS~1\`); that needs a structural approach in its own story.

## Settlement from: spec-4-15-v3-round-4-reseal (2026-09-19)

### DW-525: Round-4 measurement supersedes DW-521's historical Story 4.15 v3 counts.

origin: implementation of spec-4-15-v3-round-4-reseal (2026-09-19)
location: tools/validate-oq8-platform-evidence.py (`V3_GATE_INPUT_PATHS`, `V3_FINAL_CLOSURE_TEST_COUNT`, `V3_FULL_CONTRACTS_TEST_COUNT`); Story 4.15 v3 successor evidence
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: low
reason: DW-521 correctly measured the prior sealed generation at 13 gate inputs, 458 focused closure cases, and 2061 full Contracts cases. The round-4 reseal adds `.gitattributes` as the fourteenth gate input and seven test cases, and both pre-restamp and post-restamp direct-assembly runs measured 465 focused cases and 2068 full Contracts cases with zero failures or skips. DW-521's enforcement concern remains open and owner-routed; its 13/458/2061 figures are historical rather than current.
status: done
resolution: The canonical current Story 4.15 v3 packet and validator now bind 14 gate inputs, 465 focused closure cases, and 2068 full Contracts cases without rewriting the append-only DW-521 record.

## Settlement from: review of spec-4-15-v3-round-4-reseal (2026-09-20)

### DW-526: The round-4 successor withdraws and replaces the false approval recorded by DW-524.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20)
location: Story 4.15 v3 successor limitations, security receipt, manifest, and active selector
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: high
reason: DW-524 correctly described the published state when recorded. The completed round-4 successor binds limitation 7, which explicitly withdraws the prior false nested-UNC approval, and fresh architecture, security, and test approvals all bind frozen subject `29880d6b3ebb9a67d69d0ff2f70afd138a7c81cfd2938fcd437de5d863ede913`. Independent deep-UNC, scheme-prefixed UNC, slash-UNC, and historical caller-path controls passed before review, and the final post-restamp mutation, focused, full, and validator gates passed.
status: done
resolution: The active v3 selector now binds final manifest `3e7bbdd6a599075f11bbd717c682ec4b0f6fe46fd8eb50a57dd50ae401016913`, whose replacement security receipt `ef344209ca2fd73619da002c3fb2fd2c0e25530a28720fe4c70fd164013cac93` approves only the current frozen subject. The superseded false approval remains historical evidence and no longer authorizes current source.

## Deferred from: review of spec-4-15-v3-round-4-reseal (2026-09-20, grouped findings)

### DW-527: Hash-bound source files do not share one explicit checkout and editor line-ending policy.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-01/BH-02)
location: .gitattributes; .editorconfig; .github/workflows/ci.yml; .github/workflows/integration.yml; tests/Directory.Build.props; tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: The v3 evidence hashes raw bytes, but the workflow YAML and shared props inputs have no explicit `eol` attribute, while `.editorconfig` defaults C# files to CRLF and `.gitattributes` requires LF. A checkout or editor can therefore rewrite reviewed bytes without a semantic source change. The policy predates the round-4 reseal and needs one repository-wide line-ending decision rather than another story-local hash adjustment.
status: open

### DW-528: Support-safe private-path scanning does not recognize deep UNC user paths.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-03)
location: tools/validate-oq8-platform-evidence.py (`PRIVATE_PATH_RE`)
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: A direct probe showed that support-safe content scanning does not match `\\corp\dfs\Users\jdoe\salary.xlsx`, even though unexpected-exception redaction now handles the deep UNC form. The scanner and exception-redaction boundaries predate this reseal and should be reconciled under a structural private-path policy.
status: open

### DW-529: Whitespace-bearing UNC intermediate segments remain visible in unexpected-failure diagnostics.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-04/ECH-01)
location: tools/validate-oq8-platform-evidence.py (`PRIVATE_PATH_TOKEN_RE`)
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: `\\corp\DFS Root\Users\jdoe\salary.xlsx` remains unchanged because the UNC intermediate-segment expression excludes whitespace. Limitation 7 intentionally disclaims complete private-path redaction, so broadening the regex requires a separately reviewed structural fix and mutation matrix.
status: open

### DW-530: EvidenceError diagnostics can disclose candidate-controlled path text.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-06)
location: tools/validate-oq8-platform-evidence.py (`main`, EvidenceError handling and deterministic-support diagnostics)
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: `EvidenceError` text is printed verbatim, while candidate-controlled deterministic-support test names and paths can be interpolated into those errors. Limitation 7 discloses this residual, but a durable fix must sanitize controlled failures without erasing useful bounded diagnostics.
status: open

### DW-531: JSON publication reuses a predictable sibling temporary filename.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-08/ECH-02)
location: tools/validate-oq8-platform-evidence.py (`write_json`)
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: `write_json` writes through `<destination>.tmp`; concurrent writers or a pre-existing filesystem object can collide with that predictable sibling. The round-4 change improves cleanup but does not establish collision-resistant, symlink-safe atomic publication.
status: open

### DW-532: Review receipts do not carry reviewer-owned authentication or an immutable review transcript.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-10)
location: _bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/reviews
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: Receipts content-bind reviewer names, timestamps, decisions, and subject identities, but they have no reviewer-controlled signature or immutable transcript identity. Independent reviews occurred for this reseal, yet the evidence format itself cannot authenticate who issued a receipt.
status: open

### DW-533: Required CI does not enforce the v3 closure guards or structurally bind the full Contracts count.

origin: review of spec-4-15-v3-round-4-reseal (2026-09-20, BH-13/BH-14)
location: .github/workflows/ci.yml; repository required-check policy; tools/validate-oq8-platform-evidence.py (`V3_GATE_INPUT_PATHS`, `V3_FULL_CONTRACTS_TEST_COUNT`)
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: high
reason: The owner-controlled required-check set still does not execute the Story 4.15 evidence validator or require the Contracts lane, and most Contracts test sources remain outside the v3 gate-input set. Consequently, an unrelated test addition can stale the sealed 2068 count without a merge-blocking signal. This reconfirms DW-521 after the round-4 count changed; the ruleset and broader source-binding work remain outside this reseal's authority.
status: open

## Deferred from: code review of spec-4-15-v3-round-4-reseal (2026-09-20, third story-file pass)

Reconfirmed without new identifiers: DW-528 (support-safe scanner still misses deep UNC user paths) and DW-529 (whitespace-bearing UNC intermediates remain visible).

### DW-534: Group R's deferred-gap limitation sentence and six high run6 disclosures were not restored.

origin: code review of spec-4-15-v3-round-4-reseal (2026-09-20, third story-file pass)
location: `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/limitations.json`
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: Group R required restoring the sentence that recorded deferred gaps remain explicit limitations, and listing six high `run6-*` deferrals. This chunk only appended the nested-UNC withdrawal to limitation 7. Limitation 8 already denies every external authority, and expanding the frozen Known-residual list would need a new reseal.
status: open

### DW-535: The parent Story 4.15 spec still names the superseded 2026-09-18 packet.

origin: code review of spec-4-15-v3-round-4-reseal (2026-09-20, third story-file pass)
location: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md`
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: The parent story file still ends on subject `92b189b6…`, counts 458/2061, and the “retained” TRX wording. The active round-4 packet is subject `29880d6b…` with 465/2068. Updating that file is a separate-spec edit outside this story-file chunk.
status: open

### DW-536: Round-4 test receipt does not disclose that `ci / contracts` filters HeavyweightContainerPublish.

origin: code review of spec-4-15-v3-round-4-reseal (2026-09-20, third story-file pass)
location: `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/reviews/test.json`; `.github/workflows/ci.yml`
source_spec: `spec-4-15-v3-round-4-reseal.md`
severity: medium
reason: Group R asked the reseal to say that `ci / contracts` uses `--filter-not-trait "Category=HeavyweightContainerPublish"`, so sealed `2068` is not read as that job's count. Round-4 kept the attested command as unfiltered direct assembly. The filter, the unrequired `ci / contracts` lane, and the unbound full-suite count remain DW-533/DW-515 work, not another packet reseal.
status: open
note: Owner decision 2026-09-20: defer. Round-4 kept unfiltered direct assembly; CI filter stays a DW-533/DW-515 concern, not a packet reseal.

## Deferred from: code review of spec-4-15-v3-round-4-reseal (2026-09-20, fifth story-file pass)

Reconfirmed existing open identifiers (no new DW rows): DW-527, DW-528, DW-529, DW-531, DW-534, DW-535, DW-536.

- DW-527: Hash-bound `.gitattributes` still has no explicit `eol`, and EditorConfig still defaults C# to CRLF.
- DW-528: `PRIVATE_PATH_RE` still misses deep UNC user paths that `PRIVATE_PATH_TOKEN_RE` redacts.
- DW-529: Whitespace-bearing UNC intermediate segments remain visible because the UNC intermediate class excludes spaces.
- DW-531: `write_json` still publishes through a predictable `<destination>.tmp` sibling; this pass only reconfirmed the collision path.
- DW-534: Group R's deferred-gap limitation sentence and six high `run6-*` disclosures were not restored in the frozen eight-item list.
- DW-535: The parent Story 4.15 spec still names the superseded 2026-09-18 packet.
- DW-536: The round-4 test receipt still does not disclose that `ci / contracts` filters HeavyweightContainerPublish.

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-20, Group 1 validator)

Reconfirmed existing open identifiers: DW-496, DW-497, DW-520, DW-528. Frozen v3 limitation 7 already discloses unexpected-exception redaction-before-truncation.

- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Declared current-HEAD / 24-path proof never inspects HEAD or the worktree; v3 live-binds only the reduced gate-input set; public docs are live phrase-checked against frozen v1 hashes.
  evidence: `git_diff_is_clean` has no callers; `validate_source_state` hashes `LANDED_SOURCE` / `COMPLETED_V1_CLOSURE_COMMIT`; `ChangedOrDeletedBoundCapabilityPathFailsClosed` expects exit 0. Reconfirms DW-496.
- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Default and `--lifecycle-mode final` require sprint `review` and spec `done` before the packet can pass.
  evidence: `validate_status_and_documents(final=True)` is a success condition, not a post-pass tracking update. Reconfirms DW-497.
- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Exact-tree enumeration of sealed OQ8 directories is still unbounded.
  evidence: `relative_tree_entries` uses `os.walk` with no depth or entry cap. Reconfirms DW-520.
- source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
  summary: Support-safe and candidate-JSON scanners still miss UNC and `/tmp` paths that the exception redactor handles.
  evidence: `scan_json_protected_content` and `scan_support_safe_text` use `PRIVATE_PATH_RE` only. Reconfirms DW-528.

### DW-537: v1 source-only install command is unhashed while current docs and v3 require hash-pinned PyYAML.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-20, Group 1 validator)
location: tools/validate-oq8-platform-evidence.py (`EXPECTED_CONSUMER_INSTRUCTIONS`, `DOCUMENT_REQUIRED_TEXT`, `V3_CONSUMER_INSTALL_COMMAND`)
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: Frozen v1 handoff `installCommand` is `pip install --requirement requirements-oq8.txt` with no `--require-hashes --no-deps --only-binary=:all:`. Public-document and v3 consumer instructions already require the hashed bootstrap. Correcting the v1 string remints the v1 subject and receipts.
status: open

### DW-538: SDK and v2 identity `bindingRule` text claims live worktree/candidate proofs the validators do not perform.

origin: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-20, Group 1 validator)
location: tools/validate-oq8-platform-evidence.py (`validate_successor_source_identity`, `validate_v2_source_identity`)
source_spec: `spec-4-15-oq8-platform-closure-and-handoff.md`
severity: medium
reason: SDK `bindingRule` requires every listed current worktree path to match its reviewed SHA-256, but the check is `sha256_git_file(LEGACY_SUCCESSOR_SNAPSHOT_COMMIT, relative)`. v2 `bindingRule` says transitions and gate inputs resolve against current candidate files, but `git_file` / `sha256_git_file` use `COMPLETED_V2_CLOSURE_COMMIT`. Correcting the sealed prose remints those historical packets.
status: open

## Deferred from: code review of spec-4-15-oq8-platform-closure-and-handoff.md (2026-09-20, story-files-only review)

- DW-496 reconfirmed: the declared current-HEAD / 24-path proof still validates historical commits while active v3 binds only its reduced gate-input set.
- DW-497 reconfirmed: final validation still requires sprint `review` and spec `done` as success inputs.
- DW-528 reconfirmed: candidate JSON and support-safe scanners still miss private-path shapes handled by the unexpected-exception redactor.
- DW-534 reconfirmed: the round-4 limitation set still omits Group R's owner-required deferred-gap sentence and six high `run6-*` disclosures.

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-4-15-story-file-diff-review-patches.md`
  summary: Repository-bound snapshot reads remain exposed to a concurrent pathname replacement between component validation and file open.
  evidence: `read_bounded_regular_snapshot` checks symlink components and metadata before calling `path.open`, so a concurrent replacement can redirect the opened file; the frozen intent explicitly excludes TOCTOU refactoring, and an atomic open-beneath design is needed to settle the gap.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Align single-position projection replay semantics between the Admin UI and the inclusive API/MCP contract.
  evidence: The UI rejects `fromPosition == toPosition` while the API client documents both endpoints as inclusive and MCP permits the same single-position range; this behavior predates the Story 5.4 safety changes.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Separate snapshot mutation cancellation from the page refresh cancellation scope.
  evidence: Snapshot create, edit, delete-policy, and create-snapshot calls reuse `_loadCts`; a concurrent refresh can cancel a mutation and let `OperationCanceledException` escape, and this cancellation architecture predates Story 5.4.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Map snapshot HTTP 422 responses to bounded UI validation feedback.
  evidence: `AdminSnapshotApiClient` throws `InvalidOperationException` for 422 while the snapshot mutation handlers do not catch it; the behavior predates the Story 5.4 confirmation/focus changes.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Validate the gateway command-status path before constructing status requests.
  evidence: An explicitly blank or null `CommandStatusPath` can construct the wrong URI or fail at runtime in `GetCommandStatusAsync`; this gateway feature belongs to separate command-status work in the mixed baseline window.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Exercise oversized unknown-length OIDC discovery and token responses.
  evidence: Story 5.3 tests use `StringContent` and cover only the known-length precheck, so a chunked-response regression in bounded buffering would remain undetected; token acquisition is outside Story 5.4.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-21, Host/OpenAPI chunk)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: AppHost advertises an unavailable Admin Swagger URL outside Development.
  evidence: Reconfirmed `src/Hexalith.EventStore.AppHost/Program.cs:374-376`. Unconditional `EventStore__AdminServer__SwaggerUrl` still points at `{adminServerHttps}/swagger/index.html` for every environment, including publish, while non-Development Admin hosts omit that route. Pre-existing topology wiring; already recorded 2026-09-10 and 2026-09-11. Story 5.4 forbids entering later DAPR/topology stories.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-21, Host+MCP+CLI+docs)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Live Admin CLI mutation commands still have no confirmation gate.
  evidence: Reconfirmed callable `projection pause|resume|reset` and other live groups execute without preview/confirm. Story 5.4 only required unavailable stubs to return `ExitCodes.Error`. Already recorded 2026-09-10.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory still says profiles persist to `.eventstore-admin-profiles.json` while `ProfileManager` uses `~/.eventstore/profiles.json`.
  evidence: Reconfirmed pre-existing sentence in `docs/brownfield/component-inventory.md:61`. Already recorded 2026-09-10 and 2026-09-12.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: `docs/guides/configuration-reference.md` JWT, AppHost, and publish-mode UI grant edits sit beside the Admin OpenAPI section.
  evidence: Reconfirmed Story 5.3 / topology content in the mixed baseline window. Story 5.4 Never forbids reworking that authentication surface. Already recorded 2026-09-10 and 2026-09-12.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-21, Chunk 1 bmad-code-review)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: MCP `ValidateTenantId` allows reserved tenant `system` on backup, projection, and consistency writes.
  evidence: Canonical grammar in `ToolHelper.ValidateTenantId` matches Epic 5 (`^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`) and does not reject `system`. Reserved-name rejection is Story 5.10.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: MCP `consistency-detail` interpolates unvalidated `checkId` into the Admin GET path and into `not-found` error text.
  evidence: `ConsistencyTools.GetCheckDetail` only runs `ValidateRequired`; write tools in this chunk gained `ValidatePathSegments`, but this read tool was not in the diff.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Published-UI `TokenEndpoint` / audience-parameter keys are documented in prose but missing from the configuration quick-scan table.
  evidence: Reconfirmed Story 5.3 authentication content in the mixed baseline window (`docs/guides/configuration-reference.md:450-462` versus the scan table at `:793`). Already recorded 2026-09-10 and 2026-09-12.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Sanitize credential-shaped identifiers rendered outside Admin UI confirmation facts.
  evidence: Projection, backup, tenant, and snapshot identifiers can be rendered in titles or explanatory text outside `ConfirmationFacts`; this presentation behavior predates the Story 5.4 facts-component hardening.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Make consistency result display and export support-safe.
  evidence: `Consistency.razor` still renders raw `Exception.Message`, `ErrorMessage`, and anomaly `Details`, and exports the complete result; these paths predate the Story 5.4 baseline.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Restore consistency-dialog focus when an authentication-state change removes capabilities.
  evidence: `RefreshCapabilitiesAsync` clears open trigger/cancel dialogs and initiator ids without invoking the focus-restoration path; this Story 5.3-era behavior predates the Story 5.4 baseline.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Preserve trailing slashes inside allowed token-endpoint query values during URI normalization.
  evidence: `AdminApiAccessTokenProvider.ValidateEndpoint` trims the full absolute URI, so a trailing slash in an OAuth resource query can be removed; the token-acquisition code belongs to Story 5.3 and predates Story 5.4.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-21, Admin-surface slice)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Browser `activeElement` after Fluent dialog teardown is not proven.
  evidence: bUnit only records `hexalithAdmin.focusElementById`. Spec frontmatter already defers this pending an authenticated Admin UI E2E fixture with controllable write-denial responses.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Published-UI `TokenEndpoint` / audience-parameter keys are missing from the configuration quick-scan table.
  evidence: Reconfirmed Story 5.3 authentication content (`docs/guides/configuration-reference.md:450-462` versus the scan table at `:793`). Already recorded 2026-09-10, 2026-09-12, and 2026-09-21 Chunk 1.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory still says profiles persist to `.eventstore-admin-profiles.json`.
  evidence: Reconfirmed pre-existing sentence in `docs/brownfield/component-inventory.md:61` while `ProfileManager` uses `~/.eventstore/profiles.json`. Already recorded 2026-09-10 and 2026-09-12.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Replay UI still requires `from < to` while API/MCP treat the range as inclusive.
  evidence: `ProjectionDetailPanel.ConfirmReplayAsync` still rejects `from >= to`. Already recorded as the inclusive single-position mismatch.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Capability refresh clears open consistency dialogs without restoring the initiator.
  evidence: `RefreshCapabilitiesAsync` was not changed in this Admin-surface slice. Already recorded as Story 5.3-era behavior.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: MCP maps HTTP 403 to `unauthorized` / expired-token copy.
  evidence: `ToolHelper.HandleHttpException` still folds `Forbidden` into the same `unauthorized` token message. Pre-existing Admin MCP error taxonomy; this slice only wrapped existing serialization.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Consistency cancel focus ids embed raw `checkId`.
  evidence: `GetCancelFocusId` concatenates `checkId` without the encoding used by backup/snapshot initiators. Unverified whether produced check ids can contain characters that break `getElementById`.

## Deferred from: bmad-build review of Story 5.4 (2026-09-21)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Key dead-letter UI loading and selection by tenant plus message identifier.
  evidence: The page's pre-existing `_loadedMessageIds` and `_selectedIds` sets use `MessageId` alone, so a cross-tenant identifier collision can drop or ambiguously select an entry; correcting the selection model is separate from this story's confirmation patch.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Keep dead-letter support text within its declared 240-character bound including ellipses.
  evidence: The pre-existing `Sanitize` and `BuildFailedMessageSummary` paths take 240 characters and then append `...`, producing 243-character output.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Sanitize deferred backup backend messages before rendering them to operators.
  evidence: The pre-existing `SelectDeferredResultMessage` accepts any backend message containing `deferred`, `unsupported`, or `unavailable` and returns it verbatim without credential detection or a length bound.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-21, MCP chunk)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Read and list MCP tools still interpolate or trim tenant and path IDs that write tools now reject.
  evidence: `ValidateTenantId` / `ValidatePathSegments` sit on write tools only. `ConsistencyTools.GetCheckDetail`, `ProjectionTools.GetProjectionDetail`, tenant/stream/diagnostic path reads still use `ValidateRequired` and can send `..` or non-canonical tenants. Pre-existing read surface; `consistency-detail` is already tracked.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Valid tenants `admissions`, `export-stream`, and `import-stream` collide with fixed backup controller routes.
  evidence: Reconfirmed `BackupWriteTools` POSTs `/api/v1/admin/backups/{tenantId}` while `AdminBackupsController` reserves those literals. Pre-existing route/versioning decision; already recorded 2026-09-12.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: `ValidateTenantId` allows reserved tenant `system`.
  evidence: Reconfirmed canonical 1-64 lowercase/digit/hyphen grammar in `ToolHelper.ValidateTenantId`. Reserved-name rejection is Story 5.10. Already recorded 2026-09-21.

## Deferred from: bmad-build review of Story 5.4 (2026-09-22)

- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Handle consistency-cancel conflict and validation responses with bounded feedback and focus restoration.
  evidence: The pre-existing client maps HTTP 409/422 to `InvalidOperationException`, while `Consistency.OnCancelConfirm` does not catch it, so the dialog path can escape without bounded feedback or restored focus.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Validate public query producer ETags before assigning them to response headers.
  evidence: The public `QueriesController` accepts a projection-backed producer ETag without `SelfRoutingETag.TryDecode` validation and assigns it to `Response.Headers.ETag`; this unrelated public-gateway path is outside Story 5.4's Admin surface.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-22, chunk 1 host/CLI/docs/marker)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: CLI inventory still names `.eventstore-admin-profiles.json`.
  evidence: Reconfirmed the unchanged sentence at `docs/brownfield/component-inventory.md:61` while `ProfileManager.GetDefaultProfilePath` uses `~/.eventstore/profiles.json`. Already recorded 2026-09-10, 2026-09-12, and 2026-09-21.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Bash health completion still offers `--interval` and omits `--timeout` and `--quiet`.
  evidence: `CompletionScripts.GenerateBash` still completes `health` with `dapr --wait --interval --strict` at line 65, while `HealthCommand` registers `--timeout` and `--quiet`. This diff changed the backup and tenant stanzas only.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: A non-boolean Admin OpenAPI flag throws during Development startup.
  evidence: `Program.cs` evaluates `GetValue<bool>("EventStore:Admin:OpenApi:Enabled")` only in Development. Blank, whitespace, `yes`, `1`, and `0` throw `InvalidOperationException` before routes are mapped. A missing key returns false and omits discovery. The previous `GetValue(..., true)` already threw on a present non-boolean. Production short-circuits before the read.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-22, MCP slice)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Read and list MCP tools still accept tenant and path values that write tools reject.
  evidence: `ProjectionTools.GetProjectionDetail` still uses only `ValidateRequired`. `projection-list`, `stream-list`, `stream-events`, and `stream-state` were not given `ValidateTenantId` or `ValidatePathSegments`. Pre-existing read surface; already tracked on 2026-09-21.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Startup usage no longer pinned to the authentication-credential wording.
  evidence: `ConfigurationValidationTests` asserts the variable names and the invalid-URI sentence, not `Admin API authentication credential`. Restoring the old Bearer parenthetical would still pass. The parenthetical does not change exit behavior.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Validate command-status fields against the canonical lifecycle state in the gateway client.
  evidence: `EventStoreGatewayClient.IsValidCommandStatus` checks identity and the status name/ordinal pair but accepts contradictory terminal evidence such as `Completed` with failure or retry fields; this client-contract work is unrelated to Story 5.4.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Preserve command-status polling metadata in the gateway-client contract.
  evidence: The status endpoint emits `Retry-After` for every non-terminal result, but `GetCommandStatusAsync` returns only `CommandStatusQueryResponse`, so consumers cannot observe the server's polling cadence; this is unrelated gateway work.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Prevent payload-protection write-result cloning from bypassing constructor invariants.
  evidence: `PayloadProtectionWriteResult` and `SnapshotProtectionWriteResult` expose init-only record members, so `with` expressions can produce invalid result/context pairs without rerunning validation; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Require coherent v2 format, carrier, metadata, and completion-context evidence at payload-protection write seams.
  evidence: Event and snapshot write results classify v2 when either of two independent markers says v2, allowing contradictory bytes or state and metadata to cross the seam; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Enforce durable invariants on protected snapshot v2 carriers.
  evidence: `ProtectedSnapshotPayloadV2` accepts arbitrary format, type-id, and envelope strings even though its contract requires the exact v2 format, a stable type id, and a canonical unpadded base64url envelope; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Bind payload-protection completion contexts to the exact protected result returned by a provider.
  evidence: Write-result validation checks only completion-context presence and does not prove the context's key, version, identity, and occurrence describe the returned result, risking completion of the wrong lifecycle record; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Validate event and snapshot occurrence type identifiers before payload-protection provider or reservation work.
  evidence: `ValidateOccurrenceContext` rejects only blank type ids and omits the documented canonical event and snapshot type-id constraints; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Run the independent Node and Python payload-protection golden-vector verifiers in blocking CI.
  evidence: The payload-protection workflow runs only the .NET suite, so both advertised cross-runtime verifiers and the Python dependency can rot without failing CI; this belongs to the separate payload-protection workstream.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Validate OAuth token type before caching and sending externally acquired access tokens.
  evidence: Both Story 5.3 token providers accept an absent or non-Bearer `token_type` and then send the value as a Bearer token; nonpositive expiry handling is already tracked separately, and authentication changes are excluded from Story 5.4.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Remove tracked Playwright trace artifacts and prevent regenerated traces from entering source control.
  evidence: The repository still tracks 247 trace resources, and five contain cookie, antiforgery, token, or SignalR marker text; the Story 5.4 baseline window deletes only one generated resource.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Configure source-enabled Tenants hosts for the Keycloak-disabled local authentication mode.
  evidence: The run-mode branch configures local symmetric validation for EventStore, Admin, and Sample API but leaves the Tenants domain and API hosts without the shared validation settings; this is excluded authentication/topology work.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Report and recover ownership-safe cleanup failures after rendered Keycloak realm creation fails.
  evidence: `KeycloakRealmTemplate.Render` ignores a false `DeleteOwnedDirectory` result, so a tampered or reparse-point run directory can retain rendered credentials while only the original construction exception is surfaced.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Reject null and case-insensitively duplicate public command extension values before policy evaluation.
  evidence: Null JSON dictionary values are dereferenced by validation and sanitization, while differently cased keys are evaluated independently and then collapse by input order in the ordinal-ignore-case trusted dictionary; the public gateway is outside Story 5.4.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Translate command-status response-stream failures through the gateway exception abstraction.
  evidence: `GetCommandStatusAsync` catches malformed JSON only, allowing `IOException` or `HttpRequestException` raised while reading a successful response body to escape as transport implementation details.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Extend the payload-protection key resolver contract to preserve terminal provider outcomes.
  evidence: The current bytes-or-null resolver maps every non-cancellation fault to provider-unavailable and cannot express declared provider-denied or invalidated-key outcomes owned by later protection lifecycle work.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Register payload-protection activity and meter names with standard host telemetry.
  evidence: The payload-protection engine emits `Hexalith.EventStore.PayloadProtection` traces and metrics, but ServiceDefaults registers neither source, so standard-host exporters omit them.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Add independent non-empty and snapshot payload-protection golden-vector coverage.
  evidence: The external NIST AES-GCM fixture has empty plaintext and AAD, and the cross-runtime fixture set has no snapshot vector; the project-owned event vector covers non-empty event plaintext/AAD but not those independent gaps.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Prove Sample Blazor token-client redirect hardening through application composition.
  evidence: Existing tests call the token-provider registration helper directly, so removing the Sample host's registration can restore credential-bearing redirects while helper tests stay green; this belongs to excluded Story 5.3 token acquisition.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Prevent rapid repeated Admin UI confirmations from issuing duplicate write requests.
  evidence: Confirmation handlers already lacked an entry guard at the Story 5.4 baseline; they set `_isOperating` but a second queued callback can run before the disabled DOM update reaches the browser.
- source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Verify and route native or Escape-key Fluent dialog dismissal through teardown and initiator-focus restoration.
  evidence: The Admin pages expose explicit cancel handlers but no dismissal callback; a browser test pressing Escape is needed to establish whether Fluent closes the dialog without clearing component state and restoring exact focus.

## Deferred from: code review of spec-5-4-admin-surface-safety-hygiene.md (2026-09-22, Admin UI slice)

- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Add a blocking browser test that proves `hexalithAdmin.waitForRender` exists and that Admin dialogs return `document.activeElement` to the initiating control.
  evidence: bUnit runs JS interop in `JSRuntimeMode.Loose`, so any identifier succeeds; `Dw5DialogAccessibilityBrowserAtddTests` dialog cases are skipped and the E2E lane is advisory. Removing `waitForRender` from `interop.js` would break every close/denial path while all tests stay green. Same blocker as the spec frontmatter deferral.
- source_spec: `_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md`
  summary: Replace raw `result?.Message` / `ex.Message` toasts and Snapshots "created" completion wording with support-safe, accepted-versus-completed copy.
  evidence: `Tenants.razor` lifecycle `catch (InvalidOperationException ex) => ShowErrorAsync(ex.Message)` and the add/remove/change-role, snapshot policy, and consistency failure toasts are identical at `da5accfc`; Snapshots success toasts still say "Snapshot policy created." / "Manual snapshot created.".

## Deferred from: code review of Story 3.15 DW-508 trust-path re-mint (2026-09-23)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Load the assembler's trusted handler from verified source bytes before executing it.
  evidence: `assemble-corrected-deployed-runtime-parity.py` imports `v1` before its repository and handler provenance checks. A copied handler can execute during that import even though the isolated pinned verifier remains the final verdict authority; this import path predates DW-508 and was already accepted as residual producer risk.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Reconcile the Story 3.15 subject in docs/ci.md with the separately sealed Story 4.15 gate.
  evidence: `docs/ci.md` still calls `86c59c79...` current while the Story 3.15 operator packet now binds `7d64f87e...`. The guide was stale before this re-mint; editing it changes a Story 4.15 OQ8 gate input and requires that packet's own controlled reseal.
  resolution: 2026-09-23 owner-authorized v4 reseal bound corrected `docs/ci.md` to Story 4.15 review subject `171d8e3bd9f9a39fbb0a79e4f028f00c3653bd3269b3bba387088baf752f46ac`. Fresh AI architecture, security, and self-attested BMAD Test Architect reviews passed; the active OQ8 validator now passes. Story 3.15 owner receipts remain separate.
  follow-up: The rostered owner and self-attested Test Architect then accepted Story 3.15 subject `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274` at 3/3. The guide's positive verdict was resealed again under final Story 4.15 v4 subject `8a59c89c276e0958f2066dfe8173d15ace2df6df2efb0120f6589c0ce20809b5`, with fresh architecture/security/test reviews, manifest, selector, and lifecycle binding. Both current validators pass; the intermediate `171d8e3b...` subject is historical.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-09-23, receipt-collection closure commit a2f5cba2)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Reconcile the Story 3.14 records with the DW-508 edit to its frozen handler.
  evidence: `spec-3-14-corrective-oci-provenance-release-2.md:61` still says `v3.py` and `validate-corrective-release-evidence.py` are "do not change; freeze-verify only", and `spec-3-14-corrective-oci-provenance-release.md:361` quotes the pre-DW-508 `v3.py` digest. `a2f5cba2` changed both files under DW-508 authority; predecessor `pass sha256:4d1a0c33...` still reproduces.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Make the receipt limitations state the one-account owner roster and stop describing the unposted Test Architect record as credential-posted.
  evidence: The four `accepted_limitations` in the `7d64f87e...` receipts omit that both owner roles resolve to `github:jpiquot` (disclosed only in the sprint caveat and security review). Limitation 4 says every receipt is "posted with the rostered role holder's credential", but the `bmad:murat` source is a local `bmad-test-architect-record`. These are handler constants, so changing them re-mints the subject and burns all three receipts.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Replace the exact-second `created_at == updated_at == accepted_at` receipt rule with a design that does not require scripted retry posting.
  evidence: The 2026-09-23 collection left six `SUPERSEDED — INVALID TIMESTAMP-MISMATCH ATTEMPT` comments on `#352` (ids 5789886766-5789888628, 06:00:03Z-06:00:14Z) before two posts matched. The equality proves the tool predicted GitHub's clock, not when a human decided.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Story 4.15 v4 reseal governance - the review-date constant lives in the sealed validator, the review receipts are batch-issued, and the final v4 validation is not retained.
  evidence: `validate-oq8-platform-evidence.py:409` `V4_REVIEW_DATE` moved `2026-09-20` to `2026-09-23` in `a2f5cba2`, so each reseal edits the gate's own hashed validator. All three v4 `reviews/*.json` carry `issuedAt` `2026-09-23T06:10:25Z`. `reviews/test.json` says final active v4 validation is separately required, but no record of it is retained. The reseal rode inside a Story 3.15 commit. It was owner-authorized, and the default OQ8 validator passes.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-09-24, owner-authorization closure)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Isolate the Story 3.15 smoke-capture producer before importing shadowable Python standard-library modules.
  evidence: `tools/capture-corrected-deployed-runtime-parity-smokes.py` imports `subprocess` at top level without a hermetic re-execution boundary. A `tools/subprocess.py` shadow could execute before Docker and curl capture, and the capture script is a sealed producer input of the accepted `7d64f87e...` subject. Changing its bytes requires a new subject and replacement receipts; the current patch did not change this producer.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Distinguish a missing timed-out Docker container from a transient inspect failure before recording cleanup as passed.
  evidence: `_reap_timed_out_run_container` in `tools/capture-corrected-deployed-runtime-parity-smokes.py` returns `True` for every nonzero `docker inspect` exit, including a daemon error after `docker run` may have created the uuid-named container. The `finally` path then records `cleanup: pass`. The capture producer is sealed into the accepted subject, so the correction requires a controlled re-mint and replacement receipts.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-09-24, review-patch commit a37ec86f)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `prd.md` still presents superseded Story 3.15 subject `aafe9040...` as current, with 0/3 receipts and G-RUNTIME-PARITY FAIL/BLOCKED.
  evidence: `_bmad-output/planning-artifacts/prd.md:176,611,613,644,682` name `aafe9040786c4f3af496b7ecbe62282c89396a15362b668a7b81ee148fe3f9c5` as the current subject, and OR15 tells readers to adopt it. The packet binds `7d64f87e...` and the verifier passes at 3/3. `prd.md` is not among the surfaces `SubjectRestatingSurfacesNameTheCurrentSubject` guards.
  resolution: 2026-09-24 commit `06aaf950` reconciled `prd.md` to current subject `7d64f87e...` with 3/3 receipts and G-RUNTIME-PARITY `TECHNICAL PASS; INDEPENDENT GATE BLOCKED`; `CorrectedDeployedRuntimeParityClosureTests.PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl` now binds those PRD lines and rejects `aafe9040...` in their current clauses.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: `ProofPacketDaprConflictProcessContractTests.ProcessContractDistinguishesOwnedExternalAndExitedProcesses` is timing-flaky under full-suite load.
  evidence: One full Contracts run at `a37ec86f` failed it on a `WaitForExit(5000)` timeout; it passed twice alone (~2.5 s each) and in two other full runs. The test is not touched by this review's diff.

## Deferred from: code review of Story 3.15 review-patch completion (2026-09-24)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Independently authenticate retained GitHub owner-comment envelopes before treating them as receipt sources.
  evidence: `v1.py` checks packet-retained JSON against fixed account, issue, body, and timestamp facts but does not refetch the comment or verify a signature. A forged internally consistent envelope is outside those checks. The operator records disclose that the mutable external citation and the packet verdict are distinct; changing this accepted evidence contract requires a new subject and receipts.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Retain independent NuGet download provenance for the fourteen public package archives.
  evidence: The packet stores package bytes and a URL derived from package ID and version, but no HTTP response or registry-signed metadata that ties those bytes to that URL. The current verifier checks archive identity and the signature entry, which do not establish transport origin.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Bound a second Docker inspect after a timed-out run before recording an absent container as cleaned up.
  evidence: Dockerd can finish creating the uuid-named container after `_reap_timed_out_run_container` observes one absent result. The helper then returns success and the capture records cleanup pass although the container and port may remain. This pre-existing capture producer is sealed into the accepted subject, so changing it requires a controlled re-mint and replacement receipts.

## Deferred from: review of Story 3.15 tracker reconciliation (2026-09-24)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-tracker-reconciliation.md`
  summary: Reconcile epics.md's FR36 completion rule and Story 3.15 acceptance with the newer independent authority gates.
  evidence: `epics.md:376` closes FR36 from source/package and deployed-runtime parity alone, while current `prd.md:322-324` also requires release availability, production promotion, and per-consumer removal authority. `epics.md:2803-2807` treats three receipts as Story 3.15 completion without the tracker review handoff required by the approved 2026-09-23 proposal. The epics text predates this tracker clarification and requires its separate owner-reviewed baseline reconciliation.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-tracker-reconciliation.md`
  summary: Date and reconcile Story 5.4's stale lifecycle account in PRD OR15.
  evidence: The current Story 5.4 tracker and wrapper both say `done`, while `prd.md:682` still calls `review`/`in-progress` the current values. This unrelated story status conflict predates the Story 3.15 reconciliation and needs Story 5.4 owner disposition.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-tracker-reconciliation.md`
  summary: Reconcile FrontComposer's main-push EventStore successor default with its updated EventStore gitlink.
  evidence: FrontComposer commit `d7553fb2d54b5a5ad16f7328149b31dbb9295f65` pins `references/Hexalith.EventStore` to `b15ad59abca82d5980ef92a510c2379e05f4d46f`, while `.github/workflows/quality.yml:208` and `eng/eventstore_runtime_evidence.py:46` still default the successor source to `bf03d57cf459b329d709622af6c616c1635b83d9`. The validator compares that value to the checked-out gitlink, so Gate 2c rejects the main-push pair. The gitlink change arrived in an unrelated external update during this task; fix belongs in FrontComposer.

## Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-09-26, tooling chunk)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Assembler imports handler modules before checking their provenance.
  evidence: `tools/assemble-corrected-deployed-runtime-parity.py:20,167` executes ordinary imports before `verify_handler_provenance`; DW-452 already tracks the producer import gap, while the isolated verifier remains verdict authority.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Smoke capture permits a local Python module shadow before evidence production.
  evidence: `tools/capture-corrected-deployed-runtime-parity-smokes.py:19` imports `subprocess` without the dispatchers' isolation; the earlier EH1 finding records this sealed-producer risk.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Retained GitHub receipt sources prove internal consistency but not independent authenticity.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:1049-1073` checks retained JSON fields without a live fetch or signature; the 2026-08-22 story decision accepted and disclosed this limitation.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: NuGet.org package transport origin has no independent retained proof.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:649-683` checks archive identity and a derived URL but no service response or registry attestation; an earlier deferred item records the gap.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Failed Docker inspect can be mistaken for absence after a timed-out container run.
  evidence: `tools/capture-corrected-deployed-runtime-parity-smokes.py:114` returns cleanup success on any nonzero inspect result, including daemon failure; previously tracked as EH2.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: A container created after the sole Docker inspect can escape timeout cleanup.
  evidence: `tools/capture-corrected-deployed-runtime-parity-smokes.py:107-115` inspects once and immediately treats a missing name as clean; previously tracked as an accepted-subject race.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: A failed Docker run can leave a created container without cleanup.
  evidence: `tools/capture-corrected-deployed-runtime-parity-smokes.py:213-218,288` sets `container_created` only after exit 0 and deliberately skips removal on nonzero exit under an existing owner policy.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Retained-file and nuspec reads have no memory or decompression bound.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:398` reads whole files before size validation and `tools/release_evidence_handlers/v3.py:488` expands a nuspec with `archive.read`; DW-413/DW-433 already track the gap.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Subject-bound receipt limitation misdescribes the Test Architect record as credential-posted.
  evidence: `tools/deployed_runtime_parity_handlers/v1.py:83` says every receipt was posted with a role holder's credential, but `v1.py:1073` accepts a local self-attested Test Architect source; prior BH7 deferred the wording change because it re-mints the accepted subject.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Incomplete assembler rollback restores only closure.json and can leave mismatched support files.
  evidence: `tools/assemble-corrected-deployed-runtime-parity.py:98,489` restores only the prior closure after build_document writes the registry, inventory, and subject; the existing deferred-work ledger already records this packet consistency risk.

## Deferred from: Story 3.15 curl-isolation re-mint (2026-09-26)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md`
  summary: Reconcile the updated CI guide with Story 4.15's separate OQ8 evidence seal.
  evidence: The active Story 4.15 v5 packet pins `docs/ci.md` SHA-256 `8e12004e30ef06295afc7630de8fe0d07ce612d08572fa415d5eb78370058721`, while the current guide hashes to `e48417d8fdd99b4b07634d08ead0a5118e77f610ab4ed2d47dff4892c821f584` after the Story 3.15 curl-isolation re-mint. The guide's OQ8 section still calls v4 active and v5 pending although the selector and lifecycle select v5. The v5 source seal also binds the tracked tree, file modes, and root submodule pins, so changing one guide hash cannot restore the gate. A separate, reviewed Story 4.15 successor/source reseal is required after the source settles; preserve the existing v5 packet history and submodule pointers.
  verification: 2026-09-26 clean-HEAD check — `python3 tools/oq8-v5-packet.py --validate-active` exited 1 with `V5 source tree changed outside reviewed evidence and selector`; `python3 tools/validate-oq8-platform-evidence.py` exited 1 with `Story 4.15 v5 reviewed packet, selector, or lifecycle validation failed`. With this preparation's uncommitted files present, the active-v5 command instead exits 1 at its clean-committed-checkout precondition. Neither result approves current OQ8 evidence.
  status: open
