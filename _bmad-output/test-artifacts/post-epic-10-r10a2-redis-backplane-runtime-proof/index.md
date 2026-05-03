# R10-A2 Redis Backplane Runtime Proof — Evidence Index

Story: `post-epic-10-r10a2-redis-backplane-runtime-proof`
Schema: pre-dates `signalr-operational-evidence/v1`; retained for historical record. New evidence runs in this folder must follow the canonical schema in [`_bmad-output/test-artifacts/signalr-operational-evidence-template.md`](../signalr-operational-evidence-template.md).

| Run id | Date (UTC) | Commit SHA | Topology | Result | Classification | Evidence file |
| --- | --- | --- | --- | --- | --- | --- |
| `r10a2-20260502133014-2dbac7ba671d4599ad5c5bf4e836330c` | 2026-05-02T13:30:41Z | `f286f54` | A=eventstore-a / B=eventstore-b / Redis=localhost:6379 | Pass — 31 ms cross-instance receipt; 4 negative controls held | `pass` (positive proof + controls) | [`evidence-2026-05-02-133041Z.md`](evidence-2026-05-02-133041Z.md) |
| `r10a2-20260502130507-ff66f58ce8194b869bdf1c5f09d663b4` | 2026-05-02T13:05:11Z | `f286f54` | A=eventstore-a / B=eventstore-b / Redis=localhost:6379 | Pass — 31 ms cross-instance receipt; 3 negative controls held | `pass` (positive proof + controls; thinner topology/cleanup notes than the 13:30Z run) | [`evidence-2026-05-02-150535.md`](evidence-2026-05-02-150535.md) |

The 13:30:41Z run is the canonical R10-A2 evidence used by AC#11 walk-through in `docs/operations/signalr-operational-evidence.md`. The 13:05:11Z run is an earlier successful run kept for traceability.
