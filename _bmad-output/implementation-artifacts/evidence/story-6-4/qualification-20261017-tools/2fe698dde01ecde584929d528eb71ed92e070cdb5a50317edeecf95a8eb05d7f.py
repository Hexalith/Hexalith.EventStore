import hashlib,json,os,pathlib,time
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore');base=root/'_bmad-output/implementation-artifacts/evidence/story-6-4';target=base/'qualification-20261017-assets';target.mkdir(exist_ok=True)
seen={};records=[]
while True:
 for area in [root/'src',root/'tests',root/'samples']+[p for p in (root/'references').iterdir() if p.is_dir()]:
  for folder,dirs,names in os.walk(area):
   dirs[:]=[d for d in dirs if d not in {'bin','.git','references','node_modules','_bmad','_bmad-output','.agents','.codex'}]
   if 'project.assets.json' not in names:continue
   path=pathlib.Path(folder)/'project.assets.json'
   try:
    stamp=path.stat().st_mtime_ns
    if seen.get(str(path))==stamp:continue
    body=path.read_bytes();data=json.loads(body);digest=hashlib.sha256(body).hexdigest()
    if path.stat().st_mtime_ns!=stamp:continue
    output=target/(digest+'.json')
    if not output.exists():output.write_bytes(body)
    seen[str(path)]=stamp;records.append({'path':str(path.relative_to(root)),'sha256':digest,'asset':str(output.relative_to(root)),'libraries':{k:{f:v[f] for f in ['type','path','sha512'] if f in v} for k,v in data.get('libraries',{}).items()}})
   except (FileNotFoundError,PermissionError,json.JSONDecodeError):pass
 (base/'qualification-20261017-effective-assets.json').write_text(json.dumps(records,indent=2)+'\n')
 state=json.loads((base/'qualification-20261017-gates.json').read_text())
 if state.get('finishedAt'):break
 time.sleep(5)
print('EFFECTIVE_ASSET_ARCHIVE_COMPLETED',len(records),flush=True)
