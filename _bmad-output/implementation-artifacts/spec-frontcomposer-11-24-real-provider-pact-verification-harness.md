---
title: 'FrontComposer Story 11.24 Real EventStore Provider Pact Verification Harness'
type: 'feature'
created: '2026-08-10'
status: 'done'
review_loop_iteration: 0
baseline_commit: '8358ffc399bdb1f1574bd049f17b3b6ebf907619'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore has no reusable real-provider verifier for external consumer pacts, deterministic provider states, exact runtime identity, cleanup, or bounded support-safe evidence. FrontComposer Story 11.24 therefore cannot truthfully expose its known provider-wire drift.

**Approach:** Add an EventStore-owned executable provider-verification project plus focused tests. It will validate external inputs, start the production gateway pipeline on Kestrel loopback port zero with only test dependency/auth/state overrides, verify each interaction separately through PactNet, and atomically emit one complete redaction-clean report before returning a fail-closed exit code.

## Boundaries & Constraints

**Always:** Use production controllers, middleware ordering, MediatR handlers, exception mapping, model binding, and JSON serialization. Record every interaction and requested state setup/teardown with bounded reason codes and timing; probe `/ready`; stop/dispose the host and confirm clean state. Bind expected and observed source SHA, package version, Builds SHA, release-inventory hash, evidence-manifest hash, decision-record hash, subject, approvals, and input hashes. Continue safe Pact playback after identity failure, but mark evidence ineligible and exit nonzero.

**Ask First:** Any production public-API/runtime change, dependency-version change, identity-authority relaxation, or provider behavior needed solely to make a consumer pact pass.

**Never:** Modify FrontComposer or its pacts/catalog; use TestServer, WebApplicationFactory, mocked HTTP responses, response shims, fabricated approvals, or a passing verdict for partial/mismatched evidence. Never retain verifier dumps, request/response bodies, authorization values, stack traces, PII, absolute paths, endpoint URLs, or port numbers in the report.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Eligible run | Valid pacts/catalog and approved identity matching runtime | All interactions run; complete report reflects actual results | Nonzero if any interaction/state/cleanup fails |
| Current Story 11.24 run | Non-authorizing `bb94d93...` record; observed checkout `8358ffc...` | All 19 interactions still run; report records drift and ineligible failed verdict | Nonzero for approval/runtime mismatch plus contract failures |
| Bad input | Missing state, malformed/oversized JSON, traversal, symlink, duplicate/extra field | No unsafe input is used; report remains bounded when output is writable | Stable failure code; nonzero |
| Timeout/cleanup failure | Readiness, request, setup, teardown, host stop, or port closure exceeds budget | Remaining results are explicit not-run/failed; cleanup is still attempted | Incomplete evidence is never accepted; nonzero |

</frozen-after-approval>

## Code Map

