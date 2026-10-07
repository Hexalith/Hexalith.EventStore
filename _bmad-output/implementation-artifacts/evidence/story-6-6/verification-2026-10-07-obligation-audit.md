# Story 6.6: Current-amendment obligation audit — 2026-10-07

This local M8 prerequisite re-evaluates all 20 historical O-rows under the
controlling Dapr-only and trusted-code amendments. It supplies traceability and
open-gate accounting. It supplies no implementation qualification, closure,
production authority, consumer activation or V2 admission.

## Changed behavior and retained boundaries

`6-6-obligation-audit.json` binds the historical approved normative digest,
each historical obligation row, and both current amendment files. It records a
current requirement, evidence boundary, owner and blocking gate for each O-row.
O-10/O-13/O-14/O-17/O-18/O-20 replace withdrawn provider requirements with
qualified Dapr capabilities and logical readback, retaining compatible invariants.

O-01 retains the exact V05–V08 `batch-member-root:` plus ScopeOpHash and hashed
`aggregate-operation-result:` key derivations, including the K02/K07 key/hash
exclusions. Those requirements are compatible with Dapr. O-10 explicitly retains
the eight-envelope fair queue, original reservation accounts/amounts, unchanged
mismatched attach, atomic counter/charge/grant/refund/parking/erasure accounting,
and the prohibitions on paired queues and cross-counter moves. An unsupported
complete atomic participant set keeps its dependent control unavailable.

The existing CI `scripts/verify-event-evolution.py --mutations` command now checks
the audit without adding a release lane. Byte-preserving reads prevent newline
normalization from masking changed reviewed inputs. Historical approval remains
historical; current traceability does not manufacture the earlier approval for an
amended design. Every O-row remains open. Frozen intent, canonical baseline,
parent `in-progress` status, all M1–M8 dispositions and activation fences remain
unchanged. Existing PRD/memlog edits and sealed historical inputs were preserved.

## Executed checks

- Baseline Aspire lifecycle: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`,
  `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`,
  and `aspire wait eventstore --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --timeout 20`
  passed. Resources were healthy. The owned AppHost was stopped with `aspire stop`
  using the same explicit AppHost and non-interactive arguments. This is a local
  baseline observation, not a production qualification run.
- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations`
  passed unchanged against historical digest
  `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`;
  its 20 obligations and 47 implementation follow-ups remain open.
- `python3 scripts/verify-event-evolution.py --mutations` passed on final source.
  All 22 process-private negative mutations were rejected at their expected
  boundary, with a ten-second timeout for each child process.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`
  passed with zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Events.EventEvolutionObligationAuditTests -class Hexalith.EventStore.Contracts.Tests.Events.EventEvolutionEvidenceTests`
  passed 13/13 on the final reviewed source, with zero failures/errors/skips.
  Controls exercised actual missing amendment files, exact LF-to-CRLF changes,
  eight malformed root/nested schema shapes, all current policy mutations, and
  unchanged legacy replay/projection wire evidence. Temporary-tree positive
  controls admitted unrelated owner files and simulated changed submodule Git
  metadata without a whole-tree inventory or Git ancestry query.
- `python3 scripts/pack-release-packages.py /tmp/story-6-6-obligation-audit-packages 999.0.0-ci-test`
  packed all 14 manifest-owned local CI packages. Their consumer checks passed
  13 isolated package-only consumers and one isolated CLI tool consumer. No
  package was published.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false`
  passed with zero warnings/errors after the missing-file/schema controls and
  O-01/O-10 review corrections were in the final source.
- `python3 scripts/check-deferred-work.py --json` returned exit 0 with 406 existing
  legacy advisory diagnostics. No deferred-work record was altered.
- `git diff --check` passed.

## Full Contracts gate and retained blocker

The full Contracts CI lane supplied
`EVENTSTORE_PACKAGE_CONTRACT_DIR=/tmp/story-6-6-obligation-audit-packages` and used
the tracked exclusion for `Category=HeavyweightContainerPublish` (two tests):

```sh
EVENTSTORE_PACKAGE_CONTRACT_DIR=/tmp/story-6-6-obligation-audit-packages dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -notrait 'Category=HeavyweightContainerPublish' -result-ctrf /tmp/story-6-6-obligation-audit-final-contracts-results.json
```

The final-source lane returned exit 1: 2,270 passed, one failed, zero errors/skips
and zero not-run tests, from 2,271 selected tests. Its result is retained in
`obligation-audit-2026-10-07/verification-summary.json`.
The broad gate failed on
`ContractsPackageDependencyTests.SharedConsumerAuthorityValidatorPassesForEveryTrackedMsBuildSurfaceAsync`.
The shared package-authority validator reports five consumer `PackageVersion
Include` declarations in the unchanged sealed file
`evidence/6-1-p1r-31150-published-run/preflight/package-observation/Directory.Packages.props`:
Client, Contracts, DomainService, Server and ServiceDefaults. The fixture and
blocking validator were preserved. This audit neither qualifies nor repairs
that separate Story 6.1 evidence/governance boundary.

## Retained evidence and remaining work

- `obligation-audit-2026-10-07/current-preflight.json`: exact final-source current
  gate output, historical/current input identities, twenty open obligations and
  timeout-bounded mutation refusals.
- `obligation-audit-2026-10-07/verification-summary.json`: source hashes for the
  gate, audit and tests; focused and full regression results; final-source Release
  build and local package evidence; exact full-run command and retained log hashes.
- `obligation-audit-2026-10-07/release-build.stdout.txt` and
  `focused-tests.stdout.txt`: exact final-source build and focused runner output.
- `obligation-audit-2026-10-07/contracts-tests.stdout.txt` and
  `contracts-results.ctrf.json`: exact final-source full Contracts runner output
  and complete machine-readable results, including the retained sealed-fixture
  failure. Their SHA-256 digests are recorded in the summary.

The audit checks declared traceability and gate state, not semantic correctness
of an implemented provider/consumer protocol. Authoritative complete domain
catalogs and serving-peer pins, immutable execution, typed snapshot serializers,
shared production consumer integration, Dapr recovery/fleet matrices and the
separately ratified exact AD-26 production profile remain required. Missing
provider-proof-dependent operations remain fenced. No O-row or M1–M8 task closes.
