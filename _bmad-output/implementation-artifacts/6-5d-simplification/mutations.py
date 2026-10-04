#!/usr/bin/env python3
"""Six bounded corruptions of disposable verifier copies, with a clean control run.

These are specification checks, not a per-guard sweep or provider qualification.
"""
import json
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
MUTATIONS = {
    'framing': ("b'\\x00\\x01'+len(fields).to_bytes", "b'\\x00\\x02'+len(fields).to_bytes", "AssertionError: D06-activation"),
    'bound': ("'execution': 768*1024", "'execution': 1", "AssertionError: normative-cap:control/execution"),
    'legacy ordinal transition': ("else:need(ordinal==legacy['ordinal'],'legacy-ordinal')", 'else:pass', "AssertionError: ('legacy-generation-regression', 'legacy-ordinal')"),
    'queue allocator invariant': ("need(0<integer(r['ticket']) and (r['ticket']-1)%8==d['shard'],'ticket')", "need(0<integer(r['ticket']),'ticket')", "AssertionError: expected owning refusal: ticket"),
    'published model result': ("('Completed' if classification=='success' else 'Rejected'", "('Rejected' if classification=='success' else 'Rejected'", "AssertionError: status-precedence"),
}


def run(path):
    return subprocess.run(['python3', str(path)], capture_output=True, text=True)


def rejected(result, label, expected):
    assert result.returncode != 0, f'{label}: corrupted copy passed'
    assert 'SyntaxError' not in result.stderr, f'{label}: invalid mutation syntax'
    observed=result.stderr.strip().splitlines()[-1]
    assert observed==expected, (label,'wrong owning failure',observed,expected)
    return {'case': label, 'exitCode': result.returncode,
            'owningFailure': observed, 'expectedOwningFailure': expected}


def main():
    source = (HERE / 'verify.py').read_text()
    answers = (HERE / 'known-answers.json').read_bytes()
    outcomes = []
    with tempfile.TemporaryDirectory(prefix='6-5d-recovery-mutations-') as directory:
        target = Path(directory)
        (target / 'obligations.md').write_bytes((HERE / 'obligations.md').read_bytes())
        (target / 'known-answers.json').write_bytes(answers)
        verifier = target / 'verify.py'
        verifier.write_text(source)
        control = run(verifier)
        assert control.returncode == 0, control.stdout + control.stderr
        for label, (old, new, expected) in MUTATIONS.items():
            assert source.count(old) == 1, ('mutation anchor changed', label)
            verifier.write_text(source.replace(old, new, 1))
            outcomes.append(rejected(run(verifier), label, expected))
        verifier.write_text(source)
        corrupted = json.loads(answers)
        corrupted['records']['D45-window']['sha256'] = '00' * 32
        (target / 'known-answers.json').write_text(json.dumps(corrupted))
        outcomes.append(rejected(run(verifier), 'known answer', 'AssertionError: D45-window'))
    print(json.dumps({'controlResult': control.stdout.strip(),
                      'rejectedCorruptions': len(outcomes), 'cases': outcomes}, indent=2))


if __name__ == '__main__':
    main()
