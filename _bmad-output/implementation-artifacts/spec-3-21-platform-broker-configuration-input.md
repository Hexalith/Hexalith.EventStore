---
title: 'Story 3.21 Platform broker configuration input'
type: 'feature'
created: '2026-11-11'
status: 'done'
route: 'dispatch'
story_key: '3-21-production-dapr-runtime-pin-and-durable-broker-qualification'
review_loop_iteration: 0
baseline_commit: 5907a64eb91bbbbf74c38b7c8b2d71264d7fd49c
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md'
  - '{project-root}/references/Hexalith.Platform/docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform cannot test broker configuration remedies because EventStore's qualification lock binds the harness and component bytes. Platform's November 10 dispositions identify this input restriction as the first dependency; five guarantees remain rejected.

**Approach:** Extend the existing harness with an explicitly selected, bounded Platform configuration input and independent verification. Platform supplies configuration and owns execution; EventStore retains its assertions. This enables remediation experiments without promising fixes or copying the harness.

## Boundaries & Constraints

**Always:** Retain both historical locks, defaults and packet interpretations. Keep runtime/CLI 1.19.1, SDK/components 1.19.0 and existing immutable broker identities. Preserve all 24 vectors, predicates, publication counts, observation bounds, oldest replay, TLS, grants, scopes, durability, confirms, pressure controls, worker custody and full preservation/cleanup checks. Bind exact configuration bytes, declared Platform revision/path, current sources and sanitized effective inputs. Provenance is content verification, not authenticated owner acceptance.

**Never:** Accept arbitrary YAML, commands, paths to executable content, credentials, endpoints or unlisted settings. Change production, dependencies, shared catalogs, sealed workflows, existing authority or historical packets. Introduce a copied harness or descriptor schema. Run infrastructure or CI in this slice, select a broker, grant exceptions or close parent 3.21. Platform execution still requires its accepted scope and runner support.

## I/O & Edge-Case Matrix

| Input / state | Required behavior | Failure handling |
| --- | --- | --- |
| Explicit valid configuration | New profile/schema; all 24 vectors retained | Separate capture, technical and authority results |
| Unknown/duplicate key, invalid type/range, escaping file or forbidden setting | Reject before output creation or resource mutation | Identify field without private values |
| Missing/substituted configuration, sources or effective inputs | Independent verification rejects | Resealing cannot repair mismatch |
| Historical caller/packet or schema downgrade | Preserve historical behavior; reject relabelled new capture | No silent profile selection |
| Rejected guarantee, timeout or missing effect | Retain FAIL/uncertainty | No inferred absence, exception or production PASS |

</frozen-after-approval>

## Code Map

