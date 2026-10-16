# Canonical reconciliation and publication-readiness handoff

P1R remains unusable and Story 6.1 remains blocked. This package supplies an
uncommitted reconciliation and workflow implementation for later authorized
landing. It grants no release reservation, authority, production approval,
published successor selection, conformance or prerequisite acceptance. Acceptance
and sprint state remain unchanged.

## Review and source boundaries

The original EventStore checkout remains on `main` at
`247a192274d0ab01b81b8f5430d6293c3bdbda95`; Projects remains on `main` at
`2b7edae27175fa5921de5768d9580e16d615db34`. Their existing hardening package,
Projects Story 6.1 draft, indexes and workspace gitlinks are protected separately
by this package's preservation receipts. Completed recovery, enrollment,
hardening and historical failed evidence remain immutable context.

The isolated checkout is
`/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016`.
It uses its own Git directory and is detached at freshly fetched canonical main
`a2016e88f4d0da9b953fe6f4394f8b8fd4e27f72`. The full reviewed 26-path closure is
reconciled there. The executor fixture preserves canonical generated credential
rendering; the other credential fixture remains canonical. Canonical production,
security/evidence-ingestion code, package pins and gitlinks are retained. All
three new hardening Python files and four dated CI helpers are included. The
original hardening/recovery manifests remain historical and are not rewritten
as new-source manifests. The final README indexes exact diff, source hashes,
review and verification.

The baseline tag/construction Builds gitlink is
`728174f506d56204657e0c52e98a4453aef79f6a`; publisher execution Builds remains
`22a578b576a515d2af214fe81859447fffc97981`. These are distinct identities.
Future landed source `S`, its final Builds gitlink, construction inputs and
published archives remain unobserved until their actual freeze/publication.

## Evidence-ingestion landing blocker

The complete new evidence bundle has nine canonical scanner flags in preserved
review-diff text and registry metadata; see [the exact audit and disposition
requirements](evidence-ingestion-handoff.md). All 34 delivered source paths pass
the same scanner. Do not bulk-land the evidence directory or weaken ingestion.
Resolve its authorized retention/landing boundary before source freeze and CI.

## Release and destination observations

The [fresh release analysis](release-analysis/release-readiness-analysis.json)
uses installed commit-analyzer 13.0.1, canonical configuration and eleven actual
commits after published `v3.122.0`. It predicts a minor release, **3.123.0**, for
that observed canonical source. The uncommitted reconciliation is excluded from
that calculation. **3.123.0 is analysis only; 3.122.1 is a historical enrollment
proposal. Neither is reserved or selected.** Reanalyze the actual authorized
landing, including its validated Conventional Commit subjects.

All fourteen public NuGet indices currently have stable floor 3.122.0. Direct
archives and GitHub tag/release lookups return 404 for both examined versions.
Initial unauthenticated OCI requests returned 401; those attempts remain
retained. Existing Docker desktop credentials subsequently provided an
[authenticated registry observation](release-analysis/authenticated-registry-observations.json):
tag list 200 with no pagination, known 3.122.0 manifest 200, and both examined
manifest tags 404. No credential material was retained or created. The earlier
environment-variable-only authentication gap is resolved for this read-only
observation. Actual workflow publisher authentication, credentials/OIDC,
registry absence and the complete version floor must be proved again at
publication time; local Docker authentication does not prove that identity.

[Refreshed canonical observations](release-analysis/refreshed-canonical-observations.json)
show CI38905803604 in progress for the refreshed source above. P1R preparation
38905803405 and Commitlint38905803694 passed for that source. The previous
canonical CI failure remains historical. None is evidence for the uncommitted
reconciliation or a future landed `S`. The observed production environment
requires a reviewer and disallows administrator bypass; its protection was not
changed. The refresh changed only `AggregateActorTestHelper.cs`; all 34 delivery
bytes and reviewed patch hunks remain equal, as independently bound by the
[refresh applicability record](refreshed-review-applicability.json).

## Exact remaining authorized landing and source freeze

1. Obtain separate authorization to land the reviewed reconciliation. Observe
   then-current canonical main and reconcile any new changes without replacing
   production/security source or losing recovery history. Resolve the evidence
   ingestion boundary above before tracking any flagged captures. Apply the complete
   delivered diff, including untracked Python sources, dated helpers, authority
   controls and workflow changes. Preserve all original historical records.
2. Re-run actual semantic-release analysis and destination floor/absence checks.
   Reconcile the finite proposed consumer/compiler/witness/importer enrollment
   with the single resulting publication proposal before final freeze. This
   package deliberately retains historical 3.122.1 enrollment; current analysis
   predicts a different version. Neither arbitrary-version enablement nor
   publishing 3.122.1 against a minor result is permitted. Any enrollment/source
   change requires new hashes, controls and independent review as appropriate.
