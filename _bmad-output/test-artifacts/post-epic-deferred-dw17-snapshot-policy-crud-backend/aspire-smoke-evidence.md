# DW17 Aspire Smoke Evidence

Date: 2026-05-21

## Runtime

- Command: `EnableKeycloak=false aspire run --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --detach --no-build --non-interactive --format Json`
- EventStore resource: `eventstore`, state `Running`, health `Healthy`, HTTP `http://localhost:8080`
- Admin.Server resource: `eventstore-admin`, state `Running`, health `Healthy`, HTTP `http://localhost:8090`
- DAPR components observed healthy: `statestore`, `pubsub`

The first detached run used stale Debug binaries and still returned `ErrorCode=Deferred`; the apphost was stopped, Debug binaries were rebuilt, and a fresh detached no-build run was started before the successful smoke below.

## Sanitized API Evidence

- `PUT http://localhost:8090/api/v1/admin/storage/tenant-a/orders/OrderAggregate/snapshot-policy?intervalEvents=10`
  - Result: `Success=true`
  - ErrorCode: `null`
  - OperationId shape: `snapshot-policy-set-<sha256>`

- `GET http://localhost:8090/api/v1/admin/storage/snapshot-policies?tenantId=tenant-a`
  - Matching persisted policy:
    - TenantId: `tenant-a`
    - Domain: `orders`
    - AggregateType: `OrderAggregate`
    - IntervalEvents: `10`

- `DELETE http://localhost:8090/api/v1/admin/storage/tenant-a/orders/OrderAggregate/snapshot-policy`
  - Result: `Success=true`
  - ErrorCode: `null`
  - OperationId shape: `snapshot-policy-delete-<sha256>`

- Follow-up `GET` confirmed matching policy count `0` for `tenant-a/orders/OrderAggregate`.

## Notes

- JWT token and signature were not recorded.
- Runtime command-threshold behavior is covered by focused unit tests for `SnapshotManager` and `AggregateActor`; this smoke covered live Admin.Server -> EventStore write/read/delete persistence through DAPR state.
- Apphost was stopped after evidence capture.
