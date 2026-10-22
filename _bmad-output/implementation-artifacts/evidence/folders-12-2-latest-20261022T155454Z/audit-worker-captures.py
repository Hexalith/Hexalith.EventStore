#!/usr/bin/env python3
"""Join all phase proofs to independently captured process and OS-file identities."""
import hashlib
import importlib.util
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
if (packet / 'worker-capture-audit.json').exists():
    raise SystemExit('Refusing to overwrite evidence: worker-capture-audit.json')
root = Path(__import__('os').environ['EVENTSTORE_QUALIFICATION_SOURCE_ROOT']).resolve()
spec = importlib.util.spec_from_file_location('bounded_observer', root / 'tools/bounded_worker_observer.py')
observer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(observer)
required = observer.REQUIRED
errors = []
proofs = [json.loads(p.read_text()) for p in sorted((packet / 'bounded-scenarios').glob('*/*.json'))]
captures = [(path, json.loads(path.read_text())) for path in sorted((packet / 'worker-maps').glob('*.json'))]
source_bindings = json.loads((packet / 'source-binding-final/all-pdb-documents.json').read_text())
by_hash = {row['peSha256'].lower(): row for row in source_bindings}
rows = []
if len(proofs) != 30 or len(captures) != 30:
    errors.append('Require exactly 30 worker proofs and 30 synchronous captures.')
if len({p['pid'] for p in proofs}) != 30:
    errors.append('Worker PIDs are missing or duplicated.')
used = set()
for proof in proofs:
    candidates = [(path, capture) for path, capture in captures if capture['pid'] == proof['pid']]
    if len(candidates) != 1:
        errors.append('Missing or duplicate capture for PID ' + str(proof['pid']))
        continue
    path, capture = candidates[0]
    used.add(path)
    process_key = (capture['pid'], capture['start_ticks'])
    if not capture['passed'] or capture['errors'] or capture['start_ticks'] != capture.get('start_ticks_after'):
        errors.append('Rejected or changed process identity: ' + str(process_key))
    request = capture['request']
    if (request['pid'], request['start_ticks']) != process_key:
        errors.append('Worker request does not match external identity: ' + str(process_key))
    if capture['observer_pid'] == capture['pid']:
        errors.append('Observer was not a separate process: ' + str(process_key))
    if capture['observer_script_sha256'] != hashlib.sha256(Path(capture['observer_script']).read_bytes()).hexdigest():
        errors.append('Observer source changed: ' + str(process_key))
    raw_rows = observer.mapping_rows(capture['raw_maps'])
    if not raw_rows or {x['name'] for x in request['modules']} != required:
        errors.append('Missing maps or requested inventory: ' + str(process_key))
    runtime = {x['name']: x for x in proof['runtime']}
    for module in request['modules']:
        actual = runtime.get(module['name'])
        if not actual or any(module[k] != actual[k] for k in ('pe', 'pdb', 'peSha256', 'pdbSha256', 'loadedMvid')):
            errors.append('Phase runtime differs from observed request: ' + str(process_key) + ':' + module['name'])
        matches = [x for x in capture['images'] if x['path'] == module['pe']]
        if len(matches) != 1:
            errors.append('Missing or duplicate module: ' + str(process_key) + ':' + module['name'])
            continue
        image = matches[0]
        if image['dll_sha256'] != module['peSha256'].lower() or image['pdb_sha256'] != module['pdbSha256'].lower():
            errors.append('Phase module hashes differ: ' + str(process_key) + ':' + module['name'])
        if (image['pid'], image['start_ticks']) != process_key:
            errors.append('Image PID/start differs: ' + str(process_key))
        mapped = [x for x in raw_rows if (x['device_major'], x['device_minor'], x['inode']) == (image['device_major'], image['device_minor'], image['inode'])]
        if not mapped or image['mapping_rows'] != mapped or not any(x['path'] == module['pe'] for x in mapped):
            errors.append('Module is not authenticated by retained raw mappings: ' + str(process_key) + ':' + module['name'])
        bound = by_hash.get(image['dll_sha256'])
        if not bound or bound['pdbSha256'].lower() != image['pdb_sha256'] or bound['mvid'] != module['loadedMvid']:
            errors.append('Module lacks matching PE/PDB/MVID source binding: ' + str(process_key) + ':' + module['name'])
        rows.append({'pid': image['pid'], 'start_ticks': image['start_ticks'], 'phase': proof['phase'], 'module': module['name'], 'mvid': module['loadedMvid'], 'dll_sha256': image['dll_sha256'], 'pdb_sha256': image['pdb_sha256'], 'device_major': image['device_major'], 'device_minor': image['device_minor'], 'inode': image['inode'], 'capture': str(path.relative_to(packet))})
if len(used) != len(captures) or len(rows) != 120:
    errors.append('Unmatched captures or missing module joins; require exactly 120.')
result = {'passed': not errors, 'worker_count': len(proofs), 'capture_count': len(captures), 'required_module_joins': len(rows), 'rows': rows, 'errors': errors}
(packet / 'worker-capture-audit.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: v for k, v in result.items() if k != 'rows'}))
raise SystemExit(0 if result['passed'] else 1)
