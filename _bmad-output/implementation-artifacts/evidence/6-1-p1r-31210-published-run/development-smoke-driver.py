from pathlib import Path
import json,sys,uuid
sys.path.insert(0,str(Path.cwd()/'tools'))
import p1r_published_executor as e
from p1r_qualification import checks_counter
root=Path.cwd()/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run'
inputs,data,_=e.qualification.load_document(root/'owner-inputs.json',e.qualification.validate_inputs)
out=root/('development-smoke-'+uuid.uuid4().hex)
worker=e.Executor(out,inputs)
worker.inputs_sha256=e.digest(data)
worker.result['inputs_sha256']=worker.inputs_sha256
worker.save()
try:
    for version in worker.versions:
        worker.build_consumers(version)
        print(json.dumps({'built':version}),flush=True)
    worker.verify_selected_candidate_archives()
    worker.topology()
    receipt=worker.lane('post-upgrade-restore',worker.restore_case,True)
    print(json.dumps({'snapshot_restore_smoke':receipt['execution'],'out':str(out)}),flush=True)
except Exception as error:
    worker.errors.append(str(error))
finally:
    cleanup=worker.cleanup()
    if worker.pending_restore is not None:
        receipt=worker.pending_restore
        receipt['cleanup']=cleanup
        commands=e.runtime.validate_commands(receipt['commands'])
        rows={name:value['rows'] for name,value in receipt['inventories'].items()}
        receipt['checks']=e.runtime.restore_observations(receipt,commands,rows)
        receipt['assertions']=checks_counter(receipt['checks'])
        receipt['execution'],receipt['compatibility']=e.runtime.disposition('operational',receipt['assertions']['failed']==0)
        e.runtime.restore_outcome(receipt)
        path=out/'receipts'/(receipt['id']+'.json')
        e.write_json(path,receipt)
        worker.receipts.append(str(path))
    worker.save()
    print(json.dumps({'out':str(out),'errors':worker.errors,'cleanup':cleanup['execution'],'p1r_usable':False}),flush=True)
raise SystemExit(2 if worker.errors else 0)
