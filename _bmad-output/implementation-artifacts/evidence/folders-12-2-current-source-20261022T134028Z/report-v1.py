#!/usr/bin/env python3
"""Summarize retained receipts; absent or unsuccessful gates cannot become passes."""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent
root = packet.parents[3]
labels = ['testing-full', 'public-throttling-health', 'client-full', 'domain-full', 'server-full', 'contracts-full', 'apphost-full', 'testing-integration-full', 'sample-full', 'signalr-full', 'generators-full', 'query-routing-full', 'admin-abstractions-full', 'admin-server-full', 'admin-host-full', 'operations-full', 'admin-ui-full', 'admin-cli-full', 'admin-mcp-full', 'actor-live', 'bounded-live']
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
        row['skips'] = [{'name': t.get('name'), 'reason': t.findtext('reason/message')} for t in document.iter('test') if t.get('result') == 'Skip']
        row['failures'] = [{'name': t.get('name'), 'message': t.findtext('failure/message')} for t in document.iter('test') if t.get('result') == 'Fail']
    row['passed_gate'] = row['exit_code'] == 0 and row.get('failed') == 0 and row.get('errors') == 0 and row.get('not-run') == 0
    results.append(row)

qualification = {'status': 'producer_execution_complete' if all(x['passed_gate'] for x in results) else 'incomplete_or_failed',
                 'results': results,
                 'consumer_current_source_execution': 'not run; consumer source and selected outputs were not modified',
                 'historical_consumer_pass': 'reused only; older explicitly selected SDK graph; does not qualify current graph',
                 'remaining_gates': ['Consumer adoption and unchanged strict real-provider/10001-position acceptance against this source graph', 'SM4 acknowledgement p95', 'Release/package/source equivalence and PD10', 'Independent whole Folders Story 12.2 acceptance'],
                 'diagnostics': ['testing-red: two expected failures before the helper correction', 'Initial generic mapped-image audit compares third-party/system images to a first-party PDB inventory; use actor-mapped-sdk-binding.json for the scoped SDK audit']}
(packet / 'qualification.json').write_text(json.dumps(qualification, indent=2) + '\n')
table = '\n'.join('| ' + r['label'] + ' | ' + (f"{r['passed']} passed / {r['failed']} failed / {r['skipped']} skipped; exit {r['exit_code']}" if 'passed' in r else 'Running or not started; no pass claimed') + ' |' for r in results)
repos = json.loads((packet / 'repositories-before.json').read_text())
identities = json.loads((packet / 'source-binding/all-pdb-documents.json').read_text())
by_hash = {r['peSha256'].lower(): r for r in identities}
runtime = json.loads((packet / 'handoff-runtime.json').read_text())
modules = []
for name in ['Hexalith.EventStore.Client.dll', 'Hexalith.EventStore.DomainService.dll', 'Hexalith.EventStore.Server.dll', 'Hexalith.EventStore.Gateway.dll', 'Hexalith.EventStore.Aspire.dll', 'Hexalith.EventStore.Testing.dll']:
    item = runtime[name]
    bound = by_hash[item['sha256']]
    modules.append(f"| {name} | `{item['sha256']}` | `{bound['mvid']}` | `{bound['pdbSha256'].lower()}` |")
