---
title: Phase 4 architecture update and AD-26 owner decision (supersedes 2026-09-23 handoff)
date: 2026-10-07
status: pending-owner-ratification
architecture_sha256: 1ff06c5ecc94003765a2fa6fd39491d9ccdb6d5b09419fcf7c8755c30d2d31e5
assurance: single-maintainer-attested (required); no owner record issued
readiness: blocked
---

# Phase 4 architecture handoff, 2026-10-07

This handoff replaces [the 2026-09-23 handoff](phase-4-architecture-handoff-2026-09-23.md). That handoff bound spine SHA-256 `ba513f4d…`, which is now superseded, and it called AI tool-persona reviews `independent`. It cannot serve as a ratification record. Its history is kept unchanged.

## Owner direction recorded on 2026-10-07

- **AD-26: withheld.** The production target stays `[ASSUMPTION]`, the spine stays `status: draft`, and every production, readiness, and promotion gate stays blocked. Stories 3.19 and 4.16 remain blocked on ratification.
- **Inline assumptions: kept tagged.** The 11 inline `[ASSUMPTION]` clauses listed below stay in the spine for item-by-item owner review before Story 9.3 binds a baseline.

## Decision still required: AD-26

The owner holds both the Architecture role and the Platform deployment role. Under the AD-11 assurance level, the owner records one authenticated, content-bound record per role with exactly one of these outcomes.

| Outcome | What the record must contain |
| --- | --- |
| Ratify the target | The target as written in AD-26: self-managed Kubernetes, per-application DAPR sidecars, `statestore` on `state.postgresql` v1 with `actorStateStore: true`, OpenBao (AD-24), and OQ8 `oq8-postgresql-v1`. The record must also name four things. First, the durable broker; `deploy/dapr` has templates for `pubsub.kafka` and `pubsub.rabbitmq`, and `pubsub.azure.servicebus.topics` is cloud-managed. Second, the DAPR runtime pin, at least `1.18.3`; the current stable release is `1.18.4`. Third, the restore posture, including scheduler state. Fourth, the NFR7 class (c) envelope path, which is the Story 4.16 no-second-writer envelope. |
| Replace the target | The replacement deployment mode, actor state provider and version, OQ8 profile, broker, secret boundary, migration and compatibility impact, and the effect on AD-11 publication authority. AD-26 is amended before any downstream repin. |
| Keep withholding | No record. Name the evidence needed for a later decision. |

Each record carries the following:

- **Assurance label.** `single-maintainer-attested` while the registry names one human.
- **Timing.** It is created at least 24 hours after the last authored change to AD-26. That change was 2026-10-07.
- **Subject digest.** It binds the subject digest. The architecture whole-file SHA-256 is given in the frontmatter above. A section-scoped digest applies only if the owner adopts the AD-26 ratification assumption (row 7 below) and Story 9.3 defines how the section is canonicalized.
- **Evidence.** Tool-persona reviews are listed as evidence, never as approvals.

Ratification alone grants no release, deployment, traffic, readiness, or consumer-removal authority. `deploy/dapr/production-profile.yaml` is still absent.

## Inline assumptions awaiting owner review

| # | AD | Assumption | If rejected |
| --- | --- | --- | --- |
| 1 | AD-5 | Each actor-hosting app ID (EventStore, Operations, typed-reminder domain services) has its own `actorStateStore: true` component scoped to it alone, with its key prefix in the AD-26 digest. Other readers use separately scoped components, and AD-9 parity moves the AppHost. | The envelope needs another mechanism that excludes second writers at code level. Admin's private-key reads and local `keyPrefix: none` stay non-conformant either way. |
| 2 | AD-10 | The JWT contract defines versioned validation profiles: human bearer (incl. AD-29), AD-36 workload, and resource-bound delegation. Each has its own fingerprint. | AD-10's single rule set rejects valid workload assertions, so AD-10 and AD-36 must be re-scoped (deferred NM-3). |
| 3 | AD-11 | The Story 3.18 inventory is the sole NFR12 compatibility authority, and Story 2.15 writes into its schema. | Two compatibility manifests are possible, so name another single owner. |
| 4 | AD-12 | The seal comes from a Story 9.2 required transition workflow, so a truthful FAIL blocks only its transition. | The PRD "required, blocking run" stays in conflict with the truthful-FAIL rule. The product owner decides. |
| 5 | AD-17 | The route entry, generator metadata, and inventory entry derive the MessageId version, carry its digest, and fail activation on mismatch. | Choose another single carrier. The `Contracts` declaration stays the source. |
| 6 | AD-24 | Internal proofs move off the AD-25 digest ring to dedicated keys. | Digest-key retirement must keep counting gateway proofs as live references (already adopted). |
| 7 | AD-26 | Ratification binds a canonical AD-26 section digest recorded in the Story 9.3 manifest, not the whole file. | Every later spine edit invalidates ratification, and the 24-hour window restarts each time. |
| 8 | AD-27 | The platform-operation namespace is declared once in `Contracts`, cataloged in AD-33, and disjoint from AggregateIdentity. It is authorized externally only through the AD-10 human-bearer operation claim. | Story 2.14 needs another owner and carrier for platform scope. |
| 9 | AD-28 | Until actor-invocation restriction is qualified, actor methods that disclose, admit, or mutate validate one EventStore-issued context, fenced only for mutation. | Peers keep channel-only access to event reads, fences, and ETag regeneration. Record this as an accepted risk or choose another control. |
| 10 | AD-33 | Route entries declare the credential kind they admit and the AD-36 operation they require. | Receivers derive kind and operation some other way (deferred OQ-10 / adversarial M1). |
| 11 | AD-35 | Admin operations reached through McpCli end at Admin Server. | Name the admin edge McpCli uses. |

