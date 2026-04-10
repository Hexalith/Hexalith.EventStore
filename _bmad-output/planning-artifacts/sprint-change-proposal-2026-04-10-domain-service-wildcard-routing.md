# Sprint Change Proposal — Domain Service Wildcard Routing

**Date:** 2026-04-10
**Author:** Jerome (with Claude Code analysis)
**Scope:** Minor — additive change in EventStore Server, dev-only config, ~15 lines of code + 2 config entries + 4 unit tests + 1 architecture doc line
**Status:** Approved and implemented

---

## 1. Issue Summary

While exercising the Admin UI Commands page (`/commands`) under multiple tenants from the Keycloak realm (`admin-user` has access to `tenant-a`, `tenant-b`, `system`), commands submitted on behalf of any tenant other than `tenant-a` failed deterministically with **HTTP 500** and ended up in the dead-letter topic `deadletter.<tenant>.counter.events`.

### Evidence

EventStore structured log for a `tenant-b IncrementCounter` submission:

```
Dead-letter published:
  TenantId=tenant-b, Domain=counter, AggregateId=counter-1, CommandType=IncrementCounter,
  ErrorMessage=Domain service invocation failed for tenant 'tenant-b', domain 'counter',
               service 'counter/process':
               Response status code does not indicate success: 500 (Internal Server Error).
```

Compared to the same command for `tenant-a`:

```
service 'sample/process'   ← OK (resolved via static registration)
service 'counter/process'  ← 500 (resolved via convention to non-existent app)
```

### Discovery context

Discovered during a populate-the-Commands-page exploration session. After sending 17 counter commands (15 acceptable + 2 deliberately rejected) to populate the Admin UI, every `tenant-b` submission failed identically. The EventStore was reachable, the JWT carried `tenant-b` in its `eventstore:tenant` claim, and authorization was passing — the failure was strictly downstream of the domain service router.

---

## 2. Root Cause Analysis

`appsettings.Development.json` contained two static `DomainServices.Registrations` entries that mapped routing for the dev sample:

```json
"tenant-a|counter|v1":  { "AppId": "sample", ... }
"tenant-a|greeting|v1": { "AppId": "sample", ... }
```

Neither entry covered `tenant-b` or `system`. When `DomainServiceResolver.ResolveAsync` (in `Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`) failed to find an exact match for `tenant-b|counter|v1` and the DAPR config store path was disabled (`ConfigStoreName = null`), it fell through to the **convention-based fallback**:

```csharp
return new DomainServiceRegistration(
    AppId: domain,        // "counter"
    MethodName: "process",
    ...);
```

This invoked DAPR app `counter` — which **does not exist** in the Aspire topology. The actual app hosting the `counter` and `greeting` domains is named `sample`. The DAPR sidecar's invocation against the missing app id surfaced as a 500 to the calling actor.

### Why this is structural, not a one-off bug

The sample project (`samples/Hexalith.EventStore.Sample`) hosts **two domains** under a single DAPR `AppId = sample`:
- `Counter/` (Increment, Decrement, Reset, Close + projection)
- `Greeting/` (SendGreeting + aggregate)

This violates ADR #8 (Convention-First with Configuration Overrides — `architecture.md:233`) which states **AppId = domain name**. The static registrations were the workaround. The workaround only covered the single tenant the team had been testing with (`tenant-a`), so any new tenant — present in Keycloak from the start, or future-created via the Admin UI — silently inherited the bug.

---

## 3. Impact Analysis

### Epic Impact

| Epic | Impact |
|---|---|
| Domain Service Integration (FR21–FR25) | Touches the resolver — additive only, no contract change |
| Multi-tenancy (FR26+) | Removes a hidden multi-tenant block in dev environment |
| Sample / Dev Experience | Eliminates the per-tenant config maintenance burden in `appsettings.Development.json` |

### Artifact Conflicts

| Artifact | Required Change |
|---|---|
| `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` | Add wildcard fallback layer between exact-match and DAPR-config-store lookup |
| `src/Hexalith.EventStore/appsettings.Development.json` | Replace 2 tenant-specific entries with 2 wildcard entries |
| `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs` | Add 4 tests covering wildcard match, exact-precedence, domain mismatch, version mismatch |
| `_bmad-output/planning-artifacts/architecture.md` (D7 §458) | Document the 4-layer override hierarchy (exact → wildcard → config store → convention) |

