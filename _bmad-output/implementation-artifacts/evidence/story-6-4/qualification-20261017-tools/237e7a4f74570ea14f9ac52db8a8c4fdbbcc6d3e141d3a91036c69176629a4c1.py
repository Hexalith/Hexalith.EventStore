import hashlib,json,pathlib,runpy,sys
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore');tool=root/'tools/pack-release-packages.py';sys.path.insert(0,str(tool.parent));namespace=runpy.run_path(str(tool));entry=namespace['main'];original_run=entry.__globals__['run']
def serialized(command):
 actual=[*command,'-m:1'];print('SERIALIZED_PACKAGE_NATIVE_COMMAND '+json.dumps(actual),flush=True);original_run(actual)
entry.__globals__['run']=serialized
requested=sys.argv[2]
effective='999.0.0-ci-test' if requested=='0.0.0-ci-test' else requested
print(json.dumps({'officialProducer':str(tool),'officialProducerSha256':hashlib.sha256(tool.read_bytes()).hexdigest(),'requestedVersion':requested,'effectiveVersion':effective,'change':'Append only -m:1 to each official manifest-owned dotnet pack command; preserve manifest validation, projects, properties and version normalization.'}),flush=True)
sys.argv=[str(tool),sys.argv[1],effective]
raise SystemExit(entry())
