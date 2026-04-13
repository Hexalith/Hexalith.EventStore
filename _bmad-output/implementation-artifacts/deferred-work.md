# Deferred Work

## Deferred from: code review of 19-5-dapr-component-health-history (2026-03-27)

- **Unbounded state store growth per day partition** — No cap on entries per day key in `DaprHealthHistoryCollector`; with low capture intervals and many components the day-partition JSON blob can grow large. Accepted design in spec for v1.
- **`DaprComponentHealthTimeline.HasData` flag redundant with `Entries.Count > 0`** — Creates a consistency invariant the record type cannot enforce. `with` expressions allow creating inconsistent state. Pre-existing design pattern across models.
- **`IHealthQueryService.GetComponentHealthHistoryAsync` has no range guard at service level** — Controller limits to 7 days but the service interface has no such constraint. A future direct caller could pass multi-year ranges generating hundreds of parallel state store reads. No non-controller callers exist yet.
- **Missing interactive UI tests** — Spec testing strategy lists "time range buttons trigger data reload", "component click opens drill-down", "deep link parameters applied on load" as expected tests. These are not implemented. Test enhancement for future iteration.

## Deferred from: code review of 20-1-blame-view-per-field-provenance (2026-03-27)

- **D1: CommandApi controller uses `[AllowAnonymous]`** — `AdminStreamQueryController` has no auth; relies on DAPR service mesh for isolation. Follows existing CommandApi pattern. Consider adding service-to-service auth if CommandApi is ever exposed externally.
- **D2: `actor.GetEventsAsync(0)` loads ALL events into memory** — Actor API returns the full event array before blame truncation is applied. For aggregates with very large event streams, this could cause memory pressure. Requires actor API changes to support range queries.
- **D3: `ConfigureAwait(false)` in Blazor components** — Project-wide pattern. Technically unsafe in Blazor Server (can cause `StateHasChanged()` to run off sync context). Should be reviewed as a project-wide concern, not per-story.
- **D4: DeepMerge never removes keys** — JSON merge approach can only add/update fields, not remove them. Blame view may show stale fields that were removed from real aggregate state. Inherent limitation of JSON-based state reconstruction without domain Apply methods.
- **D5: JsonDiff nested removal detection missing** — Removal detection only checks top-level keys. Nested field removals are not tracked. Currently gated by D4 (DeepMerge prevents key removal), but would surface if D4 is fixed.
- **D6: `maxEvents`/`maxFields` bypass via direct CommandApi access** — Query parameters have no upper bound validation. Defense-in-depth concern gated by DAPR network isolation.
- **D7: JSON arrays treated as opaque leaf values** — Array-heavy aggregate state shows entire arrays as single changed fields rather than element-level diffs. No array diff algorithm in scope for v1.
- **D8: Test tasks 8.5-8.8 incomplete** — Truncation edge case tests, core value proposition test, BlameViewer component tests, and StreamDetail blame integration tests remain unimplemented. Should be completed before final merge.

## Deferred from: code review of 20-2-bisect-tool-binary-search-state-divergence (2026-03-27)

- **D1: O(N\*logN) state reconstruction performance** — ReconstructState replays from event 0 at each bisect midpoint. O(S\*N) total work for S steps and N events. Pre-existing pattern shared by blame/diff endpoints. Requires actor API range query support to optimize.
- **D2: DeepMerge doesn't handle field deletion** — JSON merge can only add/update fields, not remove them. Bisect state reconstruction may include stale fields. Pre-existing limitation (D4 from 20-1).
- **D3: JsonDiff uses string comparison (not DeepEquals)** — Final field-change extraction at the divergent event uses string-based diff, not semantic JSON comparison. The core bisect loop correctly uses JsonElement.DeepEquals. Pre-existing in blame/diff (D5 from 20-1).
- **D4: ConfigureAwait(false) in Blazor component** — BisectTool.razor uses ConfigureAwait(false) in LoadFieldsAsync and StartBisectAsync then accesses component state. Project-wide pattern (D3 from 20-1).
- **D5: No upper bound on maxSteps/maxFields query params** — Attacker can pass MAX_INT to bypass field count protection. Defense-in-depth concern gated by DAPR network isolation (D6 from 20-1).

