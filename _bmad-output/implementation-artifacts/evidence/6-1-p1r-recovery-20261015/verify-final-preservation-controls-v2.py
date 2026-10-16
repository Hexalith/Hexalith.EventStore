"""Exercise the real preservation verdict with unchanged/changed inert indexes."""
from contextlib import redirect_stdout
import gzip
import hashlib
import io
import json
from pathlib import Path
import runpy
import subprocess
import tempfile
from unittest import mock

HERE = Path(__file__).resolve().parent
VERIFIER = HERE/'verify-final-preservation.py'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def save(path, value):
    path.write_text(json.dumps(value, indent=2)+'\n')


def index_case(changed):
    with tempfile.TemporaryDirectory(prefix='p1r-preservation-index-') as temporary:
        root = Path(temporary)/'workspace/references/Hexalith.EventStore'
        out = root/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-recovery-20261015'
        out.mkdir(parents=True)
        index = root/'.fixture-index'
        index.write_bytes(b'original inert index bytes\n')
        original_index_sha = sha(index.read_bytes())
        protected_file = root/'protected.txt'
        protected_file.write_bytes(b'unchanged inert source\n')
        snapshot = {'bytes': protected_file.stat().st_size, 'sha256': sha(protected_file.read_bytes()),
                    'mode': protected_file.stat().st_mode & 0o777}
        raw = json.dumps([{'repository': str(root), 'path': protected_file.name,
                           'starting': snapshot}]).encode()
        zipped = gzip.compress(raw, mtime=0)
        (out/'protected-files-before.json.gz').write_bytes(zipped)
        save(out/'protected-files-encoding.json', {'path': 'protected-files-before.json.gz',
             'compressed_sha256': sha(zipped), 'uncompressed_sha256': sha(raw), 'uncompressed_bytes': len(raw)})
        save(out/'authorized-workflow-changes-v2.json', {'files': []})
        save(out/'recovery-inventory.json', {'files': [{'path': protected_file.name, **snapshot}]})
        save(out/'fresh-source-binding.json', {'files': [{'path': protected_file.name, **snapshot}]})
        save(out/'initial-source-state.json', {'repositories': [{'path': str(root), 'head': 'fixture-head',
             'branch': 'main', 'gitlinks': [], 'index_sha256': original_index_sha}]})
        if changed:
            index.write_bytes(b'refreshed inert index bytes\n')
        copy = out/VERIFIER.name
        copy.write_bytes(VERIFIER.read_bytes())

        def read_git(argv, cwd=None, **kwargs):
            replies = {('git', 'rev-parse', 'HEAD'): 'fixture-head\n',
                       ('git', 'branch', '--show-current'): 'main\n',
                       ('git', 'ls-tree', '-r', 'HEAD'): '',
                       ('git', 'rev-parse', '--git-path', 'index'): '.fixture-index\n'}
            assert tuple(argv) in replies, argv
            return replies[tuple(argv)]

        def quiet_git(argv, cwd=None, **kwargs):
            assert argv in [['git','diff','--check'], ['git','diff','--cached','--quiet'],
                            ['git','diff','--name-only','--','src/']], argv
            return subprocess.CompletedProcess(argv, 0, '', '')

        output = io.StringIO()
        with mock.patch.object(subprocess, 'check_output', side_effect=read_git), \
             mock.patch.object(subprocess, 'run', side_effect=quiet_git), redirect_stdout(output):
            try:
                runpy.run_path(str(copy), run_name='__main__')
            except SystemExit as result:
                exit_code = result.code
        result = json.loads((out/'final-preservation-v2.json').read_text())
        assert result['passed'] == (not changed)
        assert exit_code == (1 if changed else 0)
        assert not result['protected_mismatches'] and not result['recovery_mismatches']
        assert not result['changed_check_inputs']
        repo = result['repositories'][0]
        assert all(repo[key] for key in ['head_unchanged', 'branch_unchanged', 'gitlinks_unchanged'])
        assert repo['index_bytes_unchanged'] == (not changed)
        assert all(command['exit_code'] == 0 and not command['stdout'] for command in repo['commands'])
        assert protected_file.read_bytes() == b'unchanged inert source\n'
        return {'case': 'changed-index' if changed else 'unchanged-index', 'passed': True,
                'exit_code': exit_code, 'index_bytes_unchanged': repo['index_bytes_unchanged'],
                'preservation_verdict': result['passed'], 'all_other_checks_passed': True,
                'result_sha256': sha((out/'final-preservation-v2.json').read_bytes()),
                'scope': 'inert temporary index; Git reads mocked; no actual repository mutation'}


if __name__ == '__main__':
    result = {'verifier': VERIFIER.name, 'verifier_sha256': sha(VERIFIER.read_bytes()),
              'checks': [index_case(False), index_case(True)]}
    result['passed'] = all(check['passed'] for check in result['checks'])
    with (HERE/'final-preservation-index-controls-v2.json').open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    print(json.dumps(result, indent=2))
