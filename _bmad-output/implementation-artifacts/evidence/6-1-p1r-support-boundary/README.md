# P1R measured support boundary — source and tooling evidence

This directory records a bounded admission policy and focused checks. It does **not** select a successor, qualify a published package, prove writable rollback, or change P1R/Story 6.1 usability. The sealed 3.119.0 packet remains nonqualifying, and every historical failure remains visible in its original receipt. With `rollback=null`, Projects AD-17 mutation freeze and forward recovery remain in force.

## Derivation

`tools/p1r_support_boundary.json` enumerates all 163 case IDs across the 19 sealed scenario/addition receipts of `6-1-p1r-31190-published-run/packet-677345025f7b42449cbf8ce4afb43813`. It binds packet SHA-256 `1cb9d427c34db61ad250173b13016c69de7e13db522c4fa45cf478fa2d468b4c`, each lane receipt hash, and each original operation, expected and actual outcome, disposition, failed assertion count, and assertion ID inventory. The pinned table hash is `19892532995447336389fe98daaef19c44636942b3a33b791e13b2f534aab7e3`. Of the 163 cases, 39 old-route cases require proven pre-route refusal, one pre-upgrade backup case remains incompatible containment, and the rest remain supported obligations. The exact IDs and proof requirements are in `../../../specs/spec-6-1-p1r-remediation/qualification-contract.md`.

`admission-probes-20261011-final.json` (SHA-256 `eee5a924f987bf3327280961817322c0d70b0bb1fa8c62b5adb7349e481a7632`) records all 39 old-route admission calls against the final policy and source: 28 on Probe 3.70.1 and 11 on Probe 3.110.0. The `3.110.0`-to-`3.70.1` query directions use the incapable writer's admission boundary. Only these published Probe consumers were built and invoked. Each build and admission call retains literal argv, working directory, UTC start/end, exit code, exact `raw_stdout`, and its SHA-256 over the UTF-8 bytes of that retained string; admission calls also retain parsed output, loaded version, the typed denial, and assertion totals. The `output_hash_schema` field states this encoding. All 39 observations returned `refused` at `before-protected-route` with `protected_access=false`, `effect=false`, `write=false`, and positive passing assertions. The receipt records the physically loaded assembly identity observations and relevant source hashes. These are bounded tooling observations; they are not imported into a new published qualification packet or a persisted-state run.

## Focused verification

| Check | Result |
| --- | --- |
| `python3 -m unittest discover -s tools/tests -p 'test_p1r_published*.py'` | 111 tests passed. Controls reject policy tamper, missing/false admission, changed case IDs or assertion IDs, candidate failure masking, unsafe old writer dispatch, a failed lane exit, wrong refusal direction, and a version-prefix substitution. |
| `dotnet build tools/p1r-published-consumers/probe/Probe.csproj -c Release -p:EventStoreVersion=3.70.1 -p:NuGetAudit=false -m:1` | Succeeded, 0 warnings, 0 errors. |
| Same Probe build with `EventStoreVersion=3.110.0` | Succeeded, 0 warnings, 0 errors. |
| `dotnet build tools/p1r-published-consumers/host/Host.csproj -c Release -p:EventStoreVersion=3.110.0 -p:NuGetAudit=false -m:1` | Succeeded, 0 warnings, 0 errors. |
| Same Host build with `EventStoreVersion=3.119.0` | Succeeded, 0 warnings, 0 errors. Build only; no candidate execution. |
| Live Host 3.110.0 smoke, `POST /qualification/stale-fence` | Returned HTTP 409 with `accepted=false`, `denial=incapable-published-endpoint`, `boundary=before-protected-route`, and `protected_access=false`, `effect=false`, `write=false`, before actor access. |
| `python3 tools/p1r-published-qualification.py --help` | Exited 0; `prepare` and read-only `validate` remain the bounded packet interface. |
| `git diff --check` | Exited 0. |

The next published run still needs exact owner selection, actual package and operational receipts, successful candidate stale-fence and logical-alias checks, independent validation, and later owner decisions. An admission denial is never treated as a successful write or as proof that committed data survives rollback.
