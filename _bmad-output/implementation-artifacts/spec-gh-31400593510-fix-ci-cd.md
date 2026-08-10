---
title: 'Fix CI guardrail and Dapr CLI pin breaking EventStore CI/CD'
type: 'bugfix'
created: '2026-08-10'
status: 'done'
baseline_commit: 'e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two PR #340 regressions break CI/CD on current `main`:
1. [CI 31400593510](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31400593510) / [31413307759](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31413307759) — Tier-1 `Live_sidecar_workflow_targets_live_project_outside_release_gate` fails because OQ8 support capture in `.github/workflows/integration.yml` references `tests/Hexalith.EventStore.Server.Tests/` while the guardrail bans that path substring.
2. [Integration Tests 31413307050](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31413307050) — `dapr/setup-dapr` fails downloading CLI **v1.18.1** (404). That tag exists only as the **runtime**; latest CLI is **v1.18.0**. PR #340 changed `DAPR_VERSION` from `1.18.0` → `1.18.1`, and Builds `dapr-init` uses one value for both CLI install and `dapr init --runtime-version`.

**Approach:** (1) Narrow the live-lane guardrail (+ `docs/ci.md`) so OQ8 may build/`-method`-select Server.Tests support oracles without allowing a full `dotnet test` of Server.Tests as the live suite. (2) Restore an installable shared `DAPR_VERSION` (`1.18.0`) in `integration.yml` and pin that with a packaging assertion so the nonexistent CLI tag cannot return.

## Boundaries & Constraints

**Always:** Keep LiveSidecar.Tests as the only unfiltered live `dotnet test` target; keep the 21 Server.Tests OQ8 support oracles and validator/evidence identity intact; keep `release.yml` free of `integration.yml` / LiveSidecar.Tests; use an installable Dapr CLI tag for `dapr/setup-dapr` (currently `1.18.0`); work on a `fix/…` branch; validate Contracts.Tests packaging assertions in Release/package mode.

**Ask First:** Halt if runtime must remain exactly `1.18.1` while CLI stays `1.18.0` (requires separate version inputs in `references/Hexalith.Builds/Github/dapr-init`, a submodule change), if OQ8 support capture must leave `integration.yml`, if the 21 methods must move into LiveSidecar.Tests, or if production/runtime code appears necessary.