### PRD / MVP Impact

**None.** This change touches dev/test routing only; the production routing path (DAPR config store + convention) is untouched. ADR #8's "convention-first" principle is preserved — wildcards live inside the existing override mechanism.

### Risk Assessment

- **Code risk:** Low. The change is purely additive; the new wildcard layer only fires when no exact match exists, so all existing exact registrations behave identically. The 28 existing+new resolver tests all pass.
- **Operational risk:** None for production. Production deployments use the DAPR config store (layer 3) which is unchanged.
- **Test risk:** New tests cover wildcard match, exact-takes-precedence, no-wildcard-for-domain (falls through to convention), and version-respecting behavior.

---

## 4. Path Forward Evaluation

Four options were considered:

| # | Option | Effort | Future-tenant-proof | ADR #8 alignment | Verdict |
|---|---|---|---|---|---|
| 1 | Add `tenant-b\|counter\|v1` + `tenant-b\|greeting\|v1` entries | Trivial | ❌ rebreaks for every new tenant | ⚠️ perpetuates workaround | Rejected — band-aid |
| 2 | Rename DAPR `AppId sample → counter` | Medium | ✅ for `counter` only | ⚠️ breaks `greeting` routing | Rejected — destructive |
| 3 | Split sample into `sample-counter` + `sample-greeting` apps | High | ✅ | ✅ pure | Rejected — overkill for a *sample* |
| **4** | **Add wildcard tenant support `*\|domain\|version` + 2 wildcard entries** | **Low (~15 LoC)** | ✅ | ✅ extends override mechanism cleanly | **Selected** |

### Selected Approach: Option 4

**Rationale:**
- Covers all current and future tenants with a single config entry per domain — no maintenance burden as the realm grows
- Honors ADR #8: stays within the existing override mechanism, doesn't compete with the convention principle
- Preserves the sample's pedagogical "small, multi-domain example" intent
- Minimal blast radius: 15 lines of resolver code, 2 JSON entries, 4 unit tests, 1 doc line
- Zero risk to production routing (production uses DAPR config store, untouched)

---

## 5. Detailed Change Proposals

### Change 1 — `DomainServiceResolver.cs` (additive)

```csharp
// After exact-match block, before DAPR config store lookup:

// Wildcard tenant fallback: "*|domain|version" matches any tenant for the given domain.
// Allows a single registration to cover all current and future tenants — useful for
// dev/test sample apps that host multiple domains under one DAPR app-id (which would
// otherwise force one static registration per tenant). Exact registrations always win
// over wildcard. The returned record is rewritten with the actual tenantId so downstream
// invokers see the real tenant rather than the literal "*".
string wildcardKey = $"*|{domain}|{version}";
if (options.Value.Registrations.TryGetValue(wildcardKey, out DomainServiceRegistration? wildcardRegistration)) {
    logger.LogDebug(
        "Resolved domain service from wildcard tenant registration: AppId={AppId}, Method={MethodName}, WildcardKey={WildcardKey}, TenantId={TenantId}",
        wildcardRegistration.AppId,
        wildcardRegistration.MethodName,
        wildcardKey,
        tenantId);
    return wildcardRegistration with { TenantId = tenantId };
}
```

### Change 2 — `src/Hexalith.EventStore/appsettings.Development.json`

```diff
 "DomainServices": {
   "Registrations": {
-    "tenant-a|counter|v1": {
+    "*|counter|v1": {
       "AppId": "sample",
       "MethodName": "process",
-      "TenantId": "tenant-a",
+      "TenantId": "*",
       "Domain": "counter",
       "Version": "v1"
     },
-    "tenant-a|greeting|v1": {
+    "*|greeting|v1": {
       "AppId": "sample",
       "MethodName": "process",
-      "TenantId": "tenant-a",
+      "TenantId": "*",
       "Domain": "greeting",
       "Version": "v1"
     }
   }
 }
```

### Change 3 — `DomainServiceResolverTests.cs` (4 new tests)

