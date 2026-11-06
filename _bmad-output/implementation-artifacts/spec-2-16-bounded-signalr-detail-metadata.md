---
title: 'Story 2.16: Bounded SignalR Detail Metadata'
type: 'bugfix'
created: '2026-11-06'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '10a335307e417ba339afeda6ad3e2a40bfe1f092'
story_key: '2-16-bounded-signalr-detail-metadata'
context:
  - _bmad-output/implementation-artifacts/epic-2-context.md
  - _bmad-output/project-context.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NFR5 needs explicit SignalR conformance at the default 16-entry and
2,048-byte boundaries, with opaque keys and no metadata values in framework logs
above Debug. The broadcaster currently attaches a raw transport exception to a
Warning event; its message, inner exceptions or data can contain metadata values.

**Approach:** Preserve the existing ordinal-prefix clipping contract and compact
default System.Text.Json UTF-8 accounting, including punctuation and escaping.
Log only the exception type for failed detail delivery. Add regressions at both
exact limits and one entry/byte over, with opaque keys, successful/clipped/failed
deliveries, and inspection of structured log state and exception objects. Reuse
the existing receiver-validation and client-receipt tests. Keep fail-open delivery,
signal-only compatibility and all public/wire APIs intact. Scope is the existing
SignalR contract only; infrastructure and deployment remain with Hexalith.Platform.

</frozen-after-approval>

## Implementation Notes

- Selection: Stories 2.14/2.15 retain coordinated migration/compatibility work;
  Story 2.16 has no new architecture or infrastructure prerequisite. Story 2.8's
  contract and Story 2.13's accepted transport disposition are delivered. The
  Story 9.1 authorization-record machinery was retired by the owner-approved
  evidence-storage simplification; no deleted record is recreated.
- The existing working-tree changes in the Epic 8 context, Story 8.6 and sprint
  tracking implement the user's deferral and are preserved. Epic 8, including
  Azure conformance, is post-MVP. MVP staging/production is on-premises Kubernetes
  at `192.168.1.30`; Platform's existing module debug/down, tuple preflight and
  staging-policy workflows own environment coordination. This story requires no
  Platform mutation or deployment.
- Investigation: `ProjectionChangedMetadataLimits.Bound` already clips in ordinal
  order; `SignalRProjectionChangedBroadcaster` uses it and logs metadata count.
  `ProjectionChangedNotificationValidatorTests` covers receiver rejection;
  `EventStoreSignalRClientTests` covers receipt logging. The existing test logger
  drops exception objects, so new regressions must inspect actual ILogger calls.
- Footprint: one broadcaster implementation, one conformance test class, this
  owning spec, refreshed Epic 2 context and the owning sprint row. No new public
  API, dependency update, persisted-data change or external side effect.
- The initial conformance run reproduced the leak: 9 selected, 7 passed, 2 failed,
  with the raw exception carrying the metadata marker. The production correction
  removes only the detail Warning event's exception argument, preserving event
  ID 1095, severity, exception type, group, result and timing fields.
- Capturing NSubstitute calls after delivery exposed pooled generated log state:
  that state had been cleared by assertion time. Added the separate, generic
  `SignalRDetailLogCapture.cs` helper to snapshot rendered text, structured fields
  and attached exceptions during `ILogger.Log`. A first surrounding run selected
  175 cases: 173 passed and 2 failed on the capture's empty formatted state; this
  was corrected in the test helper, rather than weakening the assertions.
- Initial test builds also identified test-only xUnit1030 (`ConfigureAwait(false)`),
  CS8603 (nullable assertion return), and CS0104 (two detail record names). They
  were corrected using the test lane's `ConfigureAwait(true)`, the checked type
  assertion and an explicit record alias. Production awaits retain
  `ConfigureAwait(false)`; no warning suppression was added.
- Review additions prove that an oversized middle entry terminates the prefix,
  ordinal order selects `z` before `é` at the entry ceiling, and the client
  receipt path excludes values from structured fields and exception objects as
  well as rendered text. The helper and these tests stay in Server.Tests, which
  already references the SignalR client, without changing project dependencies.

## Review Triage Log

The one-shot workflow's independent Blind Hunter review ran against the task's
changes; no other review layers were configured. All four findings were patched.

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Bind an executable validation command | low | Story 2.16 requires a bound command. Added the exact server and client commands in Verification; both projects already run in CI. |
| Last-entry byte test does not prove prefix termination | medium | A skip-and-resume mutant would retain a later small entry. Added `OversizedMiddleEntryEndsPrefixEvenWhenLaterEntryWouldFit` with three entries and only the first retained. |
| Numeric opaque keys do not distinguish linguistic sorting | medium | Both orders agree on those keys. Added `EntryClippingUsesOrdinalRatherThanLinguisticKeyOrder` with `z` and `é` straddling the entry ceiling. |
| Existing client logger drops structured state and exceptions | medium | The prior receipt test observes only formatted text. Added `ClientReceiptLogsExcludeMetadataFromRenderedAndStructuredState` using the call-time capture, preserving the delivered metadata value in the callback. |

