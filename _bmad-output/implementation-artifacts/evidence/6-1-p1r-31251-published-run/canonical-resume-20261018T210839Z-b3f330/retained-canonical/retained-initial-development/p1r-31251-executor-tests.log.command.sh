python3 - <<'PY'
from pathlib import Path
p=Path('tools/p1r_published_qualification.py');s=p.read_text()
s=s.replace('    destinations = {item.get("Destination") for item in runner.get("Mounts", [])}\n', '''    destinations = {item.get("Destination") for item in runner.get("Mounts", [])}
    source = context["source_binding"].get("candidate_source")
    source_root = Path(source["repository"]) if source is not None else None
    if source is not None:
        require(wrapper.get("source_root") == str(source_root)
                and isolation.command_source_root(command) == source_root,
                "final isolation selected tagged source root substituted")
        validate_candidate_source(context["source_binding"], context["inputs"]["value"])
    expected_mount_count = 2 + int(source_root is not None and not any(
        source_root.is_relative_to(Path(item["Destination"])) for item in runner.get("Mounts", [])
        if item.get("Type") == "bind" and Path(item["Destination"]) != source_root))
''')
# Correct computed count when source==workspace: use wrapper's workspace from verifier containment.
s=s.replace('    expected_mount_count = 2 + int(source_root is not None and not any(\n        source_root.is_relative_to(Path(item["Destination"])) for item in runner.get("Mounts", [])\n        if item.get("Type") == "bind" and Path(item["Destination"]) != source_root))', '''    verifier_mounts = [Path(item["Destination"]) for item in runner.get("Mounts", [])
                       if item.get("Type") == "bind" and Path(context["source_binding"]["repository"]).is_relative_to(Path(item["Destination"]))]
    expected_mount_count = 2 + int(source_root is not None and not any(source_root.is_relative_to(path) for path in verifier_mounts))''')
s=s.replace('            and len(destinations) == 2 and "/var/lib/docker" in destinations', '            and len(destinations) == expected_mount_count and "/var/lib/docker" in destinations\n            and (source_root is None or any(source_root.is_relative_to(Path(destination)) for destination in destinations\n                                           if destination != "/var/lib/docker"))')
p.write_text(s)
p=Path('tools/p1r_isolation_evidence.py');s=p.read_text()
s=s.replace('    workspace = Path(next(row[\'Destination\'] for row in mounts if row[\'Type\'] == \'bind\'))', '''    workspace = Path(next(row['Destination'] for row in mounts if row['Type'] == 'bind' and repository.is_relative_to(Path(row['Destination']))))
    source = context['source_binding'].get('candidate_source')
    source_root = Path(source['repository']) if source is not None else None
    bind_roots = {workspace} | ({source_root} if source_root is not None and not source_root.is_relative_to(workspace) else set())
    if source is not None:
        require(wrapper.get('source_root') == str(source_root)
                and isolation.command_source_root(wrapper['inner_command']) == source_root,
                'isolation selected tagged source root differs')
        controller = wrapper.get('controller_source_binding', {})
        def without_python(binding):
            return {key:(without_python(value) if key == 'candidate_source' else value)
                    for key,value in binding.items() if key != 'python'}
        require(without_python(controller) == without_python(context['source_binding']),
                'controller and private source closures differ')''')
s=s.replace("        expected_destinations = {'daemon':{'/var/lib/docker'},'python-tools':set(),\n                                 'sdk':{'/var/lib/docker',str(workspace)}}[role]", "        expected_destinations = {'daemon':{'/var/lib/docker'},'python-tools':set(),\n                                 'sdk':{'/var/lib/docker',*(str(path) for path in bind_roots)}}[role]")
s=s.replace("or mount['Destination'] == str(workspace) and mount.get('Type') == 'bind'", "or Path(mount['Destination']) in bind_roots and mount.get('Type') == 'bind'")
s=s.replace('isolation.sdk_arguments(workspace,repository,invocation,daemon,volume)', 'isolation.sdk_arguments(workspace,repository,invocation,daemon,volume,source_root)')
s=s.replace("    launched = take(['exec',sdk,'python3',repository/'tools/p1r-isolated-runner.py','_inner','--out',Path(wrapper_path).parent,\n                     '--outer-id',wrapper['outer_daemon_id'],'--',*command])", "    source_arguments = ['--source-root', str(source_root)] if source_root is not None else []\n    launched = take(['exec',sdk,'python3',repository/'tools/p1r-isolated-runner.py','_inner','--out',Path(wrapper_path).parent,\n                     '--outer-id',wrapper['outer_daemon_id'],*source_arguments,'--',*command])")
p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py' > /tmp/p1r-31251-executor-tests.log 2>&1