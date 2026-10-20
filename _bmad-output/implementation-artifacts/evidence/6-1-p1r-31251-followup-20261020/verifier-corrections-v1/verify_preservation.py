"""Read-only comparison with task-entry hashes; writes only its new result file."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[5]
BASE = Path(__file__).resolve().parent
ENTRY = json.loads((BASE / 'entry.json').read_text())
ALLOWED = {'tools/p1r_published_executor.py', 'tools/p1r_check_witnesses.py',
           'tools/tests/test_p1r_published_executor.py', 'tools/tests/test_p1r_verifier_corrections.py'}
CONCURRENT = {'_bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/index.json',
              '_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md'}
HISTORY = '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/'
FOLLOWUP = '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-followup-20261020/'
WRAPPER = '_bmad-output/implementation-artifacts/spec-6-1-p1r-31251-published-qualification.md'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'))


def main():
    changes = []
    for name, before in ENTRY['tracked_files'].items():
        path = ROOT / name
        after = sha(path) if path.is_file() else None
        if after != before:
            changes.append({'path': name, 'before_sha256': before, 'after_sha256': after})
    expected_historical = {name for name in ENTRY['tracked_files'] if name.startswith(HISTORY)}
    actual_historical = {str(path.relative_to(ROOT)) for path in (ROOT / HISTORY).rglob('*') if path.is_file()}
    historical_changes = [row for row in changes if row['path'].startswith(HISTORY)]
    old_followup_changes = [row for row in changes if row['path'].startswith(FOLLOWUP)]
    unexpected = [row for row in changes if row['path'] not in ALLOWED | CONCURRENT]
    index = Path(git('rev-parse', '--git-path', 'index').decode().strip())
    if not index.is_absolute():
        index = ROOT / index
    checks = {'historical_v4_unchanged': not historical_changes and expected_historical == actual_historical,
              'original_followup_unchanged': not old_followup_changes,
              'original_frozen_wrapper_unchanged': sha(ROOT / WRAPPER) == ENTRY['tracked_files'][WRAPPER],
              'no_unexpected_tracked_changes': not unexpected,
              'head_unchanged': git('rev-parse', 'HEAD').decode().strip() == ENTRY['head'],
              'index_bytes_unchanged': sha(index) == ENTRY['index_sha256'],
              'staged_diff_empty': not git('diff', '--cached', '--no-ext-diff', '--binary'),
              'tag_source_unchanged': git('rev-parse', 'v3.125.1^{commit}').decode().strip() == '55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b',
              'production_source_unchanged': not any(row['path'].startswith('src/') for row in changes),
              'dependency_and_build_declarations_unchanged': not any(
                  row['path'] in ('.gitmodules', '.gitattributes', '.editorconfig', 'global.json', 'package.json', 'package-lock.json')
                  or Path(row['path']).name.startswith('Directory.') for row in changes)}
    observed = json.loads((BASE / 'concurrent-unrelated-observation.json').read_text())
    report = {'qualified': False, 'p1r_usable': False, 'checks': checks, 'entry_tracked_files': len(ENTRY['tracked_files']),
              'historical_files': len(expected_historical), 'historical_changes': historical_changes,
              'historical_added': sorted(actual_historical - expected_historical),
              'historical_removed': sorted(expected_historical - actual_historical),
              'original_followup_changes': old_followup_changes, 'tracked_changes': changes,
              'unrelated_concurrent_changes': [row for row in changes if row['path'] in CONCURRENT],
              'unexpected_changes': unexpected,
              'concurrent_files_current': [{'path': row['path'], 'observed_sha256': row['sha256'],
                  'current_sha256': sha(ROOT / row['path']) if (ROOT / row['path']).is_file() else None}
                  for row in observed['files']],
              'limit': 'Unrelated concurrent changes are recorded literally. This task never writes, restores, stages or commits them.'}
    (BASE / 'preservation.json').write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')
    print(json.dumps({'checks': checks, 'unexpected': unexpected, 'historical_files': len(expected_historical)}))
    return 0 if all(value for key, value in checks.items() if key != 'index_bytes_unchanged') else 1


if __name__ == '__main__':
    raise SystemExit(main())
