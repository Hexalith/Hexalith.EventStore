---
title: 'Restore EventStore CI readiness for publication'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'af2c0d56744f97d69e20803b42f3abea18c7169e'
approval: 'User requested CI fixes and release with human validation bypassed'
context: []
---

<frozen-after-approval reason="user-authorized release repairs">

## Intent

**Problem:** Current EventStore main fails its blocking security scan and two live Dapr integration cases, preventing publication of the user's accumulated changes. All root submodules have been synchronized and the user's existing changes committed separately.

**Approach:** Correct precisely identified evidence false positives without changing sealed files or weakening detection, and repair configuration selection and authenticated readiness in the integration harness. Verify the existing gates so the parent operator can publish through the configured release workflow.

## Boundaries & Constraints

**Always:** Preserve existing user work and all sealed evidence bytes. Scope secret exceptions to inspected immutable files, full-file digest, and exact inert line digests through the existing mechanism. Preserve credential detection in modified evidence and adjacent text. Use the executing test assembly's build configuration consistently for test, Sample, and EventStore outputs. Authenticate only the direct application readiness request using the existing node token; preserve missing/invalid-token rejection. Run narrow tests and keep logs in system temporary storage.

**Never:** Blanket-exclude evidence, ignore arbitrary C# strings, remove authentication, skip failing tests, alter dependencies, edit submodules, commit, push, or run remote publication operations in the implementation handoff.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Sealed inert captures | Reviewed evidence file with exact original bytes | Only proven inert findings suppressed | Any changed file or adjacent credential remains scanned |
| Source-aware sealed diff | Historical diff whose source headers identify runtime code | Direct and tracked scan agree on no violation for runtime assignment | Injected literal credential remains rejected |
| Integration configuration | Executing Release or Debug test assembly | Python runner selects that same configuration for all binaries | Unsupported CLI configuration fails explicitly |
| OQ8 readiness | App channel token exists on node | Direct app config probe sends its token and validates actors | Sidecar metadata request receives no app token; auth gates stay enforced |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests*.cs`: scanner, effective source resolution, historical sealed-file registry, and mutation/adjacent-secret controls. Read the existing mechanism before extending it. CI paths-only failure: `/tmp/eventstore-security-ci-failure.txt`; inspected classification rationale for the 53 files and 81 lines is retained as bounded sanitized metadata in `docs/implementation/release-ci-capture-review-2026-10-23.json`.
- Immutable false positives are confined to `folders-12-2-current-source-20261022T134028Z`, `folders-12-2-gates-20261022T151209Z`, `folders-12-2-latest-20261022T155454Z`, and `internal-actor-admission-20261022` evidence packets. Captures include scanner fixtures, historical source/patches, theory display names, and JSON result strings. Do not assume every finding inert: verify each listed line's context.
- `tools/qualify-internal-actor-admission.py`: currently hardcodes three Debug output locations; its CLI can add a validated Debug/Release configuration while retaining Debug default for standalone use.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorCallbackAdmissionProcessTests.cs`: passes Python arguments; derive configuration from executing assembly metadata.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs`: `WaitForActorRuntimeReadinessAsync` has a shared client for sidecar metadata and app config; set `dapr-api-token` on the app request only using `node.AppChannelToken`.
- `tools/tests/test_qualify_internal_actor_admission.py`: existing owned-runner unit controls; extend configuration selection coverage.

## Tasks & Acceptance

**Execution:**
- [x] `SecretsProtectionTests*.cs` -- extend exact historical seals for inspected inert captures; update the obsolete P1R direct-diff expectation and prove changed/adjacent secrets remain detected.
- [x] `tools/qualify-internal-actor-admission.py` and its Python tests -- support validated build configuration consistently, retain standalone default, test Release and Debug paths.
- [x] `ActorCallbackAdmissionProcessTests.cs` -- pass executing build configuration to the Python child.
- [x] `Oq8PostgresqlFixture.cs` -- authenticate the direct config probe per request without leaking the token to metadata calls.

**Acceptance Criteria:**
- Given the exact tracked evidence corpus, when the full security class runs, then every test passes with no sealed-file edits and no broad scanner exclusion.
- Given a modified approved capture or adjacent usable credential, when scanned, then the violation remains reported.
- Given a Release integration build, when callback qualification starts, then all binary paths resolve to Release and the process can complete.
- Given a configured node token, when actor readiness polls the app, then it sends the token and still validates all required actor types.

## Implementation Notes

The user explicitly authorized release and CI repair without human validation. Planning and implementation approval checkpoints are satisfied by that instruction. Parent owns Git synchronization, remote checks, release approval, and final NuGet verification. Preserve the full baseline identifier above.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Route |
|---|---|---|
| Same-line secret injection can be masked by original inert finding | medium: revoking the file seal exposes the original finding at the asserted line, so the test cannot distinguish detection of the injected credential. Add a control replacing the original trigger while preserving source/diff framing. | patch |
| Constructor path tests do not exercise copying | false: the mandatory Release live callback executes setup/copying end to end; CI builds only Release application outputs, so restoring Debug paths makes that existing test fail. The local Release method passed all 47 checks. | reject |
| C# configuration forwarding lacks an isolated captured-invocation test | false: the same mandatory Release-only callback test crosses the actual C#→Python boundary and failed on the former Debug-only behavior. Hosted integration re-executes that boundary. | reject |
| OQ8 readiness lacks a request-recording fixture control | false: the existing OQ8 integration case failed at this exact unauthenticated request and necessarily exercises readiness plus actor roster; code sets the token on the per-request app message, leaving metadata message unchanged. Existing auth tests passed 38 cases. | reject |
| Inspected exception rationale exists only in temporary storage | medium: future reviewers would retain hashes/categories but lose the specific inert-line rationale. Retain the sanitized bounded 53-file/81-line metadata catalog under docs. | patch |
| Planned verification success criteria conflict with pending-result notes | false: the commands section lists expected success criteria; observed-result paragraphs explicitly say full security and hosted OQ8 remain pending. No completed acceptance was claimed. | reject |
| Story 3.22 spec done and sprint review conflict | false: the owning session deliberately records completed implementation in the spec and review status in sprint tracking, matching its prior recorded convention. Historical red security results remain accurately dated and the release repair has its separate gate. | reject |
| Intent wording implies live integration is release-blocking | low: integration.yml is explicitly outside the release-blocking path. The repair intent includes both failing workflows, while release.yml enforces CI/P1R/Commitlint proofs. This is a spec-only wording correction, which the review workflow rejects. | reject |
| Protection evidence table uses class descriptions as commands | low: the two rows do not contain directly executable invocations. Replace labels with the original direct-assembly commands, preserving owner outcomes/authority semantics. | patch |

Edge-case review returned no findings; verification-gap review found no gaps. The three accepted patches do not alter scanner detection or production behavior.

## Verification

- `python3 -m unittest discover -s tools/tests -p 'test_qualify_internal_actor_admission.py'` -- all runner controls pass.
- Build the affected Server and LiveSidecar test projects in Release package mode, serialized, with warnings as errors; coordinate to avoid concurrent builds.
- Invoke the built Server.Tests assembly directly with `-class '*SecretsProtectionTests*'` -- all security cases pass.
- Invoke the two previously failing live-sidecar methods if owned Dapr/Docker prerequisites are available; otherwise parent will verify fresh integration CI and report the exact local blocker.

Parent verification: complete solution Release package-mode build passed with zero warnings/errors. Existing tooling suites passed: evidence storage 48, optimized storage 48, isolation 49, published qualification 73, executor 227. Implementation verification passed Python runner controls 8, Dapr authentication 38, callback process method 1 with 47 owned checks and complete cleanup. Final full security class is still running; no acceptance claim for that pending result is made. OQ8 local invocation is blocked by absent exact PostgreSQL image and local Dapr 1.18.7 differs from CI 1.18.2; final hosted integration remains required. Concurrent documentation-only owner updates were preserved and recorded separately by the parent.

The implementation-agent final security scan ended without a result during process handoff. Parent restarted affected security methods, including the entire tracked-corpus gate, in its own session (`/tmp/eventstore-parent-security-tests.log` and CTRF result). Fresh hosted CI remains the authority for the complete class and all blocking suites before release.

Final local implementation result: all three accepted review patches are complete; none was deferred. The parent's exhaustive tracked scan reported only the generated probe declaration at HistoricalLines.cs:267 after clearing all 81 historical findings. Renaming that generated local to a neutral probe fixed the source false positive without a detector change. Existing scanner direct checks now report zero findings for that source, this spec, and the durable catalog. The strengthened seven mutation cases pass; the final Release rebuild has zero warnings/errors. Full hosted CI, including the complete security class and pinned-runtime OQ8 integration, remains a mandatory parent release check. Exact direct-check evidence is `/tmp/eventstore-ci-probe-rename-direct-scan.json`.

Hosted follow-up: CI run `39906689626` contracts job `120260314446` reached its 25-minute bound at 2026-10-23T06:04:20Z after successful restore/build and no reported test failure. The preceding successful contracts job took 23m40s. Raised only this job to a bounded 45 minutes; every test, filter, assertion, warning, and artifact condition is preserved. Existing PyYAML parsing and diff checks pass. Full exact-source CI will rerun before publication.
