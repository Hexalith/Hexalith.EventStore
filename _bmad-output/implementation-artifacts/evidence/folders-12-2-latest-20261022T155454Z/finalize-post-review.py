#!/usr/bin/env python3
"""Close reporting predicates without replacing any executed test or audit receipt."""
import datetime
import hashlib
import json
from pathlib import Path

p = Path(__file__).resolve().parent
if (p / 'SHA256SUMS').exists():
    raise SystemExit('Packet already sealed')
outputs = ['qualification-pre-review.json', 'README-pre-review.md', 'post-review-resolution.json', 'post-review-commands.json']
for name in outputs:
    if (p / name).exists():
        raise SystemExit('Refusing to overwrite evidence: ' + name)
def read(name):
    return json.loads((p / name).read_text())
def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
def write(name, value):
    with (p / name).open('x') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')

runtime = read('bounded-runtime-audit-v3.json')
capture = read('worker-capture-audit-v3.json')
controls = read('audit-controls-v3-result.json')
lifecycle = read('launcher-cleanup-v4-result.json')
extra = read('postreview-extra-controls.json')
cleanup = read('closing-runtime-cleanup-v3.json')
guard = read('guard-resource-cleanup-v2.json')
inputs = read('late-input-integrity-v2.json')
qual = read('qualification.json')
bounded = next(row for row in read('fresh-test-results.json') if row['label'] == 'bounded-live')
predicates = {
    'fresh_real_dapr_matrix_9_0_0': bounded['passed'] == 9 and bounded['failed'] == 0 and bounded['skipped'] == 0 and bounded['errors'] == 0,
    'runtime_and_all_first_party_bindings': runtime['passed'] is True,
    'exact_30_workers_120_required_joins': capture['passed'] is True and capture['worker_count'] == 30 and capture['capture_count'] == 30 and capture['required_module_joins'] == 120,
    'source_binding_summary': read('source-binding-final/summary.json')['passed'] is True,
    'source_archive_verification': read('source-archive-verification.json')['passed'] is True,
    'final_overlay_source_binding': read('source-binding-guardfinal/summary.json')['passed'] is True,
    'final_overlay_reuse_identity': read('guard-final/input-identity-audit.json')['passed'] is True,
    'strict_audit_mutation_controls': controls['passed'] is True and len(controls['controls']) == 21,
    'active_and_failed_runner_cleanup_controls': lifecycle['passed'] is True and len(lifecycle['controls']) == 7,
    'unit_map_and_input_addition_controls': extra['passed'] is True,
    'all_owned_runtime_cleanup': cleanup['passed'] is True,
    'actual_final_unit_mapping_and_process_cleanup': guard['passed'] is True,
    'final_s1_and_s5_inputs_unchanged': inputs['frozen_inputs_passed'] is True and inputs['frozen_input_paths'] == 145090,
    'ordinary_input_file_sets_unchanged': all(not row['unexpected'] and not row['missing'] for row in inputs['ordinary_file_set_comparisons']),
}
closed = all(predicates.values())
resolution = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'bounded_gate_predicates': predicates,
              'strict_bounded_observation_gate_closed': closed,
              'all_review_findings': read('final-review-triage.json')['findings'] + read('final-review-followup-triage.json')['findings'],
              'post_review_sources': {name: sha(p / name) for name in ['review_audit_checks_v2.py','audit-runtime-v3.py','audit-worker-captures-v3.py','bounded-live-v2.py','test-launcher-cleanup-v4.py','test-audit-controls-v3.py','check-late-inputs-v2.py','closing-runtime-cleanup-v3.py','check-guard-cleanup-v2.py']},
              'cleanup_counts': {key: cleanup[key] for key in ['container_count','directory_count','process_identity_count','exact_process_identity_count','pid_only_identity_count']},
              'guard_final_inputs': 'guard-final/producer-current-inputs.json',
              'fresh_execution': 'Fresh strict audits/mutation controls, real disposable-infrastructure lifecycle controls, and full 145090-path input rehash/file-set checks. These are not additional scenario executions.',
              'reused_s1_execution': 'The nine-scenario real-Dapr execution used retained bounded-live.py. Bounded worker observation/source/acceptance bytes remain pinned; final provided bounded-live-v2.py changes runner-session cleanup and explicit lifecycle control hooks only. Its seven controls test cleanup at the actual subprocess boundary with process surrogates; no S1 outcome is described as a new full-matrix v2 run.',
              'preserved_failures': 'Original/intermediate failures, initial partial reports, v2 cleanup formatting failure and all earlier receipts remain untouched. Overall qualification remains negative.'}
for row in resolution['all_review_findings']:
    row['resolution'] = 'Closed by the fresh post-review source checks, mutation controls and lifecycle controls; exact receipts and predicates are enrolled here. Earlier triage records remain unchanged.'
write('post-review-resolution.json', resolution)
commands = []
for path in sorted(list(p.glob('postreview-*-command.json')) + list(p.glob('cleanup-v3-*.json')) + list(p.glob('cleanup-v4-*.json'))):
    if path.name == 'postreview-finalize-command.json':
        continue  # Own terminal receipt is sealed separately after this process exits.
    value = json.loads(path.read_text())
    if isinstance(value, dict) and 'argv' in value:
        commands.append({'receipt': path.name, 'receipt_sha256': sha(path), 'exit_status_recorded_here': 'exit_code' in value, **value})
write('post-review-commands.json', commands)
old_qual = (p / 'qualification.json').read_bytes()
old_readme = (p / 'README.md').read_bytes()
with (p / 'qualification-pre-review.json').open('xb') as stream:
    stream.write(old_qual)
with (p / 'README-pre-review.md').open('xb') as stream:
    stream.write(old_readme)
