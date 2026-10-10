# P1R 3.119.0 published requalification evidence

## Selected inputs

`observation-a8318299841d4bb2a0a6bb7d96918064/` holds the owner inputs
(`owner-inputs.json`, SHA-256 `7920375b2d30ef21072e023a443eabf03a2c958251e95cff7f616620d9e8338d`)
and the pending owner decisions (`owner-decisions.json`, SHA-256
`01bb5ab0167a660f52a6007b4d266fdca3c4cbc07c4a65fe441111c17f221dc4`).
Every execution below used these exact inputs.

| Identity | Value |
| --- | --- |
| Candidate | EventStore 3.119.0, tag `v3.119.0` at `f463442cca19e4199982a23a08bae4a490767d4a` |
| Builds execution input | 4.30.1-20-g2cf0002 at `2cf00028bbe563d80d4d12b5fb2054914f14fcb6` (stamped into the published packages) |
| Tag Builds gitlink | `468fdbba04e2d9a27d251298875125b57fa6d836`, kept as distinct package-build provenance per the frozen decision |
| Comparisons | 3.70.1 and 3.110.0 published packages; `rollback=null` |
| Runtime | Dapr 1.18.2, `daprd` SHA-256 `e730688f06b9b7cea0d616a7dde8fc0124cb6cc8d6dae149192422c7ea67f4e6` |
| Actor state | tracked `deploy/dapr/statestore-postgresql.yaml` (`state.postgresql` v1) on `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636` |

Archives are bound by SHA-256 and content hash; `*.nupkg` files are ignored by Git and remain
retrievable at their exact published versions. Rendered PostgreSQL credentials stay in
invocation-private scratch: retained configurations hold the tracked template, the private
rendering hash, and `credential_redacted=true`.

## Final execution and packet

`execution-822c180c8b1942d9bfba2601e2b5202e/` ran 2026-10-10T12:18:43Z to 12:44:24Z.
It is bound to EventStore `73c4b4f32e033f8ee2b2cca9678c84394eeebc2a` plus the second-review
patches (working-tree diff SHA-256 `b6e4c9119d2a000fbbe88d46ed27c77d7b59dde2ea12b80de849037c9e558660`),
Projects workspace `0ee8d9a4a2199b0c5bf3e304f90a746418ab97de` and Builds
`9c46679beb875dbd4c2da56aaea730bfe9601619`. All 17 canonical scenarios, both adopted
additions, the fresh-database restore and owned cleanup were recorded with no executor errors.
Restore passed 12/12, cleanup 7/7, reminder recovery 131/131 and failure cleanup 115/115.
The candidate trusted-effect and unauthorized-effect audit records were persisted and matched.

`packet-677345025f7b42449cbf8ce4afb43813/` was prepared only from that execution's receipts.
`prepare` and `validate` both exited 0 at 2026-10-10T12:46:46Z with `valid=true`,
`technically_qualified=false`, `decisions_complete=false` and `p1r_usable=false`. The four
owner decisions and same-baseline conformance are pending; no capable rollback was selected,
so mutation freeze and forward recovery remain in force.

`validate` recomputes the source binding from the live workspace: HEAD, gitlinks and the
source/configuration files outside `_bmad-output`. It still exits 0 after this README update.
After any commit, or any change to bound source, it reports
`source/configuration inputs changed` (exit 2). Validate the packet only against its captured
workspace; never patch the binding check.

## Nonpassing results

| Lane | Failed checks | Nonpassing cases |
| --- | --- | --- |
| metadata-read | 4 | 3.70.1 pascal/web floors 1 and 5 |
| legacy-metadata | 2 | 3.70.1 pascal/web missing floor |
| metadata-write | 2 | 3.110.0→3.70.1, 3.119.0→3.70.1 |
| invalid-evidence | 6 | 3.70.1 and 3.110.0 invalid floor, protected payload, unknown metadata version |
| query-wire | 60 | all 12 cross-version JSON/XML dual-principal directions; legacy-principal directions pass |
| projection-wire | 4 | JSON positive-watermark directions to or from 3.70.1; 3.110.0↔3.119.0 pass |
| mixed-api | 25 | 3.110.0 and 3.70.1 status fields; 3.110.0 and 3.119.0 stale-fence denial; 3.70.1 unsupported cursor-scope, fenced/trusted/unauthorized effects and retained floor |
| pre-upgrade-restore | 0 | containment-only backup restore remains incompatible: it cannot keep later committed writes |
| logical-event-evolution | 3 | logical alias replay, unknown-version refusal, original envelope preservation |

## Change from the earlier final run

`execution-64d7c7a84abc4f71b89e26e7793813d7/` and `packet-7e1d2c98585e4c1c995831af2998b558/`
were sealed before the first-review patches and are superseded. That fixture registered the
trusted-effect audit sink only for the candidate. 3.110.0 shares the sink contract and refuses
denials without it, so its unauthorized-effect direction recorded an error. With the sink
registered for every capability build, that direction is measured as a compatible refusal. The
four startup-failure seed checks that failed there pass in the final run.

## Attempt history

Every attempt stays at its unique path; none was resealed or reused.

| Path | Disposition |
| --- | --- |
| `observation-53e4018a…`, `observation-0847125d…`, `observation-a8318299…` | input observations; `a8318299` is the selected one |
| `execution-b3311345…` | failed: owned applications exited during startup |
| `development-smoke-*` | targeted development smokes, never packet inputs |
| `execution-19b767f1…`, `packet-3dfefb5e…`, `packet-bb668ef1…` | complete with reminder-recovery inventory errors; prepares refused (component binding, executor source) |
| `execution-64d7c7a8…`, `packet-7e1d2c98…` | pre-review final; superseded as described above |
| `observation-ceb36e85…`, `execution-61e4f244…` | post-review run stopped by `KeyboardInterrupt` |
| `observation-502d99c0…`, `execution-f3d07b03…`, `packet-98591a7b…` | complete; prepare refused `invalid invocation timestamps`, which led to the monotonic stamp |
| `observation-ac199cd7…`, `execution-4b93b736…` | stopped after 13 receipts, leaving a zero-byte receipt and four exited owned containers. Those containers were removed on 2026-10-10 by exact ID after their invocation label and recorded names were matched. |
| `packet-98816a0b…` | prepare refused `missing or substituted input`: evidence files were passed instead of their directories |
| `execution-822c180c…`, `packet-67734502…` | final, as described above |
