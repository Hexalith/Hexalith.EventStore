#!/usr/bin/env python3
"""Canonical real snapshot/GET fixture control; never a full qualification."""
import argparse,json,sys
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
for name in ('verifier-root','source-root','inputs','out'):
    p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args()
sys.path.insert(0,str(a.verifier_root/'tools'))
import p1r_qualification as preparation
import p1r_published_executor as executor
import p1r_check_witnesses as witnesses
script=Path(__file__).resolve()
result={'scope':'bounded real canonical snapshot/GET control; no full matrix or acceptance',
        'qualified':False,'p1r_usable':False,'started_utc':preparation.stamp(),'finished_utc':None,
        'script_sha256':preparation.digest(preparation.regular(script)),'errors':[]}
worker=None
try:
    raw=preparation.regular(a.inputs)
    inputs=json.loads(raw)
    executor.validate_execution_inputs(inputs,a.source_root)
    worker=executor.Executor(a.out,inputs,source_root=a.source_root)
    worker.inputs_sha256=preparation.digest(raw)
    worker.result['inputs_sha256']=worker.inputs_sha256
    worker.save()
    worker.build_consumers(worker.candidate)
    worker.build_consumers('3.110.0')
    worker.verify_selected_candidate_archives()
    worker.topology()
    identity='3.125.1-to-3.110.0'
    row,evidence=worker.case('metadata-write',identity,
        lambda checks:worker.live_case('metadata-write',identity,checks),True)
    retained=[worker.witness_sources[path] for path in sorted(worker.witness_sources)]
    case=a.out/'case.json'
    preparation.write_json(case,{'row':row,'evidence':evidence,'predicate_sources':retained})
    result['case']={'path':str(case),'sha256':preparation.digest(preparation.regular(case)),
                    'assertions':row['assertions'],'outcome':row['outcome']}
    preparation.write_json(a.out/'smoke.json',result)
    witnesses.validate_case(row,evidence,worker.source_binding,'metadata-write',retained,candidate_version='3.125.1')
    gets=[command for command in evidence['commands'] if command['argv'][:2]==['HTTP','GET']]
    result['actual_get_commands']=[{'id':command['id'],'argv':command['argv'],'exit_code':command['exit_code'],
                                  'started_utc':command['started_utc'],'finished_utc':command['finished_utc']}
                                 for command in gets]
    preparation.require(gets and all(len(command['argv'])==4 and command['argv'][3]=='request-sha256='+preparation.digest(b'')
                                    for command in gets),'real canonical GET evidence profile differs')
    preparation.require(row['assertions']['failed']==0 and worker.errors==[],'real snapshot control retained failures')
    preparation.require(preparation.digest(preparation.regular(script))==result['script_sha256'],'smoke script changed')
except Exception as error:
    result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
    if worker is not None:
        cleanup=worker.cleanup()
        result['cleanup_assertions']=cleanup.get('assertions')
        result['executor_errors']=worker.errors
        worker.result['finished_utc']=preparation.stamp()
        worker.save()
    result['finished_utc']=preparation.stamp()
    a.out.mkdir(parents=True,exist_ok=True)
    preparation.write_json(a.out/'smoke.json',result)
print(json.dumps({key:result.get(key) for key in ('errors','case','cleanup_assertions','qualified','p1r_usable')}))
raise SystemExit(2 if result['errors'] or result.get('executor_errors') else 0)
