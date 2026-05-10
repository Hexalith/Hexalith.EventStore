<!-- evidence-validator: skip -->

# Admin Operational Index Populators Evidence

Date: 2026-05-10
Mode: `EnableKeycloak=false` Aspire dev mode

## Run Summary

- Redis was flushed with `docker exec dapr_redis redis-cli FLUSHALL`.
- Aspire was restarted after the flush with `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`.
- Seeded three `IncrementCounter` commands for `tenant-a/counter/counter-1`; all completed. See `seed-summary.json`.

## AC Mapping

- AC1/AC2: `api-projections-tenant-a.json`, `redis-admin-projections-all.txt`, `redis-admin-projections-tenant-a.txt`, and `api-consistency-result.json`.
- AC3: `api-types-events-counter.json`, `api-types-commands-counter.json`, `api-types-aggregates-counter.json`, and matching `redis-admin-type-catalog-*.txt` files.
- AC4: `api-storage-overview-tenant-a.json`, `api-storage-hot-streams-tenant-a.json`, `redis-admin-storage-overview-tenant-a.txt`, `redis-admin-storage-hot-streams-tenant-a.txt`, `redis-admin-storage-stream-count-tenant-a.txt`, and `redis-admin-stream-activity-all.txt`.
- AC7: `redis-admin-key-list.txt` lists the populated `admin:projections:*`, `admin:type-catalog:*`, `admin:storage-overview:*`, `admin:storage-hot-streams:*`, and `admin:storage-stream-count:*` keys.

## Issue #17 Retest

Consistency check `01KR8N6MTQJGVF7BNN9ZZEAWV5` completed after the projection index existed. The missing projection-index false positive is resolved, but one residual warning remains: domain-specific projection position validation is not granular. That follow-up is recorded in `deferred-work.md`.
