# Sprint Change Proposal — Keycloak Dev Fast-Start

- **Date:** 2026-05-25
- **Author:** Jerome (via Correct Course workflow)
- **Workstream:** `post-epic-deferred-dw19-keycloak-dev-fast-start`
- **Scope classification:** Minor → Moderate (Direct Adjustment)
- **Status:** Approved

---

## Section 1 — Issue Summary

**Problem:** Keycloak is slow in development. Every `aspire run` and every Tier 3 integration-test run pays a full Keycloak container cold-start, which dominates the inner-loop wait.

**Context / discovery:** Raised during day-to-day development. The triggering observation: the AppHost adds Keycloak via `builder.AddKeycloak("keycloak", 8180).WithRealmImport("./KeycloakRealms")` (`src/Hexalith.EventStore.AppHost/Program.cs:63`), and the Tier 3 fixtures budget 4–5 minute startup timeouts to absorb the cold-start (`tests/.../Fixtures/KeycloakAuthFixture.cs:53`, `tests/.../Security/AspireTopologyFixture.cs:55`).

**Evidence & premise correction:** A set of general Keycloak speed-up techniques was provided (pre-built `--optimized` image, `start-dev`, in-memory DB, feature/metric disabling, local cache, baked-in realm, Testcontainers reuse). Grounding these against this repo:

- **Aspire already runs Keycloak in `start-dev` mode locally** → no Quarkus augmentation build happens in the dev loop. The proposed "single biggest win" (`kc.sh build` + `start --optimized`) targets production-shaped images and is **not applicable** to `aspire run`.
- **`start-dev` already provides** H2 in-memory DB + `--cache=local` + HTTP (techniques 2, 3, 5 are already on).
- **The realm is tiny** (`hexalith-realm.json` = 9.4 KB, 1 client) → realm import is not the bottleneck (technique 6 N/A).
- The remaining, real cost is **repeated container cold-start**. The applicable lever is **container reuse** (technique 7). Aspire's analog of Testcontainers `.WithReuse(true)` is `WithLifetime(ContainerLifetime.Persistent)`.

---

## Section 2 — Impact Analysis

| Area | Impact |
|------|--------|
| **Epic** | None. Post-epic dev-experience/infra work; no epic re-scope, re-sequence, or new epic. |
| **Stories** | None modified. New tracking entry only. |
| **PRD / MVP** | None. No product requirement, scope, or goal change. |
| **Architecture** | None. Auth design unchanged — still OIDC discovery against the `hexalith` realm; the persistent flag only affects container lifetime. |
| **UI/UX** | None. |
| **Technical / code** | AppHost (`Program.cs`) gains an opt-in flag; two Tier 3 fixtures gain a CI-safe opt-in; three docs updated. |
| **`project-context.md` rule** | "Prefer non-persistent resources" — honored via **default-OFF opt-in**; documented as an explicit, narrow exception. |
| **CI** | Unchanged. CI never sets the opt-in env vars, so CI stays cold/clean and reproducible. |

---

## Section 3 — Recommended Approach

**Option 1 — Direct Adjustment (SELECTED).** Add an opt-in `KeycloakPersistent` flag to the AppHost (default OFF) plus a CI-safe `KEYCLOAK_TEST_REUSE` opt-in for the Tier 3 fixtures, and document both.

- **Effort:** Low. **Risk:** Low.
- **Why:** Smallest change that removes the actual bottleneck (repeated cold-start) while preserving CI reproducibility and the project's non-persistent default. No architecture or contract surface is touched.

**Options not taken:**

- *Pre-built `--optimized` image:* Not viable/needed — Aspire uses `start-dev` locally (no augmentation to skip), and the repo deliberately uses .NET SDK container support with no Dockerfiles.
- *Default-ON persistence for dev:* Faster still, but overrides the project's non-persistent rule and risks stale-realm confusion. Rejected in favor of opt-in.
- *Rollback / MVP review:* N/A — no completed work to revert, no scope to cut.

**Realm-edit caveat (carried into docs):** A reused container does not re-import the realm. After editing `KeycloakRealms/hexalith-realm.json`, remove the container (`docker rm -f`) so it re-imports on the next start.

---

## Section 4 — Detailed Change Proposals

### 4.1 AppHost — opt-in persistent Keycloak

**File:** `src/Hexalith.EventStore.AppHost/Program.cs` (inside the `EnableKeycloak` block, after the `.WithRealmImport(...)` call)

```csharp
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    // Dev fast-start (opt-in, default OFF). Set KeycloakPersistent=true to reuse the Keycloak
    // container across `aspire run` restarts so the cold-start + realm import is paid once
    // instead of every restart. Default OFF honors the project's "prefer non-persistent
    // resources" rule. NOTE: a reused container does NOT re-import the realm — after editing
    // KeycloakRealms/hexalith-realm.json, remove the container (`docker rm -f`) so it re-imports
    // on the next start.
    if (bool.TryParse(builder.Configuration["KeycloakPersistent"]?.Trim(), out bool keycloakPersistent)
        && keycloakPersistent) {
        _ = keycloak.WithLifetime(ContainerLifetime.Persistent);
    }
```

