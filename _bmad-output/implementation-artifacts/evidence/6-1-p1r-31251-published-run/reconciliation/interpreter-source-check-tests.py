"""Real Git closure checks under an explicitly different reader interpreter."""
import copy
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'tools/tests'))
sys.path.insert(0, str(ROOT / 'tools'))
import p1r_qualification as p
from p1r_isolation_fixture import separate_source_fixture


class InterpreterSourceCheckTests(unittest.TestCase):
    def testExplicitInterpreterDiagnosticStillRechecksEveryPhysicalSourceRole(self):
        for mode in ('legacy', 'candidate', 'pinned'):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                if mode == 'candidate':
                    destination = f.source / 'references/Hexalith.Builds'
                    destination.parent.mkdir(parents=True)
                    subprocess.run(['git', 'clone', '--no-hardlinks', '--no-recurse-submodules', '-q',
                                    str(f.source_builds), str(destination)], check=True, capture_output=True)
                    binding = p.source_binding(f.tooling, f.source)
                elif mode == 'pinned':
                    binding = p.source_binding(f.tooling, f.pins, f.source, f.source_builds)
                else:
                    binding = p.source_binding(f.tooling)
                recorded = copy.deepcopy(binding)
                recorded['python']['version'] = [0, 0, 0]
                if mode == 'candidate':
                    recorded['candidate_source']['python']['version'] = [0, 0, 0]
                with self.assertRaises(p.InvalidPacket):
                    p.check_current_source(recorded)
                p.check_current_source(recorded, check_python=False)
                changed = f.tooling if mode == 'legacy' else f.source
                (changed / 'src/Runtime.cs').write_text('// physical drift after recorded binding\n')
                with self.assertRaises(p.InvalidPacket):
                    p.check_current_source(recorded, check_python=False)


if __name__ == '__main__':
    unittest.main()
