---
created: 2026-07-15
story_id: "2.7"
story_key: 2-7-tenants-compatibility-and-package-mode-validation
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.7: Tenants Compatibility And Package-Mode Validation

Status: review

The parent Story 2.4 spec records Debug/source and Release/package validation. This child
reviews exact EventStore/Tenants SHAs in source mode and exact package versions/hashes in
package mode, with no mixed Gateway-source/DomainService-package graph. `done` requires the
Tenants maintainer-approved repository boundary, exact SHA, compatibility disposition, and
focused restore/build/test evidence. Incomplete identity evidence remains fail-closed.

## Activation And Current Identity Gap

This story is the sole Tenants compatibility and adoption owner; no duplicate Tenants-local
story is required. Its work has two fail-closed phases:

1. The pre-authorization compatibility correction may run now and is a Story 1.20
   prerequisite. It may correct only EventStore-owned registration/proof-harness behavior
   and may not change Tenants, EventStore, or Builds dependency identities.
2. Runtime adoption remains in `review` until Story 1.20 durably records
   `final_decision: available`, `authorize_consumer_migration: true`, a 40-hex
   `tested_runtime_sha`, named owner approval, and the approved package version and SHA-256
   inventory.

At the 2026-07-17 audit, Tenants pins EventStore source commit
`a48580b1c8e43e2dd434771400c0d8008587d040` and Builds commit
`87d76ba79309f5ba86bd6cea7f2105100cabde09`, whose EventStore package version is `3.67.3`.
Neither identity is Story-1.20-approved migration evidence. Current EventStore HEAD, a tag,
or an unapproved package version cannot substitute for the packet.

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
   package, or approval identity, when this story is reviewed, then it remains `review`, no
   Tenants/EventStore/Builds gitlink is changed, and existing rollback paths remain intact.
4. Given Story 1.20 authorizes migration and names the approved EventStore source SHA, when
   Debug/source mode is adopted, then `references/Hexalith.EventStore` gitlink and checkout
   both equal that SHA, no EventStore submodule content is edited, and only Tenants-root-declared
   submodules are initialized.
5. Given the approved package version and hashes, when Release/package mode restores from an
   isolated cache, then every resolved `Hexalith.EventStore*` asset is a package at the exact
   version, fetched bytes match the approved hashes, and the selected Builds commit already
   exposes that version.
6. Given Gateway is in the EventStore release manifest, when the dependency graph is aligned,
   then `Hexalith.EventStore.Gateway` follows the same conditional source/package policy as
   DomainService and the Release assets contain no mixed Gateway-project/DomainService-package
   graph or any EventStore project reference.
7. Given source and package modes are aligned, when validation runs, then Tenants preserves its
   domain-service, AppHost, and UI registration and passes the focused source/package restore,
   build, projection/query/provenance/freshness, and package-compatibility evidence.
8. Given any compatibility failure beyond the scoped EventStore registration/harness correction
   requires a consumer behavioral or deployment-topology change, when it is found,
   then this story fails closed and routes that change to a separately approved story instead of
   broadening the adoption silently.

## Handoff

The pre-authorization correction closes the live source-topology prerequisite without any
dependency migration. The later closing Tenants commit updates only to the identities
approved by Story 1.20, records the Tenants maintainer's exact accepted commit, and links its
source/package verification evidence. Only that adoption phase is downstream of Story 1.20.
