# Current EventStore source handoff for Folders Story 12.2

Producer disposition: **not_fully_qualified**. Execution is complete. Full qualification remains open for the preexisting Server secret-material guard and missing external Server maps in 22 bounded workers. This packet records two verified SDK testing repairs, fresh producer execution and remaining gates. Folders' current-source acceptance has not been executed here. All listed test results are fresh unless explicitly identified as historical reuse.

## Source comparison and correction

EventStore starting revision: `995117f9b899a2994e99257091b0ea35cfbe5090`. Folders observed revision: `460f7b036a526ac08d563f2e69bd3a017fea54f6`. Both canonical revisions and all direct dependency revisions, initial statuses and Git index hashes are in `repositories-before.json`. Revision alone is insufficient: the tested source includes the uncommitted testing-helper and evidence-guard corrections recorded in `source-final.json` and `source-change.patch`.

The latest consumer startup handoff deliberately ran an older sealed SDK selection and excluded the later admission/rate-limit edits. `candidate-comparison.json` separately compares that input freeze, the original inherited-owner qualification and the later admission packet with this task's starting source. Admission/token/rate-limit source matches its own packet; seven later changes to inventoried ordinary files concern existing Admin/protection work and remain preserved. Additional protection source files are identified by `repository-source-delta-since-c66.txt`; `candidate-uninventoried-source-inputs.json` distinguishes prior manifest omissions from repository additions. No prior consumer result is relabeled as current-graph execution.

Verified SDK defect: `TestServiceOverrides.RemoveDaprHealthChecks` omitted `projection-delivery-unresolved-conflict`, which requires real Dapr state even when a test replaces command routing. This caused the earlier three isolated health assertions to return 503. The helper now removes that exact sidecar-dependent registration. Two regression rows failed before the fix and pass afterward, covering registration before/after the override and retaining self/unrelated checks. Production readiness, existing HTTP assertions, deadlines, retries, admission rules and storage bounds are unchanged. The unchanged 33-case public throttle/health matrix now passes.

The second SDK testing defect is stale frozen-MSBuild enrollment: two sealed inherited-owner reproduction projects were added after the original inventory. The original **984-file count and digest remain unchanged**. Two exact additional paths are independently authenticated with digest `167bd2d4402c2a6bb8fa37d7f417ff2074ccdff8b52bd263e3c7b75523cdd927`. Regular-file/parent-link checks and live-build exclusion protections remain. Six new positive/tamper rows supplement the original mutation controls. The initial failing `contracts-full` is retained; final rerun and focused guard results are separate receipts.

## Fresh build and regression execution

All commands ran from `/home/administrator/projects/hexalith/eventstore`. Each named JSON receipt retains literal argv, UTC timestamps, process identity, exit code and elapsed time; its log/XML retains output. Task environment is recorded in receipts or, for the bounded launcher, `bounded-infrastructure/selection.json`. Debug restore/build used project references:

```bash
dotnet build Hexalith.EventStore.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false
```

The complete solution build passed with zero warnings/errors. `dependency-configuration-observed.json` retains 32 source-reference project/import configuration hashes observed after execution began; those are not relabeled as a pre-execution raw-byte freeze. `debug-project-graph.json` preserves the evaluated restore graphs and project libraries. Tests run as individual built xUnit assemblies, never solution-level `dotnet test`.

| Receipt | Fresh result |
| --- | --- |
| testing-final | 155 passed / 0 failed / 0 skipped; exit 0 |
| public-throttling-health | 33 passed / 0 failed / 0 skipped; exit 0 |
| client-full | 2073 passed / 0 failed / 2 skipped; exit 0 |
| domain-full | 1164 passed / 0 failed / 0 skipped; exit 0 |
| server-full | 6360 passed / 1 failed / 25 skipped; exit 1 |
| contracts-final-full | 2442 passed / 0 failed / 2 skipped; exit 0 |
| contracts-guard-focused | 12 passed / 0 failed / 0 skipped; exit 0 |
| apphost-full | 176 passed / 0 failed / 0 skipped; exit 0 |
| testing-integration-full | 48 passed / 0 failed / 0 skipped; exit 0 |
| sample-full | 187 passed / 0 failed / 0 skipped; exit 0 |
| signalr-full | 44 passed / 0 failed / 0 skipped; exit 0 |
| generators-full | 124 passed / 0 failed / 0 skipped; exit 0 |
| query-routing-full | 14 passed / 0 failed / 0 skipped; exit 0 |
| admin-abstractions-full | 510 passed / 0 failed / 0 skipped; exit 0 |
| admin-server-full | 775 passed / 0 failed / 18 skipped; exit 0 |
| admin-host-full | 66 passed / 0 failed / 0 skipped; exit 0 |
| operations-full | 76 passed / 0 failed / 0 skipped; exit 0 |
| admin-ui-full | 1114 passed / 0 failed / 0 skipped; exit 0 |
| admin-cli-full | 384 passed / 0 failed / 0 skipped; exit 0 |
| admin-mcp-full | 429 passed / 0 failed / 8 skipped; exit 0 |
| payload-protection-full | 642 passed / 0 failed / 0 skipped; exit 0 |
| actor-live | 1 passed / 0 failed / 0 skipped; exit 0 |
| bounded-live | 9 passed / 0 failed / 0 skipped; exit 0 |

