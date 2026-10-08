from pathlib import Path
from types import SimpleNamespace
import json
from importlib.metadata import version
import subprocess
import tempfile

from bmad_loop.sweep import SweepEngine

class SubjectCaptured(Exception):
    pass

root = Path('/home/administrator/projects/hexalith/eventstore')
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
subject = 'build(sweep): migrate legacy deferred-work entries to DW format'
with tempfile.TemporaryDirectory(prefix='bmad-migration-subject-') as directory:
    ledger = Path(directory) / 'deferred-work.md'
    rewrite = '# Deferred Work\n'
    ledger.write_text(rewrite, encoding='utf-8')
    observed = []

    def capture(message, **kwargs):
        observed.append(message)
        assert message == subject
        assert kwargs['path'] == ledger
        assert kwargs['family'] == 'ledger'
        assert kwargs['accepted_text'] == rewrite
        assert kwargs['accepted_baseline_commit'] == head
        # Stop before any real publication or Git mutation.
        raise SubjectCaptured

    engine = SimpleNamespace(
        workspace=SimpleNamespace(paths=SimpleNamespace(deferred_work=ledger)),
        state=SimpleNamespace(sweep_ledger_in_doubt=True),
        _commit_ledger=capture,
    )
    task = SimpleNamespace(migration_ledger_doubt_owned=False, baseline_commit=head)
    try:
        SweepEngine._finish_migration_commit(engine, task, '# Deferred Work\n', [], rewrite)
    except SubjectCaptured:
        pass
    else:
        raise AssertionError('Migration did not reach the publication boundary')
    assert observed == [subject]

result = {
    'check': 'installed SweepEngine._finish_migration_commit publication subject',
    'installed_package_version': version('bmad-loop'),
    'repository_head': head,
    'subject': subject,
    'result': 'passed',
    'git_mutations': 0,
    'boundary': 'captured _commit_ledger call; stopped before Git publication',
}
evidence = root / '_bmad-output/implementation-artifacts/evidence/bmad-loop-commitlint-compatibility'
(evidence / 'migration-subject-check.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
