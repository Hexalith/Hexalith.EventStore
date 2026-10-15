#!/usr/bin/env python3
"""Run all approved lanes freshly after review fixes; retain literal serialized restore commands."""
import json
from pathlib import Path
import shutil
import sys

REPO = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(REPO / 'tools'))
import p1r_published_executor as executor

evidence = Path(__file__).resolve().parent.parent
plan = json.loads((evidence / 'review-round-1/fresh-execution-plan.json').read_bytes())
inputs, data, scope = executor.qualification.load_document(evidence / 'owner-inputs.json', executor.qualification.validate_inputs)
executor.require(scope == 'owner-selected', 'fresh execution requires approved owner inputs')
executor.validate_execution_inputs(inputs)

original_copytree = shutil.copytree
def fresh_copytree(source, destination, *args, **kwargs):
    if Path(source) == executor.CONSUMERS:
        kwargs['ignore'] = shutil.ignore_patterns('bin', 'obj', 'packages')
    return original_copytree(source, destination, *args, **kwargs)
shutil.copytree = fresh_copytree

class SerializedSourceExecutor(executor.Executor):
    def run(self, argv, *args, **kwargs):
        if [str(value) for value in argv[:2]] == ['dotnet', 'restore'] and 'consumers/source/' in str(argv[2]):
            argv = list(argv) + plan['source_restore_flags']
        return super().run(argv, *args, **kwargs)

worker = SerializedSourceExecutor(evidence / plan['run_name'], inputs)
worker.inputs_sha256 = executor.digest(data)
worker.result['inputs_sha256'] = worker.inputs_sha256
worker.result['review_fixed_execution'] = {
    'driver_sha256': executor.digest(Path(__file__).read_bytes()),
    'plan_sha256': executor.digest((evidence / 'review-round-1/fresh-execution-plan.json').read_bytes()),
    'previous_run': 'execution-f47a56499f0542c1b171e110aa48de2f',
    'private_copy_excludes': plan['fresh_copy_excludes'],
    'source_restore_flags': plan['source_restore_flags'],
}
worker.save()
print(json.dumps({'out': str(worker.output), 'started': True}), flush=True)
try:
    worker.execute()
except (Exception, KeyboardInterrupt) as error:
    worker.errors.append(type(error).__name__ + ': ' + str(error))
    try:
        worker.cleanup()
    except (Exception, KeyboardInterrupt) as cleanup_error:
        worker.errors.append('cleanup: ' + type(cleanup_error).__name__ + ': ' + str(cleanup_error))
        worker.save()
print(json.dumps({'out': str(worker.output), 'errors': worker.errors, 'p1r_usable': False}), flush=True)
raise SystemExit(2 if worker.errors else 0)
