#!/usr/bin/env python3
"""Independently rederive repair witnesses; this is not successor qualification."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid

REPO=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(REPO/'tools'))
import p1r_published_qualification as q
import p1r_qualification as p
import p1r_qualification_runtime as r

OUT=Path(__file__).resolve().parent
summary=json.loads((OUT/'repair-measurement-summary.json').read_bytes())
RUN=Path(summary['run'])
AUDIT=OUT/'independent-verification'
AUDIT.mkdir(exist_ok=False)
(AUDIT/'receipts').mkdir()
OLD=OUT.parent/'6-1-p1r-31210-published-run'
inputs,data,scope=q.load_document(OLD/'owner-inputs.json',q.validate_inputs)
execution=json.loads((RUN/'execution.json').read_bytes())
source=json.loads((RUN/'executor-source.json').read_bytes())
report={'scope':'existing-3.121.0-package-repair-verification-only','checks':[],'packages':[],
        'lanes':[],'errors':[],'inputs_sha256':q.digest(data),'four_role_acceptance':'pending',
        'support_disposition':'pending','conformance':'pending','p1r_usable':False,'story_6_1':'blocked'}
context={'inputs':{'value':inputs,'scope':scope,'sha256':q.digest(data)},'verified':{},'source_binding':source}

def save():q.write_json(AUDIT/'independent-validation.json',report)
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

check('current-exact-source-closure',lambda:p.check_current_source(source))
check('production-source-remains-equal-to-published-tag',lambda:q.require(
    subprocess.check_output(['git','diff','--binary','v3.121.0','--','src'],cwd=REPO)==b'' and
    subprocess.check_output(['git','ls-files','--others','--exclude-standard','--','src'],cwd=REPO)==b'',
    'production changed during repair'))
check('original-inputs-and-unchanged-source-file-binding',lambda:q.require(
    execution['inputs_sha256']==q.digest(data) and execution['executor_source_sha256']==q.digest(p.regular(RUN/'executor-source.json')),
    'input/source binding differs'))
check('repair-scope-cannot-grant-immutable-successor-qualification',lambda:q.require(
    'repair_verification_scope' in execution and not summary['p1r_usable'] and summary['story_6_1']=='blocked',
    'repair scope or blocked status missing'))
invocation=uuid.uuid4().hex
for role in q.package_roles(inputs):
    if role=='rollback':continue
    def package(role=role):
        start=p.stamp()
        row,verified=q.execute_package_lane(AUDIT,invocation,role,RUN/'consumers'/role,context['inputs'],q.default_verifier)
        end=p.stamp()
        receipt,outcome,retained=q.validate_package_receipt(AUDIT,invocation,role,row['receipts'][0],
            context['inputs'],*p.validate_times({'started_utc':start,'finished_utc':end}))
        q.require(row['execution']=='passed' and verified is not None,'signature/graph/physical load check failed')
        context['verified'][role]=verified
        report['packages'].append({'role':role,'row':row,'retained_files':len(retained)})
    check('independent-package-signature-assets-lock-physical-load:'+role,package)

paths=[Path(v) for v in execution['receipts']]
receipts=[q.parse_json(q.regular(v)) for v in paths]
check('every-registered-receipt-included',lambda:q.require(len(paths)==len(set(paths)) and
    all(v.parent.parent==RUN for v in paths),'duplicate or substituted receipt'))
check('unique-cleanup-and-required-dependent-restore',lambda:q.require(
    len([v for v in receipts if v.get('schema')==r.CLEANUP_SCHEMA])==1 and
    len([v for v in receipts if v.get('schema')==r.RESTORE_SCHEMA])==1,'missing/ambiguous recovery receipts'))
for receipt,path in zip(receipts,paths):
    def audit(receipt=receipt,path=path):
        group,lane,outcome=q.receipt_outcome(receipt,context)
        report['lanes'].append({'group':group,'lane':lane,'receipt':str(path),'sha256':q.digest(q.regular(path)),**outcome})
    check('independent-all-predicates-probes-inventories-configuration:'+receipt.get('lane',receipt['schema']),audit)

bycase={(v['lane'],case['id']):case for v in receipts if 'lane' in v for case in v['cases']}
old=json.loads((OLD/'review-round-1/measurement-summary.json').read_bytes())['nonpassing_cases']
resolved=[]
for case in old:
    current=bycase[(case['lane'],case['id'])]
    actual={v['id']:v['passed'] for v in current['checks']}
    def supported(case=case,current=current,actual=actual):
        q.require(current['operation']=='supported' and current['outcome']=='effect' and
                  all(actual[name] is True for name in case['failed_checks']) and current['assertions']['failed']==0,
                  'formerly failed supported case was reclassified or still fails')
        if case['lane']=='metadata-write' or case['lane']=='checkout' and case['id']=='metadata-write':
            prefixes=['package:','source:'] if case['lane']=='checkout' else ['']
            q.require(all(actual[prefix+'supported-append-restart-rehydrates-thirteen'] is True and
                          actual[prefix+'retained-floor-five-preserved'] is True for prefix in prefixes),
                      'append/restart/floor proof missing')
    check('supported-direction-restored:'+case['lane']+'/'+case['id'],supported)
    resolved.append({'lane':case['lane'],'id':case['id'],'old_failed_count':len(case['failed_checks'])})
report['repaired_cases']=resolved
check('six-cases-and-exact-sixteen-original-failures-covered',lambda:q.require(
    len(resolved)==6 and sum(v['old_failed_count'] for v in resolved)==16,'original failure coverage differs'))
check('nonempty-complete-shared-inventory-and-strict-equality',lambda:q.require(
    summary['cleanup']['shared']['complete'] and bool(summary['cleanup']['shared']['before']) and
    summary['cleanup']['shared']['before']==summary['cleanup']['shared']['after'] and
    summary['cleanup']['assertions']=={'attempted':7,'passed':7,'failed':0},'strict cleanup failed or shared inventory vacuous'))
negative=Path(summary['negative_isolation_control']['receipt'])
negative_value=q.parse_json(q.regular(negative))
check('private-resource-change-still-fails-original-shared-preserved',lambda:q.require(
    [v['id'] for v in negative_value['checks'] if not v['passed']]==['shared-preserved'] and
    not any(v['kind']=='container' for v in negative_value['owned']),
    'private change was ignored or sentinel targeted'))
check('repair-summary-verified-with-pending-acceptance',lambda:q.require(summary['verified'] is True and
    all(summary[k]=='pending' for k in ('four_role_acceptance','conformance','support_disposition')),
    'repair not verified or acceptance changed'))
check('source-closure-still-stable-after-audit',lambda:p.check_current_source(source))
report['valid_evidence']=not report['errors']
report['finished_utc']=p.stamp()
report['audit_script_sha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
save()
print(json.dumps({'independent_audit':str(AUDIT),'valid_evidence':report['valid_evidence'],'errors':report['errors']}),flush=True)
raise SystemExit(0 if report['valid_evidence'] else 2)
