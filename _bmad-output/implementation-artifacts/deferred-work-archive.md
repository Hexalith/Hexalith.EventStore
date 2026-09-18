### DW-4: Follow-up review still recommended for 3-4-aspire-security-resource-naming after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-3-4-aspire-security-resource-naming.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260730-064902-1608; this entry preserves the lingering recommendation for a deliberate later review.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 116af80f implemented the independent Aspire-security follow-up guards; _bmad-output/implementation-artifacts/spec-independent-followup-reviews.md:93-94 records all five guards implemented and independently reviewed.

### DW-5: Follow-up review still recommended for 3-6-manifest-driven-release-packaging after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-3-6-manifest-driven-release-packaging.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260731-203343-5b29; this entry preserves the lingering recommendation for a deliberate later review.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 116af80f implemented the independent release-packaging follow-up guards in ReleasePackageManifestTests and release_package_contract.py; _bmad-output/implementation-artifacts/spec-independent-followup-reviews.md:93-94 records completion.

### DW-7: The reusable release workflow silently produces a green run that publishes nothing when `main` advances between release dispatch and the `Semantic Release` step. `actions/checkout` pins the dispatched `github.sha`; semantic-release then does its own `git fetch`, sees the live `origin/main` is ahead, prints `ℹ The local branch main is behind the remote one, therefore a new version won't be published.`, and exits 0. The operator sees a successful Release run and reasonably assumes a release was cut. Harden `domain-release.yml` to fail loudly (or emit an unmissable error annotation + non-success outcome) when the checked-out release SHA is no longer the live `main` tip at semantic-release time, instead of a silent no-op green.

origin: migrated from legacy ledger ("Deferred from: release-skip race diagnosis (2026-07-21, run 29799288142)"), 2026-08-30
location: actions/checkout
reason: source_spec: none owner_repo: `Hexalith.Builds` — reusable `.github/workflows/domain-release.yml` (currently pinned in this repo as `builds-execution-sha: cf04c419378dfe1bd3c41a9244b5e3283092056e`). NOT owned by `Hexalith.EventStore`; the EventStore `release.yml` only calls the reusable workflow, so this fix cannot land here. summary: The reusable release workflow silently produces a **green run that publishes nothing** when `main` advances between release dispatch and the `Semantic Release` step. `actions/checkout` pins the dispatched `github.sha`; semantic-release then does its own `git fetch`, sees the live `origin/main` is ahead, prints `ℹ The local branch main is behind the remote one, therefore a new version won't be published.`, and exits 0. The operator sees a successful Release run and reasonably assumes a release was cut. Harden `domain-release.yml` to **fail loudly** (or emit an unmissable error annotation + non-success outcome) when the checked-out release SHA is no longer the live `main` tip at semantic-release time, instead of a silent no-op green. evidence: Run https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29799288142 — dispatched 03:43:45Z on `41f5ed0f` (then the live tip; `verify-source` passed). At 03:52:04Z an automated submodule-bump commit `4245f0f8` ("fix: update submodule references…") landed on `main` (the concurrent bmad-loop auto-push hazard — see project memory `concurrent-bmad-loop-git`). At 04:04:06Z the release job checked out the pinned `41f5ed0f`; at 04:06:33Z semantic-release aborted with the "branch is behind remote" message. Job conclusion: success. No `v3.79.0` tag/release/packages were produced despite releasable `feat:`/`fix:` commits since `v3.78.0`. EventStore's own `verify-source` gate only re-checks the tip at run *start*, leaving a ~20-min window; closing the race durably requires a re-assert-tip-then-fail step inside the reusable workflow (Builds), or preventing pushes to `main` during a release. Immediate remediation for this incident was an operator re-dispatch once `main` was quiescent (run 29800856877).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/Github/publish-containers/tests/test_publish_script_contract.py:387 proves stale source now fails before semantic-release

### DW-8: Add a guardrail test asserting the `postgres:18.4` tag in `.github/workflows/integration.yml`'s "Pull PostgreSQL container image" step matches `Oq8PostgresqlFixture.PostgresImage`, so the two literals cannot silently drift.

origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
location: github/workflows/integration.yml
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md` summary: Add a guardrail test asserting the `postgres:18.4` tag in `.github/workflows/integration.yml`'s "Pull PostgreSQL container image" step matches `Oq8PostgresqlFixture.PostgresImage`, so the two literals cannot silently drift. evidence: Blind-hunter review of the CI fix -- the workflow comment asks a human to keep the tag in sync but nothing enforces it; the repo already has this pattern for release authority (`ContainerPublishingGovernanceTests.cs`) but not for this image tag.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs:11 pins and compares the workflow and fixture image identities

### DW-10: Pin the live-sidecar PostgreSQL image by digest (`postgres@sha256:...`) instead of the mutable `18.4` tag, with a documented rotation process.

origin: migrated from legacy ledger ("Deferred from: live-sidecar PostgreSQL image pull CI fix (2026-07-20)"), 2026-08-30
location: Oq8PostgresqlFixture.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29738838856-fix-ci-cd.md` summary: Pin the live-sidecar PostgreSQL image by digest (`postgres@sha256:...`) instead of the mutable `18.4` tag, with a documented rotation process. evidence: Blind-hunter review of the CI fix -- a mutable tag gives no guarantee the bits pulled today match the bits validated previously; digest pinning needs coordinated changes to both the workflow and `Oq8PostgresqlFixture.cs`, out of scope for the minimal unblock-CI fix.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .github/workflows/integration.yml:80 pulls PostgreSQL by immutable sha256 digest

### DW-13: Generalize the reusable publication preflight's hard-coded EventStore package count of exactly 14 so other callers can supply their own immutable expected inventory size without weakening EventStore's manifest contract.

