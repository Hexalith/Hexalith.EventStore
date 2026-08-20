# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 3 enables maintainers to build, test, package, publish, and verify EventStore independently of local checkout state. It separates deterministic release gates from live infrastructure coverage, makes dependency and package inventory authority explicit, aligns repository and Aspire topology conventions, and establishes fail-closed evidence from an exact source and package set through an immutable multi-platform deployed image. Invalid candidates remain preserved but non-authorizing; only a separately authorized corrective release and independent parity verification may select a conforming deployed-runtime identity.

## Stories

- Story 3.1: Re-Tier Live-Sidecar Tests from the Release Gate
- Story 3.2: Harden DAPR ETag Timeout for Integration Conditions
- Story 3.3: References-Based Submodule Layout
- Story 3.4: Aspire Security Resource Naming
- Story 3.5: Shared Package Catalog and Source/Package Reference Modes
- Story 3.6: Manifest-Driven Release Packaging
- Story 3.7: Shared Workflow Caller Migration
- Story 3.8: Workflow Reference and Validation Safety
- Story 3.9: Supply-Chain Publishing Backlog
- Story 3.10: Generated API DAPR/Aspire Smoke Preflight
- Story 3.11: Validated Central Package Catalog Refresh
- Story 3.12: Multi-Platform EventStore Container Publishing Correction
- Story 3.13: v3.94.1 Deployed Runtime Evidence Disposition
- Story 3.14: Corrective OCI Provenance Release
- Story 3.15: Corrected Deployed Runtime Parity Closure

## Requirements & Constraints

Deterministic release-gate tests and live-DAPR tests must reside in separate, independently executed lanes. Live tests remain visible through a dedicated integration workflow with bounded sidecar readiness/warm-up and persisted state evidence, but do not gate semantic-release. The DAPR ETag service preserves its production request timeout while allowing a per-instance integration override without shared mutable state or weakened cancellation/failure behavior. Local generated-API preflight must be support-safe, read-only unless explicitly allowed to start topology, distinguish environment blockers from product defects, and require persisted event plus read-model/query evidence rather than HTTP status alone.

Root-declared Hexalith submodules live under `references/`; solution, build, documentation, Aspire, and instruction paths use that layout, and nested submodules are not initialized. The Keycloak-backed Aspire resource uses the stable service-role name `security`, with AppHost, generated deployment output, fixtures, diagnostics, and topology lookups aligned while Keycloak-specific implementation configuration remains intact.

Package references are the default in every configuration. Source references require explicit `UseHexalithProjectReferences=true` and an available root-declared source path. All source-owned NuGet versions come from the imported Hexalith.Builds catalog; consumers define no local version, override, or fallback. Catalog updates use configured-source evidence and compatibility-proven rollback groups; coupled families move together, exceptions record rationale and removal triggers, and missing or unlisted search results never justify downgrades.

Restore/build uses the `.slnx`; tests run per project. Release commands assert package mode, never package submodule projects, and publish only packages governed by `tools/release-packages.json`. Validation inspects package archive bytes and metadata, rejects missing or unexpected inventory, and proves package-only consumer restoration. EventStore workflow files remain thin callers of shared Hexalith.Builds CI, security, validation, and publishing behavior; reusable execution references, caches, source identity, credentials, package validation, and publish ordering fail closed before irreversible publication. Deferred credential, attestation, SBOM, and provenance improvements remain explicit backlog work and gain no implementation authority from this epic.

The released EventStore container is produced with .NET SDK container support as one immutable OCI image index containing exactly `linux/amd64` and `linux/arm64` child manifests. Raw index, manifest, and config bytes, digests, sizes, platform fields, required OCI provenance labels, and bounded smoke results must agree. Wrong media type, extra/missing/nested/unknown platforms, unresolved descriptors, mixed lineage, or unavailable platform smoke blocks the evidence gate. Published tags are immutable; a failed candidate is retained and corrected only by a new semantic version.

`v3.94.1` is rejected, non-authorizing evidence and cannot select a deployed identity. A corrective release requires a durable, one-use, content-bound pre-publication authority. Positive deployed-runtime parity is established only by independent verification of one canonical release identity binding repository, version/tag, exact source SHA, workflow and Builds revisions, authority digest, package manifest and package hashes, OCI index/children/configs, and runtime smoke. Planning approval, story completion, tags, self-declared roles, or evidence spliced from another release authorize neither publication, deployment, nor consumer infrastructure removal.

## Technical Decisions

Release behavior is governed by three authorities: the shared Builds NuGet catalog for dependency versions, `tools/release-packages.json` for the EventStore package inventory, and the SHA-pinned shared publisher/validator for OCI shape and validation. Release/package validation defaults to package mode; explicit source mode is a development option, not a publication input.

AppHost resources, DAPR app IDs and sidecar options, component and ACL scopes, deployment overlays, and topology tests change as one unit. High-risk validation inspects persisted state, topology, package contents, raw registry objects, immutable digests, and smoke evidence; status codes and prior pass flags are insufficient.

The canonical release identity is encoded once and hashed from retained canonical bytes. Every verifier derives identity edges from trusted workflow facts and raw evidence rather than reserialization or mutable tags. Deployment profiles identify a release by its validated OCI index digest. Evidence acceptances and consumer-removal authorization are distinct, authenticated, content-bound decisions; any changed transitive evidence invalidates their receipts.

## Cross-Story Dependencies

Stories 3.1 and 3.3 establish the test-lane and repository-layout foundations. Story 3.2 depends on 3.1; Story 3.4 depends on 3.3; Story 3.5 depends on 3.3 plus completed consumer-parity work; Story 3.6 depends on 3.5. Story 3.7 uses 3.1 and 3.6, Story 3.8 follows 3.6-3.7, and Story 3.9 records only the remaining supply-chain backlog. Story 3.10 combines the 3.1 lane boundary with Epic 2's generated API surface, while Story 3.11 refreshes the catalog established by 3.5.

Story 3.12 builds on 3.6 and 3.8. Story 3.13 independently preserves the negative `v3.94.1` disposition and may proceed in parallel with Story 3.14. Story 3.14 builds on 3.6, 3.8, and 3.12 and creates the separately authorized candidate. Story 3.15 depends only on completed source/package parity and Story 3.14; it must not reuse Story 3.13 facts to splice lineage or treat parity evidence as deployment or consumer-mutation authority.