**Never:** Allow `dotnet test tests/Hexalith.EventStore.Server.Tests/` as the live lane; reintroduce `--filter "Category=LiveSidecar"`; relocate/duplicate Server.Tests oracles; change `release.yml`/`ci.yml` lane composition; modify Builds/submodules without Ask First approval; weaken upload gates merely to hide init failures; leave `DAPR_VERSION` on a CLI tag that 404s.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| OQ8 integration.yml | LiveSidecar `dotnet test` + Server.Tests build/`-method`/`--support-ctrf` | Guardrail passes | Full-suite Server.Tests live run must still fail closed |
| Forbidden full Server.Tests live run | `dotnet test tests/Hexalith.EventStore.Server.Tests/` present | Guardrail fails closed | Must keep failing |
| Category filter regression | `--filter "Category=LiveSidecar"` present | Guardrail fails closed | Must keep failing |
| Installable Dapr pin | `DAPR_VERSION: '1.18.0'` (or approved installable shared pin) | `dapr/setup-dapr` can download CLI; init proceeds | Nonexistent CLI tag (e.g. `1.18.1`) must fail packaging assertion |
| Release coupling | `release.yml` references integration/LiveSidecar | Guardrail fails closed | Must keep failing |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:621-632` -- failing live-lane fact; refine L628 and add Dapr-version pin assertion(s). Primary edit target.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:479-499` -- Server.Tests remains on deterministic CI; read-only companion.
- `.github/workflows/integration.yml:24,57-59,73-119` -- restore `DAPR_VERSION` to installable `1.18.0` (pre-#340); keep OQ8 Server.Tests support capture unless Ask First says otherwise.
- `references/Hexalith.Builds/Github/dapr-init/action.yml:4-19,95` -- single `version` feeds CLI setup and `--runtime-version`; default `1.18.0`; read-only unless Ask First approves separate CLI/runtime inputs.
- `tools/validate-oq8-platform-evidence.py:29-96,410-472` -- pins Server.Tests FQNs; read-only evidence identity.
- `docs/ci.md:50-62` -- clarify OQ8 method-selected Server.Tests support oracles + that `DAPR_VERSION` must be an installable CLI tag because Builds uses one shared value.
- `_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md` -- reuse Server.Tests oracles; planning evidence only.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/integration.yml` -- set `DAPR_VERSION` back to installable `1.18.0` (or the Ask First–approved shared pin) -- restore `dapr/setup-dapr` download.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- refine live-lane guardrail for OQ8 support capture; assert `integration.yml` pins an installable shared `DAPR_VERSION` that cannot regress to a missing CLI tag; keep Category-filter and release-coupling fails-closed.
- [x] `docs/ci.md` -- document OQ8 support-oracle exception and the shared CLI/runtime pin constraint for Integration Tests.
- [x] Focused packaging assertions -- cover I/O matrix edges (current workflow passes; full-suite/Category/missing-CLI-tag regressions fail closed).

**Acceptance Criteria:**
- Given HEAD with PR #340's OQ8 `integration.yml` step, when `Live_sidecar_workflow_targets_live_project_outside_release_gate` (refined) runs, then it passes while still forbidding full Server.Tests live runs, Category filters, and release coupling.
- Given `integration.yml` with an installable shared `DAPR_VERSION`, when packaging assertions run, then they pass; given a nonexistent CLI tag such as `1.18.1`, then they fail closed.
- Given Contracts.Tests in Release/package mode, when packaging tests for this change execute, then zero failures and warnings-as-errors build succeeds.
- Given the final diff, when inspected, then production runtime, validator pins, committed evidence hashes, and OQ8 capture command shape remain unchanged unless a recorded Ask First decision says otherwise; Builds submodule unchanged unless Ask First approved.

## Spec Change Log

## Design Notes

**Guardrail:** Story 3.1 policy is “live project owns live coverage; no Category filters,” not “never mention Server.Tests in integration.yml.” Story 4.14 intentionally builds Server.Tests and invokes pinned `-method` oracles for `--support-ctrf`. Prefer refining the assertion over moving 21 methods.

Allowed:
```yaml
dotnet build tests/Hexalith.EventStore.Server.Tests/...csproj ...
dotnet .../Hexalith.EventStore.Server.Tests.dll -method ... -ctrf ...support...
```
Forbidden:
```bash
dotnet test tests/Hexalith.EventStore.Server.Tests/
```

**Dapr pin:** `dapr/setup-dapr` installs the **CLI** from `dapr/cli` releases; `dapr init --runtime-version` installs the **runtime** from `dapr/dapr`. CLI has `v1.18.0` only in the 1.18 line; runtime has `v1.18.1`+. With one shared Builds input, EventStore must pin an installable CLI tag. Restoring `1.18.0` matches pre-#340 green Integration Tests and Builds' default. Do not invent CLI `1.18.1`.

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- expected: restore succeeds.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests.Live_sidecar_workflow_targets_live_project_outside_release_gate` -- expected: 1 passed (after refinement).
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` -- expected: packaging class zero failures.
- `rg -n "DAPR_VERSION:" .github/workflows/integration.yml` -- expected: installable shared pin (`1.18.0` unless Ask First changed it).
- `git diff --check && git status --short` -- expected: no whitespace errors; only intended workflow/guardrail/docs/spec paths differ.

## Suggested Review Order

**Dapr shared pin**

- Restore installable CLI tag used by Builds `dapr-init` for both CLI and runtime.
  [`integration.yml:24`](../../.github/workflows/integration.yml#L24)

**Live-lane packaging guardrail**

- Assert OQ8 support shape, forbid full Server.Tests suite, pin every `DAPR_VERSION`.
  [`ReleasePackageManifestTests.cs:1402`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L1402)

- Mutation theory keeps full-suite, Category filter, missing CLI tag, and release coupling fails-closed.
  [`ReleasePackageManifestTests.cs:630`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L630)

**Docs**

- Record OQ8 support-oracle exception and installable shared Dapr CLI pin rule.
  [`ci.md:56`](../../docs/ci.md#L56)
