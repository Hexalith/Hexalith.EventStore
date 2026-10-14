from pathlib import Path
import json,os,shutil,sys,tempfile,uuid
sys.path.insert(0,str(Path.cwd()/'tools'))
import p1r_published_executor as e
root=Path.cwd()/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run'
out=root/('source-restore-triage-'+uuid.uuid4().hex)
out.mkdir()
worker=e.Executor.__new__(e.Executor)
worker.output=out
worker.scratch=Path(tempfile.mkdtemp(prefix='p1r-source-restore-'))
worker.commands,worker.configurations,worker.processes,worker.receipts,worker.errors=[],[],[],[],[]
worker.result={'schema':'hexalith.p1r.source-restore-triage.v1','started_utc':e.stamp(),'scope':'focused Debug/source restore diagnostic only; not operational qualification','p1r_usable':False}
worker.save()
source=e.preparation.source_binding()
e.write_json(out/'executor-source.json',source)
consumer=out/'consumer'
shutil.copytree(e.CONSUMERS,consumer,ignore=shutil.ignore_patterns('bin','obj','packages'))
environment=dict(os.environ,NUGET_PACKAGES=str(consumer/'packages'),DOTNET_NOLOGO='true',DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE='false',DOTNET_USE_POLLING_FILE_WATCHER='1')
environment.pop('CI',None)
argv=['dotnet','restore',consumer/'host/Host.csproj','--disable-parallel','-m:1','-p:BuildInParallel=false','-p:Configuration=Debug','-p:EventStoreVersion=3.121.0','-p:NuGetAudit=false','-p:UseCurrentSource=true','-p:UseHexalithProjectReferences=true','-p:EventStoreSourceRoot='+str(e.ROOT)]
try:
    worker.run(argv,cwd=consumer,env=environment,timeout=180,check=False)
finally:
    shutil.rmtree(worker.scratch)
    worker.result['finished_utc']=e.stamp()
    worker.result['exit_code']=worker.commands[-1]['exit_code'] if worker.commands else None
    worker.save()
    print(json.dumps({'out':str(out),'exit_code':worker.result['exit_code']}),flush=True)
raise SystemExit(worker.result['exit_code'] or 0)