| Test | What it proves |
|---|---|
| `ResolveAsync_WildcardTenantRegistration_MatchesAnyTenant` | Single `*\|counter\|v1` entry resolves for `tenant-a`, `tenant-b`, AND `system`, with `TenantId` correctly rewritten to the actual caller |
| `ResolveAsync_ExactRegistration_TakesPrecedenceOverWildcard` | When both `tenant-a\|counter\|v1` and `*\|counter\|v1` exist, `tenant-a` resolves to the exact override; `tenant-b` falls through to wildcard |
| `ResolveAsync_NoWildcardForDomain_FallsThroughToConvention` | A wildcard for one domain doesn't poison resolution of unrelated domains (still uses convention) |
| `ResolveAsync_WildcardRegistration_RespectsVersionInKey` | Wildcards are version-scoped: a `v2` wildcard does not match a `v1` lookup |

### Change 4 — `architecture.md` D7 §458 (documentation)

```diff
- **Service discovery:** Convention-based by default — AppId = domain name, MethodName = "process"
  (zero configuration). Override hierarchy: (1) static registrations in appsettings.json
  (for local dev/test), (2) DAPR config store ..., (3) convention fallback.

+ **Service discovery:** Convention-based by default — AppId = domain name, MethodName = "process"
  (zero configuration). Override hierarchy: (1) **exact** static registrations in appsettings.json
  keyed by `tenant|domain|version` (for local dev/test), (2) **wildcard tenant** static registrations
  keyed by `*|domain|version` — single entry covers all tenants for a given domain, useful when one
  DAPR app hosts multiple sample domains, (3) DAPR config store ..., (4) convention fallback.
  Exact registrations always win over wildcards; wildcard registrations have their `TenantId`
  rewritten to the actual caller before being returned to downstream invokers.
```

---

## 6. Implementation Handoff

| Aspect | Value |
|---|---|
| **Scope classification** | Minor — direct dev implementation |
| **Handoff recipient** | Dev (executed inline by Claude during this session) |
| **Restart required** | `eventstore` and `sample` Aspire resources (both done via Aspire MCP) |
| **Migration / data impact** | None — pure routing-resolver change |

### Success Criteria

1. ✅ All 28 `DomainServiceResolverTests` pass (24 existing + 4 new)
2. ✅ `tenant-b counter IncrementCounter` returns HTTP **202** (was 500)
3. ✅ `system counter IncrementCounter` returns HTTP **202** (was 500)
4. ✅ Event keys appear in Redis under `eventstore||AggregateActor||tenant-b:counter:counter-*||...:events:N`
5. ✅ Event keys appear in Redis under `eventstore||AggregateActor||system:counter:counter-1||...:events:1`
6. ✅ EventStore logs no longer show `service 'counter/process': 500 Internal Server Error` for non-tenant-a callers

### Validation Run (2026-04-10)

```
=== Validation: tenant-b + system commands (was 500 before fix) ===
  tenant-b  counter-1 IncrementCounter     -> 202 OK
  tenant-b  counter-1 IncrementCounter     -> 202 OK
  tenant-b  counter-1 DecrementCounter     -> 202 OK
  tenant-b  counter-2 IncrementCounter     -> 202 OK
  system    counter-1 IncrementCounter     -> 202 OK

=== Redis state ===
eventstore||AggregateActor||tenant-b:counter:counter-1||tenant-b:counter:counter-1:events:1
eventstore||AggregateActor||tenant-b:counter:counter-1||tenant-b:counter:counter-1:events:2
eventstore||AggregateActor||tenant-b:counter:counter-1||tenant-b:counter:counter-1:events:3
eventstore||AggregateActor||tenant-b:counter:counter-2||tenant-b:counter:counter-2:events:1
eventstore||AggregateActor||system:counter:counter-1||system:counter:counter-1:events:1
```

All success criteria met.

---

## 7. Follow-up Notes

- **Bootstrap 401 (out of scope, related):** The `Hexalith.Tenants.Bootstrap.TenantBootstrapHostedService` startup logs `Bootstrap unexpected response: StatusCode=401` because it calls `POST /api/v1/commands` without a JWT. This is a separate issue from the routing fix and does **not** prevent tenants from being created via the Admin UI (which forwards user JWT correctly). Worth tracking as its own ticket.
- **Architectural debt remaining:** The Sample project still violates the strict "AppId = domain" convention by hosting two domains. The wildcard mechanism is the legitimate way to handle this in dev/test (per the updated ADR #8 wording), but if at some point the project decides to enforce the convention more strictly in samples, splitting the sample into two AppHost resources (`sample-counter`, `sample-greeting`) would be the path. Not blocking, not urgent.
