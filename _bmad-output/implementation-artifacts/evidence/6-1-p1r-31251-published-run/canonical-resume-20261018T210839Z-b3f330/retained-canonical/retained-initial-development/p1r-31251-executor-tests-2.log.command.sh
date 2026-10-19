python3 - <<'PY'
from pathlib import Path
p=Path('tools/tests/test_p1r_published_executor.py');s=p.read_text();pos=s.index("if __name__ == \"__main__\":") if 'if __name__ == "__main__":' in s else s.index("if __name__ == '__main__':")
s=s[:pos]+'''class PublishedPatchSourceTests(unittest.TestCase):
    """Real selection, wait and snapshot callers; Git observations are tooling fixtures."""

    def fixture(self):
        source_root = Path('/selected/tagged-source')
        clean = {'head': qualification.SELECTED_SOURCE_COMMIT, 'diff_sha256': preparation.digest(b''),
                 'files': [{'path': 'src/Production.cs', 'git_blob': 'a'*40,
                            'index_blob': 'a'*40, 'sha256': 'b'*64}],
                 'gitlinks': {'references/Hexalith.Builds': qualification.SELECTED_TAG_BUILDS}}
        builds = dict(copy.deepcopy(clean), head=qualification.SELECTED_TAG_BUILDS)
        source = {'repository': str(source_root), 'main': clean,
                  'dependencies': [{'path':'references/Hexalith.Builds','initialized':True,'binding':builds}]}
        binding = {'repository':str(preparation.ROOT), 'main':{'head':'f'*40}, 'candidate_source':source}
        candidate = {'version':'3.125.1','tag':'v3.125.1','tag_commit':qualification.SELECTED_SOURCE_COMMIT,
            'tag_builds_commit':qualification.SELECTED_TAG_BUILDS,
            'builds':{'version':'4.33.1','commit':'c'*40},'feed':'https://api.nuget.org/v3/index.json'}
        inputs = {'candidate':candidate,'rollback':None,'comparisons':[{'version':v} for v in qualification.COMPARISON_VERSIONS],
            'assertion_instrumentation':{'mechanism':executor.MECHANISM},'selected_additions':list(qualification.ADDITIONS),
            'operational_profile':{'runtime':'dapr','runtime_version':'1.18.2','backend':'state.postgresql','backend_image':executor.POSTGRES}}
        def git(root,*arguments):
            if Path(root) != source_root:
                return b'f'*40
            if arguments[0] == 'ls-tree':
                return ('160000 commit '+qualification.SELECTED_TAG_BUILDS+'\\treferences/Hexalith.Builds\\n').encode()
            return qualification.SELECTED_SOURCE_COMMIT.encode()
        return source_root,binding,inputs,git

    def testNewerVerifierCanSelectOnlyExactCleanTaggedProductionAndBuildClosure(self):
        source_root,binding,inputs,git = self.fixture()
        for mutation in (None,'head','builds','dirty','missing-builds','tag','feed'):
            changed = copy.deepcopy(binding)
            selected = copy.deepcopy(inputs)
            if mutation == 'head': changed['candidate_source']['main']['head'] = '0'*40
            if mutation == 'builds': changed['candidate_source']['dependencies'][0]['binding']['head'] = '0'*40
            if mutation == 'dirty': changed['candidate_source']['main']['diff_sha256'] = preparation.digest(b'dirty source')
            if mutation == 'missing-builds': changed['candidate_source']['dependencies'] = []
            if mutation == 'tag': selected['candidate']['tag_commit'] = '0'*40
            if mutation == 'feed': selected['candidate']['feed'] = 'https://substituted.invalid/v3/index.json'
            with self.subTest(mutation=mutation), mock.patch.object(qualification,'validate_inputs',return_value='owner-selected'), \\
                    mock.patch.object(preparation,'source_binding',return_value=changed) as bind, \\
                    mock.patch.object(preparation,'self_contained_checkout'), mock.patch.object(preparation,'git',side_effect=git):
                if mutation is None:
                    executor.validate_execution_inputs(selected,source_root)
                    bind.assert_called_once_with(executor.ROOT,source_root)
                else:
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.validate_execution_inputs(selected,source_root)

    def testMissingCandidateClosureCannotBeRehashedIntoAnImport(self):
        _,binding,inputs,_ = self.fixture()
        del binding['candidate_source']
        for binder in (qualification.bind_lane,qualification.bind_recovery):
            context = {'source_binding':binding,'inputs':{'value':inputs,'scope':'owner-selected'}}
            with self.subTest(binder=binder.__name__), self.assertRaisesRegex(preparation.InvalidPacket,'source closure missing'):
                if binder == qualification.bind_lane:
                    binder({'lane':'checkout'},{'scope':'operational'},context)
                else:
                    binder({}, {'scope':'operational'}, 'candidate-restore', context)

    def testReadyWaitRechecksSourceAfterReturningAndRefusesDrift(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.http = mock.Mock(return_value={'ready':True})
        worker.source_binding = {'repository':'/selected/verifier','candidate_source':{'repository':'/selected/source'}}
        worker.inputs = None
        item = {'process':mock.Mock()}
        item['process'].poll.return_value = None
        with mock.patch.object(preparation,'check_current_source',side_effect=preparation.InvalidPacket('post-wait drift')) as check:
            with self.assertRaisesRegex(preparation.InvalidPacket,'post-wait drift'):
                worker.wait('http://127.0.0.1:12345/ready',item)
        worker.http.assert_called_once()
        check.assert_called_once_with(worker.source_binding)

    def testSelectedPatchRequiresEveryGenuineSnapshotWitness(self):
        helper = QualifiedSnapshotWitnessTests()
        helper.candidate_version = '3.125.1'
        helper.setUp()
        self.addCleanup(helper.doCleanups)
        for lane in ('post-upgrade-restore','snapshot-tail','retained-covered','retained-uncovered','metadata-write'):
            for writer in (('3.125.1',) if lane == 'post-upgrade-restore' else ('3.125.1','source')):
                with self.subTest(lane=lane,writer=writer):
                    fixture = helper.fixture(writer_version=writer,lane=lane)
                    helper.validate(fixture,lane)
                    del fixture[1]['snapshot_fixture_source']
                    with self.assertRaises(preparation.InvalidPacket):
                        helper.validate(fixture,lane)
        self.assertIn('3.125.1',qualification.ISOLATION_VERSIONS)
        self.assertFalse(executor.witnesses.requires_qualified_snapshot('3.126.0','snapshot-tail','source-to-3.110.0'))


''' +s[pos:];p.write_text(s)
p=Path('tools/tests/test_p1r_isolation.py');s=p.read_text();pos=s.index('class ControllerTests(')
s=s[:pos]+'''class SeparateSourceMountTests(unittest.TestCase):
    def testExactSourceArgumentAndMountAreIndependentOfVerifierRoot(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            verifier,source = base/'verifier',base/'source'
            verifier.mkdir();source.mkdir()
            inputs = verifier/'inputs.json';inputs.write_text('{}')
            command = ['python3',str(verifier/'tools/p1r-published-executor.py'),'run','--out',str(verifier/'run'),
                       '--inputs',str(inputs),'--source-root',str(source)]
            self.assertEqual(isolation.command_source_root(command),source)
            self.assertEqual(isolation.executor_input(command,verifier),{'path':str(inputs),'sha256':preparation.digest(b'{}')})
            with mock.patch.object(preparation,'declared_dependencies',return_value=['references/Hexalith.Builds']):
                args = isolation.sdk_arguments(verifier,verifier,'a'*32,'daemon','volume',source)
            self.assertIn('type=bind,src='+str(source)+',dst='+str(source),args)
            self.assertIn('type=bind,src='+str(verifier)+',dst='+str(verifier),args)
            for changed in (command+['--source-root',str(verifier)],command[:-1]+[str(source/'..')],
                            command[:7]+['--inputs',str(inputs)]):
                with self.subTest(command=changed), self.assertRaises(preparation.InvalidPacket):
                    isolation.executor_input(changed,verifier)

    def testOrdinaryCheckoutBoundaryRefusesLinkedAndBorrowedMetadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'.git/objects/info').mkdir(parents=True)
            with mock.patch.object(preparation,'declared_dependencies',return_value=[]), \\
                    mock.patch.object(preparation,'git',return_value=str(root/'.git').encode()):
                preparation.self_contained_checkout(root)
                (root/'.git/objects/info/alternates').write_text('/shared/objects')
                with self.assertRaisesRegex(preparation.InvalidPacket,'alternates'):
                    preparation.self_contained_checkout(root)
            shutil.rmtree(root/'.git')
            (root/'.git').write_text('gitdir: /shared/worktrees/candidate')
            with self.assertRaisesRegex(preparation.InvalidPacket,'ordinary Git'):
                preparation.self_contained_checkout(root)


''' +s[pos:];p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py' > /tmp/p1r-31251-executor-tests-2.log 2>&1