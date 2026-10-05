# Story 6.6 — private replay ownership and last-good state

Story status remains **in-progress**. This evidence closes no M1–M8 task,
O-row or activation gate. The Dapr-only amendment governs this pass; the earlier
AD-13 approval digest remains historical and unchanged.

## Changes and observed behavior

`Client/Aggregates/PrivatePagedReplayStateSession.cs` implements an **unregistered
local** `IPagedReplayStateSession` kernel. A validated `PrivateReplayPageScope`
names one operation, owner generation and page. Random per-instance handles
cannot be consumed by another session. A monotonic 15-minute page lease, the
originating token and each method token fence borrowed calls. It captures a
successor synchronously before returning a `ValueTask`, reserves the complete
array-backed caller capacity and private copy before allocation, rejects unknown
memory capacity, and validates only the private copy through an expiring facade.
Validation receives the original operation token. One local successor is allowed;
concurrent or recursive capture cannot replace it.

Prior reads receive separately charged copies. A caller mutation cannot change
the retained canonical prior. Cancellation or expiry clears borrowed and
unsealed private work while retaining last-good prior bytes for owner recovery.
Sealing ends the borrowed handler invocation and retains private candidate bytes
for independent owner recovery. **Sealing is local preparation, not a Dapr
commit or authenticated readback.** Private owners clear their entire allocated
capacity before releasing reservations. Conservative incoming-array capacity
charges remain until owner disposal; caller-owned input arrays are never cleared
or retained by this kernel.

Timeline mode requires contiguous entries within the admitted page, at most 1,000
target events and 64 MiB of cumulative canonical timeline-state bytes. Each entry
is copied and validated privately; the terminal entry must equal the successor.
Missing, extra, out-of-order, recursive or mismatched entries refuse the page.
State-only mode accepts no timeline entries. Count-zero forms match B4a's numeric
forms: empty stream, empty target at a nonempty head, snapshot zero-tail with
head equal to target and timeline disabled, and the `long.MaxValue` sentinel.
An empty page cannot change its canonical prior. These numeric checks do not
authenticate a snapshot or source proof.

The serving legacy `AggregateReplayer` now retains canonical initial state and
the state after each successful Apply. If Apply mutates then throws, `Partial`
returns the previous canonical state and sequence, preserving the documented
legacy `Partial.StateJson` meaning. Serialization failure before Apply or while
sealing a successful Apply produces safe typed `Failed`/`Unexpected`, with no
state or timeline and no getter error text. Serialization cancellation exceptions
remain distinct. Regression cases cover the first failed Apply and a later failure with
timeline both disabled and enabled, plus initial/successor serialization errors.

This change adds serialization of initial state and every successful event in
state-only legacy replay. Timeline replay reuses those same serialized strings.
That work is required to preserve immutable last-good evidence; it is not a
projection-cost optimization. The legacy serializer still has no proved bounded
state/working-graph allocation profile. This pass does not claim B6 production
memory qualification for that route.

## Verification

Before runtime edits, the repository-local Aspire workflow started the AppHost
and inspected resource state. `aspire start --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`
succeeded; `aspire describe` showed dependent resources waiting for `security`.
The owned AppHost was stopped successfully before builds. This is a local
baseline observation, not a live event-evolution or production profile proof.

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations`
  — exit 0; historical digest
  `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`
  unchanged; 20 obligations and 47 follow-ups remain open.
- `python3 scripts/verify-event-evolution.py --mutations` — exit 0;
  current Dapr-only source boundary passed; all 11 negative mutations rejected
  with per-mutation 10-second timeouts; V2 fenced, no production qualification.
- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`
  — final exit 0, zero warnings/errors. Initial new-test builds failed on
  collection expressions targeting `ReadOnlyMemory<byte>`, a blocking test
  `ValueTask.Result`, and two CA1822 fixture diagnostics. Those cases were
  corrected without changing a gate or suppressing analyzers.
