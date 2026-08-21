# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 3 enables maintainers to build, test, package, publish, and verify EventStore independently of local checkout state. It separates deterministic release gates from live infrastructure coverage, makes dependency and release inventory authority explicit, and establishes fail-closed evidence from one exact source and package set through an immutable multi-platform deployed image. Invalid candidates remain preserved but non-authorizing; only a separately authorized corrective release followed by independent parity verification may select a conforming deployed-runtime identity.

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

Deterministic release-gate tests and live-DAPR tests must run in separate lanes. Live tests remain visible through a dedicated integration workflow with bounded sidecar readiness handling and persisted end-state evidence, but do not gate semantic release. Integration-only timeout overrides must preserve production defaults, cancellation, and failure behavior. Local generated-API preflight must distinguish environment blockers from product defects and require persisted event plus read-model/query evidence rather than HTTP status alone.

Root-declared Hexalith submodules live under `references/`; solution, build, documentation, Aspire, and instruction paths use that layout. The Keycloak-backed Aspire resource uses the stable service-role name `security`, with AppHost, deployment output, fixtures, diagnostics, and topology lookups aligned while Keycloak remains the implementation technology.

Package references are the default in every configuration. Source references require explicit `UseHexalithProjectReferences=true` and an available root-declared source path. All source-owned NuGet versions come from the imported Hexalith.Builds catalog; consumers define no local version authority or fallback. Catalog updates require configured-source and compatibility evidence, move coupled families together, and preserve explicit rollback groups.

Restore and build use the `.slnx`; tests run per project. Release commands assert package mode, exclude submodule projects, and publish only the inventory in `tools/release-packages.json`. Validation inspects package bytes and metadata, rejects missing or unexpected inventory, and proves package-only consumption. EventStore workflows remain thin callers of shared Hexalith.Builds CI, security, validation, and publishing behavior. Deferred credential, signing, attestation, SBOM, and provenance improvements remain explicit backlog work and gain no implementation authority from this epic.

The released EventStore container is produced with .NET SDK container support as one immutable OCI image index containing exactly `linux/amd64` and `linux/arm64` children. Raw index, manifest, and config bytes, digests, sizes, platform fields, required OCI provenance labels, and bounded smoke results must agree. Wrong media type, extra, missing, nested, duplicate, variant, or unknown platforms, unresolved descriptors, mixed lineage, or unavailable platform smoke blocks the gate. Published tags are immutable; a failed candidate is corrected only by a new semantic version.

`v3.94.1` is permanently rejected, non-authorizing evidence and cannot select a deployed identity. A corrective release requires a durable, one-use, content-bound pre-publication authority. Positive deployed-runtime parity requires independent verification of one canonical identity binding repository, version/tag, exact source SHA, workflow run and revision, Builds execution SHA, authority digest, package manifest and hashes, OCI index/children/configs, provenance labels, and both platform smokes. Planning approval, story completion, tags, prior pass flags, self-declared roles, or evidence from another release authorize neither publication, deployment, nor consumer infrastructure removal.

## Technical Decisions

Release behavior has three explicit authorities: the shared Builds catalog for dependency versions, `tools/release-packages.json` for the EventStore package inventory, and the SHA-pinned shared publisher/validator for OCI shape and validation. Release validation defaults to package mode; explicit source mode is a development option, not a publication input.

AppHost resources, DAPR app IDs and sidecar options, component and ACL scopes, deployment overlays, and topology tests change as one unit. High-risk validation inspects persisted state, topology, package contents, raw registry objects, immutable digests, and smoke evidence; status codes, mutable tags, labels alone, and inherited pass flags are insufficient.

The platform-owned, versioned release-evidence codec emits the canonical bytes used for hashing; verifiers do not reserialize them. Every identity edge is derived from trusted workflow facts and retained raw evidence. Deployment identity is the validated OCI index digest, with observations mapped only through its recorded child-manifest/config chain. Positive closure requires content-bound receipts from the authenticated EventStore owner, Release owner, and Test Architect. Those receipts establish evidence only; deployment and consumer removal require separate authority, and any changed transitive evidence invalidates the receipts.

## Cross-Story Dependencies

Stories 3.1 and 3.3 establish the test-lane and repository-layout foundations. Story 3.2 depends on 3.1; Story 3.4 on 3.3; Story 3.5 on 3.3 plus completed consumer-parity work; and Story 3.6 on 3.5. Stories 3.7-3.8 build the shared-workflow path from those foundations, while Story 3.9 records only deferred supply-chain work. Story 3.10 combines the live-lane boundary with Epic 2's generated API surface, and Story 3.11 refreshes the catalog established by 3.5.

Story 3.12 builds on 3.6 and 3.8. Story 3.13 independently preserves the negative `v3.94.1` disposition and may proceed in parallel with Story 3.14. Story 3.14 builds on 3.6, 3.8, and 3.12 to create a separately authorized candidate. Story 3.15 depends only on completed Story 1.20 source/package parity and Story 3.14; it must not use Story 3.13 facts to splice lineage or treat parity evidence as deployment or consumer-mutation authority. Epic 3 closes only after positive Story 3.15 parity and every other Epic 3 story are complete.