3. Freeze full landed current-main `S`, exact reviewed source closure, final
   Builds gitlink `Btag`, SDK/central props/construction identities, and the
   separately reviewed publisher execution identity `Bexec`. Capture actual Git
   objects, source hashes and imported inputs. Validate actual commit messages
   with the owning repository's pinned commitlint before any Git mutation.
4. Obtain successful **push** `ci.yml`, `p1r-qualification.yml` and
   `commitlint.yml` runs for the same current-main `S`, including canonical
   .NET/package/consumer and secret/evidence gates. Resolve recorded local
   environment blockers in the approved clean-checkout environment. Do not
   initialize prohibited nested dependencies or weaken gates. Every later
   source change invalidates the freeze and requires new verification/CI.

## Authority and guarded publication

The new workflow requires `bypass-validation=false`, an explicit future
`release-version`, real `release-authority-issue-url` and
`release-authority-owner`; it sets authority required to true. Do not reuse the
historical proposal as a default. Supply only separately authorized real values.

5. After source freeze and successful exact-source CI, separately authorize
   dispatch of `release.yml` from current main with those real inputs. The
   unprotected job rechecks all three exact-source push proofs and exposes a
   read-only candidate artifact/hash for that dispatch's exact run/attempt.
   This candidate is not an authority grant and performs no destination write.
6. Independently inspect the candidate bytes, source/version, manifest,
   contract/Bexec hashes, protected environment and run identity. The real
   authorized release owner must supply a genuine immutable issue comment with
   schema `hexalith.release-publication-authority.v1`, exact candidate identity
   hash, role, rationale, validity window and fresh nonce according to the pinned
   publisher contract. An expired, edited, wrong-owner, mismatched, consumed or
   replayed authority is refused. No comment or nonce from a synthetic test is
   usable. A new dispatch/attempt requires its newly observed identity and new
   authority; production approval does not replace this grant. Rerun **all**
   jobs to regenerate the candidate for a new attempt. Failed-jobs-only reruns
   with an old candidate are explicitly refused before artifact download.
7. Obtain the existing protected `production` approval separately. Actual
   publication also requires publication-enable exactly true, approved
   publisher credentials/OIDC, npm signatures/provenance, SDK restore, Release
   build with warnings as errors, fourteen-package manifest validation, current
   main/exact-source proofs, and authenticated NuGet/OCI absence/version-floor
   checks. The protected job downloads the same-run artifact and rederives the
   exact identity before verify and publish. The approved publisher validates
   the genuine authority and records one-use consumption **before the first
   NuGet package write**; the container publisher requires the same consumption
   receipt. Never bypass the guarded workflow with direct push commands.
8. Retain the exact release run/attempt and complete success/failure evidence.
   Never overwrite an existing published package or tag's bytes. Any failure
   after consumption remains an observed failed attempt; obtaining new authority
   is a separate owner decision and must also satisfy destination collision
   checks. This task performed no dispatch, authority write or publication.

## Observed selection, qualification and acceptance

9. Independently observe the actual release/tag object and peeled `S`, all
   fourteen NuGet identities, signatures, archive hashes and repository commits,
   and OCI multi-platform/child digests and provenance. Obtain explicit owner
   selection of the observed successor tuple and exact input bytes. No analysis
   version, local build, fixture, previous run or historical selection substitutes.
10. In a fresh invocation, qualify the selected published tuple through the
    private isolated runner from a self-contained ordinary checkout in a real
    declared workspace. Shared object alternates or linked Git metadata outside
    its mount are unsupported. Prepare, measure and validate through the same
    pinned runtime; host interpreter substitution is refused. Retain the entire
    invocation and physical input/receipt/binary/manifest paths at their original
    absolute locations, since validation rereads them. Preserve rollback `null`, comparisons 3.70.1 and
    3.110.0, Dapr 1.18.2 and the complete PostgreSQL v1 profile, including
    `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
    Require all seventeen lanes plus adopted Reminder recovery/logical-event
    evolution, genuine nine/manual-version-one/three-tail snapshot proof,
    fresh persisted restore, append/restart thirteen, floor five, preserved
    original hashes/second tenant and all corruption/protection/uncovered
    controls. Retain single-writer drain, mutation freeze, forward recovery,
    complete shared-inventory equality and exact private/outer cleanup. Preserve
    all failed attempts. Source-only controls cannot qualify a published tuple.
    The four dated helpers remain immutable CI dependencies, not the new
    operational audit: that audit must explicitly bind the new proposal, require
    zero executor errors and recheck current source after all waits before sealing.
11. Obtain EventStore Owner, Builds Owner, Solution Architect and Test Architect
    decisions/conformance for that same tuple. The Solution Architect's
    support-boundary disposition remains pending. Obtain independent Projects
    P1R acceptance; this package changes none of those decisions.

Remaining path: **authorized landing and exact-source CI → guarded publication
→ observed successor selection and qualification → P1R acceptance → P0/P2 → P3
→ architect sign-off → P4 → independent READY → Story 6.1 implementation**.