- `Hexalith.EventStore.slnx` -- add runner and focused test projects under `/tests/`; solution remains build-only.
- `src/Hexalith.EventStore/Program.cs:12` -- production registration, middleware, endpoint, and controller-map reference; do not change.
- `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj:29` -- reusable assembly containing production controllers/middleware/serialization composition.
- `src/Hexalith.EventStore/Controllers/CommandsController.cs:65`, `QueriesController.cs:35` -- real HTTP behavior and known response/ETag differences.
- `src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs:17` -- sanctioned command/outbox and DAPR-health overrides.
- `tests/Hexalith.EventStore.Admin.UI.E2E/PlaywrightFixture.cs:39` -- race-free `127.0.0.1:0`, address discovery, and bounded disposal precedent.
- `_bmad-output/implementation-artifacts/frontcomposer-11-24-runtime-identity-successor.md` and bound evidence -- external identity input; currently unavailable and source-mismatched.
- `../tests/Hexalith.FrontComposer.Shell.Tests/Pact/` -- read-only four-pact/19-interaction input and 19-state catalog.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.ProviderVerification/` -- implement strict CLI/input/identity validation, production Kestrel host, deterministic state coordinator and infrastructure seams, per-interaction PactNet execution, bounded report model/redaction validation, atomic write, cleanup, and exit-code policy.
- [x] `tests/Hexalith.EventStore.ProviderVerification.Tests/` -- cover positive, interaction failure, missing state, timeout, cleanup failure, malformed/oversized/path-hostile input, identity/approval mismatch, completeness, and adversarial redaction.
- [x] `Hexalith.EventStore.slnx` and `tests/Hexalith.EventStore.ProviderVerification/README.md` -- register both projects and document exact solution build, focused test executable, and external-input runner commands.
- [x] `_bmad-output/implementation-artifacts/evidence/frontcomposer-story-11-24/provider-verification/` -- run the untouched FrontComposer artifacts and retain the truthful failed report and command/exit evidence without changing predecessor identity files.
- [x] Review patch -- include each interaction description, safe host metadata, and bounded run/startup/readiness/cleanup instants and durations in the report.
- [x] Review patch -- after any fatal post-input failure, append `interaction.not-run` results and `state.setup.not-run`/`state.teardown.not-run` events for every remaining interaction while keeping completeness false; cover this path with focused tests.
- [x] Review patch -- drain isolated Pact verifier stdout/stderr directly to null streams without materializing strings.
- [x] Review patch -- enforce lowercase-only identity hashes, exact numeric GitHub issue-comment receipt URLs, explicit timestamp offsets, and strict post-freeze ordering with adversarial tests.
- [x] Review patch -- rerun the focused suite and untouched 19-interaction evidence run, then refresh command/exit evidence and report hash.
- [x] Step-4 patch -- eliminate Pact TOCTOU by hashing one byte snapshot before normalizing only per-interaction non-contract metadata.
- [x] Step-4 patch -- strictly validate manifest/Pact consumer identity, JSON value kinds, and duplicate request fields with stable input codes.
- [x] Step-4 patch -- centralize the 19 supported provider states, reject unsupported catalog states, and remove silent-success defaults from all state-aware seams.
- [x] Step-4 patch -- bind runtime identity to clean provider-affecting EventStore and Builds worktrees, exact source version suffix, package evidence, and provenance tuples; reject future receipts.
- [x] Step-4 patch -- require the exact synthetic authenticated-state header and validate tenant/domain/permission claims and requested authorization values.
- [x] Step-4 patch -- preserve accurate host-bound/stopped/port-closed facts through post-bind readiness failures and bounded cleanup.
- [x] Step-4 patch -- require ordered unique interaction indices and exactly one matching setup then teardown callback for completeness.
- [x] Step-4 patch -- reject arbitrary absolute paths and private/loopback endpoint forms, preserve the safe `bearer requirement` phrase only, and make temporary report cleanup failures stable.
- [x] Step-4 patch -- bound verifier timeout kill, wait, and null-stream drain behavior.
- [x] Step-4 patch -- move repository discovery under fail-closed reporting so valid report output still receives a minimal input-failure report outside the checkout.
- [x] Step-4 patch -- add a self-contained real-Kestrel one-interaction Pact test plus focused tests for every hardening item.
- [x] Step-4 patch -- register focused tests in EventStore release/local CI lists and refresh README, spec counts, full build, focused tests, real evidence/hash, redaction, and temporary-file scans.

**Acceptance Criteria:**
- Given external Pact, catalog, report, and identity paths, when verification runs, then real Kestrel on OS-assigned IPv4 loopback exercises production controller/serialization behavior and all hosts/state/ports are deterministically cleaned.
- Given any failed interaction, missing state, timeout, malformed input, cleanup fault, identity mismatch, redaction leak, or count/report incompleteness, when finalization completes, then the bounded JSON identifies only stable safe codes, reconciles every requested item, has `finalVerdict: failed`, and the process exits nonzero.
- Given the committed FrontComposer inputs, when the completed harness runs, then all 19 results and known wire differences are recorded truthfully while the unavailable `bb94d93...` authority versus the observed runtime mismatch remains explicit and non-authorizing.

## Spec Change Log

- 2026-08-10: Implemented the provider-verification executable, focused test matrix, solution registration, operator documentation, and truthful 19-interaction evidence run; moved to in-review.
- 2026-08-10: Root audit reopened implementation for bounded report metadata/timing, fatal-path reconciliation, null-stream verifier output, and stricter identity parsing; preserve the truthful 19-interaction behavior and external artifacts unchanged.
- 2026-08-10: Step-4 triage accepted twelve implementation patches covering TOCTOU, strict inputs/states/identity/auth, lifecycle/completeness/redaction/timeout/root handling, real-Kestrel proof, and CI registration; preserve external FrontComposer artifacts and production public API.
- 2026-08-10: Final verification observed EventStore `e60a3777c581d70b62f67173ccc2372b5b64a425` after an external fast-forward from the captured baseline; regenerated evidence binds that runtime without changing the approved `bb94d93...` authority.

## Design Notes

Run PactNet once per manifest interaction with file source plus description/state filter so failures do not suppress later results. State-aware in-memory routers/auth/validators/ETag services seed infrastructure outcomes only; PactNet still calls the real HTTP pipeline. Native verifier text is discarded immediately and reduced to enumerated result codes.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- expected: zero warnings/errors.
- `dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release -m:1` then execute its built xUnit v3 assembly -- expected: focused cases pass.
- Documented `dotnet run --project tests/Hexalith.EventStore.ProviderVerification/Hexalith.EventStore.ProviderVerification.csproj --configuration Release --no-build -- ...` -- expected for current inputs: complete failed report, 19 interaction results, clean teardown, nonzero exit.

**Observed:** solution build succeeded with zero warnings/errors; focused tests passed 73/73; the external run exited 4 with 19/19 results, 19 setup and teardown events, complete cleanup, and the expected failed/non-authorizing verdict.

## Suggested Review Order

**Execution and real hosting**

- Orchestrates fail-closed verification, complete reporting, and bounded lifecycle cleanup.
  [`ProviderVerificationApplication.cs:23`](../../tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationApplication.cs#L23)

- Composes production middleware and controllers on IPv4 loopback Kestrel port zero.
  [`ProviderVerificationHost.cs:44`](../../tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs#L44)

**Input and identity boundaries**

- Reconciles external manifests, catalogs, Pact files, states, and hashes before hosting.
  [`VerificationInputLoader.cs:16`](../../tests/Hexalith.EventStore.ProviderVerification/VerificationInputLoader.cs#L16)

- Binds approvals, package provenance, Builds identity, checkout cleanliness, and loaded runtime.
  [`RuntimeIdentityValidator.cs:20`](../../tests/Hexalith.EventStore.ProviderVerification/RuntimeIdentityValidator.cs#L20)

**Pact and provider-state execution**

- Verifies immutable Pact snapshots per interaction while discarding native mismatch output.
  [`PactInteractionVerifier.cs:14`](../../tests/Hexalith.EventStore.ProviderVerification/PactInteractionVerifier.cs#L14)

- Defines the exhaustive state vocabulary accepted by deterministic test-only seams.
  [`SupportedProviderStates.cs:3`](../../tests/Hexalith.EventStore.ProviderVerification/SupportedProviderStates.cs#L3)

- Records isolated setup and teardown transitions for every interaction.
  [`ProviderStateCoordinator.cs:5`](../../tests/Hexalith.EventStore.ProviderVerification/ProviderStateCoordinator.cs#L5)

**Evidence safety and truthfulness**

- Atomically enforces bounded, path-free, endpoint-free, credential-free JSON output.
  [`SafeReportWriter.cs:28`](../../tests/Hexalith.EventStore.ProviderVerification/SafeReportWriter.cs#L28)

- Retains the truthful failed verdict, exact identities, timings, and 19 results.
  [`provider-verification.json:2`](evidence/frontcomposer-story-11-24/provider-verification/provider-verification.json#L2)

**Tests, CI, and operation**

- Proves one matching Pact traverses real Kestrel and leaves its port closed.
  [`RealKestrelPactTests.cs:10`](../../tests/Hexalith.EventStore.ProviderVerification.Tests/RealKestrelPactTests.cs#L10)

- Runs the focused safety suite in the release-blocking CI lane.
  [`ci.yml:41`](../../.github/workflows/ci.yml#L41)

- Documents exact solution, test, and external-input verification commands.
  [`README.md:7`](../../tests/Hexalith.EventStore.ProviderVerification/README.md#L7)
