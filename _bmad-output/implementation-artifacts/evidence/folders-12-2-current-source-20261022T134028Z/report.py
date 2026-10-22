#!/usr/bin/env python3
"""Summarize retained receipts; absent or unsuccessful gates cannot become passes."""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent
root = packet.parents[3]
labels = ['testing-final', 'public-throttling-health', 'client-full', 'domain-full', 'server-full', 'contracts-final-full', 'contracts-guard-focused', 'apphost-full', 'testing-integration-full', 'sample-full', 'signalr-full', 'generators-full', 'query-routing-full', 'admin-abstractions-full', 'admin-server-full', 'admin-host-full', 'operations-full', 'admin-ui-full', 'admin-cli-full', 'admin-mcp-full', 'payload-protection-full', 'actor-live', 'bounded-live']


def read(name):
    path = packet / name
    return json.loads(path.read_text()) if path.exists() else None


def artifact(name, key, expected=True):
    value = read(name)
    return value is not None and value.get(key) == expected


results = []
for label in labels:
    receipt = packet / (label + '.json')
    xml = packet / (label + '.xml')
    row = {'label': label, 'fresh_execution': True, 'exit_code': json.loads(receipt.read_text()).get('exit_code') if receipt.exists() else None}
    if xml.exists():
        document = ET.parse(xml).getroot()
        assemblies = document.findall('assembly')
        row.update({key: sum(int(a.get(key, '0')) for a in assemblies) for key in ('total', 'passed', 'failed', 'errors', 'skipped', 'not-run')})
        row['seconds'] = sum(float(a.get('time', '0')) for a in assemblies)
        row['xml_assemblies'] = len(assemblies)
        row['xml_test_records'] = len(list(document.iter('test')))
        row['skips'] = [{'name': t.get('name'), 'reason': t.findtext('reason') or t.findtext('reason/message')} for t in document.iter('test') if t.get('result') == 'Skip']
        row['failures'] = [{'name': t.get('name'), 'message': t.findtext('failure/message')} for t in document.iter('test') if t.get('result') == 'Fail']
    minimum = {'public-throttling-health': 33, 'actor-live': 1, 'bounded-live': 9, 'contracts-guard-focused': 12}.get(label, 1)
    row['nonempty_expected_inventory'] = row.get('xml_assemblies', 0) > 0 and row.get('xml_test_records', 0) >= minimum and row.get('total', 0) == row.get('xml_test_records', -1)
    row['passed_gate'] = row['exit_code'] == 0 and row.get('failed') == 0 and row.get('errors') == 0 and row.get('not-run') == 0 and row['nonempty_expected_inventory']
    if label in ('actor-live', 'bounded-live', 'public-throttling-health'):
        row['passed_gate'] = row['passed_gate'] and row.get('skipped') == 0
    if label == 'bounded-live':
        row['outer_cleanup_passed'] = artifact('bounded-infrastructure/result.json', 'owned_infrastructure_cleanup') and artifact('bounded-infrastructure/result.json', 'observed_cleanup') and artifact('bounded-launcher-final.json', 'exit_code', 0)
        row['passed_gate'] = row['passed_gate'] and row['outer_cleanup_passed']
    results.append(row)

gates = {'debug_build': artifact('debug-source-build.json', 'exit_code', 0),
         'standalone_protection_build': artifact('payload-protection-build.json', 'exit_code', 0) and artifact('payload-tests-build.json', 'exit_code', 0),
         'source_binding': artifact('source-binding-final/summary.json', 'passed') and artifact('pdb-source-binding-consumer-final.json', 'exit_code', 0),
         'contracts_guard_repair': artifact('contracts-guard-focused.json', 'exit_code', 0),
         'preservation': artifact('preservation-final.json', 'passed'),
         'actor_actual_images': artifact('actor-mapped-sdk-binding.json', 'passed'),
         'actor_qualification_cleanup': artifact('actor-live/result.json', 'passed'),
         'bounded_identity_coverage': artifact('bounded-runtime-audit.json', 'passed'),
         'final_owned_cleanup': artifact('cleanup-final.json', 'passed') and artifact('temporary-cleanup.json', 'passed'),
         'cleanup_controls': artifact('fixture-tool-cleanup-tests.json', 'exit_code', 0),
         'diff_check': artifact('diff-check-final.json', 'exit_code', 0) and artifact('new-guard-diff-check-verification.json', 'passed')}
