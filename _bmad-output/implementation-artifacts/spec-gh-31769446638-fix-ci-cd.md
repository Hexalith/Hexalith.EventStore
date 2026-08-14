---
title: 'Restore Nightly Quality LLM benchmark dry-run budget gate'
type: 'bugfix'
created: '2026-08-14'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'd31679c1855ccec94d82ce862ffb3a11917bab8e'
context:
  - '{project-root}/references/Hexalith.FrontComposer/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FrontComposer Nightly Quality run 31769446638 fails on `main` (`d31679c1855ccec94d82ce862ffb3a11917bab8e`) because `.github/benchmark-budget.json` was never committed. `budget-status` crashes before writing `artifacts/benchmark/budget.json`, then `run-benchmark` crashes on the missing artifact. The job is a no-spend dry run and should still emit candidate evidence.

**Approach:** Fail closed on spend, always emit budget and run-summary artifacts, and treat the expected dry-run exit 2 as advisory — matching `eng/release_prepublish.py` `phase_benchmark`.

## Boundaries & Constraints

**Always:** Keep `api_spend_allowed` false unless a committed budget evaluates to `available`. Write `budget.json` and `run-summary.json` even when the budget file or artifact is missing. Keep nightly as candidate-evidence-only with no provider calls. Preserve `CiGovernanceTests.NightlyBenchmarkWorkflow_UsesEmbeddedPromptContractAndReadOnlyEvidence` (embedded v1 corpus, Bench `BenchmarkHarnessTests`, `budget-blocked` + non-zero exit when spend is denied).

**Ask First:** Any positive monthly cap, `provider_cost_metadata_available: true`, real provider results, or durable baseline writes.

**Never:** Call a provider API. Invent a live spend ledger. Change `run-benchmark` success to exit 0 for `budget-blocked`. Weaken Gate 3a / release filters. Edit EventStore parent dirty `_bmad-output` story 3-13 files. Recurse into nested submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Missing budget file | `budget-status --budget` path does not exist | Writes `{status:budget-unknown, api_spend_allowed:false}`, exit 2 | No crash; no spend |
| Fail-closed committed budget | File present with cap ≤ 0, expired, missing cost metadata, or retry storm | Same `budget-unknown` / no-spend artifact, exit 2 | No crash |
| Missing budget artifact | `run-benchmark --budget-artifact` path missing | Writes 20-prompt `budget-blocked` summary, exit 2 | No crash |
| Nightly dry run | Scheduled job, no `--provider-results` | Both artifacts uploaded; job green; no API spend | `run-benchmark` exit 2 is advisory |

</frozen-after-approval>

## Code Map

