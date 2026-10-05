# Story 6.6 V1 logical-read and cancellation slice — 2026-10-05

This is partial implementation evidence at `d48e1aeb9930cb6dc69ce61944d6e1ea27afc21f` plus the current working-tree changes. It does not close an M1–M8 task or authorize V2 writes.

## Changes

- Aggregate actor rehydration checks the application-owned logical digest after unprotection and before domain invocation. Historical V1 events without this additive digest remain readable.
- The addressed event-stream reader rejects a stored event whose tenant, domain, aggregate, sequence or payload disagrees with the requested key.
- `EventPersister` checks cancellation before staging, passes the caller token to Dapr actor-state reads and writes, and checks sequence overflow before position reservation. `AggregateActor` now forwards its caller token to the persister.

## Verification

- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Actors.AggregateActorDomainResultTests` — 35 passed, zero failed or skipped.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll` — 3,622 total, zero failed, 25 skipped. The skip count includes existing DW1 red-phase tests and is not Story 6.6 acceptance evidence.
- `git diff --check` — passed.

## Open acceptance

The production evolution reader is not integrated through replay, projections, subscriptions, operator inspection and all query consumers. The Dapr control/recovery, compatibility, two-host, package-only and live-sidecar matrices remain unverified. `deploy/dapr/production-profile.yaml` and `scripts/verify-event-evolution.py` are absent. The prior PostgreSQL feasibility evidence belongs to the superseded design. V2 admission remains fenced, and the story stays `in-progress`.
