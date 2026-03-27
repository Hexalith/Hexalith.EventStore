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

- **D1: O(N*logN) state reconstruction performance** — ReconstructState replays from event 0 at each bisect midpoint. O(S*N) total work for S steps and N events. Pre-existing pattern shared by blame/diff endpoints. Requires actor API range query support to optimize.
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
