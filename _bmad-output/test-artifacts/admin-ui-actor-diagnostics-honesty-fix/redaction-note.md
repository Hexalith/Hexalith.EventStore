# Redaction Note

Evidence preserves local sample tenant/domain/aggregate identifiers, Redis key names, command correlation ids, lookup statuses, count provenance, owner app id, state store name, and lookup source.

Evidence omits bearer tokens, signing material, connection strings, raw actor state values, and event payload values. In aggregate-state.json, found state entries keep key names and size/status evidence while jsonValue is replaced with <redacted:raw-state-value-present>.

Redis key scanning in this folder was used only for local manual evidence after flushing the dev Redis instance. Production code does not enumerate Redis keys for actor inventory.
