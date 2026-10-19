"""Regression checks for the approved sibling-source lifecycle and final import."""
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'tools/tests'))
sys.path.insert(0, str(ROOT / 'tools'))
import p1r_isolation as isolation
import p1r_published_qualification as qualification
import p1r_qualification as preparation
import test_p1r_published_executor as published_tests


class SiblingTaggedSourceTests(published_tests.Published31251SnapshotWitnessTests):
    def testActualImportAcceptsSiblingTaggedSourceMountAndRejectsRehashedSubstitution(self):
        with tempfile.TemporaryDirectory(prefix='p1r-sibling-source-') as temporary:
            target = Path(temporary) / 'tagged-source'
            shutil.move(str(self.tagged_root), target)
            self.tagged_root = target
            receipt, context = self.import_fixture(scope='operational')
            self.assertEqual(qualification.receipt_outcome(receipt, context)[2]['execution'], 'passed')
            for mutation in ('mount', 'command'):
                receipt, context = self.import_fixture(scope='operational')
                if mutation == 'mount':
                    def change(value):
                        mount = next(row for row in value['runner_proof']['Mounts']
                                     if row['Destination'] == str(self.tagged_root))
                        mount['Destination'] = str(self.tagged_root.parent / 'substituted')
                    self.change_isolation(receipt, 'wrapper', change)
                else:
                    self.change_isolation(receipt, 'private',
                                          lambda value: value['command'].__setitem__(-1, str(preparation.ROOT)))
                    self.change_isolation(receipt, 'wrapper',
                                          lambda value: value['inner_command'].__setitem__(-1, str(preparation.ROOT)))
                with self.subTest(mutation=mutation), self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt, context)


class ControllerSourceOrderTests(unittest.TestCase):
    def testControllerRechecksSelectedSourceBeforeProvisioningAndAfterOuterCleanup(self):
        witness = published_tests.Published31251SnapshotWitnessTests()
        witness.setUp()
        self.addCleanup(witness.doCleanups)
        events = []
        check = preparation.check_current_source
        docker = isolation.IsolatedRunner.docker
        finalize = qualification.finalize_isolation_receipts
        def checked(binding, **kwargs):
            events.append(('source', kwargs))
            return check(binding, **kwargs)
        def called(worker, *args, **kwargs):
            events.append(('docker', None))
            return docker(worker, *args, **kwargs)
        def finalized(*args, **kwargs):
            events.append(('finalize', None))
            return finalize(*args, **kwargs)
        with mock.patch.object(preparation, 'check_current_source', side_effect=checked), \
                mock.patch.object(isolation.IsolatedRunner, 'docker', new=called), \
                mock.patch.object(qualification, 'finalize_isolation_receipts', side_effect=finalized):
            witness.import_fixture(scope='operational')
        self.assertEqual(events[0][0], 'source')
        final = next(index for index, value in enumerate(events) if value[0] == 'finalize')
        self.assertEqual(events[final - 1][0], 'source')
        self.assertTrue(any(value == ('source', {'check_python': False}) for value in events[final + 1:]))


if __name__ == '__main__':
    unittest.main()
