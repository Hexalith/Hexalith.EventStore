#!/usr/bin/env python3
"""Finish auditing existing receipts without changing their frozen validators."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('verifier-root', 'source-root', 'execution', 'inputs', 'decisions', 'previous-audit', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    sys.path.insert(0, str(args.verifier_root / 'tools'))
    import p1r_qualification as p
    import p1r_qualification_runtime as runtime
    import p1r_published_qualification as q
    import p1r_check_witnesses as witnesses
    result = {'schema': 'hexalith.p1r.remaining-audit.v1', 'started_utc': p.stamp(),
              'finished_utc': None, 'invocation': uuid.uuid4().hex, 'valid_evidence': False,
              'qualified': False, 'p1r_usable': False, 'errors': [], 'packages_reused': {},
              'receipts': [], 'case_witnesses': [], 'commands': [],
              'scope': 'Existing execution only; frozen validators; no acceptance or runtime reexecution'}
    script = Path(__file__).resolve()
    result['script'] = {'path': str(script), 'sha256': p.digest(p.regular(script))}

    def save():
        p.write_json(args.out / 'audit.json', result)

    def error(stage, exc, **fields):
        result['errors'].append({'stage': stage, 'type': type(exc).__name__, 'message': str(exc), **fields})
        save()

    def command(label, argv):
        row = {'label': label, 'argv': list(map(str, argv)), 'cwd': str(args.out), 'started_utc': p.stamp()}
        try:
            process = subprocess.run(row['argv'], cwd=args.out, capture_output=True, text=True, timeout=600)
            row.update(exit_code=process.returncode, stdout=process.stdout, stderr=process.stderr)
        except Exception as exc:
            row.update(exit_code=None, error={'type': type(exc).__name__, 'message': str(exc)})
        row['finished_utc'] = p.stamp()
        result['commands'].append(row)
        save()
        return row

    save()
    try:
        p.require(p.ROOT == args.verifier_root.resolve(), 'verifier root substituted')
        selection, raw, scope = q.load_document(args.inputs, q.validate_inputs)
        p.require(scope == 'owner-selected' and selection['candidate']['version'] == '3.125.1', 'selection differs')
        result['inputs_sha256'] = p.digest(raw)
        decisions, decision_bytes, _ = q.load_document(args.decisions, q.validate_decisions)
        p.require(decisions['inputs_sha256'] == p.digest(raw), 'decision inputs differ')
        result['decisions_sha256'] = p.digest(decision_bytes)
        binding = p.source_binding(args.verifier_root, args.source_root)
        q.validate_candidate_source(binding, selection)
        p.write_json(args.out / 'source-binding.json', binding)
        (args.out / 'global.json').write_bytes(p.regular(args.source_root / 'global.json'))
        execution = q.parse_json(p.regular(args.execution / 'execution.json'))
        source_bytes = p.regular(args.execution / 'executor-source.json')
        p.require(execution['finished_utc'] and execution['errors'] == [] and execution['inputs_sha256'] == p.digest(raw),
                  'execution incomplete, failed, or substituted')
        p.require(q.parse_json(source_bytes) == binding and p.digest(source_bytes) == execution['executor_source_sha256'],
                  'execution source binding differs')
        context = {'inputs': {'value': selection, 'scope': scope, 'sha256': p.digest(raw)},
                   'source_binding': binding, 'verified': {}}
        previous_bytes = p.regular(args.previous_audit / 'audit.json')
        previous = q.parse_json(previous_bytes)
        result['previous_audit'] = {'path': str(args.previous_audit), 'sha256': p.digest(previous_bytes)}
        for role in ['candidate', *('comparison-' + v for v in q.COMPARISON_VERSIONS)]:
            p.check_current_source(binding)
            row = previous['packages'][role]
            receipt, outcome, retained = q.validate_package_receipt(
                args.previous_audit, previous['invocation'], role, row['receipts'][0], context['inputs'],
                *q.validate_times(previous))
            verified = q.verified_identities(receipt, outcome)
            p.require(verified is not None, 'reused package proof is nonpassing: ' + role)
            context['verified'][role] = verified
            result['packages_reused'][role] = {'outcome': outcome,
                'receipt_path': str(args.previous_audit / row['receipts'][0]),
                'receipt_sha256': p.digest(p.regular(args.previous_audit / row['receipts'][0])),
                'retained_files': sorted(retained)}
            save()
        paths = execution['receipts']
        inventory = q.executed_case_inventory(selection)
        lane_paths = []
        measured_lanes = set()
        for name in paths:
            path = Path(name)
            p.require(path.parent == args.execution / 'receipts', 'receipt escaped original execution')
            data = p.regular(path)
            receipt = q.parse_json(data)
            lane = receipt.get('lane', receipt.get('role', receipt['schema']))
            row = {'path': str(path), 'sha256': p.digest(data), 'lane': lane,
                   'assertions': receipt['assertions'], 'validated': False}
            try:
                group, validated_lane, outcome = q.receipt_outcome(receipt, context)
                row.update(validated=True, group=group, lane=validated_lane, outcome=outcome)
            except Exception as exc:
                row['refusal'] = {'type': type(exc).__name__, 'message': str(exc)}
                error('receipt', exc, lane=lane, path=str(path))
            result['receipts'].append(row)
            if receipt.get('lane') in inventory:
                lane_paths.append(path)
                measured_lanes.add(receipt['lane'])
                q.lane_outcome(receipt)
                p.require([c['id'] for c in receipt['cases']] == list(inventory[receipt['lane']]), 'case inventory differs')
                retained = receipt['execution_evidence']
                for measured, evidence in zip(receipt['cases'], retained['cases']):
                    case = {'lane': receipt['lane'], 'id': measured['id'], 'validated': False,
                            'assertions': measured['assertions']}
                    try:
                        witnesses.validate_case(measured, evidence, binding, receipt['lane'],
                                                retained.get('predicate_sources'),
                                                candidate_version=selection['candidate']['version'])
                        case['validated'] = True
                    except Exception as exc:
                        case['refusal'] = {'type': type(exc).__name__, 'message': str(exc)}
                        error('case-witness', exc, lane=receipt['lane'], case=measured['id'])
                    result['case_witnesses'].append(case)
            save()
        p.require(measured_lanes == set(inventory), 'missing scenario/addition lane')
        result['case_count'] = len(result['case_witnesses'])
        p.require(result['case_count'] == sum(map(len, inventory.values())), 'case accounting incomplete')
        result['process_controls'] = runtime.new_process_section()
        try:
            runtime.run_process_controls(result['process_controls'], args.out, result['invocation'],
                                         binding['python']['path'], lambda: p.check_current_source(binding), save)
            retained, count = runtime.validate_process_controls(
                args.out, result['process_controls'], result['invocation'], binding['python']['path'],
                *q.validate_times({'started_utc': result['started_utc'], 'finished_utc': p.stamp()}))
            result['process_control_assertions'] = count
            result['process_control_files'] = sorted(retained)
        except Exception as exc:
            error('process-controls', exc)
        cli = args.verifier_root / 'tools/p1r-published-qualification.py'
        table = args.out / 'successor-support-boundary.json'
        command('measure-boundary', ['python3', cli, 'measure-boundary', '--inputs', args.inputs,
                                     '--out', table, *[item for path in lane_paths for item in ('--receipt', path)]])
        packet = args.out / 'strict-packet'
        argv = ['python3', cli, 'prepare', '--out', packet, '--source-root', args.source_root,
                '--inputs', args.inputs, '--decisions', args.decisions, '--support-boundary', table,
                '--candidate-evidence', args.execution / 'consumers/candidate', '--process-controls']
        for version in q.COMPARISON_VERSIONS:
            argv += ['--comparison-evidence', args.execution / ('consumers/comparison-' + version)]
        for path in paths:
            argv += ['--receipt', path]
        command('strict-prepare', argv)
        command('strict-validate', ['python3', cli, 'validate', packet])
        p.check_current_source(binding)
        p.require(p.digest(p.regular(script)) == result['script']['sha256'], 'supplementary audit changed')
        result['source_checked_after_all_commands'] = True
        result['audit_completed'] = True
    except (Exception, KeyboardInterrupt) as exc:
        error('audit', exc)
    finally:
        result['finished_utc'] = p.stamp()
        save()
    print(json.dumps({'audit_completed': result.get('audit_completed', False), 'errors': len(result['errors']),
                      'case_count': result.get('case_count'), 'valid_evidence': False,
                      'qualified': False, 'p1r_usable': False}))
    return 2


if __name__ == '__main__':
    raise SystemExit(main())
