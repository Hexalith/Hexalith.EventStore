# Deferred Work

## Deferred from: code review of 19-5-dapr-component-health-history (2026-03-27)

- **Unbounded state store growth per day partition** — No cap on entries per day key in `DaprHealthHistoryCollector`; with low capture intervals and many components the day-partition JSON blob can grow large. Accepted design in spec for v1.
- **`DaprComponentHealthTimeline.HasData` flag redundant with `Entries.Count > 0`** — Creates a consistency invariant the record type cannot enforce. `with` expressions allow creating inconsistent state. Pre-existing design pattern across models.
- **`IHealthQueryService.GetComponentHealthHistoryAsync` has no range guard at service level** — Controller limits to 7 days but the service interface has no such constraint. A future direct caller could pass multi-year ranges generating hundreds of parallel state store reads. No non-controller callers exist yet.
- **Missing interactive UI tests** — Spec testing strategy lists "time range buttons trigger data reload", "component click opens drill-down", "deep link parameters applied on load" as expected tests. These are not implemented. Test enhancement for future iteration.
