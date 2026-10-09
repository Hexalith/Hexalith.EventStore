import datetime,hashlib,json,pathlib,subprocess,sys,time
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
label=sys.argv[1]; output=pathlib.Path(sys.argv[2]); command=sys.argv[3:]; output.mkdir(parents=True,exist_ok=True)
def hashes():
    files=[]
    for directory in ['src/Hexalith.EventStore.Client','src/Hexalith.EventStore.Server','src/Hexalith.EventStore.Contracts','tests/Hexalith.EventStore.Client.Tests/Events','tests/Hexalith.EventStore.Server.Tests/Events']:
        files.extend(p for p in (root/directory).rglob('*') if p.is_file() and 'bin' not in p.parts and 'obj' not in p.parts)
    files.extend(root/p for p in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','.github/workflows/event-evolution-local-guards.yml','scripts/verify-dapr-logical-callback-guards.py'])
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(files))}
def dlls():
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/command[1]).parent.glob('*.dll')} if len(command)>1 and command[1].endswith('.dll') else {}
before=hashes(); db=dlls(); started=datetime.datetime.now(datetime.timezone.utc).isoformat(); wall=time.monotonic()
with (output/(label+'.log')).open('w') as log:
    result=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT,timeout=600,check=False)
after=hashes(); da=dlls(); receipt={'argv':command,'cwd':str(root),'startedUtc':started,'endedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'elapsedSeconds':round(time.monotonic()-wall,3),'exitCode':result.returncode,'inputsSha256Before':before,'inputsSha256After':after,'inputSetUnchanged':before==after,'executedDllsSha256Before':db,'executedDllsSha256After':da,'executedDllsUnchanged':db==da}
(output/(label+'.json')).write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps({k:receipt[k] for k in ('argv','exitCode','elapsedSeconds','inputSetUnchanged','executedDllsUnchanged')}))
sys.exit(result.returncode or (0 if before==after and db==da else 2))