- `dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`
  — exit 0, zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -noLogo`
  — exit 0, 949 passed, zero failed/skipped/not-run (retained run: 5.808 seconds).
- `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -noLogo`
  — exit 0, 425 passed, zero failed/skipped/not-run (retained run: 10.695 seconds).
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false`
  — exit 0, zero warnings/errors (retained run: 63.80 seconds).
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -noLogo -class Hexalith.EventStore.Client.Tests.Aggregates.PrivatePagedReplayStateSessionTests -class Hexalith.EventStore.Client.Tests.Aggregates.AggregateReplayerTests`
  — exit 0, 49 passed, zero failed/skipped/not-run (retained run: 0.215 seconds).
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -noLogo`
  — exit 0, 3,715 total, 3,690 passed, zero failures, 25 existing skips
  (retained run: 46.055 seconds). Those skipped obligations are not claimed complete.
- `python3 scripts/pack-release-packages.py /tmp/story-6-6-private-replay-packages 0.0.0-ci-test`
  — exit 0; the existing script normalized its documented local CI version to
  `999.0.0-ci-test`. Packages remained local and were not published.
- `python3 scripts/validate-consumer-package-references.py /tmp/story-6-6-private-replay-packages --package Hexalith.EventStore.Client`
  — exit 0; complete package inventory validated, one isolated package-only
  Client consumer built with zero warnings/errors.
- `EVENTSTORE_PACKAGE_CONTRACT_DIR=/tmp/story-6-6-private-replay-packages dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -noLogo`
  — exit 0, 2,241 passed, zero failed/skipped/not-run (262.927 seconds).
  The standard package fixtures ran against the local inventory; their full
  consumer lane validated 13 isolated package-only consumers and one tool
  consumer. This does not establish the dedicated event-evolution
  already-compiled or deployed/fleet compatibility matrix.
- `git diff --check` — exit 0.

The [source inventory](2026-10-05-private-replay/source-sha256.json) binds the
exact owned source and test bytes. Initial inspected HEAD was
`4bd66fd472ef662e0b13aa8a7b62c1ee324d356e`; HEAD advanced externally to
`0de100da04ad3d195320548979477acda815eeb8` during this pass. External history and
unrelated changes were preserved. No branch, staging, commit, push, dependency update,
submodule operation, deployment or runtime activation was performed by this pass.

The final stable-source runs retain their full command, working directory,
exit code, log hash and source-inventory hash in [gate results](2026-10-05-private-replay/gate-results.json).
Raw output is linked there for both preflights, Debug builds/suites, Release
build/focused replay/Server tests and local package checks. The full
[Contracts output](2026-10-05-private-replay/release-contracts-suite.log) was
retained from its successful execution. Earlier preliminary tool output was
not retained as a complete raw artifact; the corrected diagnostic summaries
above do not purport to be full failure transcripts.

## Remaining local work and authority boundaries

The session is not registered or used by a serving endpoint. It does not provide
the Dapr durable page CAS/ledger, exact request/response pins, authenticated
continuation/transcript, timeline metadata codec/protection/storage, final
pointer/readback, or a qualified owner retention protocol. The page owner must
also reserve working graphs, proof, transport and caller timeline serialization
capacity in the same budget. Its caller-supplied state validator and serializer
ID do not establish authoritative manifest, implementation/options/loader
closure or readiness. There is no `/replay-state/pages` activation or fabricated
authenticated completion. Local once-only preparation is not durable retry
idempotency, and no production memory claim follows from these tests.

M1–M8 still need the actual allow-listed per-domain manifests, trusted
loader/catalog binding, serving consumer/replay/projection/subscription/query
integrations, Dapr-owned control and recovery boundaries, exact carriers and
effect receipts, safe resume/capture/operator routes, dedicated evolution
API/wire/already-compiled consumers and all affected end-state/crash matrices.
The new internal classes add no public API; the ordinary package fixture does
not close that broader compatibility matrix. These are remaining local
implementation/evidence requirements, not all external blockers.

Separately, `deploy/dapr/production-profile.yaml` remains absent and AD-26
unratified. The parent independently ran `test -f deploy/dapr/production-profile.yaml`;
it returned exit 1 with no output. Exact component capabilities, production two-host/shared-backend
proof and deployment/fleet authority remain unprovided. They cannot be
substituted by local signatures, a registry candidate, a monotonic test clock,
typed logical readback or Redis/Development health. V2 and proof-dependent
operations stay fenced. No M1–M8 checkbox or O-row is closed.
