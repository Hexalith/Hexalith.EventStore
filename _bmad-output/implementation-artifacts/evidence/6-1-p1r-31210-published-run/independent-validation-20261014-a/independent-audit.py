#!/usr/bin/env python3
"""Independently validate evidence without granting pending owner dispositions."""
import argparse
import hashlib
import base64
import json
import subprocess
import sys
import uuid
import time
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--repo', type=Path, required=True)
parser.add_argument('--run', type=Path, required=True)
parser.add_argument('--inputs', type=Path, required=True)
parser.add_argument('--boundary', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
sys.path.insert(0, str(args.repo / 'tools'))
import p1r_published_qualification as q
import p1r_qualification as prep

args.out.mkdir(parents=True, exist_ok=False)
(args.out / 'receipts').mkdir()
report = {'schema':'hexalith.p1r.independent-evidence-audit.v1',
          'started_utc':prep.stamp(), 'checks':[], 'packages':[], 'lanes':[],
          'errors':[], 'four_role_acceptance':'pending', 'support_disposition':'pending',
          'p1r_usable':False, 'story_6_1':'blocked'}

def save():
    q.write_json(args.out / 'independent-validation.json', report)

def check(name, function):
    try:
        result = function()
        report['checks'].append({'id':name,'passed':True})
        save()
        return result
    except Exception as error:
        message = str(error) if isinstance(error, prep.InvalidPacket) else type(error).__name__ + ': ' + str(error)
        report['checks'].append({'id':name,'passed':False,'reason':message})
        report['errors'].append(name + ': ' + message)
        save()
        return None

save()
inputs, data, scope = q.load_document(args.inputs, q.validate_inputs)
context = {'inputs':{'value':inputs,'scope':scope,'sha256':q.digest(data)}, 'verified':{},
           'source_binding':q.parse_json(q.regular(args.run / 'executor-source.json'))}
execution = q.parse_json(q.regular(args.run / 'execution.json'))
evidence_root = args.inputs.parent
proposal_path = args.repo.parent.parent / '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-publication/qualification-proposal.json'
proposal = q.parse_json(q.regular(proposal_path))
approval = q.parse_json(q.regular(evidence_root / 'approval.json'))
decisions = q.parse_json(q.regular(evidence_root / 'owner-decisions.json'))
check('exact-approved-proposal-and-retained-profile-additions', lambda: q.require(
    inputs['candidate'] == proposal['candidate_proposed']
    and inputs['rollback'] is None
    and [v['version'] for v in inputs['comparisons']] == proposal['comparisons_proposed']
    and all(inputs['operational_profile'][k] == v for k,v in proposal['operational_profile_proposed'].items())
    and inputs['selected_additions'] == proposal['additions_proposed']
    and inputs['assertion_instrumentation']['mechanism'] == proposal['assertion_instrumentation_proposed']['mechanism']
    and approval['proposal_sha256'] == q.digest(q.regular(proposal_path)), 'approved proposal substituted'))
check('all-four-owner-decisions-and-conformance-stay-pending', lambda: q.require(
    decisions['inputs_sha256'] == q.digest(data)
    and len(decisions['decisions']) == 4
    and all(v['decision'] == 'pending' for v in decisions['decisions'])
    and decisions['conformance']['status'] == 'pending', 'invented role acceptance'))
report.update(inputs_sha256=q.digest(data), execution_sha256=q.digest(q.regular(args.run / 'execution.json')),
              executor_source_sha256=q.digest(q.regular(args.run / 'executor-source.json')))
check('exact-current-source-closure', lambda: prep.check_current_source(context['source_binding']))
check('execution-owner-input-binding', lambda: q.require(execution['inputs_sha256']==q.digest(data), 'execution inputs substituted'))
check('execution-source-file-binding', lambda: q.require(execution['executor_source_sha256']==report['executor_source_sha256'], 'execution source substituted'))
invocation = uuid.uuid4().hex
report['invocation'] = invocation
for role in q.package_roles(inputs):
    if role == 'rollback':
        continue
    directory = args.run / 'consumers' / role
    deadline=time.monotonic()+1800
    while not (directory/'evidence.json').is_file():
        if time.monotonic()>deadline:
            raise TimeoutError('published consumer manifest wait expired: '+role)
        report['waiting_for']='consumer:'+role
        save()
        time.sleep(5)
    def package_check(role=role,directory=directory):
        start=prep.stamp()
        row, verified=q.execute_package_lane(args.out, invocation, role, directory,
                                             context['inputs'], q.default_verifier)
        end=prep.stamp()
        receipt, outcome, retained=q.validate_package_receipt(args.out, invocation, role,
            row['receipts'][0],context['inputs'],*prep.validate_times({'started_utc':start,'finished_utc':end}))
        q.require(row['execution']=='passed' and verified is not None,'published package lane failed')
        context['verified'][role]=verified
        report['packages'].append({'role':role, 'row':row, 'retained_file_count':len(retained)})
    check('fresh-signature-graph-load-verification:'+role,package_check)

deadline=time.monotonic()+3600
while True:
    try:
        execution=q.parse_json(q.regular(args.run/'execution.json'))
        if execution.get('finished_utc') is not None:
            break
    except Exception:
        pass
    if time.monotonic()>deadline:
        raise TimeoutError('executor completion wait expired')
    report['waiting_for']='executor completion'
    save()
    time.sleep(5)
report.pop('waiting_for',None)
report['execution_sha256']=q.digest(q.regular(args.run/'execution.json'))
lane_receipts=[]
for name in execution['receipts']:
    path=Path(name)
    receipt=q.parse_json(q.regular(path))
    label=receipt.get('lane',receipt.get('schema'))
    def receipt_check(receipt=receipt,path=path):
        group,lane,outcome=q.receipt_outcome(receipt,context)
        report['lanes'].append({'group':group,'lane':lane,'receipt':str(path),
            'sha256':q.digest(q.regular(path)),**outcome})
        if group in ('scenarios','additions'):
            lane_receipts.append(receipt)
    check('independent-receipt-contract-bindings-witnesses:'+label,receipt_check)

def positive_snapshot_check():
    restore_lane=next(r for r in lane_receipts if r['lane']=='post-upgrade-restore')
    case_evidence=restore_lane['execution_evidence']['cases'][0]
    observation=case_evidence['observations']
    seed=observation['seed_snapshot']
    expected=seed['persisted_snapshot']
    q.require(expected['snapshotEnvelopeVersion']==1 and expected['sequenceNumber']==9
              and expected['state']=={'count':9}, 'missing qualified folded snapshot')
    probes=[]
    persisted=[]
    for command in case_evidence['commands']:
        argv=command['argv']
        if 'manual-snapshot' in argv:
            q.require(command['exit_code']==0,'manual snapshot invocation failed')
            probes.append(q.parse_json(command['output'].encode())['observation'])
        if argv[:2]==['docker','exec'] and 'psql' in argv:
            try:
                values=json.loads(command['output'])
            except (json.JSONDecodeError,TypeError):
                continue
            if isinstance(values,list):
                for item in values:
                    if isinstance(item,list) and len(item)==2 and 'tenant-a' in item[0] and item[0].endswith(':snapshot'):
                        value=item[1]
                        if isinstance(value,str):
                            try: value=json.loads(base64.b64decode(value,validate=True))
                            except Exception: continue
                        if value==expected: persisted.append(command['output_sha256'])
    q.require(any(v['outcome']=='Created' and v['sequence']==9 for v in probes)
              and bool(persisted), 'qualified creation differs from literal probe or provider outputs')
    report['qualified_snapshot']={'created_at_sequence':9,'state':{'count':9},'envelope_version':1,
        'persisted_provider_output_sha256':persisted,'manual_result_count':len(probes)}
check('qualified-snapshot-created-by-published-api-and-raw-persisted-readback',positive_snapshot_check)

inventory=q.executed_case_inventory(inputs)
check('all-seventeen-scenarios-and-two-adopted-additions',lambda:q.require(
    set(r['lane'] for r in lane_receipts)==set(inventory) and len(lane_receipts)==len(inventory),
    'missing or duplicate required lane'))
deadline=time.monotonic()+600
while not args.boundary.is_file():
    if time.monotonic()>deadline:
        raise TimeoutError('boundary measurement wait expired')
    report['waiting_for']='measured boundary'
    save()
    time.sleep(5)
report.pop('waiting_for',None)
check('successor-boundary-rederived-from-fresh-receipts',lambda:q.require(
    q.measure_successor_support_table(inputs,lane_receipts)==q.parse_json(q.regular(args.boundary)),
    'measured boundary differs'))
check('pending-support-disposition-remains-unaccepted',lambda:q.require(
    q.parse_json(q.regular(args.boundary))['status']=='measured-pending-disposition'
    and q.parse_json(q.regular(args.boundary))['disposition'] is None,'invented support disposition'))
report['case_count']=sum(len(r['cases']) for r in lane_receipts)
report['assertions']=prep.checks_counter([c for r in lane_receipts for case in r['cases'] for c in case['checks']])
report['failed_cases']=[{'lane':r['lane'],'id':c['id'],'operation':c['operation'],
                       'outcome':c['outcome'],'disposition':c['disposition'],
                       'checks':[v['id'] for v in c['checks'] if not v['passed']]}
                      for r in lane_receipts for c in r['cases']
                      if c['assertions']['failed'] or c['disposition']=='incompatible']
report['executor_errors']=execution['errors']
report['finished_utc']=prep.stamp()
report['valid_evidence']=not report['errors']
save()
script=Path(__file__).read_bytes()
(args.out/'independent-audit.py').write_bytes(script)
q.write_json(args.out/'audit-binding.json',{'script_sha256':hashlib.sha256(script).hexdigest(),
    'argv':sys.argv,'python':sys.executable, 'claim':'evidence validation only; pending disposition and four-role acceptance are unchanged'})
print(json.dumps({'out':str(args.out), 'valid_evidence':report['valid_evidence'],
                  'case_count':report['case_count'],'errors':report['errors'],'p1r_usable':False},sort_keys=True))
sys.exit(0 if report['valid_evidence'] else 2)