origin: migrated from legacy ledger ("Deferred from: immutable manual release hardening (2026-07-20)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md` summary: Generalize the reusable publication preflight's hard-coded EventStore package count of exactly 14 so other callers can supply their own immutable expected inventory size without weakening EventStore's manifest contract. evidence: The shared validator currently enforces `len(package_ids) == 14`; that is correct for EventStore but makes the otherwise reusable release workflow product-specific.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/.github/workflows/domain-release.yml:86 exposes caller-supplied expected-package-count

### DW-14: Give each container mapping its own frozen repository identity and phase evidence when multiple container mappings share one release invocation.

origin: migrated from legacy ledger ("Deferred from: immutable manual release hardening (2026-07-20)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-simplify-release-architecture.md` summary: Give each container mapping its own frozen repository identity and phase evidence when multiple container mappings share one release invocation. evidence: The current EventStore caller has one approved mapping, while the shared publisher reuses one preflight evidence directory and frozen identity; a second mapping would collide with the first mapping's repository identity.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/Github/publish-containers/publish-containers.sh:167 freezes all repositories and line 187 creates per-mapping evidence

### DW-19: Expose an authoritative persisted global-position/watermark to projections and `QueryCursorScope`, consumed by Hexalith.Projects Story 6.1-P2's watermark-replay/restart requirement.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: EventEnvelopeAssertions.cs
reason: source_spec: none summary: Expose an authoritative persisted global-position/watermark to projections and `QueryCursorScope`, consumed by Hexalith.Projects Story 6.1-P2's watermark-replay/restart requirement. evidence: Split from the 6.1-P2 dual-principal query envelope + safe-denial boundary work at Jerome's direction 2026-07-18 — the watermark is largely independent of the identity/envelope and safe-denial work (which are coupled to each other) and can ship separately; a `GlobalPosition` already exists per-event at persistence time (`EventEnvelopeAssertions.cs`, `EventEnvelopeBuilder.cs`) but nothing today exposes it as an authoritative watermark.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs:71 exposes AddProjectionWatermark for a positive persisted global position

### DW-20: Epic D retrospective follow-through requires a dedicated REST generator hardening story or backlog item. Scope it from the D5/D7 deferred items below rather than scattering generator diagnostics into unrelated security, correctness, or UI stories. Minimum scope: unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: RestQueryBinding
reason: 2026-07-05: Epic D retrospective follow-through requires a dedicated REST generator hardening story or backlog item. Scope it from the D5/D7 deferred items below rather than scattering generator diagnostics into unrelated security, correctness, or UI stories. Minimum scope: unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, and generated external API error-semantics coverage.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md:29-40 records the requested first wave implemented; _bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md:17 and :246-259 record done status and every minimum-scope result.

### DW-21: Query freshness/projection metadata needed a platform-owned gateway contract before UI or generated REST stories could treat stale/current state or projection version as production-backed evidence. RESOLVED 2026-07-11 by Story 2.8 / AD-15 for EventStore route provenance, route-aware ETags, and fail-safe consumers. Genuine persisted-age evidence remains the separate D6 handoff; the Tenants producer cleanup remains Story 4.7.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: n/a
reason: 2026-07-05: Query freshness/projection metadata needed a platform-owned gateway contract before UI or generated REST stories could treat stale/current state or projection version as production-backed evidence. **RESOLVED 2026-07-11 by Story 2.8 / AD-15** for EventStore route provenance, route-aware ETags, and fail-safe consumers. Genuine persisted-age evidence remains the separate D6 handoff; the Tenants producer cleanup remains Story 4.7.
status: done 2026-07-11
archived: 2026-09-18
resolution: Query freshness/projection metadata needed a platform-owned gateway contract before UI or generated REST stories could treat stale/current state or projection version as production-backed evidence. RESOLVED 2026-07-11 by Story 2.8 / AD-15 for EventStore route provenance, route-aware ETags, and fail-safe consumers. Genuine persisted-age evidence remains the separate D6 handoff; the Tenants producer cleanup remains Story 4.7.

### DW-22: Generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details before accepting a live-smoke blocker. → tracked as Story 3.8 (Epic 3, companion to 3.1); re-homed from TEST-1.1 on 2026-07-07. RESOLVED 2026-07-07 by Story 3.8 — `scripts/generated-api-smoke-preflight.sh`; AC10 live-topology gate met (generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, support-safe failure details).

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: scripts/generated-api-smoke-preflight.sh
reason: 2026-07-05: Generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details before accepting a live-smoke blocker. → tracked as Story 3.8 (Epic 3, companion to 3.1); re-homed from TEST-1.1 on 2026-07-07. **RESOLVED 2026-07-07 by Story 3.8** — `scripts/generated-api-smoke-preflight.sh`; AC10 live-topology gate met (generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, support-safe failure details).
status: done 2026-07-07
archived: 2026-09-18
resolution: Generated API proof stories need a reusable DAPR/Aspire smoke preflight that reports placement/scheduler availability, generated API endpoint URLs, DAPR sidecar state, and support-safe failure details before accepting a live-smoke blocker. → tracked as Story 3.8 (Epic 3, companion to 3.1); re-homed from TEST-1.1 on 2026-07-07. RESOLVED 2026-07-07 by Story 3.8 — `scripts/generated-api-smoke-preflight.sh`; AC10 live-topology gate met (generated API endpoints, DAPR sidecar readiness, placement/scheduler readiness, support-safe failure details).

### DW-23: Packaging governance tests hard-code external dependency patch versions. Consider a lower-maintenance guard that still proves central version pins and emitted package metadata stay aligned, so routine published package bumps do not require brittle test-only edits.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: n/a
reason: 2026-07-01: Packaging governance tests hard-code external dependency patch versions. Consider a lower-maintenance guard that still proves central version pins and emitted package metadata stay aligned, so routine published package bumps do not require brittle test-only edits.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs:28-41 intentionally avoids a patch-version assertion while proving one concrete nonblank central pin.

### DW-24: Handler-backed query routes need explicit provenance so the gateway can decide whether projection ETags are valid for the response.

origin: migrated from legacy ledger ("Existing deferred work"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-1-2-domain-query-handler-routing.md` summary: Handler-backed query routes need explicit provenance so the gateway can decide whether projection ETags are valid for the response. evidence: `HandlerAwareQueryRouter` already used the same `QueryRouterResult` shape as projection routes before this story, and `QueriesController` falls back to request/domain projection ETag lookup when no projection type is supplied; changing that safely needs a separate route-provenance contract rather than metadata passthrough alone. status: reconciled 2026-07-11 — see the reconciliation section below.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs:81 stamps handler route metadata explicitly

### DW-25: The EventStore platform portion of AD-15 is owned and implemented by Story 2.8: additive provenance contract, authoritative router stamping, route-first conditional evaluation, projection-only freshness/ETag evidence, and fail-safe client/generated REST behavior.

origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
location: n/a
reason: The EventStore platform portion of AD-15 is owned and implemented by **Story 2.8**: additive provenance contract, authoritative router stamping, route-first conditional evaluation, projection-only freshness/ETag evidence, and fail-safe client/generated REST behavior.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs:94 stamps HandlerComputed provenance authoritatively

### DW-26: The 2026-07-05 gateway-contract prerequisite is superseded for metadata propagation by AD-14 + Stories 1.2/1.3/2.2, and for EventStore route provenance enforcement by AD-15 + Story 2.8.

origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
location: n/a
reason: The 2026-07-05 gateway-contract prerequisite is superseded for metadata propagation by **AD-14** + Stories 1.2/1.3/2.2, and for EventStore route provenance enforcement by **AD-15** + Story 2.8.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:212 stamps ProjectionBacked provenance, superseding the old prerequisite

### DW-27: Story 4.7 is now Tenants-only follow-up.

origin: migrated from legacy ledger ("Route-provenance contract reconciliation (updated 2026-07-11)"), 2026-08-30
location: references/Hexalith.Tenants/.../TenantQueryResult.cs
reason: **Story 4.7 is now Tenants-only follow-up.** It retains the producer cleanup that stops aliasing `ProjectionVersion := ETag` in `references/Hexalith.Tenants/.../TenantQueryResult.cs`; no EventStore platform enforcement remains assigned to Story 4.7.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs:23-34 preserves normalized ETag metadata without aliasing it into ProjectionVersion.

### DW-31: REST generator silently drops a `record struct` contract carrying `[RestRoute]` (the `TypeKind != Class` check returns null) with no HESREST diagnostic — inconsistent with every other unsupported-shape path, which reports a diagnostic. Add a diagnostic or explicitly support the shape.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: RestRoute
reason: REST generator silently drops a `record struct` contract carrying `[RestRoute]` (the `TypeKind != Class` check returns null) with no HESREST diagnostic — inconsistent with every other unsupported-shape path, which reports a diagnostic. Add a diagnostic or explicitly support the shape.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs:78-108 now verifies source and referenced unsupported record/struct contracts produce HESREST006.

### DW-32: Referenced-message discovery (`RestApiMessageParser.ParseReferenced`) is driven off `CompilationProvider` and emits a reference-equality `ImmutableArray`, so it re-runs the referenced-assembly walk on every compilation and weakens IDE incrementality. Consistent with the generator's pre-existing CompilationProvider usage; perf-only. Consider an equatable model/comparer if editor responsiveness regresses.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-02)"), 2026-08-30
location: RestApiMessageParser.ParseReferenced
reason: Referenced-message discovery (`RestApiMessageParser.ParseReferenced`) is driven off `CompilationProvider` and emits a reference-equality `ImmutableArray`, so it re-runs the referenced-assembly walk on every compilation and weakens IDE incrementality. Consistent with the generator's pre-existing CompilationProvider usage; perf-only. Consider an equatable model/comparer if editor responsiveness regresses.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs:31-49 applies RestApiMessageDescriptorArrayComparer to collected, referenced, and combined descriptor arrays.

### DW-36: Command contracts with duplicate JSON property names are not diagnosed; the new duplicate JSON-name check only runs for queries, so generated command serialization/model-binding can still fail later. Deferred as command/generator hardening outside the D5 query proof.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)"), 2026-08-30
location: n/a
reason: Command contracts with duplicate JSON property names are not diagnosed; the new duplicate JSON-name check only runs for queries, so generated command serialization/model-binding can still fail later. Deferred as command/generator hardening outside the D5 query proof.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:97-100 validates duplicate JSON names for every message; RestApiDiagnosticTests.cs:155-185 covers commands.

### DW-38: Query JSON names are deduplicated with `StringComparer.Ordinal`; names differing only by case can still bind ambiguously through query string/model-binding conventions. Deferred as generator hardening outside the D5 query proof.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03)"), 2026-08-30
location: StringComparer.Ordinal
reason: Query JSON names are deduplicated with `StringComparer.Ordinal`; names differing only by case can still bind ambiguously through query string/model-binding conventions. Deferred as generator hardening outside the D5 query proof.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1299-1307 uses StringComparer.OrdinalIgnoreCase; RestApiDiagnosticTests.cs:166-185 covers case-only duplicates.

### DW-39: Route-template validator (`RestApiRouteTemplateParser.GetTemplateError`) false-rejects legitimate inline route constraints containing braces, e.g. `{id:regex(^\d{3}$)}`: `close` binds to the constraint's inner `}`, so the parameter text contains `{` and is rejected as "unescaped brace". Generator hardening; no D5 route uses constraints.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: id:regex(^\d{3}$
reason: Route-template validator (`RestApiRouteTemplateParser.GetTemplateError`) false-rejects legitimate inline route constraints containing braces, e.g. `{id:regex(^\d{3}$)}`: `close` binds to the constraint's inner `}`, so the parameter text contains `{` and is rejected as "unescaped brace". Generator hardening; no D5 route uses constraints.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:351 verifies a regex constraint containing escaped braces is emitted successfully.

### DW-40: `RestApiControllerEmitter.RouteParameterMatchesProperty` compares the C# Name with `OrdinalIgnoreCase` but the JsonName with `Ordinal`, while route binding is case-insensitive. A route token matching a property's JsonName only case-insensitively is not excluded from the emitted query payload → phantom / double-bound parameter. Generator hardening; not exercised by D5.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: RestApiControllerEmitter.RouteParameterMatchesProperty
reason: `RestApiControllerEmitter.RouteParameterMatchesProperty` compares the C# Name with `OrdinalIgnoreCase` but the JsonName with `Ordinal`, while route binding is case-insensitive. A route token matching a property's JsonName only case-insensitively is not excluded from the emitted query payload → phantom / double-bound parameter. Generator hardening; not exercised by D5.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1590-1592 compares both CLR and JSON names with OrdinalIgnoreCase.

### DW-41: Query-binding expression (`RestApiControllerEmitter.GetQueryBindingExpression`) silently falls back to aggregate `"index"` / empty entity when `AggregateSource`/`EntitySource` is neither `Constant` nor `Route` (malformed `[RestQueryBinding]` or a future enum member); the validator only guards the `"Route"`-missing case, so no HESREST diagnostic is emitted. Same silent-drop class the diagnostics work aims to close. Generator hardening; `[RestQueryBinding]` not used by D5.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: RestApiControllerEmitter.GetQueryBindingExpression
reason: Query-binding expression (`RestApiControllerEmitter.GetQueryBindingExpression`) silently falls back to aggregate `"index"` / empty entity when `AggregateSource`/`EntitySource` is neither `Constant` nor `Route` (malformed `[RestQueryBinding]` or a future enum member); the validator only guards the `"Route"`-missing case, so no HESREST diagnostic is emitted. Same silent-drop class the diagnostics work aims to close. Generator hardening; `[RestQueryBinding]` not used by D5.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1335-1375 rejects unsupported and out-of-range query-binding sources; RestApiDiagnosticTests.cs:188-200 verifies HESREST012.

### DW-42: `[RestQueryBinding]` with `Constant` entity source and no supplied value produces a silent empty-string entity id (`binding.EntityValue ?? string.Empty` → `Literal("")`). Generator hardening; not used by D5.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: RestQueryBinding
reason: `[RestQueryBinding]` with `Constant` entity source and no supplied value produces a silent empty-string entity id (`binding.EntityValue ?? string.Empty` → `Literal("")`). Generator hardening; not used by D5.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1369-1373 rejects missing constant entity values; RestApiDiagnosticTests.cs:188-200 covers whitespace constants.

### DW-44: Empty (as opposed to absent) `DAPR_HTTP_PORT` yields the base address `http://localhost:` and `new Uri(...)` throws `UriFormatException` at startup with an opaque message, in both `Sample.Api` and `Sample.BlazorUI`. The `?? "3500"` fallback only guards null. Minor robustness.

origin: migrated from legacy ledger ("Deferred from: code review of D-5-proof-sample-blazorui-queries (2026-07-03, re-review)"), 2026-08-30
location: DAPR_HTTP_PORT
reason: Empty (as opposed to absent) `DAPR_HTTP_PORT` yields the base address `http://localhost:` and `new Uri(...)` throws `UriFormatException` at startup with an opaque message, in both `Sample.Api` and `Sample.BlazorUI`. The `?? "3500"` fallback only guards null. Minor robustness.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: samples/Hexalith.EventStore.Sample.Api/Services/DaprHttpEndpointResolver.cs:41 handles blank ports and validates the numeric range

### DW-46: HIGH — RESOLVED/SUPERSEDED. AD-14 added `QueryRouterResult.Metadata` and the carrier path; Story 2.8 / AD-15 now stamps route provenance, preserves genuine producer freshness/version evidence only for `ProjectionBacked`, and gates generated headers accordingly. D6 remains responsible for additional persisted-age production sources, while Story 4.7 retains only the Tenants `ProjectionVersion := ETag` producer cleanup.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: QueryRouterResult.Metadata
reason: HIGH — **RESOLVED/SUPERSEDED.** AD-14 added `QueryRouterResult.Metadata` and the carrier path; Story 2.8 / AD-15 now stamps route provenance, preserves genuine producer freshness/version evidence only for `ProjectionBacked`, and gates generated headers accordingly. D6 remains responsible for additional persisted-age production sources, while Story 4.7 retains only the Tenants `ProjectionVersion := ETag` producer cleanup.
status: done 2026-07-11
archived: 2026-09-18
resolution: HIGH — RESOLVED/SUPERSEDED. AD-14 added `QueryRouterResult.Metadata` and the carrier path; Story 2.8 / AD-15 now stamps route provenance, preserves genuine producer freshness/version evidence only for `ProjectionBacked`, and gates generated headers accordingly. D6 remains responsible for additional persisted-age production sources, while Story 4.7 retains only the Tenants `ProjectionVersion := ETag` producer cleanup.

### DW-48: MEDIUM — External REST error-semantics coverage gap. The 2054-line `TenantsQueryControllerIntegrationTests` was replaced by a 296-line generated-controller test covering 401/request-shape/freshness/ETag-304 but not 403/RBAC, gateway-failure → problem-details, or invalid-cursor at the generated surface. Add once the transport-fault and 400-vs-500 patches land.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: TenantsQueryControllerIntegrationTests
reason: MEDIUM — External REST error-semantics coverage gap. The 2054-line `TenantsQueryControllerIntegrationTests` was replaced by a 296-line generated-controller test covering 401/request-shape/freshness/ETag-304 but not 403/RBAC, gateway-failure → problem-details, or invalid-cursor at the generated surface. Add once the transport-fault and 400-vs-500 patches land.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs:437,561,589,724 directly covers generated 403, 503, invalid-cursor 400, and ETag/304 semantics.

### DW-49: LOW — Generator silently falls back to aggregate `"index"` for invalid `[RestQueryBinding]` sources (None / out-of-range enum / empty Constant) with no HESREST diagnostic, and `RestApiQueryBindingDescriptor.GetHashCode` can NRE on a null constant value. Re-logged from the D5 review; now exercised by D7 `[RestQueryBinding]` usage so worth prioritizing.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: RestQueryBinding
reason: LOW — Generator silently falls back to aggregate `"index"` for invalid `[RestQueryBinding]` sources (None / out-of-range enum / empty Constant) with no HESREST diagnostic, and `RestApiQueryBindingDescriptor.GetHashCode` can NRE on a null constant value. Re-logged from the D5 review; now exercised by D7 `[RestQueryBinding]` usage so worth prioritizing.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1335-1375 now diagnoses invalid, out-of-range, and empty query bindings before emission.

### DW-50: LOW — RESOLVED 2026-07-31 by Story 2.12. `Hexalith.Tenants.csproj` now gives Gateway and DomainService complementary source/package edges under the shared dependency-mode contract. The current graph cannot mix source Gateway with package DomainService. After the Tenants solution restore hit `MSB3202` on forbidden/uninitialized nested submodule projects, package-mode validation covered all 17 tracked Tenants projects individually with zero warnings or errors.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04)"), 2026-08-30
location: Hexalith.Tenants.csproj
reason: LOW — **RESOLVED 2026-07-31 by Story 2.12.** `Hexalith.Tenants.csproj` now gives Gateway and DomainService complementary source/package edges under the shared dependency-mode contract. The current graph cannot mix source Gateway with package DomainService. After the Tenants solution restore hit `MSB3202` on forbidden/uninitialized nested submodule projects, package-mode validation covered all 17 tracked Tenants projects individually with zero warnings or errors.
status: done 2026-07-31
archived: 2026-09-18
resolution: LOW — RESOLVED 2026-07-31 by Story 2.12. `Hexalith.Tenants.csproj` now gives Gateway and DomainService complementary source/package edges under the shared dependency-mode contract. The current graph cannot mix source Gateway with package DomainService. After the Tenants solution restore hit `MSB3202` on forbidden/uninitialized nested submodule projects, package-mode validation covered all 17 tracked Tenants projects individually with zero warnings or errors.

### DW-52: MEDIUM — `ListByCursorAsync` (`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:482-488`) lets a `StatusCode = 200` `EventStoreGatewayException` (null/JSON-`null`/shape-mismatch payload from the generic `SubmitQueryAsync<T>`) escape into the Blazor circuit — it catches only `IsUnauthorized` and `IsUnavailableOrInvalid` (`>= 400`), unlike the sibling methods' unfiltered catch-all. Undermines AC7 fail-closed. Pre-existing pattern carried through the migration (filters unchanged by D7); trivially patchable by mirroring the sibling catch-all. Low likelihood — needs a malformed 200 (projection/contract bug).

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:482-488
reason: MEDIUM — `ListByCursorAsync` (`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:482-488`) lets a `StatusCode = 200` `EventStoreGatewayException` (null/JSON-`null`/shape-mismatch payload from the generic `SubmitQueryAsync<T>`) escape into the Blazor circuit — it catches only `IsUnauthorized` and `IsUnavailableOrInvalid` (`>= 400`), unlike the sibling methods' unfiltered catch-all. Undermines AC7 fail-closed. Pre-existing pattern carried through the migration (filters unchanged by D7); trivially patchable by mirroring the sibling catch-all. Low likelihood — needs a malformed 200 (projection/contract bug).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1932 now catches every EventStoreGatewayException on the list path

### DW-53: LOW — Tenant-list path has no invalid-cursor recovery (`TenantQueryGateway.cs:485-487, 936`): `IsUnavailableOrInvalid` treats every `>= 400` alike, so a 400 invalid/expired list cursor surfaces as "gateway unavailable" instead of resetting to page 1 (as `GetTenantAuditAsync` does via `IsInvalidAuditCursor`). Low likelihood — list cursors are server-issued protected cursors.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
location: TenantQueryGateway.cs:485-487
reason: LOW — Tenant-list path has no invalid-cursor recovery (`TenantQueryGateway.cs:485-487, 936`): `IsUnavailableOrInvalid` treats every `>= 400` alike, so a 400 invalid/expired list cursor surfaces as "gateway unavailable" instead of resetting to page 1 (as `GetTenantAuditAsync` does via `IsInvalidAuditCursor`). Low likelihood — list cursors are server-issued protected cursors.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1900 implements invalid-list-cursor page-one recovery

### DW-54: (re-confirms existing D7 entry) LOW — empty `Constant` `[RestQueryBinding]` value emits an empty aggregate id with no HESREST diagnostic (`RestApiControllerEmitter.cs:376`). Same silent-drop class as the already-listed generator-diagnostic hardening item; the GetHashCode-NRE sub-claim was refuted (`GetString` never returns null).

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
location: RestApiControllerEmitter.cs:376
reason: (re-confirms existing D7 entry) LOW — empty `Constant` `[RestQueryBinding]` value emits an empty aggregate id with no HESREST diagnostic (`RestApiControllerEmitter.cs:376`). Same silent-drop class as the already-listed generator-diagnostic hardening item; the GetHashCode-NRE sub-claim was refuted (`GetString` never returns null).
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1350-1373 rejects empty Constant aggregate and entity values with HESREST012.

### DW-55: (re-confirms existing D7 entry) — RESOLVED/SUPERSEDED by Story 2.8 / AD-15 for EventStore provenance enforcement and generated header gating; see the reconciled HIGH item above. D6 and the Tenants-only Story 4.7 producer cleanup remain separate.

origin: migrated from legacy ledger ("Deferred from: code review of D-7-proof-tenants-ui-host-submodule (2026-07-04, re-review round 2)"), 2026-08-30
location: n/a
reason: (re-confirms existing D7 entry) — **RESOLVED/SUPERSEDED by Story 2.8 / AD-15** for EventStore provenance enforcement and generated header gating; see the reconciled HIGH item above. D6 and the Tenants-only Story 4.7 producer cleanup remain separate.
status: done 2026-07-11
archived: 2026-09-18
resolution: (re-confirms existing D7 entry) — RESOLVED/SUPERSEDED by Story 2.8 / AD-15 for EventStore provenance enforcement and generated header gating; see the reconciled HIGH item above. D6 and the Tenants-only Story 4.7 producer cleanup remain separate.

### DW-71: Generated command success responses hard-code `/api/v1/commands/status/{id}` as a relative status `Location`.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: /api/v1/commands/status/{id
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` summary: Generated command success responses hard-code `/api/v1/commands/status/{id}` as a relative status `Location`. evidence: `RestApiControllerEmitter.AppendCommandAction` writes `Response.Headers["Location"] = "/api/v1/commands/status/" + Uri.EscapeDataString(...)`, while the platform `CommandsController.Submit` builds an absolute URI from the current request host. Dedicated generated API hosts may not expose that relative status route, so status-location policy needs a focused generated-host design. resolution: Policy defined as architecture invariant AD-17 (absolute-to-gateway, fail-closed when unconfigured, single-sourced gateway status key) via `sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`. status: **RESOLVED 2026-07-07 by Story 2.6** — `RestApiControllerEmitter.AppendCommandAction` no longer emits any hard-coded relative `/api/v1/commands/status/` literal; the generated command 202 resolves an **absolute** `Location` at request time through the injected `ICommandStatusLocationBuilder` (`Hexalith.EventStore.Client.Gateway`), and emits **no** `Location` header when the gateway status base is unconfigured (fail-closed per AD-10). Absorbs rest-generator-hardening Second-Wave item **S2**. Evidence: `RestApiControllerGenerationTests` + `RestApiGeneratedControllerErrorSemanticsTests` (110/110).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:282 uses ICommandStatusLocationBuilder instead of a relative literal

### DW-73: Generated Sample API command success responses expose the generator's relative `/api/v1/commands/status/{id}` status location even though the external API host does not itself map that status route.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: /api/v1/commands/status/{id
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md` summary: Generated Sample API command success responses expose the generator's relative `/api/v1/commands/status/{id}` status location even though the external API host does not itself map that status route. evidence: `SampleApiGeneratedControllerRuntimeTests` proves the compiled Sample API generated command action emits the existing generated `Location` header; `Sample.Api` maps only generated controllers and default endpoints, so polling that relative status URL depends on an external routing/proxy policy not owned by this proof story. resolution: Policy defined as architecture invariant AD-17 (absolute-to-gateway, fail-closed when unconfigured, single-sourced gateway status key) via `sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`. status: **RESOLVED 2026-07-07 by Story 2.6** — `Sample.Api` opts into the absolute status base via `AddEventStoreCommandStatusLocation` (config `EventStore:GatewayStatusBase`) and defaults fail-closed; `SampleApiGeneratedControllerRuntimeTests` now proves **both** absolute-when-configured (`https://gateway.example/api/v1/commands/status/{statusId}`, never relative) and no-`Location`-when-unconfigured against the real compiled `CounterRestController` (Sample.Tests 116/116).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: samples/Hexalith.EventStore.Sample.Api/Program.cs:84 configures the absolute gateway status base and fails closed when absent

### DW-74: Sample DAPR app-id handlers append `dapr-app-id` and `dapr-api-token` headers without replacing preexisting values.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md` summary: Sample DAPR app-id handlers append `dapr-app-id` and `dapr-api-token` headers without replacing preexisting values. evidence: `samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs` and `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs` call `TryAddWithoutValidation` for DAPR routing headers, so a caller-provided conflicting value could produce duplicate sidecar routing/token headers; this handler behavior predates the generated Sample API host proof and needs a focused outbound-DAPR-header policy fix. status: reconciled 2026-07-07 (sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy). Policy decided and formalized as architecture invariant **AD-18** (Outbound Sidecar Control-Plane Headers Are Handler-Owned): replace-not-append, handler-owned, innermost handler, caller/inbound values never routed. Scope is wider than the two files named here — the byte-identical defect also lives in `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs`. Enforcement owned by **Story 2.7** (centralize a single handler in `Hexalith.EventStore.Client` via `AddEventStoreGatewayClient(appId, apiToken?)`, delete the 3 in-repo copies, add pre-existing-header replacement test + a guardrail structural test). The identical `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` copy is a **coordinated submodule follow-up requiring maintainer approval** (Story 2.4 lineage), not modified under Story 2.7.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs:12 removes and replaces handler-owned DAPR headers

### DW-75: Raw SignalR hub leave calls do not validate projection type or tenant id before building and removing malformed group names.

origin: migrated from legacy ledger ("Deferred from: follow-up review of spec-1-5-domain-module-hosting-observability (2026-07-06)"), 2026-08-30
location: ArgumentException.ThrowIfNullOrWhiteSpace(projectionType/tenantId
reason: source_spec: `_bmad-output/implementation-artifacts/spec-2-5-scoped-metadata-rich-projection-notifications.md` summary: Raw SignalR hub leave calls do not validate projection type or tenant id before building and removing malformed group names. evidence: `ProjectionChangedHub.LeaveGroupCoreAsync` validates scoped suffixes added by Story 2.5 but still lacks the projection/tenant null, blank, and colon guards that `JoinGroupCoreAsync` applies; malformed raw `LeaveGroup` or `LeaveGroupScoped` calls can reach `RemoveFromGroupAsync` and debug logs with invalid group names. The leave path and its projection/tenant validation gap pre-date this story, while the scoped-suffix validation was the only changed behavior here. status: **RESOLVED 2026-07-07 by sprint-change-proposal-2026-07-07-signalr-hub-leave-validation** — `LeaveGroupCoreAsync` now applies the same `ArgumentException.ThrowIfNullOrWhiteSpace(projectionType/tenantId)` + colon guards as `JoinGroupCoreAsync` (leave stays authorization-free by design). Covered by 5 new `ProjectionChangedHubTests` (leave/scoped-leave projection+tenant colon and null/blank), Server.Tests green (34/34). Satisfies Epic 2 retro Action #6 completion gate (same safe group rules as join).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:161 validates blank projection and tenant identifiers on leave

### DW-83: No production code calls `IProjectionStateEraser` — only the DI registration in `ServiceCollectionExtensions.cs:56`. The end-to-end read-model/checkpoint drift fix is unreachable from any wired in-tree path; it depends on a future Admin/GDPR-1 erasure trigger. Deferred as expected — the caller is exactly what the governing-contract decision (see Story 1.9 Review Findings) resolves. Do not add a caller in isolation before that decision.

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-11)"), 2026-08-30
location: ServiceCollectionExtensions.cs:56
reason: No production code calls `IProjectionStateEraser` — only the DI registration in `ServiceCollectionExtensions.cs:56`. The end-to-end read-model/checkpoint drift fix is unreachable from any wired in-tree path; it depends on a future Admin/GDPR-1 erasure trigger. Deferred as expected — the caller is exactly what the governing-contract decision (see Story 1.9 Review Findings) resolves. Do not add a caller in isolation before that decision.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:245 wires the production projection-erasure trigger through the authenticated admin boundary

### DW-84: Complete the in-progress Story 1.9 erasure refactor so the Server and Server.Tests projects compile again.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Complete the in-progress Story 1.9 erasure refactor so the Server and Server.Tests projects compile again. evidence: Pre-existing Story 1.9 working-tree changes delete `IProjectionStateEraser`, `ProjectionStateEraser`, and `ReadModelEraseTarget` while production registration and `StorageKeyIsolationTests` still reference them; `ProjectionCheckpointTracker` also exposes an internal capability through a public class.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:102 registers the completed projection erase coordinator graph

### DW-85: Preserve or explicitly version the released erasure API surface being removed by Story 1.9.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Preserve or explicitly version the released erasure API surface being removed by Story 1.9. evidence: The pre-existing Story 1.9 diff removes released interface members and public erasure types without an API-compatibility gate, creating source and binary breaks that the current concrete-class tests cannot detect.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelStoreReleasedShapeTests.cs:34 preserves the released interface and tests additive erasure seams

### DW-86: Make Story 1.9 erasure capability DI fail closed for custom stores and checkpoint trackers.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Make Story 1.9 erasure capability DI fail closed for custom stores and checkpoint trackers. evidence: The pre-existing registrations can bind a default DAPR eraser behind a custom non-capable `IReadModelStore` or unconditionally cast a custom `IProjectionCheckpointTracker`, risking wrong-backend mutation or resolution-time failure instead of `Unsupported`.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Server.Tests/Configuration/ProjectionEraseRegistrationTests.cs:55 proves the eraser shares the configured read-model store singleton

### DW-87: Wire and verify Story 1.9 projection slot discovery and canonical read-model address ownership end to end.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Wire and verify Story 1.9 projection slot discovery and canonical read-model address ownership end to end. evidence: The pre-existing slot/address types lack reliable registration and contract coverage, declarations can be skipped by handler-registration early returns, and no production writer currently proves it uses the same canonical key factory as erasure.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddressFactory.cs:48 wires aggregate-owned manifest discovery through the canonical address factory

### DW-88: Finish the Story 1.9 persisted erasure coordinator and lifecycle/admin boundary before exposing partial seams.

origin: migrated from legacy ledger ("Deferred from: review of spec-gh-29184319584-fix-live-sidecar-ci (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29184319584-fix-live-sidecar-ci.md` summary: Finish the Story 1.9 persisted erasure coordinator and lifecycle/admin boundary before exposing partial seams. evidence: The active story requires resumable coordination, rebuild-checkpoint deletion, delivery-last ordering, lifecycle serialization, active-rebuild refusal, structured outcomes, and an authenticated boundary, but the reviewed pre-existing diff does not yet provide those runtime components.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs:345 returns success only after the persisted resumable coordinator completes

### DW-92: Bound the correlation-index overflow marker so a hot shared correlationId is not permanently ambiguous.

origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
location: DaprCommandCorrelationIndex.cs:81
reason: source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md` summary: Bound the correlation-index overflow marker so a hot shared correlationId is not permanently ambiguous. evidence: DaprCommandCorrelationIndex.cs:81 refreshes OverflowExpiresAt on every over-capacity AddAsync, so a steadily-loaded correlationId stays Ambiguous (409) indefinitely even after the original 128 entries expire.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Commands/DaprCommandCorrelationIndex.cs:52-81,128-167 bounds overflow by TTL and prunes expiry; tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandCorrelationIndexTests.cs:188-232 proves expired overflow resolves to NotFound.

### DW-93: (Story 4.4) Prevent domain re-execution when a Recoverable (stored-but-unpublished) idempotency record expires after the retention window.

origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md` summary: (Story 4.4) Prevent domain re-execution when a Recoverable (stored-but-unpublished) idempotency record expires after the retention window. evidence: IdempotencyChecker.ClassifyAsync applies the bounded ExpiresAt to Recoverable records too (expiry check precedes the disposition branch), so a retry after 24h is treated as a miss and could re-execute the domain. Broader recovery is owned by Story 4.4.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs:348 exempts Recoverable records from ordinary expiry until publication recovery completes

### DW-94: Correct the drain activity message-id telemetry tag for legacy correlation-keyed drain records.

origin: migrated from legacy ledger ("Deferred from: code review of 4-2-resume-and-idempotency-integrity (2026-07-12)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md` summary: Correct the drain activity message-id telemetry tag for legacy correlation-keyed drain records. evidence: DrainUnpublishedEventsAsync sets eventstore.message_id to the tracking id (a correlationId for legacy records) before the real message id is added, undermining message-id-primary telemetry.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1377 keeps tracking identity separate and line 1428 tags the proven message id

### DW-95: Retained legacy aggregate-wide checkpoint feeds the empty-stream drift branch (`ProjectionUpdateOrchestrator.cs:129`). An erased/recreated identity that had a legacy checkpoint and later reads an empty stream logs spurious `CheckpointDriftDetected` (diagnostic noise only — no mutation, no suppressed delivery). Direct consequence of the human-approved Option A retained-legacy-key relaxation; revisit if diagnostic noise is a problem or if a bounded legacy-key cleanup is added.

origin: migrated from legacy ledger ("Deferred from: code review of story-1.9 (2026-07-13)"), 2026-08-30
location: ProjectionUpdateOrchestrator.cs:129
reason: Retained legacy aggregate-wide checkpoint feeds the empty-stream drift branch (`ProjectionUpdateOrchestrator.cs:129`). An erased/recreated identity that had a legacy checkpoint and later reads an empty stream logs spurious `CheckpointDriftDetected` (diagnostic noise only — no mutation, no suppressed delivery). Direct consequence of the human-approved Option A retained-legacy-key relaxation; revisit if diagnostic noise is a problem or if a bounded legacy-key cleanup is added.
status: done 2026-09-01
archived: 2026-09-18
resolution: closed by human decision: Retain the approved compatibility behavior because the extra diagnostic has no state or delivery impact.
decision: 2026-09-01 Accept diagnostic noise — Retain the approved compatibility behavior because the extra diagnostic has no state or delivery impact.

### DW-100: (HARD GATE for Story 1.12/1.13) Run the `ReadModelBatchLiveSidecarTests` lane in a working Tier-3 (real Redis/DAPR) environment before wiring the coordinated batch into production projection dispatch, and add the omitted Task-8 scenarios.

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-coordinated-read-model-batch-writes (2026-07-13, decision follow-up)"), 2026-08-30
location: ReadModelBatchLiveSidecarTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md` summary: (HARD GATE for Story 1.12/1.13) Run the `ReadModelBatchLiveSidecarTests` lane in a working Tier-3 (real Redis/DAPR) environment before wiring the coordinated batch into production projection dispatch, and add the omitted Task-8 scenarios. evidence: The live lane is the ONLY real-backend evidence for AC2/AC3/AC7/AC8 and for the story's founding premise (a void DAPR/Redis transaction can partially commit), but it never executed here (VSTest host exit 144 during collection-fixture startup; pre-existing `DaprETagServiceLiveSidecarTests` fails identically). Deterministic fakes/recorder are "request-shape evidence only, never completion proof." The authored `ReadModelBatchLiveSidecarTests.cs` also currently omits injected partial-prefix old-view visibility, conflict/abort restoration, and post-dispatch cancellation reconciliation despite Task 8 being checked. Decision 2026-07-13: deterministic evidence accepted to advance Story 1.10; the live run + missing-scenario authoring HARD-GATE the Story 1.12/1.13 production wiring. Also add deterministic transaction partial-commit coverage (see the [Review][Patch] item on `VerifyTransactionAsync`), which does NOT need the live env.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs:123 covers partial-prefix visibility, with conflict and cancellation cases at lines 164 and 205
gate: 1-12, 1-13

### DW-107: `HasFailures` blast radius on named-metadata rejection — a single domain service returning malformed/version-skewed named-projection metadata sets `hasFailures`, which makes `AdminOperationalIndexHostedService.StartAsync` skip ALL admin index writes AND the named-route catalog `Replace` for every app in the refresh; this is a once-at-startup load with no periodic retry, so named dispatch is disabled process-wide until restart. The atomic all-or-nothing publish is spec-mandated (§2); the cross-app coupling + missing refresh cadence is the broader concern.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
location: src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:37
reason: `HasFailures` blast radius on named-metadata rejection — a single domain service returning malformed/version-skewed named-projection metadata sets `hasFailures`, which makes `AdminOperationalIndexHostedService.StartAsync` skip ALL admin index writes AND the named-route catalog `Replace` for every app in the refresh; this is a once-at-startup load with no periodic retry, so named dispatch is disabled process-wide until restart. The atomic all-or-nothing publish is spec-mandated (§2); the cross-app coupling + missing refresh cadence is the broader concern. [src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:37] [verification-gap]
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:405-422 periodically retries metadata refresh for every known registration instead of loading only at startup.

### DW-110: `DomainProjectionCatalogRegistry` is in-memory and empty after a domain-service restart — until the gateway re-queries `/admin/operational-index-metadata` (a startup-only load), `Contains(fingerprint)` is false → `/project/v2` returns 400 `UnsupportedCapability` → the coordinator defers/retries. Overlaps the metadata refresh-cadence gap above.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-13)"), 2026-08-30
location: /admin/operational-index-metadata
reason: `DomainProjectionCatalogRegistry` is in-memory and empty after a domain-service restart — until the gateway re-queries `/admin/operational-index-metadata` (a startup-only load), `Contains(fingerprint)` is false → `/project/v2` returns 400 `UnsupportedCapability` → the coordinator defers/retries. Overlaps the metadata refresh-cadence gap above. [src/Hexalith.EventStore.DomainService/DomainProjectionCatalogRegistry.cs:8] [edge-case-hunter]
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:405-422 periodically re-queries metadata, while src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:316-327,634-646 re-registers the emitted fingerprint after restart.

### DW-112: The active-rebuild gate remains a pre-existing check-then-act race: `ProjectionEraseCoordinator` snapshots `HasActiveOperatorRebuildForDomainAsync` before lifecycle admission, so a rebuild can become active between the check and the actor call while `allowFreshBegin` remains true. Closing this requires rebuild admission to share a persisted lifecycle fence rather than relying on the existing point-in-time store query.

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-read-model-and-projection-checkpoint-erasure (2026-07-14)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs:143
reason: The active-rebuild gate remains a pre-existing check-then-act race: `ProjectionEraseCoordinator` snapshots `HasActiveOperatorRebuildForDomainAsync` before lifecycle admission, so a rebuild can become active between the check and the actor call while `allowFreshBegin` remains true. Closing this requires rebuild admission to share a persisted lifecycle fence rather than relying on the existing point-in-time store query. [`src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs:143`]
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-112 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-112 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-113: HTTP 200 with a literal `null` metadata body is treated as a successful empty load (`AdminOperationalIndexHostedService.cs:96`), so existing admin indexes can be rewritten from an incomplete response. This behavior predates Story 1.12; harden the legacy metadata loader to classify a null success body as a failed load before any index write or catalog replacement.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-asynchronous-multi-projection-dispatch (2026-07-14, chunk 1)"), 2026-08-30
location: AdminOperationalIndexHostedService.cs:96
reason: HTTP 200 with a literal `null` metadata body is treated as a successful empty load (`AdminOperationalIndexHostedService.cs:96`), so existing admin indexes can be rewritten from an incomplete response. This behavior predates Story 1.12; harden the legacy metadata loader to classify a null success body as a failed load before any index write or catalog replacement.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:169-175 classifies an HTTP-200 null metadata body as a failed load.

### DW-122: Repair or explicitly disposition the named-projection lifecycle cleanup defect before selecting an approved parity runtime.

origin: migrated from legacy ledger ("Deferred from: exact-SHA gate of 1-20-owner-approved-parity-closure-and-runtime-pin (2026-07-16)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` status: implementation-complete/evidence-confirmed summary: Repair or explicitly disposition the named-projection lifecycle cleanup defect before selecting an approved parity runtime. historical_evidence: Clean detached candidate `85877902f8d60a466ab90cd8b68b53838863db1c` built Release with 0 warnings/errors and passed the broad unit lanes, but `Hexalith.EventStore.Server.LiveSidecar.Tests.dll` finished 42 passed / 2 failed. The isolated `NamedProjectionDispatchLiveSidecarTests` run finished 5 passed / 1 failed, and `NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger` reproduced alone at 0 passed / 1 failed because the Redis lifecycle hash remained present instead of returning to the idle/absent baseline. The initial full lane also reported an unreleased lifecycle lease in `ConcurrentDuplicateReverseAndConflict_StayEquivalentToOneInOrderDelivery`. closure_evidence: Corrective commit `7b73a2f5cde990b0a026ec280f7620d067b3d110` is present in exact clean detached commit `772cdfefa8163704de0f57042af5b0507c1ac771`. At that commit the exact formerly failing normal-delivery method passed 1/1, `NamedProjectionDispatchLiveSidecarTests` passed 6/6, and the complete live-sidecar lane passed 44/44. Story 1.16's separate named durable follow-up-review disposition remains open; it is not an implementation failure.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: commit 7b73a2f5cde990b0a026ec280f7620d067b3d110 corrected lifecycle cleanup and passed the formerly failing live-sidecar lane

### DW-123: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20's tested runtime SHA.

origin: migrated from legacy ledger ("Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)"), 2026-08-30
location: global.json
reason: source_spec: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` status: implementation-complete/evidence-confirmed owner: EventStore build/release maintainer summary: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20's tested runtime SHA. historical_evidence: - `global.json` formerly pinned SDK `10.0.299` (pre-baseline seed); - the installed SDK and host/runtime were later observed as `10.0.302` / `10.0.9` before ASP.NET caught up; - effective central ASP.NET pins were `10.0.9`. closure_evidence: - SDK correction `d6c849aaf8f77f967377f72b763bd44b3131a713`, ASP.NET correction `3a43d5e6151ebc51e945bf1b6cecda92fd198a09`, and validation hardening `8c70efb08b1bf2fcd077ad930c5827d1ab1594da` are present in commit `772cdfefa8163704de0f57042af5b0507c1ac771`; - the exact executable preflight observed repository and installed SDK `10.0.302`, effective ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10`. consequence: The baseline mismatch no longer blocks the current readiness audit. A later candidate, package build, or publication must repeat the executable preflight and fails closed if the baseline regresses. closure: 1. Update the repository seed and central ASP.NET pins to the AD-11 baseline, or record named architecture-owner approval for a newer replacement. 2. Install and capture the matching SDK/runtime. 3. Restore and build `Hexalith.EventStore.slnx` in Release. 4. Run the focused package/runtime validation owned by the correction. 5. Commit the correction independently. 6. Use that resulting commit, or a reviewed descendant, as the Story 1.20 candidate runtime. reopen_trigger: Any Story 1.20 packet update, package build, container publication, or owner-approval request that names a runtime without satisfying this baseline.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: global.json:3 and references/Hexalith.Builds/Props/Directory.Packages.props:172 carry a newer approved SDK/ASP.NET baseline

### DW-124: Reconcile stale sample domain registrations and prove Tenants handler routing in the real source topology before selecting the Story 1.20 runtime.

origin: migrated from legacy ledger ("Deferred from: Story 1.20 current-HEAD source-topology gate (2026-07-17)"), 2026-08-30
location: src/Hexalith.EventStore/appsettings.Development.json
reason: source_spec: `_bmad-output/implementation-artifacts/2-7-tenants-compatibility-and-package-mode-validation.md` status: implementation-complete/evidence-confirmed owner: EventStore Story 2.7 summary: Reconcile stale sample domain registrations and prove Tenants handler routing in the real source topology before selecting the Story 1.20 runtime. evidence: - the original packet harness forced `UseHexalithProjectReferences=false`, compiled no `tenants` AppHost resource, and then timed out waiting for it; - the corrected exact-source run at `772cdfefa8163704de0f57042af5b0507c1ac771` compiled the Tenants resource and reproduced twice as 0/1 with HTTP 404 / `query_projection_missing`; - Tenants operational-metadata calls returned HTTP 200, but merged base configuration still registers `orders` and `inventory` against the sample service while the current sample discovers only `counter` and `greeting`; - an absent configured binding makes `AdminOperationalIndexHostedService` log Event 6101 and skip every derived index write, including `admin:query-types:tenants`, so `list-tenants` falls back to a nonexistent projection. closure_evidence: - root cause fixed on `main` by commit `fd8ab24da230058f2f239765b68d5e0a135b4b76`, which removed the stale `tenant-a|orders|v1` and `tenant-b|inventory|v1` registrations from `src/Hexalith.EventStore/appsettings.Development.json` (no `orders`/`inventory` remain in the EventStore host `DomainServices:Registrations`; the residual `orders`/`inventory` in `KeycloakRealms/hexalith-realm.json` are unrelated JWT auth attributes); - proved 2026-07-20 on clean source SHA `4f4906b3f30a3d4ed2658effc1c4f189f2f647c0` (contains `fd8ab24d`) in Debug/project-reference mode with only root-declared submodules initialized; - command: `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -p:UseHexalithProjectReferences=true -p:UseSharedCompilation=false --filter "FullyQualifiedName~QueryResponseProvenanceE2ETests"`; - build compiled `Hexalith.Tenants` and the `tenants` AppHost resource from source (project references, not package mode); topology started on a placement freed of the concurrent Tenants Aspire session; - result: `QueryResponseProvenanceE2ETests.LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence` PASSED (Total 1, Passed 1, Failed 0, Skipped 0); the test restarts EventStore with `admin:query-types:tenants` cleared, waits for the `tenants` resource healthy, then asserts `list-tenants` returns HTTP 200 with `HandlerComputed` provenance and no ETag/projection-version/is-stale evidence, and that persisted Redis `admin:query-types:tenants` rebuilt from live metadata contains `list-tenants`; - raw artifacts captured this session as `source-topology-provenance.trx` / `source-topology-provenance.2.log` (ephemeral scratchpad); re-capture durably if `4f4906b3` (or a descendant preserving this behavior) is the selected Story 1.20 runtime. consequence: The stale-registration / `query_projection_missing` blocker no longer prevents crediting the handler-provenance lane at source SHA `4f4906b3`. This closure records prerequisite evidence only; it does NOT authorize Story 1.20 consumer migration, and all Tenants/EventStore/Builds identity changes remain blocked per closure step 4. Per reopen_trigger, any Story 1.20 runtime selection of a different SHA must re-run this exact-source proof against that SHA. closure: 1. Compile the query-provenance E2E with `UseHexalithProjectReferences=true` and only root-declared submodules initialized. 2. Remove or correctly environment-scope stale sample registrations, or otherwise reconcile absent configured bindings without weakening fail-closed handling for genuine metadata failures. 3. Prove `admin:query-types:tenants` contains `list-tenants` and the exact live E2E returns 200 with `HandlerComputed` provenance and no projection evidence. 4. Keep all Tenants/EventStore/Builds identity changes blocked until Story 1.20 separately authorizes migration. reopen_trigger: Any selected runtime or parity packet that credits the handler-provenance lane without a healthy compiled Tenants resource and a positive exact-source result.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore/appsettings.Development.json:15 contains only live counter, greeting, tenants, and global-administrators registrations

### DW-129: The former EventStore-local `Microsoft.Playwright` declaration was removed; effective MSBuild evaluation now resolves it exactly once from Hexalith.Builds, and the import-only wrapper guard rejects future local masks.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-17)"), 2026-08-30
location: Microsoft.Playwright
reason: **RESOLVED 2026-07-31 by Story 3.5.** The former EventStore-local `Microsoft.Playwright` declaration was removed; effective MSBuild evaluation now resolves it exactly once from Hexalith.Builds, and the import-only wrapper guard rejects future local masks.
status: done 2026-07-31
archived: 2026-09-18
resolution: The former EventStore-local `Microsoft.Playwright` declaration was removed; effective MSBuild evaluation now resolves it exactly once from Hexalith.Builds, and the import-only wrapper guard rejects future local masks.

### DW-134: Story 3.5's dependency-mode truth table has no row for build configurations other than Debug/Release (e.g. `Staging`, case variants) with `UseHexalithProjectReferences` unset, and its required test list omits the case — implementers may choose either reference-graph edge with no specified expectation. Owned by Story 3.5's active cycle; route into its review, do not patch from a 1.20 review.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: _bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md
reason: Story 3.5's dependency-mode truth table has no row for build configurations other than Debug/Release (e.g. `Staging`, case variants) with `UseHexalithProjectReferences` unset, and its required test list omits the case — implementers may choose either reference-graph edge with no specified expectation. Owned by Story 3.5's active cycle; route into its review, do not patch from a 1.20 review. [`_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md`]
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs:17 explicitly covers Staging with unset dependency-mode properties

### DW-135: Story 3.5's contract does not define precedence when explicit `UseNuGetDeps` and explicit `UseHexalithProjectReferences` conflict ("preserve its existing mapping" vs "normalize … one authoritative boolean" with no truth-table row, AC, or test naming the winner) — contradictory caller properties could activate both or neither reference edge. Owned by Story 3.5's active cycle.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: _bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md
reason: Story 3.5's contract does not define precedence when explicit `UseNuGetDeps` and explicit `UseHexalithProjectReferences` conflict ("preserve its existing mapping" vs "normalize … one authoritative boolean" with no truth-table row, AC, or test naming the winner) — contradictory caller properties could activate both or neither reference edge. Owned by Story 3.5's active cycle. [`_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs:12-22,45-55 covers conflicting explicit flags, while :125-145 proves UseHexalithProjectReferences wins and exactly one dependency edge remains.
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-135, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-137: Pin `commitlint.config.mjs` to LF in `.gitattributes` or make its exact-content contract line-ending agnostic.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: commitlint.config.mjs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md` summary: Pin `commitlint.config.mjs` to LF in `.gitattributes` or make its exact-content contract line-ending agnostic. evidence: The existing contract compares LF bytes while `.gitattributes` leaves the config under `text=auto`; a Windows or `core.autocrlf=true` checkout can materialize CRLF and fail independently of policy content.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .gitattributes:12 pins commitlint.config.mjs to LF and CommitMessagePolicyTests.cs:477 guards it

### DW-140: RESOLVED 2026-07-20 — the repository excludes `chore` and uses specific non-release types, including `build(deps)` for automated dependency maintenance.

origin: migrated from legacy ledger ("Deferred from: code review of spec-1-20-add-github-approval-login (2026-07-18, loop 4)"), 2026-08-30
location: commitlint.config.mjs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29682903822-fix-ci-cd.md` summary: RESOLVED 2026-07-20 — the repository excludes `chore` and uses specific non-release types, including `build(deps)` for automated dependency maintenance. evidence: `commitlint.config.mjs`, `CONTRIBUTING.md`, project context, and Dependabot prefixes now agree with the shared Git instruction that prohibits `chore`.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: commitlint.config.mjs:7 excludes chore and admits build for dependency maintenance

### DW-148: `SubmitCommandHandler.Log.ResultPayloadDropped`'s message text ("...because final command status was not Completed...") is stale under the flag-driven withholding logic and can be logged even when the reported `FinalStatus` is `Completed`.

origin: migrated from legacy ledger ("Deferred from: code review of spec-gh-29740868410-fix-ci-cd.md (2026-07-20)"), 2026-08-30
location: src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:538
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29740868410-fix-ci-cd.md` summary: `SubmitCommandHandler.Log.ResultPayloadDropped`'s message text ("...because final command status was not Completed...") is stale under the flag-driven withholding logic and can be logged even when the reported `FinalStatus` is `Completed`. evidence: PR #319 (`6945714b`) changed the drop decision from `finalStatus?.Status == CommandStatus.Completed` to `!processingResult.ResultPayloadWithheld` (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:538`) but left the EventId=1107 log message template (line 777) referencing the old status-based reasoning, so the warning can misstate why the payload was withheld. NOTE: superseded 2026-07-20 by the idempotency result-payload gating fix above, which reverted the drop decision back to the durable status-store read -- this item no longer applies to current code but is kept for historical trace.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:543 again gates payload delivery on durable Completed status, matching the log text

### DW-149: [MEDIUM] Fail-closed publisher/validator/authority/smoke suite is not a PR/required check -- `Tools/test-publish-containers.ps1` runs only in Hexalith.Builds `build-release.yml` (push-to-main at reviewed SHA `9ec0a032`; `workflow_dispatch`-only, i.e. worse, at current HEAD); no `pull_request`-triggered Builds workflow runs it.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: Tools/test-publish-containers.ps1
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [MEDIUM] Fail-closed publisher/validator/authority/smoke suite is not a PR/required check -- `Tools/test-publish-containers.ps1` runs only in Hexalith.Builds `build-release.yml` (push-to-main at reviewed SHA `9ec0a032`; `workflow_dispatch`-only, i.e. worse, at current HEAD); no `pull_request`-triggered Builds workflow runs it. evidence: Story 3.12 code review (verification-gap layer) -- a PR to Hexalith.Builds that inverts `_validate_platforms`, drops the expiry check, or loosens owner-role validation merges green because the suite is not in PR CI; the regression surfaces only on a later push/dispatch, after merge. Owned by the Hexalith.Builds maintainer. Persists at live HEAD.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/.github/workflows/ci.yml:12 runs on pull_request and line 153 executes test-publish-containers.ps1

### DW-153: [LOW] The Builds-identity gate is behaviorally tested only for the SHA-mismatch branch; the repository-identity, authority-URL, and owner-allowlist branches in `domain-release.yml` are only substring-asserted, not provoked with negative env permutations.

origin: migrated from legacy ledger ("Deferred from: code review of 3-12-multi-platform-eventstore-container-publishing-correction (2026-07-21)"), 2026-08-30
location: domain-release.yml
reason: source_spec: `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` summary: [LOW] The Builds-identity gate is behaviorally tested only for the SHA-mismatch branch; the repository-identity, authority-URL, and owner-allowlist branches in `domain-release.yml` are only substring-asserted, not provoked with negative env permutations. evidence: Story 3.12 code review (verification-gap layer). Lower priority: the gate is production-proven working (v3.77.2 run 29694935552 step succeeded), so this is defense against a future logic regression in the repo comparison, not a current defect. Owned by the Hexalith.Builds maintainer.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/Github/publish-containers/tests/test_governed_release_workflow.py:1011-1065 behaviorally rejects workflow repository, path, SHA, head, and input identity mutations; references/Hexalith.Builds/.github/workflows/domain-release.yml:265-283 enforces runtime identity.

### DW-164: [LOW] `SampleApiLaunchSettingsTests.ExtractBlock` matches an LF-only marker (`";\n\nif (security is not null)"`) against `src/Hexalith.EventStore.AppHost/Program.cs`, so the test fails on any working tree where that file is checked out or rewritten with CRLF line endings. Make the marker line-ending agnostic (normalize the text, or match on a CRLF-tolerant pattern) the way the sibling `TenantsApiLaunchSettingsTests` does with its `#endif` marker.

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-dedicated-external-tenants-api-host (2026-07-26)"), 2026-08-30
location: ";\n\nif (security is not null)"
reason: source_spec: `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md` summary: [LOW] `SampleApiLaunchSettingsTests.ExtractBlock` matches an LF-only marker (`";\n\nif (security is not null)"`) against `src/Hexalith.EventStore.AppHost/Program.cs`, so the test fails on any working tree where that file is checked out or rewritten with CRLF line endings. Make the marker line-ending agnostic (normalize the text, or match on a CRLF-tolerant pattern) the way the sibling `TenantsApiLaunchSettingsTests` does with its `#endif` marker. evidence: Discovered during Story 2.5 review-loop verification (2026-07-26), NOT caused by that story — it concerns sample-api, not tenants-api. `dotnet test tests/Hexalith.EventStore.AppHost.Tests/...` = 50/51, the single failure being `AppHost_RegistersSampleApiAsExternalServiceInvocationOnlyHost` at `SampleApiLaunchSettingsTests.cs:41` via `ExtractBlock` at `:93` ("Expected sample-api resource registration assignment before the security block"). Root cause confirmed: `file src/Hexalith.EventStore.AppHost/Program.cs` reports CRLF terminators on disk while `git show HEAD:...` is LF, so `git status` reads clean (`* text=auto` normalizes on compare) and `IndexOf` returns -1. A fresh LF checkout (CI) passes; a CRLF working tree fails. The Tenants-specific lane in the same project passes 4/4. Owned by Hexalith.EventStore.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs:71-106 tests both LF and CRLF policy parsing.

### DW-169: [RESOLVED 2026-07-27] Platform prerequisite — preserve `ReadModelFreshnessState` as the independent threshold/age view and introduce the existing `ProjectionLifecycleState` alongside it in consumer UI snapshots/rows, so `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` receive distinct canonical treatment without corrupting freshness semantics.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, D3 owner decision)"), 2026-08-30
location: ReadModelFreshnessState
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [RESOLVED 2026-07-27] Platform prerequisite — preserve `ReadModelFreshnessState` as the independent threshold/age view and introduce the existing `ProjectionLifecycleState` alongside it in consumer UI snapshots/rows, so `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` receive distinct canonical treatment without corrupting freshness semantics. evidence: The 2026-07-27 Story 2.6 owner decision superseded D3 and kept AC3 unchanged. The Tenants working-tree patch threads `ProjectionLifecycleState` through all five gateway/UI surfaces, localizes and renders the four operational states in `TruthStateBadge`, and pins their icon/label/color semantics plus fail-closed lifecycle-action remediation. Focused badge/action tests pass 31/31 and the full UI suite passes 1090/1090. The separate D6 `Aging` reachability and mutation-gate item remains open below; this resolved entry covers only operational lifecycle representation.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants commit 55e6000a implements the lifecycle revalidation recorded by the entry.

### DW-170: [MEDIUM] Active CI operator documentation still names the superseded Hexalith.Builds release SHA and is not guarded against drifting from the immutable release pin.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, D3 owner decision)"), 2026-08-30
location: docs/ci.md:158
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-30204107907-fix-ci-cd.md` summary: [MEDIUM] Active CI operator documentation still names the superseded Hexalith.Builds release SHA and is not guarded against drifting from the immutable release pin. evidence: Verification-gap review found `docs/ci.md:158` and `docs/ci-secrets-checklist.md:54` still name `cf04c419378dfe1bd3c41a9244b5e3283092056e`, while `.github/workflows/release.yml` and the repaired governance test authorize `f75daebd4c522c081a6f62e274cf25e07971de69`. `DocumentationAndContainerDefaultsDescribeTheExactReleaseContract` reads both documents but does not bind either to the approved SHA. Updating those documents is real follow-up work but outside this spec's frozen two-test-file boundary.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:958-959 guards the release SHA in both docs/ci-secrets-checklist.md and docs/ci.md.

### DW-171: [HIGH] `EnrichRowsAsync` publishes member/owner counts as `TenantCountValue.Known(...)` from a detail payload that `LoadTenantDetailAsync` returns raw — no tenant-identity check, no `Members` null guard, no `IsDegraded` check. A detail projection returning the wrong tenant attributes another tenant's member and owner counts to the row; a payload omitting `members` throws `NullReferenceException` that escapes the gateway into the Blazor render.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1009-1022
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [HIGH] `EnrichRowsAsync` publishes member/owner counts as `TenantCountValue.Known(...)` from a detail payload that `LoadTenantDetailAsync` returns raw — no tenant-identity check, no `Members` null guard, no `IsDegraded` check. A detail projection returning the wrong tenant attributes another tenant's member and owner counts to the row; a payload omitting `members` throws `NullReferenceException` that escapes the gateway into the Blazor render. evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter + verification-gap, independently) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1009-1022` and `:1028-1034`]. The sibling search path guards all three cases at `:715-722` (`!string.Equals(detail.TenantId, candidate.TenantId, StringComparison.Ordinal) || detail.Name is null || detail.Members is null`), which is direct evidence the guards are considered necessary. `TenantDetail.Members` is a positional `IReadOnlyList<TenantMember>` with no null validation (`src/Hexalith.Tenants.Contracts/Queries/TenantDetail.cs:13`), so `{"members": null}` deserializes to null. The NRE escape path is confirmed: `EnrichRowsAsync` catches only `EventStoreGatewayException` (`:1018`), `ListByCursorAsync` likewise (`:849`), and `ListTenantsAsync` returns from the non-search path at `:500` outside its `try`. No test feeds a mismatched or null-member detail into the enrichment loop.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:2134-2153 validates identity and usable members.

### DW-172: [HIGH] A single row's detail-enrichment failure with any status outside `{403,404,503}` unwinds past the already-successful list fetch and maps the whole page to `TenantListSurfaceKind.Error`. The most reachable trigger is the client's own `EventStoreGatewayException(200, "Query response did not contain a payload.")`.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1024-1026
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [HIGH] A single row's detail-enrichment failure with any status outside `{403,404,503}` unwinds past the already-successful list fetch and maps the whole page to `TenantListSurfaceKind.Error`. The most reachable trigger is the client's own `EventStoreGatewayException(200, "Query response did not contain a payload.")`. evidence: Story 2.6 second-pass code review (edge-case-hunter + blind-hunter + verification-gap) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1024-1026`]. `EventStoreGatewayClient.SubmitQueryAsync<T>` throws status-200 gateway exceptions at `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:223-247` and `:186-192`, and a malformed 304 raises 502 at `:148-152` — none match the `when` filter. The parallel search path degrades correctly instead, accepting any `EventStoreGatewayException` via `IsHydrationAvailabilityFailure` (`:812`). Related: even within the filter, one routine `404` (tenant deleted between the index and detail projections) forces the whole surface to `Freshness = Unknown`, which `TenantLifecycleAvailabilityInput.Evaluate` (`State/TenantDetail/TenantLifecycleAvailability.cs:42`) treats as blocking for every lifecycle operation. Only `403` is tested (`TenantQueryGatewayTests.cs:1373`).
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:2162-2177 degrades malformed rows independently instead of unwinding the list.

### DW-173: [MEDIUM] Non-`EventStoreGatewayException` failures escape four of the six public read methods. `GetTenantAsync` and `GetConfigurationProjectionProofAsync` have a generic `catch (Exception)`; the user-tenants, global-admins, audit and list paths do not, so an unhandled exception crashes the interactive Blazor circuit instead of degrading.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:283
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] Non-`EventStoreGatewayException` failures escape four of the six public read methods. `GetTenantAsync` and `GetConfigurationProjectionProofAsync` have a generic `catch (Exception)`; the user-tenants, global-admins, audit and list paths do not, so an unhandled exception crashes the interactive Blazor circuit instead of degrading. evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter + verification-gap) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:283`, `:368`, `:391`, `:405`, `:849` versus `:118`+`:134`]. Two concrete reachable triggers: (1) `HttpContent.ReadFromJsonAsync` throws `NotSupportedException` on a non-JSON `Content-Type` — the common shape when an auth redirect or reverse proxy answers `200 text/html` — and `EventStoreGatewayClient.ReadQueryResponseAsync` wraps only `JsonException` (`:455-470`) while `SendTranslatingAsync` translates only `HttpRequestException`/`TaskCanceledException` (`:291-303`); (2) `PaginatedResult<T>` is a positional record with no null validation (`PaginatedResult.cs:6`), so a `{"items": null}` body dereferences null at `:244`, `:338`, `:451`. `GlobalAdministratorsPage.LoadAsync` (`:674-687`) and `MyTenantsPanel.LoadAsync` have no try/catch of their own.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants commit 08918975 hardens direct tenant read gateways.

### DW-174: [MEDIUM] Three further 304 evidence asymmetries beyond the `Degraded` one already on record: a 304 carrying no lifecycle evidence re-affirms the previous claim (so `Current` survives indefinitely where the identical 200 yields `Unknown`); per-row `Freshness` is never rewritten on any 304 branch, so row badges contradict the surface banner; and a `Degraded` surface cannot be cleared by refreshing because the server ETag is content-derived and does not change when only lifecycle recovers.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass)"), 2026-08-30
location: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163-1173
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] Three further 304 evidence asymmetries beyond the `Degraded` one already on record: a 304 carrying no lifecycle evidence re-affirms the previous claim (so `Current` survives indefinitely where the identical 200 yields `Unknown`); per-row `Freshness` is never rewritten on any 304 branch, so row badges contradict the surface banner; and a `Degraded` surface cannot be cleared by refreshing because the server ETag is content-derived and does not change when only lifecycle recovers. evidence: Story 2.6 second-pass code review (edge-case-hunter + blind-hunter) [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163-1173`, the four 304 branches at `:232-244`, `:313-325`, `:429-435`, `:872-886`, and `:1270-1272`]. Confirmed pre-existing: the pre-2.6 predicate `metadata?.IsDegraded == true || metadata?.IsStale is not null ? ResolveFreshness(metadata) : previous` produced the same carry-forward and the same 200/304 divergence; Story 2.6 widened the trigger set with the lifecycle clause but did not introduce the shape. The 200 paths rewrite rows explicitly (`:262`, `:353`, `:467`) while the 304 branches use `previous with { ... }`, which copies `Rows` by reference; both `TenantDataGrid.razor:76` and `GlobalAdministratorsPage.razor:346` bind the per-row value. Server ETag is per-projection, not per-response-state (`QueriesController.cs:113-115`). status: RESOLVED 2026-07-30 by the Story 2.6 fourth-pass review. Retained rows were already rewritten in the 2026-07-27 patch; all retained-snapshot lifecycle resolution now consumes only explicit, valid `304` lifecycle evidence and otherwise fails closed to `Unknown`, so neither `Current` nor `Degraded` is sticky. A gateway regression proves a projection-backed lifecycle-less `304` cannot inherit the previous value.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:2002-2013 rewrites list rows through the lifecycle resolver.

### DW-179: [HIGH] Mutation-gate fail-open in the ratified 2.11 consumer logic — the tenant correction surface enables a mutation that `ProjectionLifecyclePolicy.CanMutate` denies. `CanMutate` requires `provenance == ProjectionBacked && lifecycle == Current`, but `ResolveFreshness` also returns `Current` on the legacy fall-through (`Lifecycle == Unknown` with `IsStale == false`). `TenantCorrectionStartIntent` gates only on `Freshness is Current`, so a producer emitting no lifecycle header but legacy `IsStale: false` unlocks a correction the platform policy forbids.

origin: migrated from legacy ledger ("Deferred from: code review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, second pass — routed to Story 2.11)"), 2026-08-30
location: src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs:83
reason: source_spec: `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` summary: [HIGH] Mutation-gate fail-open in the ratified 2.11 consumer logic — the tenant correction surface enables a mutation that `ProjectionLifecyclePolicy.CanMutate` denies. `CanMutate` requires `provenance == ProjectionBacked && lifecycle == Current`, but `ResolveFreshness` also returns `Current` on the legacy fall-through (`Lifecycle == Unknown` with `IsStale == false`). `TenantCorrectionStartIntent` gates only on `Freshness is Current`, so a producer emitting no lifecycle header but legacy `IsStale: false` unlocks a correction the platform policy forbids. evidence: Story 2.6 second-pass code review (blind-hunter + edge-case-hunter), mechanism verified against source 2026-07-26. `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs:83` (`CanMutate` = `isAuthorized && IsProjectionConfirmed(provenance, lifecycle)`, and `IsProjectionConfirmed` requires `lifecycle == Current`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1156-1160` (legacy `IsStale switch { false => Current }`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs:88`. Reachable through the shipped producer: `QueriesController.cs:136-139` omits the lifecycle header entirely when `Lifecycle == Unknown`. NOT fixable as a mechanical patch — `TenantAuditRow` carries neither `Provenance` nor `Lifecycle`, so the intent cannot call `CanMutate`; closing it needs a design choice (widen the row, add a gateway-computed `CanMutate` flag, or stop deriving `Current` from legacy evidence). Story 2.6's ratified-overlap section assigns this defect to Story 2.11 and requires resolution before 2.11 leaves `review`. Owned by Story 2.11 / the Hexalith.Tenants maintainer. status: RESOLVED 2026-07-27 by Story 2.11, Tenants `5eed7a97b87988e2f1e286a0483490ca7ef75d2b`, contained by the maintainer-authored and published merge `d2e5a1211f469041fdc593fd4e4678755f6863c8`; the EventStore gitlink pinned that merge at acceptance. The "not fixable as a mechanical patch" note was superseded by Tenants `55e6000`, which added `Lifecycle` to `TenantAuditRow`; the chosen option was widening the row. `TenantAuditRow` now also carries `Provenance` (failing closed to `Unknown` for absent or out-of-range values), set on the 200, `304`, and missing-payload audit paths, and `TenantCorrectionStartIntent.Evaluate:88` requires `ProjectionLifecyclePolicy.IsProjectionConfirmed(Provenance, Lifecycle)` in addition to `Freshness is Current`. The exhaustive one-way invariant proves that an available intent always satisfies `CanMutate`; correction-specific checks may still deny a platform-eligible mutation. Evidence: fail-closed cases for the legacy `IsStale == false` fall-through, missing/`HandlerComputed`/`Unknown`/invalid provenance, all five non-`Current` lifecycles, complete 200/304 row transport, missing-payload reset, and an untrusted-evidence component regression. Review-patch UI evidence is 292/292 focused and 1226/1226 full-suite, with Release UI/integration builds clean and the Tier-3 persisted consumer path 1/1. The broader mutation-gate follow-up was resolved on 2026-07-30 by the owner-approved Story 2.6 scope expansion.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants commit 5eed7a97b87988e2f1e286a0483490ca7ef75d2b implements the correction-start intent guard.

### DW-181: [MEDIUM] Four legacy Fluent v4 / FAST tokens survive in three Tenants UI stylesheets, so the "accent" callout treatment never tracks the active theme. `hexalith-ux-instructions.md` forbids `--accent-*` and `--neutral-foreground-*` outright — they belong to the previous major version and do not resolve under Fluent V5, so every occurrence falls through to its system-colour fallback and renders `LinkText` / `GrayText` in every theme with the intended accent silently absent. The UX instruction's own escape hatch requires these files to be tracked as an explicit, allowlisted migration backlog rather than silently exempted; this entry is that tracking. Migrate each to a Fluent 2 design token (or a Fluent primitive) and keep the `@media (forced-colors: active)` fallbacks.

origin: migrated from legacy ledger ("Deferred from: focused UX acceptance review of 2-6-tenants-ui-client-library-alignment-and-ux-evidence (2026-07-26, Sally)"), 2026-08-30
location: hexalith-ux-instructions.md
reason: source_spec: `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md` summary: [MEDIUM] Four legacy Fluent v4 / FAST tokens survive in three Tenants UI stylesheets, so the "accent" callout treatment never tracks the active theme. `hexalith-ux-instructions.md` forbids `--accent-*` and `--neutral-foreground-*` outright — they belong to the previous major version and do not resolve under Fluent V5, so every occurrence falls through to its system-colour fallback and renders `LinkText` / `GrayText` in every theme with the intended accent silently absent. The UX instruction's own escape hatch requires these files to be tracked as an explicit, allowlisted migration backlog rather than silently exempted; this entry is that tracking. Migrate each to a Fluent 2 design token (or a Fluent primitive) and keep the `@media (forced-colors: active)` fallbacks. evidence: Story 2.6 focused UX acceptance review (Sally), verified against source at Tenants `11d6992`, 2026-07-26. Occurrences: `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Pages/TenantAuditPage.razor.css:20` (`var(--accent-stroke-rest, LinkText)`) and `:27` (`var(--neutral-foreground-hint, GrayText)`); `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Members/RemoveTenantMemberFlow.razor.css:22` and `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Configuration/SetTenantConfigurationFlow.razor.css:49` (both `var(--accent-fill-rest, LinkText)`). All four carry an `fc-css-exception` marker, but each marker justifies only the **layout** (border-inline-start + padding Fluent has no primitive for) — none declares or justifies the **token** choice, so `DomainUiFluentConformanceTests` passes them while the theme-tracking rule stays broken. NOT attributable to Story 2.6: neither the published `11d6992` change nor the 2026-07-27 lifecycle-presentation patch touches these three stylesheets; the lifecycle-state styles remain clean. Suggested durable fix: extend the conformance guard to reject legacy v4/FAST token names outright, so the marker cannot mask them. Owned by the Hexalith.Tenants maintainer.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Tenants commits 2e19cc8e0b99d232a38fcaf0e7e6d967fd7f642f, bb85fadb149fed1fa00dfd9c8d3315df541566e8, and 281e3c3c3d2ce13a5282383216c104914a724390 replaced the residual legacy Fluent tokens; a current stylesheet scan has zero --accent-* or --neutral-foreground-* hits.

### DW-182: [HIGH] Member, configuration, metadata, tenant-lifecycle, and global-administrator projection gates still accept `Freshness == Current` without requiring projection-confirmed lifecycle/provenance, so legacy-current/unknown-lifecycle responses can arm mutations outside the correction-start surface.

origin: migrated from legacy ledger ("Deferred from: code review of 2-11-query-provenance-consumption-in-generated-rest-and-tenants (2026-07-27)"), 2026-08-30
location: MemberAccessReview.razor:391
reason: source_spec: `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` summary: [HIGH] Member, configuration, metadata, tenant-lifecycle, and global-administrator projection gates still accept `Freshness == Current` without requiring projection-confirmed lifecycle/provenance, so legacy-current/unknown-lifecycle responses can arm mutations outside the correction-start surface. evidence: Story 2.11 code review (verification-gap), confirmed against `MemberAccessReview.razor:391`, `EditTenantMetadataFlow.razor:233`, `TenantLifecycleCommandFlow.razor:200`, and `GlobalAdministratorCorrectionSnapshot.cs:293,369-371`. Pre-existing and explicitly outside the owner-confirmed correction-start scope; the story records the affected surfaces and says a separate scope decision is required. status: RESOLVED 2026-07-30 by the owner-approved Story 2.6 scope expansion. Existing member and global-administrator lifecycle gates remain fail-closed; configuration set/remove and metadata edit now consume lifecycle directly; lifecycle availability rejects `Unknown` and every other non-`Current` value and re-evaluates open flows. The full Tenants UI suite passes 1583/1583.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/RemoveTenantConfigurationFlowTests.cs:284-290 rejects non-current lifecycle removal.

### DW-195: [MEDIUM] EventStore and FrontComposer enforce opposite invariants on the release pin. EventStore asserts the pinned Builds release SHA must differ from its `references/Hexalith.Builds` gitlink; FrontComposer asserts they must be equal. Applying the Story 3.1 fix pattern to EventStore would break EventStore's guard.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: references/Hexalith.Builds
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM] EventStore and FrontComposer enforce opposite invariants on the release pin. EventStore asserts the pinned Builds release SHA must **differ** from its `references/Hexalith.Builds` gitlink; FrontComposer asserts they must be **equal**. Applying the Story 3.1 fix pattern to EventStore would break EventStore's guard. evidence: Story 3.1 closure code review (edge-case-hunter), verified 2026-07-28 against `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:310` (`gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha)`) versus `references/Hexalith.FrontComposer/tests/.../CiGovernanceTests.cs:474-482` (three-way equality). Both are defensible in isolation — EventStore decouples release authority from the dev-time gitlink, FrontComposer locks them together — but no workspace-level document states which rule applies where. Suggested durable fix: document one workspace-wide rule in Hexalith.Builds and have each guard cite it.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:634-647 now permits the release pin to be equal to or an ancestor of the validated Builds source.

### DW-196: [LOW, FrontComposer-owned] Asymmetric supply-chain pinning: `release.yml` pins Builds to an exact SHA under the REL-6 identity rationale, while `ci.yml` and `quality.yml` both consume Builds at `@main` — the lanes that authorize the release run unpinned Builds code.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: release.yml
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [LOW, FrontComposer-owned] Asymmetric supply-chain pinning: `release.yml` pins Builds to an exact SHA under the REL-6 identity rationale, while `ci.yml` and `quality.yml` both consume Builds at `@main` — the lanes that authorize the release run unpinned Builds code. evidence: Story 3.1 closure code review (verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/.github/workflows/release.yml:89`, `ci.yml:25`, `quality.yml:40`. Related: the release pin currently trails Builds `main` by a large margin, so the release path executes Builds logic no other lane validated. Owned by the Hexalith.FrontComposer maintainer. Suggested durable fix: assert a bounded `git rev-list --count <pin>..origin/main` distance, or pin all three lanes.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs:1161-1217 requires exact release and CI Builds SHA pins and proves they are identical.

### DW-198: [MEDIUM, FrontComposer-owned] Analyzer-ledger hardening bundle — the CA1707 fail-closed gate compares only an aggregate count and one hash, so re-attestation is indistinguishable from laundering a violation.

origin: migrated from legacy ledger ("Deferred from: code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: references/Hexalith.FrontComposer/tests/.../AnalyzerPolicyGovernanceTests.cs:552-597,793-808
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [MEDIUM, FrontComposer-owned] Analyzer-ledger hardening bundle — the CA1707 fail-closed gate compares only an aggregate count and one hash, so re-attestation is indistinguishable from laundering a violation. evidence: Story 3.1 closure code review (edge-case-hunter + verification-gap), verified 2026-07-28 against `references/Hexalith.FrontComposer/tests/.../AnalyzerPolicyGovernanceTests.cs:552-597,793-808` and `_bmad-output/contracts/analyzer-policy-exception-ledger-v1.json:26,37,74-75,105`. Five distinct gaps: (1) the failure message prints only the new count and hash, never the added/removed `path:line:token` set, so offsetting additions and removals are invisible; (2) the inventory is computed from `git ls-files --cached` plus working-tree bytes, so an attested hash can encode uncommitted content and untracked test files go uncounted; (3) a new test project under `tests/` silently expands the CA1707 exemption with no `testProjectRoots` assertion; (4) this re-attestation refreshed only `identifierInventory`, leaving the census `sourceCommit` and `implementationCount` stale so the ledger self-contradicts; (5) the linked `naming-ca1707-test-convention` disposition names "test source inventory drifts" as its revalidation trigger, but its `decisionDate`/evidence were not bumped, so an owner revalidation obligation was discharged by a hash paste. Owned by the Hexalith.FrontComposer maintainer (Story 11.19+). Suggested durable fix, smallest first: emit the token delta in the failure message; assert every added token is a method-declaration identifier on a `[Fact]`/`[Theory]` member (the ledger already carries the Roslyn machinery); compute the inventory from `git show HEAD:<path>` and fail on a dirty tree.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.FrontComposer commit d0dbc2ce closes analyzer-evidence fail-opens and anchors the inventory.

### DW-200: [LOW] The `docs/ci.md` publication-pin literal is unguarded, so the exact drift the Story 3.1 closure fixed (`cf04c419…` → `f75daebd…`) will recur silently at the next Builds pin advance.

origin: migrated from legacy ledger ("Deferred from: post-merge code review of 3-1-re-tier-live-sidecar-tests-from-release-gate (2026-07-28)"), 2026-08-30
location: docs/ci.md
reason: source_spec: `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md` summary: [LOW] The `docs/ci.md` publication-pin literal is unguarded, so the exact drift the Story 3.1 closure fixed (`cf04c419…` → `f75daebd…`) will recur silently at the next Builds pin advance. evidence: Story 3.1 post-merge code review (edge-case-hunter + verification-gap), verified 2026-07-28. No test references `docs/ci.md` (`grep -rn 'docs/ci.md' tests/ --include='*.cs'` returns nothing); the only enforced copy of the pin is `ApprovedBuildsReleaseSha` at `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:13`, asserted against `.github/workflows/release.yml` only. The document is cited by Story 3.1 as a `source_files` release-flow authority and already went stale once this way. Suggested durable fix: extend `ContainerPublishingGovernanceTests` to assert the 40-hex pin quoted in `docs/ci.md` equals `ApprovedBuildsReleaseSha`, so the doc participates in the same lockstep the workflow already has.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:958-959 now checks the docs/ci.md publication pin.

### DW-210: [MEDIUM] No CI lane ever executes the source-mode (`HEXALITH_TENANTS_SOURCE`) AppHost topology, so the Tenants security dependents that `AspireSecurityResourceNamingTests` conditionally asserts are verified nowhere.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.4 (2026-07-30)"), 2026-08-30
location: github/workflows/ci.yml:34
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` summary: [MEDIUM] No CI lane ever executes the source-mode (`HEXALITH_TENANTS_SOURCE`) AppHost topology, so the Tenants security dependents that `AspireSecurityResourceNamingTests` conditionally asserts are verified nowhere. evidence: Story 3.4 review pass 4 (verification-gap), verified 2026-07-30. `.github/workflows/ci.yml:34` runs `tests/Hexalith.EventStore.AppHost.Tests` through the Builds reusable workflow with no `UseHexalithProjectReferences` override, so `Directory.Build.props:51` defaults it to `false` and neither `tenants` nor `tenants-api` exists in the built model. The single source-mode job (`.github/workflows/ci.yml:99-105`) runs `dotnet test --filter FullyQualifiedName~TenantsApiLaunchSettingsTests`, which excludes the naming class entirely. The pre-existing condition is that narrow source-mode filter, not the new test; its `if (builder.Resources.Any(... "tenants" ...))` guard at `AspireSecurityResourceNamingTests.cs:80-88` silently shrinks the expected dependent set instead of failing. Demonstration: delete `_ = tenants.WithJwtBearerSecurity(security);` from `src/Hexalith.EventStore.AppHost/Program.cs:159` and both CI jobs stay green. Suggested durable fix, smallest first: extend the source-mode job's `--filter` to include `AspireSecurityResourceNamingTests`; longer term, give the source-mode lane a real suite rather than one filtered class.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: .github/workflows/ci.yml:130-175 defines a blocking tenants-source-mode job that restores, builds, and tests the AppHost with UseHexalithProjectReferences=true.

### DW-220: [LOW] Add the intentionally preserved `awaiting-operator` value to the sprint-status schema comments. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:19,205`. Pre-existing schema-comment drift outside Story 8.1.

origin: migrated from legacy ledger ("Deferred from: code review of 8-1-shared-payload-protection-security-spec-and-adr (2026-08-01)"), 2026-08-30
location: _bmad-output/implementation-artifacts/sprint-status.yaml:19,205
severity: low
reason: [LOW] Add the intentionally preserved `awaiting-operator` value to the sprint-status schema comments. Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:19,205`. Pre-existing schema-comment drift outside Story 8.1.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 8d6f7dacd2ed3ccbf3c1ffd2a18e7477f5eb2c0b removed the sole awaiting-operator value by moving Story 4.6 to done, so the requested schema-comment addition is moot.

### DW-222: [LOW] `review_loop_iteration: 1` was not incremented despite two documented hardening passes recorded in the same file's Spec Change Log.

origin: migrated from legacy ledger ("Deferred from: code review of story-3.13 (2026-08-04)"), 2026-08-30
location: spec-3-13-deployed-runtime-parity-closure.md:7
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: [LOW] `review_loop_iteration: 1` was not incremented despite two documented hardening passes recorded in the same file's Spec Change Log. evidence: `spec-3-13-deployed-runtime-parity-closure.md:7` frontmatter still reads `review_loop_iteration: 1`, while the Spec Change Log records "Applied all 15 code-review patches" and, separately, "Applied the second review-hardening pass ... 115 focused mutation cases," both dated 2026-08-04. Cosmetic drift, not blocking.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:562 records the human-authorized issue-attempt reset rationale.

### DW-241: [MEDIUM] The two `DeadLetterMessage` producers now disagree about the dead-letter contract, and the suite that encodes it only exercises one of them.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:148
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] The two `DeadLetterMessage` producers now disagree about the dead-letter contract, and the suite that encodes it only exercises one of them. evidence: `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:148` encodes "Dead-letter should contain full command envelope for replay" as an invariant of the contract. Story 4.4's `DeadLetterMessage.FromDrainExhaustion` deliberately emits a reduced, non-replayable envelope, but that suite exercises only `FromException`, so the two producers contradict each other with nothing reconciling them. Either scope the invariant to replay-eligible producers or assert the reduced shape explicitly.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterMessageTests.cs:194-244 now exercises both FromException and FromDrainExhaustion producer contracts.

### DW-243: [LOW] Log EventId allocation across the partial `Log` classes is unguarded, so a duplicate EventId can be introduced silently.

origin: migrated from legacy ledger ("Deferred from: code review of 4-4-committed-event-publication-recovery (2026-08-07)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [LOW] Log EventId allocation across the partial `Log` classes is unguarded, so a duplicate EventId can be introduced silently. evidence: Story 4.4 adds EventIds 2010-2019 in `AggregateActor` (with `2019` declared out of order between `2015` and `2016`) and `5006` in `IdempotencyChecker`, but `AggregateActor` is a partial class whose `Log` block is one of several and nothing in the repo asserts uniqueness of EventIds within a category. A cheap reflection test over the `LoggerMessage` attributes would pin the ranges.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionLogEventIdUniquenessTests.cs, added by commit 64a21632, guards log EventId uniqueness.

### DW-245: `package-availability.json` embeds machine-local absolute search roots under `/home/administrator/...`, which are non-portable durable evidence.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: package-availability.json
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `package-availability.json` embeds machine-local absolute search roots under `/home/administrator/...`, which are non-portable durable evidence. evidence: Blind-hunter review of the Story 3.13 packet; fail-closed package recovery already records 404/unavailable, but retained search roots remain host-specific.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:332 records that durable search sources are named without reintroducing absolute host paths.

### DW-246: Epic 3 context rewrite thins earlier concrete cross-story constraints without an explicit supersession note.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: epic-3-context.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Epic 3 context rewrite thins earlier concrete cross-story constraints without an explicit supersession note. evidence: Review of `epic-3-context.md` in the baseline..HEAD scoped diff; historical live-sidecar/DaprETag specificity was reduced while adding 3.12/3.13 guidance.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:360 records the owner's explicit ratification of the Epic 3 context compression and why no context rewrite is required.

### DW-247: Expected AC4 acceptance scaffolding (`acceptances/{subject_sha256}` layout / receipt schema example) is narrative-only and not checked into hashed manifests.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: acceptances/{subject_sha256
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Expected AC4 acceptance scaffolding (`acceptances/{subject_sha256}` layout / receipt schema example) is narrative-only and not checked into hashed manifests. evidence: Blind-hunter review; 0/3 acceptances are intentional while fail-closed, but reopen tooling has no committed empty convention.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:820-825 records and validates all three content-bound acceptance receipts.

### DW-248: Support-safety hostname privacy only special-cases `.internal`/`.local`, so other private DNS names can bypass the literal-IP private check.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Support-safety hostname privacy only special-cases `.internal`/`.local`, so other private DNS names can bypass the literal-IP private check. evidence: Edge-case hunter on `HostLooksPrivate` / `AddressIsPrivate` in `DeployedRuntimeParityClosureTests.cs`.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:8638-8658 rejects .corp/.lan and every absolute-URI host outside an explicit public allowlist.

### DW-249: Retained `smoke-results.json` can declare top-level `"result": "pass"` while runtime-verification/crosswalk mark execution unverified/fail.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: smoke-results.json
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Retained `smoke-results.json` can declare top-level `"result": "pass"` while runtime-verification/crosswalk mark execution unverified/fail. evidence: Blind-hunter review; product gate is already fail-closed via runtime-verification, but the smoke summary over-claims relative to that gate. Fixing requires hash-bound evidence edits beyond this patch pass.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/runtime-verification.json:2-5 consistently records v2/pass execution.

### DW-250: Crosswalk `approval_contract.required_receipt_fields` omits `schema` while the verifier’s `RequiredReceiptFields` requires it.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: approval_contract.required_receipt_fields
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Crosswalk `approval_contract.required_receipt_fields` omits `schema` while the verifier’s `RequiredReceiptFields` requires it. evidence: Blind-hunter review; correcting the crosswalk would rehash core evidence and was deferred to avoid churn while 0/3 acceptances remain.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/identity-crosswalk.json:555-563 includes schema in required_receipt_fields.

### DW-251: Review-subject blocker text still claims smoke logs lack cleanup facts after cleanup=pass appears in retained logs/runtime-verification.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Review-subject blocker text still claims smoke logs lack cleanup facts after cleanup=pass appears in retained logs/runtime-verification. evidence: Blind-hunter review; blocker wording is hash-bound and should be narrowed only when evidence is intentionally republished.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/review-subject.json:34-37 lists current blockers without the obsolete missing-cleanup claim.

### DW-252: Governed inline CI checkouts (`semantic-release-governance`, `tenants-source-mode`) set `persist-credentials: false`, but Contracts helpers never assert it and valid fixtures omit it.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md` summary: Governed inline CI checkouts (`semantic-release-governance`, `tenants-source-mode`) set `persist-credentials: false`, but Contracts helpers never assert it and valid fixtures omit it. evidence: Verification-gap/edge-case review — deleting those lines leaves Shared_ci / Semantic_release_governance / mutation tests green; out of scope for the mixed-job false-positive fix.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:1704 and :1768 assert persist-credentials: false in both governed checkout fixtures.

### DW-254: Release `verify-source` job body and fail-closed release inputs (`expected-package-count`, `timeout-minutes`) lack Contracts assertions comparable to the CI job-scoped guards.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-29567058321-fix-ci-cd.md` summary: Release `verify-source` job body and fail-closed release inputs (`expected-package-count`, `timeout-minutes`) lack Contracts assertions comparable to the CI job-scoped guards. evidence: Edge-case review of post-baseline release topology changes — not caused by this mixed-job guardrail story.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:597-662 guards the verify-source body, timeout, and source verification contract.

### DW-256: Redact absolute local_search_roots from retained package-availability.json (and refresh checksums/bindings) so support-safe evidence does not embed host filesystem paths.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: package-availability.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Redact absolute local_search_roots from retained package-availability.json (and refresh checksums/bindings) so support-safe evidence does not embed host filesystem paths. evidence: Blind-hunter review of Story 3.13; package-availability.json lists /home/administrator/... roots while JsonEvidenceIsSupportSafe currently accepts them.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:332 records the support-safe redaction of absolute package search roots.

### DW-262: Dev Agent Debug Log still cites stale focused/suite test totals that no longer match the 140-test verifier count.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Dev Agent Debug Log still cites stale focused/suite test totals that no longer match the 140-test verifier count. evidence: Blind-hunter review of the Story 3.13 record found 115/117 and suite 999/1001 figures while the proof packet and latest hardening pass report 140 focused tests; documentation-only drift, not a verifier defect.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:775-777 records refreshed focused 240/240 and complete 1494/1494 totals.

### DW-263: Several Task parent checkboxes remain unchecked while child boxes and later tasks are marked complete.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Several Task parent checkboxes remain unchecked while child boxes and later tasks are marked complete. evidence: Blind-hunter review showed Tasks 4–7 and 9 parents unchecked despite checked children; AC2/AC4 intentionally remain open, so this is progress-tracking hygiene rather than a functional gap.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:197-284 marks the task parents complete consistently.

### DW-264: Story 4.5 LiveSidecar ownership prose was added in docs/ci.md within the same baseline range as Story 3.13's ownership rewrite.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: docs/ci.md
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.5 LiveSidecar ownership prose was added in docs/ci.md within the same baseline range as Story 3.13's ownership rewrite. evidence: Spec Code Map allows only the Story 3.12-to-1.20 ownership paragraph change in docs/ci.md; the Story 4.5 paragraph is concurrent scope leakage outside 3.13's single-goal delivery and should be owned by Story 4.5 tracking.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:362 records the owner's reassignment of the co-landed Story 4.5 docs change to its owning story.

### DW-265: Add an `acceptances/{subject_sha256}/` scaffold or receipt template beside the roster before AC4 collection.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: acceptances/{subject_sha256}/
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Add an `acceptances/{subject_sha256}/` scaffold or receipt template beside the roster before AC4 collection. evidence: Blind-hunter review on 2026-08-09; AC4 still requires three content-bound receipts and 0/3 remain missing, but fail-closed review does not need the scaffold to stay non-done.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:820-825 records the completed acceptance layout and three validated receipts.

### DW-266: Re-measure the full Contracts.Tests suite after the ninth hardening pass and refresh Dev Agent / proof-packet totals if they drift.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Re-measure the full Contracts.Tests suite after the ninth hardening pass and refresh Dev Agent / proof-packet totals if they drift. evidence: Blind-hunter review on 2026-08-09; last recorded full-suite measurement was 1001 on 2026-08-05 while focused coverage continued to grow.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:775-777 contains the post-hardening full-suite remeasurement.

### DW-267: Reviewer roster maps both eventstore-owner and release-owner to the same github:jpiquot identity.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Reviewer roster maps both eventstore-owner and release-owner to the same github:jpiquot identity. evidence: AC4 asks for distinct EventStore-owner and Release-owner acceptances, but the hash-bound roster and verifier currently authorize the same identity for both roles; separation of duties is not enforced and was not renegotiated in frozen intent.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:331 records the owner's explicit ratification of github:jpiquot holding both eventstore-owner and release-owner roles.

### DW-268: Same working tree advances Epic 4 tracker rows and Story 4.5 LiveSidecar docs/ci prose beside Story 3.13.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: docs/ci
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Same working tree advances Epic 4 tracker rows and Story 4.5 LiveSidecar docs/ci prose beside Story 3.13. evidence: Story 3.13 Code Map limits docs/ci.md edits to the deployed-closure ownership paragraph and forbids scope leakage, yet the baseline diff also includes Epic 4 status moves and LiveSidecar prose outside that ownership scope.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:362 records the owner's reviewed decision not to rewrite published co-landed history and assigns each material change to its owning story.

### DW-269: Retained fail-closed runtime-verification.json remains schema v1 without pass-path v2 command/smoke_results shape.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: runtime-verification.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Retained fail-closed runtime-verification.json remains schema v1 without pass-path v2 command/smoke_results shape. evidence: ValidateRuntimeExecution now requires hexalith.eventstore.story-3-13-runtime-verification/v2 with command and smoke_results, while the live fail-closed citation is still v1; reopen owners lack a documented migration to a closable pass packet.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/runtime-verification.json:2-11 implements the v2 pass-path shape.

### DW-271: Story 4.4 activation recovery can permanently starve publication-index entries beyond the fixed head scan and work budgets.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 4.4 activation recovery can permanently starve publication-index entries beyond the fixed head scan and work budgets. evidence: `RearmOutstandingPublicationsAsync` always restarts at the first entry, persists no cursor, and schedules no continuation while a continuously active actor may never receive another activation.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 4b0a7b1d3628a857f131cfbff99030714aefc747 changed AggregateActor activation recovery so already-armed head entries do not consume the probe/work budgets and later entries receive recovery opportunity.

### DW-278: Persisted duplicate publication-index entries survive normalization and can leave stale capacity behind.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Persisted duplicate publication-index entries survive normalization and can leave stale capacity behind. evidence: `Normalize` removes only null elements, while refresh and removal operate on the first matching message ID despite the type contract promising de-duplication.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 659f3c72a653525f028b0a2212fb568e82bb50cb added owner-map normalization and duplicate suppression; src/Hexalith.EventStore.Server/Actors/UnpublishedPublicationIndex.cs:117-171 now emits at most one well-formed owner per MessageId.

### DW-281: BMAD project-context sync can write `AGENTS.md` outside the selected project through a compass area path.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: AGENTS.md
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: BMAD project-context sync can write `AGENTS.md` outside the selected project through a compass area path. evidence: `cmd_sync` joins unvalidated absolute or parent-traversing `area` values to `project_root` without resolving and enforcing containment.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 2b9502b6c0781f3a9b4fbb0db0363deca334b608 removed .agents/skills/bmad-project-context/scripts/context.py, including the vulnerable compass-area filesystem writer.

### DW-282: BMAD project-context sync can duplicate or remove user-authored text when managed markers are missing, reversed, or duplicated.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: BMAD project-context sync can duplicate or remove user-authored text when managed markers are missing, reversed, or duplicated. evidence: `apply_block` validates neither marker cardinality nor ordering before slicing or appending the managed block.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 2b9502b6c0781f3a9b4fbb0db0363deca334b608 removed the marker-rewriting project-context implementation; the installed skill now contains no apply_block/cmd_sync code.

### DW-283: BMAD project-context sync does not re-anchor relative Markdown links that include fragments or query strings.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: decision.md
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: BMAD project-context sync does not re-anchor relative Markdown links that include fragments or query strings. evidence: `rewrite_links` only processes targets that literally end in `.md`, so links such as `decision.md#rationale` break when moved to another directory.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 2b9502b6c0781f3a9b4fbb0db0363deca334b608 removed the project-context rewrite_links implementation and its executable script.

### DW-284: The installed BMAD project-context implementation lacks regression coverage for its filesystem-writing and resolution paths.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The installed BMAD project-context implementation lacks regression coverage for its filesystem-writing and resolution paths. evidence: The local suite contains three smoke tests and conditionally skips the referenced full Layer-1 suite, leaving resolve, sweep, compass, sync, remote, and cache behavior unverified.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 2b9502b6c0781f3a9b4fbb0db0363deca334b608 removed the implementation whose missing filesystem regression suite this entry targeted.

### DW-292: The Story 4.5 evidence validator can report success when Python assertions are disabled.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: validate-evidence.py
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The Story 4.5 evidence validator can report success when Python assertions are disabled. evidence: Validation is implemented with `assert` statements and has no `__debug__` guard, so `python -O validate-evidence.py` removes the checks.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py:13 fails closed when Python assertions are disabled.

### DW-293: The Story 4.5 evidence validator accepts truthy non-boolean invariant values.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The Story 4.5 evidence validator accepts truthy non-boolean invariant values. evidence: `assert all(race["invariants"].values())` accepts strings such as `"false"` instead of requiring every value to be exactly `True`.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py:530-532 requires every invariant value to be exactly true.

### DW-294: The Story 4.5 source-binding validator does not fail when an evidence-relevant source path is omitted.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: source-state.md
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The Story 4.5 source-binding validator does not fail when an evidence-relevant source path is omitted. evidence: `validate_source_binding` verifies only the rows present in `source-state.md` and has no exact expected-path set.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py:685-696 enforces REQUIRED_SOURCE_ROWS before validating hashes.

### DW-296: Builds `dapr-init` still uses one shared version for CLI install and runtime init, so EventStore cannot pin CLI 1.18.0 with runtime 1.18.1 without a submodule change.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-gh-31400593510-fix-ci-cd.md` summary: Builds `dapr-init` still uses one shared version for CLI install and runtime init, so EventStore cannot pin CLI 1.18.0 with runtime 1.18.1 without a submodule change. evidence: Integration failure 31413307050 and Ask First in the approved spec; restoring shared 1.18.0 unblocks CI but leaves CLI/runtime decoupling as a Builds enhancement. status: resolved 2026-08-11 — `runtime-version` now independently selects the Dapr runtime while omitted callers retain `version` as the legacy fallback.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: references/Hexalith.Builds/Github/dapr-init/action.yml:9-38 defines runtime-version independently with a legacy version fallback.

### DW-297: The concurrent integration workflow's Dapr 1.18.0 runtime pin cannot reproduce the OQ8 packet's validator-pinned Dapr 1.18.1 fresh capture.

origin: migrated from legacy ledger ("Deferred from: spec-gh-29567058321-fix-ci-cd review (2026-08-08)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: The concurrent integration workflow's Dapr 1.18.0 runtime pin cannot reproduce the OQ8 packet's validator-pinned Dapr 1.18.1 fresh capture. evidence: The live fixture records the actual `daprd --version`, while the OQ8 validator requires 1.18.1 and the current workflow passes the shared 1.18.0 pin to `dapr-init`; resolving it requires the separately owned Builds CLI/runtime decoupling change. status: resolved 2026-08-11 — Integration now passes runtime 1.18.2 independently from CLI 1.18.0, fresh validation requires that explicit runtime, and immutable Story 4.14 remains pinned to observed runtime 1.18.1.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .github/workflows/integration.yml:25 and :68 pin and pass DAPR_RUNTIME_VERSION independently from the CLI version.

### DW-304: Write sanitized fresh-capture outputs atomically.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Write sanitized fresh-capture outputs atomically. evidence: A process interruption can currently leave a partial test, support, or validation document in the capture directory.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tools/validate-oq8-platform-evidence.py:737-743 writes JSON through a temporary file and atomically replaces the destination.

### DW-308: Bind the closure-assembly commit identity after the Story 4.15 artifacts land.

origin: migrated from legacy ledger ("Deferred from: Story 4.15 Step 4 review (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` summary: Bind the closure-assembly commit identity after the Story 4.15 artifacts land. evidence: The packet binds the landed OQ8 capability commit and current path equivalence, but not the later commit that contains the closure layer itself.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/review-subject.json:13 binds completedV1ClosureSnapshotCommit.

### DW-310: [MEDIUM] `Normalize` does not dedupe duplicate MessageIds (reconfirmed, group-1 review).

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-4-committed-event-publication-recovery (2026-08-11)"), 2026-08-30
location: UnpublishedPublicationIndex.cs:148-157
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-4-committed-event-publication-recovery.md` summary: [MEDIUM] `Normalize` does not dedupe duplicate MessageIds (reconfirmed, group-1 review). evidence: `UnpublishedPublicationIndex.Normalize` drops nulls only; duplicate MessageIds inflate `Count` toward capacity. Already on ledger; reconfirmed at `UnpublishedPublicationIndex.cs:148-157`.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 659f3c72a653525f028b0a2212fb568e82bb50cb resolves this duplicate of DW-278; src/Hexalith.EventStore.Server/Actors/UnpublishedPublicationIndex.cs:117-171 now deduplicates MessageIds.

### DW-320: No binding between a committed capture and the receipt of the run that produced it — append-durability-race.json armedAtUtc falls inside the post-mutation window, not the race-test-results.json window; disclosed in prose but not machine-checked.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../commands.md:71
reason: No binding between a committed capture and the receipt of the run that produced it — append-durability-race.json armedAtUtc falls inside the post-mutation window, not the race-test-results.json window; disclosed in prose but not machine-checked. [`evidence/story-4-5/0776785f.../commands.md:71`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 5e8f175b2ced4715f7c6f765386812cc1001dbb4 added CAPTURE_BINDINGS; validate-evidence.py:92-97 and :327-340 require each committed capture to equal the exact post-mutation receipt payload.
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-320, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-323: Redaction gate uses `! rg …`, so an rg failure (exit 2) inverts to success and reports clean having scanned nothing.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../commands.md:135
reason: Redaction gate uses `! rg …`, so an rg failure (exit 2) inverts to success and reports clean having scanned nothing. [`evidence/story-4-5/0776785f.../commands.md:135`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: commands.md:291-303 now captures rg's exit code and treats only exit 1 as clean while failing exit 0 and exit >=2.

### DW-324: commands.md leaks errexit from the mutation wrapper, uses `exit 2` in the canonical-overwrite guard (closes an interactive shell), and runs the redact/hash block on unguarded variables.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../commands.md:89
reason: commands.md leaks errexit from the mutation wrapper, uses `exit 2` in the canonical-overwrite guard (closes an interactive shell), and runs the redact/hash block on unguarded variables. [`evidence/story-4-5/0776785f.../commands.md:89`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: commands.md:15-29 uses return rather than exit for canonical-overwrite refusal, and :187-215 scopes mutation failure handling without leaking errexit.

### DW-327: Story 4.5 provider profile is a source literal validated against itself (daprRuntime "1.18.1", redisImage "redis:6") and the two deterministic classes lack Collection/Trait attributes — deferred to the approved append-fencing follow-up, which must re-capture across multiple provider profiles anyway, so fixing runtime attribution and test placement is cheapest as part of that multi-profile capture.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: AppendDurabilityRaceLiveSidecarTests.cs:364
reason: Story 4.5 provider profile is a source literal validated against itself (daprRuntime "1.18.1", redisImage "redis:6") and the two deterministic classes lack Collection/Trait attributes — deferred to the approved append-fencing follow-up, which must re-capture across multiple provider profiles anyway, so fixing runtime attribution and test placement is cheapest as part of that multi-profile capture. [`AppendDurabilityRaceLiveSidecarTests.cs:364`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 5e8f175b re-captured live-sidecar evidence with the runtime container/image and resolved-port metadata.

### DW-328: Story 4.5 evidence packet left partially updated by the 2026-08-11 review: harness and validator patched (D1/D2/D4/D5) but the live re-capture could not run — DaprTestContainerFixture probes localhost:50005/50006 while Dapr CLI 1.18 publishes placement/scheduler on 6050/6060, and the local control plane is 1.18.2 against a packet claiming 1.18.1. validate-evidence.py fails until a fresh capture regenerates the receipts, source-state.md, and evidence-sha256.txt.

origin: migrated from legacy ledger ("Deferred from: code review of spec-4-5-append-durability-race-evidence (2026-08-11)"), 2026-08-30
location: DaprTestContainerFixture.cs:47
reason: Story 4.5 evidence packet left partially updated by the 2026-08-11 review: harness and validator patched (D1/D2/D4/D5) but the live re-capture could not run — DaprTestContainerFixture probes localhost:50005/50006 while Dapr CLI 1.18 publishes placement/scheduler on 6050/6060, and the local control plane is 1.18.2 against a packet claiming 1.18.1. validate-evidence.py fails until a fresh capture regenerates the receipts, source-state.md, and evidence-sha256.txt. [`DaprTestContainerFixture.cs:47`]
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 5e8f175b; DaprTestContainerFixture.cs:47-52 now probes both 50005/6050 and 50006/6060.

### DW-339: Exercise the release-provenance and deployment-authority validators against a real artifact.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: release-provenance.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Exercise the release-provenance and deployment-authority validators against a real artifact. evidence: `ValidateRelease` and the deployment-authority path validate `release-provenance.json`, `deployment-authority.json`, and `deployment-authority-source.json`, none of which exist in the 21-file committed evidence directory; those code paths have only ever seen synthetic fixtures.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:992 validates the committed candidate through ValidateRelease, and the retained candidate now contains release-provenance.json, deployment-authority.json, and deployment-authority-source.json.

### DW-341: Anchor and scaffold the AC4 acceptance-receipt location.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: acceptances/{subject_sha256
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Anchor and scaffold the AC4 acceptance-receipt location. evidence: `approval_contract.external_receipt_location` is the relative string `acceptances/{subject_sha256}` with no stated root, `required_receipt_fields` binds to no roster version, and the directory does not exist, so AC4 receipt collection cannot begin.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/disposition/6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97/acceptances/a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec/eventstore-owner.json:1 proves the subject-addressed acceptance directory is scaffolded and populated.
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-341, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-342: Bind the outer evidence manifest's own bytes to a hash.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: review-subject.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Bind the outer evidence manifest's own bytes to a hash. evidence: `evidence-sha256.txt` is absent from `evidence-core-sha256.txt` and unbound in `review-subject.json`. Mitigated because its entry set is structurally pinned by `ExpectedOuterFiles` and its listed hashes are recomputed against live bytes, so the practical exposure is narrow.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-342 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-342 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-343: Disclose concurrent Epic 4 and docs changes carried inside the Story 3.13 review range.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: docs/ci.md
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Disclose concurrent Epic 4 and docs changes carried inside the Story 3.13 review range. evidence: Epic 4 tracker rows and Story 4.5/4.14/OQ8/DAPR-pin prose in `docs/ci.md` land inside `1d6e9321..HEAD` from `fe715c70`, `ab1666dd`, `b927472a`, `35a1eecd`, and `86308550`. The proof packet's non-mutation attestation is scoped only to submodule gitlinks, so it under-discloses what its own reviewed range changed.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:365 explicitly discloses the concurrent Epic 4, Story 4.5/4.14, OQ8, and DAPR-pin changes carried in the reviewed range.

### DW-345: Refresh retained evidence `checked_at` timestamps after byte rewrites.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: package-availability.json
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Refresh retained evidence `checked_at` timestamps after byte rewrites. evidence: `package-availability.json` declares `checked_at: 2026-08-04T11:17:05Z` and `registry-readback.json` declares `2026-08-04T11:48:07Z`, but both files were rewritten on 2026-08-09 for host-path redaction and the `cli_candidate_consequence` string.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-345 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-345 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-346: HIGH - Story 1.21 must repair Epic 1 frozen evidence corrupted by the SDK-token sweep in `089369bb`, under its own authority record.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: HIGH - Story 1.21 must repair Epic 1 frozen evidence corrupted by the SDK-token sweep in `089369bb`, under its own authority record. evidence: `089369bb` ("docs: clear remaining root predecessor SDK patch tokens", 25 files) rewrote `10.0.301` to `10.0.302` inside owner-approved Story 1.20 evidence. Story 3.13 restored only its own `fa2d1c99...` tree at `3d6dea69`. Genuine content mismatches remain at HEAD in `critical-evidence-sha256.txt` for `38f85086fc25...`, `4983299103bf...`, and `ec0d35a082bc...` (one `environment.txt` each). Story 3.13 must not write predecessor bytes again, so this needs a separate scoped story. Verified not affected: Story 3.13's `predecessor-tree-sha256.txt` passes 40/40, and the `nuget-sha256.txt` failures are missing proof packages, not corruption. status: resolved 2026-08-20 by Story 1.21. Durable evidence-owner receipt `25a01f60f8f231babb3db860dc8a59d2d46264f6cefe6db7f461fa615316d732` records the observed interactive approval and binds subject `ee5fb076bac380faa0b01ccd7aa96ec9f77955faa96f45c34aafc75d7bc8d26e` plus frozen-block digest `26c7a378bffb3a90eee0fe037aeeeec2e16a290ed91fadd5b4a4db6219db7e92`. The subject restored exactly the three pinned parent blobs; all critical manifests pass 33/33, packages remain independently unavailable at 0/14 per tree, broader Story 1.20 drift is zero, and `bmad:murat` verification binds result `e22e1aef2d24fea81d49ce5e9f495d4ff8d02e989da8b37c1937b517a477e3ab`.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-1-21/evidence-owner-authorization-sha256.txt:1 retains the Story 1.21 evidence-owner authorization.

### DW-347: Reseal or revert Story 4.5's self-invalidating evidence packet.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-11)"), 2026-08-30
location: evidence/story-4-5/0776785f.../validate-evidence.py
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Reseal or revert Story 4.5's self-invalidating evidence packet. evidence: `evidence/story-4-5/0776785f.../validate-evidence.py` was modified by `3e365150` after the packet was sealed at `86308550`; `sha256sum -c evidence-sha256.txt` now reports a genuine content mismatch. Found incidentally during the Story 3.13 evidence-integrity sweep; not a Story 3.13 defect.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 5e8f175b re-sealed the Story 4.5 packet; its evidence-sha256.txt verifies against the retained files.

### DW-356: Replace the five Windows early-return vacuous passes in the container-publishing governance suite with real skips.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: ContainerPublishingGovernanceTests.cs
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Replace the five Windows early-return vacuous passes in the container-publishing governance suite with real skips. evidence: `ContainerPublishingGovernanceTests.cs` returns early at lines 207, 239, 266, 287, 443 and 485 under `OperatingSystem.IsWindows()`. An early return is an xUnit pass, so AC1's "zero-skipped coverage" is satisfied by construction on Windows. Only line 287 is new in this chunk; the other five predate it.
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs now Assert.Skip on Windows for the seven POSIX shell governance cases instead of early-return vacuous passes.

### DW-359: Decide whether the OCI image index should carry provenance annotations.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Decide whether the OCI image index should carry provenance annotations. evidence: `_PublishMultiArchContainers` passes no labels to `CreateImageIndex`, and `validate_packet_files` checks the index only for `schemaVersion`, `mediaType` and two descriptors. The multi-arch tag — the artifact a registry UI surfaces — has no `org.opencontainers.image.*` metadata, and no test asserts either way. The spec requires labels on the child configs only, so this is out of the current contract.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-359 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-359 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-363: Decouple the authority-window theory from the frozen timestamps and split the seven-scenario mutation Fact.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21)"), 2026-08-30
location: n/a
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Decouple the authority-window theory from the frozen timestamps and split the seven-scenario mutation Fact. evidence: `RetainedAuthorityRejectsInvalidWindowAndEditedRecord` InlineData sits exactly one second off the frozen `created_at` (`2026-08-20T11:06:06Z`); if the window check ever passes, the assertion falls through to an opaque summary-mismatch error instead of the intended message. Separately, `CanonicalReleaseIdentityBindsRetainedBytesAndRejectsMutations` packs seven independent mutation scenarios into one ~180-line `[Fact]`, so the first failure hides the other six.
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: CorrectiveOciProvenanceReleaseTests splits retained-byte mutations into focused Facts/Theories and decouples authority-window cases from frozen absolute timestamps via created_at-relative offsets, with the edited-record case as its own Fact.

### DW-364: Split the governed release path into its own reusable workflow file so legacy callers stop having to grant `attestations: write` and `id-token: write`.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, D5 disposition)"), 2026-08-30
location: github/workflows/domain-release.yml
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` owner_repo: `Hexalith.Builds` — reusable `.github/workflows/domain-release.yml`. NOT owned by `Hexalith.EventStore`; the EventStore `release.yml` only calls the reusable workflow, so this fix cannot land here. summary: Split the governed release path into its own reusable workflow file so legacy callers stop having to grant `attestations: write` and `id-token: write`. evidence: GitHub validates the maximum permissions across every job in a called workflow, including jobs that never run. Because `governed-release` (`domain-release.yml:478`) declares both scopes, every caller must grant them — EventStore's `release.yml` now does. The legacy `release` job (`:240`) declares no `permissions:` block, so it inherits the caller's set and executes in the protected `production` environment holding both write scopes unused. The obvious narrow fix — an explicit `permissions:` block on the legacy job — is blocked by an existing Builds contract test, `test_governed_release_workflow.GovernedOffParityTests.test_only_the_governed_job_requests_attestation_permissions`, which asserts `assertNotIn("permissions:", job_slice(workflow, "release"))`; that shape was tried during this review and reverted. Splitting the two paths into separate reusable workflow files removes the coupling without contradicting that contract. Epic 3 explicitly withholds signing/SBOM/attestation authority, so the grant should not persist longer than necessary. severity: medium status: accepted — ratified for now (Story 3.14 D5 option A); nothing is signed or attested because `governed-release: false` keeps the governed job skipped, and `ContainerPublishingGovernanceTests` pins that input.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Hexalith.Builds commit bd94f7f; references/Hexalith.Builds/.github/workflows/domain-release.yml:248-252 omits id-token/attestations globally while :495-508 grants them only to governed-release.

### DW-371: Receipt `source_url` requires a GitHub commit anchor that cannot exist; the existing deferral's "pre-existing pattern inherited" rationale is false and is corrected here.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-21, chunk 1)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:4910-4912
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Receipt `source_url` requires a GitHub commit anchor that cannot exist; the existing deferral's "pre-existing pattern inherited" rationale is false and is corrected here. evidence: `DeployedRuntimeParityClosureTests.cs:4910-4912` requires `…/commit/<SelectedSourceSha>#story-3-13-disposition-<envelopeHash>-<role>`. GitHub mints `#commitcomment-<id>`, so the 3/3 story-completable path is reachable only from `CreateDispositionReceipts` fixtures. The spec Defer list attributes this to a pattern "inherited from `ValidateAcceptances`", but that helper uses a different, subject-keyed anchor `#story-3-13-<subjectHash>-<role>` (`:6613`) — the disposition anchor format is newly authored by commit `56aa0fec`. The durable source record lives in `sources/` inside the same directory as the receipt, so anyone who can author the receipt can author its source: the cross-check proves consistency, not independence. Contrast `LoadReviewerRoster:7010-7024`, which constrains `authority_source` to an https github.com issue-comment URL. Owner decision 2026-08-21: keep the deferral, correct the rationale; non-blocking while 3/3 receipts remain uncollected. severity: medium
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-371 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-371 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-372: Mark or re-tier the heavyweight container-publish theories so the CI-gating Contracts lane is not paying for real `dotnet publish` cycles.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
location: github/workflows/ci.yml
severity: low
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Mark or re-tier the heavyweight container-publish theories so the CI-gating Contracts lane is not paying for real `dotnet publish` cycles. evidence: `ContainerPublicationRejectsMissingProvenanceInputs` runs two full `dotnet publish -t:PublishContainer` cycles at an 8-minute budget each and `ContainerPublicationRejectsMalformedProvenanceInputs` runs four `dotnet msbuild -t:ValidateContainerProvenanceInputs` invocations, all inside `Hexalith.EventStore.Contracts.Tests`, which `.github/workflows/ci.yml` runs as a blocking deterministic gate. Nothing marks them excludable from a fast lane, and the two theories use inconsistent proof strategies (real publish versus direct private-target invocation) for the same guard. Pre-existing: the real-publish pattern arrived in an earlier Story 3.14 round. severity: low
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: CorrectiveOciProvenanceReleaseTests marks RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs, ContainerPublicationRejectsMissingProvenanceInputs, and ContainerPublicationRejectsMalformedProvenanceInputs with Category=HeavyweightContainerPublish; .github/workflows/ci.yml Contracts lane uses --filter-not-trait to exclude them from the default gate; ReleasePackageManifestTests binds the filter.

### DW-373: Document how a second corrective release adds a `v4` evidence handler; the v3 handler is a deliberate single-packet allowlist with no successor and no procedure.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
location: tools/release_evidence_handlers/v3.py:15
severity: medium
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: Document how a second corrective release adds a `v4` evidence handler; the v3 handler is a deliberate single-packet allowlist with no successor and no procedure. evidence: `tools/release_evidence_handlers/v3.py:15` pins `EXPECTED_PACKET_CODEC_SHA256 = 814502bd…` and `:211` additionally rejects `codec["version"] != CODEC_VERSION`, so v3 accepts exactly the one retained codec digest of the frozen v3.96.2 packet. `tools/validate-corrective-release-evidence.py:14` has a single `HANDLERS` entry. Separately, `V3_PUBLICATION_PREFLIGHT_SHA256 = 830af8af…` is the *executed* `eadddc7b` shared preflight; the currently pinned `a07078ad` (and the development gitlink `307a043`) hash to `fe5ffc3f…`, so the legacy role-evidence branch is already closed to anything produced by today's pin. That is correct fail-closed behaviour for the frozen packet but leaves the next corrective release with no documented path, and no `docs/` page describes the `release_evidence_handlers` package or the dispatch table at all. severity: medium
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: docs/ci.md documents the v4 evidence-handler succession procedure without changing live v3.py / dispatcher pins.

### DW-374: The deferred-work ledger itself records a stale publication pin.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release (2026-08-21, chunk 2)"), 2026-08-30
location: deferred-work.md:14
severity: low
reason: source_spec: `/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: The deferred-work ledger itself records a stale publication pin. evidence: `deferred-work.md:14` describes the release-skip race entry's owner_repo as "currently pinned in this repo as `builds-execution-sha: cf04c419378dfe1bd3c41a9244b5e3283092056e`"; the caller has since rotated through `63409393…` to `a07078ad…`. The ledger is append-only legacy-advisory format, so this is recorded rather than edited in place. severity: low
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: append-only clarification recorded below; historical DW-7 pin prose left unchanged.
clarification: 2026-09-05 — The historical `builds-execution-sha: cf04c419378dfe1bd3c41a9244b5e3283092056e` text inside the DW-7 owner_repo note is stale relative to the current EventStore release pin `22a578b576a515d2af214fe81859447fffc97981` (see `.github/workflows/release.yml` `uses:` / `builds-execution-sha` and `ApprovedBuildsReleaseSha`). Intermediate pins included `63409393…` and `a07078ad…`. This ledger remains append-only; do not rewrite the DW-7 entry.

### DW-375: Replace the fixture-only Story 3.13 durable-source URL anchor with a GitHub-minted immutable acceptance reference before collecting the three production receipts.

origin: migrated from legacy ledger ("Deferred from: bmad-build review closure of Story 3.13 (2026-08-22)"), 2026-08-30
location: n/a
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Replace the fixture-only Story 3.13 durable-source URL anchor with a GitHub-minted immutable acceptance reference before collecting the three production receipts. evidence: `RejectDispositionReceipt` requires `#story-3-13-disposition-<envelope-sha256>-<role>` on a commit URL, but GitHub commit-comment anchors use `#commitcomment-<id>`. The retained source record currently proves consistency with its receipt, not independent external existence. The owner accepted deferral while the disposition remains at 0/3 receipts; this does not authorize Story 3.13 completion.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-13/disposition/6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97/acceptances/a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec/sources/eventstore-owner.json:1 retains the GitHub-minted durable acceptance source.

### DW-381: The Story 3.15 Test Architect receipt (`bmad:murat`) has no externally-checkable anchor comparable to the two GitHub-issue-comment-backed owner receipts.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 3)"), 2026-08-30
location: evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/acceptances/bb58d691.../test-architect.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: The Story 3.15 Test Architect receipt (`bmad:murat`) has no externally-checkable anchor comparable to the two GitHub-issue-comment-backed owner receipts. evidence: `evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/acceptances/bb58d691.../test-architect.json` is sourced from a `bmad-test-architect-record` (self-attested by the assembling tooling), unlike the `eventstore-owner`/`release-owner` receipts, which are independently verifiable via `gh api repos/Hexalith/Hexalith.EventStore/issues/comments/<id>`. Reproduces the same durable-receipt-anchor gap already tracked above for Story 3.13's disposition receipts, now recurring for Story 3.15; this is the project's established pattern for `bmad:`-role receipts generally, not a defect unique to this diff. severity: low
status: done 2026-09-01
archived: 2026-09-18
resolution: closed by human decision: Record bmad-role self-attestation as the accepted assurance boundary.
decision: 2026-09-01 Accept established pattern — Record bmad-role self-attestation as the accepted assurance boundary.

### DW-382: `closure.json` declares `deployed_runtime_parity: "available"` and a non-null `selected_deployed_identity` even when `acceptances.receipts` is empty.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-22, loop 3)"), 2026-08-30
location: closure.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: `closure.json` declares `deployed_runtime_parity: "available"` and a non-null `selected_deployed_identity` even when `acceptances.receipts` is empty. evidence: Confirmed unchanged from `HEAD` (pre-existing, not introduced by the 2026-08-22 loop-3 diff) via `git show HEAD:.../closure.json`. The real gate is `_exact_list(receipts, 3, ...)` in `tools/deployed_runtime_parity_handlers/v1.py:360`, which fails closed regardless of those two fields' declared values, so there is no functional hole — but a consumer reading the JSON file directly instead of running `validate-corrected-deployed-runtime-parity.py` would misread pre-acceptance state as already authorized. severity: low
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: docs/ci.md:541-545 explicitly defines the available identity fields as an ungranted claim until all three receipts validate.

### DW-387: The "Suggested Review Order" section's absolute line-number anchors into four sibling files have no re-derivation task tied to them.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-23, loop 4)"), 2026-08-30
location: 3-13-deployed-runtime-parity-closure.md:802
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: The "Suggested Review Order" section's absolute line-number anchors into four sibling files have no re-derivation task tied to them. evidence: Anchors into `3-13-deployed-runtime-parity-closure.md:802`, `docs/ci.md:357`, `sprint-status.yaml:225`, and `deferred-work.md:1410` (this file) will rot on the next edit to any of those files, the same anchor-rot bug class this spec elsewhere treats as requiring an explicit "re-derive Code Map anchors" checklist item. `spec-3-13-deployed-runtime-parity-closure.md#suggested-review-order` (cited by heading, not by line: the original `:331-354` citation had already rotted at authoring time, and the section moved again during loop 5). severity: low
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:384 records replacement of the rotting line citation with a heading citation.

### DW-388: Story 3.15's lifecycle surfaces disagree as committed and no test cross-checks them.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: sprint-status.yaml:227
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 3.15's lifecycle surfaces disagree as committed and no test cross-checks them. evidence: `sprint-status.yaml:227` moved `backlog` -> `in-progress` while `spec-3-15-corrected-deployed-runtime-parity-closure.md:5` carries the in-review token and `docs/ci.md` declares 3.15 validation passing with a selected identity. No Story 3.15 story record carries a `Status:` line, and `CorrectedDeployedRuntimeParityClosureTests.cs` contains no sprint/spec cross-check. Already tracked as loop 3's open patch at `spec-3-13-deployed-runtime-parity-closure.md:249`; owned by Story 3.15. severity: medium
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md:5 and _bmad-output/implementation-artifacts/sprint-status.yaml:246 now both say in-progress.

### DW-389: One of the three Story 3.15 digests published in `docs/ci.md` is bound by no test.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: docs/ci.md
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: One of the three Story 3.15 digests published in `docs/ci.md` is bound by no test. evidence: `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` asserts the subject `bb58d691...` and selected identity `4b141085...`, but the predecessor digest `4d1a0c33...` at `docs/ci.md:382-383` is asserted nowhere against ci.md — it appears only as a test constant, in validator stdout assertions, and as `PREDECESSOR_SHA256` in `tools/deployed_runtime_parity_handlers/v1.py:27`. A stale copy leaves the suite green while naming a predecessor the verifier would reject. severity: low
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:1138-1171 asserts the exact predecessor digest in the bounded docs/ci.md Story 3.15 section.

### DW-394: `docs/ci.md` flattens the known receipt-authenticity asymmetry for Story 3.15.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-13-deployed-runtime-parity-closure (2026-08-24, loop 5)"), 2026-08-30
location: docs/ci.md
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: `docs/ci.md` flattens the known receipt-authenticity asymmetry for Story 3.15. evidence: `docs/ci.md:397-399` states the EventStore owner, Release owner and Test Architect "have provided real authenticated receipts". The two owner receipts are GitHub-issue-comment backed and independently checkable via `gh api`; the `bmad:murat` Test Architect receipt is sourced from a `bmad-test-architect-record`, i.e. self-attested by the same tooling that assembled the packet — an asymmetry this same ledger already records for Story 3.13's disposition receipts. severity: low
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: docs/ci.md:547-550 explicitly discloses the self-attested Test Architect record and lack of independent external authentication.

### DW-395: Story 3.13's three role-bound acceptances are a self-attestation, not independent three-party review.

origin: migrated from legacy ledger ("Deferred from: Story 3.13 acceptance collection (2026-08-24)"), 2026-08-30
location: evidence/story-3-13/disposition/6cee8dad.../acceptances/a7ecd455.../
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md` summary: Story 3.13's three role-bound acceptances are a self-attestation, not independent three-party review. evidence: The packet-bound roster maps `eventstore-owner` and `release-owner` to the same account (`github:jpiquot`), and `test-architect` to `bmad:murat`, a tooling-attested record with no external anchor. The 3/3 gate at `evidence/story-3-13/disposition/6cee8dad.../acceptances/a7ecd455.../` is therefore satisfied by one human plus a bmad record. The two owner receipts are genuinely GitHub-minted and independently re-fetchable (comments 5395155800 / 5395155988 on issue 351), so the evidence is authentic; what is absent is reviewer independence. Same pattern already tracked for Story 3.15's `bmad:murat` receipt. severity: medium
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-395 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-395 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-396: The development-gitlink guard encodes "deliberately independent of the release pin" as a permanent inequality, so a legitimate submodule bump onto the release pin fails a test with no failure meaning.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-14-corrective-oci-provenance-release.md (2026-08-24)"), 2026-08-30
location: ContainerPublishingGovernanceTests.cs:539
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` summary: The development-gitlink guard encodes "deliberately independent of the release pin" as a permanent inequality, so a legitimate submodule bump onto the release pin fails a test with no failure meaning. evidence: `ContainerPublishingGovernanceTests.cs:539` asserts `gitlinkEntry.Groups["sha"].Value.ShouldNotBe(ApprovedBuildsReleaseSha)`. The documented property is that the `uses:` ref and `builds-execution-sha` agree with each other and that the gitlink is read independently (`git ls-tree`), not that the gitlink may never equal `a07078ad…`. The current gitlink is `2f46aaee…`, so the assertion cannot fire today; it becomes a false red the first time an ordinary `build(deps)` bump happens to land on the pinned revision. Pre-existing — introduced before the chunk-3 range (`94591f35`..`da52e2c8`) and untouched by it. severity: low
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:644-647 now enforces ancestor-or-equal instead of pin/gitlink inequality.

### DW-398: The Story 3.13 closure-packet gate `ValidateAcceptances` still enforces the unmintable `#story-3-13-<hash>-<role>` commit anchor that only a fixture can satisfy.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: DeployedRuntimeParityClosureTests.cs:7378
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: The Story 3.13 closure-packet gate `ValidateAcceptances` still enforces the unmintable `#story-3-13-<hash>-<role>` commit anchor that only a fixture can satisfy. evidence: `DeployedRuntimeParityClosureTests.cs:7378` builds `ApprovedSourceSha + "#story-3-13-" + subjectHash + "-" + role` and `:7405,:7409` require `retained-immutable-external-record` and `acceptance-source/v1`, while the disposition path moved to `/v2` and `github-issue-comment` (`:74,:5400-5412`). Live at `:1011` and at `:6144` inside the `story_may_be_done` gate. A genuine GitHub-collected receipt is rejected by it; the synthetic `CreateAcceptanceReceipts` fixture is its only witness. Same defect class Story 3.13 was reopened to remove.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-398 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-398 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-399: `author_association` requirements diverge between the registry authority source and acceptance receipts, and the divergence was resolved downward to keep real evidence passing.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: registry/role-registry-source.json
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: `author_association` requirements diverge between the registry authority source and acceptance receipts, and the divergence was resolved downward to keep real evidence passing. evidence: `_validate_receipts` requires MEMBER/OWNER/COLLABORATOR (`v1.py:794,:809`); the retained roster comment `registry/role-registry-source.json` is CONTRIBUTOR, so `_validate_registry` admits CONTRIBUTOR too. Owner decision needed: tighten the registry to match the receipts (requires a new roster comment) or record the weaker bar as intended.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: tools/deployed_runtime_parity_handlers/v1.py:898 and :1032 apply the same EXPECTED_AUTHOR_ASSOCIATIONS set to registry and receipt sources.

### DW-402: `FrozenStory314PacketRemainsByteForByteUnchanged` hashes a single file despite asserting whole-packet immutability.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: release-identity.json
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: `FrozenStory314PacketRemainsByteForByteUnchanged` hashes a single file despite asserting whole-packet immutability. evidence: The test re-hashes only `release-identity.json` and runs the 3.14 validator; every other file in the frozen packet could be rewritten with the test still green.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:957 enumerates and hashes all 66 retained Story 3.14 packet files and pins the resulting manifest digest.

### DW-403: The `_bmad-output/test-artifacts/` gate artifacts backing the Test Architect receipt disagree with the matrix they summarize and cite a nonexistent test method.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: _bmad-output/test-artifacts/
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: The `_bmad-output/test-artifacts/` gate artifacts backing the Test Architect receipt disagree with the matrix they summarize and cite a nonexistent test method. evidence: `traceability-matrix.md` lists S315-UNIT-001 as `CheckedInTechnicalPacketFailsClosedUntilThreeReceiptsExist`, which exists nowhere; every listed line number is 1-3 low, indicating the matrix was generated before the final edits. `e2e-trace-summary.json` reports `cases: 19` against the matrix's 48, `pct: 100` on zero P1/P2/P3 totals, and `evaluator: Administrator` while the matrix signs off as `bmad:murat`. The files also sit outside the hash-closed packet and are bound by nothing.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/test-artifacts/gate-decision.json:8-13 marks every former PASS-shaped status SUPERSEDED and explains why the artifact is unusable.

### DW-404: The Hexalith.Builds gitlink was rotated to the tip of origin/main while the release workflow pin was left behind.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-15-corrected-deployed-runtime-parity-closure (2026-08-25, loop 2)"), 2026-08-30
location: origin/main
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` summary: The Hexalith.Builds gitlink was rotated to the tip of origin/main while the release workflow pin was left behind. evidence: The gitlink moved to `22a578b5` (== `origin/main`), which changes `Github/publish-containers/publication_preflight.py` and `publish-containers.sh` -- the executed release helpers -- but `.github/workflows/release.yml` still pins `a07078ad`. The Builds-side counterpart of this diff's preflight tightening is therefore not in the executed release path, and rotation is supposed to happen from the pin rather than from main.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .github/workflows/release.yml:115 and :122 align the reusable-workflow ref and executed Builds SHA at 22a578b576a515d2af214fe81859447fffc97981.

### DW-405: BLOCKING, OWNER DECISION -- the next Release run fails at container publish, after NuGet packages are already pushed, because the mandatory `ContainerProvenanceCreated` input is not supplied by the pinned Builds publisher.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: Directory.Build.targets
severity: high
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: high summary: BLOCKING, OWNER DECISION -- the next Release run fails at container publish, after NuGet packages are already pushed, because the mandatory `ContainerProvenanceCreated` input is not supplied by the pinned Builds publisher. evidence: This change removed the `ContainerProvenanceCreated` fallback from `Directory.Build.targets` and added a hard `<Error>` at `Directory.Build.targets:67` requiring an exact UTC RFC 3339 second. `.github/workflows/release.yml:91` pins `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@a07078ad74d3727bc5a6b6d85d47d56a6e5c9fec`, and at that SHA `Github/publish-containers/publish-containers.sh:181-182` passes only `ContainerProvenanceSourceSha` and `ContainerProvenanceReleaseVersion`. The flag is passed only from Builds `22a578b5`, which is the submodule gitlink -- and a reusable workflow resolves from its `uses:` ref, not from the gitlink, so bumping the gitlink does not change what CI executes. Reproduced directly: `dotnet msbuild src/Hexalith.EventStore/Hexalith.EventStore.csproj -t:ValidateContainerProvenanceInputs -p:ContainerProvenanceSourceSha=... -p:ContainerProvenanceReleaseVersion=3.96.2` emits `Directory.Build.targets(67,5): error : ContainerProvenanceCreated must be an exact UTC RFC 3339 second.` Both SHAs are reachable on Builds `origin/main` (the reachability concern is separate, below). Resolution requires an owner decision between rotating the release pin (which also forces the gitlink further ahead, because `ContainerPublishingGovernanceTests` asserts gitlink != `ApprovedBuildsReleaseSha`) and restoring the fallback until the pin rotates. Deliberately not actioned in this loop: rotating a CI pin is outward-facing and belongs to the Story 3.14 lane.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .github/workflows/release.yml:115-122 selects Builds 22a578b576a515d2af214fe81859447fffc97981, whose publisher supplies the mandatory provenance creation instant.

### DW-406: OWNER ACTION -- Story 3.15 has no dedicated acceptance issue, and the three superseded receipts were spliced onto Story 3.14's thread.

origin: migrated from legacy ledger ("bmad-build code review of spec-3-15-corrected-deployed-runtime-parity-closure.md (2026-08-25, loop 3)"), 2026-08-30
location: evidence/story-3-15/superseded-acceptances/bb58d691.../sources/
severity: high
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: high summary: OWNER ACTION -- Story 3.15 has no dedicated acceptance issue, and the three superseded receipts were spliced onto Story 3.14's thread. evidence: Both superseded owner sources under `evidence/story-3-15/superseded-acceptances/bb58d691.../sources/` are anchored on `https://github.com/Hexalith/Hexalith.EventStore/issues/346#issuecomment-...`, i.e. Story 3.14's acceptance thread -- the cross-lineage reuse Story 3.13 was reopened to prevent. The verifier now rejects issues 324 and 346 by number, so re-collection requires a dedicated Story 3.15 issue. Opening it and requesting acceptances is an Ask First action and was not performed.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/registry/role-registry-source.json:3 anchors the roster on dedicated issue #352.

### DW-419: The pinned release publisher cannot supply the newly mandatory container creation timestamp, while a governance test encodes release-pin/gitlink inequality as policy.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: github/workflows/release.yml
severity: high
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: high summary: The pinned release publisher cannot supply the newly mandatory container creation timestamp, while a governance test encodes release-pin/gitlink inequality as policy. evidence: `.github/workflows/release.yml` pins Builds `a07078ad...`, whose publisher omits `ContainerProvenanceCreated`; `Directory.Build.targets` rejects that omission, and NuGet publication precedes container publication. `ContainerPublishingGovernanceTests.cs` separately requires the release pin to differ from the development gitlink, obstructing the straightforward alignment fix. This belongs to the Story 3.14 release lane.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: .github/workflows/release.yml:115-122 now pins both the reusable workflow and builds-execution-sha to the timestamp-capable Builds revision.

### DW-420: The legacy release job retains unused `attestations: write` and `id-token: write` permissions.

origin: migrated from legacy ledger ("Trusted-verifier hardening pass (2026-08-25) -- appended under the loop-4 heading"), 2026-08-30
location: github/workflows/release.yml
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The legacy release job retains unused `attestations: write` and `id-token: write` permissions. evidence: `.github/workflows/release.yml` grants both permissions to the production release job although the current legacy path does not consume them. Removing or splitting them changes release-workflow authority and is outside Story 3.15's evidence-only boundary.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Hexalith.Builds commit bd94f7f; the legacy release job inherits only the root scopes at domain-release.yml:248-263, while sensitive scopes are job-local at :495-508.

### DW-442: SUPERSEDES the loop-3 entry stating that opening a dedicated Story 3.15 acceptance issue and requesting acceptances "is an Ask First action and was not performed" -- it was subsequently performed.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: n/a
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: SUPERSEDES the loop-3 entry stating that opening a dedicated Story 3.15 acceptance issue and requesting acceptances "is an Ask First action and was not performed" -- it was subsequently performed. evidence: Dedicated issue [#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352) was opened, its MEMBER-authenticated roster comment `5407975180` was retained as the registry `authority_source`, and two complete acceptance rounds were collected on that thread: `5408186984`/`5408189299` against subject `dab64f5f...`, then `5409145568`/`5409148235` against `a8cc777e...`, plus two timestamp-mismatched attempts `5409140199`/`5409147909` marked superseded. The earlier ledger and spec wording is stale at HEAD and is corrected by this entry rather than edited in place, because the ledger is append-only. What remains genuinely unperformed is collecting a *third* round against the current subject `663747b1...`, which this landing did not do. status: open — recorded correction; the outstanding owner action is the new receipt round.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 30336c2c records the final Story 3.15 subject and all three current owner/test-architect receipts.
decision: 2026-08-31 Implement verified change — Implement the concrete change described by DW-442, add focused regression coverage, and update any directly affected contract or operator documentation.

### DW-443: Ledger-format repair notice for the trusted-verifier hardening block filed under the loop-4 heading.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: closure.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: Ledger-format repair notice for the trusted-verifier hardening block filed under the loop-4 heading. evidence: That block carried machine-local absolute `source_spec` paths, omitted `severity:`, sat under a heading naming a different pass, and duplicated several still-open loop-3 items (the v3-versus-v1 timestamp parser and the hardcoded `closure.json` inventory path are also duplicated within the block itself). The absolute paths, missing severities and heading were repaired in place; the duplicates are left standing because the ledger is append-only. Nothing enforces any of this: every `Dw6*` governance case and both `Dw4` ATDD cases are `[Fact(Skip = ...)]`, which is tracked separately above. status: open — format repaired; the missing enforcement gate remains deferred.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 44b79719 migrated the legacy ledger material into structured DW records.

### DW-445: The retained roster comment names the ratified artifact `reviewer-roster.json` while the packet retains `registry/owner-role-registry.json`.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: reviewer-roster.json
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: low summary: The retained roster comment names the ratified artifact `reviewer-roster.json` while the packet retains `registry/owner-role-registry.json`. evidence: `EXPECTED_REGISTRY_AUTHORITY_BODY` requires the retained comment body verbatim, and that body's wording was copy-carried from Story 3.13. The reference is understood to mean the retained registry file. Correcting it requires a new owner comment on `#352` (an external write) plus another subject re-mint, so the mismatch is recorded in the verifier source, the story record and `docs/ci.md` instead. status: open — recorded as a known mismatch by owner decision (loop 6).
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-445 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-445 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-447: The retained Production smoke bytes were produced by the pre-loop-6 capture tool, so the bound tool of record can no longer reproduce the bytes it certifies.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-6 authorized batch landing (2026-08-25)"), 2026-08-30
location: n/a
severity: medium
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: medium summary: The retained Production smoke bytes were produced by the pre-loop-6 capture tool, so the bound tool of record can no longer reproduce the bytes it certifies. evidence: The smokes are timestamped `2026-08-21T19:24-19:26`. The loop-6 batch bound both producers into the closure `dispatch` block and simultaneously changed the capture tool (bounded cleanup budget, populated-directory refusal, prerequisite docstring), so the currently bound capture digest is not the digest of the tool that produced the retained logs. Re-capturing was deliberately not done: it would replace evidence rather than bind it, and needs Docker plus arm64 binfmt emulation. Every *future* producer edit now re-mints the subject. status: open — accepted for this packet; re-capture belongs to a separately authorized evidence pass.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-447 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-447 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-450: SUPERSEDES the loop-6 landing note in the sense that two loop-6 fixes were themselves regressions, both reproduced with live controls and closed in loop 7.

origin: migrated from legacy ledger ("Deferred from: Story 3.15 loop-7 landing (2026-08-25)"), 2026-08-30
location: n/a
severity: high
reason: source_spec: `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md` severity: high summary: SUPERSEDES the loop-6 landing note in the sense that two loop-6 fixes were themselves regressions, both reproduced with live controls and closed in loop 7. evidence: (1) Narrowing the nuspec DTD scan to the XML prolog also made `_reject_prolog_declarations` return silently when the prolog did not begin with `<`. `utf-8-sig` strips exactly one BOM, so a doubled `EF BB BF` left a residual `U+FEFF` and the scan was skipped; with the fix reverted in a scratch copy a nuspec carrying `<!DOCTYPE package [<!ENTITY smuggle "Hexalith.Evil">]>` is ACCEPTED and returns id `Hexalith.Evil`. (2) Adding `TypeError` to `_is_repository_path`'s catch in both dispatchers made a bytes repository path answer False, so such a module escaped displacement and the post-execution shadow check; verified `str -> True, bytes -> False` before the fix and `bytes -> True` after. The lesson recorded here: a narrowing fix needs a control proving the *old* behaviour is still covered on the path it narrowed. status: closed 2026-08-25 (loop 7) — retained as a recorded regression class, not as open work.
status: done 2026-08-31
archived: 2026-09-18
resolution: already resolved: commit 31683502 closes the doubled-BOM prolog and bytes-path fail-open regressions with live controls.

### DW-455: `REVIEW_ROSTER` names two reviewers as specific accountable personas but the security role is only a generic role label.

origin: migrated from legacy ledger ("Deferred from: code review of story-4-15-oq8-platform-closure-and-handoff (2026-08-30)"), 2026-08-30
location: tools/validate-oq8-platform-evidence.py:237-241
severity: low
reason: source_spec: `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` severity: low summary: `REVIEW_ROSTER` names two reviewers as specific accountable personas but the security role is only a generic role label. evidence: `REVIEW_ROSTER = {"architecture": "Winston (System Architect)", "security": "Security Reviewer", "test": "Murat (Test Architect)"}` — `tools/validate-oq8-platform-evidence.py:237-241`. The asymmetry recurs through both the v1 and v2 review schemas. status: open — deferred, cosmetic; doesn't affect the evidentiary integrity of the check itself.
status: done 2026-08-31
archived: 2026-09-18
resolution: closed by human decision: Record the current DW-455 behavior and its verified limitation as an intentional, human-approved disposition.
decision: 2026-08-31 Accept current contract — Record the current DW-455 behavior and its verified limitation as an intentional, human-approved disposition.

### DW-472: Restrict manual-snapshot success inference.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`CreateManualSnapshotAsync`)
reason: `CreateManualSnapshotAsync` catches inspection, reconstruction, creation, and save failures together, then reports `Created` whenever any pre-existing snapshot has the current sequence. An earlier infrastructure failure can therefore be misreported as successful creation; inference should be limited to an ambiguous snapshot save and compare the exact expected snapshot.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; AggregateActor.cs:1695-1805 tracks snapshot-save ambiguity and compares the attempted snapshot before inferring Created.

### DW-474: Add a pre-commit drain-retry persistence repair test.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: tests/Hexalith.EventStore.Server.Tests (drain-retry persistence)
reason: Existing tests cover normal retry persistence and commit-then-throw ambiguity only; no test proves that a failure before the first save commits is discarded, inspected, and repaired with exactly one durable retry increment.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; EventDrainRecoveryTests.cs:535 exercises retry-increment failure before commit and verifies one durable repair.

### DW-475: Establish actor-state batch safety after admission staging failures.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`StagePendingCommandCountAsync` and `ActorStateMachine.CheckpointAsync`)
reason: `StagePendingCommandCountAsync` and `ActorStateMachine.CheckpointAsync` run before the guarded save, but no catch discards their batch if a staging call throws. A Dapr implementation guarantee or fault test must establish whether a post-staging exception can retain a commit-capable abandoned state batch for a later save.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; AggregateActor.cs:891-940 discards staged admission mutations on pre-commit infrastructure failure, covered at AggregateActorInfrastructureFailureTests.cs:1974.

### DW-476: Apply actor discard-or-poison handling to legacy idempotency paths.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (legacy idempotency migration and source reads)
reason: A legacy migration can stage the new key before legacy-key removal throws, while legacy source or redirect reads swallow state-manager failures as `Unavailable`; both paths can leave a possibly unsafe cache without actor-owned discard-or-poison remediation.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; AggregateActorFencingTests.cs:483 and the corresponding AggregateActor poison/discard paths cover the lost-checkpoint branch.

### DW-477: Preserve cancellation for aggregate event metadata reads.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (`GetEventsAsync`)
reason: `GetEventsAsync` catches metadata-read `OperationCanceledException` as `Exception` and wraps it in `EventDeserializationException`, although adjacent event reads preserve cancellation, so callers and telemetry can misclassify cancellation as corrupt state.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; AggregateActorGetEventsTests.cs:89 and :102 prove cancellation is propagated rather than converted into deserialization failure.

### DW-480: Finalize a reused stale-Processing slot after failed replacement admission.

origin: migrated from legacy ledger ("unsectioned flat append from spec-4-7-tenants-query-provenance-follow-up.md"), 2026-09-06
location: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs (stale checkpoint cleanup and replacement admission)
reason: After stale-checkpoint cleanup commits, pre-commit replacement-admission inspection returns false and overwrites `pendingCommandTracked`; the `finally` path then skips decrementing the now-ownerless durable pending slot.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 57fa0909; AggregateActor.cs:959-964 preserves stale processing-slot ownership, covered at AggregateActorInfrastructureFailureTests.cs:1489.

### DW-495: Persist recovered handler query-type indexes when sibling domain metadata fails.

origin: administrator authorization 2026-09-06 after Story 4.7 AC3 halt (P7-BH-07)
location: src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs
reason: `AdminOperationalIndexHostedService.StartAsync` skipped every admin index write, including `admin:query-types:{domain}`, whenever any configured domain metadata source failed. The refresh loop then updated named projection routes only, so a healthy Tenants metadata load never persisted `admin:query-types:tenants` while `sample` continued to throw. `DaprDomainQueryHandlerRegistry` treated the missing catalog as “no handlers” and fail-opened to `ProjectionBacked`. Administrator authorized a separate EventStore change: persist handler query-type indexes for domains whose metadata loaded successfully, and rewrite them when a later `RefreshAsync` recovers that binding. Named-route `Replace` and projection/type-catalog indexes remain all-or-nothing.
status: done 2026-09-06
archived: 2026-09-18
evidence: Release `Hexalith.EventStore.Client.Tests.Indexes.AdminOperationalIndexHostedServiceTests` 12/12, 0 skipped, 0.586s. Live Story 4.7 proof `Generated_tenants_api_get_tenant_reads_verified_redis_state_without_projection_authority` 1/1, 0 skipped, 26.173s after Redis catalogs were deleted: Event 6104 persisted recovered query-type indexes for 2 domains while Event 6101 still skipped projection/type-catalog writes; EventStore invoked `tenants/method/query`; DAPR hash `eventstore||admin:query-types:tenants` contained `get-tenant`, `get-tenant-audit`, `get-tenant-users`, `get-user-tenants`, and `list-tenants`.
resolution: `AdminOperationalIndexHostedService` now writes `admin:query-types:{domain}` for every domain whose metadata loaded successfully, including when sibling sources fail, and `RefreshAsync` rewrites that catalog when a binding recovers. Named-route `Replace` and projection/type-catalog indexes remain all-or-nothing.

