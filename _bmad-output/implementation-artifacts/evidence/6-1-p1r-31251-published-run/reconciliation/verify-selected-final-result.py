"""Parent verification of one completed frozen run and its fresh independent audit."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--index', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(root / 'tools'))
import p1r_qualification as preparation
import p1r_published_qualification as qualification

require = preparation.require
sha = lambda path: hashlib.sha256(preparation.regular(path)).hexdigest()
read = lambda path: qualification.parse_json(preparation.regular(path))
require(not args.out.exists(), 'parent audit output already exists')
index = read(args.index)
require(index['finished_utc'] and index['errors'] == [], 'canonical driver incomplete or failed')
require(index['qualified'] is False and index['p1r_usable'] is False, 'canonical driver invented acceptance')
require([row['label'] for row in index['commands']] == ['outer-before', 'observe', 'run', 'audit-v2', 'outer-after']
        and all(row['exit_code'] == 0 for row in index['commands']), 'canonical command inventory or exits differ')
for row in index['commands']:
    command = read(Path(row['record']))
    require(command['finished_utc'] and command['exit_code'] == row['exit_code'], 'literal command incomplete')
    preparation.regular(Path(command['output_log']))
execution_root = Path(index['execution'])
audit_root = Path(index['audit'])
execution = read(execution_root / 'execution.json')
audit = read(audit_root / 'audit.json')
inputs, raw, scope = qualification.load_document(Path(index['inputs']['path']), qualification.validate_inputs)
require(scope == 'owner-selected' and inputs['candidate']['version'] == '3.125.1'
        and preparation.digest(raw) == index['inputs']['sha256'] == execution['inputs_sha256'] == audit['inputs_sha256'],
        'selected input identities differ')
require(inputs['rollback'] is None and inputs['selected_additions'] == list(qualification.ADDITIONS), 'selected matrix differs')
producer_path = execution_root / 'executor-source.json'
producer = read(producer_path)
require(sha(producer_path) == execution['executor_source_sha256'] and execution['finished_utc']
        and execution['errors'] == [], 'full execution or source binding incomplete')
qualification.validate_candidate_source(producer, inputs)
preparation.check_current_source(producer, check_python=False)
require(read(audit_root / 'source-binding.json') == producer, 'fresh audit used a different source or interpreter closure')
require(audit['valid_evidence'] is True and audit['errors'] == [] and audit['finished_utc']
        and audit['qualified'] is False and audit['p1r_usable'] is False
        and audit['source_checked_after_all_commands'] is True, 'independent audit did not retain valid unqualified measurements')
decisions_path = Path(index['decisions']['path'])
decisions, _, _ = qualification.load_document(decisions_path, qualification.validate_decisions)
require(sha(decisions_path) == index['decisions']['sha256'] and decisions == audit['decisions'],
        'audit replaced observed owner decisions')
inventory = qualification.executed_case_inventory(inputs)
require(len(inventory) == 19 and sum(map(len, inventory.values())) == 163, 'canonical case inventory differs')
lanes, recovery, failed = {}, {}, []
for name in execution['receipts']:
    path = Path(name)
    require(path.parent == execution_root / 'receipts', 'receipt escaped execution')
    value = read(path)
    if value['schema'] == qualification.LANE_RECEIPT_SCHEMA:
        lane = value['lane']
        require(lane not in lanes and [case['id'] for case in value['cases']] == list(inventory[lane]),
                'lane inventory missing, duplicate or reordered')
        counts = {'attempted': 0, 'passed': 0, 'failed': 0}
        for case in value['cases']:
            actual = preparation.checks_counter(case['checks'])
            require(actual == case['assertions'] and actual['attempted'] > 0, 'case counters omit real checks')
            for key in counts:
                counts[key] += actual[key]
            failed.extend({'lane': lane, 'case': case['id'], 'check': check['id'], 'receipt': str(path)}
                          for check in case['checks'] if not check['passed'])
        require(counts == value['assertions'] == audit['lanes'][lane]['assertions']
                and sha(path) == audit['lanes'][lane]['sha256'], 'raw lane and audited counters or bytes differ')
        lanes[lane] = {'path': str(path), 'sha256': sha(path), 'cases': len(value['cases']), 'assertions': counts}
    else:
        key = 'candidate-restore' if value['schema'].endswith('.restore-receipt.v1') else 'container-cleanup'
        require(key not in recovery and sha(path) == audit['recovery'][key]['sha256']
                and value['assertions'] == audit['recovery'][key]['assertions'], 'recovery receipt differs from audit')
        recovery[key] = {'path': str(path), 'sha256': sha(path), 'assertions': value['assertions']}
require(set(lanes) == set(inventory) and set(recovery) == {'candidate-restore', 'container-cleanup'}, 'required receipt missing')
counts = {key: sum(row['assertions'][key] for row in lanes.values()) for key in ('attempted', 'passed', 'failed')}
require(counts == audit['lane_assertions'] and audit['case_count'] == 163 and len(failed) == counts['failed'],
        'aggregate counters or failed-check inventory differ')
source_proof = execution_root / 'consumers/source/source-evidence.json'
require(read(source_proof) == qualification.source_consumer_binding(source_proof.parent, producer),
        'actual Debug references, assets, locks or loaded binaries differ')
wrappers = {}
for name, directory in (('observe', Path(index['observation'])), ('execution', execution_root), ('audit', audit_root)):
    wrapper_path = directory.with_name(directory.name + '-isolation') / 'isolation.json'
    wrapper = read(wrapper_path)
    private_path = wrapper_path.parent / 'private-execution.json'
    private = read(private_path)
    require(wrapper['passed'] is True and wrapper['command_exit_code'] == 0 and wrapper['errors'] == []
            and private['passed'] is True and private['exit_code'] == 0 and private['errors'] == []
            and private['source_checked_after_cleanup'] is True, 'private or outer execution incomplete')
    require(all(row['remaining'] == [] and row['errors'] == [] for row in wrapper['cleanup'] + private['cleanup']),
            'owned cleanup failed')
    wrappers[name] = {'wrapper': str(wrapper_path), 'wrapper_sha256': sha(wrapper_path),
                      'private': str(private_path), 'private_sha256': sha(private_path)}
require([(row['label'], row['exit_code']) for row in audit['commands']] ==
        [('measure-boundary', 0), ('strict-prepare', 2), ('strict-validate', 2)], 'strict gate outcomes differ')
require('successor support boundary lacks measured owner disposition' in audit['commands'][1]['stderr'],
        'strict gate lost missing-disposition refusal')
table = read(audit_root / 'successor-support-boundary.json')
require(table['status'] == 'measured-pending-disposition' and table['disposition'] is None, 'architect disposition invented')
preparation.check_current_source(producer, check_python=False)
result = {'scope': 'parent independent raw inventory, source consumer, wrapper and audit consistency checks; no acceptance',
          'observed_utc': preparation.stamp(), 'script_sha256': sha(Path(__file__).resolve()),
          'canonical_index': str(args.index), 'canonical_index_sha256': sha(args.index),
          'case_count': 163, 'lane_assertions': counts, 'failed_checks': failed, 'lanes': lanes,
          'recovery': recovery, 'wrappers': wrappers, 'source_consumer_sha256': sha(source_proof),
          'producer_python_check': False, 'python_scope': 'original private interpreter independently bound by producer and audited private proofs; host reader interpreter differs',
          'qualified': False, 'p1r_usable': False, 'valid_measurement_evidence': True}
preparation.write_json(args.out, result)
print(json.dumps({'out': str(args.out), 'sha256': sha(args.out), 'assertions': counts, 'failed_checks': failed,
                  'qualified': False, 'p1r_usable': False}))
