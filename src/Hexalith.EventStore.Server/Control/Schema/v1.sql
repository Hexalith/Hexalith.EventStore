CREATE SCHEMA hexalith_eventstore_control;
CREATE TABLE hexalith_eventstore_control.rows (
    namespace text COLLATE "C" NOT NULL
        CHECK (namespace = 'eventstore.control.v1'),
    address text COLLATE "C" NOT NULL
        CHECK (octet_length(address) BETWEEN 1 AND 256),
    generation numeric(20,0) NOT NULL
        CHECK (generation BETWEEN 0 AND 18446744073709551615),
    owner_fence text COLLATE "C" NOT NULL
        CHECK (owner_fence ~ '^[0-7][0-9A-HJKMNP-TV-Z]{25}$'),
    payload bytea NOT NULL
        CHECK (octet_length(payload) BETWEEN 1 AND 104857600),
    registry_deployment bytea,
    registry_scope_kind bytea,
    registry_scope_id bytea,
    registry_subject bytea,
    registry_first_ticks bigint,
    registry_shard smallint,
    registry_deployment_hash bytea,
    registry_scope_hash bytea,
    CHECK ((address LIKE 'owner-registry-entry:%' AND
        (registry_deployment IS NOT NULL AND registry_scope_kind IS NOT NULL
         AND registry_scope_id IS NOT NULL AND registry_subject IS NOT NULL
         AND registry_first_ticks IS NOT NULL AND registry_shard IS NOT NULL
         AND registry_deployment_hash IS NOT NULL AND registry_scope_hash IS NOT NULL))
        OR (address NOT LIKE 'owner-registry-entry:%' AND
        (registry_deployment IS NULL AND registry_scope_kind IS NULL
         AND registry_scope_id IS NULL AND registry_subject IS NULL
         AND registry_first_ticks IS NULL AND registry_shard IS NULL
         AND registry_deployment_hash IS NULL AND registry_scope_hash IS NULL))),
    CHECK (registry_deployment IS NULL OR octet_length(registry_deployment) BETWEEN 1 AND 1024),
    CHECK (registry_scope_kind IS NULL OR registry_scope_kind IN
        (decode('74656e616e74','hex'), decode('6465706c6f796d656e74','hex'))),
    CHECK (registry_scope_id IS NULL OR octet_length(registry_scope_id) BETWEEN 1 AND 1024),
    CHECK (registry_subject IS NULL OR octet_length(registry_subject) BETWEEN 1 AND 1024),
    CHECK (registry_shard IS NULL OR registry_shard BETWEEN 0 AND 255),
    CHECK (registry_deployment_hash IS NULL OR octet_length(registry_deployment_hash) = 32),
    CHECK (registry_scope_hash IS NULL OR octet_length(registry_scope_hash) = 32),
    PRIMARY KEY (namespace, address)
);
CREATE INDEX owner_registry_scope_order ON hexalith_eventstore_control.rows
    (registry_scope_hash, registry_first_ticks, registry_subject)
    WHERE address LIKE 'owner-registry-entry:%';
CREATE INDEX owner_registry_shard_order ON hexalith_eventstore_control.rows
    (registry_deployment_hash, registry_shard, registry_first_ticks,
     registry_scope_kind, registry_scope_id, registry_subject)
    WHERE address LIKE 'owner-registry-entry:%';
