# Sprint Change Proposal — `NullReferenceException` from ETag Actor Proxy (Remoting/Non-Remoting Invocation Mismatch)

- **Date:** 2026-05-25
- **Author:** Jérôme Piquot (with Claude Code)
- **Trigger:** `System.NullReferenceException` (Source `Dapr.Actors`) at `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync`, surfacing in `DaprETagService.GetCurrentETagAsync` (`DaprETagService.cs:33`).
- **Scope classification:** Minor (localized code regression; no PRD/epic/MVP impact).
- **Related (distinct):** `sprint-change-proposal-2026-05-25-dapr-placement-actor-outage.md` — that one is a `TaskCanceledException` from actors *hanging* to timeout when placement is unreachable. This is a different fault: a synchronous NRE thrown *inside the proxy* before any network call, independent of placement health.

---

## Section 1 — Issue Summary

`DaprETagService` retrieves the current projection ETag by invoking the `ETagActor`. The recent cancellation-token work (commits `f60bf356`, `bcccd504` — "add cancellation token support to projection and query methods") changed the invocation so it could thread a `CancellationToken` into the actor call.

The problem: `IETagActor.GetCurrentETagAsync()` is a **remoting** interface method and exposes **no** `CancellationToken` overload. To pass a token, the code cast the remoting proxy to the base `ActorProxy` and called the weakly-typed, **non-remoting** `InvokeMethodAsync<string?>(method, ct)`:

```csharp
IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(   // remoting proxy
    new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
...
if (proxy is ActorProxy actorProxy) {
    return actorProxy.InvokeMethodAsync<string?>(                      // non-remoting call
        nameof(IETagActor.GetCurrentETagAsync), cancellationToken);
}
```

A proxy built by the **remoting** `CreateActorProxy<IETagActor>` overload has its **non-remoting interactor left null**. Calling the non-remoting `InvokeMethodAsync<T>` on it dereferences that null → `NullReferenceException` thrown inside `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync`. This happens **synchronously, before any sidecar/network call**, on **every** ETag fetch.

### Root cause

Actor **invocation-model mismatch**: a *remoting*-created proxy invoked via the *non-remoting* code path. (Dapr.Actors 1.17.9.)

### Two aggravating factors

1. **Silent failure (the real harm).** The `catch (Exception ex)` fail-open at `DaprETagService.cs:43` swallows the NRE and returns `null`. So in production the ETag fetch returns `null` **100% of the time** — silently disabling the conditional-GET / projection change-detection optimization served by ETags (`QueriesController` lines 97 & 156, `CachingProjectionActor` line 49). The system "works" but degraded; the only visible symptom is a first-chance NRE in the debugger.
2. **Test gap.** Every test in `DaprETagServiceTests` substitutes `IETagActor` directly (a mock, **not** an `ActorProxy`), so the production `if (proxy is ActorProxy)` branch was **never executed** by any test. The regression shipped green. (Reinforces Epic 2 retro R2-A6: mock-only coverage of an adapter edge is not coverage.)

### Evidence index

| Signal | Value |
|--------|-------|
| Exception | `System.NullReferenceException`, `Source=Dapr.Actors` |
| Throw site | `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync` (non-remoting path) |
| Surface site | `DaprETagService.GetCurrentETagAsync` → `DaprETagService.cs:33` |
| Introduced by | cancellation-token feature (`f60bf356`, `bcccd504`) |
| Masking | `catch (Exception)` fail-open at `DaprETagService.cs:43` → returns `null` every fetch |
| Test blind spot | `DaprETagServiceTests` mock `IETagActor`; the `proxy is ActorProxy` branch is never run |

---

## Section 2 — Impact Analysis

- **No PRD / epic / MVP impact.** No requirement changes.
- **Functional impact:** ETag-based optimizations (conditional GET / change detection) were silently inert wherever `DaprETagService` is the live implementation. Correctness was preserved (callers treat a null ETag as "unknown") but the performance/bandwidth benefit was lost.
- **Process impact:** a silent fail-open masked a 100%-failure regression; an adapter-edge branch had zero test execution.
- **Affected artifacts:**
  - `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` — fix (this proposal).
  - `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` — regression-doc test (this proposal).

---

## Section 3 — Recommended Approach

**Direct adjustment.** Invoke the ETag actor through its **remoting** `IETagActor` interface and remove the non-remoting cast/branch entirely. Cancellation is honoured by the pre-checks (`ThrowIfCancellationRequested` before and after proxy creation) plus the existing **3 s `RequestTimeout`** in `_proxyOptions`; the remoting interface deliberately exposes no per-call token. The `ETagActor.GetCurrentETagAsync` implementation returns in-memory state (`Task.FromResult`), so mid-flight cancellation has negligible practical value over the 3 s bound.

### Alternatives considered

