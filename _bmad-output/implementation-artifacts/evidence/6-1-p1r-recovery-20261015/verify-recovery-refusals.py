"""Exercise actual recovery refusal paths in a temporary workspace, with Git reads mocked."""
import ast
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
from unittest import mock

HERE = Path(__file__).resolve().parent
HELPER = HERE / 'recover-reviewed-closure.py'
RECOVERY = 'b7a1917cec2a59ffe80dc3ee977cf3aa31376b14'
PARENT = '210b90da172cc7ca946fbb06cfd44390bffa707b'
BASE = '5b0df296abc86adf8d37a92fae02e26574d19ad5'
PREFIX = '_bmad-output/implementation-artifacts/evidence/'
SUCCESSOR = PREFIX + '6-1-p1r-successor-enrollment-20261014/'
REAL_MANIFEST = json.loads((HERE.parent/'6-1-p1r-successor-enrollment-20261014/reviewed-source.json').read_text())
REAL_HELPERS = json.loads((HERE.parent/'6-1-p1r-successor-enrollment-20261014/required-ci-test-dependencies.json').read_text())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, indent=2)+'\n').encode()


def fixture_contents():
    # Keep the actual 23 path identities and four helper dependencies. Replace only
    # payloads with explicit inert fixtures so no historical blob can be modified.
    blobs = {entry['path']: ('inert fixture for '+entry['path']+'\n').encode()
             for entry in REAL_MANIFEST['files']}
    reviewed = [{'path': path, 'bytes': len(data), 'sha256': sha(data)}
                for path, data in blobs.items()]
    blobs[SUCCESSOR+'reviewed-source.json'] = encode({'files': reviewed})
    helper_paths = {entry['path'] for entry in REAL_HELPERS['files']}
    blobs[SUCCESSOR+'required-ci-test-dependencies.json'] = encode(
        {'files': [entry for entry in reviewed if entry['path'] in helper_paths]})
    for index in range(115):
        blobs[SUCCESSOR+f'fixture-{index:03}.txt'] = b'inert delivery fixture\n'
    delivery = [{'path': path.removeprefix(SUCCESSOR), 'bytes': len(data), 'sha256': sha(data)}
                for path, data in blobs.items() if path.startswith(SUCCESSOR)]
    assert len(delivery) == 117
    blobs[SUCCESSOR+'delivery-manifest.json'] = encode({'files': delivery})
    return blobs


def workspace_hashes(root):
    return {str(path.relative_to(root)): sha(path.read_bytes()) for path in root.rglob('*') if path.is_file()}


