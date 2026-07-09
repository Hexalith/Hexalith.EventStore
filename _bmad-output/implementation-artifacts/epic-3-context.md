# Epic 3 Context: Release And Repository Reliability

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 3 makes EventStore releases reproducible and repository operations predictable. It removes live DAPR sidecar flakiness from the deterministic release gate while preserving live coverage, aligns all root-declared Hexalith submodules and tooling paths under `references/`, separates Debug source-reference behavior from Release package-reference behavior, keeps Aspire topology naming stable for operators, and constrains package publication to a reviewable EventStore manifest. The epic also moves CI/CD security policy toward thin module callers of shared Hexalith.Builds workflows and adds a local smoke preflight so generated API/DAPR/Aspire validation failures can be classified as environment, topology, generated API, or state-evidence failures.

## Stories

- Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate
- Story 3.2: Harden DAPR ETag Timeout For Integration Conditions
- Story 3.3: References-Based Submodule Layout
- Story 3.4: Aspire Security Resource Naming
- Story 3.5: Debug Source References And Release Package References
- Story 3.6: Manifest-Driven Release Packaging
- Story 3.7: Shared CI/CD Security Gates And Supply-Chain Backlog
- Story 3.8: Generated API DAPR/Aspire Smoke Preflight

## Requirements & Constraints

Release-gate tests must be deterministic. Tests that require a live `daprd` sidecar belong outside the per-push release gate, carry a live-sidecar category, and run in a dedicated integration lane with sidecar readiness, actor warm-up, and visible non-publishing failures.

`DaprETagService` must keep its production default actor request timeout unless a caller supplies an explicit override. Test and integration paths may use the override to tolerate cold-start latency, but genuine production actor failures must not be hidden by a weakened fail-open path.

Root-declared submodules must live under `references/`. Solution files, MSBuild properties, generated API reference documentation, Aspire metadata, LLM instructions, and repository scans must resolve Hexalith module paths through that layout. Nested submodules must not be initialized or required.

The Aspire identity-provider resource must be named `security` while preserving Keycloak as the implementation. Realm import, ports, dependencies, authentication behavior, fixture token logic, and endpoint resolution must continue to work through the service-role name.

Cross-repo Hexalith dependencies must be selected by build intent. Debug builds may use source project references when root-declared source exists and the mode is enabled; Release builds must default to centrally pinned package references. Validation must rerun restore after dependency-mode changes so stale project-reference assets cannot leak into package-mode builds.

Release packaging must be manifest-driven. Only the EventStore packages declared in `tools/release-packages.json` may be built, packed, and published by release tooling. Submodule packages and unexpected package families must fail validation, and NuGet metadata must expose package dependencies rather than local source paths.

EventStore CI/CD workflows must reuse shared Hexalith.Builds security gates through thin caller workflows. Shared Hexalith.Builds workflow/action references intentionally use `@main`; third-party action SHA pinning remains enforced by the shared workflows. NuGet API-key based publishing remains documented until Trusted Publishing, attestations, SBOM, and provenance hardening are implemented as follow-up work.

Generated API smoke evidence must be support-safe and must distinguish environment blockers from product defects. Smoke output must not expose tokens, JWTs, connection strings, private addresses, raw payloads, or stack traces, and status-only responses are insufficient when persisted/read-model state evidence is required.

## Technical Decisions

Release behavior is manifest-governed. `tools/release-packages.json` is the release inventory, package-reference mode is the default for release validation, source project references require explicit `UseHexalithProjectReferences=true`, and EventStore release jobs must not produce submodule packages.

The release validation surface uses `Hexalith.EventStore.slnx` for restore and build, keeps package versions centralized in `Directory.Packages.props`, and runs unit tests per project instead of using solution-level `dotnet test` as the default validation path.

High-risk verification requires real persisted or runtime evidence where the behavior depends on DAPR, package output, generated controllers, or topology. Live-sidecar coverage belongs in an integration lane; generated API smoke checks must classify failures before treating them as evidence against generated REST behavior.

AppHost resource names, DAPR app IDs, component scopes, ACL policies, pub/sub topics, and deployment overlays must remain aligned by tests when topology or resource naming changes.

## Cross-Story Dependencies

Story 3.8 is the companion evidence path for Story 3.1. Live-sidecar re-tiering depends on a smoke preflight that can identify blocked local infrastructure before generated API or DAPR failures are accepted as product failures.

Story 3.3 underpins Stories 3.5 and 3.6 because release/package validation must not depend on stale root-level submodule paths or local checkout shape.

Story 3.5 must be coherent before Story 3.6 can prove package metadata and release output, since manifest-driven publishing depends on Release package-reference mode and centrally pinned dependency versions.

Story 3.7 coordinates workflow caller migration with package and release validation. Its implementation boundary includes shared workflow reference checks, cache behavior, and explicit backlog tracking for supply-chain publishing hardening.
