# Sprint Change Proposal - Tenants Package-Mode And Gateway Dependency Posture

Date: 2026-07-05
Project: eventstore
Scope classification: Minor
Recommended path: Direct Adjustment
Approval: Approved by Administrator on 2026-07-05 after proposal review

## 1. Issue Summary

The Epic D action item "Resolve or explicitly track Tenants package-mode and Gateway dependency posture" remained open. The concrete evidence is already recorded in `_bmad-output/implementation-artifacts/deferred-work.md`: `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` unconditionally project-references `Hexalith.EventStore.Gateway` while Tenants otherwise uses package mode by default for EventStore dependencies.

This creates an ambiguous dependency graph: a package-mode Tenants build can still pull `Hexalith.EventStore.Gateway` from source while `Hexalith.EventStore.DomainService` and related EventStore dependencies come from packages. Since `Hexalith.EventStore.Gateway` is now part of the EventStore release manifest, this should either be resolved as a normal package dependency in Tenants or deliberately documented as a source-only exception with validation coverage.

## 2. Impact Analysis

Epic impact:

- Epic 2, Story 2.4 is affected because the Tenants external API proof must validate the corrected architecture in package-reference mode, not only in a local source-submodule graph.
- Epic 3, Story 3.5 is affected because it owns Debug source-reference and Release package-reference behavior for cross-repo Hexalith dependencies.
- Epic 3, Story 3.6 is affected because it owns manifest-driven release packaging and package metadata validation for `Hexalith.EventStore.Gateway`.

Artifact impact:

- PRD and Architecture already contain the right product and architectural rules through FR21, FR22, NFR9, NFR11, and AD-11. No PRD or architecture rewrite is required.
- `epics.md` needed specific acceptance criteria so the Gateway/Tenants posture cannot remain an implicit deferred-work note.
- `sprint-status.yaml` should mark the Epic D action item done once the explicit story tracking exists.

Technical impact:

- No EventStore source code change is required by this course correction.
- The eventual implementation owner must choose one posture for Tenants:
  - Preferred: package-reference mode consumes `Hexalith.EventStore.Gateway` as a centrally pinned package.
  - Allowed only with explicit rationale: `Gateway` remains source-only, but the exception is documented and tested so it cannot accidentally mix source Gateway with package DomainService/Client/Server dependencies.

## 3. Recommended Approach

Use Direct Adjustment. The issue does not invalidate MVP scope and does not require rollback. It is a planning specificity gap: generic package-mode requirements existed, but the Tenants/Gateway edge case was not named in the stories that will close it.

Effort: Low. The current change is planning-only; implementation is a focused project-file/package-validation task in the affected stories.

Risk: Low after Story 3.5/3.6 validation. The main risk is a package-mode Tenants build that fails or double-loads EventStore assemblies because `Gateway` is source while adjacent EventStore dependencies are packages.

Timeline impact: None to MVP sequencing. This sharpens existing Epic 2 and Epic 3 acceptance criteria rather than adding a new epic.

## 4. Detailed Change Proposals

Story: 2.4 Tenants External API Host Adoption
Section: Acceptance Criteria

OLD:

```text
**And** Tenants UI and generated API evidence must not rely on ad hoc payload fields or missing freshness metadata to claim projection-confirmed success
**And** any CI-gated DAPR/Aspire blockers are documented with exact commands and failure reasons.
```

NEW:

```text
**And** Tenants UI and generated API evidence must not rely on ad hoc payload fields or missing freshness metadata to claim projection-confirmed success
**And** the Tenants external API proof runs or records exact blockers for package-reference mode with `UseHexalithProjectReferences=false`, proving it does not depend on source-only EventStore project references or a mixed source `Hexalith.EventStore.Gateway` plus package `Hexalith.EventStore.DomainService` graph
**And** any CI-gated DAPR/Aspire blockers are documented with exact commands and failure reasons.
```

Rationale: The Tenants generated API proof is the first consumer-facing place where the mixed dependency graph can hide. Its validation must prove package mode or document the blocker.

Story: 3.5 Debug Source References And Release Package References
Section: Acceptance Criteria

OLD:

```text
**Given** project files reference external Hexalith libraries
**When** source and package modes are evaluated
**Then** each dependency has exactly one active source per mode
**And** host applications that are not library packages are not disguised as package dependencies.
```

NEW:

