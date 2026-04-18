[<- Back to Hexalith.EventStore](../../README.md)

# Disaster Recovery Procedure

This page covers backup strategies, recovery procedures, and data verification for the Hexalith.EventStore across all supported DAPR state store backends. Whether you are running a local development environment with Redis, an on-premise production deployment with PostgreSQL, or a cloud deployment with Azure Cosmos DB, you will find step-by-step procedures to recover from data loss scenarios.

> **Prerequisites:** [Prerequisites and Local Dev Environment](../getting-started/prerequisites.md), [Deployment Progression Guide](deployment-progression.md)

## v1 GA Scope and SLA Carve-Out

Hexalith.EventStore v1 ships **without a product-level disaster recovery SLA**. The framework guarantees zero loss for committed events at the application layer (persist-then-publish, write-once event keys, checkpointed state machine — see [Reliability NFRs](../../_bmad-output/planning-artifacts/prd.md)), but **infrastructure-level RTO/RPO is the operator's responsibility** and inherits the characteristics of the chosen DAPR state store backend.

What v1 provides:

- **Backend-native recovery primitives** — fully documented per backend in this guide (Redis RDB/AOF, PostgreSQL WAL/PITR, Azure Cosmos DB continuous backup).
- **Application-layer integrity guarantees** — every committed event is durable in the state store before any side effect; restoring the state store deterministically restores the system.
- **Operational runbook** — four scenario-driven recovery procedures with explicit verification steps (see [Disaster Recovery Runbook](#disaster-recovery-runbook)).
- **Data verification toolkit** — SQL queries that detect sequence gaps, metadata drift, and tenant leakage post-restore.

What v1 explicitly **does not** provide:

- **Contractual RTO/RPO** — the targets in the table below are *operator-achievable* with correct backend configuration; they are not guaranteed by the EventStore product.
- **Automated failover orchestration** — multi-region/AZ failover relies on the underlying backend's capabilities (e.g. Cosmos DB automatic failover, PostgreSQL replication failover); EventStore does not automate cross-region promotion.
- **Backup immutability enforcement** — operators must configure write-once protection (S3 Object Lock, Azure immutable blobs, PostgreSQL WAL archive write-protection) at the infrastructure layer.
- **Automated restore-integrity testing** — EventStore ships verification SQL queries; periodic snapshot→restore→replay→hash drills are an operator process.

This carve-out closes architecture **GAP-14** by making the v1 scope explicit. **v2 will introduce** a product-level DR SLA, automated multi-region failover, an automated restore-integrity test in the Tier-3 chaos suite, and backup-immutability validation — see the [roadmap](../community/roadmap.md).

> **Customer guidance:** If your deployment requires a contractual DR SLA for v1, treat the operator-provided RTO/RPO targets as a *capability statement*, not a *commitment*, and validate them in your environment before signing off. For regulated workloads requiring guaranteed RTO/RPO, defer adoption to v2 or contract directly for a managed-service tier.

## Understanding the Data Model

Before planning your backup strategy, you need to understand what the event store persists and what is critical for recovery.

Hexalith.EventStore uses a DAPR state store — a database backend configured through a YAML component file — to persist all event data. A DAPR sidecar (a helper process running alongside your application) translates state store operations into backend-specific database calls, so the same key patterns apply regardless of which database you use.

The event store persists four types of data:

| Data Type | Key Pattern | Mutability | Recovery Priority |
|-----------|-------------|------------|-------------------|
| Events | `{tenant}:{domain}:{aggregateId}:events:{sequence}` | Write-once, immutable | **Critical** — source of truth |
| Metadata | `{tenant}:{domain}:{aggregateId}:metadata` | Updated with ETag concurrency | **High** — tracks sequence numbers |
| Snapshots | `{tenant}:{domain}:{aggregateId}:snapshot` | Updated periodically | **Low** — can be rebuilt from events |
| Command Status | `{tenant}:{correlationId}:status` | TTL 24 hours, ephemeral | **None** — auto-expires, advisory only |

**Events are the only critical data.** Each event is written once and never modified. They are the single source of truth for all aggregate state. Metadata tracks the latest sequence number per aggregate but can be reconstructed by scanning events. Snapshots are a performance optimization — the system rebuilds them automatically by replaying events when a snapshot is missing or stale. Command status entries expire after 24 hours and exist only for client polling.

The event store follows a persist-then-publish pattern: events are first written to the state store (the database), and only then published to a pub/sub system (DAPR's event distribution mechanism) for downstream consumers. This means the state store always contains the complete event history. If the pub/sub system fails, events remain safe in the state store and can be republished.

## RTO/RPO Considerations

Recovery Time Objective (RTO) is the maximum acceptable downtime — how quickly you must restore service. Recovery Point Objective (RPO) is the maximum acceptable data loss — how much recent data you can afford to lose.

The event store architecture mandates zero data loss for committed events. Your RTO/RPO depends on which state store backend you use:

| Backend | Environment | RPO | RTO | Zero Data Loss |
|---------|-------------|-----|-----|----------------|
| Redis | Development | Last RDB snapshot or AOF sync | Container restart (seconds) | No — development only |
| PostgreSQL | On-premise production | Last WAL commit (near-zero with synchronous replication) | Backup restore + WAL replay (minutes to hours) | Yes — with WAL archiving |
| Azure Cosmos DB | Cloud production | Configurable (continuous backup = seconds) | Automatic failover (minutes with multi-region) | Yes — with continuous backup |

> **Note:** The zero data loss requirement applies to production deployments. For development environments using Redis, data loss on container restart is expected behavior.

## Redis Backup and Recovery (Development)

Redis serves as the state store backend for local Docker Compose development environments. It is not recommended for production event store data due to limited durability guarantees.

### Redis Persistence Options

Redis supports two persistence mechanisms:

- **RDB (Redis Database):** Point-in-time snapshots saved at configurable intervals. Fast to restore but you lose changes since the last snapshot.
- **AOF (Append Only File):** Logs every write operation. More durable than RDB but larger files and slower restarts.

> **Note:** The default Docker Compose development setup may run Redis without persistence enabled. In this case, all data is lost on container restart. Re-run the sample application to regenerate test data.

### Backup Procedure

1. Trigger an RDB snapshot:

    ```bash
    $ docker exec <redis-container> redis-cli BGSAVE
    ```

2. Wait for the save to complete:

    ```bash
    $ docker exec <redis-container> redis-cli LASTSAVE
    ```

3. Copy the dump file from the container:

    ```bash
    $ docker cp <redis-container>:/data/dump.rdb ./backup/dump.rdb
    ```

For AOF backups, copy the `appendonly.aof` file:

```bash
$ docker cp <redis-container>:/data/appendonly.aof ./backup/appendonly.aof
```

### Restore Procedure

1. Stop the Redis container:

    ```bash
    $ docker compose stop redis
    ```

2. Replace the data file with your backup:

    ```bash
    $ cp ./backup/dump.rdb <redis-data-volume>/dump.rdb
    ```

3. Restart the Redis container:

    ```bash
    $ docker compose up -d redis
    ```

4. Verify the data was restored:

    ```bash
    $ docker exec <redis-container> redis-cli DBSIZE
    ```

> **Tip:** For local development, the simplest recovery is often to restart the sample application, which recreates the counter domain test data automatically.

## PostgreSQL Backup and Recovery (Production)

PostgreSQL is the recommended state store backend for on-premise production deployments. It provides full ACID durability, WAL-based point-in-time recovery, and mature backup tooling.

### DAPR State Store Table Structure

DAPR creates a `state` table in the configured database to store all event store data:

```sql
-- DAPR state store table structure
CREATE TABLE state (
    key TEXT NOT NULL PRIMARY KEY,
    value JSONB NOT NULL,
    etag TEXT NOT NULL,
    insertdate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updatedate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expiredate TIMESTAMP WITH TIME ZONE
);
```

All event store keys — events, metadata, snapshots, and command status — are rows in this table. The `key` column contains the composite key pattern described in the data model section.

### Backup Strategies

#### Logical Backup with pg_dump

Use `pg_dump` for portable, human-readable backups. Suitable for smaller deployments or one-time migrations:

```bash
# Full database backup (custom format for flexible restore)
$ pg_dump -h <host> -U <user> -d <dapr-database> -Fc -f eventstore-backup.dump

# Backup only the state table
$ pg_dump -h <host> -U <user> -d <dapr-database> -t state -Fc -f eventstore-state-backup.dump
```

#### Physical Backup with pg_basebackup

Use `pg_basebackup` for fast, binary-level backups with WAL archiving. Recommended for production:

```bash
# Full physical backup with WAL files
$ pg_basebackup -h <host> -U <replication-user> -D /backup/base -Ft -z -Xs
```

#### Point-in-Time Recovery with WAL Archiving

For zero data loss, configure continuous WAL archiving. This captures every transaction and allows restoring to any point in time:

1. Enable WAL archiving in `postgresql.conf`:

    ```text
    wal_level = replica
    archive_mode = on
    archive_command = 'cp %p /archive/wal/%f'
    ```

2. Take a base backup as the starting point:

    ```bash
    $ pg_basebackup -h <host> -U <replication-user> -D /backup/base -Ft -z -Xs
    ```

3. WAL files are continuously archived to `/archive/wal/` and can replay transactions up to the last committed write.

### Restore Procedure

Follow these steps to restore the event store from a PostgreSQL backup:

1. **Stop the EventStore application** to prevent new writes during restoration:

    ```bash
    $ kubectl scale deployment eventstore --replicas=0 -n hexalith
    ```

2. **Restore from logical backup** (pg_dump):

    ```bash
    # Restore full database
    $ pg_restore -h <host> -U <user> -d <dapr-database> -c eventstore-backup.dump

    # Or restore only the state table
    $ pg_restore -h <host> -U <user> -d <dapr-database> -t state eventstore-state-backup.dump
    ```

3. **Or restore with point-in-time recovery** (PITR):

    ```bash
    # Create recovery.signal file
    $ touch /var/lib/postgresql/data/recovery.signal
    ```

    Add to `postgresql.conf`:

    ```text
    restore_command = 'cp /archive/wal/%f %p'
    recovery_target_time = '2026-03-01 14:30:00 UTC'
    ```

    Start PostgreSQL — it replays WAL files up to the target time.

4. **Verify event stream integrity** (see [Data Verification Procedures](#data-verification-procedures) below).

5. **Restart the EventStore application:**

    ```bash
    $ kubectl scale deployment eventstore --replicas=1 -n hexalith
    ```

6. **Verify actor rehydration** by sending a test command and confirming the aggregate loads its state from restored events.

## Azure Cosmos DB Backup and Recovery (Cloud)

Azure Cosmos DB is the state store backend for Azure Container Apps cloud deployments. It provides built-in redundancy, automatic backups, and multi-region failover.

### Automatic Backup Modes

Azure Cosmos DB offers two backup modes:

- **Periodic backup** (default): Automatic backups at configurable intervals (default: every 4 hours). Retains 2 copies by default. Restore requires an Azure support request.
- **Continuous backup:** Point-in-time restore within a retention window (7 or 30 days). Self-service restore via Azure Portal or CLI.

> **Tip:** For production event stores, enable continuous backup mode to achieve near-zero RPO and self-service recovery.

### Configuring Continuous Backup

Enable continuous backup on your Cosmos DB account:

```bash
$ az cosmosdb update \
    --name <cosmos-account> \
    --resource-group <resource-group> \
    --backup-policy-type Continuous \
    --continuous-tier Continuous7Days
```

### Restore Procedure

Cosmos DB restores create a new account — you cannot restore in-place. Follow these steps:

1. **Identify the target restore point** — the timestamp just before data loss occurred:

    ```bash
    # Check the restorable timestamp range
    $ az cosmosdb restorable-database-account list \
        --account-name <cosmos-account>
    ```

2. **Create a restore request:**

    ```bash
    $ az cosmosdb restore \
        --account-name <cosmos-account> \
        --resource-group <resource-group> \
        --target-database-account-name <restored-account> \
        --restore-timestamp "2026-03-01T14:30:00Z" \
        --location <region>
    ```

3. **Wait for the restore to complete** (this may take several minutes to hours depending on data size):

    ```bash
    $ az cosmosdb show \
        --name <restored-account> \
        --resource-group <resource-group> \
        --query "provisioningState"
    ```

4. **Update the DAPR state store component** to point to the restored account. Update the component YAML with the new Cosmos DB endpoint:

    ```yaml
    # deploy/dapr/statestore-cosmosdb.yaml
    metadata:
        - name: url
          value: "https://<restored-account>.documents.azure.com:443/"
    ```

5. **Verify event stream integrity** (see [Data Verification Procedures](#data-verification-procedures) below).

6. **Switch traffic to the restored instance** by restarting the EventStore application with the updated component configuration.

### Multi-Region Failover

Azure Cosmos DB supports automatic failover for multi-region deployments:

```bash
# Enable automatic failover
$ az cosmosdb update \
    --name <cosmos-account> \
    --resource-group <resource-group> \
    --enable-automatic-failover true
```

With automatic failover enabled, Cosmos DB promotes a secondary region if the primary becomes unavailable. RTO is typically minutes with no data loss for reads. Write availability depends on your consistency level configuration.

## Data Verification Procedures

After restoring from any backup, verify the integrity of your event store data before resuming normal operations.

### Event Stream Integrity Checks

1. **Check aggregate metadata consistency.** Verify that metadata sequence numbers match the actual event count for each aggregate:

    For PostgreSQL:

    ```sql
    -- Find aggregates where metadata sequence doesn't match event count
    SELECT
        m.key AS metadata_key,
        m.value->>'sequence' AS metadata_sequence,
        COUNT(e.key) AS actual_event_count
    FROM state m
    LEFT JOIN state e ON e.key LIKE
        REPLACE(m.key, ':metadata', ':events:%')
    WHERE m.key LIKE '%:metadata'
    GROUP BY m.key, m.value->>'sequence'
    HAVING (m.value->>'sequence')::int != COUNT(e.key);
    ```

2. **Verify event key continuity.** Check for gaps in the event sequence for each aggregate:

    For PostgreSQL:

    ```sql
    -- Find event sequence gaps per aggregate
    WITH event_keys AS (
        SELECT
            key,
            REGEXP_REPLACE(key, ':events:\d+$', '') AS aggregate_prefix,
            (REGEXP_MATCH(key, ':events:(\d+)$'))[1]::int AS seq
        FROM state
        WHERE key LIKE '%:events:%'
    )
    SELECT
        aggregate_prefix,
        seq AS missing_after,
        seq + 1 AS expected_next
    FROM event_keys ek
    WHERE NOT EXISTS (
        SELECT 1 FROM event_keys ek2
        WHERE ek2.aggregate_prefix = ek.aggregate_prefix
        AND ek2.seq = ek.seq + 1
    )
    AND seq < (
        SELECT MAX(seq) FROM event_keys ek3
        WHERE ek3.aggregate_prefix = ek.aggregate_prefix
    )
    ORDER BY aggregate_prefix, seq;
    ```

3. **Test actor rehydration.** Activate a known aggregate by sending a command and verify the actor successfully loads its state from the restored events. A successful response confirms the event stream is intact.

4. **Verify snapshot consistency.** If snapshots are present, confirm they reflect the correct state at their recorded sequence number. Snapshots that are inconsistent will be automatically replaced on the next snapshot interval — this is self-healing and not a blocking issue.

### Tenant Isolation Verification

Confirm that no cross-tenant data leakage occurred during the restore:

For PostgreSQL:

```sql
-- List distinct tenants in the state store
SELECT DISTINCT SPLIT_PART(key, ':', 1) AS tenant
FROM state
WHERE key LIKE '%:events:%'
ORDER BY tenant;
```

Verify the tenant list matches your expected tenant set. If unexpected tenants appear, investigate the backup source.

### Azure Cosmos DB Verification

For Cosmos DB, use the Azure Portal Data Explorer or SDK queries to verify:

```sql
-- Count all restored documents
SELECT VALUE COUNT(1) FROM c

-- Inspect restored keys for a specific tenant prefix
SELECT TOP 100 c.id
FROM c
WHERE STARTSWITH(c.id, "tenant-a:")
```

> **Warning:** Never log event payloads during verification. Event data may contain sensitive information. Verify using key patterns and counts only.

## Pub/Sub Recovery

The pub/sub system (DAPR's event distribution mechanism) is **not** the source of truth — the state store is. Events published through pub/sub can always be reconstructed from the state store.

### How Missed Deliveries Are Handled

During an outage, pub/sub messages may be lost. The event store handles this through multiple layers:

- **DAPR retry policies:** Configured in the resiliency component, DAPR automatically retries failed pub/sub deliveries with exponential backoff.
- **Dead-letter topics:** Messages that exhaust all retries are routed to dead-letter topics (`deadletter.{tenant}.{domain}.events`) for manual inspection and reprocessing.
- **Persist-then-publish guarantee:** Events are always written to the state store before publication. If publication fails, the event is safe and the checkpointed state machine marks it for republication.

### Republishing Events

If events were persisted but never published (for example, due to a pub/sub outage during the publish phase), the event store handles this automatically. When an actor is reactivated, the checkpointed state machine detects events in the `EventsStored` state (persisted but not yet published) and republishes them.

To trigger republication for a specific aggregate, send any command targeting that aggregate. The actor activation will:

1. Rehydrate state from the event store
2. Detect any unpublished events via checkpoint state
3. Republish those events to the correct tenant and domain topics

### Dead-Letter Recovery

Inspect dead-letter topics for failed messages:

```bash
# Check dead-letter topic for a specific tenant and domain
# Topic pattern: deadletter.{tenant}.{domain}.events
```

Dead-letter messages retain the full CloudEvents 1.0 envelope, including the original event data, correlation ID, and failure reason. Process them by fixing the underlying subscriber issue and then resubmitting.

## Backup Immutability (Operator Responsibility)

A backup is only as trustworthy as its protection against tampering and accidental deletion. Ransomware, insider threats, and operator error can destroy mutable backups in seconds — making the original event store unrecoverable. Configure write-once protection at the storage layer for every production backup destination.

> **v1 scope reminder:** EventStore does not enforce immutability — see [v1 GA Scope and SLA Carve-Out](#v1-ga-scope-and-sla-carve-out). The configuration below is the operator's responsibility, and v2 will introduce a startup-time check that verifies the configured backup destination has immutability enabled.

### PostgreSQL WAL Archive Protection

WAL archives are continuously written by PostgreSQL and must remain unmodified once archived. Two layers of protection are recommended:

1. **Filesystem-level write-once permissions.** After a WAL file lands in the archive directory, strip write permission so neither the database user nor backup tooling can mutate it:

    ```bash
    # In postgresql.conf — script runs after WAL file is archived
    archive_command = 'cp %p /archive/wal/%f && chmod 0440 /archive/wal/%f'
    ```

2. **Off-host immutable storage.** Replicate WAL files to immutable object storage as soon as they are written:

    - **AWS S3 with Object Lock (Compliance mode):** prevents deletion until the retention period expires, even by the root account.

        ```bash
        $ aws s3api put-object-lock-configuration \
            --bucket eventstore-wal-archive \
            --object-lock-configuration '{"ObjectLockEnabled":"Enabled","Rule":{"DefaultRetention":{"Mode":"COMPLIANCE","Days":30}}}'
        ```

    - **Azure Blob Storage with immutable policies (legal hold or time-based):**

        ```bash
        $ az storage container immutability-policy create \
            --resource-group <rg> --account-name <storage> \
            --container-name wal-archive --period 30
        ```

3. **Base backups (`pg_basebackup` output)** should land in the same immutable destination — the WAL archive alone is insufficient without a base.

### Azure Cosmos DB Backup Immutability

Cosmos DB **continuous backup** is automatically write-protected by the Azure platform — restore points within the retention window cannot be deleted by operators. To harden further:

- **Enforce continuous backup at account creation** (not periodic) and disable downgrade via Azure Policy:

    ```bash
    $ az cosmosdb update \
        --name <cosmos-account> --resource-group <rg> \
        --backup-policy-type Continuous \
        --continuous-tier Continuous30Days
    ```

- **Lock the resource group** to prevent account deletion:

    ```bash
    $ az lock create --name "eventstore-cosmos-no-delete" \
        --resource-group <rg> --lock-type CanNotDelete
    ```

- For **periodic backup mode** (default), backups are platform-managed but invisible to the customer — restore requires an Azure support request. Treat this as a *last-resort* tier and prefer continuous backup for production.

### Redis Backup Immutability (Development Only)

Redis backups (`dump.rdb`, `appendonly.aof`) are file copies and inherit zero immutability protection. Because Redis is **not recommended for production event store data** (see the [RTO/RPO table](#rtorpo-considerations)), no operational immutability requirement applies. If you must persist development backups for regression testing, store them under filesystem write-once permissions (`chmod 0440`) on a separate host.

### Restore-Integrity Verification

The [Data Verification Procedures](#data-verification-procedures) section provides SQL queries to detect sequence gaps, metadata drift, and tenant leakage **after** a restore. For v1, operators should run these queries as part of every restore drill. Run a full restore drill in a non-production environment at least quarterly:

1. Snapshot a known-good production backup
2. Restore into an isolated environment (separate network, separate DAPR sidecar)
3. Run all queries from the [Event Stream Integrity Checks](#event-stream-integrity-checks) section
4. Verify a sample of aggregates rehydrates and accepts a no-op command
5. Compare a SHA-256 hash of the restored event payload set against the source

> v2 will automate steps 1-5 as a Tier-3 chaos test.

## Disaster Recovery Runbook

This section provides condensed step-by-step procedures for common disaster scenarios.

### Scenario 1: State Store Data Loss (Full Database Loss)

**Detection signals:** Application errors on all command submissions, actor activation failures, database connection errors.

**Immediate actions:**

1. Stop the EventStore application to prevent error cascading
2. Assess the scope of data loss (full database vs. partial)
3. Identify the most recent viable backup

**Recovery steps:**

1. Restore the database from the latest backup (see backend-specific sections above)
2. For PostgreSQL with WAL archiving: use PITR to restore to the last committed transaction
3. For Azure Cosmos DB with continuous backup: restore to a point just before the incident
4. Run [data verification procedures](#data-verification-procedures) on the restored database
5. Restart the EventStore application
6. Monitor actor rehydration and verify command processing resumes

**Verification:** Send test commands to known aggregates and confirm state matches expectations.

### Scenario 2: Partial Data Corruption (Specific Tenant or Aggregate)

**Detection signals:** Errors for specific aggregates or tenants while others work normally, ETag mismatch errors, unexpected sequence numbers.

**Immediate actions:**

1. Identify the affected tenant and aggregate IDs from error logs
2. Assess whether corruption is in events (critical) or metadata/snapshots (recoverable)

**Recovery steps:**

1. If only metadata or snapshots are corrupted:
    - Delete the corrupted metadata/snapshot keys
    - The actor will rebuild metadata from events and create a new snapshot on next activation
2. If events are corrupted:
    - Restore the affected keys from backup using backend-specific tools
    - For PostgreSQL: restore specific rows using `pg_restore` with table-level filtering
    - For Cosmos DB: perform a point-in-time restore and extract the needed documents
3. Run [data verification procedures](#data-verification-procedures) on the affected aggregates
4. Reactivate the affected actors by sending commands

**Verification:** Verify the affected aggregates load correctly and their event sequences are complete.

### Scenario 3: Pub/Sub System Failure (Messages Lost, State Store Intact)

**Detection signals:** Downstream consumers report missing events, dead-letter topics accumulating messages, pub/sub health checks failing.

**Immediate actions:**

1. Confirm the state store is healthy (events are persisted and intact)
2. Check dead-letter topics for queued messages
3. Assess whether the pub/sub infrastructure itself needs recovery

**Recovery steps:**

1. Fix the pub/sub infrastructure issue (restart brokers, restore configuration)
2. Check the dead-letter topics and reprocess any failed messages
3. For events stuck in `EventsStored` checkpoint state: reactivate the affected actors by sending commands — the state machine will automatically republish
4. Verify downstream consumers received all events by comparing sequence numbers

**Verification:** Confirm downstream consumers have received events up to the latest sequence number for each aggregate.

### Scenario 4: Complete Environment Failure (All Infrastructure Lost)

**Detection signals:** Total infrastructure loss (datacenter failure, cloud region outage, catastrophic configuration error).

**Immediate actions:**

1. Activate your disaster recovery plan and notify stakeholders
2. Identify off-site backup locations and their freshness
3. Provision replacement infrastructure

**Recovery steps:**

1. Deploy fresh infrastructure using your deployment guides:
    - [Docker Compose](deployment-docker-compose.md) for development
    - [Kubernetes](deployment-kubernetes.md) for on-premise production
    - [Azure Container Apps](deployment-azure-container-apps.md) for cloud production
2. Restore the state store database from off-site backup
3. Deploy DAPR component configurations (state store, pub/sub, resiliency)
4. Deploy the EventStore application
5. Run [data verification procedures](#data-verification-procedures)
6. Reactivate actors and verify command processing
7. Verify pub/sub subscriptions and downstream consumer connectivity

**Verification:** End-to-end test: submit a new command, verify it processes successfully, verify the event is published to subscribers, verify the aggregate state is correct.

## Next Steps

- **Related:** [Troubleshooting Guide](troubleshooting.md) — operational error resolution
- **Related:** [DAPR Component Configuration Reference](dapr-component-reference.md) — state store backend configuration details
- **Related:** [Security Model](security-model.md) — security considerations during recovery operations
- **Related:** [Deployment Progression Guide](deployment-progression.md) — environment comparison and progression path
- **Individual deployment guides** for environment-specific setup:
    - [Docker Compose Deployment Guide](deployment-docker-compose.md)
    - [Kubernetes Deployment Guide](deployment-kubernetes.md)
    - [Azure Container Apps Deployment Guide](deployment-azure-container-apps.md)