def exercise(case):
    with tempfile.TemporaryDirectory(prefix='p1r-recovery-refusal-') as temporary:
        workspace = Path(temporary)
        root = workspace/'references/Hexalith.EventStore'
        root.mkdir(parents=True)
        out = root/PREFIX/'6-1-p1r-recovery-20261015'
        blobs = fixture_contents()
        oid_data = {hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(): data
                    for data in blobs.values()}
        tree = b''.join(b'100644 blob '+hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest().encode()
                        +b' '+str(len(data)).encode()+b'\t'+path.encode()+b'\0'
                        for path, data in sorted(blobs.items()))
        for repo in [workspace, root]:
            (repo/'.fixture-index').write_bytes(b'unchanged fixture index\n')
            (repo/'protected.txt').write_bytes(b'preserve this unrelated file\n')
        watched = root/('tools/p1r-published-executor.py' if case == 'concurrent-destination-change'
                        else SUCCESSOR+'fixture-000.txt')
        watched.parent.mkdir(parents=True, exist_ok=True)
        watched.write_bytes(b'initial destination bytes\n')
        for path, data in blobs.items():
            if path.startswith(PREFIX+'6-1-p1r-31210-published-run/'):
                existing = root/path
                existing.parent.mkdir(parents=True, exist_ok=True)
                existing.write_bytes(data)
        before = workspace_hashes(workspace)
        concurrent_bytes = b'concurrent human edit must survive\n'
        watched_reads = 0
        read_bytes = Path.read_bytes

        def observed_read(path):
            nonlocal watched_reads
            if path == watched:
                watched_reads += 1
                if case == 'concurrent-destination-change' and watched_reads == 2:
                    path.write_bytes(concurrent_bytes)
            return read_bytes(path)

        def git_reads(argv, cwd=None, **kwargs):
            assert argv[0] == 'git', 'the recovery helper may only invoke read-only Git here'
            arguments = argv[1:]
            if arguments == ['rev-parse', 'HEAD']:
                return (BASE+'\n').encode()
            if arguments == ['rev-parse', RECOVERY+'^']:
                return (PARENT+'\n').encode()
            if arguments == ['rev-parse', 'fix/p1r-successor-enrollment']:
                return (RECOVERY+'\n').encode()
            if arguments[:1] == ['show']:
                return blobs[arguments[1].split(':', 1)[1]]
            if arguments == ['ls-tree', '-rlz', RECOVERY]:
                return tree
            if arguments[:2] == ['cat-file', 'blob']:
                return oid_data[arguments[2]]
            if arguments == ['ls-files', '-z']:
                return b'protected.txt\0'
            if arguments == ['branch', '--show-current']:
                return b'main\n'
            if arguments == ['status', '--short']:
                return b''
            if arguments == ['remote', '-v']:
                return b'origin fixture://read-only (fetch)\n'
            if arguments == ['log', '-5', '--oneline']:
                return b'fixture inert history\n'
            if arguments == ['rev-parse', '--git-path', 'index']:
                return b'.fixture-index\n'
            if arguments == ['ls-tree', '-r', 'HEAD']:
                return b''
            raise AssertionError('unexpected Git command: '+repr(arguments))

        syntax = ast.parse(HELPER.read_text(), filename=str(HELPER))
        for statement in syntax.body:
            if isinstance(statement, ast.Assign) and any(isinstance(target, ast.Name) and target.id == 'ROOT'
                                                         for target in statement.targets):
                statement.value = ast.Call(func=ast.Name(id='Path', ctx=ast.Load()),
                                           args=[ast.Constant(value=str(root))], keywords=[])
        ast.fix_missing_locations(syntax)
        refusal = None
        with mock.patch.object(subprocess, 'check_output', side_effect=git_reads), \
             mock.patch.object(Path, 'read_bytes', observed_read):
            try:
                exec(compile(syntax, str(HELPER), 'exec'), {'__file__': str(HELPER)})
            except AssertionError as error:
                refusal = str(error)
        after = workspace_hashes(workspace)
        expected = dict(before)
        if case == 'concurrent-destination-change':
            expected[str(watched.relative_to(workspace))] = sha(concurrent_bytes)
            assert watched_reads == 2
            assert refusal == 'concurrent destination change: tools/p1r-published-executor.py', refusal
        else:
            assert refusal == 'existing evidence conflicts: '+SUCCESSOR+'fixture-000.txt', refusal
        assert after == expected, 'refusal must leave every byte, including the concurrent edit, intact'
        assert not out.exists(), 'pre-write refusal must happen before the recovery receipt directory is created'
        return {'case': case, 'passed': True, 'refusal': refusal, 'destination_preserved': True,
                'all_other_fixture_bytes_preserved': True, 'recovery_outputs_created': False,
                'scope': 'isolated recovery safety fixture; no package qualification or real Git mutation'}


if __name__ == '__main__':
    result = {'recovery_helper': HELPER.name, 'recovery_helper_sha256': sha(HELPER.read_bytes()),
              'fixture_overrides': ['workspace root', 'read-only Git responses', 'one concurrent destination write'],
              'checks': [exercise('existing-evidence-conflict'), exercise('concurrent-destination-change')]}
    result['passed'] = all(check['passed'] for check in result['checks'])
    with (HERE/'recovery-refusal-controls.json').open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    print(json.dumps(result, indent=2))
