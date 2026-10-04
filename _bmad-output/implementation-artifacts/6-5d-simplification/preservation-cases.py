#!/usr/bin/env python3
"""Exercise preservation refusals with simulated Git replies; mutate no history."""
import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

HERE = Path(__file__).resolve().parent


def main():
    spec = importlib.util.spec_from_file_location('correction_acceptance', HERE / 'acceptance.py')
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)
    results = []
    with patch.object(gate.subprocess, 'run', return_value=SimpleNamespace(
            returncode=1, stderr='simulated non-ancestor')):
        try:
            gate.main()
            raise RuntimeError('non-ancestor accepted')
        except AssertionError as error:
            assert error.args[0][0] == 'correction baseline is not an ancestor', error
            results.append({'case': 'non-ancestor', 'owningFailure': error.args[0][0]})
    real_git = gate.git
    for lane in ('committed', 'worktree', 'untracked'):
        def scenario(*args):
            if lane == 'committed' and args[0] == 'log':
                return b'src/unauthorized.cs\n'
            if lane == 'worktree' and args[0] == 'diff':
                return b'src/unauthorized.cs\0'
            if lane == 'untracked' and args[0] == 'ls-files':
                return b'src/unauthorized.cs\0'
            return real_git(*args)
        with patch.object(gate, 'git', side_effect=scenario):
            try:
                gate.main()
                raise RuntimeError(lane + ' forbidden path accepted')
            except AssertionError as error:
                assert error.args[0] == ('changes outside correction scope', ['src/unauthorized.cs']), error
                results.append({'case': lane, 'owningFailure': error.args[0][0]})
    print(json.dumps({'result': 'passed', 'gitReplies': 'simulated; no commit or workspace mutation',
                      'cases': results}, indent=2))


if __name__ == '__main__':
    main()
