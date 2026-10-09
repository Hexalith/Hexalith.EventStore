import pathlib,hashlib,json,subprocess,datetime,sys
root=pathlib.Path(__file__).resolve().parents[5]; out=pathlib.Path(__file__).resolve().parent
def inventory(base,generated=False):
 return [{'path':str(p.relative_to(root)), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size} for p in sorted(base.rglob('*')) if p.is_file() and (generated or not {'bin','obj','.git'}.intersection(p.relative_to(base).parts))]
def snapshot(name):
 tracked=subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'],cwd=root).decode().split('\0'); files=[]
 for s in sorted(filter(None,tracked)):
  p=root/s
  if p.is_file() and not s.startswith('_bmad-output/implementation-artifacts/evidence/story-8-3/closure-2026-10-09/'):
   files.append({'path':s,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 submodules={}
 paths=subprocess.check_output(['git','config','--file','.gitmodules','--get-regexp','path'],cwd=root,text=True)
 for line in paths.splitlines():
  s=line.split(' ',1)[1]; p=root/s
  if not (p/'.git').exists():submodules[s]={'initialized':False};continue
  submodules[s]={'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=p,text=True).strip(),'status':subprocess.check_output(['git','status','--porcelain=v1'],cwd=p,text=True),'source':[{'path':s+'/'+t,'sha256':hashlib.sha256((p/t).read_bytes()).hexdigest()} for t in subprocess.check_output(['git','ls-files','-z'],cwd=p).decode().split('\0') if t and (p/t).is_file()]}
 data={'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'status':subprocess.check_output(['git','status','--porcelain=v1'],cwd=root,text=True),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'trackedFiles':files,'submodules':submodules,'core':inventory(root/'src/Hexalith.EventStore.PayloadProtection'),'tests':inventory(root/'tests/Hexalith.EventStore.PayloadProtection.Tests'),'security':inventory(root/'src/Hexalith.EventStore.Contracts/Security'),'buildArtifacts':sum([inventory(root/s,True) for s in ['src/Hexalith.EventStore.PayloadProtection/bin','src/Hexalith.EventStore.PayloadProtection/obj','src/Hexalith.EventStore.Contracts/bin','src/Hexalith.EventStore.Contracts/obj','tests/Hexalith.EventStore.PayloadProtection.Tests/bin','tests/Hexalith.EventStore.PayloadProtection.Tests/obj']],[])}
 (out/(name+'.json')).write_text(json.dumps(data,indent=2)+'\n');print(name,len(files))
if sys.argv[1]=='snapshot':snapshot(sys.argv[2])
else:
 name=sys.argv[1];command=sys.argv[2:]; start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (out/(name+'.log')).open('wb') as f:r=subprocess.run(command,cwd=root,stdout=f,stderr=subprocess.STDOUT)
 receipt={'command':command,'cwd':str(root),'startUtc':start,'endUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exitCode':r.returncode,'logSha256':hashlib.sha256((out/(name+'.log')).read_bytes()).hexdigest()}
 (out/(name+'.command.json')).write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt));sys.exit(r.returncode)
