---
created: 2026-07-15
story_id: "1.20"
story_key: 1-20-owner-approved-parity-closure-and-runtime-pin
status: ready-for-dev
supersedes: 1-15-owner-approved-parity-closure-and-runtime-pin.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.20: Owner-Approved Parity Closure And Runtime Pin

Status: ready-for-dev

## Reissue Decision

This is the active identity for the unstarted historical Story 1.15. The earlier file
retains its discovery notes. Execution must re-read current status and evidence rather
than treating story creation as owner approval.

## Acceptance Boundary

1. Stories 1.14-1.19 are complete and reviewed, and Story 1.2 platform provenance is
   complete before lifecycle/provenance evidence is accepted.
2. Every parity capability is classified `available` or the packet remains `still blocked`;
   no partial consumer migration is authorized.
3. Evidence records source/test paths, exact commands, persisted read-back, environment,
   limitations, and rollback guidance; mock-only or HTTP-only proof cannot close a row.
4. A named EventStore owner reviews the completed exact-SHA evidence and records approval,
   date, durable source, accepted scope, limitations, and migration decision.
5. Under AD-22, the packet distinguishes and pins: the exact EventStore source commit;
   exact NuGet package IDs, versions, and hashes; and the exact container repository,
   immutable digest, and platform set. One identity must never stand in for another.
6. Consumer repositories verify both gitlink and checkout against the approved source SHA,
   or exact package/container identities when that is the approved consumption mode.
7. Any unresolved prerequisite, review, identity, production proof, or owner decision leaves
   the packet `still blocked`, Story 1.20 non-`done`, and Epic 1 `in-progress`.

Produces: `1-20-owner-approved-parity-closure-proof-packet.md`.