### 4.1b AppHost — WaitForStart relaxation (CONSIDERED, REJECTED)

A `WaitForStart(keycloak)` relaxation in run mode was prototyped to take Keycloak's ~30s cold-start off the topology critical path (JwtBearer loads OIDC metadata lazily, so services do not strictly need Keycloak healthy to boot). **Rejected:** the Tier 3 fixtures build this same AppHost via `DistributedApplicationTestingBuilder`, which executes in run mode, so the relaxation would apply to tests too. A service reaching healthy before Keycloak can validate tokens introduces an OIDC-discovery race and flaky auth tests (current and future). **Decision: all Keycloak-dependent services keep `WaitFor(keycloak)` (healthy) in every mode, including tests.** Cold-start is addressed only by reuse (4.1); the ~30s remains on the critical path on a true cold start, which is acceptable as a once-per-session cost.

### 4.2 Tier 3 fixtures — CI-safe reuse opt-in

**Files:** `tests/Hexalith.EventStore.IntegrationTests/Fixtures/KeycloakAuthFixture.cs` and `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs` (identical pattern in each).

Add field:
```csharp
    private string? _previousKeycloakPersistent;
```

In `InitializeAsync`, after `EnableKeycloak` is set to `"true"`:
```csharp
    // Opt-in container reuse for faster LOCAL test iteration (default OFF so CI stays
    // cold/clean). Set KEYCLOAK_TEST_REUSE=true to keep the Keycloak container warm
    // between `dotnet test` runs — only the first run pays the cold-start.
    _previousKeycloakPersistent = Environment.GetEnvironmentVariable("KeycloakPersistent");
    if (bool.TryParse(Environment.GetEnvironmentVariable("KEYCLOAK_TEST_REUSE"), out bool reuseKeycloak)
        && reuseKeycloak) {
        Environment.SetEnvironmentVariable("KeycloakPersistent", "true");
    }
```

In `DisposeAsync`, alongside the existing `EnableKeycloak` restore:
```csharp
    Environment.SetEnvironmentVariable("KeycloakPersistent", _previousKeycloakPersistent);
```

### 4.3 Docs

- **`docs/guides/troubleshooting.md`** — new subsection **"Keycloak Slow Startup (Dev Fast-Start)"** after the existing Keycloak Startup Issues section: explains the cold-start cost, the `KeycloakPersistent=true` / `KEYCLOAK_TEST_REUSE=true` switches, and the realm-reset (`docker rm -f`) step.
- **`docs/getting-started/quickstart.md`** — extend the first-run **Note** (line 34) with a **Tip** pointing to the fast-start switch and the troubleshooting anchor.
- **`_bmad-output/project-context.md`** — add a bullet after the `EnableKeycloak=false` line documenting `KeycloakPersistent=true` as a narrow, opt-in exception to the non-persistent rule, with the realm re-import caveat.

### 4.4 Sprint tracking

- **`_bmad-output/implementation-artifacts/sprint-status.yaml`** — append `post-epic-deferred-dw19-keycloak-dev-fast-start: backlog` with a comment block referencing this proposal.

---

## Section 5 — Implementation Handoff

- **Scope:** Minor → Moderate. Direct implementation by the Developer agent; sprint-status update accompanies it.
- **Recipient:** Developer agent.
- **Tasks:** Apply 4.1–4.4. Validate the AppHost builds (`dotnet build src/Hexalith.EventStore.AppHost`). Smoke: `KeycloakPersistent=true aspire run …` twice — second start reattaches to the warm Keycloak container in ~seconds. Confirm CI path unchanged (no opt-in env vars set).
- **Success criteria:**
  1. `KeycloakPersistent=true` keeps the Keycloak container alive across `aspire run` restarts; warm restart skips the cold-start.
  2. Default (`KeycloakPersistent` unset) behavior is byte-for-byte unchanged.
  3. `KEYCLOAK_TEST_REUSE=true` reuses the container across Tier 3 test runs; unset (CI) keeps cold/clean.
  4. Docs describe both switches and the realm-reset step.
  5. `TreatWarningsAsErrors` build stays green.

---

## Appendix — Checklist Disposition

- **§1 Trigger & context:** Done — dev/test loop slowed by repeated Keycloak cold-start.
- **§2 Epic impact:** N/A — no epic change; new tracking entry only.
- **§3 Artifact conflicts:** PRD N/A, Architecture N/A, UI/UX N/A; `project-context.md` non-persistent rule reconciled via default-OFF opt-in (Action-taken).
- **§4 Path forward:** Option 1 (Direct Adjustment) selected; Options 2 (rollback) and 3 (MVP review) N/A; `--optimized` image rejected as inapplicable.
- **§5/§6:** Proposal components complete; Minor→Moderate handoff to Developer agent; sprint-status updated on approval.

---

## Review Findings (code review 2026-05-25)

Adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Acceptance Auditor confirmed every §4 change item and §5 criterion is faithfully implemented with zero AC violations. Cross-layer triage dismissed 12 findings as noise/false-positive (parallelization race already closed by `DisableTestParallelization`, `bool.TryParse` trims internally so the trim asymmetry is cosmetic, file uses K&R braces + build green, persistent-container reuse + stale-realm are by-design/documented trade-offs).

### Patch

- [x] [Review][Patch] Add port-8180 concurrency caveat to docs — warn against running `KeycloakPersistent=true aspire run` and `KEYCLOAK_TEST_REUSE=true dotnet test` simultaneously; both bind host port 8180, so a second independently-named topology fails on the bound port. [`docs/guides/troubleshooting.md` Dev Fast-Start subsection]
- [x] [Review][Patch] Align realm-file path reference to the qualified `KeycloakRealms/hexalith-realm.json` (matches `troubleshooting.md` and the `Program.cs` comment). [`_bmad-output/project-context.md:86`]

### Defer

- [x] [Review][Defer] Fixtures don't restore `KeycloakPersistent` if `InitializeAsync` throws before `DisposeAsync` runs — deferred, mirrors pre-existing `EnableKeycloak` flaw [`tests/.../Fixtures/KeycloakAuthFixture.cs:51`, `tests/.../Security/AspireTopologyFixture.cs:57`]

### Outstanding verification (spec §5 success criteria — not code findings)

- §5.1 runtime warm-reattach smoke (`KeycloakPersistent=true aspire run` ×2 with Docker) not yet executed — explicitly pending per sprint-status comment.
- §5.5 `TreatWarningsAsErrors` build reported green; recommend confirming with `dotnet build src/Hexalith.EventStore.AppHost` + IntegrationTests before close.

---

## Runtime Warm-Reattach Smoke + Spike (2026-05-25, executed)

The §5.1 smoke was finally executed with Docker + the `aspire` CLI live. **It initially FAILED, and the spike that followed corrected a premise error in this proposal.**

### §5.5 — build (PASS)

`dotnet build … -c Release` on **AppHost** and **IntegrationTests**: 0 warnings, 0 errors, both before and after the fix below.

### §5.1 — warm-reattach: original implementation FAILS

`KeycloakPersistent=true aspire run` ×3 produced **three distinct containers** (`8619c7d8…`, `b9082cab…`, `dacc6f40…`), each paying a full Keycloak cold-start + realm re-import. The container *survives* an `aspire run` stop (persistence works), but the **next** `aspire run` deletes and recreates it.

**Root cause (proven by a clean two-run config diff):** DCP only reuses a persistent container when its `lifecycle-key` (a hash of the docker create spec) is byte-stable across runs. Aspire assigns Keycloak's `http`/`management` endpoints **random host ports every run**, so the `lifecycle-key` churns (`89c7af58…` → `6b960173…` → `642cbb1e…`) while password, dev cert, injected files, and Cmd are all stable. The injected `OTEL_EXPORTER_OTLP_ENDPOINT` port also varies under the `aspire run` CLI (it ignores `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`), but the endpoint env turned out **not** to be in the hash — only the container's own port bindings were. **§4.1 as written (bare `WithLifetime(ContainerLifetime.Persistent)`) does not deliver reuse and is superseded by §4.1c.**

### §4.1c — implemented fix (proxyless fixed ports)

In the `KeycloakPersistent` block of `Program.cs`, pin Keycloak's endpoints proxyless to fixed host ports so the docker bindings — and thus the `lifecycle-key` — are deterministic:

```csharp
_ = keycloak
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint("http", e => { e.Port = 8180; e.IsProxied = false; })
    .WithEndpoint("management", e => { e.Port = 8543; e.IsProxied = false; });
```

**Verification (PASS):** two consecutive `aspire run` **reuse the same container** (`dd5e1cad…`, identical `lifecycle-key` `84b0a99a…`, fixed bindings `8443→8180`, `9000→8543`); OIDC discovery returns **HTTP 200** with `issuer=https://localhost:8180/realms/hexalith`. The cold-start is now paid once.

### Fragility (demonstrated) + status

Proxyless fixed ports bind directly, so any host process on a chosen port wedges Keycloak in `Created` and stalls the whole topology. This is not hypothetical: the first management-port pick (`9180`) collided with **Logitech G Hub** (`lghub_updater.exe`) and had to be moved to `8543`. Concurrent topologies / `KEYCLOAK_TEST_REUSE=true dotnet test` + `aspire run` also conflict on `8180`/`8543`.

**Disposition:** Landed **EXPERIMENTAL**. Docs (`troubleshooting.md`, `quickstart.md`, `project-context.md`) updated to describe the pin + fragility. **dw19 stays `in-progress`** pending the follow-up: validate Tier 3 fixture reuse (`KEYCLOAK_TEST_REUSE=true dotnet test` ×2 — fixtures inherit the pin) and harden the fixed-port selection. CI path unchanged (no opt-in env vars set).
