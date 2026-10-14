from pathlib import Path
import json,shutil,sys,uuid
sys.path.insert(0,str(Path.cwd()/'tools'))
import p1r_published_executor as e
root=Path.cwd()/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run'
inputs,data,_=e.qualification.load_document(root/'owner-inputs.json',e.qualification.validate_inputs)
e.validate_execution_inputs(inputs)
out=root/('source-fallback-'+uuid.uuid4().hex)
base_copytree=shutil.copytree
def fresh_consumers(source,destination,*args,**kwargs):
    if Path(source)==e.CONSUMERS:
        kwargs['ignore']=shutil.ignore_patterns('bin','obj','packages')
    return base_copytree(source,destination,*args,**kwargs)
shutil.copytree=fresh_consumers
class SerializedSourceExecutor(e.Executor):
    def run(self,argv,*args,**kwargs):
        if [str(part) for part in argv[:2]]==['dotnet','restore'] and 'consumers/source/' in str(argv[2]):
            argv=list(argv)+['--disable-parallel','-m:1','-p:BuildInParallel=false','-p:Configuration=Debug']
        return super().run(argv,*args,**kwargs)
worker=SerializedSourceExecutor(out,inputs)
worker.inputs_sha256=e.digest(data)
worker.result['inputs_sha256']=worker.inputs_sha256
worker.result['supplement']={'scope':'fresh checkout lane with explicit serialized Debug/source restore on unchanged frozen sources','original_run':'execution-f47a56499f0542c1b171e110aa48de2f','private_copy_excludes':['bin','obj','packages'],'driver_sha256':e.digest(Path(__file__).read_bytes())}
worker.support_rules=e.provisional_successor_execution_policy(inputs)
worker.save()
try:
    for version in worker.versions:
        worker.build_consumers(version)
        print(json.dumps({'built_package_consumer':version}),flush=True)
    worker.verify_selected_candidate_archives()
    worker.build_consumers(worker.candidate,source=True)
    print(json.dumps({'built_source_consumer':True}),flush=True)
    worker.topology()
    e.preparation.check_current_source(worker.source_binding)
    receipt=worker.lane('checkout',worker.checkout_case,True)
    e.preparation.check_current_source(worker.source_binding)
    print(json.dumps({'checkout':receipt['execution'],'assertions':receipt['assertions'],'out':str(out)}),flush=True)
except Exception as error:
    worker.errors.append(str(error))
finally:
    cleanup=worker.cleanup()
    worker.save()
    print(json.dumps({'out':str(out),'errors':worker.errors,'cleanup':cleanup['execution'],'p1r_usable':False}),flush=True)
raise SystemExit(2 if worker.errors else 0)
