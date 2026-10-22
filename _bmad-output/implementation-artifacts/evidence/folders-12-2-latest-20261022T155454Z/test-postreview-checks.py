"""Mutation controls for retained Unit maps and freshly enumerated input scopes."""
import importlib.util
import json
from pathlib import Path
import tempfile
from review_audit_checks import ordinary_paths, unit_mapping_valid
p = Path(__file__).resolve().parent
target = p / 'postreview-extra-controls.json'
if target.exists():
    raise SystemExit('Refusing to overwrite evidence')
spec = importlib.util.spec_from_file_location('reader', '/tmp/eventstore-folders-12-2-latest-20261022T155454Z/tools/bounded_worker_observer.py')
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)
identity = json.loads((p / 'selected-final-server-process-identity.json').read_text())
raw = (p / identity['maps']).read_text()
rows = reader.mapping_rows(raw)
checks = [{'control': 'Pristine retained Unit mapping', 'passed': unit_mapping_valid(identity, rows)},
          {'control': 'Empty raw Unit map rejected even with recomputed receipt hash', 'passed': not unit_mapping_valid(identity, [])}]
wrong = dict(identity, inode=identity['inode'] + 1)
checks.append({'control': 'Unit inode mismatch rejected', 'passed': not unit_mapping_valid(wrong, rows)})
with tempfile.TemporaryDirectory(prefix='folders-input-set-control-') as directory:
    root = Path(directory)
    (root / 'references').mkdir()
    (root / 'known.cs').write_text('class Known {}')
    expected = ordinary_paths(root)
    for name in ('extra.cs', 'extra.csproj', 'Directory.Build.props', 'config.json', 'components.yaml'):
        extra = root / name
        extra.write_text('controlled input')
        current = ordinary_paths(root)
        checks.append({'control': 'New ordinary input detected: ' + name, 'passed': current - expected == {str(extra)}})
        extra.unlink()
    # Ignore rules cannot hide newly introduced evaluated inputs.
    (root / '.gitignore').write_text('hidden.cs\n')
    baseline = ordinary_paths(root)
    (root / 'hidden.cs').write_text('class Hidden {}')
    checks.append({'control': 'Ignored source addition still detected', 'passed': ordinary_paths(root) - baseline == {str(root / 'hidden.cs')}})
result = {'passed': all(row['passed'] for row in checks), 'checks': checks, 'owned_temporary_directory_absent': not Path(directory).exists()}
with target.open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps(result))
raise SystemExit(0 if result['passed'] and result['owned_temporary_directory_absent'] else 1)
