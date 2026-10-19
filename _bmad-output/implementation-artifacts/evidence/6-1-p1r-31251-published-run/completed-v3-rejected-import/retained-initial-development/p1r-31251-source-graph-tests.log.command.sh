python3 - <<'PY'
from pathlib import Path
p=Path('tools/p1r_published_qualification.py');s=p.read_text()
s=s.replace('        references = {Path(path) for framework', '''        project_path = root / project["name"].lower() / (project["name"] + ".csproj")
        require(restore.get("projectPath") == str(project_path), "Debug source assets consumer project path differs")
        files.append({"path": str(project_path), "sha256": digest(regular(project_path))})
        references = {Path(path) for framework''')
s=s.replace('                path = (root / relative(project["assets"])).parent / library["path"]\n                require(path.resolve().is_relative_to(source_root), "Debug source graph escapes selected source closure")', '''                path = (project_path.parent / library["path"]).resolve()
                require(path.is_relative_to(source_root), "Debug source graph escapes selected source closure")
                files.append({"path": str(path), "sha256": digest(regular(path))})''')
p.write_text(s)
p=Path('tools/tests/test_p1r_published_qualification.py');s=p.read_text()
s=s.replace("            loaded = []\n            for name in q.SUCCESSOR_PACKAGE_IDS:", "            consumer_project = root/stem/(project+'.csproj')\n            consumer_project.parent.mkdir(parents=True,exist_ok=True);consumer_project.write_text('<Project />')\n            for name in q.SUCCESSOR_PACKAGE_IDS:\n                source_project = source_root/'src'/name/(name+'.csproj')\n                source_project.parent.mkdir(parents=True,exist_ok=True);source_project.write_text('<Project />')\n            loaded = []\n            for name in q.SUCCESSOR_PACKAGE_IDS:")
s=s.replace("'assets':{'project':{'restore':{'frameworks':{'net10.0':{'projectReferences':references}}}},", "'assets':{'project':{'restore':{'projectPath':str(consumer_project),'frameworks':{'net10.0':{'projectReferences':references}}}},")
s=s.replace("'path':str(source_root/'src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj')", "'path':os.path.relpath(source_root/'src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj',consumer_project.parent)")
s=s.replace("for mutation in (None,'root','assembly','sdk','lock'):", "for mutation in (None,'root','assembly','sdk','lock','project-path','missing-project'):")
s=s.replace("                elif mutation == 'lock':\n                    (root/'host/packages.lock.json').write_text('{\"substituted\":true}')", "                elif mutation == 'lock':\n                    (root/'host/packages.lock.json').write_text('{\"substituted\":true}')\n                elif mutation == 'project-path':\n                    assets = root/'host/obj/project.assets.json'\n                    data = q.parse_json(assets.read_bytes());data['project']['restore']['projectPath'] = str(root/'other/Host.csproj')\n                    q.write_json(assets,data)\n                elif mutation == 'missing-project':\n                    (Path(binding['candidate_source']['repository'])/'src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj').unlink()")
p.write_text(s)
PY
PYTHONPATH=tools:tools/tests python3 -m unittest test_p1r_published_qualification.TaggedSourceConsumerTests > /tmp/p1r-31251-source-graph-tests.log 2>&1
cat /tmp/p1r-31251-source-graph-tests.log