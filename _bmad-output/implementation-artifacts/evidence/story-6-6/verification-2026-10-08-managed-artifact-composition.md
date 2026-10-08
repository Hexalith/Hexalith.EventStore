# Story 6.6 — retained managed dependency composition, 2026-10-08

This is a dormant M1 prerequisite under the implementation spec and trusted-code
amendment. It preserves frozen intent and canonical baseline
`1329b35e52852952ecb2c94aabf100674e9691e3`. The run started from
`b31c87fbca6280295593f6b0be8f8d4f950a01e1`; external work advanced HEAD to
`07eacb9c659e372a0c1a466e21700af4d8dffd6a`. External Story 6.1/planning changes
and the dirty root-declared Builds submodule were preserved. No Git history,
dependency pins, deployment, publication or retained-data migration was changed
by this prerequisite. All M1–M8 tasks and O01–O20 obligations stay open.

## Implemented boundary

`EventManagedArtifactSet` captures and hashes every private managed image before
creating one owned collectible context. It inspects exact retained-image
assembly definitions/references and refuses missing static references, mixed
domain/context declarations and duplicate logical or loader simple names,
including case/version collisions. It eagerly binds the private set before
returning a root. Later ordinary managed resolution uses those exact objects or
explicit declared Default-context imports. It throws a non-FileNotFound exception
for undeclared names, preventing normal Default/Resolving fallback. The native
resolution hook also throws instead of returning zero.

Shared imports check exact runtime object/context, version, G row and current
file hash under a composed hashing charge. They establish local supplied object
and file identities; they **do not** bind immutable executed framework/native
images or establish authoritative dependency/catalog completeness. This
composition is internal, unregistered and provides no readiness or activation.

An owned-context managed-load observation records sticky capability loss after
an undeclared explicit path/stream load. Tests prove a retained binding refuses
immediately, before the independent manual context scan. Observation cannot undo
that completed load or preceding effects. Arbitrary explicit loading into other
contexts, direct native loading, complete process coverage, framework execution,
64 MiB/65,536-row scale and production qualification remain unproven.

The artifact context seam rejects a pre-existing same-identity object rather than
assigning foreign bytes private-image provenance. Private images, declarations,
identity/reference workspace and import hashing have composed reservations;
private arrays clear before capacity release. Tests observe both root and
dependency buffers after disposal, original cancellation tokens and zero live
reservations after refusal. Runtime loader allocations are not a newly qualified
maximum-capacity bound.

## Verification

Exact final source hashes and archived logs are in
[commands-and-source.json](managed-artifacts-2026-10-08/commands-and-source.json).

| Command / lane | Observed result | Retained evidence |
| --- | --- | --- |
| `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | Exit 0; zero warnings/errors | [build](managed-artifacts-2026-10-08/client-build.log) |
| Built Client assembly `-class Hexalith.EventStore.Client.Tests.Events.EventManagedArtifactSetTests` | Exit 0; 16 passed; zero failed/skipped/not-run | [focused tests](managed-artifacts-2026-10-08/focused-tests.log) |
| Complete built Client assembly | Exit 0; 1,378 passed; zero failed/skipped/not-run | [full tests](managed-artifacts-2026-10-08/client-full.log) |
| `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` | Exit 0; zero warnings/errors | [Release build](managed-artifacts-2026-10-08/release-build.log) |
| `python3 scripts/verify-event-evolution-managed-artifacts.py --timeout 60 --output /tmp/story-6-6-managed-set-guards-source` | Exit 0; 16-control baseline passes; seven compiling mutations killed | [source guards](managed-artifacts-2026-10-08/source-guards/result.json) |
| Same guard script with `--configuration Release --dependency-mode packages --timeout 60 --output /tmp/story-6-6-managed-set-guards-package` | Exit 0; 16-control baseline passes; seven compiling mutations killed | [package guards](managed-artifacts-2026-10-08/package-guards/result.json) |
| `dotnet restore tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false` | Stalled at determination; interrupted; successful completion unobserved | [CI restore mode](managed-artifacts-2026-10-08/package-restore.log) |
| `actionlint .github/workflows/event-evolution-local-guards.yml` | Exit 0; no diagnostics | [workflow lint](managed-artifacts-2026-10-08/workflow-lint.log) |
| Historical preflight `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` | Exit 0; historical digest unchanged; 20 obligations/47 follow-ups open | [historical preflight](managed-artifacts-2026-10-08/historical-preflight.log) |
| Current preflight `python3 scripts/verify-event-evolution.py --mutations` | Exit 0; 22 controls; all 20 obligations open | [current preflight](managed-artifacts-2026-10-08/current-preflight.log) |
| `python3 scripts/check-deferred-work.py --json` | Exit 0; existing historical advisories | [deferred checker](managed-artifacts-2026-10-08/deferred-check.log) |

Every new mutation compiles before its named test fails with a retained Shouldly
assertion. Independent emitted assemblies demonstrate private dependency output
7 while Default retains same-identity output 99, source deletion/replacement,
missing static references before context creation, case/version ambiguity,
undeclared load-by-name, native P/Invoke refusal, explicit stream/path observation,
foreign pre-existing context contents, in-load cancellation and two-image clearing.
Build and execution share one 60-second deadline per mutation lane. The added
workflow uses existing pinned actions and root-only submodule preparation, with
Release/package dependency warm-up for the new lane. Hosted CI was not observed.
Package-mode test builds are not package publication or the complete historical
package/compiled-consumer/fleet qualification matrix.

The separate full-project package warm-up restore remained at `Determining
projects to restore...` for more than five minutes. Only its owned process was
interrupted with SIGINT; when cancellation also stalled, that same process
received SIGTERM. The combined restore/lint shell subsequently exited 0, but the
restore log ends with `Attempting to cancel the build...`: this is interrupted
verification, not successful restore evidence. No cause or product failure is
inferred. The already-passing final Release solution build and both isolated
guard modes remain separate evidence. Workflow lint was run independently after
the interrupted warm-up and passed. Hosted restore/CI remains unobserved.

Before runtime edits, Aspire startup/inspection showed all declared resources
Running/Healthy, and `aspire wait eventstore --timeout 30 --non-interactive` passed.
A duplicate startup attempt had collided with the first owned instance's
dashboard port; `aspire ps`/`aspire describe` identified the healthy first instance.
`aspire stop --non-interactive` closed it before builds, and final `aspire ps`
reported no running AppHost. No AppHost model change was made.

## Remaining work and separate activation blocker

M1 still needs complete reviewed catalogs/peer pins, immutable executed
framework/native bindings, qualified process observations and integrated admitted
routes. M2–M7 still need the actual Dapr logical evidence schema/carrier/signing
purpose and concrete control participants/owners, followed by shared reader,
replay/query/projection/subscription/operator integration and recovery evidence.
Existing design candidates have no authority and were not silently adopted.

The exact command `test -f deploy/dapr/production-profile.yaml` returned exit 1
with no output: the separately ratified production profile is absent. Production,
fleet/broker and required live Dapr acceptance evidence remain separate blockers.
V2 and unavailable proof-dependent operations remain fenced. Passing these local
controls closes no parent task or obligation and does not complete Story 6.6.