- `references/Hexalith.FrontComposer/.github/workflows/nightly.yml:41-47` -- budget step already `continue-on-error: true` but `run-benchmark` is blocking; add the same advisory posture as release dry-run. Keep `--budget .github/benchmark-budget.json`.
- `references/Hexalith.FrontComposer/eng/llm_benchmark.py:19-23,75-93,96-165` -- `read_json` raises `SystemExit` on missing files. `budget_status` already treats omitted `--budget` as `budget-unknown` and writes output. `run_benchmark` already treats omitted artifact as `{}`. Missing *supplied* paths must take those same fail-closed write paths instead of crashing.
- `references/Hexalith.FrontComposer/eng/release_prepublish.py:308-330` -- reuse: omit-or-unknown budget, `tolerate_failure=True`, require summary file exists.
- `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests.Bench/Skills/SkillBenchmarkBudgetPolicy.cs:4-16` -- C# oracle: null / cap≤0 / expired / no cost metadata / retry storm → `BudgetUnknown`.
- `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs:532-566` -- extend: missing-file `budget-status` writes artifact; nightly `run-benchmark` step is advisory; keep existing 20-prompt `budget-blocked` contract.
- `references/Hexalith.FrontComposer/.github/benchmark-budget.json` -- add fail-closed placeholder (cap `0`, `provider_cost_metadata_available: false`) so the documented path exists. Do not set a positive cap.
- `references/Hexalith.FrontComposer/tests/README.md:102-103` -- note missing/fail-closed budget means no spend and still writes the artifact.
- `references/Hexalith.FrontComposer/_bmad-output/implementation-artifacts/deferred-work.md:2151-2153` -- read-only provenance for this restore; do not edit the ledger.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.FrontComposer/eng/llm_benchmark.py` -- missing `--budget` / `--budget-artifact` files use the existing omit-path fail-closed write + exit 2; do not change valid-JSON evaluation.
- [x] `references/Hexalith.FrontComposer/.github/benchmark-budget.json` -- commit a fail-closed placeholder (cap 0, cost metadata false).
- [x] `references/Hexalith.FrontComposer/.github/workflows/nightly.yml` -- keep the budget path; mark `run-benchmark` advisory (`continue-on-error: true`) so dry-run exit 2 does not fail the job.
- [x] `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs` -- lock missing-file artifact emission, fail-closed placeholder evaluation, and nightly `run-benchmark` advisory posture; keep the existing `budget-blocked` dry-run assertion.
- [x] `references/Hexalith.FrontComposer/tests/README.md` -- document fail-closed / missing-file behavior.

**Acceptance Criteria:**
- Given `.github/benchmark-budget.json` is absent, when `budget-status --budget` that path runs, then it writes `budget-unknown` with `api_spend_allowed: false` and exits 2.
- Given the committed fail-closed placeholder, when `budget-status` runs, then spend stays denied and an artifact is written.
- Given the budget artifact is absent, when `run-benchmark` runs, then it writes a 20-prompt `budget-blocked` summary and exits 2.
- Given Nightly Quality runs without provider results, when both Python steps finish, then artifacts exist, the job is green, and no provider API is invoked.
- Given spend is denied, when the existing governance dry-run assertion runs, then classification remains `budget-blocked` and the exit code is non-zero.

## Spec Change Log

## Verification

**Commands:**
- `DiffEngine_Disabled=true python3 references/Hexalith.FrontComposer/eng/llm_benchmark.py budget-status --budget /tmp/fc-missing-budget.json --output /tmp/fc-budget.json; echo $?` -- expected: exit 2 and `{"api_spend_allowed": false, "status": "budget-unknown"}`
- `DiffEngine_Disabled=true python3 references/Hexalith.FrontComposer/eng/llm_benchmark.py run-benchmark --root references/Hexalith.FrontComposer --budget-artifact /tmp/fc-missing-artifact.json --output /tmp/fc-run-summary.json; echo $?` -- expected: exit 2, `classification=budget-blocked`, `prompt_count=20`
- `DiffEngine_Disabled=true python3 references/Hexalith.FrontComposer/eng/llm_benchmark.py budget-status --budget references/Hexalith.FrontComposer/.github/benchmark-budget.json --output /tmp/fc-committed-budget.json; echo $?` -- expected: exit 2, spend denied
- `DiffEngine_Disabled=true dotnet test references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj --configuration Release --filter "FullyQualifiedName~NightlyBenchmarkWorkflow"` -- expected: all matching tests pass

## Suggested Review Order

**Fail-closed budget read**

- Missing budget paths now reuse the omit-path default instead of crashing.
  [`llm_benchmark.py:26`](../../references/Hexalith.FrontComposer/eng/llm_benchmark.py#L26)

- `budget-status` still writes `budget-unknown` and denies spend.
  [`llm_benchmark.py:81`](../../references/Hexalith.FrontComposer/eng/llm_benchmark.py#L81)

- `run-benchmark` still writes a 20-prompt `budget-blocked` summary.
  [`llm_benchmark.py:105`](../../references/Hexalith.FrontComposer/eng/llm_benchmark.py#L105)

**Nightly dry-run wiring**

- Expected dry-run exit 2 is advisory; unexpected missing evidence is not.
  [`nightly.yml:47`](../../references/Hexalith.FrontComposer/.github/workflows/nightly.yml#L47)

- Job fails closed if `run-summary.json` was never written.
  [`nightly.yml:50`](../../references/Hexalith.FrontComposer/.github/workflows/nightly.yml#L50)

- Committed placeholder keeps monthly cap at zero with no cost metadata.
  [`benchmark-budget.json:1`](../../references/Hexalith.FrontComposer/.github/benchmark-budget.json#L1)

**Governance locks**

- Tests lock advisory `run-benchmark` plus the blocking summary existence check.
  [`CiGovernanceTests.cs:558`](../../references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs#L558)

- Missing-file, placeholder, and missing-artifact rows are executed against the CLI.
  [`CiGovernanceTests.cs:580`](../../references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Governance/CiGovernanceTests.cs#L580)

- Docs state that a missing or fail-closed budget still writes no-spend evidence.
  [`README.md:103`](../../references/Hexalith.FrontComposer/tests/README.md#L103)
