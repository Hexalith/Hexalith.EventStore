#!/usr/bin/env python3
"""Complete only the remaining bounded process controls in a fresh output."""
import argparse
import json
from pathlib import Path
import sys
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('verifier-root', 'source-root', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    (args.out / 'receipts').mkdir()
    sys.path.insert(0, str(args.verifier_root / 'tools'))
    import p1r_qualification as p
    import p1r_qualification_runtime as runtime
    result = {'started_utc': p.stamp(), 'finished_utc': None, 'invocation': uuid.uuid4().hex,
              'scope': 'bounded process controls only; no runtime qualification or acceptance',
              'qualified': False, 'p1r_usable': False, 'errors': [],
              'process_controls': runtime.new_process_section()}
    script = Path(__file__).resolve()
    result['script'] = {'path': str(script), 'sha256': p.digest(p.regular(script))}

    def save():
        p.write_json(args.out / 'controls.json', result)

    save()
    try:
        binding = p.source_binding(args.verifier_root, args.source_root)
        p.write_json(args.out / 'source-binding.json', binding)
        runtime.run_process_controls(result['process_controls'], args.out, result['invocation'],
                                     binding['python']['path'], lambda: p.check_current_source(binding), save)
        retained, count = runtime.validate_process_controls(
            args.out, result['process_controls'], result['invocation'], binding['python']['path'],
            *p.validate_times({'started_utc': result['started_utc'], 'finished_utc': p.stamp()}))
        p.require(all(row['execution'] == 'passed' for row in result['process_controls']['rows']),
                  'bounded process control failed')
        result['assertions'] = count
        result['retained_files'] = sorted(retained)
        p.check_current_source(binding)
        p.require(p.digest(p.regular(script)) == result['script']['sha256'], 'control driver changed')
        result['passed'] = True
    except (Exception, KeyboardInterrupt) as error:
        result['errors'].append({'type': type(error).__name__, 'message': str(error)})
        result['passed'] = False
    finally:
        result['finished_utc'] = p.stamp()
        save()
    print(json.dumps({key: result[key] for key in ('passed', 'errors', 'qualified', 'p1r_usable')}))
    return 0 if result['passed'] else 2


if __name__ == '__main__':
    raise SystemExit(main())
