python3 - <<'PY'
from pathlib import Path
p=Path('tools/p1r_published_qualification.py');s=p.read_text().replace('            files.append({"path": str(path), "sha256": row["sha256"]})', '''            built = source_root / "src" / row["name"] / "bin/Debug/net10.0" / (row["name"] + ".dll")
            require(digest(regular(built)) == row["sha256"], "Debug loaded assembly differs from selected source build output")
            files.extend([{"path": str(path), "sha256": row["sha256"]},
                          {"path": str(built), "sha256": row["sha256"]}])''')
p.write_text(s)
p=Path('tools/tests/test_p1r_published_qualification.py');s=p.read_text();pos=s.index('class PostgresqlComponentTests(')
s=s[:pos]+'''class TaggedSourceConsumerTests(unittest.TestCase):
    def fixture(self, directory):
        base = Path(directory)
        root,source_root = base/'consumers/source',base/'candidate'
        root.mkdir(parents=True);source_root.mkdir()
        sdk = b'{"sdk":{"version":"10.0.401"}}'
        (root/'global.json').write_bytes(sdk)
        binding = {'candidate_source':{'repository':str(source_root),'main':{'files':[{'path':'global.json','sha256':q.digest(sdk)}]}}}
        manifest = {'role':'source','configuration':'Debug','projects':[]}
        for project in ('Host','Domain','Probe'):
            stem = project.lower()
            files = {'assets':stem+'/obj/project.assets.json','lock':stem+'/packages.lock.json','loaded':'loaded/'+project+'.json'}
            output = stem+'/bin/Debug/net10.0'
            references = {str(source_root/'src'/name/(name+'.csproj')):{} for name in q.SUCCESSOR_PACKAGE_IDS}
            loaded = []
            for name in q.SUCCESSOR_PACKAGE_IDS:
                data = (project+name).encode()
                # Build outputs are shared; each consumer loads the same actual source assembly.
                data = name.encode()
                for path in (root/output/(name+'.dll'),source_root/'src'/name/'bin/Debug/net10.0'/(name+'.dll')):
                    path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
                loaded.append({'name':name,'path':str(root/output/(name+'.dll')),'sha256':q.digest(data)})
            values = {'assets':{'project':{'restore':{'frameworks':{'net10.0':{'projectReferences':references}}}},
                                'libraries':{'Server':{'type':'project','path':str(source_root/'src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj')}}},
                      'lock':{},'loaded':{'assemblies':loaded}}
            for name,path in files.items():
                (root/path).parent.mkdir(parents=True,exist_ok=True);q.write_json(root/path,values[name])
            manifest['projects'].append({'name':project,'output':output,**files})
        q.write_json(root/'evidence.json',manifest)
        return root,binding

    def testIndependentSourceGraphProofRejectsRehashedRootAndLoadedBinarySubstitution(self):
        for mutation in (None,'root','assembly','sdk','lock'):
            with self.subTest(mutation=mutation),tempfile.TemporaryDirectory() as directory:
                root,binding = self.fixture(directory)
                path = root/'source-evidence.json'
                q.write_json(path,q.source_consumer_binding(root,binding))
                reference = {'path':str(path),'sha256':q.digest(path.read_bytes()),'content':path.read_text()}
                if mutation == 'root':
                    assets = root/'host/obj/project.assets.json'
                    data = q.parse_json(assets.read_bytes())
                    refs = data['project']['restore']['frameworks']['net10.0']['projectReferences']
                    key = next(iter(refs));refs[str(q.ROOT/'src/Substituted.csproj')] = refs.pop(key)
                    q.write_json(assets,data)
                    record = q.parse_json(path.read_bytes())
                    next(item for item in record['files'] if item['path']==str(assets))['sha256'] = q.digest(assets.read_bytes())
                    q.write_json(path,record)
                    reference.update(sha256=q.digest(path.read_bytes()),content=path.read_text())
                elif mutation == 'assembly':
                    (root/'host/bin/Debug/net10.0/Hexalith.EventStore.Server.dll').write_bytes(b'substituted')
                elif mutation == 'sdk':
                    (root/'global.json').write_text('{"sdk":{"version":"10.0.402"}}')
                elif mutation == 'lock':
                    (root/'host/packages.lock.json').write_text('{"substituted":true}')
                if mutation is None:
                    q.validate_source_consumer({'source_consumer':reference},binding)
                else:
                    with self.assertRaises(preparation.InvalidPacket):
                        q.validate_source_consumer({'source_consumer':reference},binding)


''' +s[pos:];p.write_text(s)
PY
python3 -O -m unittest discover -s tools/tests -p 'test_p1r_isolation_hardening.py' > /tmp/p1r-31251-verifier-hardening-optimized-tests.log 2>&1