## Verification

Source baseline: `10a335307e417ba339afeda6ad3e2a40bfe1f092`, with this story's
uncommitted changes. No commit or push was requested. Commands below use Debug
and explicit Hexalith project references; builds keep warnings as errors.

The bound NFR5 validation commands are:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:GeneratePackageOnBuild=false -m:1 --no-restore
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SignalR*' -class '*ProjectionChangeNotifierOptionsTests' -class '*ProjectionChangedNotificationValidatorTests'
dotnet build tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:GeneratePackageOnBuild=false -m:1
dotnet tests/Hexalith.EventStore.SignalR.Tests/bin/Debug/net10.0/Hexalith.EventStore.SignalR.Tests.dll
```

The server command runs this story's conformance class, existing broadcaster and
hub/recovery tests, default-option validation and receiver size validation. The
client command runs the complete client suite, including receipt redaction.
`.github/workflows/ci.yml` already includes both test projects under the shared
`ci` workflow's unit-test projects; this story changes no CI workflow.

Results on 2026-11-06:

- The initial server build (the same command without `--no-restore`) restored the
  source graph. After correcting the test compilation issues noted above, the
  final server build passed with **0 warnings, 0 errors**. Its bound test command
  passed **178/178**, including all **12** Story 2.16 conformance cases, with
  **0 errors, 0 skipped, 0 not run**.
- The client build passed with **0 warnings, 0 errors**. The complete client
  suite passed **63/63**, with **0 errors, 0 skipped, 0 not run**.
- The independent review rechecked all four patches, including the synchronous
  log-state snapshots and client receipt test, and found no remaining concrete
  issue. No finding was deferred.
- `git diff --check` passed for tracked changes; the new C# and spec files were
  also checked for trailing whitespace and final newlines.

Baseline topology commands (local repository test AppHost only):

```bash
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

Initially no AppHost was running. Start succeeded (exit 0); describe exited 0
but reported `security` (Keycloak) **Running / Unhealthy** and application
projects **Waiting**. This is not a passing live-topology baseline. Stop succeeded
(exit 0), releasing the resources before the builds. No topology repair was made.
Platform's production composition and Kubernetes at `192.168.1.30` were untouched.

CI run link: **not available**; no CI run was triggered for these uncommitted
changes. Release/package and full repository test suites were not run. This spec
records local implementation and independent review completion; the owning sprint
story is **review**, awaiting normal CI/repository acceptance. These results do not
close G-NFR-OWNERSHIP for its other owners or claim MVP/production readiness.
Concurrent external LiveSidecar fixture edits were preserved and excluded from
this story; they are outside the tested Server.Tests project graph.

## Code Review (2026-11-06)

Scope: `10a335307..101448e3d`, limited to the code this spec owns: `SignalRProjectionChangedBroadcaster.cs`,
`SignalRDetailLogCapture.cs` and `SignalRDetailMetadataConformanceTests.cs`. Two sets of files are excluded.
The Epic 8 and Story 8.6 tracking edits record the owner's deferral. The three
`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalog*` files (+81/−1) are excluded
because this spec calls them outside concurrent edits, although they landed in the same `fix: story 2.16`
commit. All four review layers completed: Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG)
and Acceptance Auditor (AA).

CI context: CI run `42219198586` (the push of `101448e3d`) failed in `ci / build-and-test`. The failure
happened in `prepare-module-build` with HXW011 ("Platform identity … has no local v<SemVer> release tag"),
before any test ran. CI on `main` has failed the same way since `3d4ba67db`, so Story 2.16's tests have no
CI result yet. CodeQL, Advisory Tests, Commitlint and the three Payload Protection workflows passed on this
commit. Integration Tests were still running.

### Review Findings

