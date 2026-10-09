import datetime,hashlib,json,pathlib,subprocess,sys,time
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
label=sys.argv[1]; out=pathlib.Path(sys.argv[2]); command=sys.argv[3:]
out.mkdir(parents=True,exist_ok=True)
def git(*args,cwd=root):
    return subprocess.check_output(['git',*args],cwd=cwd)
def input_hashes():
    files=[]
    files += [root/p.decode() for p in git('ls-files','-z','--cached','--others','--exclude-standard').split(b'\0') if p]
    declared=git('config','--file','.gitmodules','--get-regexp','path').decode().splitlines()
    for declaration in declared:
        repository=root/declaration.split(None,1)[1]
        if not repository.is_dir() or not (repository/".git").exists(): continue
        for p in git('ls-files','-z','--cached','--others','--exclude-standard',cwd=repository).split(b'\0'):
            if p: files.append(repository/p.decode())
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(files)) if p.is_file()}
def dlls():
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/command[1]).parent.glob('*.dll')} if len(command)>1 and command[1].endswith('.dll') else {}
before=input_hashes(); db=dlls(); head=git('rev-parse','HEAD').decode().strip()
started=datetime.datetime.now(datetime.timezone.utc).isoformat(); wall=time.monotonic()
with (out/(label+'.log')).open('w') as log:
    try:
        completed=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT,timeout=1200,check=False)
        code=completed.returncode; error=None
    except subprocess.TimeoutExpired:
        code=124; error='Command exceeded 1200-second deadline.'
after=input_hashes(); da=dlls()
receipt={'argv':command,'cwd':str(root),'head':head,'startedUtc':started,'endedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'elapsedSeconds':round(time.monotonic()-wall,3),'exitCode':code,'error':error,'inputsSha256Before':before,'inputsSha256After':after,'inputSetUnchanged':before==after,'executedDllsSha256Before':db,'executedDllsSha256After':da,'executedDllsUnchanged':db==da}
(out/(label+'.json')).write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps({k:receipt[k] for k in ('argv','head','exitCode','elapsedSeconds','inputSetUnchanged','executedDllsUnchanged')}),flush=True)
sys.exit(code or (0 if before==after and db==da else 2))
