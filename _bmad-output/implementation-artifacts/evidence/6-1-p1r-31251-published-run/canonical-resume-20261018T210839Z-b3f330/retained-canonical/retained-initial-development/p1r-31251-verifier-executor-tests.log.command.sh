python3 - <<'PY'
from pathlib import Path
p=Path('tools/tests/p1r_isolation_fixture.py');s=p.read_text()
s=s.replace("            self.containers[identity] = value\n", '''            if role == 'sdk' and self.worker.source_root is not None and not self.worker.source_root.is_relative_to(self.worker.workspace):
                value['Mounts'].append({'Destination':str(self.worker.source_root),'Source':str(self.worker.source_root),'Type':'bind','RW':True})
            self.containers[identity] = value
''')
s=s.replace("    command = ['python3',str(preparation.ROOT/'tools/p1r-published-executor.py'),'run','--out',str(root),'--inputs',str(selected)]", "    source = context['source_binding'].get('candidate_source')\n    source_root = Path(source['repository']) if source is not None else None\n    command = ['python3',str(preparation.ROOT/'tools/p1r-published-executor.py'),'run','--out',str(root),'--inputs',str(selected)]\n    if source_root is not None:\n        command += ['--source-root', str(source_root)]")
s=s.replace("worker = isolation.IsolatedRunner(root/'wrapper',workspace=preparation.ROOT,outer_host='unix:///outer')", "worker = isolation.IsolatedRunner(root/'wrapper',workspace=preparation.ROOT,outer_host='unix:///outer',source_root=source_root)")
p.write_text(s)
p=Path('tools/tests/test_p1r_published_executor.py');s=p.read_text()
s=s.replace("class Published31251SnapshotWitnessTests(ProposedQualifiedSnapshotWitnessTests):\n    candidate_version = '3.125.1'\n", '''class Published31251SnapshotWitnessTests(ProposedQualifiedSnapshotWitnessTests):
    candidate_version = '3.125.1'

    def setUp(self):
        super().setUp()
        self.tagged_root = Path(self.temporary.name) / 'tagged-source'
        self.tagged_root.mkdir()
        self.test_binding = None
        original_binding,original_git = preparation.source_binding,preparation.git
        def bind(root=preparation.ROOT,source_root=None):
            if self.test_binding is not None and Path(root) == preparation.ROOT:
                return copy.deepcopy(self.test_binding)
            return original_binding(root,source_root) if source_root is not None else original_binding(root)
        def git(root,*arguments):
            if Path(root) == self.tagged_root:
                if arguments[0] == 'ls-tree':
                    return ('160000 commit '+qualification.SELECTED_TAG_BUILDS+'\\treferences/Hexalith.Builds\\n').encode()
                return qualification.SELECTED_SOURCE_COMMIT.encode()
            return original_git(root,*arguments)
        for patcher in (mock.patch.object(preparation,'source_binding',side_effect=bind),
                        mock.patch.object(preparation,'self_contained_checkout'),
                        mock.patch.object(preparation,'git',side_effect=git)):
            patcher.start()
            self.addCleanup(patcher.stop)

    def attach_isolation(self,receipt,context):
        binding = context['source_binding']
        production = {'head':qualification.SELECTED_SOURCE_COMMIT,'diff_sha256':preparation.digest(b''),
            'files':[{'path':'src/Production.cs','sha256':'a'*64,'git_blob':'b'*40,'index_blob':'b'*40}],
            'gitlinks':{'references/Hexalith.Builds':qualification.SELECTED_TAG_BUILDS}}
        binding['candidate_source'] = {'repository':str(self.tagged_root),'main':production,'retained_inputs':[],
            'python':binding['python'],'dependencies':[{'path':'references/Hexalith.Builds','initialized':True,
                'binding':dict(copy.deepcopy(production),head=qualification.SELECTED_TAG_BUILDS)}]}
        self.test_binding = binding
        context['inputs']['value']['candidate'].update(tag='v3.125.1',tag_commit=qualification.SELECTED_SOURCE_COMMIT,
            tag_builds_commit=qualification.SELECTED_TAG_BUILDS,feed='https://api.nuget.org/v3/index.json')
        source_path = Path(self.temporary.name)/'executor-source.json'
        executor.write_json(source_path,binding)
        receipt['execution_evidence'].update(executor_source=binding,executor_source_sha256=executor.digest(source_path.read_bytes()))
        receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
        return super().attach_isolation(receipt,context)
''')
# The legacy no-root negative control should reach its original exact-HEAD guard.
s=s.replace('        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"), \\\n             mock.patch.object(preparation, "git", side_effect=observations):\n            with self.assertRaisesRegex(preparation.InvalidPacket, "exact source"):', '        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"), \\\n             mock.patch.object(preparation, "source_binding", return_value={}), \\\n             mock.patch.object(qualification, "validate_candidate_source"), \\\n             mock.patch.object(preparation, "git", side_effect=observations):\n            with self.assertRaisesRegex(preparation.InvalidPacket, "exact source"):')
p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py' > /tmp/p1r-31251-verifier-executor-tests.log 2>&1