## Deferred from: code review of 20-3-step-through-event-debugger (2026-03-27)

- **D1: All admin stream endpoints (blame, bisect, step, diff) use O(N) JSON merge for state reconstruction instead of snapshot-accelerated actor state** — Each request replays all events from event 0. With 100-event snapshot intervals, actor-backed reconstruction would be O(snapshot_interval) per call. Requires actor API changes to expose snapshot-aware state reconstruction for admin queries. Should be addressed as a single cross-cutting story for all endpoints together.
- **W1: `totalEvents` from last `SequenceNumber` not `allEvents.Length`** — If sequences are non-contiguous, `HasNext`/`HasPrevious` computed properties give wrong results and navigation hits 404s. Pre-existing pattern shared by blame/bisect endpoints. Requires actor API to provide true event count.
- **W2: `GetEventsAsync(0)` loads entire stream into memory** — Admin step endpoint loads all events for the aggregate on every request. No pagination. Pre-existing pattern (same as D2 from 20-1).
- **W3: `DeepMerge`/`FlattenJson` have no recursion depth limit** — Deeply nested event payloads could cause `StackOverflowException`. Pre-existing code used by blame/bisect/step endpoints.
- **W4: `FieldChange` constructor throws on empty `FieldPath`** — `JsonDiff` can produce empty-string field paths from malformed JSON keys, causing `ArgumentException` that surfaces as HTTP 500. Pre-existing in `JsonDiff`/`FieldChange`.

## Deferred from: code review of 20-4-command-sandbox-test-harness (2026-03-27)

- **IssueBanner lacks severity parameter** — Info/success/warning banners all render as warning style. Sandbox (and all Epic 20 components) cannot distinguish severity visually. Pre-existing component limitation — all IssueBanner usages across the project have the same constraint.
- **IStreamQueryService.SandboxCommandAsync returns nullable vs spec non-nullable** — The nullable return (`SandboxResult?`) is a pragmatic choice allowing the Admin.Server facade to return 404 on null. Changing the interface contract requires broader refactoring of the delegation pattern across all IStreamQueryService implementations.

## Deferred from: code review of 15-10-admin-ui-data-pipeline-fixes (2026-03-30)

- **4 already-correct methods use LogWarning instead of LogError** — `GetAggregateBlameAsync`, `GetEventStepFrameAsync`, `BisectAsync`, `SandboxCommandAsync` all log at Warning then rethrow. The 8 fixed methods now log at Error then rethrow. Spec-sanctioned inconsistency for v1.
- **Duplicate AdminClaimTypes classes across UI and Server** — `Admin.UI/Services/AdminClaimTypes.cs` (property `Role`) and `Admin.Server/Authorization/AdminClaimTypes.cs` (property `AdminRole`) both hold `"eventstore:admin-role"`. Creates drift risk if either is changed independently.
- **OperationCanceledException from linked CancellationTokenSource leaks as 500** — Methods with 30/60s hard timeouts (`GetAggregateBlameAsync`, `GetEventStepFrameAsync`, `BisectAsync`, `GetCorrelationTraceMapAsync`) throw `OperationCanceledException` that the controller explicitly does not catch, resulting in 500 instead of 504/408.
- **No test verifying controller returns HTTP 503 on service failure** — `AdminStreamsController.IsServiceUnavailable` maps exceptions to 503 but no test exercises this path end-to-end.

## Deferred from: code review of 16-5-tenant-management-quotas-onboarding-comparison.md (2026-04-06, round 2 — 2026-04-09)

