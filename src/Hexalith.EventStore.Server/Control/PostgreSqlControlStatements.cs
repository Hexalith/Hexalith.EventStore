namespace Hexalith.EventStore.Server.Control;

/// <summary>Fixed parameterized SQL for the one application-owned metadata table.</summary>
internal static class PostgreSqlControlStatements
{
    internal const string Namespace = "eventstore.control.v1";

    internal const string Select = """
        SELECT generation, owner_fence, payload, registry_deployment, registry_scope_kind,
               registry_scope_id, registry_subject, registry_first_ticks, registry_shard,
               registry_deployment_hash, registry_scope_hash
          FROM hexalith_eventstore_control.rows
         WHERE "namespace" = $1 AND address = $2
        """;

    internal const string SelectForUpdate = Select + " FOR UPDATE";

    internal const string Insert = """
        INSERT INTO hexalith_eventstore_control.rows
            ("namespace", address, generation, owner_fence, payload,
             registry_deployment, registry_scope_kind, registry_scope_id, registry_subject,
             registry_first_ticks, registry_shard, registry_deployment_hash, registry_scope_hash)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
        ON CONFLICT DO NOTHING
        """;

    internal const string Update = """
        UPDATE hexalith_eventstore_control.rows
           SET generation = $3, owner_fence = $4, payload = $5,
               registry_deployment = $6, registry_scope_kind = $7,
               registry_scope_id = $8, registry_subject = $9,
               registry_first_ticks = $10, registry_shard = $11,
               registry_deployment_hash = $12, registry_scope_hash = $13
         WHERE "namespace" = $1 AND address = $2
           AND generation = $14 AND owner_fence = $15 AND payload = $16
           AND registry_deployment IS NOT DISTINCT FROM $17
           AND registry_scope_kind IS NOT DISTINCT FROM $18
           AND registry_scope_id IS NOT DISTINCT FROM $19
           AND registry_subject IS NOT DISTINCT FROM $20
           AND registry_first_ticks IS NOT DISTINCT FROM $21
           AND registry_shard IS NOT DISTINCT FROM $22
           AND registry_deployment_hash IS NOT DISTINCT FROM $23
           AND registry_scope_hash IS NOT DISTINCT FROM $24
        """;

    internal const string Delete = """
        DELETE FROM hexalith_eventstore_control.rows
         WHERE "namespace" = $1 AND address = $2
           AND generation = $3 AND owner_fence = $4 AND payload = $5
           AND registry_deployment IS NOT DISTINCT FROM $6
           AND registry_scope_kind IS NOT DISTINCT FROM $7
           AND registry_scope_id IS NOT DISTINCT FROM $8
           AND registry_subject IS NOT DISTINCT FROM $9
           AND registry_first_ticks IS NOT DISTINCT FROM $10
           AND registry_shard IS NOT DISTINCT FROM $11
           AND registry_deployment_hash IS NOT DISTINCT FROM $12
           AND registry_scope_hash IS NOT DISTINCT FROM $13
        """;
}