- `tools/qualify-dapr-brokers.py`: reuse `Run.components`, `kafka_config`, lifecycle and scenario execution.
- `tools/dapr_broker_qualification.py`: `validate_profile`, `validate_packet` and unchanged scenario evaluators.
- `tools/dapr_runtime_qualification.py`: `profile_path`, `source_input`; existing trusted historical-input archive pattern.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs`: existing explicit C# entry point.

## Tasks & Acceptance

**Execution:**
- [x] `deploy/dapr/qualification/public-identities/1.19.1-original-inputs/` — preserve affected pinned source bytes from `c94e6fffaf2d689c94bb288f151a65e10a2fea82` before editing; retain their trusted identity for historical verification.
- [x] `tools/dapr_platform_broker_configuration.py` — add strict schema-1 parsing and configuration provenance/content checks. Permit only RabbitMQ `reconnectWaitSeconds` in `{1,2,3}`, outbound timeout milliseconds in `{250,500,1000,2000,3000}`, Kafka `consumeRetryInterval` milliseconds in `{100,200,500,1000}` and broker `group.initial.rebalance.delay.ms` in `{0,100,500,1000}`. Missing tuning fields retain defaults. These bounds define experiments, not proven remedies.
- [x] `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` — add an explicit profile identity with unchanged runtime/broker identities and a complete current source inventory; preserve both prior lock files.
- [x] `tools/{qualify-dapr-brokers,dapr_broker_qualification,dapr_runtime_qualification}.py` — add `--platform-config` and explicit profile dispatch; retain schema-3 configuration bytes, provenance, rendered-input commitments and execution receipts. Verify effective inputs against requested settings plus only established lab substitutions/scenario mutations. Preserve historical schema-1/2 verification and reject downgrade or mixed identities.
- [x] `tests/Hexalith.EventStore.IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs` — forward optional configuration only with the new profile; retain original invocation and matrix behavior.
- [x] `tools/tests/test_dapr_platform_broker_configuration.py`, existing qualification tests — exercise actual producer configuration application beside positive, rejection, substitution, downgrade and historical controls, without Docker. Retain custody/preservation regressions and prove schema-3 FAIL adjudication for rejected guarantees.
- [x] `deploy/dapr/qualification/README.md`, parent spec and `deferred-work.md` — document exact input syntax, provenance limits and Platform execution handoff. Preserve all five rejected guarantees, including RabbitMQ's separate upstream requirements; correct stale mitigation suggestions by reference to Platform's dated dispositions.

**Acceptance Criteria:**
- Given a Platform configuration and the new explicit profile, when admitted and rendered, then only permitted settings differ and every scenario and security invariant remains enforced.
- Given a new capture, when independently verified, then configuration/source/effective-input substitution fails even after artifact hashes are recomputed.
- Given existing callers and retained packets, when the harness or verifier runs, then their original profile interpretation and trusted source checks remain effective.
- Given this enabling slice completes, when parent eligibility is evaluated, then 3.21 stays in progress, no broker is selected and live qualification remains unproven.

## Implementation Notes

- 2026-11-11: Implemented the approved bounded input through the existing producer and verifier. Platform input provenance is an unverified declaration bound to exact supplied bytes; schema-3 verification requires the separate original JSON. Historical locks and packet interpretations remain. No infrastructure or CI was run; Platform execution still requires accepted scope and runner support.

## Spec Change Log

- 2026-11-11: Added the explicit platform-config-v1 profile, bounded JSON, schema-3 effective-input and execution receipts, trusted historical-input archive, C# forwarding/controls and Platform handoff documentation. No frozen intent, historical lock, sealed workflow, dependency pin, shared catalog or gitlink changed.

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | medium | Kafka raw measurement and sanitized properties are separate supplied observations; the producer regenerates sanitized properties rather than deriving them from the written file. Bind sanitization to the measured raw file and retain private-value redaction. Patch. |
| B2 | high | Replacing every schema-3 sidecar executable with `/bin/false` leaves the resealed synthetic packet accepted. Bind launches to the measured Dapr executable. Patch. |
| B3 | high | Appending `--resources-path=/untrusted` leaves the resealed packet accepted because only separate-form flags are counted. Reject additional supported-flag forms. Patch. |
| B4 | high | An app-ID launch retaining control-plane flags but omitting resources/config is skipped by configuration verification and accepted. Require complete bindings for every identified sidecar launch. Patch. |
| B5 | high | Substituting `busybox:latest` for Kafka's digest-pinned launch image is accepted after resealing. Validate the immutable image in the execution binding. Patch. |
| B6 | high | Replacing the Kafka command with an echo of the expected substring is accepted. Require the actual supported startup command and properties path. Patch. |
| B7 | high | A matching Kafka launch with exit code 1 still satisfies execution coverage. Require successful container creation and its observed container identity. Patch. |
| B8 | medium | Passing the packet's own retained configuration as the independent input succeeds. Reject paths inside the packet and aliases to retained configuration. Patch. |
| B9 | high | Restoring historical profile inputs, deleting Platform files and relabelling render receipts permits a tuned capture to pass as schema 2. Reject Platform effective inputs and tuned component bytes under historical interpretations. Patch. |
| B10 | medium | Historical verification checks the new current tool inventory even when a complete original tool set is present, coupling historical packets to unrelated current Platform source drift. Admit complete historical tools before current-tool checks. Patch. |
| E1 | medium | The edge-case reproduction independently confirms B8: the retained packet copy satisfies the supposed separate-input boundary. Same root cause; patch. |
| E2 | high | The edge-case reproduction independently confirms B6: an echoed marker can accompany a different effective properties path. Same startup-matching root cause; patch. |
| E3 | high | The claims pass confirms the effective-input substitution claim fails through B6's weak Kafka startup matcher. Same root cause; patch. |
| V1 | medium | The verification layer found 128 tests and zero Platform controls under the established qualification discovery pattern; the explicit filename finds 18. Make the suite discoverable without sealed-workflow changes. Patch. |
| V2 | medium | All 18 controls passed when requested 500ms was monkeypatched to render 250ms. Assert literal rendered durations independently for every permitted timeout and retain inbound-policy controls. Patch. |

All 15 findings above were addressed through eight shared patch roots. The final
28-control suite covers actual-file Kafka sanitization, measured Dapr identity,
strict sidecar/Kafka execution, independent-input aliases, stripped downgrades,
historical-tool independence, established discovery and literal timeout values.
No review finding was deferred. The intent and acceptance criteria are unchanged.

## Design Notes

The owner's November 11 “yes” reopens remediation planning. This first slice removes the input blocker identified by Platform. It does not reopen the closed refresh or superseded ES-DIAG. Platform's unpublished descriptor-v2 design and current Redis-only composition supply no durable-broker execution capability.

## Verification

- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'`
- `python3 -B -m unittest discover -s tools/tests -p test_dapr_platform_broker_configuration.py`
- Build the changed IntegrationTests project in Debug/source mode; run its narrow non-live controls. Retain exact dependency blockers and focused fallback evidence. Do not run the explicit live broker class here.
- `git diff --check`; confirm historical locks, frozen parent intent, sealed files and gitlinks are unchanged. Record commands, counts and source revision here; run output stays outside Git.

