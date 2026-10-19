#!/usr/bin/env python3
"""Fresh bounded controls; never a full P1R invocation or acceptance decision."""
import argparse,json,os,sys,uuid
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('mode',choices=['ownership','reminder'])
for name in ('verifier-root','source-root','inputs','out','wrapper'):
 p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args();sys.path.insert(0,str(a.verifier_root/'tools'))
import p1r_qualification as preparation
import p1r_published_executor as executor
import p1r_isolation as isolation
import p1r_check_witnesses as witnesses
source=Path(__file__).read_bytes();result={'scope':'bounded private control; not full qualification','mode':a.mode,'p1r_usable':False,'script_sha256':preparation.digest(source),'started_utc':preparation.stamp(),'errors':[]}
try:
 if a.mode=='ownership':
  preparation.require(os.geteuid()==0,'control must exercise actual private root ownership')
  preparation.require(not a.out.exists(),'control output already exists')
  (a.out/'receipts').mkdir(parents=True)
  binding=preparation.source_binding(a.verifier_root,a.source_root)
  preparation.write_json(a.out/'executor-source.json',binding)
  receipt=a.out/'receipts/control.json';preparation.write_json(receipt,{'scope':'synthetic metadata with real SDK ownership','p1r_usable':False})
  before=preparation.regular(receipt)
  preparation.write_json(a.out/'execution.json',{'schema':'hexalith.p1r.executor.v1','scope':'controlled ownership metadata; canonical run vector is not executed','invocation':uuid.uuid4().hex,'finished_utc':preparation.stamp(),'inputs_sha256':preparation.digest(preparation.regular(a.inputs)),'executor_source_sha256':preparation.digest(preparation.regular(a.out/'executor-source.json')),'receipts':[str(receipt)]})
  command=['python3',str(a.verifier_root/'tools/p1r-published-executor.py'),'run','--out',str(a.out),'--inputs',str(a.inputs),'--source-root',str(a.source_root)]
  result['controlled_command_vector']=command
  result['handoff']=isolation.handoff_executor_output(command,a.wrapper,binding,a.source_root,True)
  preparation.require(preparation.regular(receipt)==before,'handoff changed control bytes')
  result['receipt']=str(receipt);result['receipt_sha256_before_host_finalization']=preparation.digest(before)
 else:
  raw=preparation.regular(a.inputs);inputs=json.loads(raw)
  executor.validate_execution_inputs(inputs,a.source_root)
  worker=executor.Executor(a.out,inputs,source_root=a.source_root)
  worker.inputs_sha256=preparation.digest(raw);worker.result['inputs_sha256']=worker.inputs_sha256;worker.save()
  result['cases']=[]
  try:
   worker.build_consumers(worker.candidate);worker.verify_selected_candidate_archives();worker.topology()
   for identifier in ('scheduled-restart-effect','stale-generation-refusal','tenant-preservation'):
    row,evidence=worker.case('reminder-recovery',identifier,lambda checks,identifier=identifier:worker.reminder_case(identifier,checks),True)
    retained=[worker.witness_sources[path] for path in sorted(worker.witness_sources)]
    case_path=a.out/('case-'+identifier+'.json');preparation.write_json(case_path,{'row':row,'evidence':evidence,'predicate_sources':retained})
    witnesses.validate_case(row,evidence,worker.source_binding,'reminder-recovery',retained,candidate_version='3.125.1')
    result['cases'].append({'id':identifier,'path':str(case_path),'sha256':preparation.digest(preparation.regular(case_path)),'assertions':row['assertions'],'outcome':row['outcome']})
    preparation.write_json(a.out/'smoke.json',result)
   preparation.require(worker.errors==[],'canonical Executor control recorded errors')
   preparation.require(all(c['assertions']['failed']==0 for c in result['cases']),'Reminder control retained nonpassing assertions')
  finally:
   cleanup=worker.cleanup();result['cleanup_assertions']=cleanup.get('assertions');result['executor_errors']=worker.errors
   worker.result['finished_utc']=preparation.stamp();worker.save()
 preparation.require(preparation.digest(Path(__file__).read_bytes())==result['script_sha256'],'control script changed')
except Exception as error:
 result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
 result['finished_utc']=preparation.stamp()
 preparation.write_json(a.out/'smoke.json',result)
print(json.dumps({k:result[k] for k in ('mode','errors','p1r_usable')},sort_keys=True))
raise SystemExit(2 if result['errors'] else 0)