qualification = {'status': 'producer_gates_passed_with_recorded_skips' if all(x['passed_gate'] for x in results) and all(gates.values()) else 'not_fully_qualified',
                 'results': results,
                 'execution_complete': all(r['exit_code'] is not None and r.get('xml_test_records', 0) > 0 for r in results),
                 'evidence_gates': gates,
                 'remaining_failures': ['Full Server secret-material guard: 21 unchanged starting-revision locations', 'Strict external worker-module corroboration: Server maps absent for 22 of 30 workers; all CLR MVID/source checks pass'],
                 'consumer_current_source_execution': 'not run; consumer source and selected outputs were not modified',
                 'historical_consumer_pass': 'reused only; older explicitly selected SDK graph; does not qualify current graph',
                 'remaining_gates': ['Full Server secret-material guard: 21 preexisting tracked locations; immutable historical evidence preserved', '22 absent external Server module maps in the stricter bounded-worker audit', 'Consumer adoption and unchanged strict real-provider/10001-position acceptance against this source graph', 'SM4 acknowledgement p95', 'Release/package/source equivalence and PD10', 'Independent whole Folders Story 12.2 acceptance'],
                 'diagnostics': ['testing-red: two expected failures before the helper correction', 'contracts-full: initial frozen membership failure; superseded by passing contracts-guard-focused and contracts-final-full, originals retained', 'pdb-hash-negative: expected refusal of changed frozen hash values', 'Initial generic mapped-image audit compares third-party/system images to a first-party PDB inventory; use actor-mapped-sdk-binding.json for the scoped SDK audit', 'Initial closing cleanup misread lower-case Docker absence text; corrected independent inspect/list checks pass', 'Initial runtime-audit command/output name collision retained as receipt plus stdout; separate command label regenerated the read-only audit without changing observations']}
(packet / 'qualification.json').write_text(json.dumps(qualification, indent=2) + '\n')
table = '\n'.join('| ' + r['label'] + ' | ' + (f"{r['passed']} passed / {r['failed']} failed / {r['skipped']} skipped; exit {r['exit_code']}" if 'passed' in r else 'Running or not started; no pass claimed') + ' |' for r in results)
repos = json.loads((packet / 'repositories-before.json').read_text())
identities = json.loads((packet / 'source-binding-final/all-pdb-documents.json').read_text())
binding_summary = read('source-binding-final/summary.json')
binding_index = read('bound-source-index-final.json')
by_hash = {r['peSha256'].lower(): r for r in identities}
runtime = json.loads((packet / 'handoff-runtime.json').read_text())
modules = []
for name in ['Hexalith.EventStore.Client.dll', 'Hexalith.EventStore.DomainService.dll', 'Hexalith.EventStore.Server.dll', 'Hexalith.EventStore.Gateway.dll', 'Hexalith.EventStore.Aspire.dll', 'Hexalith.EventStore.Testing.dll']:
    item = runtime[name]
    bound = by_hash[item['sha256']]
    modules.append(f"| {name} | `{item['sha256']}` | `{bound['mvid']}` | `{bound['pdbSha256'].lower()}` |")
