"""Check installed subject propagation with every Git operation stubbed."""
from pathlib import Path
from unittest.mock import patch
import json
import subprocess
import tempfile

from bmad_loop import verify

root = Path('/home/administrator/projects/hexalith/eventstore')
evidence = root / '_bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility'
validation = json.loads((evidence / 'commitlint-validation.json').read_text())
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
checks = []
with tempfile.TemporaryDirectory(prefix='bmad-publication-subjects-') as directory:
    repo = Path(directory)
    operand = repo / 'deferred-work.md'
    operand.write_text('# Deferred Work\n', encoding='utf-8')
    for candidate in validation['candidates']:
        assert candidate['exit_code'] == 0
        subject = candidate['message']
        calls = []

        def git_stub(repository, *arguments):
            assert repository == repo
            calls.append(arguments)
            assert arguments in (
                ('add', '--', ':(literal)deferred-work.md'),
                ('commit', '-m', subject, '--', ':(literal)deferred-work.md'),
            ), arguments
            return 0, ''

        def status_stub(repository, *arguments, **kwargs):
            assert repository == repo
            assert arguments == ('status', '--porcelain', '--', ':(literal)deferred-work.md')
            return 0, ' M deferred-work.md\n', ''

        with patch.object(verify, '_git', git_stub), patch.object(verify, '_git_out', status_stub), patch.object(verify, '_git_raw', side_effect=AssertionError('Unexpected Git call')), patch.object(verify, 'rev_parse_head', return_value=head):
            result = verify.commit_paths(repo, subject, [operand])
        assert result == head
        assert calls == [
            ('add', '--', ':(literal)deferred-work.md'),
            ('commit', '-m', subject, '--', ':(literal)deferred-work.md'),
        ]
        checks.append({'subject': subject, 'captured_commit_arguments': list(calls[-1]), 'result': 'passed'})

result = {
    'check': 'installed verify.commit_paths forwards all validated complete subjects',
    'boundary': 'all Git operations stubbed; hooks are not executed by this check',
    'real_git_mutations': 0,
    'subjects_checked': len(checks),
    'checks': checks,
}
(evidence / 'publication-subjects-check.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(f"Passed subject propagation for {len(checks)} candidates; no real Git mutations.")