## Routed items (not decided by architecture)

| Owner | Item |
| --- | --- |
| Product owner (PRD) | (1) The Assurance Control's "required, blocking run" wording against the truthful-FAIL rule. (2) PRD §9.2 anonymous endpoints against UI static assets and login callbacks. (3) The NFR18 text still says "no story owns it", although Story 6.7 now does. (4) The glossary "DAPR Boundary" is narrower than §8.4. (5) The PRD §11.3 architecture digest is stale; refresh it only through Story 9.3. |
| Correct-course | Owning stories are needed for: the durable broker; the AD-33 catalog schema, codec, and envelope, ahead of Stories 2.14 and 2.15; the DAPR runtime pin; and the restore posture. An owner is also needed for the authenticated fallback, the gateway Dapr framework routes, peer ACL deny rules, and Operations caller-app-ID authorization (Story 5.7 is the candidate). Story 2.14's scope must extend to the gateway `ClaimsTenantValidator` and to migrating the existing `system` tenant and `RestTenantSource.System`. AD-34 through AD-36 must be propagated into the epics constraint lists in the same change that repins the digest. |
| Story 5.5 | Re-reconcile AD-36 when the story reaches `done`, binding the stable header, claims, and lifetime contract at that point. Confirm the stale-`304` risk under `Direct` with a test. |
| Builds owner | FrontComposer `4.6.0` and Fluent UI GA alignment. DAPR CLI `1.18.2`. `ReleaseEvidenceCodec`, with Story 3.19. |
| Owner | MediatR `14.2.0` license posture before the next release. |
| Platform documentation | The Kubernetes guide pins `1.14.4`. `deploy/README.md` pins `daprd` `1.18.0`. The Compose guide uses `latest` tags. `deploy/dapr/*.yaml` uses `{env:...}` interpolation where `secretKeyRef` is needed. |

## Validation and evidence

- **Lint.** `uv run .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` returned `ok: true` with 0 findings on the final bytes. `git diff --check` reported no whitespace errors.
- **Input reconciliation.** These were run before the update:
  - [proposals 2026-09-26 to 2026-09-30](reconcile-update-2026-10-07-proposals-0926-0930.md)
  - [Dapr boundary](reconcile-update-2026-10-07-dapr-boundary.md)
  - [2026-10-07 correct-course](reconcile-update-2026-10-07-correct-course.md)
  - [Story 5.5](reconcile-update-2026-10-07-story-5-5.md)
  - [Stack reality](reconcile-update-2026-10-07-stack-reality.md)
- **Reviewer gate.** All of these are tool-persona evidence only:
  - [reconcile closure](review-update-2026-10-07-reconcile-closure.md)
  - [rubric](review-update-2026-10-07-rubric-walker.md)
  - [technology reality](review-update-2026-10-07-technology-reality.md)
  - [adversarial](review-update-2026-10-07-adversarial-divergence.md)
  - [gate closure](review-update-2026-10-07-gate-closure.md)

  The gate found 2 critical and 20 high findings. All critical and high findings are closed in the final bytes. The deferred mediums are in the memlog with revisit triggers.
- **Preservation.** Only `architecture.md`, the run memlog (append-only), and the review files in this folder were written. PRD, epics, sprint status, and other planning files were not edited. Nothing was committed or pushed.
