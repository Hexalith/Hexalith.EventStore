# Story 6.6 local bounded V1 scratch-admission verification

This run completes the existing local verification prerequisite for the bounded
V1 producer's private-copy scratch boundary. Story 6.6 remains **in progress**;
all eight M1–M8 acceptance tasks and activation requirements remain open. The
producer and its public registration/profile APIs were unchanged.

The observed repository revision is
`27ac3c628db75ac6a410c88689546edd4de60ae4`, with ongoing Story 5.5 work preserved.
The parent baseline remains `1329b35e52852952ecb2c94aabf100674e9691e3`.
[Initial input hashes](bounded-v1-scratch-admission-2026-10-07/inputs.json) and
[review-patched input hashes](bounded-v1-scratch-admission-2026-10-07/review-patch-inputs.json)
identify the corresponding local source/test/build inputs. Only the new test
changed among the initial selected inputs during review. These captures do not
freeze unrelated work or confer deployment authority.

## Discriminating controls

The new `BoundedV1DomainResultProducerScratchTests` theory uses 42 and 43
one-MiB already-serialized payloads plus a tiny regular-event sentinel. Before
producer execution, the real wire admission accepts the complete maximum-size
wire image in both cases. Encoded-response capacity therefore cannot explain
the producer's 43-event refusal.

The 42-event case completes with one sentinel serializer invocation and exact,
detached serialized output. The 43-event case returns the existing `ResultLimit`
before any serializer callback. Each serialized source has a distinct byte
pattern, and both cases verify every original source digest. The admitted case
checks output digests by index to detect duplication or reordering. Successful
output, unexpected successful refusal output and test source arrays are cleared
in `finally`; wire-admission assertions also run inside that cleanup boundary.

The mutation verifier compiles copies of the actual producer, admission and
stream source with the same test/fixtures and the current Contracts project.
It imports the repository's pinned test/build/package configuration and root
targets and copies the root `.editorconfig`. Unmodified
isolated controls pass first. Removing only
`|| scratch > MaximumScratchBytes` preserves encoded-size admission: the
42-event case still passes and the 43-event case fails because the expected
`InvalidOperationException` was not thrown. The verifier checks the exact
theory identities and owning assertion, explicit exit codes, zero assembly
errors and unchanged source/configuration bytes. Each subprocess has a
60-second timeout and a command record written before launch, including timeout
or launch-failure details if execution fails. The result records the verifier
digest, Python invocation/optimization flag and both sets of managed image and
runtime descriptor hashes. Dependency hashes must match between builds; image
hashes must stay unchanged throughout each execution.
Runtime production source is never edited and temporary compilations are
removed. Fresh timestamped run directories preserve earlier output. Explicit
checks remain active under Python optimization.

## Verification

| Check | Result and capture |
| --- | --- |
| Review-patched Debug/source DomainService test build, warnings as errors | Exit 0; zero warnings/errors. [Command](bounded-v1-scratch-admission-2026-10-07/review-patch-build.json), [log](bounded-v1-scratch-admission-2026-10-07/review-patch-build.log), [built image hashes](bounded-v1-scratch-admission-2026-10-07/review-patch-build-images.json). |
| Review-patched scratch class in the actual test assembly | 2 passed; zero failures/skips/errors. [Command and before/after image hashes](bounded-v1-scratch-admission-2026-10-07/review-patch-focused.json), [XML](bounded-v1-scratch-admission-2026-10-07/review-patch-focused.xml). |
| Full Debug DomainService assembly after the reviewed build | 494 passed; zero failures/skips/errors. [Command and before/after image hashes](bounded-v1-scratch-admission-2026-10-07/review-patch-domainservice-full.json), [XML](bounded-v1-scratch-admission-2026-10-07/review-patch-domainservice-full.xml). |
| Final isolated guard control, including captured `python3 -O` invocation | Unmodified: 2 pass. Mutated: 42 passes, 43 fails in its owning assertion; expected mutant exit 1. [Result](bounded-v1-scratch-admission-2026-10-07/runs/20261007T065437632764Z/mutation-result.json). |

Reproduce the focused lane after a Debug/source build:

```sh
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -m:1 -warnaserror -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -noLogo -class Hexalith.EventStore.DomainService.Tests.BoundedV1DomainResultProducerScratchTests
python3 -O _bmad-output/implementation-artifacts/evidence/story-6-6/bounded-v1-scratch-admission-2026-10-07/verify-scratch-mutation.py
```

The initial actual-assembly and isolated runs' commands, logs and XML remain in
the capture directory. The reviewed build's managed images and runtime
descriptors stayed unchanged through both actual-assembly test runs. The final
verifier adds fresh run directories and optimization-safe checks; its exact
digest is in the final mutation result. This is one targeted guard removal,
not exhaustive mutation or allocation qualification. Exact-capacity and finer
accounting controls remain a deferred M8 verification refinement.

The following Aspire facts are uncaptured session tool observations, separate
from the persisted test evidence. Before test edits, `aspire start --isolated` started the explicitly selected
EventStore AppHost, and `aspire describe` inspected its resource state. Resources
were running/healthy except the finished Tenants executable. The owned AppHost
was stopped through the explicitly selected `aspire stop` before builds. This
Development observation grants no live event-evolution, fleet or production
qualification. No AppHost source, dependency, package version or Git history
was changed.

## Remaining dependency and release scope

The next bounded-router replacement still requires actual per-domain payload
types, once-only serializer declarations, exact legacy aliases/formats,
immutable options/identities and measured payload/internal token/scratch bounds.
No application registrations were found in the inspected EventStore source or
samples or allowed ordinary Platform inputs. The optional profile API and test
fixtures do not supply authoritative application declarations. The compatibility
fallback therefore remains unchanged.

Complete authoritative catalogs, immutable checked-artifact execution binding,
qualified loader observations, Dapr control/recovery capability, all consumer
integrations, compatibility and two-host fleet evidence remain open. The
separately ratified production profile is still required. This test-only run
does not re-run the recorded broader Server gate or retire its prior secret
content-guard blocker. V2 and unavailable proof-dependent operations stay fenced.

Story 6.5 and its four children are already done, with the October 4 owner
approval receipt preserved. No renewed Story 6.5 approval is needed. The requested
`_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` is absent;
an accepted release record still needs completed release gates and actual
accepted release/package identities with supporting evidence. No accepted release
record, publication or deployment is fabricated by this local control.

All changes remain in EventStore. Folders Story 4.19 stays queued. The focused
one-shot review and its disposition are recorded in the
[subordinate execution spec](../../spec-bounded-v1-producer-scratch-admission.md).
