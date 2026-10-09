import hashlib,importlib.util,json,pathlib,shutil,tempfile
ROOT=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
SPEC=importlib.util.spec_from_file_location('callback_current', ROOT/'scripts/verify-dapr-logical-callback-guards.py')
MOD=importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(MOD)
BASE=MOD.BASE; BASE.CONFIGURATION='Release'; BASE.DEPENDENCY_MODE='packages'
OUT=pathlib.Path('/tmp/story66-callback-private-full'); OUT.mkdir(exist_ok=True)
def hashes():
    paths=[p for d in (BASE.CLIENT,BASE.SERVER,BASE.DOMAIN,BASE.CONTRACTS,BASE.DEFAULTS,BASE.UNIQUE_IDS) for p in (ROOT/d).rglob('*') if p.is_file() and 'bin' not in p.parts and 'obj' not in p.parts]
    paths+=list((ROOT/MOD.TESTS/'Events').glob('Dapr*.cs'))
    paths+=[ROOT/p for p in ('scripts/verify-dapr-logical-callback-guards.py','scripts/verify-dapr-logical-model-guards.py','.github/workflows/event-evolution-local-guards.yml','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json')]
    return {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))}
with tempfile.TemporaryDirectory(prefix='eventstore-callback-full-') as temporary:
    work=pathlib.Path(temporary); BASE.prepare(work)
    for pattern in ('DaprLogicalReconstruction*.cs','DaprLogicalCallbackFenceTests.cs'):
        for path in (ROOT/MOD.TESTS/'Events').glob(pattern): shutil.copyfile(path,work/MOD.TESTS/'Events'/path.name)
    project=work/MOD.TESTS/f'{MOD.TESTS.name}.csproj'
    assembly=work/MOD.TESTS/f'bin/Release/net10.0/{MOD.TESTS.name}.dll'
    for label,command in [('build',['dotnet','build',str(project),'--configuration','Release','-p:UseHexalithProjectReferences=false','-m:1','--nologo']),('tests',['dotnet',str(assembly),'-class',MOD.CLASS])]:
        before=hashes(); result=BASE.run(command,work,OUT/f'{label}.log',60); after=hashes()
        BASE.COMMANDS[-1].update({'rootInputSetSha256Before':before,'rootInputSetSha256After':after,'rootInputSetUnchanged':before==after})
        if result.returncode or before != after: raise RuntimeError(f'{label}: failure or root input drift')
        if label=='tests' and not all(text in result.stdout for text in ('Total: 92','Failed: 0','Skipped: 0','Not Run: 0')): raise RuntimeError('incomplete 92-test matrix')
    receipt={'scope':'current-byte composed callback loss matrix','configuration':'Release','dependencyMode':'packages','result':'passed','tests':92,'commands':BASE.COMMANDS,'actualCrossActorInvocation':False,'productionQualification':False,'activationAuthority':False}
    (OUT/'result.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('current-byte private full matrix: 92 passed; dynamic root inputs and executed DLLs unchanged')
