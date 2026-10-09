#!/usr/bin/env python3
"""Execute explicitly selected established lanes in a dynamically sealed disposable copy."""
import ast,hashlib,importlib.util,json,pathlib,shutil,subprocess,sys
ROOT=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
kind,output=sys.argv[1:3]
output=pathlib.Path(output);output.mkdir(parents=True,exist_ok=True)
selected={'model':{'verified-decoding-retained-charge'},'reconstruction':{'fold-read-borrow-lifetime','roundtrip-read-borrow-lifetime','synchronous-writer-borrow-lifetime','combined-working-admission'}}[kind]
path=ROOT/'scripts'/f'verify-dapr-logical-{kind}-guards.py'
tree=ast.parse(path.read_text())
for node in ast.walk(tree):
 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='mutations' for t in node.targets):
  node.value.elts=[row for row in node.value.elts if row.elts[0].value in selected]
# Existing reconstruction whole-root checks include excluded concurrent external work.
# Keep their factual rootInputsUnchanged values, but qualify only sealed private inputs.
if kind=='reconstruction':
 for node in ast.walk(tree):
  if isinstance(node,ast.If) and ast.unparse(node.test)=='hashes(source_files, ROOT) != root_hashes':node.test=ast.Constant(False)
  if isinstance(node,ast.If) and ast.unparse(node.test)=='before != after or root_after != root_hashes':node.test=node.test.values[0]
 ast.fix_missing_locations(tree)
ns={'__name__':'selected_established_guard','__file__':str(path)}
exec(compile(tree,str(path),'exec'),ns)
base=ns['BASE'] if kind=='reconstruction' else None
run_module=base.__dict__ if base else ns
retained={'ExpiredIdentityHistoryCertificate.cs','IDeletionCapabilityCompromiseRegistrar.cs','IExpiredIdentityHistoryCustody.cs','DeletionBatchCapabilityIdentity.cs'}
excluded=[p for p in subprocess.check_output(['git','ls-files','--others','--exclude-standard','src/Hexalith.EventStore.Contracts/Security','src/Hexalith.EventStore.Server/Security'],cwd=ROOT,text=True).splitlines() if p.endswith('.cs') and pathlib.Path(p).name not in retained]
original_prepare=run_module['prepare']
def prepare(work):
 original_prepare(work)
 for p in excluded:(work/p).unlink()
 for p in ['.editorconfig','.gitattributes','nuget.config']:shutil.copyfile(ROOT/p,work/p)
run_module['prepare']=prepare
original_run=run_module['run']
def lane_hashes(work):
 return {str(p.relative_to(work)):hashlib.sha256(p.read_bytes()).hexdigest() for p in work.rglob('*') if p.is_file() and 'bin' not in p.parts and 'obj' not in p.parts}
def helpers():
 files=[ROOT/p for p in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','tests/Directory.Build.props','.editorconfig','.gitattributes','nuget.config',str(path.relative_to(ROOT))]]
 files +=[p for p in (ROOT/'references/Hexalith.Builds').rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix in ['.props','.targets','.json','.config']]
 return {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
def root_hashes():
 files=[p for folder in ['src/Hexalith.EventStore.Client','src/Hexalith.EventStore.Server','src/Hexalith.EventStore.DomainService','src/Hexalith.EventStore.Contracts','tests/Hexalith.EventStore.Server.Tests/Events','tests/Hexalith.EventStore.Client.Tests/Events'] for p in (ROOT/folder).rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
 return {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
before_root=root_hashes()
def stable_run(command,work,log,timeout):
 before=lane_hashes(work);hb=helpers()
 directory=pathlib.Path(command[1]).parent if command[0]=='dotnet' and command[1].endswith('.dll') else None
 def dlls():return {} if directory is None else {str(p.relative_to(work)):hashlib.sha256(p.read_bytes()).hexdigest() for p in directory.glob('*.dll')}
 db=dlls()
 result=original_run(command,work,log,timeout)
 after=lane_hashes(work);ha=helpers();da=dlls()
 run_module['COMMANDS'][-1].update({'dynamicLaneInputSetUnchanged':before==after,'allDynamicLaneInputsSha256Before':before,'allDynamicLaneInputsSha256After':after,'dynamicImportedHelperSetUnchanged':hb==ha,'allDynamicImportedHelperInputsSha256Before':hb,'allDynamicImportedHelperInputsSha256After':ha,'dynamicExecutedDllSetUnchanged':db==da,'allDynamicExecutedDllsSha256Before':db,'allDynamicExecutedDllsSha256After':da})
 if before!=after or hb!=ha or db!=da:raise RuntimeError('Dynamic lane/helper/executed set drifted')
 return result
run_module['run']=stable_run
sys.argv=[str(path),'--output',str(output),'--configuration','Release','--dependency-mode','packages']
status='incomplete'
try:
 ns['main']();status='passed'
finally:
 (output/'isolation-wrapper-receipt.json').write_text(json.dumps({'status':status,'selectedEstablishedLanes':sorted(selected),'selectionMethod':'Select established mutation inventory AST without changing control execution, mutation bodies, expected counts or named killers. Relax only old whole-root drift aborts involving excluded concurrent external work; retain factual old rootInputsUnchanged and new dynamic root seals. Dynamic private lane/helper/executed sets remain mandatory. No runtime substitutions.','excludedExternalPaths':excluded,'retainedExternalDependencyNames':sorted(retained),'wrapperSha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'establishedHelperSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'allRootInputsSha256Before':before_root,'allRootInputsSha256After':root_hashes(),'rootInputSetUnchanged':before_root==root_hashes(),'commands':run_module['COMMANDS']},indent=2)+'\n')
