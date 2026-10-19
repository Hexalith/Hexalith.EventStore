python3 - <<'PY'
from pathlib import Path
p=Path('tools/tests/test_p1r_published_executor.py');s=p.read_text().replace("        self.tagged_root.mkdir()\n        self.test_binding = None", "        self.tagged_root.mkdir()\n        (self.tagged_root/'.gitmodules').write_text('[submodule \\\"Builds\\\"]\\n path = references/Hexalith.Builds\\n url = fixture-builds\\n')\n        self.test_binding = None")
p.write_text(s)
p=Path('tools/tests/test_p1r_isolation_hardening.py');s=p.read_text();pos=s.index('class LifecycleTests(')
s=s[:pos]+'''class TaggedSourceLifecycleTests(unittest.TestCase):
    def testIndependentImportRefusesRehashedRoleAndRootSubstitution(self):
        for record,mutation in (
            ('private',lambda value:value['source_binding']['candidate_source'].__setitem__('repository',str(preparation.ROOT))),
            ('wrapper',lambda value:value.__setitem__('source_root',str(preparation.ROOT))),
            ('wrapper',lambda value:value['controller_source_binding']['candidate_source']['main'].__setitem__('head','0'*40)),
            ('wrapper',lambda value:value['inner_command'].__setitem__(-1,str(preparation.ROOT))),
        ):
            witness = published_tests.Published31251SnapshotWitnessTests()
            witness.setUp()
            self.addCleanup(witness.doCleanups)
            receipt,context = witness.import_fixture(scope='operational')
            self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
            witness.change_isolation(receipt,record,mutation)
            with self.subTest(record=record),self.assertRaises(preparation.InvalidPacket):
                qualification.receipt_outcome(receipt,context)
            witness.doCleanups()


''' +s[pos:];p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py' > /tmp/p1r-31251-verifier-executor-tests-2.log 2>&1