`qualification.json` enumerates exact skips and failures without treating skips as acceptance. `affected-server-regressions.json` proves all 547 admission/authentication/rate regressions passed in the fresh full Server run; only their name inventory is reused. These overlap the full Server result and must not be added to it. `server-secret-guard-disposition.json` binds all 21 reported locations to the unchanged starting revision; 20 additionally match the initial source/evidence inventory. No secret-guard assertion, scanner exemption or historical capture was changed. The root spec location not included in that initial inventory matches the clean starting revision. `testing-red` is an expected diagnostic and is excluded from green results. `fixture-tool-cleanup-tests` verifies the existing process runner's cleanup controls. The initial generic mapped-image comparison included third-party/system images outside the first-party PDB set and is retained; `actor-mapped-sdk-binding.json` is the correctly scoped audit.

## Executed binary/source identity

`binary-final.json` inventories 1,012 Debug binary/manifest paths and 65 unique PE/PDB pairs. The final hash-pinned PDB audit binds 4917 ordinary document copies and 387 generated copies with no errors. Generated-disk differences authenticate against matching embedded source and remain explicit in its summary. `bound-source-index-final.json` links 5304 document copies to content-addressed archives, including dependency source supplied through project references. `--current-inputs` verifies ordinary PDB inputs across those dependency boundaries too. `debug-binary-freeze.json` and the first audit retain the earlier inventory; `standalone-payload-delta.json` records the intentional refresh of 14 paths for the project outside the solution. The executed core graphs stayed unchanged. `testing-style-delta.json` and `contracts-guard-binary-delta.json` separately record refreshed test-only images; intermediate source audits remain retained under their pre-change directories. Original selected protection images remain in `initial-protection-images/`; `handoff-runtime.json` now records the final producer outputs and origin paths. Diagnostic producer manifests must not replace consumer runtime manifests.

**Execution images differ by build context.** `tested-image-crosswalk.json` maps every actor-loaded SDK image to the selected producer image. For example, actor Client/DomainService images differ from the selected Client/DomainService images used by the bounded graph. The 22 comparable SDK load observations have identical ordinary PDB source checksums, and 11 are byte-identical to the selected images. All 26 first-party actual loads have independent PE/PDB/source bindings. No byte identity, IL equivalence or interchangeable assembly-reference compatibility is inferred for differing images. The table below identifies the selection, not a claim that every lane loaded those bytes. Consumer acceptance must bind its actual assembled graph.

| Producer module | DLL SHA-256 | MVID | PDB SHA-256 |
| --- | --- | --- | --- |
| Hexalith.EventStore.Client.dll | `11d54e3006714341fbc17d7a92699c8eca61b1b9adc30043be62c334d979f56d` | `9e672923-d119-453b-a2e3-42cebd96b969` | `4f91d811f0c39fe2eba0af4011598e64df2114d868c6d1690528f12e04146a6a` |
| Hexalith.EventStore.DomainService.dll | `175c53f46b20328942ea3a20dd267c4a21faaff60c7cc53a9a9d3ba60d261273` | `99a9e402-c7e8-402b-ad2a-ec6eceb26a41` | `36b8b7be6efc46d5fb76af333df5b9b9e732e183042c8f78dfe333475b43e5fc` |
| Hexalith.EventStore.Server.dll | `5df8efb0ba56480637ab5b88afd0ec1ab6c6b01edc4b282d511723ef3f6aa71e` | `fa83fb9d-54f5-4da3-8e0e-7eddf8cffd57` | `98fa5409b450e6ebb0944c7927d178908a4d53f7443c109437a584b6709d2dc0` |
| Hexalith.EventStore.Gateway.dll | `ce5ccb65d259c76a1e06e1c4baf65f16c442b8f9368ebad8b2406728be66a493` | `c49e1ab4-2ca3-4cda-b92e-d5b2d0de4fcd` | `8393153d28f904c32816bd5f3fa5c6f91204414009579766cf13bddef05173b1` |
| Hexalith.EventStore.Aspire.dll | `fc328b6e715fbd5b80c566d11bbaabf6fbb2b93f107a2a2c284063db24bd07a9` | `70201ee3-e0e3-41a1-9f3a-0647812b2412` | `686de88773e85cc995ebd99fb077fcb4baf1fecc1465f44e4e488239b9510834` |
| Hexalith.EventStore.Testing.dll | `2a01091fe0c14a1e0c54d9926ac0d2735bace4bb135361f33b5be5624769d4e1` | `7926c3b0-123b-42b8-9f13-82f5394730a6` | `6b9de1a6055aabb9a1f95c8b0c5b0918d51e33738d01e16e5b6bd82da7e61671` |

