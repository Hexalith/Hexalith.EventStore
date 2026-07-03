# Deferred Work

- 2026-07-01: Packaging governance tests hard-code external dependency patch versions. Consider a lower-maintenance guard that still proves central version pins and emitted package metadata stay aligned, so routine published package bumps do not require brittle test-only edits.

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