### Implementation validation — 2026-11-11

Source baseline: `5907a64eb91bbbbf74c38b7c8b2d71264d7fd49c`; implementation was uncommitted at the initial validation. The initial implementation profile bound 90 current inputs; the final reviewed profile binds 91. Historical affected bytes
are archived from `c94e6fffaf2d689c94bb288f151a65e10a2fea82`. Two pre-existing
test-input pin mismatches required their already-pinned historical bytes from
`0a6725d51bb17a9eae28a24a53f3636bab9d48d7` and
`07f5ef132deb17d45d7209357f8ac89ef39bc557`; the archive manifest distinguishes
those from the c94e6fff snapshots. Both historical lock files remain byte-identical.

- `python3 -B -m unittest discover -s tools/tests -p 'test_dapr*qualification.py'`
  — 128 passed, including historical schema-1/2, custody, preservation and cleanup
  regressions. Repeated after the final source edits as recorded below.
- `python3 -B -m unittest discover -s tools/tests -p test_dapr_platform_broker_configuration.py`
  — 18 controls cover actual producer application/defaults, strict input rejection
  before output, substitution/resealing, downgrade/mixed identities, all 24 vectors,
  the five rejected guarantees as schema-3 FAIL, and separate authority.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false --no-restore -m:1`
  — blocked by pre-existing CS1704 in `Hexalith.Tenants.Contracts`: source
  `Hexalith.EventStore.Contracts` 4.2.0 and package 4.1.0 have the same simple name.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true --no-restore -m:1`
  — fallback blocked by CS0234, missing `Hexalith.Commons` in the existing restored
  Contracts assets. No dependency pins or project configuration were changed.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:BuildProjectReferences=false --no-restore -m:1`
  — focused source-mode build against existing dependency outputs succeeded, zero
  errors; retained pre-existing MSB3277 xUnit assert 4.0.2/4.0.3 assembly warning.
- `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll -class '*DurableBrokerConfigurationInputTests'`
  — 6 passed, zero failed/skipped. The explicit live broker class was not run.
- `git diff --check` — passed. Direct byte comparisons against the baseline
  confirm both historical locks, `ci.yml`, `integration.yml` and frozen parent
  intent are unchanged; `git diff --raw` confirms no gitlink changes. Run output
  remains outside Git. No CI execution or new live qualification is claimed.

All enabling tasks are implemented. Story 3.21 remains `in-progress`, selected
broker remains null, production eligibility remains FAIL, and all five rejected
guarantees remain unresolved pending Platform's accepted execution and the
separate upstream/owner dependencies in its 2026-11-10 dispositions.


### Final review and acceptance validation — 2026-11-11

- `python3 -B -m unittest discover -v -s tools/tests -p 'test_dapr*qualification.py'`
  — 156 passed, zero failed/skipped in 40.869 seconds: 128 historical controls
  plus all 28 Platform controls through the discovery wrapper. The separate
  `test_dapr_platform_broker_configuration.py` invocation passed 28/28 after fixes.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:BuildProjectReferences=false --no-restore -m:1`
  — passed, zero errors and one existing MSB3277 xUnit assembly warning. The full
  dependency-graph blockers recorded above remain; no dependency refresh occurred.
