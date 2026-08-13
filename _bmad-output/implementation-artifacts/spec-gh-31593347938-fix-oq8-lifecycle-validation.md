---
title: 'Fix OQ8 lifecycle validation after Story 4.8 migration'
type: 'bugfix'
created: '2026-08-12'
status: 'done'
review_loop_iteration: 6
baseline_commit: 'e20063981f6ff79c386f9050ac0f5874e3e4b05f'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run 31593347938 fails 21 Contracts tests because the OQ8 closure validator still requires `4-8-durable-admission-evidence-ledger: backlog`. The approved August migration made Story 4.8 a non-executable evidence ledger and requires its sprint-status row to remain absent.

**Approach:** Align the fail-closed lifecycle validator with the migration: accept the authoritative absence, explicitly reject any reintroduced Story 4.8 row, preserve all executable Epic 4 status checks, and rebind the corrected validator only after fresh content-bound architecture, security, and test review.

## Boundaries & Constraints

**Always:** Keep Story 4.8 absent and reject its retired key; preserve exact, unique statuses for Epic 4 and Stories 4.9–4.15; retain nine lifecycle regression cases; obtain fresh independent architecture, security, and test approval for the corrected subject before issuing receipts or handoff metadata.

**Ask First:** Changes to landed-source or capability-path identity, external authority, public OQ8 semantics, or immutable Story 4.14 evidence; inability to obtain all three approvals.

**Never:** Restore the orphan row; weaken identity, manifests, receipt binding, or bounded failures; copy old approvals to changed bytes; mutate Story 4.14 evidence; claim external authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Authoritative lifecycle | Story 4.8 row absent; required Epic 4 rows exact and unique | Candidate, final-lifecycle, and committed validators continue past lifecycle checks | Fail later only for an independent violated invariant |
| Retired row returns | Any exact Story 4.8 sprint-status row, with any value | Validator rejects the lifecycle | Bounded, deterministic lifecycle error naming the retired key |
| Executable status drifts | Required row missing, duplicated, or wrong | Existing fail-closed behavior is unchanged | Existing missing/ambiguous or drift error names the affected key |

</frozen-after-approval>

## Code Map

