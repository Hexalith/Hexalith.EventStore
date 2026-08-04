# Epic 3 Context: Release And Repository Reliability

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 3 makes EventStore releases reproducible and repository operations predictable. It separates deterministic release gates from live DAPR coverage, standardizes submodule and Aspire resource layout, makes dependency source/package selection explicit, centralizes package-version authority, constrains publication to a reviewed manifest, and moves CI/CD policy into shared workflows. It also requires support-safe runtime evidence, correct multi-platform container output, and an independently approved identity chain from source and packages to the deployed image so operators can select a proven artifact without relying on mutable tags or local checkout state.

## Stories

- Story 3.1: Re-Tier Live-Sidecar Tests From Release Gate
- Story 3.2: Harden DAPR ETag Timeout For Integration Conditions
- Story 3.3: References-Based Submodule Layout
- Story 3.4: Aspire Security Resource Naming
- Story 3.5: Shared Package Catalog And Source/Package Reference Modes
- Story 3.6: Manifest-Driven Release Packaging
- Story 3.7: Shared Workflow Caller Migration
- Story 3.8: Workflow Reference And Validation Safety
- Story 3.9: Supply-Chain Publishing Backlog
- Story 3.10: Generated API DAPR/Aspire Smoke Preflight
- Story 3.11: Validated Central Package Catalog Refresh
- Story 3.12: Multi-Platform EventStore Container Publishing Correction
- Story 3.13: Deployed Runtime Parity Closure

## Requirements & Constraints

Release-gate tests must be deterministic. Tests requiring a live `daprd` sidecar carry an explicit live-sidecar category and run in a dedicated integration lane with readiness retry and actor warm-up; their failures remain visible but do not block semantic-release publishing. Local generated-API smoke tooling must classify environment and emulation failures separately from product failures, operate read-only unless explicitly told to start infrastructure, and require persisted event and read-model/query evidence rather than treating status codes as proof.

`DaprETagService` keeps its production actor-request timeout unless a caller explicitly overrides it. Integration paths may tolerate cold activation, but the override must not weaken handling of genuine production actor failures.

Root-declared Hexalith submodules live under `references/`. Solution, build, documentation, Aspire metadata, instruction, and tooling paths must use that layout; nested submodules must not be initialized or required.

The Aspire identity-provider resource is named `security` while Keycloak remains the implementation. Realm import, ports, dependencies, authentication, fixture behavior, resource lookups, and telemetry identity must continue through the service-role name.

Cross-repository dependencies are selected by build intent. Source references require explicit `UseHexalithProjectReferences=true` and available root-declared source; unset or `false` means package intent in every configuration. Source-owned NuGet versions come only from `references/Hexalith.Builds/Props/Directory.Packages.props`; consumers import that catalog without local package versions or fallbacks. Validation must restore again after changing modes and prove there is exactly one active dependency source per mode, with no mixed source/package graph. Repository migrations remain within each maintainer's authority, while Story 3.5 retains ecosystem-wide FR21 completion responsibility: an unauthorized or incomplete affected-repository migration blocks Story 3.5 rather than becoming unmapped follow-up work.

The shared catalog uses the latest validated compatible versions. Stable pins prefer stable releases; prerelease channels, major upgrades, framework/SDK coupling, and aligned release families require grouped compatibility proof. Retained exceptions need rationale, evidence, and a removal trigger; missing, unlisted, or older search results never justify a downgrade.

Release packaging is manifest-driven. Only EventStore packages declared in `tools/release-packages.json` may be built, packed, or published; unexpected and submodule packages fail validation. Release commands explicitly use package mode, and NuGet metadata exposes package dependencies rather than local source paths.

EventStore CI/CD uses thin callers for shared Hexalith.Builds gates. Ordinary shared CI workflow/action references intentionally use `@main`, while the release publisher/caller execution is bound to an exact approved Hexalith.Builds SHA and shared workflows enforce third-party action pinning. Dependency-mode-aware caches, manifest validation, consumer restore, credentials, artifact identity, and head SHA must fail before irreversible publication. Trusted Publishing, attestations, SBOM, provenance, and credential modernization remain separately authorized backlog work.

The EventStore container is produced with .NET SDK container support as one immutable OCI image index containing exactly `linux/amd64` and `linux/arm64` children. Wrong media type, missing, duplicate, extra, unresolved, or mismatched platform descriptors, digest/config inconsistencies, and an unexecuted or failed platform smoke all fail closed. A failed release is preserved as non-authorizing evidence and corrected only with a new semantic version.

Deployed-runtime closure must map an owner-approved source SHA and exact package versions/hashes to the immutable OCI index digest, both child manifests/configs, release run, and support-safe runtime proof. Missing or inconsistent evidence fails closed. Planning approval alone grants no authority to publish, mutate registry objects, change consumers, or infer identity from a branch, tag, consumer SHA, or other mutable reference.

## Technical Decisions

`tools/release-packages.json` is the release inventory; package-reference mode is the default for release validation, and source references require explicit opt-in. `references/Hexalith.Builds/Props/Directory.Packages.props` is the sole source-owned NuGet catalog.

Release validation restores and builds `Hexalith.EventStore.slnx`, runs tests per project, validates manifest-exact package output and dependency metadata, and keeps deterministic and live-sidecar lanes distinct.

High-risk behavior is verified through persisted state, package bytes/metadata, topology, raw registry objects, immutable digests, and bounded runtime smoke as applicable. Support-safe reports omit tokens, JWTs, connection strings, private addresses, raw payloads, and stack traces.

AppHost resource names, DAPR app IDs, sidecar options, component scopes, ACL policies, topics, deployment overlays, and topology tests change as one aligned unit. Container children are built with .NET SDK support, not Dockerfiles, and deployment identity is the validated OCI index digest rather than its tag.

## Cross-Story Dependencies

Story 3.10 is the companion evidence path for Story 3.1: live-sidecar and generated-API findings require the preflight's environment/product/state-evidence classification.

Story 3.3 must be complete before Story 3.5. The resulting dependency-mode and central-catalog posture underpins Story 3.6 packaging and must be complete before Story 3.11 refreshes catalog versions.

Stories 3.7 and 3.8 coordinate thin workflow migration with safe references, caches, validation, and publish ordering. Story 3.9 records unresolved supply-chain work without expanding those completed safeguards.

Story 3.13 begins only after Stories 1.20 and 3.12 are complete. It independently revalidates their source/package and multi-platform release packets; it cannot change their status, reopen Epic 1, authorize consumer migration, or mutate published artifacts.