- **Non-remoting factory + `InvokeMethodAsync<T>(method, ct)`** (i.e. create the proxy via `IActorProxyFactory.Create(actorId, actorType, options)`, which returns an `ActorProxy` whose non-remoting interactor *is* initialized). This would preserve mid-flight token propagation and is a legitimate Dapr pattern, **but** `ActorProxy` is abstract with no mockable invocation surface (`InvokeMethodAsync<T>` is non-virtual; the type has no accessible ctor), so the happy path is not unit-testable without a live sidecar. Rejected: more risk, worse testability, for a benefit (cancelling an O(1) in-memory call inside a 3 s window) that is effectively nil.
- **Rollback / MVP review:** not applicable.

The chosen fix also **unifies the production and test invocation paths** (both now call `IETagActor.GetCurrentETagAsync()`), structurally eliminating the untested branch that allowed the regression.

---

## Section 4 — Detailed Change Proposals

### 4.1 — `DaprETagService.GetCurrentETagAsync` (implemented)

Removed the `proxy is ActorProxy` cast and the `InvokeETagActorAsync` helper. The actor is now invoked through the remoting interface, with a cancellation pre-check retained.

**OLD**
```csharp
IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
    new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
return await InvokeETagActorAsync(proxy, cancellationToken).ConfigureAwait(false);
// ... plus a private InvokeETagActorAsync that cast to ActorProxy and called
//     the non-remoting InvokeMethodAsync<string?>(method, ct).
```

**NEW**
```csharp
// Invoke through the strongly-typed IETagActor (remoting) interface. We must NOT cast to the
// base ActorProxy and call the weakly-typed InvokeMethodAsync<T>(method, ct): that is the
// *non-remoting* path, and on a proxy built by the remoting CreateActorProxy<IETagActor>
// overload its non-remoting interactor is null → NullReferenceException. Cancellation is
// honoured by the pre-checks plus the 3 s RequestTimeout in _proxyOptions.
IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
    new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
cancellationToken.ThrowIfCancellationRequested();
return await proxy.GetCurrentETagAsync().ConfigureAwait(false);
```

The `catch (OperationCanceledException) { throw; }` / `catch (Exception) { log; return null; }` fail-open structure is retained (the fail-open is still the correct posture for a genuinely unreachable actor; it was only *masking* the always-on NRE, not the cause of it).

### 4.2 — Regression-documentation test (implemented)

Added `GetCurrentETagAsync_InvokesActorThroughRemotingInterface` to `DaprETagServiceTests`. It documents the NRE root cause in-line and pins the surviving contract (service awaits `IETagActor.GetCurrentETagAsync()` and returns its value). The test is explicit that a mocked `IETagActor` cannot reproduce the real-`ActorProxy` NRE — so the **durable guard is structural** (the branch no longer exists), not the assertion.

### 4.3 — Live-sidecar Tier-2 regression test (implemented)

Added `DaprETagServiceLiveSidecarTests` (`tests/.../Integration/`, `[Collection("DaprTestContainer")]`). It drives the **production** path end-to-end against a real `daprd` sidecar and a real `ETagActor` over the Redis actor state store — the adapter edge the mock-based unit and existing "integration" suites cannot reach (the existing `ETagActorIntegrationTests` substitutes `IActorProxyFactory`, so it never builds a real `ActorProxy` and never saw the NRE).

- `GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` — seeds state via the real actor's `RegenerateAsync`, then asserts `DaprETagService.GetCurrentETagAsync` returns the **actual persisted ETag** (R2-A6 end-state inspection). Fails against the pre-fix code (NRE → fail-open null); passes after.
- `GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing` — a never-regenerated actor returns a genuine cold-start null via the real remoting path, distinguishing it from the masked-NRE null.

Both pass against live infra (DAPR placement/scheduler/Redis up). Requires `dapr init` + Docker, per CLAUDE.md Tier 2.

### 4.4 — Not in scope / deliberately not done

- **No change to the fail-open contract.** Returning `null` on a real actor outage is intended behaviour (callers treat null as "no ETag").
- **No refactor of the existing `ETagActorIntegrationTests`.** It remains a useful notification-path test; it is simply not the adapter-edge guard (4.3 now provides that).

---

## Section 5 — Implementation Handoff

- **Scope:** Minor — Developer agent, done in this session.
- **Verification (this session):**
  1. `Hexalith.EventStore.Server` + `Server.Tests` build in Release under `TreatWarningsAsErrors` — 0 warnings, 0 errors.
  2. `DaprETagServiceTests` (Tier 1, mocked) — **14/14 passing** (13 existing + 1 new).
  3. `DaprETagServiceLiveSidecarTests` (Tier 2, live `daprd` + real `ETagActor` + Redis) — **2/2 passing**.
- **Success criteria:**
  1. ETag fetch no longer throws/masks an NRE; a healthy `ETagActor` returns its actual ETag (not a fail-open null).
  2. Pre-cancellation still throws `OperationCanceledException` before proxy creation; OCE is not converted to a fail-open null (existing tests green).
  3. Production and test invocation paths are unified (no `proxy is ActorProxy` branch remains).
  4. The adapter edge is now covered by a live-sidecar Tier-2 test (4.3) that exercises the real proxy against a real actor (per R2-A6).
- **Senior review:** recommended before close, per the project's mandatory review stage.