```text
**Given** project files reference external Hexalith libraries
**When** source and package modes are evaluated
**Then** each dependency has exactly one active source per mode
**And** host applications that are not library packages are not disguised as package dependencies.

**Given** cross-repo consumers such as Tenants depend on reusable EventStore gateway host components
**When** `UseHexalithProjectReferences=false` or Release package mode is selected
**Then** `Hexalith.EventStore.Gateway` is consumed through a centrally pinned `PackageReference` or explicitly documented as a deliberate source-only exception with validation coverage
**And** the dependency graph does not mix a source `Hexalith.EventStore.Gateway` with package-mode EventStore dependencies such as `Hexalith.EventStore.DomainService`, `Client`, `Server`, or `ServiceDefaults`.
```

Rationale: Story 3.5 is the natural owner for cross-repo package/reference mode behavior. Naming `Gateway` prevents a broad package-mode story from missing the concrete deferred-work item.

Story: 3.6 Manifest-Driven Release Packaging
Section: Acceptance Criteria

OLD:

```text
**Given** package metadata is validated
**When** generated NuGet packages are inspected
**Then** external Hexalith dependencies appear as package dependencies
**And** local source project paths do not leak into release package metadata.
```

NEW:

```text
**Given** package metadata is validated
**When** generated NuGet packages are inspected
**Then** external Hexalith dependencies appear as package dependencies
**And** local source project paths do not leak into release package metadata
**And** `Hexalith.EventStore.Gateway` package metadata carries package dependencies, not source paths, so external package-mode consumers can restore without EventStore source checkout state.
```

Rationale: `Gateway` is in `tools/release-packages.json`, so release validation should prove it behaves like a consumable package.

## 5. Change Analysis Checklist

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Epic D / D-7 review deferred-work item exposed the mixed Tenants/Gateway dependency graph. |
| 1.2 Core problem | [x] | Technical limitation / planning gap: package-mode requirements did not name `Hexalith.EventStore.Gateway` in the Tenants consumer posture. |
| 1.3 Evidence | [x] | `_bmad-output/implementation-artifacts/deferred-work.md` records the exact `Hexalith.Tenants.csproj` source-reference concern. |
| 2.1-2.5 Epic impact | [x] | Affects Stories 2.4, 3.5, and 3.6 only; no new epic required. |
| 3.1 PRD conflict | [x] | No conflict; PRD FR21/FR22 already cover package mode. |
| 3.2 Architecture conflict | [x] | No conflict; AD-11 already covers manifest-governed release and package-reference mode. |
| 3.3 UX conflict | [N/A] | No UI behavior changes. |
| 3.4 Other artifacts | [x] | `epics.md` and `sprint-status.yaml` updated; deferred-work remains as implementation evidence. |
| 4.1 Direct adjustment | [x] | Viable, low effort, low risk. |
| 4.2 Rollback | [N/A] | No completed implementation needs rollback. |
| 4.3 MVP review | [N/A] | MVP scope unchanged. |
| 4.4 Recommended path | [x] | Direct Adjustment. |
| 5.1-5.5 Proposal components | [x] | This document contains issue summary, impact, path, action plan, and handoff. |
| 6.1-6.2 Final review | [x] | Story acceptance criteria now carry the explicit tracking. |
| 6.3 Approval | [x] | Approved by Administrator on 2026-07-05 after proposal review; downstream implementation still follows the normal story approval and review path. |
| 6.4 Sprint status | [x] | Action item marked done in this correction. |
| 6.5 Handoff | [x] | Routed to Developer for Story 3.5/3.6 and Story 2.4 implementation validation. |

## 6. Implementation Handoff

Scope classification: Minor.

Route to: Developer agent for the next Story 3.5 / 3.6 / 2.4 implementation work.

Responsibilities:

- Story 3.5 implementer: decide whether Tenants consumes `Hexalith.EventStore.Gateway` as a package in package mode or carries a documented source-only exception with tests.
- Story 3.6 implementer: validate `Hexalith.EventStore.Gateway` package metadata does not leak source project paths.
- Story 2.4 implementer: validate the Tenants external API proof in package-reference mode or document exact blockers.

Success criteria:

- `UseHexalithProjectReferences=false` package-mode validation no longer leaves `Hexalith.EventStore.Gateway` as an accidental source project reference in Tenants.
- No mixed source `Gateway` plus package `DomainService`/`Client`/`Server` dependency graph remains unowned.
- Any remaining exception is explicitly documented with a validation command and blocker.
