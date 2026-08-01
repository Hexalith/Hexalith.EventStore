---
id: SUPPLY-CHAIN-PUBLISHING
title: Supply-Chain Publishing Backlog
classification: backlog
status: accepted
source_story: 3.9
created: 2026-08-01
updated: 2026-08-01
evidence_verified: 2026-08-01
artifact_owner: Administrator
reviewer: Administrator
delegation: Administrator was authorized during the Story 3.9 code review to act for Paige and Amelia.
---

# Supply-Chain Publishing Backlog

## Purpose and Boundary

This artifact records unresolved publishing hardening without reopening completed release
safeguards. It authorizes no workflow, repository-setting, credential, package, container,
registry, or runtime change. Every implementation requires a separate approved story and,
where another repository or external service owns the change, that owner's approval.

The inventory was derived from the EventStore release caller, semantic-release commands,
credential documentation, package manifest and validators, current CI/security callers, and
the completed manifest/container publishing stories. Paths below are repository-relative and
were verified on 2026-08-01.

## Inventory Rules

- **Lifecycle:** `open` means the gap is evidenced and has no accepted implementation;
  `blocked` means a named dependency prevents implementation; `accepted-risk` requires an
  explicit owner decision and review date; `closed` requires implementation plus validation
  evidence. This artifact contains only open work; completed safeguards are listed separately.
- **Theme completeness:** trusted publishing, attestations, SBOM, provenance, and credential
  modernization must each map to at least one item below or to an explicit evidence-backed
  no-gap disposition. No theme currently has a no-gap disposition.
- **Owner semantics:** an owner is the accountable repository/service role that must approve
  and deliver a future story. Naming an external owner here records the coordination boundary;
  it does not claim that owner has accepted delivery.
- **Dependencies:** prerequisites that must be satisfied before implementation may complete.
- **Risks:** the concrete consequence of leaving the item open or implementing it incorrectly.
- **Validation expectations:** minimum evidence a future implementation story must produce.
- **Current evidence paths:** repository-relative paths supporting the current classification.
  Review requires every path to resolve and be reverified when the artifact is updated.

## Open Inventory

### SCP-1 — NuGet Trusted Publishing and API-key retirement

- **Themes:** trusted publishing; credential modernization
- **Lifecycle:** open
- **Scope:** Replace EventStore's long-lived `NUGET_API_KEY` publication path with an approved
  NuGet.org Trusted Publishing/OIDC flow in the shared release workflow, then remove the key
  from the EventStore caller, semantic-release command, secret inventory, and preflight contract.
- **Owner:** Hexalith.Builds release-workflow maintainer, EventStore release owner, and NuGet.org
  organization administrator.
- **Dependencies:** NuGet.org policy bound to this repository/environment/workflow; an approved
  shared publisher contract; least-privilege workflow identity permissions; migration and
  rollback procedure that cannot produce a partial release.
- **Risks:** the current reusable secret is long-lived and repository-scoped; an incorrect OIDC
  subject or permission boundary could allow unauthorized publication or block all releases.
- **Validation expectations:** prove the exact repository/ref/environment identity; publish a
  non-production test package or use a provider-supported dry run; fail closed for an invalid
  subject; verify no release path reads `NUGET_API_KEY`; remove the secret only after the new path
  succeeds; preserve all source, manifest, destination, and container preflight gates.
- **Current evidence paths:** `.github/workflows/release.yml`, `.releaserc.json`,
  `scripts/validate-release-secrets.sh`, `docs/ci-secrets-checklist.md`, `docs/ci.md`.

### SCP-2 — Short-lived Zot container-publishing credentials

- **Themes:** credential modernization
- **Lifecycle:** open
- **Scope:** Replace or broker the long-lived Zot username/API-key pair used for container publish
  and immutable read-back with the shortest-lived identity mechanism supported by the registry.
- **Owner:** Zot registry owner and Hexalith.Builds container-publisher maintainer, coordinated by
  the EventStore release owner.
- **Dependencies:** documented Zot identity/token capabilities; protected-environment identity
  policy; publisher support for token acquisition, bounded lifetime, revocation, and authenticated
  digest read-back.
- **Risks:** a reusable registry credential can publish or inspect artifacts beyond the intended
  release window; a migration that authenticates publish but not digest read-back can create an
  unverifiable partial release.
- **Validation expectations:** prove least-privilege repository scope and token lifetime; exercise
  publish plus immutable digest read-back; reject expired, wrong-repository, and missing identities
  before NuGet publication; remove `HEXALITH_ZOT_USERNAME` and `HEXALITH_ZOT_API_KEY` only after the
  replacement contract passes the same fail-closed tests.
- **Current evidence paths:** `.github/workflows/release.yml`,
  `scripts/validate-release-secrets.sh`, `scripts/validate-publication-preflight.sh`,
  `docs/ci-secrets-checklist.md`, `docs/ci.md`.

### SCP-3 — Release SBOMs for NuGet packages and container images

