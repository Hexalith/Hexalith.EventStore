# Story 6.6 local trusted-code artifact verification

**Status:** The approved local qualification slice is verified. Story 6.6 remains
in progress; all M1–M8 tasks and activation obligations remain open.

This report applies the [approved trusted-code amendment](../../story-6-6-trusted-code-amendment.md)
to the existing manifest candidate, supplied dependency graph and exact-file
primitives. The [capture](trusted-code-artifacts-2026-10-06/capture.json) binds
selected source inputs, compiled assemblies, the scoped diff and exact command
outputs. It is a new record; earlier sealed captures and the failed loader probe
are preserved.

## Local changes and controls

The Client XML documentation now states the reviewed deployment's obligations:
complete trusted catalog and options, authoritative pins, immutable checked
artifacts bound to execution, and loader observations with subsequent capability
loss. The local primitives compare supplied claims and currently readable file
bytes. They neither authenticate those claims nor establish that these deployment
obligations have been met.

Thirty-six additional controls bring the three affected test classes to 39 tests:

- Managed/native byte admission uses the independent SHA-256 known answer for
  `abc`; changed bytes, missing files and missing directories refuse. Original
  pre-cancellation precedes opening a missing file.
- Managed versions, native ABIs, context claims, logical identities and kinds
  must match their exact G rows. Missing edge targets, omitted edges leaving
  unreachable nodes, unknown or duplicate edges, duplicate nodes, unresolved
  roots and empty/over-count graph or root inventories refuse.
- Invalid node, edge and root scalars refuse while the fixture artifacts are
  deliberately absent, proving those checks precede file access.
- Foreign-domain rows, duplicate dependency keys, invalid kinds, more than
  65,536 rows and oversized rows refuse before service registration. Existing
  controls also retain exact fingerprint/row pin comparison, private manifest
  ownership and referenced-manifest budget refusal.
- The local resolved-graph workspace rejects an over-budget file-path claim
  against its 64 MiB guard before filesystem access.

The file fixtures contain small synthetic byte strings; they are not executed
managed/native artifacts. Version, ABI and context tests compare caller-supplied
claims to rows, rather than deriving actual runtime identities from loaded code.

Two explicit controls retain the limits of this preparation. Omitting both a
node and its incoming edge can produce a consistent partial supplied graph that
passes: G rows do not encode the authoritative edge/root inventory. A path can
also be replaced after a successful check; the subsequent check refuses, but
the first hash did not make that path immutable or bind later execution. Neither
control is reported as catalog completeness or artifact-execution qualification.

## Verification

The capture indexes each full command, exit code and retained log.

| Check | Result |
| --- | --- |
| Debug Client project build with project references | Passed; zero warnings/errors |
| Focused built Debug Client assembly, three affected classes | 39 passed; zero failures/skips |
| Final Release solution build with package references and warnings as errors | Passed; zero warnings/errors |
| Final full Release Client assembly | 1,099 passed; zero failures/skips |
| Current Dapr-only source preflight with timed mutations | Passed; all 11 prohibited mutations rejected |
| Historical approval preflight with mutations | Passed; original digest and 20 obligations/47 open follow-ups preserved |
| Deferred-work checker | Passed |
| Working-tree whitespace check and story invariants | Passed; frozen intent/baseline preserved, status in progress, eight tasks open |

The first Debug build failed with CS8506/CS8131 in a new test's tuple switch.
Explicitly typing the tuple corrected it. The failed output and successful
retry are retained. A final Release build and Client run followed XML
documentation completion, binding the final compiled slice.

The Dapr-only preflight still checks that boundary; it does not qualify this
loader policy, complete a catalog or grant readiness. Historical AD-13 approval
remains a distinct input. This slice introduces no registration migration,
worker infrastructure, manifests, startup readiness or V2 admission.

## Exact repository gate still blocked

```text
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -noLogo -class '*SecretsProtectionTests' -method '*TrackedReusableContent_DoesNotContainUsableSecrets'
```

The command exits 1 with one failure and no skips. Its only reported location
remains `_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff:2590`.
That separately owned, SHA-bound evidence capture remains intact; this slice
does not add a guard exception or change its preserved bytes. The existing
owner reconciliation is still required. The focused output is retained in
this capture separately from passing Client checks.

## Remaining qualification and authority

Actual authoritative per-domain manifests, complete reviewed roots/edges and
catalogs, exact options/serializer bounds, gateway and serving-peer pins, and
execution from the same immutable checked artifacts remain missing. Managed,
native, reflection and late-load observations and subsequent capability loss
are not integrated or qualified by this local slice. Maximum-size startup/call
cost, complete consumer routing, compatibility and Dapr recovery/fleet evidence
remain required. The failed explicit-load controls retain their negative result;
the amendment supplies no hostile-code confinement or universal before-effect
guarantee.

`test -f deploy/dapr/production-profile.yaml` still exits 1. The approved exact
production profile and two-host Dapr qualification remain absent. V2 and
proof-dependent operations stay fenced. No package, publication, deployment or
live production lane was run in this slice; the earlier package consumer results
remain scoped to their retained capture. All changed runtime-source members in
this slice are documentation-only; executable preparation is unchanged.

The [parent spec](../../spec-6-6-event-versioning-and-upcasting-implementation.md)
keeps its frozen intent and `baseline_commit`. No M1–M8 task or O-row is closed.
This slice performs no Git history, dependency or remote mutation.
