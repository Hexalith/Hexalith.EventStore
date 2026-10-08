---
title: 'Verify image synchronization in Dapr integration-test infrastructure'
type: 'bugfix'
created: '2026-08-28'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: 'adb3a999ba26e92ec0b2abbdb992b3e58035ba2f' # historical implementation baseline
baseline_commit: '7d76df4981fb070c4d84d817bf6fc800f27d0adb' # current verification baseline
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/spec-postgres-image-governance.md'
  - 'docs/concepts/architecture-overview.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** This implementation record remains in progress even though its synchronization guard has shipped. Its tag-only description predates the completed image-governance work and conflicts with the current digest-pinned test infrastructure.

**Approach:** Verify the existing deterministic Contracts packaging guard and reconcile this record with the completed [image-governance spec](spec-postgres-image-governance.md). PostgreSQL is a backend selected for the existing Dapr integration-test profile. Application and domain persistence continue through Dapr; this task introduces no SQL, database client, provider-specific application behavior, or new storage requirement.

## Boundaries & Constraints

**Always:** Verify the existing guard identifies the workflow value inside the uniquely named `Pull PostgreSQL container image` step and the fixture/validator values through `PostgresImage` and `POSTGRES_IMAGE`. Preserve the completed image-governance contract, including its reviewed digest assertion and content-bound successor evidence. Keep backend image consistency confined to test infrastructure and preserve Dapr as the application infrastructure abstraction.

**Block If:** The existing guard fails, a declaration is ambiguous, or satisfying this task would require changing a content-bound authority. Report the actual failure before changing the scope or retained evidence.

**Never:** Change production code, dependencies, the selected image, the deferred-work ledger, the workflow, fixture, validator, existing governance tests, or retained OQ8 evidence. Add no image literal or replacement guard. The previously reviewed digest assertion remains governed by the completed image-governance spec. Only this implementation record needs editing.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Synchronized authorities | Each source exposes exactly one identical reviewed image | Existing governance test passes | No error expected |
| Image drift | Any extracted image differs from either peer | Governance test fails and identifies the differing values | Deterministic assertion failure |
| Missing or ambiguous authority | An existing mutation case removes, malforms, or duplicates a recognized declaration | Governance test fails before accepting an arbitrary match | Deterministic count assertion names the source |
| Mutable or unreviewed image | All declarations agree on an invalid or unreviewed identity | Existing digest-governance test rejects the identity | Existing reviewed contract remains enforced |

</intent-contract>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- existing, read-only synchronization and digest guard; 18 cases cover live agreement, invalid identities, malformed/missing/duplicate declarations, and drift diagnostics.
- `.github/workflows/integration.yml` -- read-only CI image authority in the named pull step.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- read-only test-runtime image authority.
- `tools/validate-oq8-platform-evidence.py` -- read-only evidence image authority.
- `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` -- existing xUnit/Shouldly build and test entry point.
- `docs/concepts/architecture-overview.md` and `docs/ci.md` -- read-only Dapr boundary and existing infrastructure image-rotation guidance.
- `_bmad-output/implementation-artifacts/spec-postgres-image-governance.md` -- completed successor requirements and retained evidence bindings; read-only.
- `_bmad-output/implementation-artifacts/spec-postgres-tag-sync-guardrail.md` -- update this stale completion record with current verification and the user's Dapr clarification.
- `_bmad-output/implementation-artifacts/deferred-work.md` and retained OQ8 evidence -- explicitly read-only.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- build the owning test project and run the existing guard, verifying agreement and every negative matrix case without editing source.
- [x] `_bmad-output/implementation-artifacts/spec-postgres-tag-sync-guardrail.md` -- record successful verification, reconcile superseded tag-only requirements, and confirm the task-scoped diff changes only this record.

**Acceptance Criteria:**
- Given the checked-in integration workflow, OQ8 fixture, and evidence validator, when `PostgreSqlImageGovernanceTests` runs, then it extracts one image from each authoritative surface and proves all three values are identical.
- Given a missing, malformed, or duplicated declaration from the existing mutation matrix, when the structural guard evaluates that source, then it fails closed with a source-specific count diagnostic instead of selecting an arbitrary value.
- Given one authority changes while either peer retains its prior image, when the structural guard runs, then it fails and reports the compared image values.
- Given the completed image-governance contract, when its invalid-identity cases run, then mutable, malformed, and unreviewed identities remain rejected.
- Given the final task-scoped diff, when reviewed, then only this implementation record changes; existing image identities, source, evidence, and the deferred-work ledger remain unchanged, and the record states that application infrastructure access remains behind Dapr.

## Spec Change Log