- `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll -class '*DurableBrokerConfigurationInputTests'`
  — 6 passed, zero failed/skipped/not run.
- `git diff --check` and direct Git-object/source SHA-256 comparisons — passed.
  The final profile verifies 91 inputs; all nine archived files match their exact
  recorded Git objects. Both historical locks, both frozen intent blocks, sealed
  workflows and gitlinks remain unchanged.

Every matrix row has executed, passing coverage in the final Python output:

| Matrix row | Executed controls |
| --- | --- |
| Valid explicit configuration, full matrix and separate authority | `testActualProducerAppliesOnlyPermittedTuningsAndPreservesDefaults`, `testPositiveSchema3PacketKeepsAll24VectorsAndSeparateAuthority` |
| Invalid, duplicate, escaping or forbidden input before output | `testUnknownDuplicateForbiddenTypesRangesAndProvenanceAreRejectedWithoutValues`, `testAdmissionRejectsEscapingAndExecutableInputsBeforeOutput` |
| Missing/substituted configuration, sources or effective inputs | `testMissingOrSubstitutedIndependentConfigurationCannotBeResealed`, `testSubstitutedProvenanceSourcesEffectiveInputsAndExecutionAreRejected`, `testIndependentConfigurationCannotComeFromPacketOrSameFileAlias` |
| Historical interpretation and downgrade rejection | `testNewProfileAndBothHistoricalLocksRetainImmutableIdentities`, `testFullStrippingRelabelingCannotAdmitTunedSchema3AsHistorical`, historical schema-1/2 suites |
| Rejected guarantee, timeout or missing effect remains FAIL | `testObservedRejectedGuaranteesStayFailAndCannotBeCountedAsPassingCapture`, historical cancellation/pressure reconciliation controls |

The approved enabling slice is complete. The parent sprint key remains
`in-progress` rather than advancing to review: its frozen intent and this slice
explicitly preserve the open parent gates. The parent's no-Git-mutation constraint
kept the completed build uncommitted. The owner subsequently requested a local
commit on 2026-11-11, authorizing recording these reviewed changes without
advancing any parent gate. The validation baseline is
`5907a64eb91bbbbf74c38b7c8b2d71264d7fd49c`; no CI run/link or new live qualification
exists for this slice. Platform execution still requires accepted scope and runner
support. All five rejected guarantees, null broker selection and FAIL production
eligibility remain.
