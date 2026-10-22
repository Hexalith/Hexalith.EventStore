#!/usr/bin/env python3
"""Read-only terminal-receipt and exact identity checks for guard executions."""
import datetime
import hashlib
import json
import importlib.util
from review_audit_checks import unit_mapping_valid, binding_errors
from pathlib import Path

packet = Path(__file__).resolve().parent
target = packet / 'guard-resource-cleanup-v2.json'
if target.exists():
    raise SystemExit('Refusing to overwrite evidence')
errors = []
rows = []
identities = {}
for name in ['server-active-observation.json', 'final-server-process-identity.json', 'selected-final-server-process-identity.json']:
    row = json.loads((packet / name).read_text())
    identities[row['pid']] = {'start_ticks': row['start_ticks'], 'receipt': name}
for row in json.loads((packet / 'guard-server-process-identities.json').read_text())['processes']:
    if row.get('start_ticks'):
        identities[row['pid']] = {'start_ticks': row['start_ticks'], 'receipt': 'guard-server-process-identities.json'}
for label in ['server-full', 'guardfix-server-full', 'guardtight-server-full', 'guardfixtures-server-full', 'guardfinal-server-full']:
    receipt = json.loads((packet / (label + '-command.json')).read_text())
    if 'exit_code' not in receipt:
        errors.append('Execution still active: ' + label)
    pid = receipt['pid']
    expected = identities.get(pid)
    stat = Path('/proc') / str(pid) / 'stat'
    current = None
    try:
        text = stat.read_text()
        current = text[text.rfind(')') + 1:].split()[19]
    except FileNotFoundError:
        pass
    absent = current is None or expected is not None and current != expected['start_ticks']
    if not absent:
        errors.append('Owned guard identity not proved absent: ' + label)
    rows.append({'label': label, 'command_receipt': label + '-command.json', 'pid': pid,
                 'expected_identity': expected, 'current_start_ticks': current, 'absent': absent,
                 'exit_code': receipt.get('exit_code'),
                 'identity_scope': 'PID/start ticks plus completed child receipt' if expected else 'Completed child receipt and PID absence; start ticks were not captured'})
process = json.loads((packet / 'selected-final-server-process-identity.json').read_text())
binding = json.loads((packet / 'source-binding-guardfinal/all-pdb-documents.json').read_text())
errors += binding_errors(binding, json.loads((packet / 'source-binding-guardfinal/summary.json').read_text()))
spec = importlib.util.spec_from_file_location('os_map_reader', '/tmp/eventstore-folders-12-2-latest-20261022T155454Z/tools/bounded_worker_observer.py')
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)
raw_rows = reader.mapping_rows((packet / process['maps']).read_text())
unit = [row for row in binding if row['pe'] == process['unit']]
actual_bound = (unit_mapping_valid(process, raw_rows) and len(unit) == 1 and unit[0]['peSha256'].lower() == process['unit_sha256']
                and unit[0]['pdbSha256'].lower() == process['pdb_sha256']
                and hashlib.sha256((packet / process['maps']).read_bytes()).hexdigest() == process['maps_sha256'])
if not actual_bound:
    errors.append('Actual final Unit process mapping differs from PE/PDB source binding')
result = {'passed': not errors, 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'processes': rows, 'final_actual_mapped_unit_source_bound': actual_bound,
          'final_unit_mvid_from_bound_pe': unit[0]['mvid'] if len(unit) == 1 else None,
          'scope': 'Guard execution processes only; Dapr resources and private-tool directories have separate cleanup receipts.',
          'errors': errors}
with target.open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps(result))
raise SystemExit(0 if result['passed'] else 1)
