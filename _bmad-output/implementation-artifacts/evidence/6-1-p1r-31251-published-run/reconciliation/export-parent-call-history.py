"""Archive only this root thread's literal shell/edit tool calls and outputs."""
import datetime
import hashlib
import json
from pathlib import Path
import uuid

base = Path(__file__).resolve().parent
session = Path('/home/administrator/.codex/sessions/2026/10/18/rollout-2026-10-18T18-18-43-01a14fce-eaf3-7902-959b-2fa847834a3a.jsonl')
known = set()
for index in base.glob('parent-call-history-*/index.json'):
    known.update(row['call_id'] for row in json.loads(index.read_bytes())['calls'])
sha = lambda data: hashlib.sha256(data).hexdigest()
out = base / ('parent-call-history-' + uuid.uuid4().hex)
out.mkdir()
calls = {}
prefix = hashlib.sha256()
size = 0
last_line = 0
with session.open('rb') as stream:
    for number, raw in enumerate(stream, 1):
        last_line = number
        prefix.update(raw)
        size += len(raw)
        record = json.loads(raw)
        payload = record.get('payload', {})
        text = payload.get('input')
        if (record.get('timestamp', '') < '2026-10-18T21:39:00' or payload.get('type') != 'custom_tool_call'
                or payload.get('name') != 'exec' or not isinstance(text, str)
                or not any(marker in text for marker in ('tools.exec_command(', 'tools.apply_patch('))):
            continue
        identity = payload['call_id']
        if identity in known:
            continue
        path = out / (identity + '.exec.js')
        path.write_text(text)
        calls[identity] = {'call_id': identity, 'timestamp': record['timestamp'], 'session': str(session),
                           'line': number, 'input': path.name, 'input_sha256': sha(path.read_bytes()), 'outputs': []}
with session.open('rb') as stream:
    for number, raw in enumerate(stream, 1):
        if number > last_line:
            break
        record = json.loads(raw)
        payload = record.get('payload', {})
        identity = payload.get('call_id')
        if identity not in calls or payload.get('type') != 'custom_tool_call_output':
            continue
        path = out / (identity + '.output-' + str(number) + '.json')
        path.write_text(json.dumps(payload, indent=2) + '\n')
        calls[identity]['outputs'].append({'path': path.name, 'sha256': sha(path.read_bytes()),
                                           'line': number, 'timestamp': record.get('timestamp')})
result = {'scope': 'literal parent shell/edit tool calls and outputs from this qualification thread only; no user messages or analysis exported; no re-execution',
          'observed_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'export_script_sha256': sha(Path(__file__).read_bytes()), 'session': str(session),
          'session_prefix_bytes': size, 'session_prefix_lines': last_line, 'session_prefix_sha256': prefix.hexdigest(),
          'pending_output_scope': 'calls with empty outputs were still pending at this snapshot; later archives retain later completed calls',
          'calls': list(calls.values())}
(out / 'index.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'out': str(out), 'calls': len(calls),
                  'outputs': sum(len(row['outputs']) for row in calls.values())}))
