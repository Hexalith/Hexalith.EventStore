---
title: 'Fix CI provider paths and split Dapr CLI/runtime pins'
type: 'bugfix'
created: '2026-08-11'
status: 'done'
baseline_commit: 'd6521b308ee58771b13e32e409ace28393295c70'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Current `main` fails because provider-verification tests resolve FrontComposer outside the repository and live OQ8 uses one value for independently versioned Dapr runtime and CLI. Run `31415125153` also records an invalid historical squash title, although current commitlint is green.

**Approach:** Resolve Pact inputs through the root-declared FrontComposer submodule; extend the shared Builds bootstrap with backward-compatible separate CLI/runtime inputs; run CLI `1.18.0` with runtime `1.18.2`; and keep historical OQ8 evidence pinned to its observed `1.18.1` through mode-specific validation and freshly reviewed closure bindings.

## Boundaries & Constraints

**Always:** Preserve dirty work and immutable Story 4.14 capture. Keep old Builds callers working by falling back to `version` when no runtime input is supplied. Commitlint-validate exact candidates before the local Builds commit. Re-review and reseal every invalidated Story 4.15 artifact.

**Ask First:** Production/profile changes beyond runtime `1.18.2`; history rewrites; weaker runtime/evidence/release/commitlint gates; ruleset bypass changes; or pushes.

**Never:** Fabricate reviews, rewrite captured `1.18.1` observations, accept multiple versions per mode, duplicate Dapr bootstrap logic, remove ProviderVerification from CI, initialize nested submodules, or alter unrelated files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Provider fixtures | Root checkout with FrontComposer submodule | Pact tests load tracked inputs and reach intended assertions | Missing input fails; never search workspace siblings |
| Split Dapr bootstrap | CLI `1.18.0`, runtime `1.18.2` | CLI installs and `dapr init` reports runtime `1.18.2` | Missing runtime input falls back to legacy shared `version`; nonexistent CLI fails closed |
| Fresh versus immutable OQ8 | Fresh `1.18.2`; committed `1.18.1` | Each mode requires its exact version | Cross-mode or omitted fresh version is rejected |
| Historical commitlint | Invalid immutable `35a1eecd` plus valid current/future candidates | No history rewrite; current/future exact candidates pass | Do not weaken commitlint to make the old run green |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.ProviderVerification.Tests/{InputHardeningTests,ProviderVerificationApplicationTests}.cs` -- replace `../..` workspace assumptions with `references/Hexalith.FrontComposer`; both run `31415412092` failures share this cause.
- `references/Hexalith.Builds/Github/dapr-init/{action.yml,README.md}` -- add runtime input with legacy fallback; commit locally before updating the pointer.
- `.github/workflows/integration.yml:23-59` -- pass pinned CLI `1.18.0` and runtime `1.18.2` separately.
- `tools/validate-oq8-platform-evidence.py:667,967,1801` -- dirty user-owned validator; parameterize only fresh-capture runtime while committed Story 4.14 validation remains `1.18.1`.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:630-669,1402-1431` and `docs/ci.md:60-65` -- guard and document independent pins/action wiring and forbidden mutations.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/**` and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- rebind validator, subject, real reviews, handoff, and manifests; Story 4.14 stays immutable.
- `.github/workflows/commitlint.yml` and successful run `31415411905` -- read-only proof that `31415125153` is historical metadata, not a current workflow defect.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Github/dapr-init/{action.yml,README.md}` -- split versions with legacy fallback; validate and commit locally.
- [x] `tests/Hexalith.EventStore.ProviderVerification.Tests/{InputHardeningTests,ProviderVerificationApplicationTests}.cs` -- use the declared FrontComposer path and prove both failures.
- [x] `.github/workflows/integration.yml`, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`, `docs/ci.md` -- wire `1.18.0`/`1.18.2`; mutation-test pin drift.
- [x] `tools/validate-oq8-platform-evidence.py`, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs`, `_bmad-output/implementation-artifacts/evidence/story-4-15/**` -- enforce mode identity, rerun reviews, reseal, validate.
- [x] `references/Hexalith.Builds` and project root -- preserve dirt, update only the Builds pointer, and audit Git/commitlint; the historical run is non-actionable.

**Acceptance Criteria:**
- Given a clean Actions checkout, when deterministic ProviderVerification tests run, then all 74 pass and Pact inputs resolve beneath `references/Hexalith.FrontComposer`.
- Given Integration Tests, when Dapr initializes, then CLI `1.18.0` and runtime `1.18.2` are reported, OQ8/support passes, and upload follows validation.
- Given Story 4.14, when closure validates, then runtime remains `1.18.1`, refreshed receipts bind the validator, and no external authority is claimed.
- Given exact new commit candidates, when pinned commitlint runs, then they pass; no change attempts to rewrite or cosmetically mask historical run `31415125153`.

## Spec Change Log

## Design Notes

Builds keeps `version` as the CLI/legacy input; optional `runtime-version` controls `dapr init` and otherwise falls back. Fresh validation receives one expected runtime; committed validation retains the captured runtime.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release -m:1` then its built xUnit assembly filtered to the two failed methods and full class/project -- expected: zero failures/warnings.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` then built xUnit classes `ReleasePackageManifestTests` and `Oq8PlatformClosureTests` -- expected: current contracts and negative mutations pass.
- `python3 -m py_compile tools/validate-oq8-platform-evidence.py && python3 tools/validate-oq8-platform-evidence.py` -- expected: syntax and fully resealed closure pass.
- `git -C references/Hexalith.Builds diff --check && git diff --check` -- expected: no whitespace errors; pre-existing unrelated changes preserved.

## Suggested Review Order

**Dapr version ownership**

- Start with independent CLI/runtime pins at the workflow boundary.
  [`integration.yml:24`](../../.github/workflows/integration.yml#L24)

- Preserve legacy callers while routing the explicit runtime to `dapr init`.
  [`action.yml:9`](../../references/Hexalith.Builds/Github/dapr-init/action.yml#L9)

- Document pin ownership and immutable evidence compatibility.
  [`ci.md:60`](../../docs/ci.md#L60)

**Mode-specific evidence validation**

- Enforce exact fresh runtime identity without changing committed Story 4.14.
  [`validate-oq8-platform-evidence.py:719`](../../tools/validate-oq8-platform-evidence.py#L719)

- Reject ambiguous combinations of pre-review, capture, and support modes.
  [`validate-oq8-platform-evidence.py:1940`](../../tools/validate-oq8-platform-evidence.py#L1940)

- Prove cross-mode runtime drift and schema mutations fail closed.
  [`Oq8PlatformClosureTests.cs:47`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L47)

**Provider input resolution**

- Resolve Pact assets only through the root-declared FrontComposer submodule.
  [`ProviderVerificationApplicationTests.cs:68`](../../tests/Hexalith.EventStore.ProviderVerification.Tests/ProviderVerificationApplicationTests.cs#L68)

- Keep the operator command aligned with the tested repository layout.
  [`README.md:19`](../../tests/Hexalith.EventStore.ProviderVerification/README.md#L19)

**Regression guardrails**

- Lock workflow pins, action fallback, and validator wiring against mutation.
  [`ReleasePackageManifestTests.cs:1444`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L1444)

- Guard the documented FrontComposer root against workspace-parent regressions.
  [`InputHardeningTests.cs:15`](../../tests/Hexalith.EventStore.ProviderVerification.Tests/InputHardeningTests.cs#L15)