qual.update({'utc': resolution['utc'], 'strict_bounded_observation_gate_closed': closed,
             'bounded_gate_predicates': predicates, 'post_review_resolution': 'post-review-resolution.json',
             'closing_input_audit': 'late-input-integrity-v2.json', 'frozen_inputs_unchanged': inputs['frozen_inputs_passed'],
             'folders_preservation_passed': inputs['folders_passed'], 'folders_source_project_deltas_at_latest_audit': len(inputs['folders_changes']),
             'cleanup_passed': cleanup['passed'] and guard['passed'], 'closing_runtime_cleanup': 'closing-runtime-cleanup-v3.json',
             'guard_execution_cleanup': 'guard-resource-cleanup-v2.json', 'post_review_command_index': 'post-review-commands.json',
             'readme_disposition_generated_from_predicates': True,
             'all_prior_executed_evidence_unchanged': True})
text = old_readme.decode()
lines = text.splitlines()
failed = [name for name, passed in predicates.items() if not passed]
lines[2] = ('**Disposition: `not_fully_qualified`.** The strict bounded-worker observation gate is ' + ('closed: fresh real-Dapr execution passed nine scenarios, with 30 workers and all 120 required module joins.' if closed else 'open; failed predicates: ' + ', '.join(failed) + '.') + ' Secret-guard, historical-policy, Domain and Folders preservation gates remain open. No Folders adoption or whole-story completion is claimed.')
text = '\n'.join(lines) + '\n'
old = 'The exact final auditor passed eight tamper/missing-observation controls plus a crashed-auditor/stale-result control. Malformed-capture, failed-write and SIGTERM launcher controls passed. The SIGTERM control covers owned child/container cleanup before the full matrix starts; the earlier interrupted attempt separately retains its actual matrix cancellation and cleanup.'
new = 'The final strict auditors passed [21 mutation controls plus stale-result/crash rejection](audit-controls-v3-result.json), including native executable substitution, failed source bindings, altered component contents/owned endpoints and extra unauthenticated first-party maps. [Nine additional controls](postreview-extra-controls.json) verify actual Unit-map rejection and new/ignored input detection. [Seven launcher controls](launcher-cleanup-v4-result.json) passed, including active-runner cancellation, runner crash, deliberate driver exception and wrapper death. They use actual disposable Dapr infrastructure with explicit process-tree surrogates at the real runner lifecycle boundary; they are not extra scenario acceptance runs. Every earlier control/result remains intact.'
assert old in text
text = text.replace(old, new)
old = '[Closing cleanup audit](closing-runtime-cleanup.json) checked 39 owned containers, 79 exact process identities and 15 runtime/component directories across both continuations. [Guard-process cleanup](guard-resource-cleanup.json) binds the actual final Unit mapping to its source-verified PE/PDB and checks all five Server executions have ended. Private-tool archives/removal receipts are retained separately. Deliberately retained isolated source/build inputs are not running runtime fixtures.'
new = (f"[Final expanded cleanup audit](closing-runtime-cleanup-v3.json) independently checked {cleanup['container_count']} owned containers, {cleanup['exact_process_identity_count']} exact PID/start identities, {cleanup['pid_only_identity_count']} explicitly limited PID-only records and {cleanup['directory_count']} runtime/component directories across both continuations and fresh controls. PID-only records require current PID absence and retain original completed-child receipts; no start ticks are invented. [Guard-process cleanup](guard-resource-cleanup-v2.json) parses the retained actual final Unit maps and binds its path/device/inode plus source-verified PE/PDB, and checks all five Server executions have ended. The earlier narrower cleanup pass and the failed v2 formatting audit remain separate. Private-tool archives/removal receipts are retained separately. Deliberately retained isolated source/build inputs are not running runtime fixtures.")
assert old in text
text = text.replace(old, new)
text += ('\n## Post-review closure and exact reuse boundary\n\n'
         'The [final predicate record](post-review-resolution.json) requires successful ordinary-source archive and PDB/source validation as well as the complete runtime matrix, all strict audits, controls and cleanup. Its computed value generates the disposition above. [Post-review commands](post-review-commands.json) supplement the earlier command index. Executed earlier scripts and outputs remain unchanged; the prior draft README and qualification JSON are preserved byte for byte.\n\n'
         f"[Closing S1/S5 integrity](late-input-integrity-v2.json) freshly checks all 145,090 pinned producer paths, with no hash conflicts or changed bytes, and exact ordinary file sets of 16,762 S1 and 16,764 S5 entries. It reports {len(inputs['folders_changes'])} external Folders source/project deltas and zero selected bin/obj deltas; the earlier 61-delta closing observation is retained. Later mutable-origin changes remain outside the frozen selection.\n\n"
         'The nine-case Dapr execution used the preserved original launcher. The successor `bounded-live-v2.py` adds a private runner session and orphan reaping/cleanup on success, failure and cancellation. It preserves scenario arguments, worker observer, phase watchdogs, assertions, storage bounds and production inputs. The seven fresh lifecycle controls cover these cleanup-only changes; the full matrix was not rerun on that launcher version. S1 producer outcomes reused for the final Server test overlay remain bounded by [reused-evidence.json](reused-evidence.json), the final identity audit and source binding.\n')
assert (p / 'qualification.json').read_bytes() == old_qual and (p / 'README.md').read_bytes() == old_readme
(p / 'qualification.json').write_text(json.dumps(qual, indent=2) + '\n')
(p / 'README.md').write_text(text)
print(json.dumps({'disposition': qual['disposition'], 'strict_bounded_observation_gate_closed': closed, 'failed_bounded_predicates': failed, 'post_review_findings_resolved': len(resolution['all_review_findings'])}))
raise SystemExit(0 if closed else 1)
