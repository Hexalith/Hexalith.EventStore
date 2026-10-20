"""Create new domain-only controls from retained V4 bytes; never modify V4."""
import base64
import copy
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'tools'))
import p1r_published_executor as executor
from p1r_qualification import canonical, digest, require, write_json

BASE = Path(__file__).resolve().parent
COMMANDS = ROOT / '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/final-attempt/execution-e262ce52e42b4c64a436ac3841c17ac9/commands.json'


def main():
    commands = {row['id']: row for row in json.loads(COMMANDS.read_text())}
    require(digest(commands[4009]['output'].encode()) == commands[4009]['output_sha256'], 'changed V4 raw observation')
    original = {key: executor.postgres_value(value) for key, value in json.loads(commands[4009]['output'])
                if ('tenant-a' in key or 'tenant-b' in key) and (key.endswith((':metadata', ':snapshot')) or ':events:' in key)}
    metadata_key = next(key for key in original if 'tenant-a' in key and key.endswith(':metadata'))
    original_metadata = json.loads(original[metadata_key])
    require(original_metadata['sourceProof']['sourceIncarnation'] == '01M58NY2DGPGC9N7GKDW8ZEREM'
            and original_metadata['sourceProof']['headSequence'] == 12
            and original_metadata['sourceProof']['retainedFloor'] == 1, 'wrong original V4 state')
    controls = []
    for name, variant in (('positive-retained-v4-bytes', None), ('original-mismatched-proof-refusal', 'mismatched-proof')):
        state = dict(original)
        worker = executor.Executor.__new__(executor.Executor)
        worker.active, worker.redis = [], 'new-in-memory-disposable-fixture'
        worker.raw_state = lambda *_: dict(state)
        worker.postgres_write = lambda _, key, value: state.__setitem__(key, canonical(value))
        worker.postgres_delete = lambda _, key: state.pop(key)
        worker.mutate('retained', variant)
        metadata = json.loads(state[metadata_key])
        expected = copy.deepcopy(original_metadata)
        expected['retainedFloor'] = 5
        if variant is None:
            expected['sourceProof']['retainedFloor'] = 5
            executor.retained_metadata(state, 5)
        if variant is not None:
            require(digest(state[metadata_key]) == 'c03b45fefe2269d240c01e9b2aedf6763b1a63565a3f75b30037e6332f3e0538', 'original V4 mismatch bytes changed')
        require(metadata == expected, 'changed V4 source identity, incarnation, head or digest')
        require(all(data == original[key] for key, data in state.items() if key != metadata_key), 'changed retained domain bytes')
        payload = {'scope': 'new disposable domain-only verifier fixture from retained V4; not a database backup or qualification',
                   'qualified': False, 'p1r_usable': False, 'variant': name,
                   'input': {'commands_path': str(COMMANDS.relative_to(ROOT)), 'commands_sha256': digest(COMMANDS.read_bytes()),
                             'command_id': 4009, 'output_sha256': commands[4009]['output_sha256']},
                   'metadata': metadata, 'state': {key: {'base64': base64.b64encode(data).decode(), 'sha256': digest(data)}
                                                  for key, data in sorted(state.items())},
                   'checks': {'metadata_floor_five': metadata['retainedFloor'] == 5,
                              'proof_floor': metadata['sourceProof']['retainedFloor'],
                              'identity_incarnation_head_digest_unchanged': True,
                              'retained_event_snapshot_tenant_b_bytes_unchanged': True}}
        write_json(BASE / (name + '.json'), payload)
        controls.append({'name': name, 'sha256': digest((BASE / (name + '.json')).read_bytes()), 'proof_floor': metadata['sourceProof']['retainedFloor']})
    write_json(BASE / 'v4-derived-controls.json', {'controls': controls, 'historical_commands_unchanged': True, 'qualified': False, 'p1r_usable': False})
    print(json.dumps(controls))


if __name__ == '__main__':
    main()