- **Themes:** SBOM; provenance
- **Lifecycle:** open
- **Scope:** Generate a versioned SPDX or CycloneDX SBOM for every manifest package and each
  published container platform, bind each document to the exact package hash or OCI digest, and
  retain it with the release evidence.
- **Owner:** Hexalith.Builds release-workflow maintainer with EventStore package/container owners.
- **Dependencies:** approved format and generator; deterministic component identity; retention and
  publication location; OCI attachment design compatible with the exact two-platform index contract.
- **Risks:** incomplete or unbound SBOMs provide false assurance; adding BuildKit-style
  `unknown/unknown` descriptors to the release index would violate the existing container contract.
- **Validation expectations:** cover all 14 packages and both immutable child images; validate the
  schema; bind every SBOM to exact hashes/digests and source SHA; prove no extra index descriptor is
  introduced; download and independently verify the retained documents.
- **Current evidence paths:** `tools/release-packages.json`, `tools/release_package_contract.py`,
  `docs/ci.md`,
  `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`.

### SCP-4 — Keyless artifact attestations

- **Themes:** attestations; provenance
- **Lifecycle:** open
- **Scope:** Produce verifiable build provenance attestations for NuGet archives, GitHub release
  assets, the OCI index, and both child manifests without embedding forbidden extra platform
  descriptors in the release index.
- **Owner:** Hexalith.Builds release-workflow maintainer and organization security owner, with
  EventStore release-owner acceptance.
- **Dependencies:** approved keyless identity and attestation format; least-privilege identity and
  attestation permissions; storage via GitHub attestations, release assets, or OCI referrers; an
  offline verification contract.
- **Risks:** an attestation not bound to the exact source, workflow, reusable-workflow SHA, package
  hashes, and image digests can be replayed or misattributed; in-index attestations can break the
  closed two-platform shape guard.
- **Validation expectations:** verify subject identity, source SHA, workflow/ref/environment,
  immutable Builds execution SHA, package hashes, and image digests; reject a tampered subject and
  wrong repository; retain verification output; prove the OCI index remains exactly amd64/arm64.
- **Current evidence paths:** `.github/workflows/release.yml`,
  `scripts/validate-publication-preflight.sh`, `docs/ci.md`,
  `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`.

### SCP-5 — NuGet package signing and verification policy

- **Themes:** attestations; provenance
- **Lifecycle:** open
- **Scope:** Decide whether repository, author, or keyless signing is required for EventStore NuGet
  packages; define trust roots, rotation/revocation, timestamping, and consumer verification before
  adding signing to the shared publisher.
- **Owner:** organization security owner, NuGet.org organization administrator, and
  Hexalith.Builds release-workflow maintainer.
- **Dependencies:** approved signing model and key custody; NuGet.org compatibility; shared-workflow
  signing step; verifier policy for CI and downstream consumers.
- **Risks:** unmanaged signing keys create a stronger long-lived credential than the one being
  removed; signing without enforced verification adds cost without detecting substitution.
- **Validation expectations:** verify every one of the 14 produced packages against approved trust
  roots and timestamp policy; mutation-prove rejection of tampered, expired, revoked, unsigned, and
  wrong-signer packages; document rotation and emergency revocation.
- **Current evidence paths:** `tools/release-packages.json`, `.releaserc.json`, `docs/ci.md`,
  `docs/ci-secrets-checklist.md`.

### SCP-6 — Unified package, container, and release provenance packet

- **Themes:** provenance
- **Lifecycle:** open
- **Scope:** Extend the existing source/destination/container evidence into one machine-readable
  release packet that binds the source commit, CI run, reusable-workflow commit, package manifest,
  every `.nupkg` hash, GitHub release assets, OCI index/children/configs, SBOMs, attestations, and
  final publication destinations.
- **Owner:** Hexalith.Builds evidence-contract maintainer with EventStore release owner.
- **Dependencies:** SCP-3 and SCP-4 identities; stable evidence schema and retention; independent
  verifier; explicit handling of semantic-release's tag and GitHub Release lifecycle.
- **Risks:** today the strongest container evidence and package manifest gates remain separate;
  operators cannot verify the complete release identity from one immutable packet.
- **Validation expectations:** regenerate and verify the packet from retained artifacts; reject any
  source/workflow/hash/digest/version/destination mismatch; preserve partial evidence on failure;
  prove all 14 packages and exactly two container platforms are represented once.
- **Current evidence paths:** `.github/workflows/release.yml`, `.releaserc.json`,
  `scripts/validate-publication-preflight.sh`, `tools/release-packages.json`, `docs/ci.md`,
  `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`.

### SCP-7 — Immutable provenance for CI and security workflow callers

- **Themes:** provenance
- **Lifecycle:** open
- **Scope:** Decide and enforce an immutable identity policy for the shared CI, CodeQL, dependency
  review, commitlint, and initialization workflows that currently resolve `Hexalith.Builds@main`.
  This tracks authorization provenance only; it does not reopen the completed thin-caller migration.