- **Open RBAC when users have no permission claims** — `ClaimsRbacValidator` skips permission checks when `permissionClaims.Count == 0`, treating absence of claims as unrestricted access. Pre-existing "open by default" design decision. A new user with only tenant claims but no permission claims gets full access within their tenants.
- **Write endpoints still flatten command failures to HTTP 422** — `AdminTenantsController` now sits in front of a command service that preserves upstream failure codes, but the controller still converts all rejected writes into HTTP 422. Deferred because this behavior predates the rework and any broader status-handling cleanup should be coordinated with downstream clients.
- **Detail panel refresh can race after tenant selection changes** — `ReloadDetailPanel()` reloads users for the current `_expandedTenantId` without cancellation or a post-await tenant guard. If the operator switches rows while a mutation-triggered refresh is in flight, the panel can briefly show the wrong tenant's users. Pre-existing in the page before this rework.

## Deferred from: code review of 19-6-admin-dapr-metadata-diagnostics.md (2026-04-10)

- **Pub/Sub rules parser schema mismatch with DAPR 1.17** — `DaprInfrastructureQueryService.GetPubSubOverviewAsync` still assumes nested `rules.rules[]` shape, while DAPR 1.17 metadata uses a direct `rules[]` array. This is a pre-existing issue from earlier story work and is tracked for follow-up via correct-course.

## Deferred from: code review of 15-13-stream-activity-tracker-writer (2026-04-13)

- **Single global key scalability bottleneck** — All tenants/aggregates share one DAPR state key (`admin:stream-activity:all`) with MaxEtagRetries=3. Under high write throughput, optimistic-concurrency failures will cause silent data loss. Pre-existing architecture decision for admin index pattern.
- **Constructor overload proliferation in SubmitCommandHandler** — Each new optional tracker dependency requires updating N+1 constructor overloads. Pre-existing pattern across the handler.
- **Writer/reader state store config mismatch** — Writer uses `CommandStatusOptions.StateStoreName`, reader uses `AdminServerOptions.StateStoreName`. Both default to `"statestore"` but are independently configurable. Only diverges if deployment explicitly sets different names.

## Deferred from: code review of 21-1-package-version-csproj-infrastructure (2026-04-13)

- **Icons package remains at v4.14.0** — No v5 `Microsoft.FluentUI.AspNetCore.Components.Icons` package exists on nuget.org. Kept at `4.14.0` per user approval. AC #1 partially unmet. Monitor for future v5 Icons release.
- **Stale `.nuget.g.props` artifacts tracked in git reference v4.13.2** — Two auto-generated NuGet restore cache files under `.artifacts/ui-test-obj/` still reference `microsoft.fluentui.aspnetcore.components\4.13.2`. Pre-existing repo hygiene issue — these files should be gitignored. Running `dotnet restore` regenerates them.

## Deferred from: code review of 21-0-bunit-smoke-tests-baseline (2026-04-13)

- **`">99<"` fragile assertion pattern in skeleton loading test** — `StatCardTests.StatCard_ShowsSkeletonWhenLoading` asserts `ShouldNotContain(">99<")` which is brittle against whitespace or attribute changes. Works for current implementation but fragile for future refactors.
- **Operator role not tested for Settings visibility** — `NavMenu_SettingsHiddenForNonAdminRole` only tests `AdminRole.ReadOnly`, not `AdminRole.Operator`. The `>=` comparison boundary at Operator is untested.
- **No test for partial ActionLabel/ActionHref** — EmptyState tests cover both-provided and neither-provided, but not the partial case (only one of ActionLabel/ActionHref set).
- **No test for Icon/ChildContent RenderFragment parameters** — EmptyState has conditional branches for Icon and ChildContent render fragments that are completely untested.
- **No test for Subtitle/Title (tooltip) parameters on StatCard** — StatCard's optional Subtitle and Title parameters have no dedicated assertions.
- **No test for unknown/default Severity fallback values** — StatCard's switch-expression default branch (unknown severity strings) is never exercised.
- **No test for delayed aria-live announcement mechanism** — The 5-second debounced `QueueAnnouncement` logic is untested; only the `aria-live` attribute presence is verified.
- **No Dispose/lifecycle tests for NavMenu or StatCard** — Both components implement `IDisposable` with event unsubscription and CancellationTokenSource cleanup, but no test verifies disposal behavior.
