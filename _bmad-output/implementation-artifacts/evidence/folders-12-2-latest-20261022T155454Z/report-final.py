#!/usr/bin/env python3
"""Summarize fresh executions and explicitly scoped reuse for the final test overlay."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent
selection = json.loads((packet / 'final-server-selection.json').read_text())
root = Path(selection['source_root'])
server_label = selection['full_server_label']
producer_root = json.loads((packet / 'snapshot-selection.json').read_text())['snapshot']
cursor_root = json.loads((packet / 'guard-repair/overlay.json').read_text())['successor']
tightening_root = json.loads((packet / 'guard-tightening/overlay.json').read_text())['successor']
fixture_root = json.loads((packet / 'guard-fixtures/overlay.json').read_text())['successor']
for name in ('fresh-test-results.json', 'affected-server-regressions.json', 'fresh-secret-guard-findings.json', 'commands.json', 'qualification.json', 'README.md'):
    if (packet / name).exists():
        raise SystemExit('Refusing to overwrite evidence: ' + name)

def read(name):
    return json.loads((packet / name).read_text())

def write(name, value):
    with (packet / name).open('x') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

labels = [server_label, 'guardfix-server-full', 'guardtight-server-full', 'guardfixtures-server-full', 'guardfix-cursor-regressions', 'guardtight-focused', 'guardfixtures-focused', selection['focused_label'], 'client-full', 'testing-full', 'contracts-full', 'domain-full',
          'domain-full-repeat', 'domain-failure-focused', 'runtime-preflight',
          'actor-live', 'bounded-live', 'public-throttling-health', 'testing-integration-full']
tests = []
all_cases = {}
for label in labels:
    xml = packet / (label + '.xml')
    document = ET.parse(xml).getroot()
    assemblies = document.findall('assembly')
    row = {'label': label, 'xml': xml.name, 'xml_sha256': sha(xml),
           'execution_source_root': str(root) if label in (server_label, selection['focused_label']) else cursor_root if label.startswith('guardfix-') else tightening_root if label.startswith('guardtight-') else fixture_root if label.startswith('guardfixtures-') else producer_root,
           'relation_to_final_server_overlay': 'Fresh final Server overlay execution' if label in (server_label, selection['focused_label']) else 'Earlier intermediate guard execution; failures retained' if label.startswith(('guardfix-', 'guardtight-', 'guardfixtures-')) else 'Fresh S1 execution, reused for unchanged producer bytes under explicit identity audit',
           **{key: sum(int(a.get(key, '0')) for a in assemblies) for key in ('passed', 'failed', 'skipped', 'errors', 'total', 'not-run')},
           'failures': [], 'skips': []}
    for case in document.findall('.//test'):
        all_cases.setdefault(label, {})[case.get('name')] = case.get('result')
        if case.get('result') == 'Fail':
            row['failures'].append({'name': case.get('name'), 'message': case.findtext('failure/message'), 'stack': case.findtext('failure/stack-trace')})
        if case.get('result') == 'Skip':
            row['skips'].append({'name': case.get('name'), 'reason': case.findtext('reason/message') or case.findtext('reason')})
    tests.append(row)
write('fresh-test-results.json', tests)

initial = packet.parent / 'folders-12-2-gates-20261022T151209Z/affected-server-regressions.json'
inventory = json.loads(initial.read_text())
names = [row['name'] for row in inventory['rows']]
affected = [{'name': name, 'fresh_result': all_cases[server_label].get(name, 'MISSING')} for name in names]
write('affected-server-regressions.json', {'inventory_kind': 'Only names reused, no earlier result substituted', 'inventory_source': str(initial), 'inventory_sha256': sha(initial),
    'result_source': server_label + '.xml', 'count': len(affected), 'passed': len(affected) == 547 and all(row['fresh_result'] == 'Pass' for row in affected), 'rows': affected})

guard = next((failure for row in tests if row['label'] == server_label for failure in row['failures'] if failure['name'].endswith('.TrackedReusableContent_DoesNotContainUsableSecrets')), None)
findings = []
reported_count = 0
if guard:
    message = guard['message']
    count_match = re.search(r'but was\s+(\d+)', message)
    reported_count = int(count_match.group(1)) if count_match else None
    details = message.split('Only path and line are reported: ', 1)[1] if 'Only path and line are reported: ' in message else ''
    for location in details.strip().split(', ') if details else []:
        name, line = location.rsplit(':', 1)
        file = root / name
        findings.append({'path': name, 'line': int(line), 'snapshot_sha256': sha(file)})
complete_guard = read(selection['complete_guard_inventory'])
if (complete_guard['snapshot'] != str(root) or not complete_guard['binary_unchanged']
        or not complete_guard['enumerated_paths_unchanged'] or complete_guard['changed_input_paths']
        or complete_guard['index_before_sha256'] != complete_guard['index_after_sha256']
        or complete_guard['reported_locations'] != reported_count
        or len(complete_guard['locations']) != reported_count
        or complete_guard['assembly_sha256'] != sha(Path(complete_guard['assembly']))):
    raise RuntimeError('Complete owning-reader inventory does not authenticate the final guard execution')
display = [(row['path'], row['line']) for row in findings]
full_display = [(row['path'], row['line']) for row in complete_guard['locations'][:200]]
if display != full_display:
    raise RuntimeError('XML display differs from complete owning-reader inventory')
write('fresh-secret-guard-findings.json', {'source': 'fresh ' + server_label + '.xml', 'source_root': str(root), 'reported_count': reported_count,
    'retained_locations': len(complete_guard['locations']), 'all_locations_retained': True, 'xml_display_limit': 200, 'xml_display_locations': findings,
    'complete_owning_reader': selection['complete_guard_inventory'], 'complete_owning_reader_sha256': sha(packet / selection['complete_guard_inventory']),
    'locations': complete_guard['locations'], 'classifications': selection['complete_guard_classification'],
    'relationship': 'Original21 analysis and historical rewrites are separate, in original-21-secret-analysis.json and historical-guard-input-delta.json. No exemptions inferred.'})

commands = []
for file in sorted(packet.rglob('*.json')):
    value = json.loads(file.read_text())
    if isinstance(value, dict) and 'argv' in value and 'utc_start' in value:
        if str(Path(__file__).resolve()) in value['argv']:
            continue  # The summarizer's completed receipt remains separately retained.
        if 'exit_code' not in value and file.parent == packet:
            raise RuntimeError('Command is still active: ' + file.name)
        commands.append({'receipt': str(file.relative_to(packet)), 'exit_status_recorded_here': 'exit_code' in value, **value})
write('commands.json', commands)

runtime = read('bounded-runtime-audit.json')
capture = read('worker-capture-audit.json')
controls = read('audit-controls-result.json')
cleanup = read('closing-runtime-cleanup.json')
guard_cleanup = read('guard-resource-cleanup.json')
inputs = read('late-input-integrity.json')
actor = read('actor-runtime-audit.json')
binding = read('source-binding-final/summary.json')
overlay_binding = read(selection['pdb_binding'] + '/summary.json')
overlay_identity = read(selection['identity_audit'])
archive = read('source-archive-verification.json')
bounded = next(row for row in tests if row['label'] == 'bounded-live')
coverage_closed = (bounded['passed'] == 9 and bounded['failed'] == 0 and bounded['errors'] == 0 and bounded['skipped'] == 0
                   and runtime['passed'] and capture['passed'] and controls['passed'] and cleanup['passed']
                   and inputs['frozen_inputs_passed'] and overlay_identity['passed'] and overlay_binding['passed'] and guard_cleanup['passed'])
revisions = [{'repository': name, 'head': value['repository']['head'], 'ordinary_inputs': len(value['inputs'])}
             for name, value in read('origin-before.json').items()]
result = {'disposition': 'not_fully_qualified', 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'scope': 'S1 fresh isolated latest-selected EventStore producer graph plus separately bound final Server test-only overlay; no consumer adoption or whole-story acceptance.',
    'source_revisions': revisions, 's1_source_archive': 'ordinary-source.tar.gz', 's1_source_archive_sha256': sha(packet / 'ordinary-source.tar.gz'),
    's1_source_inventory': 'snapshot-inputs.json', 's1_binary_inventory': 'debug-binary-freeze.json',
    'final_server_test_overlay': selection,
    'original_selected_server_execution': {'receipt': 'server-full-command.json', 'result': 'interrupted after a proven cursor loop; no completed XML or passing outcome', 'diagnosis': 'server-infinite-loop-cancellation.json'},
    'strict_bounded_observation_gate_closed': coverage_closed, 'worker_count': capture['worker_count'], 'required_module_joins': capture['required_module_joins'],
    's1_pdb_source_binding_passed': binding['passed'], 's1_source_archive_verified': archive['passed'],
    'final_server_pdb_source_binding_passed': overlay_binding['passed'], 'test_overlay_reuse_identity_passed': overlay_identity['passed'],
    'complete_historical_packet_materialization_preserved': False,
    'historical_packet_materialization_audit': 'guard-final/internal-actor-snapshot-seal-explanation.json',
    'frozen_inputs_unchanged': inputs['frozen_inputs_passed'], 'folders_preservation_passed': inputs['folders_passed'],
    'closing_input_audit': 'late-input-integrity.json', 'earlier_input_audit_retained': 'final-input-integrity.json',
    'actor_runtime_audit_passed': actor['passed'], 'cleanup_passed': cleanup['passed'] and guard_cleanup['passed'],
    'guard_execution_cleanup': 'guard-resource-cleanup.json',
    'fresh_tests': [{key: value for key, value in row.items() if key not in ('failures', 'skips')} for row in tests],
    'fresh_secret_guard_reported_count': reported_count, 'secret_guard_original_history_gate_closed': False,
    'remaining_gates': ['Original immutable-history credential material/policy blocker; external rewritten bytes do not retrospectively qualify the original sealed graph.',
        'Current fresh full Server failures, enumerated in fresh-test-results.json; all false positives remain failures until lawfully resolved.',
        'Selected internal-actor historical mirror omits217 ignored members and LF-normalizes261 captured sources; origin current3410-member seal passes, while the isolated mirror seal fails. Original21 lineage is separate.',
        'Unchanged sealed-capture assertion demands a verified historical false positive, conflicting with selected .diff source recognition; assertion and historical bytes are preserved.',
        'Selected guard drops diff hunk-header text: separate pre-existing false-negative gap.',
        'Preserved Domain full-suite nondeterminism, including the current100ms setup timeout; focused/repeat results are diagnostic.',
        'Strict Folders preservation predicate failed for external source/project drift. Exact deltas are retained; this task did not modify or restore Folders files.',
        'Fresh Folders source-reference adoption, exact assembled runtime/source binding, strict provider and10001-position authenticated recovery acceptance.',
        'Existing skipped/deferred debt, SM4 acknowledgement p95, Release/package/source equivalence/PD10, independent whole-story acceptance.'],
    'earlier_failures_preserved': ['folders-12-2-current-source-20261022T134028Z', 'folders-12-2-gates-20261022T151209Z'],
    'historical_results_substituted_for_current': False, 'explicit_test_overlay_reuse': 'reused-evidence.json',
    'folders_adoption_claimed': False, 'whole_story_complete': False, 'assistant_staged_committed_pushed': False,
    'package_pins_changed_by_assistant': False, 'published_or_deployed': False}
write('qualification.json', result)

unit_rows = [row for row in read(selection['pdb_binding'] + '/all-pdb-documents.json')
             if row['pe'] == str(root / 'tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll')]
if len(unit_rows) != 1:
    raise RuntimeError('Final Server test PE/PDB binding is not unique')
unit = unit_rows[0]
lines = [
    '# Folders Story 12.2 — EventStore producer qualification continuation', '',
    '**Disposition: `not_fully_qualified`.** The strict bounded-worker observation gate is closed: fresh real-Dapr execution passed nine scenarios, with 30 workers and all 120 required module joins. Secret-guard, historical-policy, Domain and Folders preservation gates remain open. No Folders adoption or whole-story completion is claimed.', '',
    '## Source selection and exact inputs', '',
    'The user selected “use latest” after another session committed a draft packet and changed source/dependency inputs. S1 freezes that ordinary producer selection at root revision `cb414530223f77cc845450a4877a4472559de2ad` plus exact overlays. Historical checkout materialization has a separately recorded limitation below. Later mutable-origin edits are preserved and are outside this qualification. Successive isolated test stages preserve S1 and every earlier failure.', '',
    '| Stage | Input root | Execution scope |', '| --- | --- | --- |',
    f'| S1 | `{producer_root}` | Fresh Debug solution, producer/Core regressions, actor and bounded real-Dapr acceptance |',
    f'| S2 | `{cursor_root}` | Cursor repair; fresh intermediate Server execution |',
    f'| S3 | `{tightening_root}` | Empty-call tightening and generated signing-key fixture; fresh intermediate Server execution |',
    f'| S4 | `{fixture_root}` | Positive fixture self-ingestion repair; fresh intermediate Server execution |',
    f'| Final | `{root}` | Cursor fixture self-ingestion repair and two additional owning-reader rows; fresh final Server execution |', '',
    'All isolated input roots are deliberately retained build/review inputs. Runtime fixture directories are separately cleaned. The mutable EventStore checkout and mutable Folders checkout are not qualified by these isolated runs.', '',
    '| Repository | Selected revision |', '| --- | --- |',
]
for row in revisions:
    name = row['repository'].split('/eventstore', 1)[1].lstrip('/') or 'EventStore root'
    lines.append(f"| {name} | `{row['head']}` |")
lines += ['',
    f"S1 has 16,762 ordinary inputs in [snapshot-inputs.json](snapshot-inputs.json), archived in `ordinary-source.tar.gz` (SHA-256 `{sha(packet / 'ordinary-source.tar.gz')}`). [Source archive verification](source-archive-verification.json) checks every member. [Tracked guard inputs](tracked-guard-inputs.json), [Debug binaries](debug-binary-freeze.json) and [frozen worker runtime](qualified-worker-runtime-inputs.json) pin the other selected inputs.", '',
    f"The final test overlay and its exact source/binary inventories are enrolled in [final-server-selection.json](final-server-selection.json). Its [identity audit]({selection['identity_audit']}) justifies only the stated reuse; [reused-evidence.json](reused-evidence.json) names each reused S1 lane. They are fresh S1 executions, not reruns on the final test overlay. No earlier handoff positive result substitutes for current acceptance.", '',
    f"Final Server.Tests PE SHA-256: `{unit['peSha256'].lower()}`; PDB SHA-256: `{unit['pdbSha256'].lower()}`; MVID: `{unit['mvid']}`. Its separate [PDB source audit]({selection['pdb_binding']}/summary.json) passed. The 63-pair S1 audit and each intermediate/final test binding remain separately retained. Generated documents use checksum-authenticated embedded bytes; ordinary disk sources remain authoritative. Explicit generated disk differences are retained in those summaries.", '',
    '## Changes and secret-material findings', '',
    'SDK fixture signing/channel/client values now use runtime cryptographic generation. The diff-source cursor now strictly decreases across LF/CRLF and deleted targets. Arbitrary empty calls no longer qualify as runtime sources; the narrow literal-free `Resources.OfType<ParameterResource>()` form and existing exact cancellation/configuration/cryptographic recognizers remain. Positive-control fixture construction retains its runtime inputs and original assertions. No assertion, deadline, retry, admission rule, storage limit or package pin was changed by this task.', '',
    '| Decision | How it works and benefit | Cost or constraint |', '| --- | --- | --- |',
    '| Runtime fixture generation | Host/client share cryptographically generated material; authentic behavior and rejection assertions still execute | Values vary each run, so both sides must use the same fixture value; affected checks verify this |',
    '| Worker lifetime capture | Worker waits for a separate observer to authenticate actual living OS maps; short phases cannot disappear between samples | Linux/Python observer overhead is opt-in and remains inside the existing phase watchdog |',
    '| Authenticated generated source | Embedded generated bytes must match portable PDB checksums; ordinary source still comes from disk | Requires valid embedded source or matching disk bytes; failures remain failures |',
    '| Preserve immutable negative evidence | Keep original bytes/hashes and classify findings individually | Global guard cannot pass while usable historical material remains forbidden; rewriting or broadly excluding it is outside this task |',
    '| Preserve sealed-control assertion | Keep both the selected source-language behavior and the existing expectation visible | The stale expectation remains failed; deliberately restoring a false positive or changing the assertion was not chosen |', '',
    'The [original 21-location analysis](original-21-secret-analysis.json) authenticated all 21 as unchanged at the initial investigation. Its classifications are 2 usable offline fixture signing keys, 3 literal test client secrets, 1 detector reproduction, 3 verified negative probes, and 12 verified false positives (5 runtime sources, 3 resource references, 2 nulls, 2 cryptographic generation). No provider authentication was attempted or inferred. Exact locations, file/line hashes and the offline signing check are retained.', '',
    'Original immutable-history blocker: the original graph contained reusable fixture material at `internal-actor-admission-20261022/final-source.patch:577,1300` (SHA-256 `d8d028f2155b6e3a58eccc81d58f55fbb85ba4ca600a25b5afdffb5a74a039ca`) and `review-source.patch:481` (SHA-256 `c17f28cc74090e96d6a17e0531ef02f982f924ecd4437b4567dda8991288cb0d`). [Historical Git identity](original-history-git-identity.json) checks those older bytes separately from the current paths. Changing those captured lines and dependent seals would violate the historical-evidence constraint. Excluding evidence broadly or admitting usable keys would violate the detection constraint. Later external rewrites are recorded in [historical-guard-input-delta.json](historical-guard-input-delta.json); those different bytes cannot retrospectively qualify the original sealed graph.', '',
    'The latest selected internal-actor mirror also has an exact construction limitation: 217 ignored seal members are absent and 261 captured `.cs` members have CRLF converted to LF. Every such mismatch is verified against seal-matching origin bytes in [internal-actor-snapshot-seal-explanation.json](guard-final/internal-actor-snapshot-seal-explanation.json). The current mutable-origin packet passes all 3410 members of its current seal; the isolated mirror fails its own check. Neither result restores the original21 older byte identities. Original packet verification, ordinary producer source binding and actual runtime-map coverage remain separately scoped; no full historical-packet preservation claim is made for the isolated checkout.', '',
    'The selected latest graph also tracks archived source copies and test receipts that were absent from the initial 21-location graph. [Fresh guard findings](fresh-secret-guard-findings.json) distinguish the final XML display from the complete pinned owning-reader inventory and per-location semantic classifications. A path/language-context false positive remains an actual guard failure; no archive or packet is exempted wholesale.', '',
    'The unchanged `SealedP1RRemediationSourceCapture_HidesOnlyTheKnownFalsePositive` assertion requires a finding for benign `new CancellationToken(true)` at sealed `source-candidate.diff:2590`. The selected `.diff` source-language recognizer correctly yields no finding. Historical bytes and their seal still match. Closure requires changing that assertion or restoring conservative false-positive behavior; neither was done. The separate [dropped hunk-header detection gap](guard-hunk-header-gap.json) also remains explicit.', '',
    '## Fresh executions and scoped reuse', '',
    '| Lane | Pass | Fail | Skip | Execution root |', '| --- | ---: | ---: | ---: | --- |',
]
for row in tests:
    stage = 'Final' if row['execution_source_root'] == str(root) else 'S2' if row['execution_source_root'] == cursor_root else 'S3' if row['execution_source_root'] == tightening_root else 'S4' if row['execution_source_root'] == fixture_root else 'S1'
    lines.append(f"| [{row['label']}]({row['xml']}) | {row['passed']} | {row['failed']} | {row['skipped']} | {stage} |")
lines += ['',
    'These are separate executions, not additive totals. All failure messages, skips, XML hashes and source scopes are retained in [fresh-test-results.json](fresh-test-results.json). The 547-name affected Server inventory is joined only to the final full XML in [affected-server-regressions.json](affected-server-regressions.json). Python observer regressions passed all 10 cases.', '',
    'The first selected S1 Server execution was interrupted after a proven cursor loop; it has no completed XML and is never reported as passing. Its exact process identity, triage stack, cancellation and private-dump removal are retained. The initial new cursor test build failed CA1062; its narrow parameter-validation correction and successful rebuild remain separate. All interrupted/failed earlier acceptance, Domain failures and tooling audit failures remain preserved.', '',
    'The current Domain full-suite failure occurred during unmeasured history setup under the unchanged 100ms deadline. The unchanged focused check and separate full repeat passed; these diagnostic outcomes do not erase the failed run. [domain-failure-analysis.json](domain-failure-analysis.json) and the earlier sealed continuation retain the distinct earlier Domain failures.', '',
    'Exact argv, cwd, environment overrides, UTC times, PID, elapsed time and exit status are in each `*-command.json` receipt and its log. [commands.json](commands.json) indexes completed command receipts before this summarizer; its own completed receipt is retained separately. Main build flags are `-c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false`. Test-only successors use `--no-restore -p:BuildProjectReferences=false` and unchanged authenticated producer outputs; evaluated project properties/references are retained.', '',
    '## Actual runtime mappings and cleanup', '',
    'The bounded worker stays alive after loading its four required CLR modules while a separate Python process reads actual `/proc` mappings. The observer verifies PID/start ticks before/after, opened mapped PE device/inode, PE/PDB hashes and extra SDK mappings, and retains raw maps/errors. The original parent phase watchdog owns cancellation and its deadline. The audit requires the exact nine scenarios, all phases and all original storage bounds.', '',
    '| Required module | PE SHA-256 | PDB SHA-256 | CLR/PE MVID |', '| --- | --- | --- | --- |',
]
modules = {}
for row in capture['rows']:
    modules.setdefault(row['module'], row)
for name, row in sorted(modules.items()):
    lines.append(f"| {name} | `{row['dll_sha256']}` | `{row['pdb_sha256']}` | `{row['mvid']}` |")
lines += ['',
    '[Runtime audit](bounded-runtime-audit.json), [120 module joins](worker-capture-audit.json), `worker-maps/` and `bounded-scenarios/` retain every worker PID/start tick, argv, native executable identity, raw map, required loaded MVID and source checksum. Docker creation/inspection receipts bind image IDs and digests; native daprd is version 1.18.7, SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`. SDK 10.0.402 and mapped .NET runtime 10.0.13 are recorded. Actor acceptance has 47 checks and 26 authenticated first-party OS-map observations; actor MVIDs are attributed from bound PE metadata, and its original PID-only receipts lack start ticks. They are not presented as bounded-worker CLR observations.', '',
    'The exact final auditor passed eight tamper/missing-observation controls plus a crashed-auditor/stale-result control. Malformed-capture, failed-write and SIGTERM launcher controls passed. The SIGTERM control covers owned child/container cleanup before the full matrix starts; the earlier interrupted attempt separately retains its actual matrix cancellation and cleanup.', '',
    f"[Closing cleanup audit](closing-runtime-cleanup.json) checked {cleanup['container_count']} owned containers, {cleanup['exact_process_identity_count']} exact process identities and {cleanup['directory_count']} runtime/component directories across both continuations. [Guard-process cleanup](guard-resource-cleanup.json) binds the actual final Unit mapping to its source-verified PE/PDB and checks all five Server executions have ended. Private-tool archives/removal receipts are retained separately. Deliberately retained isolated source/build inputs are not running runtime fixtures.", '',
    f"The [closing input audit](late-input-integrity.json) reports frozen producer integrity `{inputs['frozen_inputs_passed']}` and strict Folders preservation `{inputs['folders_passed']}`. Folders source/project drift and Git head/branch/index/status are retained exactly; this task did not modify, restore or build Folders. Selected bin/obj observations are timestamped in that audit. External changes are preserved and cannot support a claim that all Folders source remained unchanged.", '',
    '## Seal and remaining gates', '',
    'The original 6101-file handoff seal is `0b4d5ade382ea889b78fdb5cba7783c7f0fbaa1bdc68d06646b4fc4ade732135`; the 5031-file initial continuation seal is `eeff5272baa8ab53fb1fef872aca9db1f7fd0fb1a094d6e71a12c263778df1ec`. Both were freshly reverified, and their failed/interrupted dispositions remain. This new directory is sealed only after finalization. `SHA256SUMS` covers every regular file except itself; verification checks the complete file set.', '',
    '```sh', 'python3 verify.py', 'python3 verify.py --final-producer-inputs', 'python3 verify.py --current-inputs', '```', '',
    'Run these commands from this packet directory. The first verifies this immutable packet. The second additionally verifies the explicitly selected isolated producer/final-test inputs. The third checks file hashes in the original strict selected-input/Folders inventory and is expected to fail for recorded external Folders drift; Git metadata preservation is separately evaluated in the closing input audit. A valid seal authenticates the negative disposition; it is not acceptance.', '',
]
lines.extend('- ' + gate for gate in result['remaining_gates'])
lines += ['', 'No staging, commit, push, package-pin change, publication or deployment was performed by this task. External Git/source/dependency mutations are recorded separately.']
with (packet / 'README.md').open('x') as stream:
    stream.write('\n'.join(lines) + '\n')
print(json.dumps({'disposition': result['disposition'], 'strict_bounded_observation_gate_closed': coverage_closed, 'tests': result['fresh_tests'], 'fresh_secret_guard_reported_count': reported_count}))
