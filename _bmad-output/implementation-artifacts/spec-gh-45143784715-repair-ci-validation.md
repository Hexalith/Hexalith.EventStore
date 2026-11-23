---
title: 'Repair package checks and historical compatibility diagnostics'
type: 'bugfix'
created: '2026-11-23'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Restore EventStore's Release package checks by distinguishing producer project outputs from resolved NuGet packages and verifying the updated Builds validator. Build the historical tagged compatibility clients with the tag's SDK policy and framework. Retain the live-sidecar qualification failure location and cleanup outcome in CI.

</frozen-after-approval>

## Implementation Notes

- The failing source was `a3e8b8797b090717809844ce92da63dbc8ec2521`. Release MSBuild stamps transitive project outputs with NuGet identities and the producer version. The resolved-reference check now excludes SDK-classified project outputs. A local NuGet feed regression combines a transitively referenced project with a real package and checks both matching and mismatching package selections. It fails against the previous target.
- Builds commit `c54cbb9844e097c9cc657ec9e90af9606103755c` changed central validation for a named Platform fixture. The EventStore adapter binds the canonical companion at its selected Builds revision. The primary validator binding and mutation controls still apply.

- The Release checkpointed-package and folded-snapshot checks passed locally. All 13 isolated package consumers and the tool consumer passed. The Contracts run passed 1,718 tests with two skips for the optional package inventory.
- The published compatibility job installs both SDKs. The unchanged package templates select the current repository SDK. Tagged v3.128.2 projects select the tag's own policy and target .NET 10. The materializer built Host, Domain and Probe from each origin with zero build failures. Nine SDK-policy regression cases passed.
- The live-sidecar job now includes a diagnostic summary with the qualification and cleanup outcomes, runner source locations and counts for unreleased resources. Private runner evidence stays in the runner's temporary directory.

- The independent Platform broker profile suite already rejects the baseline digest for its manual Dapr workflow. Both that workflow and the pinned inputs match the pre-change Git tree. The guarded spec-completion command returned `STORY_IDENTITY_MISSING` for this standalone CI incident, which stays in review.

## Review Triage Log

- High, patched. Editing the actor runner or its tests would invalidate the retained Platform profile's input digests. A separate CI reporter reads their existing evidence. The original runtime qualification workflow, runner, tests and profile keep their previous bytes.
- Medium, patched. Cleanup can return false for surviving resources without raising. The summary reads the cleanup inventory and records remaining process, container and open-log counts and the runtime directory outcome.
- Medium, patched. An initialization error can precede the first result file. The reporter can recover runner frames from the specific test's TRX failure and leaves unavailable outcomes unknown. A synthetic early-startup case proves this behavior.
- Low, patched. Contributor prerequisites now identify the tagged source SDK floor, allowed patch band and local SDK inventory command.