- `requirements-oq8.txt` -- pin the repository-supported YAML parser dependency used by the OQ8 validator.
- `.github/workflows/ci.yml` and `.github/workflows/integration.yml` -- install the pinned OQ8 validator dependency before every CI lane that executes the validator directly or through Contracts tests.
- `tools/validate-oq8-platform-evidence.py:1572-1616` -- replace the hand-written YAML parser with the pinned parser and derive active and retired-key decisions from one parsed `development_status` mapping.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs:286-352,510-569,687-719` -- retain every lifecycle and hostile-input regression, add parser-boundary regressions, and prove stale candidate/reviewer dates fail after identity resealing.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:232-240` -- authoritative current state; read-only for this fix.
- `_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md:27-35` -- read-only migration authority; the post-correction readiness report at lines 257-260 confirms exclusion.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/` -- bound execution, reviews, handoff, and manifest; freshly review and reseal in dependency order.
- `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml:37-55` -- outer identities; update last.

## Tasks & Acceptance

**Execution:**
- [x] `requirements-oq8.txt`, `.github/workflows/ci.yml`, and `.github/workflows/integration.yml` -- pin PyYAML 6.0.3 and install it before the Contracts and live-sidecar OQ8 validator lanes without modifying a shared submodule or relying on runner-image packages.
- [x] `tools/validate-oq8-platform-evidence.py` -- replace the hand-written YAML parser with the pinned parser; parse exactly one document; reject malformed YAML, duplicate keys, aliases or merges that can obscure lifecycle state, and unsupported root/`development_status` structures; enforce active exact/unique statuses from one mapping; and return the bounded retired-key diagnostic for every semantically equivalent direct Story 4.8 entry.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- retain all existing lifecycle and hostile-input cases; add nested unclosed quoted scalar, malformed explicit-key value with an unclosed flow collection, and multiple tag properties on one node; preserve bounded diagnostics and every stale-date mutation.
- [x] Story 4.15 evidence directory and `4-8-eventstore-oq8-platform-evidence.yaml` -- after implementation and tests are final, rebind candidate identities/counts, obtain three fresh independent approvals for one exact subject digest, then reseal receipts, handoff, inner manifest, and outer packet without changing Story 4.14 capture evidence or authority.

**Acceptance Criteria:**
- Given the authoritative sprint status excludes Story 4.8, when all three OQ8 lifecycle modes run, then lifecycle validation passes and downstream invariants remain enforced.
- Given any Story 4.8 status row is introduced, when lifecycle validation runs, then it fails closed with a bounded error naming the retired key.
- Given the retired key is quoted, oddly spaced, empty/null, or assigned a scalar/collection value, when it is a direct `development_status` entry, then the same bounded retired-key error is returned; the same text outside that mapping and near-match keys do not trigger it.
- Given the lifecycle file contains multiple YAML documents, a non-initial BOM, or a non-printable source character, when lifecycle validation runs, then it fails closed with bounded output; unrelated supported top-level content in the single document remains outside lifecycle scope.
- Given a nested quoted scalar is unclosed, an explicit-key value contains an unclosed flow collection, or a node carries multiple tag properties, when lifecycle validation runs, then the pinned YAML parser rejects the malformed document with bounded output.
- Given lifecycle state is hidden through a duplicate key, alias, or merge, or the parsed root/target node has an unsupported structure, when lifecycle validation runs, then it fails closed before accepting any status.
- Given a required active status is missing, duplicated, or drifted, when validation runs, then the existing specific rejection remains intact.
- Given candidate subject/execution evidence or a final reviewer receipt carries the previous review date and its surrounding hashes are resealed, when validation runs, then the corresponding date-drift guard rejects it.
- Given changed validator bytes, when committed evidence is validated, then only a freshly reviewed, consistent binding chain passes.
- Given the corrected tree, when the focused OQ8 class and complete Contracts project run in Release, then all tests pass with zero build warnings or errors.

## Spec Change Log

- 2026-08-12 review loop 1: Adversarial edge-case and verification-gap review showed the text regex accepted quoted or null-valued Story 4.8 rows and that fresh-review dates lacked mutation coverage. The Code Map, tasks, acceptance criteria, and verification now require one structure-aware `development_status` parse, YAML-shape and scoping boundaries, and resealed stale-date mutations. This avoids semantically reintroducing the retired story or allowing future weakening of freshness guards to remain green. KEEP: positive Story 4.8 absence enforcement; bounded key-specific diagnostics; all nine lifecycle regression cases; exact active-status behavior; immutable Story 4.14 evidence and landed capability identity; three independent exact-subject approvals; unchanged external-authority exclusions.
- 2026-08-12 review loop 2: Blind and edge-case review showed the dependency-free YAML subset accepted a colon without YAML separation for active rows, normalized non-YAML Unicode whitespace, parsed unrelated top-level syntax, and did not reject tagged, anchored, or aliased values consistently. Tighten the non-frozen parser design and test matrix to decode the supported YAML scalar forms exactly, require a real block-mapping separator for ordinary entries, keep the retired-key diagnostic conservative for every requested spelling, ignore document markers outside the target mapping, and fail closed on unsupported direct-entry values. This avoids treating malformed YAML as an authoritative active status or letting unsupported YAML constructs bypass lifecycle validation. KEEP: all loop-1 guarantees; duplicate top-level and deeper-indentation rejection; quoted/empty/null/collection retired-key coverage; scoped near-match and out-of-mapping positives; stale-date guards; exact-subject evidence rebinding and external-authority exclusions.
- 2026-08-12 review loop 3: Final blind, edge-case, and verification-gap review showed the strict subset still ignored additional YAML documents/BOM-prefixed declarations, admitted non-printable source characters, reflected hostile duplicate keys without a bound, rejected unrelated top-level aliases despite the documented scope, and lacked normalized-duplicate/escaped-key coverage. Require exactly one YAML document, permit a BOM only at stream start, validate YAML-printable source characters before parsing, keep attacker-controlled diagnostics generic and bounded, scope unsupported-node rejection to `development_status`, and exercise normalized duplicates plus every supported numeric escape width. This avoids retired-key bypass through a second document and diagnostic/control-character injection while preserving intentional top-level scoping. KEEP: every loop-1 and loop-2 regression; strict separator and ASCII syntax-whitespace behavior; exact active values; unsupported direct-value and explicit-key rejection; immutable Story 4.14 evidence; fresh exact-subject reviews; unchanged external-authority exclusions.
- 2026-08-12 review loop 4: Final review found that inline/indented document markers and aliased or property-wrapped `development_status` keys with inline values could evade duplicate-mapping detection; it also found that tagged anchors used only by unrelated top-level aliases were rejected and malformed top-level tokens were silently skipped. Define a bounded top-level grammar that rejects every additional document marker regardless of inline content/indentation, detects normalized target keys before considering their value shape, tracks supported anchors through node properties, permits unrelated resolved aliases, and rejects unrecognized top-level syntax. This prevents hidden retired mappings while preserving the intended outside-target scope. KEEP: all prior-loop lifecycle, scalar, printability, bounded-output, date, evidence, authority, and full-suite guarantees.
- 2026-08-12 review loop 5: Exact-subject test review showed the top-level scan still skipped an indented shadow `development_status` mapping and accepted an unclosed flow value on an unrelated root key. Detect any normalized target declaration regardless of indentation and reject it unless it is the single supported root block mapping; validate unrelated root values only within the explicitly supported scalar/empty-block subset and reject malformed or unsupported flow syntax before lifecycle acceptance. This closes the remaining hidden-retired-row and malformed-root bypasses without attempting general YAML compatibility. KEEP: all loop-1 through loop-4 cases and successful focused/full verification behavior.
- 2026-08-13 human resolution of review iteration 6: The repeated malformed-YAML escapes prove the hand-written parser design is the root cause. Replace it with the proper, pinned PyYAML 6.0.3 parser and add the smallest repository-owned dependency/bootstrap configuration needed by every CI lane. Reopen implementation, regression, and evidence resealing; add the final demonstrated nested unclosed quoted scalar, malformed explicit-key/unclosed-flow value, and multiple-tag-property cases. Do not attempt further ad-hoc parsing; if the dependency cannot be installed deterministically, stop rather than weakening validation. KEEP: Story 4.8 remains absent; every semantically equivalent direct retired entry returns the bounded retired-key diagnostic; Epic 4 and Stories 4.9-4.15 remain exact and unique; all existing lifecycle and hostile-input regressions remain; Story 4.14 evidence and all external-authority exclusions remain immutable; candidate rebinding waits for final implementation/tests; and architecture, security, and test approvals must be fresh and bind one exact subject digest.

## Design Notes

Treat Story 4.8 absence as a positive invariant over the parser-produced `development_status` mapping, not as a text-pattern omission. Use pinned PyYAML 6.0.3 through its safe composition APIs so scanning, document boundaries, quoted scalars, explicit keys, flow collections, properties, tags, anchors, and aliases are governed by the YAML parser. Require exactly one document with a mapping root and exactly one scalar `development_status` key whose value is a block mapping of scalar string keys to scalar string values. Detect semantic duplicate keys before construction; reject merge keys and every alias/merge or unsupported node shape that can obscure lifecycle state; fail closed on parser errors without exposing an unbounded exception or attacker-controlled key. Check the normalized direct keys for the retired Story 4.8 key before validating its value so every syntactically valid scalar/null/collection spelling receives the bounded retired-key diagnostic. Active and retired-key rules consume the same direct-entry set. Do not add another hand-written YAML scanner or parser fallback. Install the exact dependency explicitly in CI; a missing dependency is a bounded configuration failure, not permission to weaken validation.

## Verification

**Commands:**
- `python3 -m venv .oq8-python && .oq8-python/bin/python -m pip install --requirement requirements-oq8.txt` -- expected: creates an isolated consumer environment and installs the exact pinned YAML parser used by local validation.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.RequiredSprintStatusMustBeUnique -noColor` -- expected: 9/9 pass, including retired Story 4.8 rejection.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.PreReviewCandidateInputsPassInIsolation -noColor` -- expected: isolated candidate pre-review passes; direct final-tree pre-review is not used because its lifecycle is intentionally final.
- `.oq8-python/bin/python tools/validate-oq8-platform-evidence.py --lifecycle-mode final && .oq8-python/bin/python tools/validate-oq8-platform-evidence.py` -- expected: final lifecycle and reviewed packet pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all discovered tests pass with zero skips.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests --no-build --configuration Release --logger "trx;LogFileName=Hexalith.EventStore.Contracts.Tests.trx" --results-directory "TestResults/Hexalith.EventStore.Contracts.Tests" --collect:"XPlat Code Coverage"` -- expected: CI-parity Contracts gate passes.
- `(cd _bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747 && sha256sum --check closure-sha256.txt) && sha256sum --check _bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/validator-sha256.txt` -- expected: every sealed inner artifact and validator checksum passes.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Parser and lifecycle boundary**