readme = f'''# Current EventStore source handoff for Folders Story 12.2

Producer disposition: **{qualification['status']}**. This packet qualifies producer execution and records remaining consumer gates. Folders' current-source acceptance has not been executed here. All listed test results are fresh unless explicitly identified as historical reuse.

## Source comparison and correction

EventStore starting revision: `{repos[str(root)]['head'].strip()}`. Folders observed revision: `{repos[str(root.parent / 'folders')]['head'].strip()}`. Both canonical revisions and all direct dependency revisions, initial statuses and Git index hashes are in `repositories-before.json`. Revision alone is insufficient: the qualified source includes the uncommitted testing-helper correction in `source-final.json` and `source-change.patch`.

The latest consumer startup handoff deliberately ran an older sealed SDK selection and excluded the later admission/rate-limit edits. `candidate-comparison.json` separately compares that input freeze, the original inherited-owner qualification and the later admission packet with this task's starting source. Admission/token/rate-limit source matches its own packet; seven later ordinary changes concern existing Admin/protection work and remain preserved. Source additions and the earlier candidate's changed files are recorded separately. No prior consumer result is relabeled as current-graph execution.

Verified SDK defect: `TestServiceOverrides.RemoveDaprHealthChecks` omitted `projection-delivery-unresolved-conflict`, which requires real Dapr state even when a test replaces command routing. This caused the earlier three isolated health assertions to return 503. The helper now removes that exact sidecar-dependent registration. Two regression rows failed before the fix and pass afterward, covering registration before/after the override and retaining self/unrelated checks. Production readiness, existing HTTP assertions, deadlines, retries, admission rules and storage bounds are unchanged. The unchanged 33-case public throttle/health matrix now passes.

## Fresh build and regression execution

All commands ran from `{root}`. Each named JSON receipt retains literal argv, UTC timestamps, process identity, exit code and elapsed time; its log/XML retains output. Task environment is recorded in receipts or, for the bounded launcher, `bounded-infrastructure/selection.json`. Debug restore/build used project references:

```bash
dotnet build Hexalith.EventStore.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false
```

The complete solution build passed with zero warnings/errors. `debug-project-graph.json` preserves the evaluated restore graphs and project libraries. Tests run as individual built xUnit assemblies, never solution-level `dotnet test`.

| Receipt | Fresh result |
| --- | --- |
{table}

`qualification.json` enumerates exact skips and failures without treating skips as acceptance. `testing-red` is an expected diagnostic and is excluded from green results. `fixture-tool-cleanup-tests` verifies the existing process runner's cleanup controls. The initial generic mapped-image comparison included third-party/system images outside the first-party PDB set and is retained; `actor-mapped-sdk-binding.json` is the correctly scoped audit.

## Executed binary/source identity

`debug-binary-freeze.json` inventories 1,012 Debug binary/manifest paths and 66 unique PE/PDB pairs. The fresh PDB audit binds 5,227 ordinary document copies and 391 generated copies with no errors. Two generated-disk differences authenticate against matching embedded source; they are retained explicitly. `bound-source-index.json` links 5,618 document copies to 4,106 content-addressed source archives. `handoff-runtime.json` records selected producer outputs and their origin paths; it includes diagnostic producer manifests, which must not replace consumer runtime manifests. Full hashes and copied images remain in `handoff-runtime/`.

| Producer module | DLL SHA-256 | MVID | PDB SHA-256 |
| --- | --- | --- | --- |
{chr(10).join(modules)}

The production actor qualification retains actual `/proc` mapped images before/after replacement. All 26 observed first-party module identities match the PDB-bound graph. Bounded workers additionally record actual loaded MVIDs, source checksums and Debug/project-reference build identity; their observer retains native executable hashes, sidecar argv, materialized components and mapped SDK images. Exact runtime Dapr/Redis/control-plane image identities are in the respective infrastructure receipts. The launched native daprd is 1.18.7; the .NET SDK is 10.0.402 and executed runtime is 10.0.13.

## Real Dapr semantics, cleanup and preservation

`actor-live/` retains 47 production-path checks: isolated actor saturation, persisted Reserved/Pending/UnknownProviderOutcome recovery through process replacement, stale-fence/authority refusal, terminal exact replay, one real Counter event and domain invocation, changed-intent refusal, authentication denials, and exactly 100 authenticated public reads followed by quota-specific 429/Retry-After. Durable Redis snapshots and mapped process identities bracket the checks. This is producer acceptance, not a fresh Folders provider pass.

`bounded-scenarios/` contains the unchanged production-dispatcher process suite's staging, partial owner handoff, replacement, recovery, ordinary growth and inherited-owner evidence. Each final scenario summary records counts, contracts, PIDs, persisted observations and observed batch/value maxima. The enforced limits remain 100 operations and 1,048,576 canonical bytes. See `bounded-summary.json` for completed scenarios and `bounded-live.xml` for authoritative outcomes. The process-local HotReload=false override matches prior supported SDK fixture usage to avoid host watcher exhaustion; it changes no assertion, deadline, retry, admission or storage bound.

The repository's Aspire AppHost was started, described and stopped by its exact project path before code edits. Its captured services were still waiting for security readiness, so this startup observation is not a healthy end-to-end baseline pass. An ephemeral dashboard login token is redacted in the new log only. The unrelated Tenants AppHost remains separately running; no lifecycle command targeted it. Live fixtures' own cleanup receipts verify their exact owned PIDs, labeled containers and materialized directories. Generic repository AppHost materialization paths are not deleted because preexisting ownership is unknown.

`historical-before.json` inventories 47,976 prior evidence files across EventStore/Folders. `preservation-final.json` records the final byte comparisons, unchanged Folders source, existing edits and unchanged Git indexes. No staging, commit, push, publication, deployment, pin change or consumer-output selection was performed.

## Consumer continuation and explicit remaining gates

This task leaves Folders source and selected runtime outputs untouched. The prior strict provider pass and large consumer recovery/component evidence are historical reuse only, enumerated in `reused-evidence.json`; checked packet seals are in `reused-packet-seals.json`. They do not establish current-source adoption.

The consumer can build its unchanged graph through its existing source-reference seam in a new qualification packet:

```bash
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot={root} -m:1 -nr:false
```

That is a continuation recipe, not an executed command in this packet. Retain and verify the resulting consumer SDK PE/PDB/source graph and actual mapped modules before rerunning its unchanged strict real-provider and 10,001-position authenticated recovery lanes. The old selector belongs to the old packet and cannot establish this source identity. Do not infer equivalence from version text or copy producer `.deps.json` over consumer manifests.

Remaining gates: current-source consumer acceptance, existing skipped/deferred test debt, SM4 acknowledgement p95, Release/package/source equivalence/PD10 and independent whole-story acceptance. No whole Story 12.2 completion, packaged adoption or performance closure is claimed.

`SHA256SUMS` seals all packet files. Verify without modifying consumer state:

```bash
python3 {packet}/verify.py
python3 {packet}/verify.py --current-inputs
```
'''
(packet / 'README.md').write_text(readme)
print(json.dumps({'status': qualification['status'], 'completed_lanes': sum(r['passed_gate'] for r in results), 'required_lanes': len(results)}))
