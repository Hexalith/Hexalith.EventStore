import ast,datetime,hashlib,json,pathlib,subprocess,time
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore');out=pathlib.Path(__file__).parent
commands=[('command-state-vectors',['python3','scripts/verify-dapr-logical-command-state-vectors.py']),('logical-model-vectors',['python3','scripts/verify-dapr-logical-model-vectors.py']),('reconstruction-vectors',['python3','scripts/verify-dapr-logical-reconstruction-vectors.py']),('event-evolution-preflight',['python3','scripts/verify-event-evolution.py','--mutations']),('workflow-actionlint',['actionlint','.github/workflows/event-evolution-local-guards.yml'])]
def inputs():
 files=[p for d in ['scripts','_bmad-output/implementation-artifacts'] for p in (root/d).rglob('*') if p.is_file() and p.suffix in ['.py','.md','.json','.yml','.yaml'] and 'evidence' not in p.parts and '__pycache__' not in p.parts]
 files+=[root/'.github/workflows/event-evolution-local-guards.yml',root/'.editorconfig',root/'.gitattributes']
 files +=[root/p for p in ['_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json','_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json','_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/vectors.json']]
 return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
for name,argv in commands:
 before=inputs();start=datetime.datetime.now(datetime.timezone.utc).isoformat();tick=time.monotonic()
 r=subprocess.run(argv,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=60)
 (out/(name+'.log')).write_text(r.stdout);after=inputs()
 (out/(name+'.json')).write_text(json.dumps({'argv':argv,'cwd':str(root),'startedUtc':start,'elapsedSeconds':round(time.monotonic()-tick,3),'exitCode':r.returncode,'dynamicSelectedInputsSha256Before':before,'dynamicSelectedInputsSha256After':after,'selectedInputSetUnchanged':before==after,'scope':'static local vectors/workflow/evolution controls; not runtime or production qualification'},indent=2)+'\n')
 print(name,r.returncode, 'unchanged',before==after,flush=True)
 if r.returncode or before!=after:raise SystemExit('Static check failed/drifted: '+name)
files=[root/'scripts'/p for p in ['verify-dapr-logical-command-state-vectors.py','verify-dapr-logical-command-state-guards.py','verify-dapr-logical-reconstruction-guards.py','verify-dapr-logical-model-guards.py']]
for p in files:ast.parse(p.read_text(),filename=str(p))
(out/'script-syntax.json').write_text(json.dumps({'result':'passed','argv':['python3','ast.parse','four changed/new guard/vector scripts'],'inputsSha256':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}},indent=2)+'\n')
print('script-syntax passed',flush=True)
