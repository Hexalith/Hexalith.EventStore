#!/usr/bin/env python3
"""Rederive all ten repair lanes; final validity awaits the outer isolation audit."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid

EXPECTED_LANES = ('metadata-write','retained-covered','retained-uncovered','missing-event',
                  'invalid-evidence','checkout','post-upgrade-restore','failure-cleanup',
                  'reminder-recovery','logical-event-evolution')
SCRIPT_NAMES = {'verify-and-audit.py','verify-repair.py','independent-repair-audit.py'}


def require_inventory(q, runtime, run, execution, receipts):
    paths = [Path(value) for value in execution['receipts']]
    q.require(len(paths) == len(set(paths)) and set(paths) == set((run / 'receipts').glob('*.json'))
              and all(path.parent.parent == run and path == path.resolve() for path in paths),
              'registered and persisted receipt inventories differ')
    lanes = [value for value in receipts if value.get('schema') == q.LANE_RECEIPT_SCHEMA]
    cleanup = [value for value in receipts if value.get('schema') == runtime.CLEANUP_SCHEMA]
    restore = [value for value in receipts if value.get('schema') == runtime.RESTORE_SCHEMA]
    q.require(len(receipts) == 12 and len(lanes) == 10 and len(cleanup) == len(restore) == 1,
              'exact ten lanes plus cleanup and dependent restore required')
    q.require(len({value['lane'] for value in lanes}) == 10 and {value['lane'] for value in lanes} == set(EXPECTED_LANES),
              'exact ten distinct repair lanes required')
    q.require(all(value['invocation'] == execution['invocation'] for value in cleanup + restore),
              'recovery belongs to another measurement invocation')
    return lanes, cleanup[0], restore[0]


def reconstructed_counter(p, lanes):
    return p.checks_counter([{'id':receipt['lane']+'/'+case['id']+'/'+check['id'],'passed':check['passed']}
        for receipt in lanes for case in receipt['cases'] for check in case['checks']])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--attempt',type=Path,required=True)
    parser.add_argument('--repo',type=Path,required=True)
    args = parser.parse_args()
    repo, out = args.repo.resolve(), args.attempt.absolute()
    sys.path.insert(0,str(repo/'tools'))
    import p1r_published_qualification as q
    import p1r_qualification as p
    import p1r_qualification_runtime as runtime
    import p1r_isolation as isolation
    q.require(out == out.resolve() and out.is_relative_to(repo), 'audit attempt must be inside mounted owner repository')
    audit = out/'independent-verification'
    audit.mkdir(exist_ok=False)
    (audit/'receipts').mkdir()
    run = out/'measurement'
    old = repo/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run'
    report = {'scope':'existing-3.121.0-package-repair-verification-only','attempt':str(out),'run':str(run),
              'checks':[],'packages':[],'lanes':[],'errors':[], 'four_role_acceptance':'pending',
              'support_disposition':'pending','conformance':'pending','p1r_usable':False,'story_6_1':'blocked',
              'valid_evidence':None,'measurement_valid_evidence':False,'final_outer_isolation_audit':'pending'}
    def save():q.write_json(audit/'independent-validation.json',report)
    def check(name,call):
        try:
            value=call()
            report['checks'].append({'id':name,'passed':True})
            save()
            return value
        except Exception as error:
            message=type(error).__name__+': '+str(error)
            report['checks'].append({'id':name,'passed':False,'reason':message})
            report['errors'].append(name+': '+message)
            save()
    try:
        private = isolation.private_proof(os.environ.get(isolation.OUTER_MARKER,''))
        summary=q.parse_json(q.regular(out/'repair-measurement-summary.json'))
        binding_data=q.regular(out/'scripts-binding.json')
        binding=q.parse_json(binding_data)
        q.require(binding['attempt']==str(out) and set(binding['scripts'])==SCRIPT_NAMES,'executed script inventory differs')
        for name,row in binding['scripts'].items():
            q.require(row['executed_path']==str(out/'scripts'/name),'executed script path differs')
            q.require(all(q.digest(q.regular(Path(row[key])))==row['sha256'] for key in ('executed_path','source_path')),
                      'executed script changed: '+name)
        q.require(binding['scripts']['independent-repair-audit.py']['executed_path']==str(Path(__file__).resolve()),
                  'unfrozen audit refused')
        inputs,data,scope=q.load_document(old/'owner-inputs.json',q.validate_inputs)
        execution=q.parse_json(q.regular(run/'execution.json'))
        source=q.parse_json(q.regular(run/'executor-source.json'))
        report.update(inputs_sha256=q.digest(data),measurement_invocation=execution['invocation'],
                      scripts_binding_sha256=q.digest(binding_data), private_daemon_id=private['daemon']['ID'])
        context={'inputs':{'value':inputs,'scope':scope,'sha256':q.digest(data)},'verified':{},'source_binding':source}
        check('current-exact-source-closure',lambda:p.check_current_source(source))
        check('production-source-remains-equal-to-published-tag',lambda:q.require(
            subprocess.check_output(['git','diff','--binary','v3.121.0','--','src'],cwd=repo)==b'' and
            subprocess.check_output(['git','ls-files','--others','--exclude-standard','--','src'],cwd=repo)==b'',
            'production changed during repair'))
        check('independent-executed-driver-and-manifest-hashes',lambda:q.require(
            execution['repair_driver_sha256']==binding['scripts']['verify-repair.py']['sha256']
            ==q.digest(q.regular(Path(binding['scripts']['verify-repair.py']['executed_path']))) and
            execution['repair_scripts_binding_sha256']==q.digest(binding_data) and execution['repair_attempt']==str(out),
            'recorded driver or manifest hash differs from independently read frozen bytes'))
        check('measurement-invocation-has-no-executor-errors',lambda:q.require(not execution['errors'],'executor errors remain'))
        check('original-inputs-and-unchanged-source-file-binding',lambda:q.require(
            execution['inputs_sha256']==q.digest(data) and execution['executor_source_sha256']==q.digest(q.regular(run/'executor-source.json')),
            'input/source binding differs'))
        check('repair-scope-cannot-grant-immutable-successor-qualification',lambda:q.require(
            'repair_verification_scope' in execution and not summary['p1r_usable'] and summary['story_6_1']=='blocked'
            and summary['run']==str(run),'repair scope, run or blocked status differs'))
        invocation=uuid.uuid4().hex
        for role in q.package_roles(inputs):
            if role=='rollback':continue
            def package(role=role):
                start=p.stamp()
                row,verified=q.execute_package_lane(audit,invocation,role,run/'consumers'/role,context['inputs'],q.default_verifier)
                end=p.stamp()
                receipt,outcome,retained=q.validate_package_receipt(audit,invocation,role,row['receipts'][0],
                    context['inputs'],*p.validate_times({'started_utc':start,'finished_utc':end}))
                q.require(row['execution']=='passed' and verified is not None,'signature/graph/physical load check failed')
                context['verified'][role]=verified
                report['packages'].append({'role':role,'row':row,'retained_files':len(retained)})
            check('independent-package-signature-assets-lock-physical-load:'+role,package)

        paths=[Path(value) for value in execution['receipts']]
        receipts=[q.parse_json(q.regular(path)) for path in paths]
        lanes,cleanup,restore=require_inventory(q,runtime,run,execution,receipts)
        report['checks'].append({'id':'exact-ten-distinct-lanes-and-all-persisted-registered-recovery','passed':True})
        inventory=q.executed_case_inventory(inputs)
        for lane in lanes:
            check('complete-case-inventory:'+lane['lane'],lambda lane=lane:q.require(
                [case['id'] for case in lane['cases']]==list(inventory[lane['lane']]),'missing, duplicate or substituted repair case'))
        for receipt,path in zip(receipts,paths):
            def audit_receipt(receipt=receipt,path=path):
                group,lane,outcome=q.receipt_outcome(receipt,context)
                q.require(outcome['execution']=='passed','required repair receipt is nonpassing')
                report['lanes'].append({'group':group,'lane':lane,'receipt':str(path),'sha256':q.digest(q.regular(path)),**outcome})
            check('independent-all-predicates-probes-inventories-configuration:'+receipt.get('lane',receipt['schema']),audit_receipt)
        counters=reconstructed_counter(p,lanes)
        report['reconstructed_assertions']=counters
        report['reconstructed_cases']=sum(len(lane['cases']) for lane in lanes)
        check('reconstructed-all-cases-and-assertions-pass',lambda:q.require(counters['failed']==0 and counters['attempted']>0,
                                                                         'measurement has failed or absent assertions'))
        check('summary-agrees-with-independent-reconstruction',lambda:q.require(summary['assertions']==counters and
            sorted(summary['lanes'],key=lambda v:v['lane'])==sorted([
                {'lane':lane['lane'],'assertions':p.checks_counter([{'id':case['id']+'/'+check['id'],'passed':check['passed']} for case in lane['cases'] for check in case['checks']])}
                for lane in lanes],key=lambda v:v['lane']), 'summary does not match reconstructed receipts'))
        bycase={(lane['lane'],case['id']):case for lane in lanes for case in lane['cases']}
        failures=q.parse_json(q.regular(old/'review-round-1/measurement-summary.json'))['nonpassing_cases']
        resolved=[]
        for previous in failures:
            current=bycase[(previous['lane'],previous['id'])]
            actual={value['id']:value['passed'] for value in current['checks']}
            def supported(previous=previous,current=current,actual=actual):
                q.require(current['operation']=='supported' and current['outcome']=='effect' and
                    all(actual[name] is True for name in previous['failed_checks']) and current['assertions']['failed']==0,
                    'formerly failed supported case was reclassified or still fails')
                if previous['lane']=='metadata-write' or previous['lane']=='checkout' and previous['id']=='metadata-write':
                    prefixes=['package:','source:'] if previous['lane']=='checkout' else ['']
                    q.require(all(actual[prefix+'supported-append-restart-rehydrates-thirteen'] is True and
                                  actual[prefix+'retained-floor-five-preserved'] is True for prefix in prefixes),
                              'append/restart/floor proof missing')
            check('supported-direction-restored:'+previous['lane']+'/'+previous['id'],supported)
            resolved.append({'lane':previous['lane'],'id':previous['id'],'old_failed_count':len(previous['failed_checks'])})
        report['repaired_cases']=resolved
        check('six-cases-and-exact-sixteen-original-failures-covered',lambda:q.require(
            len(resolved)==6 and sum(value['old_failed_count'] for value in resolved)==16,'original failure coverage differs'))
        check('nonempty-complete-shared-inventory-and-strict-equality',lambda:q.require(
            cleanup['shared']['complete'] and bool(cleanup['shared']['before']) and cleanup['shared']['before']==cleanup['shared']['after']
            and cleanup['assertions']=={'attempted':7,'passed':7,'failed':0},'strict cleanup failed or shared inventory vacuous'))
        negative=Path(summary['negative_isolation_control']['receipt'])
        q.require(negative.is_relative_to(out) and negative==negative.resolve(),'negative receipt outside this attempt')
        negative_value=q.parse_json(q.regular(negative))
        check('private-resource-change-still-fails-original-shared-preserved',lambda:q.require(
            [value['id'] for value in negative_value['checks'] if not value['passed']]==['shared-preserved']
            and bool(negative_value['shared']['before']) and negative_value['shared']['before']!=negative_value['shared']['after']
            and not any(value['kind']=='container' for value in negative_value['owned']), 'private change was ignored or sentinel targeted'))
        check('summary-retains-pending-acceptance',lambda:q.require(summary['verified'] is True and
            all(summary[key]=='pending' for key in ('four_role_acceptance','conformance','support_disposition')),
            'repair not measured or acceptance changed'))
        check('source-closure-still-stable-after-audit',lambda:p.check_current_source(source))
        for name,row in binding['scripts'].items():
            q.require(all(q.digest(q.regular(Path(row[key])))==row['sha256'] for key in ('source_path','executed_path')),
                      'script changed during audit: '+name)
        q.require(q.regular(out/'scripts-binding.json')==binding_data,'script manifest changed during audit')
    except (Exception,KeyboardInterrupt) as error:
        report['errors'].append(type(error).__name__+': '+str(error))
    finally:
        report['measurement_valid_evidence']=not report['errors']
        report['finished_utc']=p.stamp()
        report['audit_script_sha256']=p.digest(p.regular(Path(__file__)))
        save()
        print(json.dumps({'independent_audit':str(audit),'measurement_valid_evidence':report['measurement_valid_evidence'],
                          'valid_evidence':None,'final_outer_isolation_audit':'pending','errors':report['errors']}),flush=True)
    return 0 if report['measurement_valid_evidence'] else 2


if __name__=='__main__':
    raise SystemExit(main())
