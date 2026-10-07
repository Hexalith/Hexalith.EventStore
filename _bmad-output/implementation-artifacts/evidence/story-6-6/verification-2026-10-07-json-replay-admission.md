# Story 6.6: Local JSON command-state replay admission

Date: 2026-10-07. Scope: compatible M4 preparation for private legacy JSON
capture and payload parsing. This does not complete M4 or Story 6.6.

The parent spec and its three context inputs were read before implementation.
The canonical baseline remains `1329b35e52852952ecb2c94aabf100674e9691e3`;
this run started at `02e99bfa282ace7d56a1d7d7ad5c0321f63293e3`.
Frozen intent, all M1–M8 dispositions, in-progress status and activation fences
remain unchanged. The Epic 6 context was refreshed from current repository
evidence. Concurrent architecture-review and Server security-test edits are
excluded from this run's owned implementation and review scope.

## Local implementation

`LegacyCommandReplayInput` now measures an existing JSON source before making
its private copy, binds contract wrappers without an extra snapshot document,
and shares count/readable/document/payload charges across nested and enumerable
inputs. Conservative admission retains the existing 100,000-event, 64 MiB
readable, 256 MiB accounted and 512 KiB metadata ceilings. Source/private JSON
bytes and token capacity are charged; decoded payload token tables are admitted
before `JsonDocument.Parse` in both contract-envelope and inline base64 routes.

Base64 decoding uses fixed four-byte and three-byte stack scratch, with exact
admitted output allocation and cancellation checks. Scratch and retained private
arrays are cleared on every exit; owned documents are disposed through the
reconstruction lifetime. Fixed metadata aliases are checked before binding.
Retained extension keys keep their case-sensitive dictionary grammar.

Admission matches existing payload classification: inline events use exact
lowercase `payload`, while contract envelopes use case-insensitive binding.
Private capture accepts comments and trailing commas already accepted by the
caller's source document. Application payload serializer behavior is preserved.
Metadata accounting follows default-Web escaping, including the fixed
DateTimeOffset converter's primitive representation. Bounded fixed-name lookup
avoids repeated decoding of large ignored property names; equivalent raw and
escaped ignored names share the emitted metadata ceiling.

## Independent review and controls

Four independent review layers completed before initial triage: blind hunter,
edge cases, verification gaps and acceptance. Follow-up inspection and probes
produced 15 individual findings: 14 retained findings in 11 patch groups and one
low rejected conservative-copy finding. All patch groups were applied. The
[triage record](json-replay-admission-2026-10-07/review/triage.json) retains each
finding's source, verdict and evidence; the initial owned diff and claims are
archived beside it. The large historical canonical-baseline diff was captured
separately and does not imply a complete review of earlier implementation.

Independent negative and corrected controls demonstrate:

- The original 32 MiB decoded token-dense JSON admitted 156,596,650 bytes before
  allocating a 268,435,456-byte token table. The corrected owner refuses before
  that parse allocation, at an accounted total of 268,435,370 bytes.
- Escaped whitespace base64 decode allocated 16,777,240 bytes before correction;
  the fixed-scratch control allocates 24 bytes. A thousand large ignored-name
  fixed lookups allocate zero bytes. These observations are aggregate allocation
  controls, not universal measurements of simultaneous live application memory.
- Refusal, malformed payload failure and cancellation clear retained buffers,
  invalidate private documents and preserve caller documents.
- Twenty thousand randomized valid/invalid base64 cases match framework behavior;
  30 independently serialized timestamp boundary controls and all 17 escaped
  contradictory fixed-metadata aliases pass their expected outcomes.
- The isolated mixed readable fixture contains 69,206,114 readable bytes while
  its accounted cost is 171,994,110 bytes, below 256 MiB. The original refuses;
  removing only JSON readable accumulation admits all three entries and makes
  the control fail. The independent accounted ceiling stays intact.

The final focused suite has 101 passing tests and zero failures/skips. Inclusive
metadata controls use independently serialized images at 512 KiB−1, 512 KiB and
512 KiB+1, including escaped keys/values. The execution directory retains initial
build/test failures, their corrections, final commands, output and source hashes.
Earlier broad passing runs are explicitly preliminary evidence for earlier hashes.

## Final verification

Exact commands, exit codes and output are retained in
[the execution directory](json-replay-admission-2026-10-07/).
The [final broad result record](json-replay-admission-2026-10-07/final-commands.json)
and source manifest identify the tested source, Debug assemblies, Release package
build and local package-only consumers.

- Final Debug/source Client suite: 1,284/1,284 passed.
- Final Debug/source DomainService suite: 495/495 passed.
- Final Debug/source Sample suite: 175/175 passed.
- Focused replay suite: 101/101 passed; no runner errors, failures, skips or
  not-run tests in any of these suites.
- Debug/source builds and `dotnet build Hexalith.EventStore.slnx --configuration
  Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` passed with
  zero compiler warnings/errors.
- All 14 manifest-owned packages packed locally at `999.0.0-ci-test`; isolated
  Contracts, Client and DomainService package-only consumers passed. The package
  manifest retains archive hashes. This is local package evidence and does not
  qualify the entire historical API/package consumer matrix.

Existing negative-path security/configuration exception diagnostics match the
prior passing run; their exact counts are retained separately from runner
outcomes in [runtime diagnostics](json-replay-admission-2026-10-07/runtime-diagnostics.json).
Historical approval verification and the current Dapr-only source/mutation
preflight remain separate checks and grant no activation authority. The deferred
ledger check grants no obligation closure.

## Remaining authority and qualification

The original transport parser already created the supplied JsonElement; this
owner cannot retroactively bound that ingress allocation. Arbitrary typed graphs,
application serializers/converters and state working allocations still require
admitted contracts. Caller-owned typed snapshot/tail replay remains an alias and
retains the earlier negative last-good-state evidence. This pass supplies no
authenticated source/continuation authority or complete private replay session.

Complete authoritative catalogs, serving-peer admission, transitive/framework/
native immutable execution binding, qualified observations, all consumer
integrations and Dapr/fleet/production-path evidence remain open. The required
`test -f deploy/dapr/production-profile.yaml` check exits 1; no ratified production
profile currently authorizes readiness. No M1–M8 task or O-row closes, and V2 and
proof-dependent operations remain fenced.

The initial local Aspire wait timed out after ten seconds (exit 17); its exact
commands and cleanup are retained in
[the baseline record](json-replay-admission-2026-10-07/aspire-baseline.json).
After all builds and package checks completed, the owned retry passed:
`aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
--non-interactive --format Json` exited 0;
`aspire wait eventstore --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
--timeout 45 --non-interactive` exited 0 and observed healthy status after 37.5
seconds. `aspire describe` reported EventStore Running/Healthy. The exact
[retry commands](json-replay-admission-2026-10-07/aspire-retry-commands.json) also
record successful owned shutdown and final `aspire ps` output `[]`.
This is a local health check and supplies no live evolution, recovery,
production-profile or fleet qualification.
