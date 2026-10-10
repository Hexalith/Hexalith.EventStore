"""Recheck the retained source response against the current bounded witness."""
import hashlib
import json
from pathlib import Path

import p1r_check_witnesses


artifact = Path(__file__).resolve().parent
repository = artifact.parents[3]
audit = json.loads((artifact / 'diagnostic-type-reaudit.json').read_text())
binding = json.loads((artifact / 'executor-source.json').read_text())
sources = json.loads((artifact / 'predicate-sources.json').read_text())


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


assert sha256(artifact / audit['retained_case']) == audit['retained_case_sha256']
assert sha256(artifact / 'executor-source.json') == audit['historical_executor_binding_sha256']
for kind in ('executor', 'witness'):
    source = audit['current_check_source']
    assert sha256(repository / source[kind + '_path']) == source[kind + '_sha256']

for path in sorted((artifact / 'source-cases').glob('*.json')):
    case = json.loads(path.read_text())
    p1r_check_witnesses.validate_case(case['case'], case['evidence'], binding, case['lane'], sources)

retained = json.loads((artifact / audit['retained_case']).read_text())
commands = [command for command in retained['evidence']['commands']
            if command['id'] == audit['retained_http_command_id']]
assert len(commands) == 1 and commands[0]['exit_code'] == 0
operation = json.loads(commands[0]['output'])
for field, message in (
    ('stale_diagnostic', 'The idempotency execution authority is no longer current.'),
    ('forged_diagnostic', 'The idempotency execution fence is missing, stale, or invalid.'),
):
    matched = audit['matched_diagnostics'][field]
    assert matched in operation[field]
    assert matched['actual_exception_type'] == 'System.InvalidOperationException'
    assert matched['denial_message'] == message

print(json.dumps({'cases_revalidated': 4, 'typed_denials_matched': 2, 'result': 'pass'}, sort_keys=True))
