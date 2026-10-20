"""Seal only this new bounded follow-up subtree, including every raw attempt."""
import hashlib
import json
from pathlib import Path
import sys

BASE = Path(__file__).resolve().parent
EXCLUDED = {'SHA256SUMS', 'seal-verification.json'}


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(data)
    return value.hexdigest()


def main():
    index = BASE / 'SHA256SUMS'
    if len(sys.argv) != 2 or sys.argv[1] not in ('create', 'verify'):
        raise RuntimeError('expected create or verify')
    paths = sorted(path for path in BASE.rglob('*') if path.is_file()
                   and path.relative_to(BASE).as_posix() not in EXCLUDED)
    if any(path.is_symlink() for path in paths):
        raise RuntimeError('substituted evidence file')
    if sys.argv[1] == 'create':
        with index.open('x') as stream:
            for path in paths:
                stream.write(sha(path) + '  ' + path.relative_to(BASE).as_posix() + '\n')
    expected = {}
    for line in index.read_text().splitlines():
        value, name = line.split('  ', 1)
        if name in expected or name in EXCLUDED or Path(name).is_absolute() or '..' in Path(name).parts:
            raise RuntimeError('invalid seal entry')
        expected[name] = value
    actual = {path.relative_to(BASE).as_posix(): sha(path) for path in paths}
    changed = [name for name in sorted(actual.keys() & expected.keys()) if actual[name] != expected[name]]
    result = {'scope': 'new verifier-corrections-v1 subtree only; no qualification',
              'command': [sys.executable, str(Path(__file__).resolve()), sys.argv[1]],
              'qualified': False, 'p1r_usable': False, 'files': len(actual),
              'bytes': sum(path.stat().st_size for path in paths), 'excluded': sorted(EXCLUDED),
              'index_sha256': sha(index), 'changed': changed,
              'added': sorted(actual.keys() - expected.keys()),
              'removed': sorted(expected.keys() - actual.keys()), 'passed': actual == expected}
    (BASE / 'seal-verification.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