The production actor qualification retains actual `/proc` mapped images before/after replacement. All 26 observed first-party module identities match the PDB-bound graph. Bounded workers additionally record actual loaded MVIDs, source checksums and Debug/project-reference build identity; their observer retains native executable hashes, sidecar argv, materialized components and mapped SDK images. Exact runtime Dapr/Redis/control-plane image identities are in the respective infrastructure receipts. The launched native daprd is 1.18.7; the .NET SDK is 10.0.402 and executed runtime is 10.0.13.

## Real Dapr semantics, cleanup and preservation

`actor-live/` retains 47 production-path checks: isolated actor saturation, persisted Reserved/Pending/UnknownProviderOutcome recovery through process replacement, stale-fence/authority refusal, terminal exact replay, one real Counter event and domain invocation, changed-intent refusal, authentication denials, and exactly 100 authenticated public reads followed by quota-specific 429/Retry-After. Durable Redis snapshots and mapped process identities bracket the checks. This is producer acceptance, not a fresh Folders provider pass.

`bounded-scenarios/` contains the unchanged production-dispatcher process suite's staging, partial owner handoff, replacement, recovery, ordinary growth and inherited-owner evidence. Each final scenario summary records counts, contracts, PIDs, persisted observations and observed batch/value maxima. The enforced limits remain 100 operations and 1,048,576 canonical bytes. See `bounded-summary.json` for completed scenarios and `bounded-live.xml` for authoritative outcomes. The process-local HotReload=false override matches prior supported SDK fixture usage to avoid host watcher exhaustion; it changes no assertion, deadline, retry, admission or storage bound.

The completed matrix executed the archived `bounded-live-v1-executed.py` launcher. Its corrected successor `bounded-live.py` has syntax validation only; its new cancellation handling is not claimed as executed acceptance. V1 did not retain transient observer exceptions. The final independent runtime audit requires complete persisted phase proofs and process/module coverage, joins each worker's self-reported PE/PDB/MVID to its actual mapped observation, and rejects missing observations or cleanup evidence. It fails for 22 absent Server OS-map observations, while all 120 CLR module proofs authenticate loaded MVIDs, hashes and source. The external observer corroborates Client, DomainService and test images in all 30 workers, and Server in eight. This stricter evidence gap remains open; it is not a failed behavioral test or inferred identity mismatch. `bounded-observation-gap-disposition.json` retains every missing observation.

The repository's Aspire AppHost was started, described and stopped by its exact project path before code edits. Its captured services were still waiting for security readiness, so this startup observation is not a healthy end-to-end baseline pass. An ephemeral dashboard login token is redacted in the new log only. The unrelated Tenants AppHost remains separately running; no lifecycle command targeted it. Live fixtures' own cleanup receipts verify their exact owned PIDs, labeled containers and materialized directories. Generic repository AppHost materialization paths are not deleted because preexisting ownership is unknown.

`cleanup-final.json` independently reconfirms all six exact owned container IDs absent through Docker inspect/list and retains the unrelated Tenants AppHost. The first case-sensitive absence probe is kept as a diagnostic; the corrected probe succeeds. `temporary-cleanup.json` retains verifier identity, archived tool binaries and exact directory removal.

`historical-before.json` inventories 47,976 prior evidence files across EventStore/Folders. `preservation-final.json` records the final byte comparisons, unchanged Folders source, existing edits and unchanged Git indexes. No staging, commit, push, publication, deployment, pin change or consumer-output selection was performed in EventStore or Folders. SDK regression tests may use isolated temporary Git fixtures.

## Consumer continuation and explicit remaining gates

This task leaves Folders source and selected runtime outputs untouched. The prior strict provider pass and large consumer recovery/component evidence are historical reuse only, enumerated in `reused-evidence.json`; checked packet seals are in `reused-packet-seals.json`. They do not establish current-source adoption.

The consumer can build its unchanged graph through its existing source-reference seam in a new qualification packet:

```bash
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -m:1 -nr:false
```

That is a continuation recipe, not an executed command in this packet. Retain and verify the resulting consumer SDK PE/PDB/source graph and actual mapped modules before rerunning its unchanged strict real-provider and 10,001-position authenticated recovery lanes. The old selector belongs to the old packet and cannot establish this source identity. Do not infer equivalence from version text or copy producer `.deps.json` over consumer manifests.

Remaining gates: the failing full Server secret-material guard, 22 missing external Server maps, current-source consumer acceptance, existing skipped/deferred test debt, SM4 acknowledgement p95, Release/package/source equivalence/PD10 and independent whole-story acceptance. No whole Story 12.2 completion, packaged adoption or performance closure is claimed. Build/binding/preservation/cleanup predicates are enumerated in `qualification.json`; a missing or failed predicate cannot produce a qualified status.

`SHA256SUMS` seals all packet files. Verify without modifying consumer state:

```bash
python3 /home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/verify.py
python3 /home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/verify.py --current-inputs
```
