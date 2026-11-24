---
title: 'Seven deferred-work fixes (DW-33, DW-43, DW-45, DW-58, DW-60, DW-63, DW-76)'
type: 'bugfix'
created: '2026-11-24'
status: 'in-progress'
route: 'oneshot'
baseline_commit: '5cb60061a93cb7ecc1d1a94fad47d486131b4b31'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The owner asked for the next seven deferred-work items in priority order (severity, then ledger number), skipping blocked or obsolete ones. Every open high, medium and low item is blocked, already resolved, or targets evidence tooling the 2026-10-23 evidence simplification deleted. The next seven actionable unranked items are: DW-33 (sample "no projection yet" contract is implicit), DW-43 (sample fabricates `count 0` on a 304 without cached state), DW-45 (sample API forwards a comma-joined multi-valued `Authorization` header), DW-58 (default replay actor's cache keys on paging it never applies), DW-60 (an unresolvable `PayloadAggregateIdPropertyName` silently skips every event), DW-63 (registry-owned domain diagnostics are disposed several times by DI) and DW-76 (SignalR `projectionType`/`tenantId` have no length or control-character bound).

**Approach:** Fix each at its source with focused regression tests, mark it done in `deferred-work.md` with a one-line resolution, and commit each separately with its DW number. No public API removals; keep behavior for valid inputs unchanged.

</frozen-after-approval>

## Implementation Notes

- Selection: every open high/medium/low entry is blocked, already resolved, or targets evidence tooling deleted by the 2026-10-23 evidence simplification; unranked items were taken in ledger order. Skipped on the way: DW-34 (historical D5 scope hygiene), DW-35 (awaits owner decision B3), DW-37 (superseded by the ApiScope fail-closed rule: referenced contracts opt in through `[RestRoute(ApiScope)]`), DW-51 (deliberate org CI decision), DW-56 (CI pipeline scope), DW-57 (projection actor type is a Contracts route-catalog constant), DW-59 (needs a per-handler marker or composite handler contract decision), DW-61 (fail-fast would break hosts; needs a decision), DW-62 (already resolved: `AllowAnonymous` on health endpoints), DW-64/65 (Tenants host composition / Roslyn guardrail deferral).
- DW-33: `CounterQueryFailure` owns the empty-state rule and failure text for the four counter components.
- DW-43: a 304 is accepted only when it confirms the cached validator.
- DW-45: single-value forwarding; inbound JwtBearer already rejects repeated headers.
- DW-58: review surfaced the documented `invalid-page` contract, so non-zero offsets are now rejected by the default replay actor; the cache hook is `private protected` and exact-type only.
- DW-60: one resolution per type, string-assignable properties, startup and per-type warnings with the CLR type name.
- DW-63: idempotent dispose guard; ownership stays with the registry.
- DW-76: shared hub validation; the scope control-character rule from the first draft was dropped to keep the hub aligned with the sender's scope contract; the SDK client mirrors the projection-type and tenant bounds.

## Review Triage Log

- A1 medium — empty state keyed only on 404: patched (reason code `query_projection_missing`).
- A2/A3 medium — every 200 labeled semantic failure, no correlation: patched (title match, correlation ID, malformed 200 keeps client text).
- A4 low — test name contradicted result: patched.
- A5 low — 304 guard ignored validator match: patched.
- A6 low — unusable 304 shows payload message: rejected, unreachable from the components (they send no validator without a cached one).
- A7 low — handler doc described a path the host never takes: patched.
- A8 low — forward the validated token instead of the raw header: rejected, pre-existing design; inbound validation already rejects malformed values.
- A9 low — outbound request may already carry `Authorization`: rejected, nothing in the sample sets one.
- A10 low — missing null-context test: patched; source-scan guard for components rejected (regex-guard disposition).
- A11/B3b/C10 false — ledger updated in each item's commit.
- B1 high — SDK client would retry forever on new hub rejections: patched (client mirrors bounds, rejections permanent). Older SDK clients against a newer hub can still loop on such values; none are produced by valid callers.
- B2 medium — sender accepts scopes the hub refused: patched by dropping the new hub scope control-character rule.
- B3 medium — silent offset ignore contradicts documented `invalid-page`: patched.
- B4/B9 medium — subclass hazard and public API addition: patched (`private protected`, exact-type merge).
- B5 low — missing tenant/snapshot/leave-100/blank-row tests: patched.
- B6 low — 100 duplicated: rejected, a shared public constant would add API; comments cross-reference.
- B7 low — format characters: patched; kebab-case enforcement rejected (could refuse groups existing clients join).
- B8 low — base hook tests and stale comments: patched.
- C1 low — teardown test passes without the guard: rejected, the guard is not observable because `Meter`/`ActivitySource` dispose is already idempotent; the test pins the teardown contract.
- C2 medium — warning only after the first lost event: patched (construction-time warning when no registered type has the property).
- C3 low — string-type check narrowed matching: patched (`IsAssignableFrom(typeof(string))`).
- C4 medium — `new`-hidden property throws `AmbiguousMatchException`: deferred as DW-546 (pre-existing).
- C5/C8/C9 low — misleading per-event message, wire name, double lookup: patched.
- C6 low — missing tests: patched.
- C7 low — setting docs: patched.