- **Owner:** Hexalith.Builds governance maintainer and EventStore repository maintainer.
- **Dependencies:** organization-wide update policy; reviewed SHA or verifiable release-tag
  rotation process; automation that keeps caller and nested action identities aligned; coordinated
  consumer rollout.
- **Risks:** mutable shared code can change the checks that mark an EventStore source SHA green;
  release later treats that exact-source CI result as an authorization prerequisite even though the
  release publisher itself is immutably pinned.
- **Validation expectations:** reject mutable or mismatched identities under the approved policy;
  prove every shared caller and nested action resolves to the reviewed identity; preserve scheduled
  update and rollback mechanics; keep the publication-capable release caller's exact-SHA guard.
- **Current evidence paths:** `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`,
  `.github/workflows/dependency-review.yml`, `.github/workflows/commitlint.yml`,
  `.github/workflows/release.yml`, `docs/ci.md`.

## Theme Coverage Crosswalk

| Required theme | Inventory coverage | Disposition |
| --- | --- | --- |
| Trusted publishing | SCP-1 | Open; no no-gap disposition |
| Attestations | SCP-4, SCP-5 | Open; no no-gap disposition |
| SBOM | SCP-3 | Open; no no-gap disposition |
| Provenance | SCP-3 through SCP-7 | Open; no no-gap disposition |
| Credential modernization | SCP-1, SCP-2 | Open; no no-gap disposition |

## Completed Safeguards — Closed Baseline

These controls remain closed. A regression requires a new approved story; this backlog may cite
that story as a dependency but does not itself authorize reopening or changing the control.

| Control | Closed behavior | Current evidence paths |
| --- | --- | --- |
| Manifest-owned NuGet inventory | Exactly 14 declared packages; caller and wrapper independently pin the count. | `tools/release-packages.json`; `scripts/validate-publication-preflight.sh`; `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` |
| Package/archive and consumer validation | Manifest project identity, package metadata, dependency closure, exact output, and package-only consumer use fail closed. | `tools/release_package_contract.py`; `tools/validate-release-packages.py`; `scripts/validate-consumer-package-references.py`; `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` |
| Exact release source and publisher | Manual release requires the live `main` SHA with successful exact-source push CI; the release publisher and nested execution SHA are immutable and equal. | `.github/workflows/release.yml`; `scripts/validate-publication-preflight.sh`; `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` |
| Pre-publish credential and destination checks | NuGet and Zot credentials, publisher helper, source identity, manifest, destination absence, and frozen identity are checked before irreversible publication. | `.releaserc.json`; `scripts/validate-release-secrets.sh`; `scripts/validate-publication-preflight.sh`; `docs/ci.md` |
| Approved container shape and mapping | Only `eventstore` is published; the OCI index is exactly linux/amd64 plus linux/arm64 with digest read-back and child smoke evidence. | `.github/workflows/release.yml`; `docs/ci.md`; `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` |

## Review and Traceability Checklist

- [x] Every required theme maps to an open item or an explicit no-gap disposition.
- [x] Every open item states scope, accountable owner roles, dependencies, risks, validation
  expectations, lifecycle, and current evidence paths.
- [x] Current release workflow, credential documentation, package manifest/validators, and
  completed container evidence were inspected.
- [x] Every listed evidence path resolved in the working tree on 2026-08-01.
- [x] Closed manifest/package/source/container safeguards are identified with closure evidence and
  were not reopened.
- [x] No publishing, workflow, credential, repository-setting, registry, or runtime change is
  authorized or implemented by this artifact.
- [x] Paige ownership and Amelia feasibility/current-workflow review were performed by
  Administrator under the explicit delegation recorded in Story 3.9.

## Focused Validation Record

Validation date: 2026-08-01. Commands are read-only; no external publication or setting mutation
was performed.

| Check | Result |
| --- | --- |
| Story scan: `rg -n "NUGET_API_KEY\|trusted publishing\|attestation\|SBOM\|provenance" docs .github _bmad-output -g "*.md" -g "*.yml" -g "*.yaml"` | Passed; current credentials and the unresolved themes are present in the cited evidence. |
| Shared-reference scan: `rg -n "Hexalith.Builds/.+@" .github references -g "*.yml" -g "*.yaml"` | Passed; the release caller uses one exact SHA while CI/security callers still use `@main`. |
| Publishing-capability scan over `.github/workflows`, `scripts`, `tools`, and `.releaserc.json` | Passed; current API-key/Zot credential use is evidenced, and no EventStore release path enables Trusted Publishing, SBOM generation, artifact attestation, or package signing. |
| Evidence-path resolution | Passed; every repository-relative path in this artifact existed on 2026-08-01. |
| `git diff --check` for Story 3.9 artifacts | Passed. |

## Accepted Review Disposition

Administrator, explicitly authorized in the Story 3.9 code-review decision to act for both Paige
and Amelia, accepts this seven-item inventory and its evidence paths on 2026-08-01. The items are
feasible only as separately approved, owner-coordinated stories. This acceptance closes the
planning-product requirement; it does not accept implementation ownership on behalf of external
maintainers and authorizes no publishing or runtime change.
