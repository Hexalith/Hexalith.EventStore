# UI Observation

Date: 2026-05-10
Target: https://localhost:8093/dapr/actors
Aspire mode: EnableKeycloak=false
Seeded actor id: tenant-a:counter:counter-1

Observation:
- The DAPR Actor Inspector loaded with Admin development role.
- AggregateActor was selected and actor id tenant-a:counter:counter-1 was inspected.
- The page displayed state rows for pending_command_count and {actorId}:metadata.
- The page did not display Actor instance not found.
- The page did not display Actor lookup unavailable.

Screenshot: admin-ui-actor-diagnostics-honesty-fix.png
