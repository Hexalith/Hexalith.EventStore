---
created: 2026-07-15
story_id: "2.7"
story_key: 2-7-tenants-compatibility-and-package-mode-validation
status: review
split_from: 2-4-tenants-external-api-host-adoption
scope_split_to: 2-12-tenants-runtime-identity-adoption-and-package-mode-validation
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.7: Pre-Authorization Registration And Provenance Correction

Status: review

This child owns only the EventStore registration/proof-harness correction required before
Story 1.20 can select a runtime. It changes no Tenants, EventStore, or Builds dependency
identity. Authorized Tenants source/package adoption is owned by Story 2.12.

## Activation And Current Identity Gap

This story is the Story 1.20 prerequisite. It may correct only EventStore-owned
registration/proof-harness behavior and may not change Tenants, EventStore, or Builds
dependency identities. Story 2.12 remains `backlog` until Story 1.20 durably records
`final_decision: available`, `authorize_consumer_migration: true`, a 40-hex
`tested_runtime_sha`, named owner approval, and the approved package version and SHA-256
inventory.

At the 2026-07-17 audit, Tenants pins EventStore source commit
`a48580b1c8e43e2dd434771400c0d8008587d040` and Builds commit
`87d76ba79309f5ba86bd6cea7f2105100cabde09`, whose EventStore package version is `3.67.3`.
Neither identity is Story-1.20-approved migration evidence. Story 2.7 leaves both unchanged;
Story 2.12 owns their later authorized adoption. Current EventStore HEAD, a tag, or an
unapproved package version cannot substitute for the packet.

## Reproduced Pre-Authorization Blocker

At exact EventStore commit `772cdfefa8163704de0f57042af5b0507c1ac771`, the proof
packet's original package-mode integration build was invalid: it compiled the AppHost
without the conditional `tenants` resource, then waited for that absent resource. Rebuilding
the exact query-provenance E2E in source mode corrected the topology and failed twice at the
real assertion with HTTP 404 / `query_projection_missing`.

The source-mode logs show successful operational-metadata HTTP responses for Tenants. The
actual failure is the merged EventStore registration set: base `appsettings.json` still
registers sample domains `orders` and `inventory`, while the current sample service hosts
only `counter` and `greeting`. An absent configured binding makes
`AdminOperationalIndexHostedService` set `HasFailures`, log Event 6101, and atomically skip
all derived index writes. This also suppresses `admin:query-types:tenants`, so
`HandlerAwareQueryRouter` sees no `list-tenants` handler capability and falls back to a
nonexistent projection.

The correction must reconcile the stale sample registrations or their environment scope
without converting genuine endpoint, payload, capability, or transport failures into
partial success. The live source-topology test must then prove handler routing and
`HandlerComputed` provenance. This correction does not authorize a gitlink, package, or
container migration.

## Acceptance Boundary

1. Given the current EventStore source topology, when the live query-provenance E2E starts,
   then the AppHost is compiled with `UseHexalithProjectReferences=true`, the root-declared
   Tenants resource exists and becomes healthy, and no nested submodule initialization is
   required.
2. Given the configured sample and Tenants bindings, when operational metadata is loaded,
   then every configured binding maps to a domain actually hosted by its selected service,
   genuine metadata failures remain fail-closed, `admin:query-types:tenants` contains
   `list-tenants`, and the exact E2E returns 200 with `HandlerComputed` provenance and no
   projection validator leakage.
3. Given Story 1.20 is blocked, non-authorizing, incomplete, or lacks any required source,
   package, or approval identity, when AC1, AC2, and the scoped fail-closed boundary are
   satisfied, then Story 2.7 may enter `review` without changing any Tenants, EventStore, or
   Builds dependency identity, and existing rollback paths remain intact. Story 1.20
   authorization is not a prerequisite for review of this pre-authorization correction.
4. Given any compatibility failure beyond the scoped EventStore registration/harness correction
   requires a consumer behavioral or deployment-topology change, when it is found,
   then this story fails closed and routes that change to Story 2.12 when it belongs to
   authorized identity adoption, or to another separately approved story, instead of
   broadening Story 2.7 silently.

## Handoff

The pre-authorization correction closes the live source-topology prerequisite without any
dependency migration and may enter review while Story 1.20 remains non-authorizing. Story
1.20 then selects and approves the exact runtime identities. Story 2.12 alone owns the later
Tenants commit, source/package adoption, maintainer approval, and verification evidence.
