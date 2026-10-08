"""Execute installed confirmation and capture its Git subject without committing."""
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import json
import subprocess
import tempfile

from bmad_loop import cli, verify

root = Path('/home/administrator/projects/hexalith/eventstore')
evidence = root / '_bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility'
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
with tempfile.TemporaryDirectory(prefix='bmad-operator-subject-') as directory:
    repo = Path(directory)
    spec = repo / 'spec.md'
    board = repo / 'sprint-status.yaml'
    record = repo / 'operator.json'
    for operand in (spec, board, record):
        operand.write_text('temporary fixture\n', encoding='utf-8')
    paths = SimpleNamespace(repo_root=repo, sprint_status=board)
    story = SimpleNamespace(story_key='1-1-a')
    calls = []

    def git_stub(repository, *arguments):
        assert repository == repo
        assert arguments[0] in ('add', 'commit')
        calls.append(arguments)
        return 0, ''

    def status_stub(repository, *arguments, **kwargs):
        assert repository == repo
        assert arguments[:2] == ('status', '--porcelain')
        return 0, ' M spec.md\n', ''

    with patch.object(cli.sprintstatus, 'advance', return_value='done'), patch.object(cli.operatoractions, 'record_path', return_value=record), patch.object(cli.operatoractions, 'drop'), patch.object(verify, 'path_ignored', return_value=False), patch.object(verify, '_git', git_stub), patch.object(verify, '_git_out', status_stub), patch.object(verify, '_git_raw', side_effect=AssertionError('Unexpected Git call')), patch.object(verify, 'rev_parse_head', return_value=head):
        assert cli._land_confirmation(repo, paths, story, spec, '2026-10-08') == 0
    commits = [arguments for arguments in calls if arguments[0] == 'commit']
    assert len(commits) == 1
    actual = commits[0][2]
    candidate = repo / 'message.txt'
    candidate.write_text(actual + '\n', encoding='utf-8')
    command = ['npx', '--no', '--', 'commitlint', '--edit', str(candidate), '--verbose']
    lint = subprocess.run(command, cwd=root, capture_output=True, text=True)
    assert lint.returncode == 0, lint.stdout + lint.stderr
    assert actual == 'build(operator): confirm 1-1-a', actual
    assert '--no-verify' not in commits[0]

result = {
    'check': 'installed cli._land_confirmation generates the compatible subject',
    'boundary': 'real confirmation and commit_paths; board/record effects and all Git operations stubbed',
    'repository_head': head,
    'real_git_mutations': 0,
    'captured_commit_arguments': list(commits[0]),
    'observed_subject': actual,
    'commitlint_command': command,
    'commitlint_exit_code': lint.returncode,
    'commitlint_output': lint.stdout + lint.stderr,
    'result': 'passed',
}
(evidence / 'operator-subject-check.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('Passed installed operator subject execution and pinned commitlint validation; no real Git mutations.')
