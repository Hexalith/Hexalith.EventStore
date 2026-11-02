# Canonical scope upgrade (4.0.0)

Public tenant identifiers now use one canonical lowercase form. `system` is reserved for retained internal storage and cannot be supplied as a public tenant. The public API normalizes valid case variants and rejects malformed or ambiguous tenant grants.

Platform operations use an explicit `platform:<scope>` declaration. Callers that previously addressed the `system` tenant directly must use the operation's declared platform scope through the authenticated route. The workload stream-read route verifies that public scope and maps the approved Tenants source reads to the retained `system` storage stream.

Update EventStore packages as one 4.0.0 cohort. Recheck caller grants, workload scope headers, and any catalog declarations that assumed `system` was a public tenant.