- [ ] [Review][Decision] Failed detail delivery keeps no exception diagnostics at any level — Warning 1095 now carries only `ex.GetType().Name`, the short name of the outer exception. No Debug-level event carries `ex`, so the stack trace and inner-exception chain are logged nowhere, for example a backplane failure wrapped in `HubException` or `AggregateException`. NFR5 allows values at Debug, but the frozen intent says "Log only the exception type for failed detail delivery". Options: accept as-is; add a Debug-level companion event that carries `ex`; or add value-free fields to 1095, such as the full type name and the base exception's type name. Reported by ECH+BH+AA. [src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs:185]
- [ ] [Review][Patch] Assert event 1095's structured fields by key, not just two rendered substrings — the rewritten positional call passes six strings. Swapping `result` with `exceptionType`, or `tenantId` with `groupName`, still compiles and passes both 1095 tests. Assert `ProjectionType`, `TenantId`, `GroupName`, `Result` and `ExceptionType` in the captured state, and that `ElapsedMilliseconds` is present. Reported by VG+BH. [tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRDetailMetadataConformanceTests.cs:178]
- [ ] [Review][Patch] The boundary theories discard the logger, so clip Warning 1096 is never checked at the limits — a spurious 1096 at exactly 16 entries or 2,048 bytes would pass. So would a wrong `OriginalCount`/`BoundedCount` one over the limit (17→16 entries, 16→15 for bytes). Clipping triggered by the byte limit is never checked in the logs. Assert that 1096 is absent at the limit and present with exact counts one over. Reported by BH+AA. [tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRDetailMetadataConformanceTests.cs:40]
- [ ] [Review][Patch] The prefix assertions depend on the returned dictionary's enumeration order — `sent.ToArray().ShouldBe(...)` passes only because `Bound` returns a `Dictionary` that enumerates in insertion order. The contract only fixes which entries are kept. Compare both sides in ordinal key order. Reported by BH. [tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRDetailMetadataConformanceTests.cs:50]
- [x] [Review][Defer] ASP.NET Core SignalR server and backplane logs are never captured over a real hub [tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRDetailMetadataConformanceTests.cs:189] — deferred, maybe-false (medium if true): the tests mock `IHubContext` and capture only the two EventStore logger categories. To settle it, run a real hub (TestServer + `HubConnection`), capture every category above Debug, and send a marker metadata value. If NFR5's "framework logs" means EventStore's own logs, close this as already satisfied.

Rejected:

- ECH-1 (`low`): the capture helper drops `BeginScope` state. No EventStore code on the detail path opens a scope; the only `BeginScope` in `src/` is the unrelated `ProjectionDeliveryCostDiagnostics`. Recording and asserting scopes would add capture code to guard a hypothetical future change.
- BH-2 (`low`): same scope gap as ECH-1, raised separately.
- ECH-2 (`false`): the client receipt test's `ToString()` check cannot miss nested values. Every current structured value is a string or a number. LoggerMessage renders an enumerable argument element by element into the message, which is asserted, and SYSLIB1015 forces every argument into the template. The JSON console formatter and the OTLP exporter convert other non-primitive values with `ToString()`.
- ECH-3 (`false`): the same refutation as ECH-2 applies to the broadcaster log theory.
- BH-3a (`false`): `State.ShouldNotBeEmpty()` is not vacuous. It guards the snapshot taken at call time: a snapshot taken after the pooled state is cleared is empty, which is the exact failure recorded in Implementation Notes.
- BH-4 (`low`): the client receipt test sits in Server.Tests and overlaps the existing SignalR.Tests test. Both projects run in CI, the bound server command runs it, and moving it would mean porting the helper.
- AA-7 (`low`): the new client receipt test reaches the private `OnProjectionChangedDetail` through reflection. The existing SignalR.Tests receipt test uses the same pattern, and Implementation Notes record the overlap.
- BH-5 (`false`): the byte-boundary test does prove escaping is counted. Its keys contain `"` and `é`, which default System.Text.Json escapes to the 6-byte `"` and `é`. Counting raw bytes or relaxed escaping would keep 16 entries in the 2,049-byte case, and that case would fail. The contract is defined as default System.Text.Json output.
- BH-6 (`low`): the trace channel is not exercised. Activities record only the exception-type tag today, so a listener harness would only guard a hypothetical future `AddException`.
- BH-7 (`false`): the client does not need to enforce ceilings on receipt. The contract puts clipping on broadcasters and rejection on the Dapr receiver validator. `Bound` caps at `Math.Min(configured, 16 entries / 2,048 bytes)`, so the hub cannot send detail over the ceilings.
- AA-1b (`low`): same scope gap as ECH-1.
- AA-3 (`low`): the link to the notifier options rests only on matching values. Both paths call the same `ProjectionChangedMetadataLimits.Bound`. `DaprProjectionChangeNotifierTests` covers the 2,048-byte boundary through the notifier on both transports in CI. The only remaining fix is an edit to this spec's command.
- AA-4 (rejected because the fix edits this spec): the bound server build command uses `--no-restore`, so it fails on a fresh checkout.
- AA-5 (rejected because the fix edits this spec; also `false`): the NFR5 command includes the Dapr receiver-validator tests. Running supporting regressions does not extend NFR5 to Dapr, and the frozen intent asks for them to be reused.
- AA-6 (rejected because the fix edits this spec): the records are stale. Frontmatter says `done` but the sprint row says `review`. Verification still says "uncommitted" and "No commit or push", and no commit SHA or CI link is recorded.