- 2026-10-08: Applied the user's clarification that PostgreSQL is an infrastructure concern behind Dapr. Investigation found the synchronization guard already shipped in `29de2507767dc061b923e4e6e40fbe1ea69f932e` and was strengthened by the completed image-governance work in `83b32fcfad7bb608098aebccdc15002636ffb431`. Replaced obsolete tag-only/new-test instructions with verification of the existing guard and reconciliation of this record; preserved all governed source and retained evidence.
- 2026-10-08: Completed verification of the existing 18-case governance guard and reconciled this completion record with the stronger image-governance contract. This task changed no source, image identity, dependency, deferred-work entry, or retained evidence.
- 2026-10-08: Completed review and clarified the tested declaration forms, baseline meanings, and reproducible preservation command. No task-scoped implementation finding remains and no work was deferred; concurrent Epic 6 edits remain outside this task.

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
|---|---|---|
| Blind 1: concurrent Epic 6 diff violates record-only acceptance | false | Acceptance explicitly names the task-scoped diff, and the verification record discloses preserved concurrent edits. The Epic 6 edit was made outside this task; it is excluded from task ownership. |
| Blind 2: Epic 6 context omits staged-state independence | medium | The controlling Dapr-only amendment explicitly requires readback independent of staged state, while the concurrent context summary omits that qualification. This unrelated edit is outside the user's explicit request for this image-sync spec and is preserved; rejected as out of scope. |
| Blind 3: Epic 6 lifecycle sentence lacks projection scope | low | The concurrent summary uses a broad lifecycle phrase where the canonical UX/AD-14 rule concerns projection freshness. This is an unrelated context edit outside the user's explicit request and is preserved; rejected as out of scope. |
| Blind 4: preservation evidence lacks a replayable command and omits two bound inputs | low | The 66-file snapshot exists and its comparisons passed, but it does not include the closure test or CI documentation. Rejected from implementation routing as a record-only finding; final verification also records an exact baseline comparison covering those inputs and retained evidence. |
| Blind 5: structural parsing claim exceeds tested forms | low | The guard counts recognized regex declarations; a typed Python reassignment or an unterminated YAML name quote can escape that structure check. This predates the task. Rejected from implementation routing as a record-only finding; the verification account is explicitly limited to the existing mutation matrix. |
| Blind 6: two baseline fields need interpretation | low | Both revisions are valid, but they serve different runs. Rejected from implementation routing as a record-only finding; inline comments distinguish the historical implementation baseline from this verification baseline. |
| Edge 1: unterminated YAML name quote can match | low | Confirmed against the unchanged named-step pattern, whose opening/closing quote markers are independently optional. This is a pre-existing parsing limitation; no governed source is changed. The record now describes tested mutation cases rather than general YAML validation. |

The verification-gap layer reported no gaps. Two reviewers started without prior context; the verification-gap layer reused the implementation verifier after a fresh child was rejected by the platform's agent-thread limit.

## Design Notes

The original synchronization requirement is already implemented. The completed image-governance spec subsequently added a reviewed digest assertion and content-bound evidence, so its stronger contract supersedes this record's historical tag-only and no-fourth-literal assumptions. This task preserves that existing contract. Backend-specific provisioning checks verify the selected Dapr test profile; they do not make PostgreSQL an application or domain dependency.

The guard recognizes the checked-in declaration forms and the existing 12 structural mutations. It is not a general YAML, C#, or Python parser; arbitrary alternate declaration syntax is outside this verification claim. Existing governed tests and evidence remain unchanged.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` -- expected: build succeeds using local source references.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.PostgreSqlImageGovernanceTests -noColor` -- expected: all 18 existing governance cases pass without failures or skips.
- `git diff --check -- tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs _bmad-output/implementation-artifacts/spec-postgres-tag-sync-guardrail.md` -- expected: no whitespace errors.
- `git diff --exit-code 7d76df4981fb070c4d84d817bf6fc800f27d0adb -- .github/workflows/integration.yml tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs tools/validate-oq8-platform-evidence.py tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs docs/ci.md _bmad-output/implementation-artifacts/deferred-work.md _bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml _bmad-output/implementation-artifacts/evidence/story-4-14 _bmad-output/implementation-artifacts/evidence/story-4-15 _bmad-output/implementation-artifacts/evidence/story-4-15-successors` -- expected: exit 0 and no diff in protected authorities, bound gate inputs, ledger, or retained evidence.

**Recorded results (2026-10-08):**
- The exact Debug/source-reference build above passed with 0 warnings and 0 errors.
- The exact focused class command above passed all 18 existing cases: 0 errors, 0 failures, 0 skips, and 0 not run. These cover live agreement of the uniquely named workflow pull step, fixture `PostgresImage`, and validator `POSTGRES_IMAGE`; 12 missing/malformed/duplicate declaration cases; 4 mutable/malformed/unreviewed identity cases; and drift diagnostics reporting all three compared values.
- The whitespace check passed. This task changed only this implementation record; separate concurrent workspace edits were observed and preserved. SHA-256 comparison against a pre-verification snapshot confirmed all 66 protected tracked source, governance, retained Story 4.14/4.15 evidence, and deferred-ledger files remained byte-for-byte unchanged.
- The exact baseline comparison above passed with exit 0 and no output, including the closure test and CI documentation omitted from the temporary 66-file snapshot. The snapshot used during this run is `/tmp/postgres-tag-sync-guardrail-protected-sha256.json`; the baseline command is the reproducible retained check.
- Application infrastructure access remains behind Dapr; PostgreSQL remains the selected backend for the existing integration-test profile. No production, dependency, or retained-evidence work was required, and no task-scoped work remains incomplete.