readme = f'''# Current EventStore source handoff for Folders Story 12.2

Producer disposition: **{qualification['status']}**. Execution is complete. Full qualification remains open for the preexisting Server secret-material guard and missing external Server maps in 22 bounded workers. This packet records two verified SDK testing repairs, fresh producer execution and remaining gates. Folders' current-source acceptance has not been executed here. All listed test results are fresh unless explicitly identified as historical reuse.

## Source comparison and correction

EventStore starting revision: `{repos[str(root)]['head'].strip()}`. Folders observed revision: `{repos[str(root.parent / 'folders')]['head'].strip()}`. Both canonical revisions and all direct dependency revisions, initial statuses and Git index hashes are in `repositories-before.json`. Revision alone is insufficient: the tested source includes the uncommitted testing-helper and evidence-guard corrections recorded in `source-final.json` and `source-change.patch`.

The latest consumer startup handoff deliberately ran an older sealed SDK selection and excluded the later admission/rate-limit edits. `candidate-comparison.json` separately compares that input freeze, the original inherited-owner qualification and the later admission packet with this task's starting source. Admission/token/rate-limit source matches its own packet; seven later changes to inventoried ordinary files concern existing Admin/protection work and remain preserved. Additional protection source files are identified by `repository-source-delta-since-c66.txt`; `candidate-uninventoried-source-inputs.json` distinguishes prior manifest omissions from repository additions. No prior consumer result is relabeled as current-graph execution.

Verified SDK defect: `TestServiceOverrides.RemoveDaprHealthChecks` omitted `projection-delivery-unresolved-conflict`, which requires real Dapr state even when a test replaces command routing. This caused the earlier three isolated health assertions to return 503. The helper now removes that exact sidecar-dependent registration. Two regression rows failed before the fix and pass afterward, covering registration before/after the override and retaining self/unrelated checks. Production readiness, existing HTTP assertions, deadlines, retries, admission rules and storage bounds are unchanged. The unchanged 33-case public throttle/health matrix now passes.

The second SDK testing defect is stale frozen-MSBuild enrollment: two sealed inherited-owner reproduction projects were added after the original inventory. The original **984-file count and digest remain unchanged**. Two exact additional paths are independently authenticated with digest `167bd2d4402c2a6bb8fa37d7f417ff2074ccdff8b52bd263e3c7b75523cdd927`. Regular-file/parent-link checks and live-build exclusion protections remain. Six new positive/tamper rows supplement the original mutation controls. The initial failing `contracts-full` is retained; final rerun and focused guard results are separate receipts.

## Fresh build and regression execution

All commands ran from `{root}`. Each named JSON receipt retains literal argv, UTC timestamps, process identity, exit code and elapsed time; its log/XML retains output. Task environment is recorded in receipts or, for the bounded launcher, `bounded-infrastructure/selection.json`. Debug restore/build used project references:

```bash
dotnet build Hexalith.EventStore.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false
```

The complete solution build passed with zero warnings/errors. `dependency-configuration-observed.json` retains 32 source-reference project/import configuration hashes observed after execution began; those are not relabeled as a pre-execution raw-byte freeze. `debug-project-graph.json` preserves the evaluated restore graphs and project libraries. Tests run as individual built xUnit assemblies, never solution-level `dotnet test`.

| Receipt | Fresh result |
| --- | --- |
{table}

`qualification.json` enumerates exact skips and failures without treating skips as acceptance. `affected-server-regressions.json` proves all 547 admission/authentication/rate regressions passed in the fresh full Server run; only their name inventory is reused. These overlap the full Server result and must not be added to it. `server-secret-guard-disposition.json` binds all 21 reported locations to the unchanged starting revision; 20 additionally match the initial source/evidence inventory. No secret-guard assertion, scanner exemption or historical capture was changed. The root spec location not included in that initial inventory matches the clean starting revision. `testing-red` is an expected diagnostic and is excluded from green results. `fixture-tool-cleanup-tests` verifies the existing process runner's cleanup controls. The initial generic mapped-image comparison included third-party/system images outside the first-party PDB set and is retained; `actor-mapped-sdk-binding.json` is the correctly scoped audit.

## Executed binary/source identity

`binary-final.json` inventories 1,012 Debug binary/manifest paths and {binding_summary['pdbCount']} unique PE/PDB pairs. The final hash-pinned PDB audit binds {binding_summary['ordinaryDocumentCopies']} ordinary document copies and {binding_summary['generatedDocumentCopies']} generated copies with no errors. Generated-disk differences authenticate against matching embedded source and remain explicit in its summary. `bound-source-index-final.json` links {len(binding_index)} document copies to content-addressed archives, including dependency source supplied through project references. `--current-inputs` verifies ordinary PDB inputs across those dependency boundaries too. `debug-binary-freeze.json` and the first audit retain the earlier inventory; `standalone-payload-delta.json` records the intentional refresh of 14 paths for the project outside the solution. The executed core graphs stayed unchanged. `testing-style-delta.json` and `contracts-guard-binary-delta.json` separately record refreshed test-only images; intermediate source audits remain retained under their pre-change directories. Original selected protection images remain in `initial-protection-images/`; `handoff-runtime.json` now records the final producer outputs and origin paths. Diagnostic producer manifests must not replace consumer runtime manifests.

**Execution images differ by build context.** `tested-image-crosswalk.json` maps every actor-loaded SDK image to the selected producer image. For example, actor Client/DomainService images differ from the selected Client/DomainService images used by the bounded graph. The 22 comparable SDK load observations have identical ordinary PDB source checksums, and 11 are byte-identical to the selected images. All 26 first-party actual loads have independent PE/PDB/source bindings. No byte identity, IL equivalence or interchangeable assembly-reference compatibility is inferred for differing images. The table below identifies the selection, not a claim that every lane loaded those bytes. Consumer acceptance must bind its actual assembled graph.

| Producer module | DLL SHA-256 | MVID | PDB SHA-256 |
| --- | --- | --- | --- |
{chr(10).join(modules)}

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
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot={root} -m:1 -nr:false
```

That is a continuation recipe, not an executed command in this packet. Retain and verify the resulting consumer SDK PE/PDB/source graph and actual mapped modules before rerunning its unchanged strict real-provider and 10,001-position authenticated recovery lanes. The old selector belongs to the old packet and cannot establish this source identity. Do not infer equivalence from version text or copy producer `.deps.json` over consumer manifests.

Remaining gates: the failing full Server secret-material guard, 22 missing external Server maps, current-source consumer acceptance, existing skipped/deferred test debt, SM4 acknowledgement p95, Release/package/source equivalence/PD10 and independent whole-story acceptance. No whole Story 12.2 completion, packaged adoption or performance closure is claimed. Build/binding/preservation/cleanup predicates are enumerated in `qualification.json`; a missing or failed predicate cannot produce a qualified status.

`SHA256SUMS` seals all packet files. Verify without modifying consumer state:

```bash
python3 {packet}/verify.py
python3 {packet}/verify.py --current-inputs
```
'''
(packet / 'README.md').write_text(readme)
print(json.dumps({'status': qualification['status'], 'completed_lanes': sum(r['passed_gate'] for r in results), 'required_lanes': len(results)}))
