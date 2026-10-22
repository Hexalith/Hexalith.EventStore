#!/usr/bin/env python3
"""Write a negative qualification disposition from explicit fresh receipts; never infer missing gates."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent

def read(name):
    return json.loads((packet / name).read_text())

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

labels = ['server-full', 'client-full', 'domain-full', 'domain-full-repeat', 'contracts-full', 'testing-full',
          'domain-sweep-focused', 'domain-retained-identity-focused', 'actor-live', 'bounded-live',
          'runtime-preflight', 'runtime-preflight-bounded', 'runtime-preflight-final']
tests = []
for label in labels:
    receipt = read(label + '.json')
    if 'exit_code' not in receipt:
        raise RuntimeError('Still-running command: ' + label)
    assembly = ET.parse(packet / (label + '.xml')).getroot().find('assembly')
    if assembly is None:
        raise RuntimeError('Missing assembly result: ' + label)
    failed = [{'name': row.get('name'), 'message': row.findtext('./failure/message')}
              for row in assembly.findall('.//test') if row.get('result') == 'Fail']
    skipped = [{'name': row.get('name'), 'reason': row.findtext('./reason/message')}
               for row in assembly.findall('.//test') if row.get('result') == 'Skip']
    tests.append({'receipt': label + '.json', 'xml': label + '.xml', 'receipt_sha256': sha(packet / (label + '.json')),
                  'xml_sha256': sha(packet / (label + '.xml')), 'exit_code': receipt['exit_code'],
                  **{name: int(assembly.get(name, '-1')) for name in ('passed', 'failed', 'skipped', 'errors', 'total')},
                  'failures': failed, 'skips': skipped})

runtime = read('bounded-runtime-audit.json')
workers = read('worker-capture-audit.json')
actor = read('actor-live/result.json')
actor_bindings = read('actor-mapped-sdk-binding.json')
preservation = read('preservation-final-result.json')
cleanup = read('cleanup-final-result.json')
pdb = read('source-binding-final/summary.json')
controls = read('launcher-cleanup-v2-result.json')
server = next(row for row in tests if row['receipt'] == 'server-full.json')
initial_domain = next(row for row in tests if row['receipt'] == 'domain-full.json')
repeat_domain = next(row for row in tests if row['receipt'] == 'domain-full-repeat.json')
if not all((runtime['passed'], workers['passed'], actor['passed'], actor_bindings['passed'],
            preservation['passed'], cleanup['passed'], pdb['passed'], controls['passed'])):
    raise RuntimeError('Required identity/lifecycle/preservation predicate failed; retain evidence and resolve before sealing.')
if server['failed'] != 1 or server['exit_code'] != 1 or initial_domain['failed'] != 2:
    raise RuntimeError('Observed negative gates differ from reviewed disposition; investigate before writing report.')

result = {
    'disposition': 'not-fully-qualified',
    'scope': 'EventStore producer continuation for Folders Story12.2; no consumer adoption or whole-story completion',
    'fresh_tests': tests,
    'counting_note': 'Focused rows overlap broad suites. Repeated full executions and preflights are separate executions, not additional distinct coverage.',
    'gates': {
        'debug_project_reference_build': read('debug-source-build.json')['exit_code'] == 0,
        'pe_pdb_current_source_binding': pdb['passed'],
        'full_server_secret_guard': False,
        'bounded_nine_case_behavior': read('bounded-live.json')['exit_code'] == 0,
        'bounded_exact_matrix_phase_storage_audit': runtime['passed'],
        'bounded_all_30_workers_120_external_module_joins': workers['passed'] and workers['required_module_joins'] == 120,
        'fresh_actor_acceptance': actor['passed'],
        'actor_all_26_first_party_load_bindings': actor_bindings['passed'],
        'owned_cleanup': cleanup['passed'],
        'protected_file_revision_and_staged_content_preservation': preservation['passed'],
        'raw_git_index_bytes_unchanged': all(row['raw_index_bytes_unchanged'] for row in preservation['repositories'].values()),
        'domain_initial_full_regression': False,
        'domain_separate_repeat_full_regression': repeat_domain['exit_code'] == 0,
        'domain_nondeterminism_resolved': False,
        'folders_current_source_adoption': False,
    },
    'bounded_summary': {'scenarios': runtime['scenario_count'], 'workers': workers['worker_count'],
                        'external_required_module_joins': workers['required_module_joins'],
                        'executed_launcher': 'bounded-live-v3-executed.py',
                        'cleanup_successor': 'bounded-live-v4.py',
                        'successor_execution_scope': 'Three real-infrastructure malformed-data/write-error/SIGTERM controls, not the full acceptance matrix'},
    'source_inventory': {'file': 'source-final.json', 'sha256': sha(packet / 'source-final.json')},
    'binary_inventory': {'file': 'debug-binary-freeze.json', 'sha256': sha(packet / 'debug-binary-freeze.json')},
    'pdb_document_archive_index': {'file': 'bound-source-index.json', 'sha256': sha(packet / 'bound-source-index.json')},
    'reused_evidence': read('reused-evidence.json'),
    'remaining_gates': [
        'Full Server guard: 19 retained findings, including three fixed credential occurrences in immutable historical patch evidence; exact rows and redacted classifications in secret-disposition.json.',
        'Two fresh DomainService full-run failures passed focused execution; separate broad repeat retained. Scheduling/nondeterminism is not fixed or qualified away.',
        'Current-source Folders acceptance, authenticated recovery and assembled consumer graph qualification; Folders source/selected outputs remain unchanged.',
        'Existing skipped/deferred test debt, SM4 acknowledgement p95, Release/package/source-equivalence/PD10 and independent whole-story acceptance.',
    ],
    'prohibited_mutations': 'No staging, commit, push, publication, deployment, dependency-pin change or Folders source/output selection performed.',
    'index_note': 'The failed raw-index preservation probe remains retained. Read-only Git status refreshed root stat-cache bytes; staged entries remain equal to HEAD, matching the initially unstaged-only tree. No index rewrite or staging command was used.'
}
with (packet / 'qualification.json').open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps({'disposition': result['disposition'], 'fresh_test_executions': len(tests), 'bounded_workers': workers['worker_count'], 'server_guard': False}))