- PyYAML composition validates one bounded document and one lifecycle mapping.
  [`validate-oq8-platform-evidence.py:1644`](../../tools/validate-oq8-platform-evidence.py#L1644)

- Retired Story 4.8 rejection precedes value-shape validation.
  [`validate-oq8-platform-evidence.py:1752`](../../tools/validate-oq8-platform-evidence.py#L1752)

- Dependency provenance rejects missing, wrong-version, or shadowed parsers.
  [`validate-oq8-platform-evidence.py:1611`](../../tools/validate-oq8-platform-evidence.py#L1611)

**Bootstrap and handoff**

- The exact parser dependency is repository-owned and version-pinned.
  [`requirements-oq8.txt:1`](../../requirements-oq8.txt#L1)

- Contracts CI installs the parser after initializing all root submodules.
  [`ci.yml:42`](../../.github/workflows/ci.yml#L42)

- Live evidence capture installs the same parser before validation.
  [`integration.yml:48`](../../.github/workflows/integration.yml#L48)

- Consumers receive explicit isolated installation and verification commands.
  [`source-only-handoff.json:12`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/source-only-handoff.json#L12)

**Exact-subject evidence chain**

- Twelve bindings freeze code, dependency, workflows, tests, and execution.
  [`review-subject.json:10`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/review-subject.json#L10)

- Three fresh receipts approve the same exact subject digest.
  [`architecture.json:7`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/reviews/architecture.json#L7)

- The outer packet is rebound only after the inner manifest.
  [`4-8-eventstore-oq8-platform-evidence.yaml:55`](4-8-eventstore-oq8-platform-evidence.yaml#L55)

**Regression and workflow guardrails**

- Lifecycle tests retain hostile cases and add final malformed-YAML regressions.
  [`Oq8PlatformClosureTests.cs:1067`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L1067)

- Dependency failures and clean-consumer ordering are explicitly exercised.
  [`Oq8PlatformClosureTests.cs:47`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L47)

- Workflow tests enforce exact interpreter, bootstrap order, and blocking ownership.
  [`ReleasePackageManifestTests.cs:632`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L632)
