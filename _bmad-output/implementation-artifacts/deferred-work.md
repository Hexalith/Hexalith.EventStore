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

## Deferred from: code review of 21-2-layout-and-navigation-foundation (2026-04-13)

- ✅ **[RESOLVED-IN-21-12 on 2026-04-16]** **Brand token CSS `@media (prefers-color-scheme: dark)` doesn't respond to user-selected theme** — Fixed by converting all 3 `@media (prefers-color-scheme: dark)` blocks to `[data-theme="dark"]` CSS selectors + System-mode fallbacks with `:root:not([data-theme])`. JS interop updated to set `data-theme` attribute alongside `color-scheme` property. All project-owned CSS custom properties now respond to the theme toggle.
- **ThemeToggle missing `JSDisconnectedException` handling** — `ThemeToggle.razor` calls `JSRuntime.InvokeAsync`/`InvokeVoidAsync` in `OnAfterRenderAsync`, `CycleTheme`, and `ApplyColorSchemeAsync` without try/catch for `JSDisconnectedException`. `MainLayout` already has proper handling. Pre-existing omission.
- **ThemeState `Changed` event not thread-safe** — `ThemeState.SetMode()` reads and writes `_mode` with no synchronization. `Changed?.Invoke()` fires on the caller's thread. Low risk in single-circuit Blazor Server scoped DI. Pre-existing pattern.
- **v4 FAST design tokens referenced in CSS not defined by v5** — `--neutral-layer-1`, `--neutral-foreground-rest`, and other FAST tokens used in `App.razor` body style and `app.css` are not defined by Fluent UI v5 (which removed FAST web components). Explicitly Story 21-8 scope.
- **FluentTreeView nested inside FluentNavCategory — potential ARIA nesting issue** — `FluentTreeView`/`FluentTreeItem` inside `FluentNavCategory` may produce invalid ARIA semantics (nav role containing tree role). Pre-existing pattern (was `FluentNavGroup` + `FluentTreeView` in v4).

## Deferred from: code review of 21-7-toast-api-update.md (2026-04-14)

- **AC 19/21/22 validation remains blocked by downstream compile errors** - Admin.UI still has downstream 21-8/21-9 compile blockers, so Admin.UI.Tests execution and visual verification gates are deferred. Treat as pre-existing blocker and re-run these gates once downstream stories land.

## Deferred from: code review of 21-8-css-token-migration (2026-04-14)

- **Card-container oracle verification pending for highest-blast-radius mapping** - AC 2b computed-style oracle for `--neutral-layer-card-container` is explicitly deferred to Task 7a because Admin.UI runtime boot is blocked by 21-9 compile failures.
- **Core visual and accessibility sweep remains deferred until 21-9 unblocks runtime** - AC 19/20/21/22 screenshot sweep, DevTools glow verification, and Axe/WebAIM checks cannot run until Admin.UI boots.
- **App.css spike-gate runtime smoke check deferred by the same runtime blocker** - Task 3.5 pre-inline-style browser spike was deferred to Task 7a due the 21-9 blocker.

## Deferred from: code review of 21-9-datagrid-remaining-enum-renames (2026-04-15)

- **Manual browser validation gates still pending execution** — ACs/tasks covering the visual sweep, runtime grid interactions, accessibility audit, and dialog/toast verification are intentionally deferred to a dedicated browser session and were not executed in this code-only review pass.
- ✅ **[RESOLVED-IN-21-9-5 on 2026-04-15]** Admin.UI.Tests compile failures deferred from story 21-9 are now resolved. **Resolution:** Story 21-9.5 brought Admin.UI.Tests from 86 → 0 compile errors and slnx 122 → 36 (36 = 21-10 Sample.BlazorUI scope). AC 8 partially passed (3 MergedCssSmokeTests first-run green); 62 latent bUnit runtime failures unmasked by compile-green are deferred to new follow-up **21-9.5.7-admin-ui-tests-v5-runtime-migration**.

## Deferred from: code review of 21-9-datagrid-remaining-enum-renames (2026-04-16, final review)

- **`FluentLabel Typo=` removed property in v5** — `CommandSandbox.razor:200` uses `<FluentLabel Typo="Typography.PaneHeader">` but `Typo` was removed from `FluentLabel` in v5. Should be migrated to `<FluentText Typo="Typography.PaneHeader">`. Pre-existing, not caused by 21-9.
- **Stale "Fluent UI v4" comment** — `AdminUIServiceExtensions.cs:27` says "Fluent UI v4 components" but the project uses v5 (`5.0.0-rc.2`). Misleading for future maintainers.
- **`FluentDialog aria-label` splatted attribute in v5** — `CommandPalette.razor:4`, `CommandSandbox.razor:197`, `EventDebugger.razor:261` use lowercase HTML `aria-label` on `<FluentDialog>`. v5 removed the component-level `AriaLabel` property; splatted attributes should still work but need runtime ARIA verification to confirm the label reaches the correct DOM element.

## Deferred from: code review of 21-8-css-token-migration (2026-04-16, round 2 — post-21-9 browser verification)

- **CorrelationTraceMap `--colorBrandBackground` CSS override relies on FluentBadge internals** — `.pipeline-stage-failed` overrides `--colorBrandBackground` to inject error-red into filled badges. Works in v5 today but fragile against future Fluent releases that change internal token consumption. Consider `BackgroundColor=` parameter or `BadgeColor.Danger` in a future cleanup.
- **`--neutral-layer-card-container` → `--colorNeutralBackground2` interim mapping pending DevTools oracle** — Both `--neutral-layer-2` and `--neutral-layer-card-container` map to the same v5 token. User completed browser tests; if cards looked correct, mapping is validated. If not, sed-replace to `--colorNeutralBackground1Hover`.
- **bUnit MergedCssSmokeTests compilation blocked by upstream stories** — 3 new smoke tests structurally correct but cannot run until upstream CS0103 chain resolves. Tests were written compile-ready per spec.

## Deferred from: code review of 21-11-navmenu-v5-fix.md (2026-04-16)

- **Tier 1 gate remains 751/753 (pre-existing, unrelated to 21-11)** — AC target is 753/753, but the two known Contracts failures predate this story and were not introduced by NavMenu changes.
- **Interactive visual evidence gate (AC2/AC6/AC12) deferred by user request** — User requested to skip this decision path ("passons ça"). Hover-state, responsive resize, and screenshot proof remain to be completed in a reviewer browser session before story closure.
